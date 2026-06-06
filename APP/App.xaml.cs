/*
APP
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace DS4WinWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [System.Security.SuppressUnmanagedCodeSecurity]
    public partial class App : Application
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string sClass, string sWindow);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        private Thread controlThread;
        public static APP.ControlService rootHub;
        public static HttpClient requestClient;
        private bool skipSave;
        private bool runShutdown;
        private bool exitApp;
        private Thread testThread;
        private bool exitComThread = false;
        private const string SingleAppComEventName = "{a52b5b20-d9ee-4f32-8518-307fa14aa0c6}";
        private EventWaitHandle threadComEvent = null;
        private Timer collectTimer;
        private static LoggerHolder logHolder;

        private MemoryMappedFile ipcClassNameMMF = null; // MemoryMappedFile for inter-process communication used to hold className of DS4Form window
        private MemoryMappedFile ipcResultDataMMF = null; // MemoryMappedFile for inter-process communication used to exchange string result data between cmdline client process and the background running APP app

        private static Dictionary<APP.AppThemeChoice, string> themeLocs = new
            Dictionary<APP.AppThemeChoice, string>()
        {
            [APP.AppThemeChoice.Default] = "DS4Forms/Themes/DefaultTheme.xaml",
            [APP.AppThemeChoice.Light] = "DS4Forms/Themes/DefaultTheme.xaml",
            [APP.AppThemeChoice.Dark] = "DS4Forms/Themes/DarkTheme.xaml",
        };

        private const string ThemeSpacingLoc = "DS4Forms/Themes/ThemeSpacing.xaml";
        private const string ThemeKeyedStylesLoc = "DS4Forms/Themes/ThemeKeyedStyles.xaml";
        private const string ThemeToolbarStylesLoc = "DS4Forms/Themes/ThemeToolbarStyles.xaml";
        private const string ThemeCompactStylesLoc = "DS4Forms/Themes/ThemeCompactStyles.xaml";
        private const string ThemeColorPickerStylesLoc = "DS4Forms/Themes/ThemeColorPickerStyles.xaml";

        public event EventHandler ThemeChanged;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            runShutdown = true;
            skipSave = true;

            ArgumentParser parser = new ArgumentParser();
            parser.Parse(e.Args);
            CheckOptions(parser);

            if (exitApp)
            {
                return;
            }

            try
            {
                Process.GetCurrentProcess().PriorityClass =
                    ProcessPriorityClass.High;
            }
            catch { } // Ignore problems raising the priority.

            // Force Normal IO Priority
            IntPtr ioPrio = new IntPtr(2);
            APP.Util.NtSetInformationProcess(Process.GetCurrentProcess().Handle,
                APP.Util.PROCESS_INFORMATION_CLASS.ProcessIoPriority, ref ioPrio, 4);

            // Force Normal Page Priority
            IntPtr pagePrio = new IntPtr(5);
            APP.Util.NtSetInformationProcess(Process.GetCurrentProcess().Handle,
                APP.Util.PROCESS_INFORMATION_CLASS.ProcessPagePriority, ref pagePrio, 4);

            // another instance is already running if TryOpenExisting returns true.
            try
            {
                if (EventWaitHandleAcl.TryOpenExisting(SingleAppComEventName,
                System.Security.AccessControl.EventWaitHandleRights.Synchronize |
                System.Security.AccessControl.EventWaitHandleRights.Modify,
                out EventWaitHandle tempComEvent))
                {
                    tempComEvent.Set();  // signal the other instance.
                    tempComEvent.Close();

                    MessageBox.Show(
                        Translations.Strings.App_AlreadyRunning,
                        Translations.Strings.App_Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    runShutdown = false;
                    Current.Shutdown();    // Quit temp instance
                    return;
                }
            }
            catch (System.UnauthorizedAccessException)
            {
                // Ignore exception
            }

            // Allow sleep time durations less than 16 ms
            APP.Util.timeBeginPeriod(1);

            // Retrieve info about installed ViGEmBus device if found
            APP.Global.RefreshViGEmBusInfo();

            // Create the Event handle
            threadComEvent = new EventWaitHandle(false, EventResetMode.ManualReset, SingleAppComEventName);
            CreateTempWorkerThread();

            CreateControlService(parser);
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            bool firstRun = false;
            try
            {
                RunStartupSetup(ref firstRun);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Translations.Strings.App_StartupFailed, ex.Message, ex.GetType().Name),
                    Translations.Strings.App_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                runShutdown = false;
                Current.Shutdown(1);
                return;
            }

            if (!EnsureConfigDirectoryStructure())
            {
                MessageBox.Show(
                    string.Format(Translations.Strings.App_CannotCreateConfigFolder, APP.Global.appdatapath),
                    Translations.Strings.App_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Current.Shutdown(1);
                return;
            }

            logHolder = new LoggerHolder(rootHub);
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Logger logger = logHolder.Logger;
            string version = APP.Global.exeversion;
            logger.Info($"APP version {version}");
            logger.Info($"APP exe file: {APP.Global.exeFileName}");
            logger.Info($"APP Assembly Architecture: {(Environment.Is64BitProcess ? "x64" : "x86")}");
            logger.Info($"OS Version: {Environment.OSVersion}");
            logger.Info($"OS Product Name: {APP.Util.GetOSProductName()}");
            logger.Info($"OS Release ID: {APP.Util.GetOSReleaseId()}");
            logger.Info($"System Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
            logger.Info("Logger created");

            bool readAppConfig = APP.Global.Load();
            if (firstRun)
            {
                APP.Global.ApplyFirstLaunchDefaults();
            }

            APP.InputLatencyPipeline.ApplySystemTuning();
            if (!firstRun && !readAppConfig)
            {
                logger.Info($@"Profiles.xml not read at location ${APP.Global.appdatapath}\Profiles.xml. Using default app settings");
            }

            // Ask user which devices the mapper should attempt to open when detected.
            // Currently only support DS4 by default to avoid extra complications from
            // Steam Input
            if (firstRun)
            {
                DS4Forms.FirstLaunchUtilWindow firstLaunchUtilWin =
                    new DS4Forms.FirstLaunchUtilWindow(APP.Global.DeviceOptions);
                firstLaunchUtilWin.ShowDialog();
                APP.Global.Save();
            }

            if (firstRun)
            {
                logger.Info("No config found. Creating default config");
                AttemptSave();

                APP.Global.SaveAsNewProfile(0, "Default");
                for (int i = 0; i < APP.ControlService.MAX_DS4_CONTROLLER_COUNT; i++)
                {
                    APP.Global.ProfilePath[i] = APP.Global.OlderProfilePath[i] = "Default";
                }

                logger.Info("Default config created");
            }
            else
            {
                EnsureDefaultProfileExists();
            }

            skipSave = false;

            if (!APP.Global.LoadActions())
            {
                APP.Global.CreateStdActions();
            }

            APP.Global.UseLang = string.Empty;
            APP.AppThemeChoice themeChoice = APP.Global.UseCurrentTheme;
            ChangeTheme(APP.Global.UseCurrentTheme, false);

            APP.Global.LoadLinkedProfiles();

            try
            {
                DS4Forms.MainWindow window = new DS4Forms.MainWindow(parser);
                MainWindow = window;
                window.IsInitialShow = true;
                window.Show();
                window.IsInitialShow = false;

                // Set up hooks for IPC command calls
                HwndSource source = PresentationSource.FromVisual(window) as HwndSource;
                CreateIPCClassNameMMF(source.Handle);

                window.CheckMinStatus();

                bool runningAsAdmin = APP.Global.IsAdministrator();
                rootHub.LogDebug($"Running as {(runningAsAdmin ? "Admin" : "User")}");

                if (APP.Global.hidHideInstalled)
                {
                    rootHub.CheckHidHidePresence();
                }

                rootHub.LoadPermanentSlotsConfig();
                window.LateChecks(parser);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Translations.Strings.App_MainWindowOpenFailed, ex.Message),
                    Translations.Strings.App_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                runShutdown = false;
                Current.Shutdown(1);
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (!Current.Dispatcher.CheckAccess())
            {
                Logger logger = logHolder.Logger;
                Exception exp = e.ExceptionObject as Exception;
                logger.Error($"Thread App Crashed with message {exp.Message}");
                logger.Error(exp.ToString());
                //LogManager.Flush();
                //LogManager.Shutdown();
                if (e.IsTerminating)
                {
                    Dispatcher.Invoke(() =>
                    {
                        rootHub?.PrepareAbort();
                        CleanShutdown();
                    });
                }
            }
            else
            {
                Logger logger = logHolder.Logger;
                Exception exp = e.ExceptionObject as Exception;
                if (e.IsTerminating)
                {
                    logger.Error($"Thread Crashed with message {exp.Message}");
                    logger.Error(exp.ToString());

                    rootHub?.PrepareAbort();
                    CleanShutdown();
                }
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            //Debug.WriteLine("App Crashed");
            //Debug.WriteLine(e.Exception.StackTrace);
            Logger logger = logHolder.Logger;
            logger.Error($"Thread Crashed with message {e.Exception.Message}");
            logger.Error(e.Exception.ToString());
            //LogManager.Flush();
            //LogManager.Shutdown();
        }

        private void RunStartupSetup(ref bool firstRun)
        {
            APP.Global.FindConfigLocation();
            firstRun = APP.Global.firstRun;

            if (firstRun)
            {
                if (!EnsureSaveLocationChosen())
                {
                    throw new InvalidOperationException(
                        "Profile save location was not configured.");
                }
            }
            else if (string.IsNullOrEmpty(APP.Global.appdatapath) ||
                     !APP.Global.HasCompleteConfigAt(APP.Global.appdatapath))
            {
                APP.Global.firstRun = true;
                firstRun = true;

                bool programAutoProfilesExists = File.Exists(
                    Path.Combine(APP.Global.exedirpath, "Auto Profiles.xml"));
                bool appDataAutoProfilesExists = File.Exists(
                    Path.Combine(APP.Global.appDataPpath, "Auto Profiles.xml"));
                bool isSameFolder = string.Equals(
                    Path.GetFullPath(APP.Global.exedirpath),
                    Path.GetFullPath(APP.Global.appDataPpath),
                    StringComparison.OrdinalIgnoreCase);
                APP.Global.multisavespots = programAutoProfilesExists &&
                    appDataAutoProfilesExists && !isSameFolder;

                if (!EnsureSaveLocationChosen())
                {
                    throw new InvalidOperationException(
                        "Profile save location was not configured.");
                }
            }
        }

        private bool EnsureSaveLocationChosen()
        {
            if (ShowSaveWhereDialog())
            {
                return true;
            }

            return ApplyDefaultAppDataSaveLocation();
        }

        private bool ShowSaveWhereDialog()
        {
            try
            {
                DS4Forms.SaveWhere savewh =
                    new DS4Forms.SaveWhere(APP.Global.multisavespots);
                savewh.ShowDialog();
                return savewh.ChoiceMade;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Translations.Strings.App_SetupWindowFailed, ex.Message),
                    Translations.Strings.App_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        private static bool ApplyDefaultAppDataSaveLocation()
        {
            try
            {
                Directory.CreateDirectory(APP.Global.appDataPpath);
                string profilesPath = Path.Combine(APP.Global.appDataPpath, "Profiles.xml");
                if (!File.Exists(profilesPath))
                {
                    APP.Global.SaveDefault(profilesPath);
                }

                APP.Global.SaveWhere(APP.Global.appDataPpath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Translations.Strings.App_CannotCreateConfigurationFolder, ex.Message),
                    Translations.Strings.App_Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private static bool EnsureConfigDirectoryStructure()
        {
            if (string.IsNullOrEmpty(APP.Global.appdatapath))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(APP.Global.appdatapath);
                Directory.CreateDirectory(Path.Combine(APP.Global.appdatapath, "Profiles"));
                Directory.CreateDirectory(Path.Combine(APP.Global.appdatapath, "Logs"));
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void EnsureDefaultProfileExists()
        {
            string profilesDir = Path.Combine(APP.Global.appdatapath, "Profiles");
            if (!Directory.Exists(profilesDir))
            {
                return;
            }

            bool hasProfiles = Directory.EnumerateFiles(profilesDir, "*.xml").Any();
            if (hasProfiles)
            {
                return;
            }

            APP.Global.SaveAsNewProfile(0, "Default");
            for (int i = 0; i < APP.ControlService.MAX_DS4_CONTROLLER_COUNT; i++)
            {
                APP.Global.ProfilePath[i] = APP.Global.OlderProfilePath[i] = "Default";
            }
        }

        private void AttemptSave()
        {
            if (!APP.Global.Save()) //if can't write to file
            {
                if (MessageBox.Show(DS4WinWPF.Properties.Resources.CannotWriteHere, Translations.Strings.App_Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.CreateDirectory(APP.Global.appDataPpath);
                        File.Copy(APP.Global.exedirpath + "\\Profiles.xml",
                            APP.Global.appDataPpath + "\\Profiles.xml");
                        File.Copy(APP.Global.exedirpath + "\\Auto Profiles.xml",
                            APP.Global.appDataPpath + "\\Auto Profiles.xml");
                        Directory.CreateDirectory(APP.Global.appDataPpath + "\\Profiles");
                        foreach (string s in Directory.GetFiles(APP.Global.exedirpath + "\\Profiles"))
                        {
                            File.Copy(s, APP.Global.appDataPpath + "\\Profiles\\" + Path.GetFileName(s));
                        }
                    }
                    catch { }
                    MessageBox.Show(DS4WinWPF.Properties.Resources.CopyComplete, Translations.Strings.App_Title);
                }
                else
                {
                    MessageBox.Show(DS4WinWPF.Properties.Resources.APPCannotEditHere, Translations.Strings.App_Title);
                }

                APP.Global.appdatapath = null;
                skipSave = true;
                Current.Shutdown();
                return;
            }
        }

        private void CheckOptions(ArgumentParser parser)
        {
            if (parser.HasErrors)
            {
                runShutdown = false;
                exitApp = true;
                Current.Shutdown(1);
            }
            else if (parser.Driverinstall)
            {
                // Retrieve info about installed ViGEmBus device if found.
                // Might not be needed here
                APP.Global.RefreshViGEmBusInfo();

                // Load APP config if it exists
                APP.Global.FindConfigLocation();
                bool readAppConfig = APP.Global.Load();
                if (readAppConfig)
                {
                    APP.Global.UseLang = string.Empty;
                    APP.AppThemeChoice themeChoice = APP.Global.UseCurrentTheme;
                    ChangeTheme(APP.Global.UseCurrentTheme, false);
                }

                CreateBaseThread();
                DS4Forms.WelcomeDialog dialog = new DS4Forms.WelcomeDialog(true);
                dialog.ShowDialog();
                runShutdown = false;
                exitApp = true;
                Current.Shutdown();
            }
            else if (parser.ReenableDevice)
            {
                APP.DS4Devices.reEnableDevice(parser.DeviceInstanceId);
                runShutdown = false;
                exitApp = true;
                Current.Shutdown();
            }
            else if (parser.Runtask)
            {
                StartupMethods.LaunchOldTask();
                runShutdown = false;
                exitApp = true;
                Current.Shutdown();
            }
            else if (parser.Command)
            {
                IntPtr hWndAPPForm = IntPtr.Zero;
                hWndAPPForm = FindWindow(ReadIPCClassNameMMF(), "APP");
                if (hWndAPPForm != IntPtr.Zero)
                {
                    bool bDoSendMsg = true;
                    bool bWaitResultData = false;
                    bool bOwnsMutex = false;
                    Mutex ipcSingleTaskMutex = null;
                    EventWaitHandle ipcNotifyEvent = null;

                    COPYDATASTRUCT cds;
                    cds.lpData = IntPtr.Zero;

                    try
                    {
                        if (parser.CommandArgs.ToLower().StartsWith("query."))
                        {
                            // Query.device# (1..4) command returns a string result via memory mapped file. The cmd is sent to the background APP 
                            // process (via WM_COPYDATA wnd msg), then this client process waits for the availability of the result and prints it to console output pipe.
                            // Use mutex obj to make sure that concurrent client calls won't try to write and read the same MMF result file at the same time.
                            ipcSingleTaskMutex = new Mutex(false, "APP_IPCResultData_SingleTaskMtx");
                            try
                            {
                                bOwnsMutex = ipcSingleTaskMutex.WaitOne(10000);
                            }
                            catch (AbandonedMutexException)
                            {
                                bOwnsMutex = true;
                            }

                            if (bOwnsMutex)
                            {
                                // This process owns the inter-process sync mutex obj. Let's proceed with creating the output MMF file and waiting for a result.
                                bWaitResultData = true;
                                CreateIPCResultDataMMF();
                                ipcNotifyEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "APP_IPCResultData_ReadyEvent");
                            }
                            else
                                // If the mtx failed then something must be seriously wrong. Cannot do anything in that case because MMF file may be modified by concurrent processes.
                                bDoSendMsg = false;
                        }

                        if (bDoSendMsg)
                        {
                            cds.dwData = IntPtr.Zero;
                            cds.cbData = parser.CommandArgs.Length;
                            cds.lpData = Marshal.StringToHGlobalAnsi(parser.CommandArgs);
                            SendMessage(hWndAPPForm, DS4Forms.MainWindow.WM_COPYDATA, IntPtr.Zero, ref cds);

                            if (bWaitResultData)
                                Console.WriteLine(WaitAndReadIPCResultDataMMF(ipcNotifyEvent));
                        }
                    }
                    finally
                    {
                        // Release the result MMF file in the client process before releasing the mtx and letting other client process to proceed with the same MMF file
                        if (ipcResultDataMMF != null) ipcResultDataMMF.Dispose();
                        ipcResultDataMMF = null;

                        // If this was "Query.xxx" cmdline client call then release the inter-process mutex and let other concurrent clients to proceed (if there are anyone waiting for the MMF result file)
                        if (bOwnsMutex && ipcSingleTaskMutex != null)
                            ipcSingleTaskMutex.ReleaseMutex();

                        if (cds.lpData != IntPtr.Zero)
                            Marshal.FreeHGlobal(cds.lpData);
                    }
                }

                runShutdown = false;
                exitApp = true;
                Current.Shutdown();
            }
        }

        private void CreateControlService(ArgumentParser parser)
        {
            controlThread = new Thread(() =>
            {
                rootHub = new APP.ControlService(parser);

                APP.Program.rootHub = rootHub;
                requestClient = new HttpClient();
                requestClient.DefaultRequestHeaders.Add("User-Agent", "APP");
                collectTimer = new Timer(GarbageTask, null, 30000, 30000);

            });
            controlThread.Priority = ThreadPriority.Normal;
            controlThread.IsBackground = true;
            controlThread.Start();
            while (controlThread.IsAlive)
                Thread.SpinWait(500);
        }

        private void CreateBaseThread()
        {
            controlThread = new Thread(() =>
            {
                APP.Program.rootHub = rootHub;
                requestClient = new HttpClient();
                requestClient.DefaultRequestHeaders.Add("User-Agent", "APP");
                collectTimer = new Timer(GarbageTask, null, 30000, 30000);
            });
            controlThread.Priority = ThreadPriority.Normal;
            controlThread.IsBackground = true;
            controlThread.Start();
            while (controlThread.IsAlive)
                Thread.SpinWait(500);
        }

        private void GarbageTask(object state)
        {
            GC.Collect(0, GCCollectionMode.Forced, false);
        }

        private void CreateTempWorkerThread()
        {
            testThread = new Thread(SingleAppComThread_DoWork);
            testThread.Priority = ThreadPriority.Lowest;
            testThread.IsBackground = true;
            testThread.Start();
        }

        private void SingleAppComThread_DoWork()
        {
            while (!exitComThread)
            {
                // check for a signal.
                if (threadComEvent.WaitOne())
                {
                    threadComEvent.Reset();
                    // The user tried to start another instance. We can't allow that,
                    // so bring the other instance back into view and enable that one.
                    // That form is created in another thread, so we need some thread sync magic.
                    if (!exitComThread)
                    {
                        Dispatcher.BeginInvoke((Action)(() =>
                        {
                            MainWindow.Show();
                            MainWindow.WindowState = WindowState.Normal;
                        }));
                    }
                }
            }
        }

        public void CreateIPCClassNameMMF(IntPtr hWnd)
        {
            if (ipcClassNameMMF != null) return; // Already holding a handle to MMF file. No need to re-write the data

            try
            {
                StringBuilder wndClassNameStr = new StringBuilder(128);
                if (GetClassName(hWnd, wndClassNameStr, wndClassNameStr.Capacity) != 0 && wndClassNameStr.Length > 0)
                {
                    byte[] buffer = ASCIIEncoding.ASCII.GetBytes(wndClassNameStr.ToString());

                    ipcClassNameMMF = MemoryMappedFile.CreateNew("APP_IPCClassName.dat", 128);
                    MemoryMappedViewAccessor ipcClassNameMMA_Now = ipcClassNameMMF.CreateViewAccessor(0, buffer.Length);
                    ipcClassNameMMA_Now.WriteArray(0, buffer, 0, buffer.Length);
                    ipcClassNameMMA_Now?.Dispose();
                    // The MMF file is alive as long this process holds the file handle open
                }
            }
            catch (Exception)
            {
                /* Eat all exceptions because errors here are not fatal for DS4Win */
            }
        }

        private string ReadIPCClassNameMMF()
        {
            MemoryMappedFile mmf = null;
            MemoryMappedViewAccessor mma = null;

            try
            {
                byte[] buffer = new byte[128];
                mmf = MemoryMappedFile.OpenExisting("APP_IPCClassName.dat");
                mma = mmf.CreateViewAccessor(0, 128);
                mma.ReadArray(0, buffer, 0, buffer.Length);
                return ASCIIEncoding.ASCII.GetString(buffer);
            }
            catch (Exception)
            {
                // Eat all exceptions
            }
            finally
            {
                if (mma != null) mma.Dispose();
                if (mmf != null) mmf.Dispose();
            }

            return null;
        }

        private void CreateIPCResultDataMMF()
        {
            // Cmdline client process calls this to create the MMF file used in inter-process-communications. The background APP process 
            // uses WriteIPCResultDataMMF method to write a command result and the client process reads the result from the same MMF file.
            if (ipcResultDataMMF != null) return; // Already holding a handle to MMF file. No need to re-write the data

            try
            {
                ipcResultDataMMF = MemoryMappedFile.CreateNew("APP_IPCResultData.dat", 256);
                // The MMF file is alive as long this process holds the file handle open
            }
            catch (Exception)
            {
                /* Eat all exceptions because errors here are not fatal for DS4Win */
            }
        }

        private string WaitAndReadIPCResultDataMMF(EventWaitHandle ipcNotifyEvent)
        {
            if (ipcResultDataMMF != null)
            {
                // Wait until the inter-process-communication (IPC) result data is available and read the result
                try
                {
                    // Wait max 10 secs and if the result is still not available then timeout and return "empty" result
                    if (ipcNotifyEvent == null || ipcNotifyEvent.WaitOne(10000))
                    {
                        int strNullCharIdx;
                        byte[] buffer = new byte[256];
                        MemoryMappedViewAccessor ipcResultDataMMA = ipcClassNameMMF.CreateViewAccessor(0, buffer.Length);
                        ipcResultDataMMA.ReadArray(0, buffer, 0, buffer.Length);
                        strNullCharIdx = Array.FindIndex(buffer, byteVal => byteVal == 0);
                        return ASCIIEncoding.ASCII.GetString(buffer, 0, (strNullCharIdx <= 1 ? 1 : strNullCharIdx));
                    }
                }
                catch (Exception)
                {
                    /* Eat all exceptions because errors here are not fatal for DS4Win */
                }
            }

            return String.Empty;
        }

        public void WriteIPCResultDataMMF(string dataStr)
        {
            // The background APP process calls this method to write out the result of "-command QueryProfile.device#" command.
            // The cmdline client process reads the result from the APP_IPCResultData.dat MMF file and sends the result to console output pipe.
            MemoryMappedFile mmf = null;
            MemoryMappedViewAccessor mma = null;
            EventWaitHandle ipcNotifyEvent = null;

            try
            {
                ipcNotifyEvent = EventWaitHandle.OpenExisting("APP_IPCResultData_ReadyEvent");

                byte[] buffer = ASCIIEncoding.ASCII.GetBytes(dataStr);
                mmf = MemoryMappedFile.OpenExisting("APP_IPCResultData.dat");
                mma = mmf.CreateViewAccessor(0, 256);
                mma.WriteArray(0, buffer, 0, (buffer.Length >= 256 ? 256 : buffer.Length));
            }
            catch (Exception)
            {
                // Eat all exceptions
            }
            finally
            {
                if (mma != null) mma.Dispose();
                if (mmf != null) mmf.Dispose();

                if (ipcNotifyEvent != null) ipcNotifyEvent.Set();
            }
        }

        public void ChangeTheme(APP.AppThemeChoice themeChoice,
            bool fireChanged = true)
        {
            if (themeChoice == APP.AppThemeChoice.Default)
            {
                // Attempt to switch theme based on currently selected Windows apps theme mode
                APP.AppThemeChoice implicitTheme = APP.Util.SystemAppsUsingDarkTheme() ?
                    APP.AppThemeChoice.Dark : APP.AppThemeChoice.Light;
                themeLocs.TryGetValue(implicitTheme, out string loc);
                ApplyThemeDictionaries(loc);

                if (fireChanged)
                {
                    ThemeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (themeLocs.TryGetValue(themeChoice, out string loc))
            {
                ApplyThemeDictionaries(loc);

                if (fireChanged)
                {
                    ThemeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Replaces merged theme dictionaries. Must mirror App.xaml order: base theme, then modern UI layer.
        /// </summary>
        private static void ApplyThemeDictionaries(string themeLoc)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            merged.Clear();
            merged.Add(new ResourceDictionary() { Source = new Uri(themeLoc, uriKind: UriKind.Relative) });
            merged.Add(new ResourceDictionary() { Source = new Uri(ThemeSpacingLoc, uriKind: UriKind.Relative) });
            merged.Add(new ResourceDictionary() { Source = new Uri(ThemeKeyedStylesLoc, uriKind: UriKind.Relative) });
            merged.Add(new ResourceDictionary() { Source = new Uri(ThemeToolbarStylesLoc, uriKind: UriKind.Relative) });
            merged.Add(new ResourceDictionary() { Source = new Uri(ThemeCompactStylesLoc, uriKind: UriKind.Relative) });
            merged.Add(new ResourceDictionary() { Source = new Uri(ThemeColorPickerStylesLoc, uriKind: UriKind.Relative) });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (runShutdown)
            {
                Logger logger = logHolder.Logger;
                logger.Info("Request App Shutdown");
                CleanShutdown();
            }
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Logger logger = logHolder.Logger;
            logger.Info("User Session Ending");
            CleanShutdown();
        }

        private void CleanShutdown()
        {
            if (runShutdown)
            {
                if (rootHub != null)
                {
                    Task.Run(() =>
                    {
                        if (rootHub.running)
                        {
                            rootHub.Stop(immediateUnplug: true);
                            rootHub.ShutDown();
                        }
                    }).Wait();
                }

                if (!skipSave)
                {
                    APP.Global.Save();
                }

                // Reset timer
                APP.Util.timeEndPeriod(1);

                exitComThread = true;
                if (threadComEvent != null)
                {
                    threadComEvent.Set();  // signal the other instance.
                    while (testThread.IsAlive)
                        Thread.SpinWait(500);
                    threadComEvent.Close();
                }

                if (ipcClassNameMMF != null) ipcClassNameMMF.Dispose();

                LogManager.Flush();
                LogManager.Shutdown();
            }
        }
    }
}
