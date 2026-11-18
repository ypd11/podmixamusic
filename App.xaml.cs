using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using WindowsShuffle.Classes;

namespace WindowsShuffle
{
    public partial class App : Application
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEX versionInfo);

        private static Mutex _mutex = null;
        private const string AppGuid = "Podmix-SingleInstance-Guid";

        private static int _dispatcherCrashCount;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Install crash handlers as early as possible so nothing disappears silently.
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            _mutex = new Mutex(true, AppGuid, out bool createdNew);

            if (!createdNew)
            {
                // App is already running. Send the file path to the existing instance.
                if (e.Args.Length > 0)
                {
                    SendArgumentToRunningInstance(e.Args[0]);
                }
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            EnableModernTls();
            FfmpegBootstrapper.EnsureAvailableInBackground();
            if (IsWindows10OrLater())
            {
                RegisterToastNotifications();
            }
            StartArgumentListener();
        }

        private static string CrashLogPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "crash.log");

        internal static void AppendCrashLog(string source, Exception ex)
        {
            try
            {
                string folder = Path.GetDirectoryName(CrashLogPath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                File.AppendAllText(CrashLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never throw.
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppendCrashLog("UI thread (Dispatcher)", e.Exception);

            // Keep the app alive instead of terminating. Limit the message box spam so a
            // recurring timer failure cannot make the app unusable.
            _dispatcherCrashCount++;
            if (_dispatcherCrashCount <= 3)
            {
                try
                {
                    MessageBox.Show(
                        $"Podmix recovered from an unexpected error and kept running.{Environment.NewLine}{Environment.NewLine}" +
                        $"Details were written to:{Environment.NewLine}{CrashLogPath}{Environment.NewLine}{Environment.NewLine}" +
                        e.Exception.Message,
                        "Podmix Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch
                {
                }
            }

            e.Handled = true;
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppendCrashLog("Unhandled (AppDomain)", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            AppendCrashLog("Unobserved task", e.Exception);
            e.SetObserved();
        }

        private static void EnableModernTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
            }
        }

        private void RegisterToastNotifications()
        {
            try
            {
                ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
            }
            catch
            {
                // Toast registration can fail on restricted systems; the in-app notification fallback still works.
            }
        }

        private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (MainWindow is MainWindow mainWindow)
                        {
                            mainWindow.RestoreMainWindow();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendCrashLog("Toast activation", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                AppendCrashLog("Toast activation", ex);
            }
        }

        private static bool IsWindows10OrLater()
        {
            var versionInfo = new OSVERSIONINFOEX();
            versionInfo.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));

            if (RtlGetVersion(ref versionInfo) == 0)
            {
                return versionInfo.dwMajorVersion >= 10;
            }

            return Environment.OSVersion.Version.Major >= 10;
        }

        private void SendArgumentToRunningInstance(string arg)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", "PodmixPipe", PipeDirection.Out))
                {
                    try
                    {
                        client.Connect(500);
                        using (var writer = new StreamWriter(client))
                        {
                            writer.WriteLine(arg);
                            writer.Flush();
                        }
                    }
                    catch { /* Instance closed during connection */ }
                }
            }
            catch (Exception ex)
            {
                AppendCrashLog("Sending argument to running instance", ex);
            }
        }

        private void StartArgumentListener()
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream("PodmixPipe", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            server.WaitForConnection();
                            using (var reader = new StreamReader(server))
                            {
                                string filePath = reader.ReadLine();
                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    var dispatcher = Dispatcher;
                                    if (dispatcher != null && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                                    {
                                        dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            try
                                            {
                                                if (MainWindow is MainWindow mainWindow)
                                                {
                                                    mainWindow.RestoreMainWindow();
                                                    _ = mainWindow.LoadFolderAndPlayFile(filePath);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                AppendCrashLog("Pipe argument handling", ex);
                                            }
                                        }));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // A listener thread exception used to kill the whole process.
                        AppendCrashLog("Pipe listener", ex);
                        Thread.Sleep(250);
                    }
                }
            })
            { IsBackground = true, Name = "PodmixPipeListener" };
            thread.Start();
        }
    }
}
