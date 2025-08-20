using Resto.Front.Api;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace IikoWindows;

/// <summary>
/// Класс работы со своими окнами в iiko.<br/> В .csproj обязательно добавьте<br/>&lt;UseWPF&gt;true&lt;/UseWPF&gt;
/// </summary>
public static class Helpers
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    private const string iikoName = "iikoFront.Net";
    private const string syrveName = "syrveFront.Net";
    private const IntPtr _frontHwnd;

    /// <summary>
    /// Инициализация визуального окна. Делается в конце метода инициализации вызываемого класса плагина
    /// </summary>
    public static void InitializeUiDispatcher()
    {
        if (Application.Current != null)
            return;

        var runningProcesses = Process.GetProcessesByName(iikoName).SingleOrDefault();
        if (runningProcesses == null)
        {
            runningProcesses = Process.GetProcessesByName(syrveName).SingleOrDefault();
            if (runningProcesses == null)
            {
                throw new Exception("Unable to find front process. Ensure that the application is running.");
            }
        }

        _frontHwnd = runningProcesses.MainWindowHandle;

        using var uiThreadStartedSignal = new ManualResetEvent(false);
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            StartupEventHandler startupHandler = null;
            startupHandler = (s, e) =>
            {
                // ReSharper disable once AccessToDisposedClosure (вызывающий поток не покинет блок using, пока мы вызовем Set)
                uiThreadStartedSignal.Set();
                app.Startup -= startupHandler;
            };
            app.Startup += startupHandler;
            app.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        uiThreadStartedSignal.WaitOne();

    }

    /// <summary>
    /// Метод отключения инициализации окна. Делается методе Dispose вызываемого класса плагина
    /// </summary>
    public static void ShutdownUiDispatcher()
    {
        Application.Current.Dispatcher.InvokeShutdown();
        Application.Current.Dispatcher.Thread.Join();
    }


    /// <summary>
    /// Метод отображения WPF окна. В классе окна <b>TWindow</b> необходимо создать метод <b>AddExternalProperties</b> с получаемыми аргументами <b>TArgs args</b>
    /// </summary>
    /// <typeparam name="TWindow">Класс WPF окна</typeparam>
    /// <typeparam name="TRequest">Тип передаваемых аргументов</typeparam>
    /// <typeparam name="TResult">Тип возвращаемых данных</typeparam>
    /// <param name="args">передаваемые данные</param>
    /// <returns>Возвращает данные <b>TResult</b></returns>
    public static TResult ShowWindow<TWindow, TRequest, TResult>(TRequest args)
        where TWindow : System.Windows.Window, new()
        where TRequest : class
        where TResult : class
    {
        var source = new CancellationTokenSource();
        TWindow window = null;
        TResult result = null;
        PluginContext.Log.Info($"ShowWindow.{typeof(TWindow).Name} :: External screen opened.");

        var t = Task.Run(() =>
        {
            window = Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    PluginContext.Log.Info($"ShowWindow.{typeof(TWindow).Name}.Invoke :: External screen opened.");
                    window = new TWindow();

                    var windowHandle = new WindowInteropHelper(window).EnsureHandle();
                    // Устанавливаем родителя
          

                    var argsMethod = typeof(TWindow).GetMethod("AddExternalProperties");
                    if (argsMethod is not null)
                    {
                        if (args is not null)
                        {
                            argsMethod.Invoke(window, new object[] { args });
                        }
                        else
                        {
                            argsMethod.Invoke(window, null);
                            PluginContext.Log.Warn(
                                $"ShowWindow.{typeof(TWindow).Name} :: AddExternalProperties {nameof(TRequest)} args is null.");
                        }
                    }
                    else
                    {
                        PluginContext.Log.Warn(
                            $"ShowWindow.{typeof(TWindow).Name} :: Doesn't have AddExternalProperties method.");
                    }
                    SetParent(windowHandle, _frontHwnd);
                    EnableWindow(_frontHwnd, false);
                    
                    window.Closed += (s, e) =>
                    {
                        // Разблокируем родительское окно
                        EnableWindow(_frontHwnd, true);
                    };


                    window.ShowDialog();

                    source.Cancel();
                    PluginContext.Log.Info($"ShowWindow.{typeof(TWindow).Name}.Invoke :: External screen closed.");
                }
                catch (Exception ex)
                {
                    PluginContext.Log.Error($"ShowWindow.{typeof(TWindow).Name} ::{ex.Message}", ex);
                }

                return window;
            });
            var resultArgs = window?.GetType()?.GetField("Result");
            if (resultArgs is not null)
            {
                result = window.Dispatcher.Invoke(()=>(TResult)resultArgs.GetValue(window));
                if (result is null)
                {
                    PluginContext.Log.Warn(
                        $"ShowWindow.{typeof(TWindow).Name} :: Result {nameof(TResult)}  is null.");
                }
            }
            else
            {
                var resultProperty = window?.GetType()?.GetProperty("Result");
                if (resultProperty is not null)
                {
                    result = window.Dispatcher.Invoke(() => (TResult)resultProperty.GetValue(window));
                    if (result is null)
                    {
                        PluginContext.Log.Warn(
                            $"ShowWindow.{typeof(TWindow).Name} :: Result {nameof(TResult)} is null.");
                    }
                }
                else
                {
                    PluginContext.Log.Warn($"ShowWindow.{typeof(TWindow).Name} :: Doesn't have Result data.");
                }

            }

            PluginContext.Log.Info($"ShowWindow.{typeof(TWindow).Name} :: External screen closed.");
            return result;
        }, source.Token);

        // t.Start();
        var x = t.ConfigureAwait(false).GetAwaiter().GetResult();


        return x;
    }


    public static TResult ShowWindowAsync<TWindow, TRequest, TResult>(TRequest args)
      where TWindow : System.Windows.Window, new()
      where TRequest : class
      where TResult : class
    {
        var source = new CancellationTokenSource();
        TWindow window = null;
        TResult result = null;
        PluginContext.Log.Info($"ShowWindowAsync.{typeof(TWindow).Name} :: External screen opened.");

        var t = Task.Run(() =>
        {
            window = Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    PluginContext.Log.Info($"ShowWindowAsync.{typeof(TWindow).Name}.Invoke :: External screen opened.");
                    window = new TWindow();
                    var argsMethod = typeof(TWindow).GetMethod("AddExternalProperties");
                    if (argsMethod is not null)
                    {
                        if (args is not null)
                        {
                            argsMethod.Invoke(window, new object[] { args });
                        }
                        else
                        {
                            argsMethod.Invoke(window, null);
                            PluginContext.Log.Warn(
                                $"ShowWindowAsync.{typeof(TWindow).Name} :: AddExternalProperties {nameof(TRequest)} args is null.");
                        }
                    }
                    else
                    {
                        PluginContext.Log.Warn(
                            $"ShowWindowAsync.{typeof(TWindow).Name} :: Doesn't have AddExternalProperties method.");
                    }

                    window.Show();

                    source.Cancel();
                    PluginContext.Log.Info($"ShowWindowAsync.{typeof(TWindow).Name}.Invoke :: External screen closed.");
                }
                catch (Exception ex)
                {
                    PluginContext.Log.Error($"ShowWindowAsync.{typeof(TWindow).Name} ::{ex.Message}", ex);
                }

                return window;
            });
            var resultArgs = window?.GetType()?.GetField("Result");
            if (resultArgs is not null)
            {
                result = window.Dispatcher.Invoke(() => (TResult)resultArgs.GetValue(window));
                if (result is null)
                {
                    PluginContext.Log.Warn(
                        $"ShowWindowAsync.{typeof(TWindow).Name} :: Result {nameof(TResult)}  is null.");
                }
            }
            else
            {

                var resultProperty = window?.GetType()?.GetProperty("Result");
                if (resultProperty is not null)
                {
                    result = window.Dispatcher.Invoke(() => (TResult)resultProperty.GetValue(window));
                    if (result is null)
                    {
                        PluginContext.Log.Warn(
                            $"ShowWindowAsync.{typeof(TWindow).Name} :: Result {nameof(TResult)} is null.");
                    }
                }
                else
                {
                    PluginContext.Log.Warn($"ShowWindowAsync.{typeof(TWindow).Name} :: Doesn't have Result data.");
                }
            }

            PluginContext.Log.Info($"ShowWindowAsync.{typeof(TWindow).Name} :: External screen closed.");
            return result;
        }, source.Token);

        // t.Start();
        var x = t.ConfigureAwait(false).GetAwaiter().GetResult();


        return x;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

    private const int MOUSEEVENTF_LEFTDOWN = 0x02;

    private const int MOUSEEVENTF_LEFTUP = 0x04;

    //This simulates a left mouse click
    public static void LeftMouseClick(int xpos, int ypos)
    {
        SetCursorPos(xpos, ypos);
        mouse_event(MOUSEEVENTF_LEFTDOWN, xpos, ypos, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, xpos, ypos, 0, 0);
    }


    public static TResponse HandleInput<TResponse>(this Window window, Func<TResponse> func)
    {
        PluginContext.Log.Info("Helpers.HandleInput.Func :: started");
        TResponse response = default;
        if (window is null)
            return response;
        try
        {
            window.Topmost = false;
            //window.Hide();
            window.WindowState = WindowState.Minimized;

            response = func();
        }
        catch (Exception ex)
        {
            PluginContext.Log.Error($"Helpers.HandleInput.Func :: {ex.Message}", ex);
        }
        finally
        {
            window.WindowState = WindowState.Normal;
            window.Topmost = true;
            window.Focusable = true;
            Keyboard.Focus(window);


            //window.Focus();
            //window.Activate();
            //window.BringIntoView();
            ////window.ShowActivated = true;
            //window.ShowDialog();
        }

        PluginContext.Log.Info("Helpers.HandleInput.Func :: finished");

        return response;
    }

    public static void HandleInput(this Window window, Action action)
    {
        PluginContext.Log.Info("Helpers.HandleInput.Action :: started");
        window.WindowState = WindowState.Minimized;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            PluginContext.Log.Error($"Helpers.HandleInput.Action :: {ex.Message}", ex);
        }
        finally
        {
            window.WindowState = WindowState.Normal;
        }

        PluginContext.Log.Info("Helpers.HandleInput.Action :: finished");
    }
}
