namespace WindowExternalHelper
{
    /// <summary>
    /// Класс для работы с окнами в iiko.
    /// </summary>
    public static class Helpers
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        private const string AppProcessName = "iikoFront.Net";

        /// <summary>
        /// Инициализация диспетчера WPF приложения.
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
        /// Завершение работы диспетчера WPF приложения.
        /// </summary>
        public static void ShutdownUiDispatcher()
        {
            if (Application.Current == null) return;

            Application.Current.Dispatcher.InvokeShutdown();
            Application.Current.Dispatcher.Thread.Join();
        }

        /// <summary>
        /// Показ WPF окна с возможностью передачи аргументов и получения результата.
        /// </summary>
        public static TResult ShowWindow<TWindow, TRequest, TResult>(TRequest args)
            where TWindow : Window, new()
            where TRequest : class
            where TResult : class
        {
            TResult result = null;
            try
            {
                PluginContext.Log.Info($"ShowWindow: Открытие окна {typeof(TWindow).Name}");

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
                            PluginContext.Log.Warn($"Метод AddExternalProperties не найден в {typeof(TWindow).Name}.");
                        }

                        window.ShowDialog();

                        var resultField = typeof(TWindow).GetField("Result");
                        if (resultField != null)
                        {
                            result = resultField.GetValue(window) as TResult;
                        }
                        else
                        {
                            PluginContext.Log.Warn($"Поле Result отсутствует в {typeof(TWindow).Name}.");
                        }
                    });
                }).Wait();
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error($"Ошибка при открытии окна {typeof(TWindow).Name}: {ex.Message}", ex);
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
        /// Симуляция клика левой кнопкой мыши.
        /// </summary>
        public static void LeftMouseClick(int xpos, int ypos)
        {
            SetCursorPos(xpos, ypos);
            mouse_event(MOUSEEVENTF_LEFTDOWN, xpos, ypos, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, xpos, ypos, 0, 0);
        }

        /// <summary>
        /// Обработка ввода с возможностью возврата результата.
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
                PluginContext.Log.Error($"Ошибка при обработке ввода: {ex.Message}", ex);
            }
            finally
            {
                RestoreWindow(window);
            }

            return response;
        }

        /// <summary>
        /// Обработка ввода без возврата результата.
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
                PluginContext.Log.Error($"Ошибка при обработке ввода: {ex.Message}", ex);
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
