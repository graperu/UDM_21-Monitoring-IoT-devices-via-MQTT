
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UDM_21.Dashboard.Controls
{
    public partial class TrendChartControl : UserControl
    {
        private static readonly SolidColorBrush GridBrush = new(Color.FromRgb(0xE5, 0xE7, 0xEB));
        private static readonly SolidColorBrush AxisTextBrush = new(Color.FromRgb(0x64, 0x74, 0x8B));
        private static readonly SolidColorBrush ThresholdBrush = new(Color.FromRgb(0xDC, 0x26, 0x26));

        private List<(DateTime Timestamp, double Value)> _points = new();
        private Brush _lineBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xCC));
        private double? _thresholdValue;
        private string _thresholdLabel = string.Empty;

        public TrendChartControl()
        {
            InitializeComponent();
        }

        public void SetSeries(
            string title,
            string unit,
            Brush lineColor,
            IReadOnlyList<(DateTime Timestamp, double Value)> points,
            double? thresholdValue = null,
            string? thresholdLabel = null)
        {
            TxtChartTitle.Text = title;
            TxtLatestUnit.Text = unit;
            DotSeriesColor.Fill = lineColor;
            _lineBrush = lineColor;
            _points = points?.OrderBy(p => p.Timestamp).ToList() ?? new List<(DateTime, double)>();
            _thresholdValue = thresholdValue;
            _thresholdLabel = thresholdLabel ?? string.Empty;

            UpdateStats();
            Redraw();
        }

        private void UpdateStats()
        {
            if (_points.Count == 0)
            {
                TxtLatestValue.Text = "--";
                TxtMinValue.Text = "--";
                TxtAvgValue.Text = "--";
                TxtMaxValue.Text = "--";
                TxtPointCount.Text = "0 điểm";
                return;
            }

            var values = _points.Select(p => p.Value).ToList();
            TxtLatestValue.Text = FormatValue(values[^1]);
            TxtMinValue.Text = FormatValue(values.Min());
            TxtAvgValue.Text = FormatValue(values.Average());
            TxtMaxValue.Text = FormatValue(values.Max());
            TxtPointCount.Text = $"{_points.Count} điểm";
        }

        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void Redraw()
        {
            ChartCanvas.Children.Clear();

            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;
            if (width <= 1 || height <= 1) return;

            if (_points.Count == 0)
            {
                TxtNoData.Visibility = Visibility.Visible;
                return;
            }
            TxtNoData.Visibility = Visibility.Collapsed;

            double minV = _points.Min(p => p.Value);
            double maxV = _points.Max(p => p.Value);
            if (_thresholdValue.HasValue)
            {
                minV = Math.Min(minV, _thresholdValue.Value);
                maxV = Math.Max(maxV, _thresholdValue.Value);
            }
            if (Math.Abs(maxV - minV) < 0.0001)
            {
                maxV += 1;
                minV -= 1;
            }
            double range = maxV - minV;
            double vPad = range * 0.15;
            minV -= vPad;
            maxV += vPad;
            range = maxV - minV;

            const double leftMargin = 48;
            const double rightMargin = 4;
            const double topMargin = 4;
            const double bottomMargin = 22;
            double plotWidth = Math.Max(10, width - leftMargin - rightMargin);
            double plotHeight = Math.Max(10, height - topMargin - bottomMargin);

            double duration = (_points[^1].Timestamp - _points[0].Timestamp).TotalMilliseconds;
            double MapX(int index) => duration <= 0
                ? leftMargin + plotWidth / 2
                : leftMargin + plotWidth *
                    (_points[index].Timestamp - _points[0].Timestamp).TotalMilliseconds / duration;

            double MapY(double value) => topMargin + plotHeight - (value - minV) / range * plotHeight;

            for (int i = 0; i <= 4; i++)
            {
                double y = topMargin + plotHeight * i / 4.0;
                var gridLine = new Line
                {
                    X1 = leftMargin,
                    X2 = leftMargin + plotWidth,
                    Y1 = y,
                    Y2 = y,
                    Stroke = GridBrush,
                    StrokeThickness = 1
                };
                ChartCanvas.Children.Add(gridLine);

                double axisValue = maxV - range * i / 4.0;
                var axisLabel = new TextBlock
                {
                    Text = FormatValue(axisValue),
                    FontSize = 11,
                    Foreground = AxisTextBrush
                };
                Canvas.SetLeft(axisLabel, 0);
                Canvas.SetTop(axisLabel, Math.Max(0, y - 6));
                ChartCanvas.Children.Add(axisLabel);
            }

            if (_thresholdValue.HasValue && _thresholdValue.Value >= minV && _thresholdValue.Value <= maxV)
            {
                double ty = MapY(_thresholdValue.Value);
                var thresholdLine = new Line
                {
                    X1 = leftMargin,
                    X2 = leftMargin + plotWidth,
                    Y1 = ty,
                    Y2 = ty,
                    Stroke = ThresholdBrush,
                    StrokeThickness = 1.3,
                    StrokeDashArray = new DoubleCollection { 4, 3 }
                };
                ChartCanvas.Children.Add(thresholdLine);

                if (!string.IsNullOrEmpty(_thresholdLabel))
                {
                    var thresholdText = new TextBlock
                    {
                        Text = _thresholdLabel,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThresholdBrush
                    };
                    Canvas.SetLeft(thresholdText, Math.Max(leftMargin, leftMargin + plotWidth - 110));
                    Canvas.SetTop(thresholdText, Math.Max(0, ty - 12));
                    ChartCanvas.Children.Add(thresholdText);
                }
            }

            var fillPoints = new PointCollection { new Point(MapX(0), topMargin + plotHeight) };
            for (int i = 0; i < _points.Count; i++)
                fillPoints.Add(new Point(MapX(i), MapY(_points[i].Value)));
            fillPoints.Add(new Point(MapX(_points.Count - 1), topMargin + plotHeight));

            var baseColor = _lineBrush is SolidColorBrush scb ? scb.Color : Colors.SteelBlue;
            var fillColor = Color.FromArgb(38, baseColor.R, baseColor.G, baseColor.B);
            var fillPolygon = new Polygon { Points = fillPoints, Fill = new SolidColorBrush(fillColor) };
            ChartCanvas.Children.Add(fillPolygon);

            var linePoints = new PointCollection();
            for (int i = 0; i < _points.Count; i++)
                linePoints.Add(new Point(MapX(i), MapY(_points[i].Value)));

            var polyline = new Polyline
            {
                Points = linePoints,
                Stroke = _lineBrush,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            ChartCanvas.Children.Add(polyline);

            if (_points.Count <= 200)
            {
                for (int i = 0; i < _points.Count; i++)
                {
                    var dot = new Ellipse
                    {
                        Width = 5,
                        Height = 5,
                        Fill = Brushes.White,
                        Stroke = _lineBrush,
                        StrokeThickness = 1.4,
                        ToolTip = $"{_points[i].Timestamp:dd/MM/yyyy HH:mm:ss}\n{_points[i].Value:0.###} {TxtLatestUnit.Text}"
                    };
                    Canvas.SetLeft(dot, MapX(i) - 2.5);
                    Canvas.SetTop(dot, MapY(_points[i].Value) - 2.5);
                    ChartCanvas.Children.Add(dot);
                }
            }

            int labelCount = Math.Min(4, _points.Count);
            for (int k = 0; k < labelCount; k++)
            {
                int idx = labelCount == 1 ? 0 : (int)Math.Round(k * (double)(_points.Count - 1) / (labelCount - 1));
                var timeLabel = new TextBlock
                {
                    Text = _points[idx].Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    FontSize = 10,
                    Foreground = AxisTextBrush
                };
                double lx = Math.Max(leftMargin - 4, Math.Min(MapX(idx) - 16, leftMargin + plotWidth - 30));
                Canvas.SetLeft(timeLabel, lx);
                Canvas.SetTop(timeLabel, topMargin + plotHeight + 2);
                ChartCanvas.Children.Add(timeLabel);
            }
        }

        private static string FormatValue(double value)
        {
            return Math.Abs(value) >= 100
                ? value.ToString("F0", CultureInfo.InvariantCulture)
                : value.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
