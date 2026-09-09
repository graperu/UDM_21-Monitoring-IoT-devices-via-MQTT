using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UDM_21.Shared;

namespace UDM_21.Dashboard
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private static void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Error("UI_UNHANDLED_EXCEPTION", e.Exception);
            MessageBox.Show(
                $"Ứng dụng đã xử lý một lỗi không mong đợi: {e.Exception.Message}",
                "Lỗi ứng dụng",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            AppLogger.Error("TASK_UNOBSERVED_EXCEPTION", e.Exception);
            e.SetObserved();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                AppLogger.Error("PROCESS_UNHANDLED_EXCEPTION", exception);
            }
        }
    }
}
