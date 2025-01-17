using Resto.Front.Api;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Windows;
using System;
// To Syrve
namespace WindowExternalHelper
{
    /// <summary>
    /// Class for Managing Windows.
    /// </summary>
    public static class Helpers
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        /// <summary>
        /// Initializing the Dispatcher of a WPF Application.
        /// </summary>
        public static void InitializeUiDispatcher()
        {
            if (Application.Current != null) return;

            using var uiThreadStartedSignal = new ManualResetEvent(false);
            var thread = new Thread(() =>
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Startup += (_, _) => uiThreadStartedSignal.Set();
                app.Run();
            })
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            uiThreadStartedSignal.WaitOne();
        }

        /// <summary>
        /// Shutting Down the Dispatcher of a WPF Application.
        /// </summary>
        public static void ShutdownUiDispatcher()
        {
            if (Application.Current == null) return;

            Application.Current.Dispatcher.InvokeShutdown();
            Application.Current.Dispatcher.Thread.Join();
        }

        /// <summary>
        /// Displaying a WPF Window with the Ability to Pass Arguments and Receive a Result.
        /// </summary>
        public static TResult ShowWindow<TWindow, TRequest, TResult>(TRequest args)
            where TWindow : Window, new()
            where TRequest : class
            where TResult : class
        {
            TResult result = null;
            try
            {
                PluginContext.Log.Info($"ShowWindow: Opening window {typeof(TWindow).Name}");

                Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var window = new TWindow();
                        var argsMethod = typeof(TWindow).GetMethod("AddExternalProperties");

                        if (argsMethod != null)
                        {
                            argsMethod.Invoke(window, new object[] { args });
                        }
                        else
                        {
                            PluginContext.Log.Warn($"Method AddExternalProperties not found in {typeof(TWindow).Name}.");
                        }

                        window.ShowDialog();

                        var resultField = typeof(TWindow).GetField("Result");
                        if (resultField != null)
                        {
                            result = resultField.GetValue(window) as TResult;
                        }
                        else
                        {
                            PluginContext.Log.Warn($"Field Result is missing in {typeof(TWindow).Name}.");
                        }
                    });
                }).Wait();
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error($"Error while opening window {typeof(TWindow).Name}: {ex.Message}", ex);
            }

            return result;
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;

        /// <summary>
        /// Simulating a Left Mouse Click.
        /// </summary>
        public static void LeftMouseClick(int xpos, int ypos)
        {
            SetCursorPos(xpos, ypos);
            mouse_event(MOUSEEVENTF_LEFTDOWN, xpos, ypos, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, xpos, ypos, 0, 0);
        }

        /// <summary>
        /// Handling Input with the Ability to Return a Result.
        /// </summary>
        public static TResponse HandleInput<TResponse>(this Window window, Func<TResponse> func)
        {
            TResponse response = default;
            if (window == null) return response;

            try
            {
                window.WindowState = WindowState.Minimized;

                response = func();
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error($"Error while handling input: {ex.Message}", ex);
            }
            finally
            {
                RestoreWindow(window);
            }

            return response;
        }

        /// <summary>
        /// Handling Input Without Returning a Result.
        /// </summary>
        public static void HandleInput(this Window window, Action action)
        {
            if (window == null) return;

            try
            {
                window.WindowState = WindowState.Minimized;

                action();
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error($"Error while handling input: {ex.Message}", ex);
            }
            finally
            {
                RestoreWindow(window);
            }
        }

        private static void RestoreWindow(Window window)
        {
            if (window == null) return;

            window.WindowState = WindowState.Normal;
            window.Topmost = true;
            window.Focusable = true;
            Keyboard.Focus(window);
        }
    }
}
