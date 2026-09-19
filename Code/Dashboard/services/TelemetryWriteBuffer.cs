using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UDM_21.Shared;

namespace UDM_21.Dashboard.Services
{

    public sealed record TelemetryWriteBufferStats(
        long Enqueued,
        long Written,
        long Dropped,
        long Pending,
        long BatchCount,
        int LargestBatch,
        double AverageBatchSize,
        double TotalWriteMilliseconds);

    public sealed class TelemetryWriteBuffer : IAsyncDisposable
    {
        public const int DefaultCapacity = 20_000;
        public const int DefaultBatchSize = 256;
        public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromMilliseconds(400);

        private readonly TelemetryHistoryManager _history;
        private readonly Channel<TelemetryMessage> _channel;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _worker;
        private readonly int _capacity;
        private readonly int _batchSize;
        private readonly TimeSpan _flushInterval;

        private long _enqueued;
        private long _written;
        private long _dropped;
        private long _pending;
        private long _batchCount;
        private long _totalWriteTicks;
        private int _largestBatch;
        private volatile bool _flushRequested;
        private int _disposed;

        public TelemetryWriteBuffer(
            TelemetryHistoryManager history,
            int capacity = DefaultCapacity,
            int batchSize = DefaultBatchSize,
            TimeSpan? flushInterval = null)
        {
            ArgumentNullException.ThrowIfNull(history);
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "capacity phải >= 1.");
            if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), "batchSize phải >= 1.");

            _history = history;
            _capacity = capacity;
            _batchSize = batchSize;
            _flushInterval = flushInterval ?? DefaultFlushInterval;
            _channel = Channel.CreateUnbounded<TelemetryMessage>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            _worker = Task.Run(() => RunAsync(_cts.Token));
            AppLogger.Info(
                "HISTORY_WRITE_BUFFER_STARTED",
                $"capacity={_capacity}; batch_size={_batchSize}; flush_ms={_flushInterval.TotalMilliseconds:F0}");
        }

        public TelemetryWriteBufferStats Stats
        {
            get
            {
                var batches = Interlocked.Read(ref _batchCount);
                var written = Interlocked.Read(ref _written);
                return new TelemetryWriteBufferStats(
                    Enqueued: Interlocked.Read(ref _enqueued),
                    Written: written,
                    Dropped: Interlocked.Read(ref _dropped),
                    Pending: Interlocked.Read(ref _pending),
                    BatchCount: batches,
                    LargestBatch: Volatile.Read(ref _largestBatch),
                    AverageBatchSize: batches == 0 ? 0 : (double)written / batches,
                    TotalWriteMilliseconds: TimeSpan.FromTicks(Interlocked.Read(ref _totalWriteTicks)).TotalMilliseconds);
            }
        }

        public bool Enqueue(TelemetryMessage? message)
        {
            if (message == null || Volatile.Read(ref _disposed) != 0) return false;

            if (Interlocked.Read(ref _pending) >= _capacity)
            {
                Interlocked.Increment(ref _dropped);
                return false;
            }

            if (!_channel.Writer.TryWrite(message))
            {
                Interlocked.Increment(ref _dropped);
                return false;
            }

            Interlocked.Increment(ref _pending);
            Interlocked.Increment(ref _enqueued);
            return true;
        }

        public async Task FlushAsync(TimeSpan? timeout = null)
        {
            _flushRequested = true;
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
            while (Interlocked.Read(ref _pending) > 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(15).ConfigureAwait(false);
            }

            _history.PrunePendingDevices();
        }

        private async Task RunAsync(CancellationToken token)
        {
            var buffer = new List<TelemetryMessage>(_batchSize);
            try
            {
                while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                {

                    await WaitForBatchWindowAsync(token).ConfigureAwait(false);
                    DrainAndWrite(buffer);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (ChannelClosedException)
            {

            }
            catch (Exception ex)
            {
                AppLogger.Error("HISTORY_WRITE_BUFFER_CRASHED", ex);
            }
            finally
            {
                DrainAndWrite(buffer);
                _history.PrunePendingDevices();
            }
        }

        private async Task WaitForBatchWindowAsync(CancellationToken token)
        {
            var slice = TimeSpan.FromMilliseconds(Math.Min(25, Math.Max(1, _flushInterval.TotalMilliseconds)));
            var deadline = DateTime.UtcNow + _flushInterval;

            while (!_flushRequested && !token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (Interlocked.Read(ref _pending) >= _batchSize) return;

                try
                {
                    await Task.Delay(slice, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void DrainAndWrite(List<TelemetryMessage> buffer)
        {
            while (true)
            {
                buffer.Clear();
                while (buffer.Count < _batchSize && _channel.Reader.TryRead(out var message))
                {
                    buffer.Add(message);
                }

                if (buffer.Count == 0) break;

                var stopwatch = Stopwatch.StartNew();
                int written;
                try
                {
                    written = _history.AddTelemetryBatch(buffer);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("HISTORY_WRITE_BATCH_FAILED", ex);
                    written = 0;
                }
                stopwatch.Stop();

                Interlocked.Add(ref _pending, -buffer.Count);
                Interlocked.Add(ref _written, written);
                Interlocked.Increment(ref _batchCount);
                Interlocked.Add(ref _totalWriteTicks, stopwatch.Elapsed.Ticks);

                var currentLargest = Volatile.Read(ref _largestBatch);
                if (buffer.Count > currentLargest)
                {
                    Interlocked.CompareExchange(ref _largestBatch, buffer.Count, currentLargest);
                }
            }

            _flushRequested = false;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _channel.Writer.TryComplete();
            try
            {
                await FlushAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await _worker.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                AppLogger.Warning("HISTORY_WRITE_BUFFER_TIMEOUT", $"pending={Interlocked.Read(ref _pending)}");
            }
            catch (Exception ex)
            {
                AppLogger.Error("HISTORY_WRITE_BUFFER_DISPOSE_FAILED", ex);
            }

            _cts.Cancel();
            try
            {
                await _worker.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception)
            {

            }

            _cts.Dispose();

            var stats = Stats;
            AppLogger.Info(
                "HISTORY_WRITE_BUFFER_STOPPED",
                $"enqueued={stats.Enqueued}; written={stats.Written}; dropped={stats.Dropped}; " +
                $"batches={stats.BatchCount}; avg_batch={stats.AverageBatchSize:F1}; " +
                $"largest_batch={stats.LargestBatch}; total_write_ms={stats.TotalWriteMilliseconds:F0}");
        }
    }
}
