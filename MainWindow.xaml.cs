
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Enc;
using WindowsShuffle.Classes;

namespace WindowsShuffle
{
    public partial class MainWindow : Window
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

		private static readonly HttpClient httpClient = new HttpClient();
        private double _playerVolume = 0.5;
        private const string DefaultThemeColor = "#34495e";
        private SplashWindow _splashWindow;
        private bool _isAppJustStarted = true;
        private bool _isFirstLoad = true;
        private bool _startupWindowRevealed = false;
        private string _openedMediaPath = "";
        private bool _bassNetRegistered = false;
        private bool _startupInitialized = false;
        private bool _startupDeviceRevealed = false;
        private bool _delayStartupWindowRevealForAlbumColor = false;
        private bool _isStartupLoading = true;
        private bool _playRequestedDuringStartup = false;
        private const int BassDefaultDevice = -1;
        private int _selectedBassDevice = BassDefaultDevice;
        private readonly HashSet<int> _initializedBassDevices = new HashSet<int>();
        private readonly DiscordPresenceManager _discord = new DiscordPresenceManager();
        public System.Windows.Forms.NotifyIcon _notifyIcon;

        public List<string> playlist = new List<string>();
        private string Root { get; set; } = null;
        private string PlaylistsPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "playlists");
        public List<int> shuffleOrder = new List<int>();
        public int currentIndex = -1;
        private DispatcherTimer timer = new DispatcherTimer();
        private DispatcherTimer _lockClockTimer = new DispatcherTimer();
        private DispatcherTimer _screenSleepTimer = new DispatcherTimer();
        private DispatcherTimer _bootGifTimer;
        private readonly List<BitmapSource> _bootGifFrames = new List<BitmapSource>();
        private readonly List<int> _bootGifFrameDelays = new List<int>();
        private int _bootGifFrameIndex = 0;
        private Random rng = new Random();
        private bool isPlaying = false;
        private bool isDetailed = false;
        private string colorHex { get; set; } = "";
        public string lastFolderPath, currentFilePath = "";
        private FileSystemWatcher libraryWatcher;
        bool isDraggingSlider = false;
        private bool isFavoritesMode, isLyricsMode = false;

        private Brush _artistNormalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDD"));
        private Brush _artistHoverBrush = Brushes.White;
        private string RatingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "ratings.json");
        private string NotificationImagesFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "notifications");
        private NotificationWindow _activeNotificationWindow;
        private Dictionary<string, int> _ratings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private string _pendingLyricsPath = null;
        private string _pendingLyricsText = null;
        private string _temCachedLyricsText = null;
        private bool _isShuttingDown = false;
        private readonly List<CoverFlowAlbum> _coverFlowAlbums = new List<CoverFlowAlbum>();
        private const int CoverFlowArtworkCacheLimit = 5;
        private readonly Dictionary<string, ImageSource> _coverFlowArtworkCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> _coverFlowArtworkCacheOrder = new LinkedList<string>();
        private int _coverFlowIndex = 0;
        private bool _isCoverFlowLoading = false;
        private bool _isCoverFlowReady = false;
        private bool isCoverFlowMode = false;
        private const string PlaylistRestoreFolder = "Folder";
        private const string PlaylistRestoreQueue = "Queue";
        private string _lastPlaylistRestoreMode = PlaylistRestoreFolder;
        private List<string> _lastPlaylistTracks = new List<string>();

        private RadioWindow radioWindow;
        private bool isRadioMode = false;
        private int _localHandle = 0;
        private int _radioHandle = 0;
        private bool _bassInitialized = false;
        private const double PreviousButtonRestartThresholdSeconds = 3.0;
        private bool _suppressLocalEndSync = false;
        private SYNCPROC _localEndSync;
        private SYNCPROC _metaSync;
        private const int StartupBootExitDelayMs = 160;
        private const int StartupBootExitDurationMs = 260;
        private const int StartupBootMinimumVisibleMs = 1800;
        private DateTime _bootViewIntroStartedAt;
        private string _lastRadioMetadata = string.Empty;
        private string RadioRecentPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "radio_recent.json");
        private DispatcherTimer _radioMonitorTimer;
        private DispatcherTimer _radioReconnectTimer;
        private DispatcherTimer _volumeHideTimer;
        private string _currentStationFaviconUrl = string.Empty;
        private string _currentRadioStationName = string.Empty;
        private string _currentRadioPrimaryUrl = string.Empty;
        private List<string> _currentRadioCandidateUrls = new List<string>();
        private int _radioReconnectAttempts = 0;
        private const int MaxRadioReconnectAttempts = 5;
        private const int RadioMetadataFallbackSeconds = 12;
        private DispatcherTimer _radioMetadataFallbackTimer;
        private int _radioMetadataFallbackHandle = 0;
        private bool _radioMetadataFallbackApplied = false;
        private string RadioFavoritesPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "radio_favorites.json");
        private string LockWallpaperFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "wallpaper");
        private string LockWallpaperSearchPattern => "lock_wallpaper.*";
        private int _encoderHandle = 0;
        private const int DevicePasscodeLength = 4;
        private const int LockScreenSleepSeconds = 30;
        private const int ScreenDimLeadSeconds = 8;
        private const double ScreenDimOpacity = 0.62;
        private const double WheelVolumeDegreesPerStep = 10.0;
        private const double TaskbarDockRightMargin = 15;
        private const double TaskbarVisualFloorOverlap = 16;
        private enum PasscodeMode
        {
            Unlock,
            VerifyCurrentForChange,
            VerifyCurrentToDisable,
            SetNew,
            ConfirmNew
        }

        private enum RadioUnavailableAction
        {
            Dismiss,
            Retry,
            SearchAgain,
            RemoveFromFavorites
        }

        private bool _isDevicePoweredOff = false;
        private bool _isDeviceLocked = false;
        private bool _suppressPasscodeMenuEvent = false;
        private string _passcodeEntry = string.Empty;
        private string _pendingNewPasscode = string.Empty;
        private PasscodeMode _passcodeMode = PasscodeMode.Unlock;
        private bool _isPasscodeEntryVisible = false;
        private bool _passcodeFlowStartedWhileLocked = false;
        private DateTime _lastScreenActivityUtc = DateTime.UtcNow;
        private readonly string _startupFilePath;
        private bool _startupFileHandled = false;
        private bool _isScreenDimmed = false;
        private bool _isWheelVolumePending = false;
        private bool _isWheelVolumeDragging = false;
        private bool _wheelVolumeStartedOnButton = false;
        private Point _wheelDragStartPoint;
        private double _lastWheelAngle = 0;
        private double _wheelVolumeAccumulator = 0;

        private bool InitBass()
        {
            if (EnsureBassDeviceReady(_selectedBassDevice))
            {
                return true;
            }

            if (_selectedBassDevice == BassDefaultDevice)
                return false;

            _selectedBassDevice = BassDefaultDevice;
            try { Config.Save("audio_output_device", _selectedBassDevice); } catch { }

            return EnsureBassDeviceReady(_selectedBassDevice);
        }

        public MainWindow()
        {
            _splashWindow = new SplashWindow();
            _splashWindow.Show();
            _splashWindow.Activate();

            RegisterBassNet();
            InitializeComponent();

            // Make sure the actual player remains the application's main window.
            Application.Current.MainWindow = this;

            NameScope.SetNameScope(this, new NameScope());
            RegisterName("PodmixMusicInstance", this);            
            _startupFilePath = GetStartupAudioFilePath();
            _delayStartupWindowRevealForAlbumColor = IsExtractAlbumCoverColorEnabled();

            // Setup timer for slider and time text
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            _lockClockTimer.Interval = TimeSpan.FromSeconds(30);
            _lockClockTimer.Tick += (s, e) => UpdateLockScreenInfo();

            _screenSleepTimer.Interval = TimeSpan.FromSeconds(1);
            _screenSleepTimer.Tick += (s, e) =>
            {
                CheckScreenSleepTimeout();
            };

            LoadViewState();
            if (string.IsNullOrWhiteSpace(colorHex))
                ApplyThemeColor(DefaultThemeColor);
            InitializeStartupWindowPlacement();
            HoldStartupDeviceUntilColorReady();

            ContentRendered += MainWindow_ContentRendered;
        }

        private void InitializeStartupWindowPlacement()
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            DockWindowToTaskbarRight();

            // Keep the main player hidden until initialization finishes.
            Opacity = 0;
            ShowInTaskbar = false;
            IsHitTestVisible = false;
        }

        private bool IsExtractAlbumCoverColorEnabled()
        {
            try { return (bool)Config.Get("extract_album_cover_color"); }
            catch { return false; }
        }

        private void HoldStartupDeviceUntilColorReady()
        {
            _startupDeviceRevealed = false;
            MainContainer.BeginAnimation(UIElement.OpacityProperty, null);
            MainContainer.Opacity = 1;
            MainContainer.IsHitTestVisible = false;
        }

        private void RevealStartupDevice()
        {
            if (_startupDeviceRevealed)
                return;

            _startupDeviceRevealed = true;
            MainContainer.IsHitTestVisible = true;
            MainContainer.BeginAnimation(UIElement.OpacityProperty, null);

            var fadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            MainContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private async void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            if (_startupInitialized)
                return;

            _startupInitialized = true;
            ContentRendered -= MainWindow_ContentRendered;

            try
            {
                await Task.Delay(1);
                LoadRatings();
                LoadLockWallpaper();
                GenerateColorIcons();
                _discord.Initialize();
                await GetInternalSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup initialization failed: {ex.Message}");
                // Don't leave the user stuck on the splash screen.
                RevealStartupWindow();
            }
        }

        private void RegisterBassNet()
        {
            if (_bassNetRegistered)
                return;

            try
            {
                var me = (string)Config.Get("me");
                var bKey = (string)Config.Get("b_key");
                BassNet.Registration(me, bKey);
                _bassNetRegistered = true;
            }
            catch
            {
                _bassNetRegistered = false;
            }
        }

        private void LoadRatings()
        {
            if (File.Exists(RatingsPath))
            {
                try { _ratings = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(RatingsPath)) ?? new Dictionary<string, int>(); }
                catch { _ratings = new Dictionary<string, int>(); }
            }
        }

        private void SaveRatings()
        {
            try { File.WriteAllText(RatingsPath, JsonConvert.SerializeObject(_ratings)); }
            catch { }
        }

        private float GetEffectiveBassVolume()
        {
            return MenuMute != null && MenuMute.IsChecked ? 0f : (float)Math.Max(0, Math.Min(1, _playerVolume));
        }

        private void ApplyBassVolume()
        {
            float volume = GetEffectiveBassVolume();

            if (_localHandle != 0)
                Bass.BASS_ChannelSetAttribute(_localHandle, BASSAttribute.BASS_ATTRIB_VOL, volume);

            if (_radioHandle != 0)
                Bass.BASS_ChannelSetAttribute(_radioHandle, BASSAttribute.BASS_ATTRIB_VOL, volume);
        }

        private int GetRadioBufferMode()
        {
            try { return Math.Max(0, Math.Min(2, Convert.ToInt32(Config.Get("radio_buffer_mode")))); }
            catch { return 1; }
        }

        private void ApplyRadioBufferSettings()
        {
            int mode = GetRadioBufferMode();

            switch (mode)
            {
                case 0:
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_BUFFER, 1500);
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_BUFFER, 350);
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PREBUF, 25);
                    break;
                case 2:
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_BUFFER, 12000);
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_BUFFER, 1800);
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PREBUF, 75);
                    break;
                default:
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_BUFFER, 5000);
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_BUFFER, 900);
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PREBUF, 50);
                    break;
            }
        }

        private void UpdateRadioBufferMenuChecks()
        {
            if (MenuRadioBuffer == null) return;

            int mode = GetRadioBufferMode();
            foreach (object child in MenuRadioBuffer.Items)
            {
                if (child is MenuItem item && item.Tag != null)
                    item.IsChecked = Convert.ToInt32(item.Tag) == mode;
            }
        }

        private void RadioBuffer_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem item) || item.Tag == null) return;

            int mode = Math.Max(0, Math.Min(2, Convert.ToInt32(item.Tag)));
            Config.Save("radio_buffer_mode", mode);
            UpdateRadioBufferMenuChecks();

            if (isRadioMode)
            {
                MessageBox.Show(
                    owner: this,
                    messageBoxText: "The new buffer size will be used the next time Podmix connects to a radio stream.",
                    caption: "Radio Buffer",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Information);
            }
        }

        private string GetDefaultRecordingFolder()
        {
            string root = System.IO.Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root))
                root = @"C:\";

            return System.IO.Path.Combine(root, "Music", "Podmix Recordings");
        }

        private string GetRecordingFolder()
        {
            string folder = string.Empty;
            try { folder = (string)Config.Get("radio_recording_folder"); } catch { }

            if (string.IsNullOrWhiteSpace(folder))
                folder = GetDefaultRecordingFolder();

            return folder;
        }

        private string EnsureRecordingFolder()
        {
            string folder = GetRecordingFolder();

            try
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }
            catch
            {
                folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Podmix Recordings");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                Config.Save("radio_recording_folder", folder);
            }

            return folder;
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Podmix Radio";

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            return fileName.Trim();
        }

        private int GetSavedAudioOutputDevice()
        {
            try { return Convert.ToInt32(Config.Get("audio_output_device")); }
            catch { return BassDefaultDevice; }
        }

        private bool EnsureBassDeviceReady(int device)
        {
            int resolvedDevice = ResolveBassOutputDevice(device);
            if (resolvedDevice == 0)
                return false;

            if (Bass.BASS_SetDevice(resolvedDevice))
            {
                _bassInitialized = true;
                int currentDevice = Bass.BASS_GetDevice();
                if (currentDevice != 0)
                    _initializedBassDevices.Add(currentDevice);
                return true;
            }

            bool initialized = Bass.BASS_Init(resolvedDevice, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            if (initialized)
            {
                _bassInitialized = true;
                int currentDevice = Bass.BASS_GetDevice();
                if (currentDevice != 0)
                    _initializedBassDevices.Add(currentDevice);
            }

            return initialized;
        }

        private int ResolveBassOutputDevice(int device)
        {
            if (device == BassDefaultDevice)
                return GetWindowsDefaultBassDevice();

            return IsBassOutputDeviceAvailable(device) ? device : 0;
        }

        private int GetWindowsDefaultBassDevice()
        {
            for (int device = 1; ; device++)
            {
                BASS_DEVICEINFO info = Bass.BASS_GetDeviceInfo(device);
                if (info == null || info.flags == BASSDeviceInfo.BASS_DEVICE_NONE)
                    break;

                if ((info.flags & BASSDeviceInfo.BASS_DEVICE_ENABLED) != 0
                    && (info.flags & BASSDeviceInfo.BASS_DEVICE_DEFAULT) != 0)
                    return device;
            }

            return 0;
        }

        private bool IsBassOutputDeviceAvailable(int device)
        {
            if (device <= 0)
                return false;

            BASS_DEVICEINFO info = Bass.BASS_GetDeviceInfo(device);
            return info != null
                && info.flags != BASSDeviceInfo.BASS_DEVICE_NONE
                && (info.flags & BASSDeviceInfo.BASS_DEVICE_ENABLED) != 0;
        }

        private void AudioOutput_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            PopulateAudioOutputMenu();
        }

        private void PopulateAudioOutputMenu()
        {
            MenuAudioOutput.Items.Clear();

            MenuAudioOutput.Items.Add(CreateAudioOutputMenuItem("Windows Default", BassDefaultDevice));
            MenuAudioOutput.Items.Add(new Separator());

            int deviceCount = 0;
            for (int device = 1; ; device++)
            {
                BASS_DEVICEINFO info = Bass.BASS_GetDeviceInfo(device);
                if (info == null || info.flags == BASSDeviceInfo.BASS_DEVICE_NONE)
                    break;

                if ((info.flags & BASSDeviceInfo.BASS_DEVICE_ENABLED) == 0)
                    continue;

                if (string.Equals(info.name, "Default", StringComparison.OrdinalIgnoreCase))
                    continue;

                string header = info.name;
                if ((info.flags & BASSDeviceInfo.BASS_DEVICE_DEFAULT) != 0)
                    header += " (Default)";

                MenuAudioOutput.Items.Add(CreateAudioOutputMenuItem(header, device));
                deviceCount++;
            }

            if (deviceCount == 0)
            {
                MenuAudioOutput.Items.Add(new MenuItem
                {
                    Header = "No output devices found",
                    IsEnabled = false
                });
            }
        }

        private MenuItem CreateAudioOutputMenuItem(string header, int device)
        {
            var item = new MenuItem
            {
                Header = header,
                Tag = device,
                IsCheckable = true,
                IsChecked = device == _selectedBassDevice
            };

            item.Click += AudioOutputDevice_Click;
            return item;
        }

        private string GetAudioOutputDeviceDisplayName(int device)
        {
            if (device == BassDefaultDevice)
                return "Windows Default";

            BASS_DEVICEINFO info = Bass.BASS_GetDeviceInfo(device);
            if (info != null && !string.IsNullOrWhiteSpace(info.name))
                return info.name;

            return "that audio output";
        }

        private void ShowAudioOutputUnavailableMessage(int device)
        {
            MessageBox.Show(
                owner: this,
                messageBoxText: $"{GetAudioOutputDeviceDisplayName(device)} is not available right now.\n\nIt may be disconnected, disabled in Windows, or temporarily unavailable. Podmix will keep using the previous audio output.",
                caption: "Audio Output",
                button: MessageBoxButton.OK,
                icon: MessageBoxImage.Information);
        }

        private void AudioOutputDevice_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem item) || item.Tag == null)
                return;

            int device = Convert.ToInt32(item.Tag);
            ChangeAudioOutputDevice(device);
            PopulateAudioOutputMenu();
        }

        private void ChangeAudioOutputDevice(int device)
        {
            if (device == _selectedBassDevice && device != BassDefaultDevice)
                return;

            int previousDevice = _selectedBassDevice;
            _selectedBassDevice = device;

            if (!EnsureBassDeviceReady(_selectedBassDevice))
            {
                _selectedBassDevice = previousDevice;
                EnsureBassDeviceReady(_selectedBassDevice);
                ShowAudioOutputUnavailableMessage(device);
                return;
            }

            int targetDevice = Bass.BASS_GetDevice();
            bool switched = true;

            if (_localHandle != 0)
                switched &= Bass.BASS_ChannelSetDevice(_localHandle, targetDevice);

            if (_radioHandle != 0)
                switched &= Bass.BASS_ChannelSetDevice(_radioHandle, targetDevice);

            Config.Save("audio_output_device", _selectedBassDevice);
            ApplyBassVolume();

            if (!switched)
                RebuildLocalPlaybackAfterOutputSwitch(targetDevice);
        }

        private void RebuildLocalPlaybackAfterOutputSwitch(int targetDevice)
        {
            if (isRadioMode || string.IsNullOrWhiteSpace(_openedMediaPath) || !File.Exists(_openedMediaPath))
                return;

            string filePath = _openedMediaPath;
            bool resumePlayback = isPlaying;
            double positionSeconds = 0;

            if (_localHandle != 0)
            {
                long positionBytes = Bass.BASS_ChannelGetPosition(_localHandle);
                positionSeconds = Math.Max(0, Bass.BASS_ChannelBytes2Seconds(_localHandle, positionBytes));
            }

            StopLocalPlayback(false);
            Bass.BASS_SetDevice(targetDevice);

            if (!PrepareLocalPlaybackStream(filePath, false))
                return;

            if (positionSeconds > 0)
                Bass.BASS_ChannelSetPosition(_localHandle, Bass.BASS_ChannelSeconds2Bytes(_localHandle, positionSeconds));

            if (resumePlayback)
            {
                Bass.BASS_ChannelPlay(_localHandle, false);
                timer.Start();
                isPlaying = true;
                UpdateUIStatus(true);
            }
        }

        private void StopLocalPlayback(bool resetPosition)
        {
            _openedMediaPath = "";

            if (_localHandle != 0)
            {
                _suppressLocalEndSync = true;
                Bass.BASS_ChannelStop(_localHandle);
                Bass.BASS_StreamFree(_localHandle);
                _localHandle = 0;
                _suppressLocalEndSync = false;
            }

            if (resetPosition)
            {
                ProgressSlider.Value = 0;
                TimeNow.Text = "00:00";
                TimeEnd.Text = "00:00";
            }
        }

        private void FreeBassDevices()
        {
            if (_initializedBassDevices.Count > 0)
            {
                foreach (int device in _initializedBassDevices.ToList())
                {
                    if (Bass.BASS_SetDevice(device))
                        Bass.BASS_Free();
                }

                _initializedBassDevices.Clear();
            }
            else if (_bassInitialized)
            {
                Bass.BASS_Free();
            }

            _bassInitialized = false;
        }

        private bool PrepareLocalPlaybackStream(string filePath, bool showError)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            if (_localHandle != 0 && IsSamePath(_openedMediaPath, filePath))
                return true;

            try
            {
                if (!InitBass())
                    throw new InvalidOperationException($"BASS could not initialize audio output. Error: {Bass.BASS_ErrorGetCode()}");

                StopLocalPlayback(false);
                int newHandle = Bass.BASS_StreamCreateFile(filePath, 0L, 0L, BASSFlag.BASS_DEFAULT);
                if (newHandle == 0)
                    throw new InvalidOperationException($"BASS could not open this file. Error: {Bass.BASS_ErrorGetCode()}");

                _localHandle = newHandle;
                _openedMediaPath = filePath;
                _localEndSync = new SYNCPROC(LocalPlaybackEndSync);
                Bass.BASS_ChannelSetSync(_localHandle, BASSSync.BASS_SYNC_END, 0, _localEndSync, IntPtr.Zero);
                ApplyBassVolume();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Playback prepare failed for '{filePath}': {ex.Message}");
                _openedMediaPath = "";
                StopLocalPlayback(false);

                if (showError)
                {
                    MessageBox.Show(
                        owner: this,
                        messageBoxText: $"Podmix could not play {System.IO.Path.GetFileName(filePath)}.\n\n{ex.Message}",
                        caption: "Playback Error",
                        button: MessageBoxButton.OK,
                        icon: MessageBoxImage.Error);
                }

                return false;
            }
        }

        private void LocalPlaybackEndSync(int handle, int channel, int data, IntPtr user)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_suppressLocalEndSync && !isRadioMode && channel == _localHandle)
                {
                    _openedMediaPath = "";
                    AdvanceToNextTrack();
                }
            }));
        }

        private bool EnsurePlayableCurrentIndex()
        {
            if (playlist.Count == 0 || shuffleOrder.Count == 0)
                return false;

            if (currentIndex >= 0 && currentIndex < shuffleOrder.Count)
            {
                int shuffledIndex = shuffleOrder[currentIndex];
                if (shuffledIndex >= 0 && shuffledIndex < playlist.Count)
                    return true;
            }

            if (!string.IsNullOrWhiteSpace(currentFilePath))
            {
                int playlistIndex = playlist.FindIndex(file => IsSamePath(file, currentFilePath));
                int shuffleIndex = shuffleOrder.IndexOf(playlistIndex);
                if (shuffleIndex >= 0)
                {
                    currentIndex = shuffleIndex;
                    return true;
                }
            }

            currentIndex = 0;
            return shuffleOrder[0] >= 0 && shuffleOrder[0] < playlist.Count;
        }

        //private bool StartQueuedStartupPlaybackIfReady()
        //{
        //    if (!_playRequestedDuringStartup || isPlaying)
        //        return false;

        //    if (!EnsurePlayableCurrentIndex())
        //        return false;

        //    _playRequestedDuringStartup = false;
        //    PlayCurrent();
        //    return true;
        //}

        private async Task<bool> StartQueuedStartupPlaybackIfReadyAsync()
        {
            if (!_playRequestedDuringStartup || isPlaying)
                return false;

            if (!EnsurePlayableCurrentIndex())
                return false;

            _playRequestedDuringStartup = false;
            await PlayCurrentAsync();
            return true;
        }

        private string GetStartupAudioFilePath()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length > 1 && File.Exists(args[1]) && IsSupportedAudioFile(args[1]))
                    return args[1];
            }
            catch { }

            return null;
        }

        private bool IsPasscodeRequired()
        {
            try
            {
                return (bool)Config.Get("lock_requires_passcode")
                    && !string.IsNullOrWhiteSpace((string)Config.Get("lock_passcode_hash"));
            }
            catch
            {
                return false;
            }
        }

        private bool IsDeviceInputBlocked()
        {
            return _isDevicePoweredOff || _isDeviceLocked;
        }

        private int GetScreenSleepMinutes()
        {
            try
            {
                return Math.Max(0, Convert.ToInt32(Config.Get("screen_sleep_minutes")));
            }
            catch
            {
                return 5;
            }
        }

        private void ResetScreenSleepTimer(bool restoreBrightness = true)
        {
            if (_isDevicePoweredOff)
                return;

            _lastScreenActivityUtc = DateTime.UtcNow;
            if (restoreBrightness)
                CancelScreenDimming(false);

            if (!_screenSleepTimer.IsEnabled)
                _screenSleepTimer.Start();
        }

        private void CheckScreenSleepTimeout()
        {
            if (_isDevicePoweredOff)
            {
                _screenSleepTimer.Stop();
                return;
            }

            int minutes = GetScreenSleepMinutes();

            TimeSpan timeout;
            if (_isDeviceLocked)
            {
                timeout = TimeSpan.FromSeconds(LockScreenSleepSeconds);
            }
            else if (minutes > 0)
            {
                timeout = TimeSpan.FromMinutes(minutes);
            }
            else
            {
                CancelScreenDimming(false);
                return;
            }

            TimeSpan idle = DateTime.UtcNow - _lastScreenActivityUtc;
            if (idle >= timeout)
            {
                TurnScreenOff();
                return;
            }

            TimeSpan dimLead = TimeSpan.FromSeconds(Math.Min(ScreenDimLeadSeconds, Math.Max(2, timeout.TotalSeconds * 0.25)));
            if (timeout - idle <= dimLead)
                StartScreenDimming();
            else
                CancelScreenDimming(false);
        }

        private void StartScreenDimming()
        {
            if (_isScreenDimmed || _isDevicePoweredOff)
                return;

            _isScreenDimmed = true;
            ScreenDimOverlay.Visibility = Visibility.Visible;
            ScreenDimOverlay.BeginAnimation(UIElement.OpacityProperty, null);

            var fadeIn = new DoubleAnimation
            {
                To = ScreenDimOpacity,
                Duration = TimeSpan.FromMilliseconds(900),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ScreenDimOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void CancelScreenDimming(bool immediate)
        {
            if (ScreenDimOverlay == null)
                return;

            ScreenDimOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            _isScreenDimmed = false;

            if (immediate || ScreenDimOverlay.Visibility != Visibility.Visible)
            {
                ScreenDimOverlay.Opacity = 0;
                ScreenDimOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, e) =>
            {
                if (!_isScreenDimmed)
                    ScreenDimOverlay.Visibility = Visibility.Collapsed;
            };

            ScreenDimOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private bool HandleCenterButtonWhileLocked()
        {
            if (_isDevicePoweredOff)
                return true;

            if (!_isDeviceLocked) return false;

            TogglePlaybackFromLockScreen();
            ResetScreenSleepTimer();
            return true;
        }

        private void TogglePlaybackFromLockScreen()
        {
            TogglePlayback(false);
        }

        private void EnsureDeviceScreenVisible()
        {
            if (IpodScreen.Visibility == Visibility.Collapsed)
                ToggleDetailedView_Click(null, null);
        }

        private void TurnScreenOff_Click(object sender, RoutedEventArgs e)
        {
            EnsureDeviceScreenVisible();
            TurnScreenOff();
        }

        private void TurnScreenOff()
        {
            if (_passcodeMode != PasscodeMode.Unlock && string.IsNullOrWhiteSpace((string)Config.Get("lock_passcode_hash")))
            {
                _suppressPasscodeMenuEvent = true;
                MenuRequirePasscode.IsChecked = false;
                _suppressPasscodeMenuEvent = false;
                Config.Save("lock_requires_passcode", false);
            }

            CancelScreenDimming(true);
            _isDevicePoweredOff = true;
            _isDeviceLocked = false;
            _passcodeEntry = string.Empty;
            _pendingNewPasscode = string.Empty;
            _passcodeMode = PasscodeMode.Unlock;
            _isPasscodeEntryVisible = false;
            _passcodeFlowStartedWhileLocked = false;

            DeviceOverlay.Visibility = Visibility.Visible;
            DeviceOffView.Visibility = Visibility.Visible;
            AnimateLockScreenToBlack();
            _lockClockTimer.Stop();
            _screenSleepTimer.Stop();

            DeviceOverlay.Focus();
        }

        private void LockScreen_Click(object sender, RoutedEventArgs e)
        {
            ShowLockScreen();
        }

        private void WakeDevice()
        {
            _isDevicePoweredOff = false;
            ShowLockScreen();
        }

        private void ShowLockScreen()
        {
            EnsureDeviceScreenVisible();
            DeviceOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOverlay.Opacity = 1;

            _isDevicePoweredOff = false;
            _isDeviceLocked = true;
            _passcodeEntry = string.Empty;
            _pendingNewPasscode = string.Empty;
            _passcodeMode = PasscodeMode.Unlock;
            _isPasscodeEntryVisible = false;
            _passcodeFlowStartedWhileLocked = false;

            DeviceOverlay.Visibility = Visibility.Visible;
            DeviceOffView.Opacity = 1;
            DeviceOffView.Visibility = Visibility.Collapsed;
            LockScreenView.Visibility = Visibility.Visible;

            ShowLockGlance();
            BtnPasscodeCancel.Visibility = Visibility.Collapsed;
            TxtLockHint.Text = IsPasscodeRequired() ? "Tap screen to enter passcode" : "Tap screen to unlock";

            UpdatePasscodeDots();
            UpdateLockScreenInfo();
            _lockClockTimer.Start();
            ResetScreenSleepTimer();
            AnimateLockScreenIn();
            DeviceOverlay.Focus();
        }

        private void UnlockDevice()
        {
            if (_isAppJustStarted) _isAppJustStarted = false;
            _isDevicePoweredOff = false;
            _isDeviceLocked = false;
            _passcodeEntry = string.Empty;
            _pendingNewPasscode = string.Empty;
            _passcodeMode = PasscodeMode.Unlock;
            _isPasscodeEntryVisible = false;
            _passcodeFlowStartedWhileLocked = false;

            _lockClockTimer.Stop();
            AnimateUnlockToPlayer();
            ResetScreenSleepTimer();
        }

        private void UpdateLockScreenInfo()
        {
            TxtLockTime.Text = DateTime.Now.ToString("h:mm");
            TxtLockLargeTime.Text = DateTime.Now.ToString("h:mm");
            TxtLockDate.Text = DateTime.Now.ToString("ddd, MMM d");
            UpdateLockNowPlayingInfo();
        }

        private void UpdateLockNowPlayingInfo()
        {
            string title = (TxtTitle.Text ?? string.Empty).Trim();
            string artist = (TxtArtist.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
            {
                TxtLockNowPlaying.Text = string.Empty;
                TxtLockNowPlaying.Visibility = Visibility.Collapsed;
                return;
            }

            if (string.IsNullOrWhiteSpace(title))
                TxtLockNowPlaying.Text = artist;
            else if (string.IsNullOrWhiteSpace(artist))
                TxtLockNowPlaying.Text = title;
            else
                TxtLockNowPlaying.Text = $"{title} - {artist}";

            TxtLockNowPlaying.Visibility = _isDeviceLocked && !_isPasscodeEntryVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ShowLockGlance()
        {
            _isPasscodeEntryVisible = false;
            PasscodePanel.BeginAnimation(UIElement.OpacityProperty, null);
            TxtLockTime.BeginAnimation(UIElement.OpacityProperty, null);
            TxtLockLargeTime.BeginAnimation(UIElement.OpacityProperty, null);
            TxtLockHint.BeginAnimation(UIElement.OpacityProperty, null);
            PasscodePanel.Opacity = 1;
            TxtLockTime.Opacity = 1;
            TxtLockLargeTime.Opacity = 1;
            TxtLockHint.Opacity = 1;
            PasscodePanel.Visibility = Visibility.Collapsed;
            TxtLockTime.Visibility = Visibility.Collapsed;
            TxtLockLargeTime.Visibility = Visibility.Visible;
            BtnPasscodeCancel.Visibility = Visibility.Collapsed;
            UpdateLockNowPlayingInfo();
        }

        private void ShowPasscodeEntry(bool showCancel)
        {
            _isPasscodeEntryVisible = true;
            PasscodePanel.Visibility = Visibility.Visible;
            TxtLockTime.Visibility = Visibility.Visible;
            TxtLockLargeTime.Visibility = Visibility.Collapsed;
            TxtLockNowPlaying.Visibility = Visibility.Collapsed;
            BtnPasscodeCancel.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
            TxtLockHint.Text = GetPasscodePrompt();
            UpdatePasscodeDots();
            ResetScreenSleepTimer();
            AnimatePasscodeEntry();
        }

        private void AnimateLockScreenIn()
        {
            LockScreenView.BeginAnimation(UIElement.OpacityProperty, null);
            LockScreenView.Opacity = 0;
            AnimateOpacity(LockScreenView, 1, 180);
            AnimateTranslateY(TxtLockLargeTime, -8, 0, 220);
        }

        private void AnimateLockScreenToBlack()
        {
            LockScreenView.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOffView.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOverlay.Opacity = 1;

            if (LockScreenView.Visibility != Visibility.Visible)
            {
                LockScreenView.Visibility = Visibility.Collapsed;
                LockScreenView.Opacity = 1;
                DeviceOffView.Opacity = 1;
                return;
            }

            DeviceOffView.Opacity = 1;

            var fadeOut = CreateEaseAnimation(0, 160);
            fadeOut.Completed += (s, e) =>
            {
                if (_isDevicePoweredOff)
                {
                    LockScreenView.Visibility = Visibility.Collapsed;
                    LockScreenView.Opacity = 1;
                }
            };

            LockScreenView.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void AnimatePasscodeEntry()
        {
            PasscodePanel.BeginAnimation(UIElement.OpacityProperty, null);
            TxtLockTime.BeginAnimation(UIElement.OpacityProperty, null);
            TxtLockHint.BeginAnimation(UIElement.OpacityProperty, null);

            PasscodePanel.Opacity = 0;
            TxtLockTime.Opacity = 0;
            TxtLockHint.Opacity = 0.65;

            AnimateOpacity(PasscodePanel, 1, 180);
            AnimateOpacity(TxtLockTime, 1, 160);
            AnimateOpacity(TxtLockHint, 1, 160);
            AnimateTranslateY(PasscodePanel, 12, 0, 220);
        }

        private void AnimateUnlockToPlayer()
        {
            DeviceOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOffView.BeginAnimation(UIElement.OpacityProperty, null);
            LockScreenView.BeginAnimation(UIElement.OpacityProperty, null);

            var fadeOut = CreateEaseAnimation(0, 180);
            fadeOut.Completed += (s, e) =>
            {
                if (!_isDeviceLocked && !_isDevicePoweredOff)
                {
                    DeviceOverlay.Visibility = Visibility.Collapsed;
                    DeviceOffView.Visibility = Visibility.Collapsed;
                    LockScreenView.Visibility = Visibility.Collapsed;
                    DeviceOverlay.Opacity = 1;
                    LockScreenView.Opacity = 1;
                    PasscodePanel.Opacity = 1;
                    TxtLockTime.Opacity = 1;
                    TxtLockHint.Opacity = 1;
                }
            };

            DeviceOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void AnimateOpacity(UIElement element, double to, int milliseconds)
        {
            element.BeginAnimation(UIElement.OpacityProperty, CreateEaseAnimation(to, milliseconds));
        }

        private void AnimateTranslateY(FrameworkElement element, double from, double to, int milliseconds)
        {
            var transform = element.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            element.RenderTransformOrigin = new Point(0.5, 0.5);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = from;
            transform.BeginAnimation(TranslateTransform.YProperty, CreateEaseAnimation(to, milliseconds));
        }

        private DoubleAnimation CreateEaseAnimation(double to, int milliseconds)
        {
            return new DoubleAnimation
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }

        private void StartBootViewIntro()
        {
            _bootViewIntroStartedAt = DateTime.UtcNow;

            TxtBootTitle.Text = "Podmix";
            TxtBootSubtitle.Text = "Welcome";
            BootView.Visibility = Visibility.Visible;
            StartBootLoadingAnimation();
            BootView.BeginAnimation(UIElement.OpacityProperty, null);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            BootView.Opacity = 1;
            BootViewScale.ScaleX = 0.92;
            BootViewScale.ScaleY = 0.92;

            var scaleIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(850),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BootViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn.Clone());
        }

        private async void HideBootViewToPlayer()
        {
            if (BootView.Visibility != Visibility.Visible)
                return;

            // Ensure the welcome screen remains visible for at least 1.8 seconds.
            int elapsedMs = (int)(DateTime.UtcNow - _bootViewIntroStartedAt).TotalMilliseconds;
            int remainingMs = StartupBootMinimumVisibleMs - elapsedMs;

            if (remainingMs > 0)
                await Task.Delay(remainingMs);

            // Check again in case the boot view was hidden while waiting.
            if (BootView.Visibility != Visibility.Visible)
                return;

            RevealStartupDevice();
            ShowLockScreen();

            BootView.BeginAnimation(UIElement.OpacityProperty, null);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            var fadeOut = new DoubleAnimation
            {
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(StartupBootExitDelayMs),
                Duration = TimeSpan.FromMilliseconds(StartupBootExitDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                StopBootLoadingAnimation();
                BootView.Visibility = Visibility.Collapsed;
                BootView.Opacity = 0;
                BootViewScale.ScaleX = 1;
                BootViewScale.ScaleY = 1;
            };

            var scaleOut = new DoubleAnimation
            {
                To = 1.04,
                BeginTime = TimeSpan.FromMilliseconds(StartupBootExitDelayMs),
                Duration = TimeSpan.FromMilliseconds(StartupBootExitDurationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            BootView.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut.Clone());
        }

        private void RevealStartupWindow()
        {
            if (_startupWindowRevealed)
                return;

            _startupWindowRevealed = true;

            // Close the splash screen.
            if (_splashWindow != null)
            {
                _splashWindow.StopLoadingTimer();
                _splashWindow.Close();
                _splashWindow = null;
            }

            ShowInTaskbar = true;
            IsHitTestVisible = true;

            // Start the welcome animation only when the main window
            // is ready to become visible.
            StartBootViewIntro();

            if (!_delayStartupWindowRevealForAlbumColor)
            {
                Opacity = 1;
                return;
            }

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            BeginAnimation(Window.OpacityProperty, fadeIn);
        }

        private async Task ShowShutdownViewAsync()
        {
            EnsureDeviceScreenVisible();
            DeviceOverlay.Visibility = Visibility.Collapsed;
            DeviceOffView.Visibility = Visibility.Collapsed;
            LockScreenView.Visibility = Visibility.Collapsed;

            TxtBootTitle.Text = "Podmix";
            TxtBootSubtitle.Text = "Goodbye";
            BootView.Visibility = Visibility.Visible;
            StartBootLoadingAnimation();
            BootView.BeginAnimation(UIElement.OpacityProperty, null);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            BootView.Opacity = 0;
            BootViewScale.ScaleX = 1.04;
            BootViewScale.ScaleY = 1.04;

            var fadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var scaleIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BootView.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            BootViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn.Clone());

            await Task.Delay(1050);
        }

        private void StartBootLoadingAnimation()
        {
            EnsureBootLoadingGifFrames();

            if (_bootGifFrames.Count == 0)
                return;

            _bootGifFrameIndex = 0;
            ImgBootLoading.Source = _bootGifFrames[0];

            if (_bootGifFrames.Count == 1)
                return;

            if (_bootGifTimer == null)
            {
                _bootGifTimer = new DispatcherTimer();
                _bootGifTimer.Tick += BootGifTimer_Tick;
            }

            _bootGifTimer.Interval = TimeSpan.FromMilliseconds(GetBootGifFrameDelay(0));
            _bootGifTimer.Start();
        }

        private void StopBootLoadingAnimation()
        {
            if (_bootGifTimer != null)
                _bootGifTimer.Stop();
        }

        private void BootGifTimer_Tick(object sender, EventArgs e)
        {
            if (_bootGifFrames.Count == 0)
                return;

            _bootGifFrameIndex = (_bootGifFrameIndex + 1) % _bootGifFrames.Count;
            ImgBootLoading.Source = _bootGifFrames[_bootGifFrameIndex];

            if (_bootGifTimer != null)
                _bootGifTimer.Interval = TimeSpan.FromMilliseconds(GetBootGifFrameDelay(_bootGifFrameIndex));
        }

        private int GetBootGifFrameDelay(int frameIndex)
        {
            if (frameIndex >= 0 && frameIndex < _bootGifFrameDelays.Count)
                return _bootGifFrameDelays[frameIndex];

            return 90;
        }

        private void EnsureBootLoadingGifFrames()
        {
            if (_bootGifFrames.Count > 0)
                return;

            string gifPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "loading.gif");
            if (!File.Exists(gifPath))
                return;

            try
            {
                using (var stream = File.OpenRead(gifPath))
                {
                    var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

                    foreach (BitmapFrame frame in decoder.Frames)
                    {
                        var frameSource = frame;
                        if (frameSource.CanFreeze)
                            frameSource.Freeze();

                        _bootGifFrames.Add(frameSource);
                        _bootGifFrameDelays.Add(GetGifFrameDelay(frame));
                    }
                }
            }
            catch
            {
                _bootGifFrames.Clear();
                _bootGifFrameDelays.Clear();
            }
        }

        private int GetGifFrameDelay(BitmapFrame frame)
        {
            var metadata = frame.Metadata as BitmapMetadata;
            if (metadata == null || !metadata.ContainsQuery("/grctlext/Delay"))
                return 90;

            try
            {
                int delay = Convert.ToInt32(metadata.GetQuery("/grctlext/Delay")) * 10;
                return Math.Max(20, delay);
            }
            catch
            {
                return 90;
            }
        }

        private void DeviceOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDevicePoweredOff)
            {
                WakeDevice();
                e.Handled = true;
                return;
            }

            if (_isDeviceLocked && !IsPasscodeRequired() && _passcodeMode == PasscodeMode.Unlock)
            {
                UnlockDevice();
                e.Handled = true;
                return;
            }

            if (_isDeviceLocked && IsPasscodeRequired() && _passcodeMode == PasscodeMode.Unlock && !_isPasscodeEntryVisible)
            {
                _passcodeEntry = string.Empty;
                ShowPasscodeEntry(false);
                e.Handled = true;
                return;
            }

            ResetScreenSleepTimer();
        }

        private void ChangeLockWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                if (!Directory.Exists(LockWallpaperFolder))
                    Directory.CreateDirectory(LockWallpaperFolder);

                foreach (string oldWallpaper in Directory.GetFiles(LockWallpaperFolder, LockWallpaperSearchPattern))
                {
                    try { File.Delete(oldWallpaper); } catch { }
                }

                string extension = System.IO.Path.GetExtension(dialog.FileName);
                string destination = System.IO.Path.Combine(LockWallpaperFolder, "lock_wallpaper" + extension);
                File.Copy(dialog.FileName, destination, true);
                LoadLockWallpaper();
            }
            catch
            {
                MessageBox.Show(this, "Could not save the lock screen wallpaper.", "Wallpaper", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLockWallpaper_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(LockWallpaperFolder))
                {
                    foreach (string wallpaper in Directory.GetFiles(LockWallpaperFolder, LockWallpaperSearchPattern))
                    {
                        try { File.Delete(wallpaper); } catch { }
                    }
                }

                ImgLockWallpaper.Source = null;
            }
            catch { }
        }

        private void LoadLockWallpaper()
        {
            try
            {
                if (!Directory.Exists(LockWallpaperFolder))
                {
                    ImgLockWallpaper.Source = null;
                    return;
                }

                string wallpaperPath = Directory.GetFiles(LockWallpaperFolder, LockWallpaperSearchPattern).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(wallpaperPath) || !File.Exists(wallpaperPath))
                {
                    ImgLockWallpaper.Source = null;
                    return;
                }

                var image = new BitmapImage();
                using (var stream = new FileStream(wallpaperPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                }

                ImgLockWallpaper.Source = image;
            }
            catch
            {
                ImgLockWallpaper.Source = null;
            }
        }

        private void PasscodeDigit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
                AppendPasscodeDigit(button.Tag.ToString());
        }

        private void PasscodeBackspace_Click(object sender, RoutedEventArgs e)
        {
            RemovePasscodeDigit();
        }

        private void PasscodeOk_Click(object sender, RoutedEventArgs e)
        {
            ProcessPasscodeEntry();
        }

        private void PasscodeCancel_Click(object sender, RoutedEventArgs e)
        {
            CancelPasscodeFlow();
        }

        private void AppendPasscodeDigit(string digit)
        {
            if (!_isDeviceLocked || !IsPasscodePanelActive() || _passcodeEntry.Length >= DevicePasscodeLength) return;

            _passcodeEntry += digit;
            TxtLockHint.Text = GetPasscodePrompt();
            UpdatePasscodeDots();
            ResetScreenSleepTimer();

            if (_passcodeEntry.Length == DevicePasscodeLength)
                ProcessPasscodeEntry();
        }

        private void RemovePasscodeDigit()
        {
            if (_passcodeEntry.Length == 0) return;

            _passcodeEntry = _passcodeEntry.Substring(0, _passcodeEntry.Length - 1);
            TxtLockHint.Text = GetPasscodePrompt();
            UpdatePasscodeDots();
            ResetScreenSleepTimer();
        }

        private bool IsPasscodePanelActive()
        {
            return PasscodePanel.Visibility == Visibility.Visible
                && (IsPasscodeRequired() || _passcodeMode != PasscodeMode.Unlock);
        }

        private void ProcessPasscodeEntry()
        {
            if (_passcodeEntry.Length != DevicePasscodeLength)
                return;

            ResetScreenSleepTimer();

            switch (_passcodeMode)
            {
                case PasscodeMode.Unlock:
                    if (HashPasscode(_passcodeEntry) == (string)Config.Get("lock_passcode_hash"))
                    {
                        UnlockDevice();
                    }
                    else
                    {
                        RejectPasscode("Wrong passcode");
                    }
                    break;

                case PasscodeMode.VerifyCurrentForChange:
                    if (HashPasscode(_passcodeEntry) == (string)Config.Get("lock_passcode_hash"))
                    {
                        StartPasscodeFlow(PasscodeMode.SetNew);
                    }
                    else
                    {
                        RejectPasscode("Wrong passcode");
                    }
                    break;

                case PasscodeMode.VerifyCurrentToDisable:
                    if (HashPasscode(_passcodeEntry) == (string)Config.Get("lock_passcode_hash"))
                    {
                        Config.Save("lock_requires_passcode", false);
                        _suppressPasscodeMenuEvent = true;
                        MenuRequirePasscode.IsChecked = false;
                        _suppressPasscodeMenuEvent = false;
                        FinishPasscodeSettingsFlow();
                    }
                    else
                    {
                        RejectPasscode("Wrong passcode");
                    }
                    break;

                case PasscodeMode.SetNew:
                    _pendingNewPasscode = _passcodeEntry;
                    _passcodeEntry = string.Empty;
                    _passcodeMode = PasscodeMode.ConfirmNew;
                    TxtLockHint.Text = GetPasscodePrompt();
                    UpdatePasscodeDots();
                    break;

                case PasscodeMode.ConfirmNew:
                    if (_passcodeEntry == _pendingNewPasscode)
                    {
                        Config.Save("lock_passcode_hash", HashPasscode(_passcodeEntry));
                        Config.Save("lock_requires_passcode", true);
                        _suppressPasscodeMenuEvent = true;
                        MenuRequirePasscode.IsChecked = true;
                        _suppressPasscodeMenuEvent = false;
                        FinishPasscodeSettingsFlow();
                    }
                    else
                    {
                        _pendingNewPasscode = string.Empty;
                        _passcodeEntry = string.Empty;
                        _passcodeMode = PasscodeMode.SetNew;
                        TxtLockHint.Text = "Passcodes did not match";
                        ShowPasscodeEntry(true);
                        UpdatePasscodeDots();
                        TriggerShake();
                    }
                    break;
            }
        }

        private void FinishPasscodeSettingsFlow()
        {
            bool returnToLockGlance = _passcodeFlowStartedWhileLocked && IsPasscodeRequired();

            _passcodeEntry = string.Empty;
            _pendingNewPasscode = string.Empty;
            _passcodeMode = PasscodeMode.Unlock;
            _isPasscodeEntryVisible = false;
            _passcodeFlowStartedWhileLocked = false;

            if (!returnToLockGlance)
            {
                UnlockDevice();
                return;
            }

            _isDevicePoweredOff = false;
            _isDeviceLocked = true;
            DeviceOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOverlay.Opacity = 1;
            DeviceOverlay.Visibility = Visibility.Visible;
            DeviceOffView.Visibility = Visibility.Collapsed;
            LockScreenView.Visibility = Visibility.Visible;
            ShowLockGlance();
            TxtLockHint.Text = "Tap screen to enter passcode";
            UpdatePasscodeDots();
            UpdateLockScreenInfo();
            _lockClockTimer.Start();
            ResetScreenSleepTimer();
            DeviceOverlay.Focus();
        }

        private void RejectPasscode(string message)
        {
            _passcodeEntry = string.Empty;
            TxtLockHint.Text = message;
            UpdatePasscodeDots();
            TriggerShake();
        }

        private void UpdatePasscodeDots()
        {
            var dotBrushes = new List<Brush>();
            for (int i = 0; i < DevicePasscodeLength; i++)
            {
                dotBrushes.Add(i < _passcodeEntry.Length
                    ? Brushes.White
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#44FFFFFF")));
            }

            PasscodeDots.ItemsSource = dotBrushes;
        }

        private string GetPasscodePrompt()
        {
            switch (_passcodeMode)
            {
                case PasscodeMode.VerifyCurrentForChange:
                    return "Enter current passcode";
                case PasscodeMode.VerifyCurrentToDisable:
                    return "Confirm passcode to turn off";
                case PasscodeMode.SetNew:
                    return "Enter new passcode";
                case PasscodeMode.ConfirmNew:
                    return "Confirm new passcode";
                default:
                    return "Enter passcode";
            }
        }

        private void StartPasscodeFlow(PasscodeMode mode)
        {
            if (_passcodeMode == PasscodeMode.Unlock)
                _passcodeFlowStartedWhileLocked = _isDeviceLocked;

            EnsureDeviceScreenVisible();

            DeviceOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOffView.BeginAnimation(UIElement.OpacityProperty, null);
            LockScreenView.BeginAnimation(UIElement.OpacityProperty, null);
            DeviceOverlay.Opacity = 1;
            DeviceOffView.Opacity = 1;
            LockScreenView.Opacity = 1;

            _isDevicePoweredOff = false;
            _isDeviceLocked = true;
            _passcodeMode = mode;
            _passcodeEntry = string.Empty;
            if (mode == PasscodeMode.SetNew)
                _pendingNewPasscode = string.Empty;

            DeviceOverlay.Visibility = Visibility.Visible;
            DeviceOffView.Visibility = Visibility.Collapsed;
            LockScreenView.Visibility = Visibility.Visible;
            ShowPasscodeEntry(mode != PasscodeMode.Unlock);
            TxtLockHint.Text = GetPasscodePrompt();

            UpdatePasscodeDots();
            UpdateLockScreenInfo();
            _lockClockTimer.Start();
            ResetScreenSleepTimer();
            DeviceOverlay.Focus();
        }

        private void CancelPasscodeFlow()
        {
            if (_passcodeMode != PasscodeMode.Unlock)
            {
                bool returnToLockGlance = _passcodeFlowStartedWhileLocked && IsPasscodeRequired();

                if (string.IsNullOrWhiteSpace((string)Config.Get("lock_passcode_hash")))
                {
                    _suppressPasscodeMenuEvent = true;
                    MenuRequirePasscode.IsChecked = false;
                    _suppressPasscodeMenuEvent = false;
                    Config.Save("lock_requires_passcode", false);
                }

                _passcodeEntry = string.Empty;
                _pendingNewPasscode = string.Empty;
                _passcodeMode = PasscodeMode.Unlock;
                _isPasscodeEntryVisible = false;
                _passcodeFlowStartedWhileLocked = false;

                if (returnToLockGlance)
                {
                    _isDevicePoweredOff = false;
                    _isDeviceLocked = true;
                    DeviceOverlay.BeginAnimation(UIElement.OpacityProperty, null);
                    DeviceOverlay.Opacity = 1;
                    DeviceOverlay.Visibility = Visibility.Visible;
                    DeviceOffView.Visibility = Visibility.Collapsed;
                    LockScreenView.Visibility = Visibility.Visible;
                    ShowLockGlance();
                    TxtLockHint.Text = "Tap screen to enter passcode";
                    UpdatePasscodeDots();
                    UpdateLockScreenInfo();
                    _lockClockTimer.Start();
                    ResetScreenSleepTimer();
                    DeviceOverlay.Focus();
                    return;
                }

                UnlockDevice();
            }
        }

        private void MenuRequirePasscode_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressPasscodeMenuEvent) return;

            if (string.IsNullOrWhiteSpace((string)Config.Get("lock_passcode_hash")))
            {
                StartPasscodeFlow(PasscodeMode.SetNew);
                return;
            }

            Config.Save("lock_requires_passcode", true);
        }

        private void MenuRequirePasscode_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressPasscodeMenuEvent) return;

            if (string.IsNullOrWhiteSpace((string)Config.Get("lock_passcode_hash")))
            {
                Config.Save("lock_requires_passcode", false);
                return;
            }

            _suppressPasscodeMenuEvent = true;
            MenuRequirePasscode.IsChecked = true;
            _suppressPasscodeMenuEvent = false;
            StartPasscodeFlow(PasscodeMode.VerifyCurrentToDisable);
        }

        private void ChangePasscode_Click(object sender, RoutedEventArgs e)
        {
            StartPasscodeFlow(string.IsNullOrWhiteSpace((string)Config.Get("lock_passcode_hash"))
                ? PasscodeMode.SetNew
                : PasscodeMode.VerifyCurrentForChange);
        }

        private void ScreenSleep_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem selectedItem) || selectedItem.Tag == null) return;

            if (!int.TryParse(selectedItem.Tag.ToString(), out int minutes))
                return;

            Config.Save("screen_sleep_minutes", Math.Max(0, minutes));
            UpdateScreenSleepMenuChecks();
            ResetScreenSleepTimer();
        }

        private void UpdateScreenSleepMenuChecks()
        {
            int selectedMinutes = GetScreenSleepMinutes();

            foreach (var item in MenuScreenSleep.Items)
            {
                if (item is MenuItem menuItem && menuItem.Tag != null)
                    menuItem.IsChecked = int.TryParse(menuItem.Tag.ToString(), out int minutes) && minutes == selectedMinutes;
            }
        }

        private string HashPasscode(string passcode)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("podmix:" + passcode));
                return Convert.ToBase64String(bytes);
            }
        }

        private string DigitFromKey(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
                return ((int)(key - Key.D0)).ToString();

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return ((int)(key - Key.NumPad0)).ToString();

            return null;
        }

        // 1. Double Click Toggle
        // NO ANIMATION
        //private void ArtworkContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    if (isRadioMode || e.ClickCount != 2) return;

        //    if (RatingOverlay.Visibility == Visibility.Collapsed)
        //    {
        //        // SHOW RATING
        //        RatingOverlay.Visibility = Visibility.Visible;
        //        ArtBlur.Radius = 60;

        //        // Hide metadata and controls
        //        ToggleMetadataVisibility(Visibility.Collapsed);

        //        UpdateStarsUI(GetRating(currentFilePath));
        //    }
        //    else
        //    {
        //        // HIDE RATING
        //        RatingOverlay.Visibility = Visibility.Collapsed;
        //        ArtBlur.Radius = 0;

        //        // Show metadata and controls again
        //        ToggleMetadataVisibility(Visibility.Visible);
        //    }
        //}

        // SIMPLE ANIMATION
        ////////private void ArtworkContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        ////////{
        ////////    if (isRadioMode || e.ClickCount != 2) return;

        ////////    if (RatingOverlay.Visibility == Visibility.Collapsed)
        ////////    {
        ////////        // SHOW
        ////////        RatingOverlay.Opacity = 0;
        ////////        RatingOverlay.Visibility = Visibility.Visible;

        ////////        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
        ////////        RatingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        ////////        ArtBlur.Radius = 60;
        ////////        ToggleMetadataVisibility(Visibility.Collapsed);
        ////////        UpdateStarsUI(GetRating(currentFilePath));
        ////////    }
        ////////    else
        ////////    {
        ////////        // HIDE
        ////////        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        ////////        fadeOut.Completed += (s, _) =>
        ////////        {
        ////////            RatingOverlay.Visibility = Visibility.Collapsed;
        ////////            RatingOverlay.Opacity = 1; // reset
        ////////        };

        ////////        RatingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);

        ////////        ArtBlur.Radius = 0;
        ////////        ToggleMetadataVisibility(Visibility.Visible);
        ////////    }
        ////////}


        // COMPLEX ANIMATION
        private void ArtworkContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isRadioMode || e.ClickCount != 2) return;

            if (RatingOverlay.Visibility == Visibility.Collapsed)
            {
                ShowRatingOverlay();
            }
            else
            {
                HideRatingOverlay();
            }
        }

        private void ShowRatingOverlay()
        {
            // Start invisible
            RatingOverlay.Opacity = 0;
            RatingOverlay.Visibility = Visibility.Visible;

            // Fade in overlay
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            RatingOverlay.BeginAnimation(Border.OpacityProperty, fadeIn);

            // Blur in background
            var blurIn = new DoubleAnimation(60, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ArtBlur.BeginAnimation(BlurEffect.RadiusProperty, blurIn);

            // Hide metadata
            ToggleMetadataVisibility(Visibility.Collapsed);

            // Update stars
            UpdateStarsUI(GetRating(currentFilePath));
        }


        private void HideRatingOverlay()
        {
            // Fade out overlay
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, _) =>
            {
                RatingOverlay.Visibility = Visibility.Collapsed;
                ToggleMetadataVisibility(Visibility.Visible);
            };

            RatingOverlay.BeginAnimation(Border.OpacityProperty, fadeOut);

            // Blur out background
            var blurOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ArtBlur.BeginAnimation(BlurEffect.RadiusProperty, blurOut);
        }

        private void HideRatingOverlayImmediately()
        {
            if (RatingOverlay.Visibility == Visibility.Collapsed)
                return;

            RatingOverlay.BeginAnimation(Border.OpacityProperty, null);
            ArtBlur.BeginAnimation(BlurEffect.RadiusProperty, null);

            RatingOverlay.Opacity = 0;
            RatingOverlay.Visibility = Visibility.Collapsed;
            ArtBlur.Radius = 0;
            ToggleMetadataVisibility(Visibility.Visible);
        }

        private void ShowLiveVolume()
        {
            if (_volumeHideTimer == null)
            {
                _volumeHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
                _volumeHideTimer.Tick += (s, e) =>
                {
                    _volumeHideTimer.Stop();
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    fadeOut.Completed += (_, __) => VolumeOverlay.Visibility = Visibility.Collapsed;
                    VolumeOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
            }

            int vol = (int)Math.Round(_playerVolume * 100);
            TxtVolumeIcon.Text = MenuMute.IsChecked || vol == 0 ? "MUTE" : "VOL";
            TxtVolumePercent.Text = MenuMute.IsChecked ? "Muted" : $"{vol}%";

            VolumeBarTrack.UpdateLayout();
            double trackWidth = Math.Max(0, VolumeBarTrack.ActualWidth);
            VolumeBarFill.Width = trackWidth * (vol / 100.0);

            VolumeOverlay.Visibility = Visibility.Visible;
            VolumeOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            VolumeOverlay.Opacity = 1;

            _volumeHideTimer.Stop();
            _volumeHideTimer.Start();
        }

        private void ToggleMetadataVisibility(Visibility visibility)
        {
            TxtTitle.Visibility = visibility;
            TxtArtist.Visibility = visibility;
            TxtAlbum.Visibility = visibility;
            ProgressSlider.Visibility = visibility;
            TimeNow.Visibility = visibility;
            TimeEnd.Visibility = visibility;
            TxtCount.Visibility = visibility;

            // Also hide/show the search and playlist buttons for avoiding interruption
            BtnShowSearch.Visibility = visibility;
            BtnTogglePlaylists.Visibility = visibility;
        }


        // 2. Star Interaction
        private void RateStar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag.ToString(), out int rating))
            {
                _ratings[currentFilePath] = rating;
                SaveRatings();
                UpdateStarsUI(rating);
            }
        }

        private void UpdateStarsUI(int rating)
        {
            var stars = RatingOverlay.FindVisualChildren<Button>().ToList();

            for (int i = 0; i < stars.Count; i++)
            {
                int starValue = i + 1;

                // Force template to apply
                stars[i].ApplyTemplate();

                var whiteLayer = stars[i].Template.FindName("WhiteStar", stars[i]) as TextBlock;
                if (whiteLayer == null)
                    continue; // safety guard

                double targetOpacity = starValue <= rating ? 1 : 0;

                var anim = new DoubleAnimation
                {
                    To = targetOpacity,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                whiteLayer.BeginAnimation(TextBlock.OpacityProperty, anim);
            }
        }

        private int GetRating(string path) => _ratings.ContainsKey(path) ? _ratings[path] : 0;

        private bool IsSupportedAudioFile(string path)
        {
            return AudioFileSupport.IsSupportedAudioFile(path);
        }

        private bool IsSamePath(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return false;

            try
            {
                string normalizedFirst = System.IO.Path.GetFullPath(first).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                string normalizedSecond = System.IO.Path.GetFullPath(second).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
            }
        }

        private List<string> GetAudioFilesFromFolder(string folder, SearchOption searchOption)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return new List<string>();

            try
            {
                return Directory.EnumerateFiles(folder, "*.*", searchOption)
                                .Where(IsSupportedAudioFile)
                                .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Folder scan failed for '{folder}': {ex.Message}");
                return new List<string>();
            }
        }

        private Task<List<string>> GetAudioFilesFromFolderAsync(string folder, SearchOption searchOption)
        {
            return Task.Run(() => GetAudioFilesFromFolder(folder, searchOption));
        }

        private List<string> GetExistingAudioFiles(IEnumerable<string> files)
        {
            if (files == null) return new List<string>();

            return files.Where(file =>
            {
                try { return File.Exists(file) && IsSupportedAudioFile(file); }
                catch { return false; }
            }).ToList();
        }

        private void RememberFolderPlaylist(string sourceFolder)
        {
            _lastPlaylistRestoreMode = PlaylistRestoreFolder;
            _lastPlaylistTracks.Clear();
            lastFolderPath = sourceFolder;
        }

        private void RememberExactPlaylist(string sourceFolder, IEnumerable<string> files)
        {
            _lastPlaylistRestoreMode = PlaylistRestoreQueue;
            _lastPlaylistTracks = GetExistingAudioFiles(files);
            lastFolderPath = !string.IsNullOrWhiteSpace(sourceFolder) ? sourceFolder : Root;
        }

        private bool ReplacePlaylistWithOrderedFiles(IEnumerable<string> files, string sourceFolder, bool rememberExactPlaylist, bool saveState)
        {
            var validFiles = GetExistingAudioFiles(files);
            if (validFiles.Count == 0)
                return false;

            isFavoritesMode = false;
            playlist.Clear();
            shuffleOrder.Clear();
            playlist.AddRange(validFiles);
            shuffleOrder = Enumerable.Range(0, playlist.Count).ToList();
            currentIndex = 0;

            string watcherPath = Directory.Exists(sourceFolder) ? sourceFolder : Root;
            if (!string.IsNullOrWhiteSpace(watcherPath) && Directory.Exists(watcherPath))
                InitializeLibraryWatcher(watcherPath);

            if (rememberExactPlaylist)
                RememberExactPlaylist(watcherPath, validFiles);
            else
                RememberFolderPlaylist(watcherPath);

            if (saveState)
                SaveViewState();

            return true;
        }

        private async Task<bool> ReplacePlaylistWithFiles(IEnumerable<string> files, string sourceFolder, bool autoPlayWhenEmpty = true)
        {
            var validFiles = GetExistingAudioFiles(files);
            if (validFiles.Count == 0)
                return false;

            isFavoritesMode = false;
            playlist.Clear();
            shuffleOrder.Clear();
            currentIndex = 0;

            bool loaded = await AddToPlaylist(validFiles.ToArray(), autoPlayWhenEmpty);
            if (!loaded) return false;

            if (!string.IsNullOrWhiteSpace(sourceFolder) && Directory.Exists(sourceFolder))
            {
                RememberFolderPlaylist(sourceFolder);
                InitializeLibraryWatcher(sourceFolder);
                SaveViewState();
            }

            return true;
        }

        private async Task<bool> LoadRootPlaylistFallbackAsync(bool autoPlayWhenEmpty = true)
        {
            if (string.IsNullOrWhiteSpace(Root) || Root == "null" || !Directory.Exists(Root))
                return false;

            ShowPlaylistLoading();

            var rootFiles = await GetAudioFilesFromFolderAsync(Root, SearchOption.AllDirectories);
            return await ReplacePlaylistWithFiles(rootFiles, Root, autoPlayWhenEmpty);
        }

        private async Task<bool> ReplacePlaylistFromFolderOrRootAsync(string requestedFolder, SearchOption searchOption, bool autoPlayWhenEmpty = true)
        {
            var files = await GetAudioFilesFromFolderAsync(requestedFolder, searchOption);

            if (files.Count > 0)
                return await ReplacePlaylistWithFiles(files, requestedFolder, autoPlayWhenEmpty);

            if (!IsSamePath(requestedFolder, Root))
                return await LoadRootPlaylistFallbackAsync(autoPlayWhenEmpty);

            return false;
        }

        public async Task LoadFolderAndPlayFile(string targetFilePath)
        {
            try
            {
                string folder = System.IO.Path.GetDirectoryName(targetFilePath);
                if (string.IsNullOrEmpty(folder)) return;

                var files = await GetAudioFilesFromFolderAsync(folder, SearchOption.TopDirectoryOnly);

                if (files.Count > 0)
                {
                    await ReplacePlaylistWithFiles(files, folder, false);

                    int playlistIndex = playlist.FindIndex(file => IsSamePath(file, targetFilePath));
                    if (playlistIndex != -1)
                    {
                        currentIndex = shuffleOrder.IndexOf(playlistIndex);
                        if (currentIndex >= 0)
                            await PlayCurrentAsync();
                    }
                    else if (playlist.Count > 0)
                    {
                        currentIndex = 0;
                        await PlayCurrentAsync();
                    }
                }
                else
                {
                    await LoadRootPlaylistFallbackAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Shell Load Error: {ex.Message}");
                await LoadRootPlaylistFallbackAsync();
            }
        }

        public void IntegrateShellExtension()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string appName = "podmix.exe"; // Lowercase as requested
            string friendlyName = "Podmix";
            IReadOnlyList<string> extensions = AudioFileSupport.Extensions;

            try
            {
                // 1. Register the Application globally in Classes
                string appKeyPath = $@"Software\Classes\Applications\{appName}";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(appKeyPath))
                {
                    key.SetValue("FriendlyAppName", friendlyName);
                    key.SetValue("Icon", exePath);

                    using (RegistryKey supportedTypes = key.CreateSubKey("SupportedTypes"))
                    {
                        foreach (var ext in extensions) supportedTypes.SetValue(ext, "");
                    }

                    using (RegistryKey shellOpen = key.CreateSubKey(@"shell\open\command"))
                    {
                        shellOpen.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                // 2. Register for each extension
                foreach (var ext in extensions)
                {
                    // Context Menu: "Open in Podmix"
                    string shellKeyPath = $@"Software\Classes\{ext}\shell\Podmix";
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(shellKeyPath))
                    {
                        key.SetValue("", "Open in Podmix");
                        key.SetValue("Icon", exePath);
                        using (RegistryKey commandKey = key.CreateSubKey("command"))
                        {
                            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
                        }
                    }

                    // Submenu: "Open With" list
                    string openWithKeyPath = $@"Software\Classes\{ext}\OpenWithProgids";
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(openWithKeyPath))
                    {
                        key.SetValue(appName, "");
                    }
                }

                // 3. Cleanup old "PodmixMusic" keys if they exist
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\PodmixMusic.exe", false);
                foreach (var ext in extensions)
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ext}\shell\PodmixMusic", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Registry Integration Error: {ex.Message}");
            }
        }

        public void RemoveShellExtension()
        {
            string appName = "podmix.exe";
            IReadOnlyList<string> extensions = AudioFileSupport.Extensions;

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Applications\{appName}", false);

                foreach (var ext in extensions)
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ext}\shell\Podmix", false);

                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}\OpenWithProgids", true))
                    {
                        if (key != null) key.DeleteValue(appName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Registry Removal Error: {ex.Message}");
            }
        }

        private string FavoritesPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "favorites.json");
        private List<string> favorites = new List<string>();

        private void LoadFavorites()
        {
            if (File.Exists(FavoritesPath))
            {
                try
                {
                    string json = File.ReadAllText(FavoritesPath);
                    favorites = (JArray.Parse(json).ToObject<List<string>>() ?? new List<string>())
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .ToList();
                }
                catch { favorites = new List<string>(); }
            }
            else
            {
                favorites = new List<string>();
            }

            UpdateFavoritesCountUI();
        }

        private bool IsFavoriteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return favorites.Any(f => IsSamePath(f, path));
        }

        private void SaveFavorites()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(FavoritesPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FavoritesPath, JArray.FromObject(favorites).ToString());
            }
            catch { }
        }

        private void MenuFavorite_Checked(object sender, RoutedEventArgs e)
        {
            if (!isRadioMode)
            {
                if (string.IsNullOrEmpty(currentFilePath)) return;
                if (!IsFavoriteFile(currentFilePath))
                {
                    favorites.Add(currentFilePath);
                    SaveFavorites();
                    UpdateFavoritesCountUI();
                }
            }
            else
            {
                // Radio logic
                var radioFavs = LoadRadioFavorites();
                if (!radioFavs.Any(r => r.Url == currentFilePath))
                {
                    radioFavs.Add(new RadioStation
                    {
                        Name = TxtTitle.Text,
                        Url = currentFilePath,
                        AlternateUrls = _currentRadioCandidateUrls.Where(u => !string.Equals(u, currentFilePath, StringComparison.OrdinalIgnoreCase)).ToList(),
                        FaviconUrl = _currentStationFaviconUrl,
                        IsUnavailable = false,
                        LastFailedUtc = null
                    });
                    SaveRadioFavorites(radioFavs);
                }
            }      
        }

        private void MenuFavorite_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!isRadioMode)
            {
                if (string.IsNullOrEmpty(currentFilePath)) return;

                // 1. Remove from master favorites list
                if (IsFavoriteFile(currentFilePath))
                {
                    favorites.RemoveAll(f => IsSamePath(f, currentFilePath));
                    SaveFavorites();
                    UpdateFavoritesCountUI();
                }

                // 2. If we are currently playing the Favorites playlist, remove it from the queue
                if (isFavoritesMode)
                {
                    int indexInPlaylist = playlist.IndexOf(currentFilePath);
                    if (indexInPlaylist != -1)
                    {
                        playlist.RemoveAt(indexInPlaylist);

                        // Fix the shuffle order and pointers
                        SyncShuffleAfterDelete(indexInPlaylist);

                        if (playlist.Count == 0)
                        {
                            // If no songs are left, trigger the clear queue logic to stop playback and reset UI
                            currentIndex = -1;
                            ClearCurrentPlaylist_Click(null, null);
                        }
                        else
                        {
                            // If we removed the last song in the shuffle sequence, wrap back to 0
                            if (currentIndex >= playlist.Count)
                            {
                                currentIndex = 0;
                            }

                            UpdateUIStatus(isPlaying);
                        }
                    }
                }
            }
            else
            {
                // Radio logic
                var radioFavs = LoadRadioFavorites();
                var itemToRemove = radioFavs.FirstOrDefault(r => IsRadioStationUrlMatch(r, currentFilePath));
                if (itemToRemove != null)
                {
                    radioFavs.Remove(itemToRemove);
                    SaveRadioFavorites(radioFavs);
                }
            }
        }

        private void UpdateFavoriteMenuStatus()
        {
            MenuFavorite.Checked -= MenuFavorite_Checked;
            MenuFavorite.Unchecked -= MenuFavorite_Unchecked;

            if (!isRadioMode)
            {
                MenuFavorite.IsChecked = IsFavoriteFile(currentFilePath);
            }
            else
            {
                var radioFavs = LoadRadioFavorites();
                MenuFavorite.IsChecked = radioFavs.Any(r => IsRadioStationUrlMatch(r, currentFilePath));
            }

            MenuFavorite.Checked += MenuFavorite_Checked;
            MenuFavorite.Unchecked += MenuFavorite_Unchecked;
        }


        private async void PlayFavorites_Click(object sender, RoutedEventArgs e)
        {
            if (favorites.Count == 0)
            {
                await LoadRootPlaylistFallbackAsync();
                return;
            }

            var validFavorites = GetExistingAudioFiles(favorites);
            if (validFavorites.Count == 0)
            {
                await LoadRootPlaylistFallbackAsync();
                return;
            }

            playlist.Clear();
            shuffleOrder.Clear();
            foreach (var song in validFavorites)
            {
                playlist.Add(song);
            }

            GenerateShuffleOrder();
            currentIndex = 0;
            isFavoritesMode = true;
            PlayCurrent();
        }

        private void UpdateFavoritesCountUI()
        {
            int count = favorites.Count;
            string suffix = count == 1 ? "song" : "songs";
            MenuPlayFavorites.Header = $"Play My Favorites ({count.ToMetricFormat()} {suffix})";
        }

        private void TxtArtist_MouseEnter(object sender, MouseEventArgs e)
        {
            TxtArtist.Foreground = _artistHoverBrush;
        }

        private void TxtArtist_MouseLeave(object sender, MouseEventArgs e)
        {
            TxtArtist.Foreground = _artistNormalBrush;
        }

        private void TxtCount_MouseEnter(object sender, MouseEventArgs e)
        {
            if (TxtCount.Text == "LIVE") return;
            TxtCount.Foreground = _artistHoverBrush;
        }

        private void TxtCount_MouseLeave(object sender, MouseEventArgs e)
        {
            if (TxtCount.Text == "LIVE") return;
            TxtCount.Foreground = _artistNormalBrush;
        }

        private void TxtArtist_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (TxtArtist.ContextMenu != null)
            {
                TxtArtist.ContextMenu.PlacementTarget = TxtArtist;
                TxtArtist.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                TxtArtist.ContextMenu.IsOpen = true;
            }

            e.Handled = true;
        }

        private void SearchOnline_Click(object sender, RoutedEventArgs e)
        {
            if (TxtArtist.Text.Contains("Connecting")
                || TxtArtist.Text.Contains("Buffering")) return;


            if (sender is MenuItem menuItem 
                && menuItem.Tag != null
                )
            {
                string query = string.Empty;

                if (isRadioMode)
                {
                    // For radio, TxtArtist contains the "Artist - Title" string
                    var split = TxtArtist.Text?.Split(new[] { '-' }, 2);
                    query = (split != null && split.Length > 0) ? split[0].Trim() : string.Empty;
                }
                else
                {
                    // For local files, combine Artist and Title for better results
                    string artist = TxtArtist.Text?.Trim();
                    query = artist?.Trim() ?? string.Empty;
                }

                // Prevent searching default icons or empty strings
                if (string.IsNullOrWhiteSpace(query) || query == "♪" || query.Contains("Connecting") || query.Contains("Buffering"))
                    return;

                try
                {
                    string baseUrl = menuItem.Tag.ToString();
                    string url = baseUrl + Uri.EscapeDataString(query);

                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }


        private async void ShuffleArtist_Click(object sender, RoutedEventArgs e)
        {
            if (isRadioMode) return;
            if (TxtArtist.Text.Equals("Unknown Artist")) return;

            TxtTitle.Text = "♪ Getting songs...";
            TxtAlbum.Text = "";

            string artist = TxtArtist.Text?.Trim();

            if (string.IsNullOrWhiteSpace(artist))
                return;

            var artistSongs = await Task.Run(() =>
            {
                return playlist.Where(file =>
                {
                    try
                    {
                        // ADDED 'using' here to release the file handle immediately
                        using (var tagFile = TagLib.File.Create(file))
                        {
                            string fileArtist =
                                tagFile.Tag.FirstAlbumArtist ??
                                tagFile.Tag.FirstPerformer ??
                                (tagFile.Tag.Performers != null && tagFile.Tag.Performers.Length > 0
                                    ? tagFile.Tag.Performers[0]
                                    : "Unknown");

                            if (string.IsNullOrWhiteSpace(fileArtist))
                                return false;

                            return fileArtist.Trim()
                                             .Equals(artist, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }).ToList();
            });

            if (artistSongs.Count == 0)
            {
                MessageBox.Show("No songs found for this artist.");
                return;
            }

            playlist = artistSongs;
            GenerateShuffleOrder();
            currentIndex = 0;
            RememberExactPlaylist(Root, artistSongs);
            SaveViewState();
            PlayCurrent();
        }


        private void InitializeLibraryWatcher(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            // Dispose old watcher if it exists
            libraryWatcher?.Dispose();

            libraryWatcher = new FileSystemWatcher
            {
                Path = path,
                Filter = "*.*", // We filter extensions in the events
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Link the events
            libraryWatcher.Created += OnLibraryChanged;
            libraryWatcher.Deleted += OnLibraryChanged;
            libraryWatcher.Renamed += OnLibraryRenamed;
        }

        // FileSystemWatcher runs on a background thread, 
        // so we MUST use Dispatcher to talk to the UI/Playlist.
        private void OnLibraryChanged(object sender, FileSystemEventArgs e)
        {
            // BeginInvoke (non-blocking) is important here: a synchronous Invoke on the
            // watcher thread can deadlock, and the old "async" lambda was effectively
            // async void, so any exception after an await could terminate the process.
            try
            {
                Dispatcher.BeginInvoke(new Action(() => _ = HandleLibraryChangedAsync(e)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Library watcher dispatch failed: {ex.Message}");
            }
        }

        private async Task HandleLibraryChangedAsync(FileSystemEventArgs e)
        {
            try
            {
                if (IsSupportedAudioFile(e.FullPath))
                {
                    if (e.ChangeType == WatcherChangeTypes.Created)
                    {
                        if (!playlist.Contains(e.FullPath))
                        {
                            playlist.Add(e.FullPath);
                            // Add new index to shuffle at a random position 
                            // or just at the end to keep it simple
                            int newIndex = playlist.Count - 1;
                            shuffleOrder.Add(newIndex);
                        }
                    }
                    else if (e.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        int indexInPlaylist = playlist.IndexOf(e.FullPath);
                        if (indexInPlaylist != -1)
                        {
                            // 1. Check if the deleted file is the one we are currently listening to
                            bool wasPlayingDeletedSong = (currentFilePath == e.FullPath);

                            // 2. Remove from data structures
                            playlist.RemoveAt(indexInPlaylist);
                            SyncShuffleAfterDelete(indexInPlaylist);

                            // 3. Handle playback
                            if (playlist.Count == 0)
                            {
                                await LoadRootPlaylistFallbackAsync();
                            }
                            else if (wasPlayingDeletedSong)
                            {
                                // The song is gone from disk! Skip to the next random song.
                                AdvanceToNextTrack();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Library watcher error: {ex.Message}");
            }
        }

        private void OnLibraryRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (Directory.Exists(e.FullPath))
                        {
                            // Trigger a refresh of the library
                            _ = RefreshLibraryAfterFolderRenameAsync();
                            return;
                        }

                        string oldPath = e.OldFullPath;
                        string newPath = e.FullPath;
                        // Check if it's a supported audio file
                        if (IsSupportedAudioFile(newPath))
                        {
                            int index = playlist.IndexOf(oldPath);

                            if (index != -1)
                            {
                                // Update the path in the playlist
                                playlist[index] = newPath;

                                // If this is the song currently playing, update the player's 
                                // internal reference so it doesn't lose its place
                                if (currentFilePath == oldPath)
                                {
                                    currentFilePath = newPath;
                                }

                                // Note: No change to shuffleOrder is needed! 
                                // Since the position (index) in the playlist stayed the same, 
                                // the shuffle indices still point to the correct slot.
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Library rename error: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Library watcher dispatch failed: {ex.Message}");
            }
        }

        private async Task RefreshLibraryAfterFolderRenameAsync()
        {
            try
            {
                await AddToPlaylistFromSearch(Root);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Library refresh error: {ex.Message}");
            }
        }

        private void SyncShuffleAfterDelete(int removedPlaylistIndex)
        {
            // 1. Find where the deleted song was in our shuffle sequence
            int shufflePosOfDeletedSong = shuffleOrder.IndexOf(removedPlaylistIndex);
            if (shufflePosOfDeletedSong < 0) return;

            // 2. Remove it from the shuffle sequence
            shuffleOrder.RemoveAt(shufflePosOfDeletedSong);

            // 3. If the deleted song was "before" our current position in the shuffle,
            // we must move our pointer back by 1 to stay on the same song.
            if (shufflePosOfDeletedSong < currentIndex)
            {
                currentIndex--;
            }
            // Note: If shufflePosOfDeletedSong == currentIndex, the 'Next_Click' 
            // call handles the pointer logic, so we don't adjust it here.

            // 4. Critical: Fix the playlist pointers for everything else
            for (int i = 0; i < shuffleOrder.Count; i++)
            {
                if (shuffleOrder[i] > removedPlaylistIndex)
                {
                    shuffleOrder[i]--;
                }
            }
        }

        // Drag the window around
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                BeginAnimation(Window.TopProperty, null);
                BeginAnimation(Window.LeftProperty, null);

                try
                {
                    DragMove();
                }
                catch
                {
                    return;
                }

                SettleWindowOnTaskbar(true);
            }
        }

        private double GetCurrentWindowWidth()
        {
            double width = ActualWidth;
            if (double.IsNaN(width) || width <= 0) width = Width;
            if (double.IsNaN(width) || width <= 0) width = MainContainer.ActualWidth;
            return double.IsNaN(width) || width <= 0 ? 260 : width;
        }

        private double GetCurrentWindowHeight()
        {
            double height = ActualHeight;
            if (double.IsNaN(height) || height <= 0) height = Height;
            if (double.IsNaN(height) || height <= 0) height = MainContainer.ActualHeight;
            return double.IsNaN(height) || height <= 0 ? 390 : height;
        }

        private double GetWindowDockTop(Rect workArea)
        {
            return workArea.Bottom - GetCurrentWindowHeight() + TaskbarVisualFloorOverlap;
        }

        private double ClampWindowLeftToWorkArea(double left, Rect workArea)
        {
            double width = GetCurrentWindowWidth();
            double minLeft = workArea.Left;
            double maxLeft = workArea.Right - width;

            if (maxLeft < minLeft)
                return minLeft;

            return Math.Max(minLeft, Math.Min(maxLeft, left));
        }

        private void DockWindowToTaskbarRight()
        {
            var workArea = SystemParameters.WorkArea;
            double width = GetCurrentWindowWidth();

            BeginAnimation(Window.TopProperty, null);
            BeginAnimation(Window.LeftProperty, null);
            Left = ClampWindowLeftToWorkArea(workArea.Right - width - TaskbarDockRightMargin, workArea);
            Top = GetWindowDockTop(workArea);
        }

        private void SettleWindowOnTaskbar(bool animate)
        {
            var workArea = SystemParameters.WorkArea;
            double targetLeft = ClampWindowLeftToWorkArea(Left, workArea);
            double targetTop = GetWindowDockTop(workArea);

            BeginAnimation(Window.LeftProperty, null);
            if (Math.Abs(Left - targetLeft) > 0.5)
                Left = targetLeft;

            BeginAnimation(Window.TopProperty, null);
            if (!animate || Top >= targetTop)
            {
                Top = targetTop;
                return;
            }

            var fall = new DoubleAnimation
            {
                From = Top,
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(520),
                EasingFunction = new BounceEase
                {
                    Bounces = 2,
                    Bounciness = 4,
                    EasingMode = EasingMode.EaseOut
                }
            };

            BeginAnimation(Window.TopProperty, fall);
        }

        #region File Management (Add & Shuffle)       

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (droppedPaths == null || droppedPaths.Length == 0) return;

                // 1. Show the styled choice window
                var win = new AddActionWindow { Owner = this };
                if (win.ShowDialog() == true)
                {
                    // 2. If user chose 'Replace', clear the current library
                    if (win.ActionChoice == "Replace")
                    {
                        //playlist.Clear();
                        ClearCurrentPlaylist_Click(null, null); // << Better use this
                    }

                    // 3. Set the library watcher path based on the first item dropped
                    if (Directory.Exists(droppedPaths[0]))
                    {
                        RememberFolderPlaylist(droppedPaths[0]);
                    }
                    else
                    {
                        RememberFolderPlaylist(System.IO.Path.GetDirectoryName(droppedPaths[0]));
                    }

                    List<string> allMusicFiles = new List<string>();

                    // 4. Collect all files from the dropped items
                    foreach (string path in droppedPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            allMusicFiles.AddRange(GetAudioFilesFromFolder(path, SearchOption.AllDirectories));
                        }
                        else if (File.Exists(path))
                        {
                            // Check individual files
                            if (IsSupportedAudioFile(path))
                            {
                                allMusicFiles.Add(path);
                            }
                        }
                    }

                    // 5. Add to playlist and start watching
                    if (allMusicFiles.Count > 0)
                    {
                        bool loaded = await AddToPlaylist(allMusicFiles.ToArray());
                        if (loaded)
                        {
                            InitializeLibraryWatcher(lastFolderPath);
                            isFavoritesMode = false;
                        }
                        else
                        {
                            await LoadRootPlaylistFallbackAsync();
                        }
                    }
                    else
                    {
                        await LoadRootPlaylistFallbackAsync();
                    }
                }
            }
        }

        private void SaveCurrentPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0) return;
            if (isFavoritesMode) return;

            try
            {
                if (!Directory.Exists(PlaylistsPath))
                    Directory.CreateDirectory(PlaylistsPath);

                string folderPath = Directory.Exists(lastFolderPath) ? lastFolderPath : Root;
                string folderName = System.IO.Path.GetFileName(lastFolderPath);

                string jsonPath = System.IO.Path.Combine(PlaylistsPath, $"{folderName}.json");

                if (File.Exists(jsonPath))
                {
                    return;
                }

                string cleanName = folderName.Replace("\"", "\\\"");
                string jsonContent = "{\n" +
                    $"  \"PlaylistName\": \"{cleanName}\",\n" +
                    $"  \"SourcePath\": \"{folderPath.Replace("\\", "\\\\")}\",\n" +
                    $"  \"DateCreated\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"\n" +
                    "}";

                File.WriteAllText(jsonPath, jsonContent);
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show(
                    owner: (System.Windows.Forms.IWin32Window)this, 
                    text: $"The playlist could not be saved.",
                    caption: "Error",
                    buttons: System.Windows.Forms.MessageBoxButtons.OK,
                    icon: System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void ClearCurrentPlaylist_Click(object sender, RoutedEventArgs e)
        {
            isFavoritesMode = false;
            ImgAlbumArt.Source = null;
            _pendingLyricsPath = _pendingLyricsText = _temCachedLyricsText = null;

            TxtAlbumArt.Visibility = Visibility.Visible;
            TxtTitle.Text = "";
            TxtArtist.Text = "";
            TxtAlbum.Text = "";
            ProgressSlider.Value = 0;
            TimeNow.Text = "00:00";
            TimeEnd.Text = "00:00";
            currentIndex = 0;
            currentFilePath = "";
            _openedMediaPath = "";

            TipTitle.Text = TxtTitle.Text;
            TipArtist.Text = TxtArtist.Text;
            TipAlbum.Text = TxtAlbum.Text;

            StopLocalPlayback(false);
            isPlaying = false;
            playlist.Clear();
            timer.Stop();
            _sleepTimer?.Stop();
            _sleepTimer = null;
            UpdateUIStatus(false);
            _discord.ClearPresence();
        }

        private void LoadSavedPlaylists()
        {
            if (!Directory.Exists(PlaylistsPath))
            {
                Directory.CreateDirectory(PlaylistsPath);
            }

            MenuPlaylists.Items.Clear();

            // Sorting logic for the Menu
            var files = Directory.GetFiles(PlaylistsPath, "*.json")
                                 .Where(f => !f.EndsWith("settings.json"))
                                 .Select(f => new { Path = f, Date = GetDateFromJson(f) })
                                 .OrderByDescending(x => x.Date)
                                 .Select(x => x.Path)
                                 .ToArray();

            if (files.Length == 0)
            {
                MenuPlaylists.Items.Add(new MenuItem
                {
                    Header = "Empty",
                    IsEnabled = false,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray
                });
            }
            else
            {
                foreach (var file in files)
                {
                    // Rename logic for the Menu
                    string playlistName = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (playlistName.Equals(System.IO.Path.GetFileName(Root), StringComparison.OrdinalIgnoreCase))
                    {
                        playlistName = "All My Songs";
                    }

                    MenuItem item = new MenuItem
                    {
                        Header = playlistName,
                        Tag = file
                    };

                    item.Click += LoadPlaylist_Click;
                    MenuPlaylists.Items.Add(item);
                }
            }
        }

        private async Task GetInternalSettingsAsync()
        {
            Root = (string)Config.Get("root");
            LoadFavorites();
            _selectedBassDevice = GetSavedAudioOutputDevice();

            bool extractAlbumCoverColor = (bool)Config.Get("extract_album_cover_color");
            MenuExtractColor.IsChecked = extractAlbumCoverColor;
            if (string.IsNullOrWhiteSpace(colorHex))
                ApplyThemeColor(DefaultThemeColor);

            bool enableDiscord = true;
            try { enableDiscord = (bool)Config.Get("discord_presence"); } catch { }
            MenuDiscordPresence.IsChecked = enableDiscord;
            _discord.IsEnabled = enableDiscord;

            int vol = Convert.ToInt32(Config.Get("volume"));
            _playerVolume = Math.Max(0, Math.Min(1, vol / 100.0));
            ApplyBassVolume();

            if (Root == "null")
            {
                SetupWindow welcome = new SetupWindow();
                if (welcome.ShowDialog() == true)
                {
                    Root = welcome.SelectedPath;
                    lastFolderPath = Root;
                    bool loaded = await ReplacePlaylistFromFolderOrRootAsync(Root, SearchOption.AllDirectories, false);
                    if (loaded && playlist.Count > 0 && shuffleOrder.Count > 0)
                    {
                        currentIndex = 0;
                        int shuffledIndex = shuffleOrder[currentIndex];
                        if (shuffledIndex >= 0 && shuffledIndex < playlist.Count)
                        {
                            string filePath = playlist[shuffledIndex];
                            currentFilePath = filePath;
                            PrepareLocalPlaybackStream(filePath, false);
                            if (!await StartQueuedStartupPlaybackIfReadyAsync())
                            {
                                await LoadStartupTrackPresentationAsync(filePath, extractAlbumCoverColor);
                                if (!isPlaying)
                                {
                                    UpdateUIStatus(false);
                                    _openedMediaPath = "";
                                }
                                UpdateFavoriteMenuStatus();
                            }
                        }
                    }
                    if (!extractAlbumCoverColor)
                        ApplyThemeColor(DefaultThemeColor);

                    lastFolderPath = Root;
                    SaveCurrentPlaylist_Click(null, null);

                    Config.Save("root", Root);
                }
                else
                {
                    Application.Current.Shutdown();
                    return;
                }
            }
            else
            {
                // Use lastFolderPath if it was successfully loaded from settings, otherwise fallback to Root.
                // If the folder exists but has no playable songs, fall back to Root too.
                string pathToLoad = (!string.IsNullOrEmpty(lastFolderPath) && Directory.Exists(lastFolderPath)) ? lastFolderPath : Root;

                bool restoredSavedQueue = _lastPlaylistRestoreMode == PlaylistRestoreQueue
                    && _lastPlaylistTracks.Count > 0
                    && ReplacePlaylistWithOrderedFiles(_lastPlaylistTracks, pathToLoad, true, false);

                if (!restoredSavedQueue)
                    await ReplacePlaylistFromFolderOrRootAsync(pathToLoad, SearchOption.AllDirectories, false);

                if (playlist.Count > 0 && shuffleOrder.Count > 0)
                {
                    currentIndex = 0;
                    int shuffledIndex = shuffleOrder[currentIndex];
                    if (shuffledIndex >= 0 && shuffledIndex < playlist.Count)
                    {
                        string filePath = playlist[shuffledIndex];
                        currentFilePath = filePath;
                        PrepareLocalPlaybackStream(filePath, false);
                        if (!await StartQueuedStartupPlaybackIfReadyAsync())
                        {
                            await LoadStartupTrackPresentationAsync(filePath, extractAlbumCoverColor);
                            if (!isPlaying)
                            {
                                UpdateUIStatus(false);
                                _openedMediaPath = "";
                            }
                            UpdateFavoriteMenuStatus();
                        }
                    }
                }
            }

            bool topMost = (bool)Config.Get("top_most");
            Topmost = topMost;
            MenuTopMost.IsChecked = Topmost;

            bool autoReconnect = (bool)Config.Get("auto_reconnect");
            MenuAutoReconnect.IsChecked = autoReconnect;

            bool showNextUp = (bool)Config.Get("show_next_up");
            MenuShowNextUp.IsChecked = showNextUp;

            bool allowHttp = (bool)Config.Get("radio_allow_http");
            MenuAllowHTTP.IsChecked = allowHttp;
            UpdateRadioBufferMenuChecks();
            EnsureRecordingFolder();

            _suppressPasscodeMenuEvent = true;
            MenuRequirePasscode.IsChecked = (bool)Config.Get("lock_requires_passcode");
            _suppressPasscodeMenuEvent = false;
            UpdateScreenSleepMenuChecks();

            if (!_startupFileHandled && !string.IsNullOrWhiteSpace(_startupFilePath))
            {
                _startupFileHandled = true;
                await LoadFolderAndPlayFile(_startupFilePath);
            }

            _isStartupLoading = false;
            await StartQueuedStartupPlaybackIfReadyAsync();

            UpdateFavoriteMenuStatus();
            BeginCoverFlowBackgroundScan();
            ResetScreenSleepTimer();
            if (extractAlbumCoverColor)
                ApplyAlbumColorFromCurrentArt();
            RevealStartupWindow();
            HideBootViewToPlayer();
        }

        private async Task LoadStartupTrackPresentationAsync(string filePath, bool extractAlbumCoverColor)
        {
            if (extractAlbumCoverColor)
            {
                await ApplyAlbumColorForFileAsync(filePath);
                await UpdateMetadataAsync(filePath);
            }
            else
            {
                _ = UpdateMetadataAsync(filePath);
            }
        }

        private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "This will permanently delete all your playlists, favorites, settings. The app will restart for a fresh setup.\n\nAre you sure you want to continue?",
                "Reset Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 1. Release all resources and locks
                    libraryWatcher?.Dispose();
                    libraryWatcher = null;
                    StopLocalPlayback(false);
                    StopRadio();
                    timer.Stop();
                    Config.ResetDefaults();

                    string appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic");
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                    // 2. Use CMD to wait 2 seconds (giving the app time to exit), 
                    // delete the folder, and then restart the app.
                    // /C carries out the command and then terminates.
                    string cmdCommand = $"/C timeout /T 2 /NOBREAK & rd /S /Q \"{appDataPath}\" & start \"\" \"{exePath}\"";

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = cmdCommand,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });

                    // 3. Shutdown the current instance immediately
                    System.Windows.Application.Current.Shutdown();
                }
                catch
                {
                    MessageBox.Show("An error occurred during reset.", "Reset Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag != null)
            {
                // Simply delegate to the method that handles both formats
                LoadPlaylistByPath(item.Tag.ToString());
            }
        }

        private void MenuPlaylists_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            LoadSavedPlaylists();
        }

        private async void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Multiselect = true, Filter = AudioFileSupport.OpenFileDialogFilter };
            if (openFileDialog.ShowDialog() == true)
            {
                var win = new AddActionWindow { Owner = this };
                if (win.ShowDialog() == true)
                {
                    // IF REPLACE: Clear the list first
                    if (win.ActionChoice == "Replace")
                    {
                        // Use your actual collection name here (e.g., FullPlaylist)
                        playlist.Clear();
                    }

                    string firstFile = openFileDialog.FileNames[0];
                    RememberFolderPlaylist(System.IO.Path.GetDirectoryName(firstFile));

                    bool loaded = await AddToPlaylist(openFileDialog.FileNames);
                    if (loaded)
                    {
                        InitializeLibraryWatcher(lastFolderPath);
                        SaveViewState();
                        isFavoritesMode = false;
                    }
                    else
                    {
                        await LoadRootPlaylistFallbackAsync();
                    }
                }
            }
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // 1. Show your styled choice window
                var win = new AddActionWindow { Owner = this };

                if (win.ShowDialog() == true)
                {
                    // 2. If user clicked "Replace Playlist", clear the current list
                    if (win.ActionChoice == "Replace")
                    {
                        // Ensure this matches your actual ObservableCollection name
                        playlist.Clear();
                    }

                    if (win.ActionChoice == "Replace")
                    {
                        await ReplacePlaylistFromFolderOrRootAsync(dialog.SelectedPath, SearchOption.AllDirectories);
                    }
                    else
                    {
                        var files = await GetAudioFilesFromFolderAsync(dialog.SelectedPath, SearchOption.AllDirectories);
                        if (files.Count > 0)
                        {
                            RememberFolderPlaylist(dialog.SelectedPath);
                            await AddToPlaylist(files.ToArray());
                            InitializeLibraryWatcher(lastFolderPath);
                            SaveViewState();
                            isFavoritesMode = false;
                        }
                        else
                        {
                            await LoadRootPlaylistFallbackAsync();
                        }
                    }
                }
            }
        }

        private void NewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0) return;

            var win = new CreatePlaylistWindow { Owner = this };
            if (win.ShowDialog() == true)
            {
                try
                {
                    string name = win.PlaylistName;
                    if (!Directory.Exists(PlaylistsPath)) Directory.CreateDirectory(PlaylistsPath);
                    string jsonPath = System.IO.Path.Combine(PlaylistsPath, $"{name}.json");

                    if (File.Exists(jsonPath))
                    {
                        MessageBox.Show(owner: this,
                            messageBoxText: "A playlist with this name already exists.",
                            caption: "Information",
                            button: MessageBoxButton.OK,
                            icon: MessageBoxImage.Information);
                        return;
                    }

                    var obj = new JObject
                    {
                        ["PlaylistName"] = name,
                        ["IsManual"] = true,
                        ["DateCreated"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                        ["Tracks"] = new JArray() 
                    };

                    File.WriteAllText(jsonPath, obj.ToString());
                    RefreshPlaylistsList();
                }
                catch
                {
                    MessageBox.Show(owner: this,
                            messageBoxText: "Failed to create playlist.",
                            caption: "Error",
                            button: MessageBoxButton.OK,
                            icon: MessageBoxImage.Error);
                }
            }
        }

        private void MenuAddToPlaylist_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;

            MenuAddToPlaylist.Items.Clear();

            if (!Directory.Exists(PlaylistsPath)) return;

            // 1. Find all JSON files that are "Manual" (contain the Tracks array)
            var files = Directory.GetFiles(PlaylistsPath, "*.json")
                                 .Where(f => !f.EndsWith("settings.json"));

            bool foundManual = false;

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var obj = JObject.Parse(json);

                    if (obj["Tracks"] != null)
                    {
                        foundManual = true;
                        string playlistName = obj["PlaylistName"]?.ToString() ?? System.IO.Path.GetFileNameWithoutExtension(file);

                        MenuItem item = new MenuItem
                        {
                            Header = playlistName,
                            Tag = file // Store the path to the JSON file
                        };

                        item.Click += (s, args) =>
                        {
                            AddCurrentSongToManualPlaylist(item.Tag.ToString());
                        };

                        MenuAddToPlaylist.Items.Add(item);
                    }
                }
                catch { /* Skip corrupt files */ }
            }

            if (!foundManual)
            {
                MenuAddToPlaylist.Items.Add(new MenuItem 
                { Header = "No manual playlists found", 
                    IsEnabled = false,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray
                });
            }
        }

        private void AddCurrentSongToManualPlaylist(string jsonPath)
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;

            try
            {
                string json = File.ReadAllText(jsonPath);
                var obj = JObject.Parse(json);

                // Ensure the Tracks array exists
                if (obj["Tracks"] is JArray tracks)
                {
                    // Check for duplicates
                    if (!tracks.Any(t => t.ToString().Equals(currentFilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        tracks.Add(currentFilePath);
                        File.WriteAllText(jsonPath, obj.ToString());

                        // Refresh UI if the PlaylistsView is currently visible
                        if (PlaylistsView.Visibility == Visibility.Visible)
                        {
                            RefreshPlaylistsList();
                        }

                        Debug.WriteLine($"Added track to {obj["PlaylistName"]}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner: this,
                    messageBoxText: $"Failed to add song to playlist: {ex.Message}",
                    caption: "Error",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Error);
            }
        }

        private async Task<bool> AddToPlaylist(string[] files, bool autoPlayWhenEmpty = true)
        {
            bool wasEmpty = (playlist.Count == 0);

            // 1. Filter files on a background thread
            var sourceValidFiles = await Task.Run(() =>
            {
                return GetExistingAudioFiles(files);
            });

            if (sourceValidFiles.Count == 0)
                return false;

            var validFiles = sourceValidFiles.Where(file => !playlist.Contains(file)).ToList();
            if (validFiles.Count == 0)
            {
                if (playlist.Count > 0)
                {
                    GenerateShuffleOrder();
                    return true;
                }

                return false;
            }

            // 2. Update the actual playlist (Back on UI thread)
            foreach (string file in validFiles)
            {
                playlist.Add(file);
            }

            if (playlist.Count > 0)
            {
                GenerateShuffleOrder(); // Keep this logic here
                if (currentIndex < 0 || currentIndex >= shuffleOrder.Count)
                    currentIndex = 0;

                if (wasEmpty && autoPlayWhenEmpty)
                {
                    currentIndex = 0;
                    PlayCurrent();
                }
            }

            string folder = System.IO.Path.GetDirectoryName(validFiles[0]);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                InitializeLibraryWatcher(folder);

            return true;
        }

        private async Task<string> GetLyricsAsync(string filePath)
        {
            // 1. Local Check (TagLib)
            try
            {
                using (var tfile = TagLib.File.Create(filePath))
                {
                    if (!string.IsNullOrEmpty(tfile.Tag.Lyrics))
                    {
                        BtnShowSearch.Visibility = Visibility.Hidden;
                        return tfile.Tag.Lyrics;
                    }
                }
            }
            catch { }

            // 2. Check temp lyrics variable
            if (!isRadioMode)
            {
                if (_temCachedLyricsText != null)
                {
                    if (_pendingLyricsPath == null && _pendingLyricsText == null)
                    {
                        if (!isRadioMode) BtnShowSearch.Visibility = Visibility.Visible;
                    }
                    return _temCachedLyricsText;
                }
            }

            // Check internet connection
            if (!IsInternetAvailable())
                return "Connect to the internet and try again.";

            // 3. API Search
            try
            {
                string artist = string.Empty;
                string title = string.Empty;

                if (!isRadioMode)
                {
                    using (var file = TagLib.File.Create(filePath))
                    {
                        artist = file.Tag.FirstPerformer ?? "";
                        title = file.Tag.Title ?? System.IO.Path.GetFileNameWithoutExtension(filePath);
                    }
                }
                else
                {
                    var fullText = TxtArtist.Text.Trim();

                    // FIX: Validate the string before attempting to access split indices
                    if (string.IsNullOrEmpty(fullText) ||
                        fullText.Contains("Connecting") ||
                        fullText.Contains("Buffering") ||
                        !fullText.Contains("-"))
                    {
                        return "Waiting for radio metadata...";
                    }

                    var split = fullText.Split(new[] { " - " }, StringSplitOptions.None);
                    if (split.Length >= 2)
                    {
                        artist = split[0].Trim();
                        title = split[1].Trim();
                    }
                    else
                    {
                        // Fallback if there is a dash but not the " - " pattern
                        var simpleSplit = fullText.Split('-');
                        if (simpleSplit.Length >= 2)
                        {
                            artist = simpleSplit[0].Trim();
                            title = simpleSplit[1].Trim();
                        }
                        else
                        {
                            return "Lyrics not found.";
                        }
                    }
                }

                string cleanTitle = Regex.Replace(title, @"\s?[\(\[][^\)\]]+[\)\]]", "").Trim();
                string query = Uri.EscapeDataString($"{artist} {cleanTitle}");
                string url = $"https://lrclib.net/api/search?q={query}";

                httpClient.DefaultRequestHeaders.UserAgent.Clear();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PodmixMusic/1.0");

                string json = await httpClient.GetStringAsync(url);
                var results = JArray.Parse(json);

                if (results != null && results.Count > 0)
                {
                    var bestMatch = results[0];
                    string lyrics = bestMatch["plainLyrics"]?.ToString();

                    if (string.IsNullOrWhiteSpace(lyrics))
                    {
                        lyrics = bestMatch["syncedLyrics"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(lyrics))
                        {
                            lyrics = Regex.Replace(lyrics, @"\[\d+:\d+[\.:]\d+\]", "").Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(lyrics))
                    {
                        if (!isRadioMode) BtnShowSearch.Visibility = Visibility.Visible;
                        return lyrics;
                    }
                }
            }
            catch { }

            return "Lyrics not found.";
        }


        private async Task CommitPendingLyrics()
        {
            if (string.IsNullOrEmpty(_pendingLyricsPath) || _pendingLyricsText == null) return;

            string path = _pendingLyricsPath;
            string lyrics = _pendingLyricsText;

            _pendingLyricsPath = null;
            _pendingLyricsText = null;

            StopLocalPlayback(false);

            // Force the system to release file handles before writing tags.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var file = TagLib.File.Create(path))
                    {
                        file.Tag.Lyrics = lyrics;
                        file.Save();
                    }
                    break;
                }
                catch (IOException e)
                {
                    // These 2 line fixed the file lock
                    // when writing lyrics to mp3 files.

                    Clipboard.Clear();
                    Clipboard.SetText(e.Message);

                    // ** DO NOT DELETE THESE 2 LINES **

                    await Task.Delay(500); 
                }
            }
        }

        public bool IsInternetAvailable()
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send("8.8.8.8", 3000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }


        private void OpenSearch_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0)
            {
                MessageBox.Show(this, "Playlist is empty.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SearchWindow existingSearch = Application.Current.Windows.OfType<SearchWindow>().FirstOrDefault();

            if (existingSearch != null)
            {
                // 3. If it exists, JUST FOCUS IT. Do not create a new one.
                existingSearch.Activate();

                if (existingSearch.WindowState == WindowState.Minimized)
                {
                    existingSearch.WindowState = WindowState.Normal;
                }
            }
            else
            {
                // 4. If it truly doesn't exist, create it.
                SearchWindow searchWindow = new SearchWindow(this);
                searchWindow.Owner = this;
                searchWindow.Show();
            }
        }

        private async void ShowLyrics_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0)
            {
                MessageBox.Show(owner: this,
                   messageBoxText: "Start listening to a playlist to view lyrics.",
                   caption: "Information",
                   button: MessageBoxButton.OK,
                   icon: MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(currentFilePath)) return;

            isCoverFlowMode = false;

            if (IpodScreen.Visibility == Visibility.Collapsed)
                ToggleDetailedView_Click(null, null);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            BtnShowSearch.Content = new Image
            {
                Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/save_icon.png")),
                Width = 14,
                Height = 14
            };
            BtnTogglePlaylists.Content = new Image
            {
                Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/close_icon.png")),
                Width = 14,
                Height = 14
            };

            BtnShowSearch.Visibility = Visibility.Hidden;

            isLyricsMode = true;

            // Set background to current album art
            ImgLyricsBackground.Source = ImgAlbumArt.Source;

            // View Switching
            PlayerView.Visibility = Visibility.Collapsed;
            PlaylistsView.Visibility = Visibility.Collapsed;
            AboutView.Visibility = Visibility.Collapsed;
            CoverFlowView.Visibility = Visibility.Collapsed;
            LyricsView.Visibility = Visibility.Visible;

            TxtLyrics.Text = "Searching for lyrics...";

            string lyrics = await GetLyricsAsync(currentFilePath);
            TxtLyrics.Text = lyrics;
            _temCachedLyricsText = lyrics;
        }

        public async Task AddToPlaylistFromSearch(string selectedFilePath)
        {
            List<string> allValidFiles = await GetAudioFilesFromFolderAsync(Root, SearchOption.AllDirectories);
            if (allValidFiles.Count == 0)
                return;

            playlist.Clear();
            shuffleOrder.Clear();
            foreach (var file in allValidFiles)
            {
                playlist.Add(file);
            }

            GenerateShuffleOrder();

            int internalIndex = playlist.IndexOf(selectedFilePath);
            if (internalIndex != -1)
            {
                currentIndex = shuffleOrder.IndexOf(internalIndex);
                PlayCurrent();
            }

            RememberFolderPlaylist(Root);
            InitializeLibraryWatcher(Root);
            SaveViewState();
        }

        private void GenerateShuffleOrder()
        {
            if (playlist.Count == 0) return;

            var weightedList = new List<int>();
            var remainingIndices = Enumerable.Range(0, playlist.Count).ToList();
            var newOrder = new List<int>();

            while (remainingIndices.Count > 0)
            {
                double totalWeight = 0;
                var weights = new List<double>();

                foreach (int index in remainingIndices)
                {
                    int rating = GetRating(playlist[index]);
                    // Weight Logic: 5 stars = 10x more likely, 1 star = 0.5x likely, Unrated = 2x
                    double weight = 1.0;
                    switch (rating)
                    {
                        case 5: weight = 10.0; break;
                        case 4: weight = 5.0; break;
                        case 3: weight = 2.0; break;
                        case 2: weight = 1.0; break;
                        case 1: weight = 0.5; break;
                        default: weight = 2.0; break;
                    }
                    weights.Add(weight);
                    totalWeight += weight;
                }

                double choice = rng.NextDouble() * totalWeight;
                double currentSum = 0;

                for (int i = 0; i < remainingIndices.Count; i++)
                {
                    currentSum += weights[i];
                    if (choice <= currentSum)
                    {
                        newOrder.Add(remainingIndices[i]);
                        remainingIndices.RemoveAt(i);
                        break;
                    }
                }
            }
            shuffleOrder = newOrder;
        }

        // Helper for hover effects
        private void Star_MouseEnter(object sender, MouseEventArgs e) => UpdateStarsUI(int.Parse(((Button)sender).Tag.ToString()));
        private void Star_MouseLeave(object sender, MouseEventArgs e) => UpdateStarsUI(GetRating(currentFilePath));

        #endregion

        #region Player Logic
        private void TriggerShake()
        {
            DoubleAnimation shake = new DoubleAnimation
            {
                From = -6,                 // Distance to move left
                To = 6,                   // Distance to move right
                Duration = TimeSpan.FromMilliseconds(30),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(4)    // 4 quick vibrations
            };

            // This moves the whole StackPanel, so the screen and body move together
            MainShakeTransform.BeginAnimation(TranslateTransform.XProperty, shake);
        }

        private void SaveViewState()
        {
            try
            {
                string appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic");
                if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);

                string settingsFile = System.IO.Path.Combine(appDataPath, "settings.json");
                var state = new JObject
                {
                    ["IsDetailedView"] = isDetailed,
                    ["LastColor"] = colorHex,
                    ["LastPlaylistPath"] = lastFolderPath,
                    ["LastPlaylistMode"] = _lastPlaylistRestoreMode
                };

                if (_lastPlaylistRestoreMode == PlaylistRestoreQueue)
                    state["LastPlaylistTracks"] = new JArray(GetExistingAudioFiles(_lastPlaylistTracks));

                File.WriteAllText(settingsFile, state.ToString(Formatting.None));
            }
            catch { }
        }

        private void LoadViewState()
        {
            try
            {
                string settingsFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PodmixMusic", "settings.json");

                if (File.Exists(settingsFile))
                {
                    string json = File.ReadAllText(settingsFile);
                    JObject state = null;
                    try { state = JObject.Parse(json); } catch { }

                    bool? savedDetailedView = null;
                    if (state?["IsDetailedView"] != null)
                        savedDetailedView = state["IsDetailedView"].Value<bool>();
                    if (savedDetailedView == true || (savedDetailedView == null && json.Contains("\"IsDetailedView\": true")))
                    {
                        // Apply the detailed look immediately
                        isDetailed = true;
                        IpodScreen.Visibility = Visibility.Visible;
                        ToolTipService.SetIsEnabled(IpodBody, false);
                        IpodScreen.Width = 260;
                        IpodBody.Width = 260;
                        IpodBody.Height = 170;
                        IpodBody.CornerRadius = new CornerRadius(0, 0, 20, 20);
                        IpodShellFrame.Visibility = Visibility.Visible;
                        MainContainer.SetResourceReference(Border.BackgroundProperty, "HoverColorBrush");
                        MainContainer.Effect = CreateDeviceShadow();
                        IpodBody.Effect = null;
                    }
                    else if (savedDetailedView == false || (savedDetailedView == null && json.Contains("\"IsDetailedView\": false")))
                    {
                        isDetailed = false;
                        IpodScreen.Visibility = Visibility.Collapsed;
                        ToolTipService.SetIsEnabled(IpodBody, true);
                        IpodBody.Width = 180;
                        IpodBody.Height = 170;
                        IpodBody.CornerRadius = new CornerRadius(20);
                        IpodShellFrame.Visibility = Visibility.Collapsed;
                        MainContainer.Background = Brushes.Transparent;
                        MainContainer.Effect = null;
                        IpodBody.Effect = CreateDeviceShadow();
                    }

                    string savedHex = state?["LastColor"]?.ToString();
                    if (string.IsNullOrWhiteSpace(savedHex) && json.Contains("LastColor"))
                    {
                        int start = json.IndexOf("#");
                        if (start >= 0 && start + 7 <= json.Length)
                            savedHex = json.Substring(start, 7);
                    }

                    if (!string.IsNullOrWhiteSpace(savedHex) && savedHex.StartsWith("#") && savedHex.Length >= 7)
                    {
                        savedHex = savedHex.Substring(0, 7);
                        colorHex = savedHex;
                        Color newColor = (Color)ColorConverter.ConvertFromString(colorHex);
                        SolidColorBrush newBrush = new SolidColorBrush(newColor);
                        IpodBody.Background = newBrush;
                        CenterPlayBtn.Background = newBrush;
                        CenterPlayBtn.Foreground = GetContrastBrush(newBrush.Color);
                        IpodScreen.Background = Brushes.Black;
                        PlaybackWheel.Fill = CalculateScreenBackground(colorHex);
                        Resources["IpodScreenBrush"] = CalculateScreenBackground(colorHex);
                        Color bodyColor = (Color)ColorConverter.ConvertFromString(colorHex);
                        Resources["HoverColorBrush"] = new SolidColorBrush(bodyColor);
                        Resources["SliderTrackBrush"] = new SolidColorBrush(bodyColor);
                    }

                    string savedPath = state?["LastPlaylistPath"]?.ToString();
                    if (string.IsNullOrWhiteSpace(savedPath) && json.Contains("LastPlaylistPath"))
                    {
                        int start = json.IndexOf("\"LastPlaylistPath\": \"") + 21;
                        int end = json.IndexOf("\"", start);
                        if (start >= 21 && end > start)
                            savedPath = json.Substring(start, end - start).Replace("\\\\", "\\");
                    }

                    if (!string.IsNullOrWhiteSpace(savedPath) && Directory.Exists(savedPath))
                        lastFolderPath = savedPath;

                    string savedMode = state?["LastPlaylistMode"]?.ToString();
                    _lastPlaylistRestoreMode = string.Equals(savedMode, PlaylistRestoreQueue, StringComparison.OrdinalIgnoreCase)
                        ? PlaylistRestoreQueue
                        : PlaylistRestoreFolder;

                    _lastPlaylistTracks = state?["LastPlaylistTracks"]?.ToObject<List<string>>() ?? new List<string>();
                    if (_lastPlaylistRestoreMode == PlaylistRestoreQueue && _lastPlaylistTracks.Count == 0)
                        _lastPlaylistRestoreMode = PlaylistRestoreFolder;
                }
            }
            catch { }
        }

        private void StartMarquee(TextBlock target, TranslateTransform transform, Canvas container)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.X = 0;

            target.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 1. Reset width to Auto so we can measure the "real" size
                target.Width = double.NaN;
                target.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                // 2. Add a tiny bit of "safety padding" to the measured width
                double textWidth = target.DesiredSize.Width + 5;
                double containerWidth = container.Width;

                if (textWidth > containerWidth)
                {
                    // LONG TEXT: Switch to Left alignment
                    // We set the width to be wider than the text so the dots CANNOT appear
                    target.Width = textWidth + 20;
                    target.TextAlignment = TextAlignment.Left;

                    DoubleAnimation anim = new DoubleAnimation
                    {
                        From = containerWidth,
                        To = -target.Width, // Move until the padded width is fully gone
                        Duration = TimeSpan.FromSeconds((textWidth + containerWidth) / 40),
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(1)
                    };
                    transform.BeginAnimation(TranslateTransform.XProperty, anim);
                }
                else
                {
                    // SHORT TEXT: Stay Centered
                    target.Width = containerWidth;
                    target.TextAlignment = TextAlignment.Center;
                }
            }), DispatcherPriority.Loaded);
        }

        private async void PlayCurrent()
        {
            try
            {
                await PlayCurrentAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayCurrent failed: {ex.Message}");
            }
        }

        private async Task PlayCurrentAsync()
        {
            await CommitPendingLyrics();

            isRadioMode = false;
            if (_radioHandle != 0)
            {
                StopRadio();
            }
            ProgressSlider.IsEnabled = true;

            if (!EnsurePlayableCurrentIndex()) return;

            _temCachedLyricsText = null;

            // Update the ContextMenu dynamically if it's currently open
            if (this.ContextMenu != null && this.ContextMenu.IsOpen && MenuShowNextUp.IsChecked && !isRadioMode)
            {
                // The "Up Next" item is at index 0 based on your ContextMenu_Opened logic
                if (this.ContextMenu.Items.Count > 0 &&
                    this.ContextMenu.Items[0] is MenuItem upNextItem &&
                    upNextItem.Header.ToString().StartsWith("Up Next"))
                {
                    // Calculate the song that will play AFTER the one we just started
                    int nextShufflePos = currentIndex + 1;
                    if (nextShufflePos >= shuffleOrder.Count) nextShufflePos = 0;

                    int actualPlaylistIndex = shuffleOrder[nextShufflePos];
                    if (actualPlaylistIndex < 0 || actualPlaylistIndex >= playlist.Count) return;

                    var nextSong = playlist[actualPlaylistIndex];

                    if (File.Exists(nextSong))
                    {
                        try
                        {
                            using (var file = TagLib.File.Create(nextSong))
                            {
                                string title = !string.IsNullOrEmpty(file.Tag.Title)
                                    ? file.Tag.Title
                                    : System.IO.Path.GetFileNameWithoutExtension(nextSong);

                                string artist = file.Tag.FirstAlbumArtist ?? file.Tag.FirstPerformer ?? "Unknown Artist";

                                // Update the existing menu item header directly without re-inserting
                                upNextItem.Header = $"Up Next: {title} - {artist}";
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Could not read 'Up Next' metadata: {ex.Message}");
                        }
                    }
                }
            }

            int shuffledIndex = shuffleOrder[currentIndex];
            if (shuffledIndex < 0 || shuffledIndex >= playlist.Count) return;

            string filePath = playlist[shuffledIndex];
            currentFilePath = filePath;

            if (!PrepareLocalPlaybackStream(filePath, true))
            {
                isPlaying = false;
                UpdateUIStatus(false);
                return;
            }

            if (!Bass.BASS_ChannelPlay(_localHandle, false))
            {
                MessageBox.Show(
                    owner: this,
                    messageBoxText: $"Podmix could not start playback for {System.IO.Path.GetFileName(filePath)}.\n\nError: {Bass.BASS_ErrorGetCode()}",
                    caption: "Playback Error",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Error);
                isPlaying = false;
                UpdateUIStatus(false);
                return;
            }

            timer.Start();
            isPlaying = true;
            UpdateUIStatus(true);
            UpdateFavoriteMenuStatus();
            await UpdateMetadataAsync(filePath);
            UpdateUIStatus(true);
            UpdateFavoriteMenuStatus();

            ShowNotification(ImgAlbumArt.Source, TxtTitle.Text, TxtArtist.Text);
            UpdateDiscordPresence();

            if (isLyricsMode)
                ShowLyrics_Click(null, null);

            if (RatingOverlay.Visibility == Visibility.Visible)
                HideRatingOverlay();

            if (PlaylistsView.Visibility == Visibility.Visible)
                ImgPlaylistsBackground.Source = ImgAlbumArt.Source;

            if (CoverFlowView.Visibility == Visibility.Visible)
                ImgCoverFlowBackground.Source = ImgAlbumArt.Source;

            if (LyricsView.Visibility == Visibility.Visible)
                ImgLyricsBackground.Source = ImgAlbumArt.Source;

            if (AboutView.Visibility == Visibility.Visible)
                ImgAboutBackground.Source = ImgAlbumArt.Source;
        }

        private static async Task<byte[]> GetOnlineCoverAsync(string artist, string album)
        {
            string key = (string)Config.Get("discogs_key");
            string secret = (string)Config.Get("discogs_secret");

            try
            {
                // 1. Buscar releases en Discogs
                string query = Uri.EscapeDataString($"{artist} {album}");
                string searchUrl =
                    $"https://api.discogs.com/database/search?q={query}&type=release&per_page=5&key={key}&secret={secret}";

                httpClient.DefaultRequestHeaders.UserAgent.Clear();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("iPodMusicPlayer/1.0");

                string json = await httpClient.GetStringAsync(searchUrl);
                var obj = JObject.Parse(json);

                var results = obj["results"] as JArray;
                if (results == null || results.Count == 0)
                    return null;

                // 2. Elegir el release con mayor score
                JObject best = results
                    .OrderByDescending(r => (int?)r["score"] ?? 0)
                    .FirstOrDefault() as JObject;

                if (best == null)
                    return null;

                string coverUrl = best["cover_image"]?.ToString();
                if (string.IsNullOrEmpty(coverUrl))
                    return null;

                // 3. Descargar la imagen
                byte[] img = await httpClient.GetByteArrayAsync(coverUrl);
                return img;
            }
            catch
            {
                //MessageBox.Show("Discogs error: " + ex.Message);
                return null;
            }
        }
        
        private void UpdateMetadata(string path)
        {
            _ = UpdateMetadataAsync(path);
        }

        private async Task UpdateMetadataAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!IsMetadataUpdateStillCurrent(path))
                return;

            //ApplyFallbackMetadata(path);
            //ApplyFallbackMetadata(path, true);

            try
            {
                string title = System.IO.Path.GetFileNameWithoutExtension(path);
                string artist = "Unknown Artist";
                string album = "Unknown Album";
                byte[] embeddedArt = null;

                await Task.Run(() =>
                {
                    using (var file = TagLib.File.Create(path))
                    {
                        title = !string.IsNullOrWhiteSpace(file.Tag.Title)
                            ? file.Tag.Title
                            : System.IO.Path.GetFileNameWithoutExtension(path);

                        artist = file.Tag.FirstAlbumArtist
                            ?? file.Tag.FirstPerformer
                            ?? "Unknown Artist";

                        album = !string.IsNullOrWhiteSpace(file.Tag.Album)
                            ? file.Tag.Album
                            : "Unknown Album";

                        if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                            embeddedArt = file.Tag.Pictures[0].Data.Data;
                    }
                });

                if (!IsMetadataUpdateStillCurrent(path))
                    return;

                ApplyMetadataText(title, artist, album);

                if (_notifyIcon != null)
                    SetNotifyIconText($"{title} - {artist} | Podmix");

                if (embeddedArt != null && embeddedArt.Length > 0)
                {
                    try
                    {
                        var image = LoadImage(embeddedArt);
                        if (image != null)
                        {
                            ImgAlbumArt.Source = image;
                            TxtAlbumArt.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch
                    {
                        ClearAlbumArt();
                    }
                }
                else
                {
                    byte[] onlineBin = await GetOnlineCoverAsync(artist, album);
                    if (!IsMetadataUpdateStillCurrent(path))
                        return;

                    if (onlineBin != null && onlineBin.Length > 0)
                    {
                        var image = LoadImage(onlineBin);
                        if (image != null)
                        {
                            ImgAlbumArt.Source = image;
                            TxtAlbumArt.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Metadata read failed for '{path}': {ex.Message}");
                if (IsMetadataUpdateStillCurrent(path))
                    ApplyFallbackMetadata(path);
                    ApplyFallbackMetadata(path, false);
            }

            if (!IsMetadataUpdateStillCurrent(path))
                return;

            StartMarquee(TxtTitle, TitleTransform, TitleCanvas);
            StartMarquee(TxtArtist, ArtistTransform, ArtistCanvas);
            StartMarquee(TxtAlbum, AlbumTransform, AlbumCanvas);
            UpdateLockNowPlayingInfo();
            ApplyAlbumColorFromCurrentArt();
        }

        private bool IsMetadataUpdateStillCurrent(string path)
        {
            return !isRadioMode && !string.IsNullOrWhiteSpace(currentFilePath) && IsSamePath(currentFilePath, path);
        }

        private void ApplyFallbackMetadata(string path, bool isLoading = false)
        {
            string title = string.IsNullOrWhiteSpace(path)
                ? "Unknown Title"
                : System.IO.Path.GetFileNameWithoutExtension(path);

            ApplyMetadataText(title, "Unknown Artist", "Unknown Album");
            ApplyMetadataText(title, isLoading ? "" : "Unknown Artist", isLoading ? "" : "Unknown Album");
            ClearAlbumArt();

            if (_notifyIcon != null)
            {
                // Use the safe truncation logic
                const string Branding = " | Podmix";
                const string Ellipsis = "...";
                const int MaxTotalLength = 63;

                string details = $"{title} - Unknown Artist";
                string fullText = details + Branding;

                if (fullText.Length <= MaxTotalLength)
                {
                    SetNotifyIconText(fullText);
                }
                else
                {
                    int allowedDetailsLength = MaxTotalLength - Branding.Length - Ellipsis.Length;
                    string truncatedDetails = details.Substring(0, allowedDetailsLength);
                    SetNotifyIconText($"{truncatedDetails}{Ellipsis}{Branding}");
                }
            }
        }

        private void ApplyMetadataText(string title, string artist, string album)
        {
            TxtTitle.Text = string.IsNullOrWhiteSpace(title) ? "Unknown Title" : title;
            TxtArtist.Text = string.IsNullOrWhiteSpace(artist) ? "Unknown Artist" : artist;
            TxtAlbum.Text = string.IsNullOrWhiteSpace(album) ? "Unknown Album" : album;

            TipTitle.Text = TxtTitle.Text;
            TipArtist.Text = TxtArtist.Text;
            TipAlbum.Text = TxtAlbum.Text;
        }

        private void ClearAlbumArt()
        {
            ImgAlbumArt.Source = null;
            TxtAlbumArt.Visibility = Visibility.Visible;
            TxtAlbumArt.Text = "♪";
        }

        private void SetNotifyIconText(string rawDetails)
        {
            if (_notifyIcon == null)
                return;

            const string Branding = " | Podmix";
            const string Ellipsis = "...";
            const int MaxTotalLength = 63;

            string fullText = rawDetails.EndsWith(Branding) ? rawDetails : rawDetails + Branding;

            // If it already contains branding and is too long, strip it for calculation
            if (rawDetails.EndsWith(Branding))
            {
                rawDetails = rawDetails.Substring(0, rawDetails.Length - Branding.Length);
            }

            if (fullText.Length <= MaxTotalLength)
            {
                _notifyIcon.Text = fullText;
            }
            else
            {
                int allowedDetailsLength = MaxTotalLength - Branding.Length - Ellipsis.Length;
                string truncatedDetails = rawDetails.Substring(0, allowedDetailsLength);
                _notifyIcon.Text = $"{truncatedDetails}{Ellipsis}{Branding}";
            }
        }

        private bool ApplyAlbumColorFromCurrentArt()
        {
            bool extractAlbumCoverColor;
            try { extractAlbumCoverColor = (bool)Config.Get("extract_album_cover_color"); }
            catch { return false; }

            if (!extractAlbumCoverColor || !(ImgAlbumArt.Source is BitmapSource bitmapSource))
                return false;

            return ApplyAlbumColorFromBitmap(bitmapSource);
        }

        private async Task<bool> ApplyAlbumColorForFileAsync(string path)
        {
            bool extractAlbumCoverColor;
            try { extractAlbumCoverColor = (bool)Config.Get("extract_album_cover_color"); }
            catch { return false; }

            if (!extractAlbumCoverColor || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                byte[] artBytes = null;
                string artist = null;
                string album = null;

                await Task.Run(() =>
                {
                    using (var file = TagLib.File.Create(path))
                    {
                        if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                            artBytes = file.Tag.Pictures[0].Data.Data;

                        artist = file.Tag.FirstAlbumArtist ?? file.Tag.FirstPerformer ?? "Unknown Artist";
                        album = file.Tag.Album ?? "Unknown Album";
                    }
                });

                if ((artBytes == null || artBytes.Length == 0) && !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(album))
                    artBytes = await GetOnlineCoverAsync(artist, album);

                BitmapImage image = LoadImage(artBytes);
                if (image == null)
                    return false;

                return ApplyAlbumColorFromBitmap(image);
            }
            catch
            {
                return false;
            }
        }

        private bool ApplyAlbumColorFromBitmap(BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
                return false;

            Color bodyColor;
            try
            {
                bodyColor = ColorFromBitmapSource(bitmapSource);
            }
            catch
            {
                return false;
            }

            colorHex = $"#{bodyColor.R:X2}{bodyColor.G:X2}{bodyColor.B:X2}".ToLower();

            Color newColor = (Color)ColorConverter.ConvertFromString(colorHex);
            SolidColorBrush newBrush = new SolidColorBrush(newColor);

            // Use the opaque colour for the shared resources too. These drive the
            // outer bezel (MainContainer) and never should contain alpha.
            Resources["HoverColorBrush"] = newBrush;
            Resources["SliderTrackBrush"] = newBrush;

            IpodBody.Background = newBrush;
            CenterPlayBtn.Background = newBrush;
            CenterPlayBtn.Foreground = GetContrastBrush(newBrush.Color);
            IpodScreen.Background = Brushes.Black;
            PlaybackWheel.Fill = CalculateScreenBackground(colorHex);
            Resources["IpodScreenBrush"] = CalculateScreenBackground(colorHex);

            SaveViewState();
            return true;
        }

        // Helper method to keep the code clean
        private BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // Important for memory management
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze(); // Makes it cross-thread safe
            return image;
        }

        private void UpdateUIStatus(bool isPlaying)
        {
            MenuPlayPause.Header = isPlaying ? "Pause" : "Play";

            if (CenterPlayBtn.Template.FindName("PlayBtnIcon", CenterPlayBtn) is TextBlock icon)
            {
                icon.Text = isPlaying ? "❚❚" : "▶";
            }

            string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/", isPlaying ? "pause_icon.png" : "play_icon.png");
            string desc = isPlaying ? "Pause" : "Play";

            BtnPlayPause.ImageSource = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
            BtnPlayPause.Description = desc;

            TipTitle.Text = TxtTitle.Text;
            TipArtist.Text = TxtArtist.Text;
            TipAlbum.Text = TxtAlbum.Text;

            if (isRadioMode)
            {
                TxtCount.Text = "LIVE";
            }
            else if (playlist.Count == 0)
            {
                TxtCount.Text = "0 / 0";
            }
            else
            {
                TxtCount.Text = $"{(currentIndex + 1)} / {playlist.Count.ToMetricFormat()}";
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (HandleCenterButtonWhileLocked()) return;
            TogglePlayback(true);
        }

        private void TogglePlayback(bool allowAddFilesPrompt)
        {
            if (!isRadioMode && playlist.Count == 0)
            {
                if (_isStartupLoading)
                {
                    _playRequestedDuringStartup = true;
                    return;
                }

                if (!allowAddFilesPrompt)
                    return;

                AddFiles_Click(null, null);
                return;
            }

            if (!isRadioMode)
            {
                if (isPlaying)
                {
                    if (_localHandle != 0)
                        Bass.BASS_ChannelPause(_localHandle);
                    timer.Stop();
                    isPlaying = false;
                    UpdateUIStatus(false);
                    _discord.ClearPresence();
                }
                else
                {
                    if (!EnsurePlayableCurrentIndex())
                    {
                        if (_isStartupLoading)
                        {
                            _playRequestedDuringStartup = true;
                            return;
                        }

                        if (!allowAddFilesPrompt)
                            return;

                        AddFiles_Click(null, null);
                        return;
                    }

                    if (_localHandle == 0 || string.IsNullOrWhiteSpace(_openedMediaPath) || !IsSamePath(_openedMediaPath, currentFilePath))
                    {
                        PlayCurrent();
                        return;
                    }

                    ApplyBassVolume();
                    Bass.BASS_ChannelPlay(_localHandle, false);
                    timer.Start();
                    isPlaying = true;
                    UpdateUIStatus(true);
                    UpdateDiscordPresence();
                }
            }
            else
            {
                if (_radioHandle != 0)
                {
                    if (isPlaying)
                    {
                        // Pauses the radio stream output
                        Bass.BASS_ChannelPause(_radioHandle);
                        isPlaying = false;
                        UpdateUIStatus(false);
                        _discord.ClearPresence();
                    }
                    else
                    {
                        // Resumes the radio stream output
                        // 'false' means it doesn't restart from the beginning
                        Bass.BASS_ChannelPlay(_radioHandle, false);
                        isPlaying = true;
                        UpdateUIStatus(true);
                        UpdateDiscordPresence();
                    }
                }
            }

        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            //if (IsDeviceInputBlocked()) return;
            if (_isAppJustStarted) return;
            AdvanceToNextTrack();
        }

        private void AdvanceToNextTrack()
        {
            if (isRadioMode || playlist.Count == 0) return;
            currentIndex++;
            if (currentIndex >= playlist.Count)
            {
                currentIndex = 0;
                GenerateShuffleOrder(); 
            }
            PlayCurrent();
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            //if (IsDeviceInputBlocked()) return;
            if (_isAppJustStarted) return;
            if (isRadioMode || playlist.Count == 0) return;

            if (ShouldRestartCurrentTrackFromPrevious())
            {
                RestartCurrentTrack();
                return;
            }

            currentIndex--;
            if (currentIndex < 0) currentIndex = playlist.Count - 1;
            PlayCurrent();
        }

        private bool ShouldRestartCurrentTrackFromPrevious()
        {
            if (_localHandle == 0)
                return false;

            long positionBytes = Bass.BASS_ChannelGetPosition(_localHandle);
            double positionSeconds = Bass.BASS_ChannelBytes2Seconds(_localHandle, positionBytes);
            return positionSeconds >= PreviousButtonRestartThresholdSeconds;
        }

        private void RestartCurrentTrack()
        {
            if (_localHandle == 0)
                return;

            Bass.BASS_ChannelSetPosition(_localHandle, Bass.BASS_ChannelSeconds2Bytes(_localHandle, 0));
            ProgressSlider.Value = 0;
            TimeNow.Text = "00:00";

            if (isPlaying)
            {
                Bass.BASS_ChannelPlay(_localHandle, false);
                _discord.ClearPresence();
                UpdateDiscordPresence();
            }
        }

        private async void IpodBody_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDevicePoweredOff || _isDeviceLocked) return;

            if (e.ClickCount == 2)
            {
                if (playlist.Count > 0)
                {
                    TriggerShake();
                    GenerateShuffleOrder();
                    currentIndex = 0;
                    PlayCurrent();

                    // Visual feedback
                    string currentTitle = TxtTitle.Text;
                    TxtTitle.Text = "🎲 Playlist Re-shuffled!";

                    await Task.Delay(3000);

                    // Instead of restoring a saved string, 
                    // we refresh from the actual current song in case it changed
                    if (currentIndex >= 0 && currentIndex < shuffleOrder.Count)
                    {
                        int shuffledIndex = shuffleOrder[currentIndex];
                        if (shuffledIndex >= 0 && shuffledIndex < playlist.Count)
                        {
                            string path = playlist[shuffledIndex];
                            UpdateMetadata(path);
                        }
                    }
                }
            }
        }

        #endregion

        #region UI Handlers
        private void OpenLibraryInExplorer_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("explorer.exe", Root); }
            catch { }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (isRadioMode) return;

            if (!isDraggingSlider && _localHandle != 0)
            {
                long lengthBytes = Bass.BASS_ChannelGetLength(_localHandle);
                long positionBytes = Bass.BASS_ChannelGetPosition(_localHandle);
                double lengthSeconds = Bass.BASS_ChannelBytes2Seconds(_localHandle, lengthBytes);
                double positionSeconds = Bass.BASS_ChannelBytes2Seconds(_localHandle, positionBytes);

                if (lengthSeconds > 0)
                {
                    ProgressSlider.Maximum = lengthSeconds;
                    ProgressSlider.Value = Math.Max(0, Math.Min(lengthSeconds, positionSeconds));
                    TimeNow.Text = TimeSpan.FromSeconds(positionSeconds).ToString(@"m\:ss");
                    TimeEnd.Text = TimeSpan.FromSeconds(lengthSeconds).ToString(@"m\:ss");
                }
            }
        }

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            isDraggingSlider = true; // Timer should now ignore the slider
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            isDraggingSlider = false;
            // Set the position immediately when they let go of the thumb
            if (_localHandle != 0)
                Bass.BASS_ChannelSetPosition(_localHandle, Bass.BASS_ChannelSeconds2Bytes(_localHandle, ProgressSlider.Value));
        }

        private void Slider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_localHandle != 0)
                Bass.BASS_ChannelSetPosition(_localHandle, Bass.BASS_ChannelSeconds2Bytes(_localHandle, ProgressSlider.Value));
        }

        private void ToggleDetailedView_Click(object sender, RoutedEventArgs e)
        {
            if (IpodScreen.Visibility == Visibility.Collapsed)
            {
                IpodScreen.Visibility = Visibility.Visible;
                ToolTipService.SetIsEnabled(IpodBody, false);
                IpodScreen.Width = 260; // Set explicit width for alignment
                IpodBody.Width = 260;   // Body grows to match screen
                IpodBody.Height = 170;
                IpodBody.CornerRadius = new CornerRadius(0, 0, 20, 20);
                IpodShellFrame.Visibility = Visibility.Visible;
                MainContainer.SetResourceReference(Border.BackgroundProperty, "HoverColorBrush");
                MainContainer.Effect = CreateDeviceShadow();
                IpodBody.Effect = null;
                isDetailed = true;
            }
            else
            {
                IpodScreen.Visibility = Visibility.Collapsed;
                ToolTipService.SetIsEnabled(IpodBody, true);
                IpodBody.Width = 180;   // Shrink back to original shuffle size
                IpodBody.Height = 170;
                IpodBody.CornerRadius = new CornerRadius(20);
                IpodShellFrame.Visibility = Visibility.Collapsed;
                MainContainer.Background = Brushes.Transparent;
                MainContainer.Effect = null;
                IpodBody.Effect = CreateDeviceShadow();
                isDetailed = false;
            }

            InvalidateMeasure();
            InvalidateArrange();
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;
            SaveViewState();
            Dispatcher.BeginInvoke(new Action(() => SettleWindowOnTaskbar(true)), DispatcherPriority.Loaded);
        }

        private void MenuTopMost_Checked(object sender, RoutedEventArgs e)
        {
            Topmost = true;
            Config.Save("top_most", true);
        }

        private void MenuTopMost_Unchecked(object sender, RoutedEventArgs e)
        {
            Topmost = false;
            Config.Save("top_most", false);
        }

        // The shadow must fit inside the transparent window margin (MainContainer Margin = 15),
        // otherwise it is clipped at the window edge and shows up as a hard-edged dark halo.
        private static DropShadowEffect CreateDeviceShadow()
        {
            return new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 2,
                Opacity = 0.32,
                Color = (Color)ColorConverter.ConvertFromString("#222222")
            };
        }

        private string GetDominantColor(BitmapSource bitmap)
        {
            if (bitmap == null) return "#333333";

            // Resize image to 1x1 to get the average color of all pixels
            var formatConvertedBitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var scaledBitmap = new TransformedBitmap(formatConvertedBitmap, new ScaleTransform(1.0 / bitmap.PixelWidth, 1.0 / bitmap.PixelHeight));

            byte[] pixels = new byte[4];
            scaledBitmap.CopyPixels(pixels, 4, 0);

            // Convert bytes to Hex string
            return $"#{pixels[2]:X2}{pixels[1]:X2}{pixels[0]:X2}";
        }

        private void ApplyThemeColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                hex = DefaultThemeColor;

            colorHex = hex.StartsWith("#") ? hex.ToLower() : $"#{hex}".ToLower();
            hex = colorHex;

            Color newColor = (Color)ColorConverter.ConvertFromString(hex);
            SolidColorBrush newBrush = new SolidColorBrush(newColor);

            // Apply to UI Elements
            IpodBody.Background = newBrush;
            CenterPlayBtn.Background = newBrush;
            CenterPlayBtn.Foreground = GetContrastBrush(newBrush.Color);

            SolidColorBrush screenBrush = CalculateScreenBackground(hex);
            IpodScreen.Background = Brushes.Black;
            PlaybackWheel.Fill = screenBrush;

            // Update Resources for Dynamic Styles
            Resources["IpodScreenBrush"] = screenBrush;
            Resources["HoverColorBrush"] = newBrush;
            Resources["SliderTrackBrush"] = newBrush;

            isDetailed = IpodScreen.Visibility == Visibility.Visible;
            SaveViewState();
        }

        private void MenuExtractColor_Checked(object sender, RoutedEventArgs e)
        {
            Config.Save("extract_album_cover_color", true);
            ApplyAlbumColorFromCurrentArt();
        }

        private void MenuExtractColor_Unchecked(object sender, RoutedEventArgs e)
        {
            Config.Save("extract_album_cover_color", false);
        }

        private SolidColorBrush CalculateScreenBackground(string hexColor)
        {
            Color bodyColor = (Color)ColorConverter.ConvertFromString(hexColor);

            // Lowering this factor (e.g., to 0.85) makes the body color "leak" 
            // into the white more strongly. 
            double whiteFactor = 0.85;

            byte r = (byte)(bodyColor.R + (255 - bodyColor.R) * whiteFactor);
            byte g = (byte)(bodyColor.G + (255 - bodyColor.G) * whiteFactor);
            byte b = (byte)(bodyColor.B + (255 - bodyColor.B) * whiteFactor);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private void GenerateColorIcons()
        {
            foreach (var item in ColorMenu.Items)
            {
                if (item is MenuItem mi && mi.Tag != null)
                {
                    try
                    {
                        string hex = mi.Tag.ToString();
                        Color col = (Color)ColorConverter.ConvertFromString(hex);

                        // Create a small colored square using a Border
                        Border iconVisual = new Border
                        {
                            Width = 16,
                            Height = 16,
                            Background = new SolidColorBrush(col),
                            CornerRadius = new CornerRadius(2), // Slightly rounded corners
                            BorderBrush = new SolidColorBrush(Colors.Black),
                            BorderThickness = new Thickness(0.5),
                            SnapsToDevicePixels = true
                        };

                        mi.Icon = iconVisual;
                    }
                    catch { /* Skip if hex is invalid */ }
                }
            }
        }

        private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                // 1. Get the parent container (the ContextMenu itself)
                var parent = mi.Parent as ItemsControl;
                if (parent != null)
                {
                    foreach (var item in parent.Items)
                    {
                        // 2. Force all other sibling submenus to close immediately
                        if (item is MenuItem otherMi && otherMi != mi)
                        {
                            if (otherMi.IsSubmenuOpen)
                            {
                                otherMi.IsSubmenuOpen = false;
                            }
                        }
                    }
                }

                // 3. Optional: If the item you just hovered HAS a submenu, 
                // open it immediately instead of waiting for the Windows delay.
                if (mi.HasItems)
                {
                    mi.IsSubmenuOpen = true;
                }
            }
        }


        public Color ColorFromBitmapSource(BitmapSource bitmapSource)
        {
            if (bitmapSource == null || bitmapSource.PixelWidth <= 0 || bitmapSource.PixelHeight <= 0)
                return Color.FromRgb(0x33, 0x33, 0x33);

            // 1. Resize image to 1x1 to let WPF's hardware scaling average the colors for us
            // This is much faster than looping through thousands of pixels manually
            var formatConvertedBitmap = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);
            var scaledBitmap = new TransformedBitmap(formatConvertedBitmap, new ScaleTransform(1.0 / bitmapSource.PixelWidth, 1.0 / bitmapSource.PixelHeight));

            byte[] pixels = new byte[4];
            scaledBitmap.CopyPixels(pixels, 4, 0);

            // pixels[0] = Blue, pixels[1] = Green, pixels[2] = Red, pixels[3] = Alpha
            // IMPORTANT: ignore the artwork's alpha channel and always return an opaque
            // colour. Album art containing transparency used to produce A < 255 here,
            // which made "HoverColorBrush" (the outer iPod bezel / MainContainer
            // background) translucent or completely invisible.
            return Color.FromArgb(255, pixels[2], pixels[1], pixels[0]);
        }

        public Brush GetContrastBrush(Color backgroundColor)
        {
            // Standard WCAG formula for relative luminance
            double luminance = (0.2126 * backgroundColor.R + 0.7152 * backgroundColor.G + 0.0722 * backgroundColor.B) / 255.0;

            // If background is light (> 0.5), use black icon. Otherwise, use white.
            return luminance > 0.5 ? Brushes.Black : Brushes.White;
        }

        private void ChangeColor_Click(object sender, RoutedEventArgs e)
        {
            Config.Save("extract_album_cover_color", false);
            MenuExtractColor.IsChecked = false;
            MenuItem item = sender as MenuItem;
            if (item?.Tag != null && item.Name != "MenuExtractColor")
            {
                colorHex = item.Tag.ToString();
                Color newColor = (Color)ColorConverter.ConvertFromString(colorHex);
                SolidColorBrush newBrush = new SolidColorBrush(newColor);
                IpodBody.Background = newBrush;
                CenterPlayBtn.Background = newBrush;
                CenterPlayBtn.Foreground = GetContrastBrush(newBrush.Color);
                IpodScreen.Background = Brushes.Black;
                PlaybackWheel.Fill = CalculateScreenBackground(colorHex);
                Resources["IpodScreenBrush"] = CalculateScreenBackground(colorHex);
                Color bodyColor = (Color)ColorConverter.ConvertFromString(colorHex);
                Resources["HoverColorBrush"] = new SolidColorBrush(bodyColor);
                Resources["SliderTrackBrush"] = new SolidColorBrush(bodyColor);
                SaveViewState();
            }
        }

        private async void CleanUpAndExit()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            await ShowShutdownViewAsync();

            if (timer.IsEnabled) timer.Stop();
            StopLocalPlayback(false);
            StopRadio();
            FreeBassDevices();
            _discord.Dispose();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            string tempPath = System.IO.Path.Combine(
                           System.IO.Path.GetTempPath(),
                           "podmix_nowplaying.png");
            if (File.Exists(tempPath)) 
                try { File.Delete(tempPath); } catch { }

            await CommitPendingLyrics();
            Application.Current.Shutdown();
        }

        public void StopRadio()
        {
            isRadioMode = false;
            _lastRadioMetadata = string.Empty;
            _radioMetadataFallbackApplied = false;
            StopRadioReconnectTimer();
            StopRadioMetadataFallbackTimer();

            if (_radioHandle != 0)
            {
                StopActiveRecording();
                Bass.BASS_ChannelStop(_radioHandle);
                Bass.BASS_StreamFree(_radioHandle);
                _radioHandle = 0;
            }
            ProgressSlider.IsEnabled = true;
            _discord.ClearPresence();
        }



        // Taskbar-specific wrappers
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            BtnPrev.ImageSource = LoadIcon(System.IO.Path.Combine(baseDir, "assets/prev_icon.png"));
            BtnPlayPause.ImageSource = LoadIcon(System.IO.Path.Combine(baseDir, "assets/play_icon.png"));
            BtnNext.ImageSource = LoadIcon(System.IO.Path.Combine(baseDir, "assets/next_icon.png"));

            BtnTogglePlaylists.Content = new Image
            {
                Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/playlist_icon.png")),
                Width = 14,
                Height = 14
            };

            BtnShowSearch.Content = new Image
            {
                Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/search_icon.png")),
                Width = 14,
                Height = 14
            };

            SettleWindowOnTaskbar(false);

            // Initialize the System Tray Icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);

            _notifyIcon.Visible = false;
            _notifyIcon.Text = "Podmix";

            // Double click the tray icon to restore the window
            _notifyIcon.DoubleClick += (s, args1) =>
            {
                RestoreMainWindow();
            };

            bool shellIntegration = (bool)Config.Get("shell_integration");

            // Unhook events temporarily to prevent re-running registry logic during startup
            MenuIntegrate.Checked -= MenuIntegrate_Checked;
            MenuIntegrate.Unchecked -= MenuIntegrate_Unchecked;

            MenuIntegrate.IsChecked = shellIntegration;

            // Re-hook events
            MenuIntegrate.Checked += MenuIntegrate_Checked;
            MenuIntegrate.Unchecked += MenuIntegrate_Unchecked;

            _isFirstLoad = false;
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            if (!_notifyIcon.Visible) _notifyIcon.Visible = true;
            string meta = string.Empty;

            if (!isRadioMode)
            {
                string title = TxtTitle.Text;
                string artist = TxtArtist.Text;
                meta = $"{title} - {artist} | Podmix";
            }
            else
            {
                // Check if the text contains a dash before splitting to avoid IndexOutOfRangeException
                if (TxtArtist.Text != null && TxtArtist.Text.Contains("-"))
                {
                    var split = TxtArtist.Text.Split(new[] { '-' }, 2);
                    meta = split.Length > 1
                        ? $"{split[0].Trim()} - {split[1].Trim()} | Podmix"
                        : $"{TxtArtist.Text} | Podmix";
                }
                else
                {
                    // Fallback for "Connecting...", "Buffering...", or single-name stations
                    meta = $"{TxtArtist.Text} | Podmix";
                }
            }

            if (meta.Length >= 64)
            {
                meta = meta.Substring(0, 60) + "...";
            }

            _notifyIcon.Text = meta;
            ShowInTaskbar = false;
            this.Hide();
        }

        public void RestoreMainWindow()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RestoreMainWindow);
                return;
            }

            ShowInTaskbar = true;
            Show();
            Visibility = Visibility.Visible;

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            if (_notifyIcon != null)
                _notifyIcon.Visible = false;

            Activate();
            Dispatcher.BeginInvoke(new Action(() => SettleWindowOnTaskbar(false)), DispatcherPriority.Loaded);
        }

        private BitmapImage LoadIcon(string path)
        {
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }

        private void Taskbar_PrevClick(object sender, EventArgs e) => Prev_Click(null, null);
        private void Taskbar_PlayPauseClick(object sender, EventArgs e) => PlayPause_Click(null, null);
        private void Taskbar_NextClick(object sender, EventArgs e) => Next_Click(null, null);

        private void VolumeUp_Click(object sender, RoutedEventArgs e)
        {
            if (MenuMute.IsChecked) MenuMute.IsChecked = false;
            _playerVolume = Math.Min(1.0, _playerVolume + 0.01);
            SaveVolume();
            ApplyBassVolume();

            ShowLiveVolume();
        }

        private void VolumeDown_Click(object sender, RoutedEventArgs e)
        {
            if (MenuMute.IsChecked) MenuMute.IsChecked = false;
            _playerVolume = Math.Max(0.0, _playerVolume - 0.01);
            SaveVolume();
            ApplyBassVolume();

            ShowLiveVolume();
        }

        private void SaveVolume()
        {
            try { Config.Save("volume", (int)(_playerVolume * 100)); }
            catch { }
        }

        private void ShowVolumeTooltip(object sender, MouseEventArgs e)
        {
            ShowLiveVolume();
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isDevicePoweredOff)
                ResetScreenSleepTimer(IsPointerOverIpodScreen(e));

            if (_isDevicePoweredOff || _isDeviceLocked)
            {
                if (e.Delta > 0)
                    VolumeUp_Click(null, null);
                else if (e.Delta < 0)
                    VolumeDown_Click(null, null);

                e.Handled = true;
                return;
            }

            if (CoverFlowView.Visibility == Visibility.Visible)
            {
                MoveCoverFlow(e.Delta < 0 ? 1 : -1);
                e.Handled = true;
                return;
            }

            if (e.Delta > 0)
                _playerVolume = Math.Min(1.0, _playerVolume + 0.02);
            else if (e.Delta < 0)
                _playerVolume = Math.Max(0.0, _playerVolume - 0.02);

            if (MenuMute.IsChecked) MenuMute.IsChecked = false;

            SaveVolume();
            ApplyBassVolume();

            ShowLiveVolume();
        }

        private void WheelSurface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point = e.GetPosition(WheelSurface);
            if (!IsPointOnWheelRing(point))
                return;

            _isWheelVolumePending = true;
            _isWheelVolumeDragging = false;
            _wheelVolumeStartedOnButton = IsEventFromButton(e.OriginalSource as DependencyObject);
            _wheelDragStartPoint = point;
            _lastWheelAngle = GetWheelAngle(point);
            _wheelVolumeAccumulator = 0;
            ResetScreenSleepTimer(false);

            if (!_wheelVolumeStartedOnButton)
            {
                WheelSurface.CaptureMouse();
                e.Handled = true;
            }
        }

        private void WheelSurface_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isWheelVolumePending)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndWheelVolumeDrag();
                return;
            }

            Point point = e.GetPosition(WheelSurface);
            if (!_isWheelVolumeDragging)
            {
                double dx = point.X - _wheelDragStartPoint.X;
                double dy = point.Y - _wheelDragStartPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < 4)
                    return;

                _isWheelVolumeDragging = true;
                WheelSurface.CaptureMouse();
            }

            double angle = GetWheelAngle(point);
            double delta = NormalizeWheelAngleDelta(angle - _lastWheelAngle);
            _lastWheelAngle = angle;
            _wheelVolumeAccumulator += delta;

            int steps = 0;
            while (_wheelVolumeAccumulator >= WheelVolumeDegreesPerStep)
            {
                steps++;
                _wheelVolumeAccumulator -= WheelVolumeDegreesPerStep;
            }

            while (_wheelVolumeAccumulator <= -WheelVolumeDegreesPerStep)
            {
                steps--;
                _wheelVolumeAccumulator += WheelVolumeDegreesPerStep;
            }

            if (steps != 0)
                AdjustVolumeByWheelSteps(steps);

            ResetScreenSleepTimer(false);
            e.Handled = true;
        }

        private void WheelSurface_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isWheelVolumePending)
                return;

            bool handled = _isWheelVolumeDragging || !_wheelVolumeStartedOnButton;
            EndWheelVolumeDrag();
            ResetScreenSleepTimer(false);
            e.Handled = handled;
        }

        private void EndWheelVolumeDrag()
        {
            _isWheelVolumePending = false;
            _isWheelVolumeDragging = false;
            _wheelVolumeStartedOnButton = false;
            _wheelVolumeAccumulator = 0;

            if (WheelSurface.IsMouseCaptured)
                WheelSurface.ReleaseMouseCapture();
        }

        private bool IsPointOnWheelRing(Point point)
        {
            double centerX = WheelSurface.ActualWidth / 2.0;
            double centerY = WheelSurface.ActualHeight / 2.0;
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            return distance >= 28 && distance <= 68;
        }

        private double GetWheelAngle(Point point)
        {
            double centerX = WheelSurface.ActualWidth / 2.0;
            double centerY = WheelSurface.ActualHeight / 2.0;
            return Math.Atan2(point.Y - centerY, point.X - centerX) * 180.0 / Math.PI;
        }

        private double NormalizeWheelAngleDelta(double delta)
        {
            while (delta > 180) delta -= 360;
            while (delta < -180) delta += 360;
            return delta;
        }

        private void AdjustVolumeByWheelSteps(int steps)
        {
            if (steps == 0)
                return;

            if (MenuMute.IsChecked)
                MenuMute.IsChecked = false;

            _playerVolume = Math.Max(0.0, Math.Min(1.0, _playerVolume + (steps * 0.02)));
            SaveVolume();
            ApplyBassVolume();
            ShowLiveVolume();
        }

        private bool IsEventFromButton(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button)
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private double preMuteVolume = 0.5;
        private void MenuMute_Checked(object sender, RoutedEventArgs e)
        {
            // Save the current volume before we kill it
            preMuteVolume = _playerVolume;
            _playerVolume = 0;
            ApplyBassVolume();

            ShowLiveVolume();
        }

        private void MenuMute_Unchecked(object sender, RoutedEventArgs e)
        {
            // Restore the volume we had before
            _playerVolume = preMuteVolume;
            if (_playerVolume <= 0) _playerVolume = 0.5;
            ApplyBassVolume();

            ShowLiveVolume();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void Exit_Click(object sender, RoutedEventArgs e) => CleanUpAndExit();
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            string helpPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "help.html");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = helpPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(owner: this,
                    messageBoxText: "Help file not found.",
                    caption: "Error",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Error);
            }

        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            ShowAbout();
        }

        private void ShowAbout()
        {
            bool wasCoverFlowMode = isCoverFlowMode;
            isCoverFlowMode = false;

            if (isLyricsMode || wasCoverFlowMode)
            {
                isLyricsMode = false;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                BtnShowSearch.Content = new Image { Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/search_icon.png")), Width = 14, Height = 14 };
                BtnTogglePlaylists.Content = new Image { Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/playlist_icon.png")), Width = 14, Height = 14 };
                BtnShowSearch.Visibility = Visibility.Visible;
            }

            if (IpodScreen.Visibility == Visibility.Collapsed)
                ToggleDetailedView_Click(null, null);

            // Ensure all other specialty views are hidden
            LyricsView.Visibility = Visibility.Collapsed;
            PlaylistsView.Visibility = Visibility.Collapsed;
            CoverFlowView.Visibility = Visibility.Collapsed;

            TxtTitle.Visibility = TxtArtist.Visibility = TxtAlbum.Visibility =
            TimeNow.Visibility = TxtCount.Visibility = TimeEnd.Visibility =
            ProgressSlider.Visibility = Visibility.Collapsed;

            ImgAboutBackground.Source = ImgAlbumArt.Source;
            AboutView.Visibility = Visibility.Visible;

            TxtCopyright.Text = $"© {DateTime.Now.Year} Exe Innovate";

            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "appicon.png");
                ImgAboutIcon.Source = new BitmapImage(new Uri(iconPath));
            }
            catch { /* Fallback to hidden if extraction fails */ }
        }

        private void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInternetAvailable())
            {
                MessageBox.Show(owner: this,
                    messageBoxText: "No internet connection.",
                    caption: "Error",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Error);
                return;
            }

            CheckUpdatesWindow updates = new CheckUpdatesWindow();
            updates.Owner = this;
            updates.ShowDialog();
        }

        // Linked to the "Back to Player" button
        private void BackToPlayer_Click(object sender, RoutedEventArgs e)
        {
            bool wasCoverFlowMode = isCoverFlowMode;
            isCoverFlowMode = false;

            if (isLyricsMode || wasCoverFlowMode)
            {
                isLyricsMode = false;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                BtnShowSearch.Content = new Image
                {
                    Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/search_icon.png")),
                    Width = 14,
                    Height = 14
                };
                BtnTogglePlaylists.Content = new Image
                {
                    Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/playlist_icon.png")),
                    Width = 14,
                    Height = 14
                };

                BtnShowSearch.Visibility = Visibility.Visible;
            }

            AboutView.Visibility = Visibility.Collapsed;
            PlaylistsView.Visibility = Visibility.Collapsed;
            LyricsView.Visibility = Visibility.Collapsed;
            CoverFlowView.Visibility = Visibility.Collapsed;

            PlayerView.Visibility = Visibility.Visible; // Ensure main player view is back

            // Restore visibility of controls
            TxtTitle.Visibility = TxtArtist.Visibility = TxtAlbum.Visibility =
            TimeNow.Visibility = TxtCount.Visibility = TimeEnd.Visibility =
            ProgressSlider.Visibility = Visibility.Visible;
        }

        private async void ShowCoverFlow_Click(object sender, RoutedEventArgs e)
        {
            await ShowCoverFlowAsync();
        }

        //private async void BeginCoverFlowBackgroundScan()
        //{
        //    if (_isCoverFlowLoading) return;

        //    MenuCoverFlow.Visibility = Visibility.Collapsed;
        //    MenuCoverFlow.IsEnabled = false;
        //    _isCoverFlowReady = false;

        //    if (string.IsNullOrEmpty(Root) || !Directory.Exists(Root))
        //        return;

        //    _isCoverFlowLoading = true;

        //    try
        //    {
        //        var albums = await Task.Run(() => ScanCoverFlowAlbums(Root));
        //        _coverFlowAlbums.Clear();
        //        _coverFlowAlbums.AddRange(albums);
        //        _coverFlowIndex = 0;
        //        _isCoverFlowReady = _coverFlowAlbums.Count > 0;

        //        MenuCoverFlow.Visibility = _isCoverFlowReady ? Visibility.Visible : Visibility.Collapsed;
        //        MenuCoverFlow.IsEnabled = _isCoverFlowReady;

        //        if (isCoverFlowMode)
        //            UpdateCoverFlowUI();
        //    }
        //    finally
        //    {
        //        _isCoverFlowLoading = false;
        //    }
        //}

        private async void BeginCoverFlowBackgroundScan()
        {
            if (_isCoverFlowLoading) return;

            MenuCoverFlow.Visibility = Visibility.Collapsed;
            MenuCoverFlow.IsEnabled = false;
            _isCoverFlowReady = false;

            if (string.IsNullOrEmpty(Root) || !Directory.Exists(Root))
                return;

            _isCoverFlowLoading = true;

            try
            {
                var albums = await Task.Run(() => ScanCoverFlowAlbums(Root));

                _coverFlowAlbums.Clear();
                _coverFlowAlbums.AddRange(albums);

                _coverFlowIndex = 0;
                _isCoverFlowReady = _coverFlowAlbums.Count > 0;

                MenuCoverFlow.Visibility = _isCoverFlowReady
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                MenuCoverFlow.IsEnabled = _isCoverFlowReady;

                if (isCoverFlowMode)
                    UpdateCoverFlowUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Cover Flow scan error: " + ex.Message);

                _isCoverFlowReady = false;
                MenuCoverFlow.Visibility = Visibility.Collapsed;
                MenuCoverFlow.IsEnabled = false;
            }
            finally
            {
                _isCoverFlowLoading = false;
            }
        }

        private async Task ShowCoverFlowAsync()
        {
            if (!_isCoverFlowReady || _coverFlowAlbums.Count == 0)
                return;

            HideRatingOverlayImmediately();

            if (IpodScreen.Visibility == Visibility.Collapsed)
                ToggleDetailedView_Click(null, null);

            isLyricsMode = false;
            isCoverFlowMode = true;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            BtnTogglePlaylists.Content = new Image
            {
                Source = LoadIcon(System.IO.Path.Combine(baseDir, "assets/close_icon.png")),
                Width = 14,
                Height = 14
            };

            BtnShowSearch.Visibility = Visibility.Collapsed;

            PlayerView.Visibility = Visibility.Collapsed;
            PlaylistsView.Visibility = Visibility.Collapsed;
            LyricsView.Visibility = Visibility.Collapsed;
            AboutView.Visibility = Visibility.Collapsed;

            TxtTitle.Visibility = TxtArtist.Visibility = TxtAlbum.Visibility =
            TimeNow.Visibility = TxtCount.Visibility = TimeEnd.Visibility =
            ProgressSlider.Visibility = Visibility.Collapsed;

            ImgCoverFlowBackground.Source = ImgAlbumArt.Source;
            CoverFlowView.Visibility = Visibility.Visible;
            UpdateCoverFlowUI();
            await Task.CompletedTask;
        }

        private List<CoverFlowAlbum> ScanCoverFlowAlbums(string rootPath)
        {
            var albums = new Dictionary<string, CoverFlowAlbum>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in GetAudioFilesFromFolder(rootPath, SearchOption.AllDirectories))
            {
                try
                {
                    using (var file = TagLib.File.Create(path))
                    {
                        string albumName = !string.IsNullOrWhiteSpace(file.Tag.Album)
                            ? file.Tag.Album
                            : "Unknown Album";

                        string artist = file.Tag.FirstAlbumArtist
                            ?? file.Tag.FirstPerformer
                            ?? "Unknown Artist";

                        string key = $"{artist}|{albumName}";
                        if (!albums.TryGetValue(key, out CoverFlowAlbum album))
                        {
                            album = new CoverFlowAlbum
                            {
                                AlbumName = albumName,
                                Artist = artist
                            };
                            albums[key] = album;
                        }

                        album.Tracks.Add(new CoverFlowTrack
                        {
                            Path = path,
                            TrackNumber = file.Tag.Track
                        });

                        // Remember the exact track that contains embedded artwork.
                        // Decode the image later, only when this album is visible in Cover Flow.
                        if (string.IsNullOrEmpty(album.CoverArtPath) && file.Tag.Pictures.Length > 0)
                        {
                            album.CoverArtPath = path;
                        }
                    }
                }
                catch
                {
                }
            }

            return albums.Values
                         .Where(a => a.Tracks.Count > 0)
                         .OrderBy(a => a.Artist)
                         .ThenBy(a => a.AlbumName)
                         .ToList();
        }

        //private ImageSource CreateImageSource(byte[] data)
        //{
        //    try
        //    {
        //        using (var stream = new MemoryStream(data))
        //        {
        //            var image = new BitmapImage();
        //            image.BeginInit();
        //            image.CacheOption = BitmapCacheOption.OnLoad;
        //            image.StreamSource = stream;
        //            image.EndInit();
        //            image.Freeze();
        //            return image;
        //        }
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}

        private ImageSource CreateImageSource(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                using (var stream = new MemoryStream(data))
                {
                    var image = new BitmapImage();

                    image.BeginInit();

                    // Decode smaller images to reduce memory usage
                    image.DecodePixelWidth = 256;

                    // Release the stream after decoding
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;

                    image.EndInit();
                    image.Freeze();

                    return image;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Cover artwork error: " + ex.Message);

                return null;
            }
        }

        private void MoveCoverFlow(int direction)
        {
            if (_coverFlowAlbums.Count == 0) return;

            _coverFlowIndex = (_coverFlowIndex + direction + _coverFlowAlbums.Count) % _coverFlowAlbums.Count;
            UpdateCoverFlowUI();
        }

        private void UpdateCoverFlowUI()
        {
            SetCoverFlowImage(CoverFlowMinus2, _coverFlowIndex - 2);
            SetCoverFlowImage(CoverFlowMinus1, _coverFlowIndex - 1);
            SetCoverFlowImage(CoverFlowCenter, _coverFlowIndex);
            SetCoverFlowImage(CoverFlowPlus1, _coverFlowIndex + 1);
            SetCoverFlowImage(CoverFlowPlus2, _coverFlowIndex + 2);

            if (_coverFlowAlbums.Count == 0)
            {
                TxtCoverFlowAlbum.Text = _isCoverFlowLoading ? "Scanning albums..." : "No albums found";
                TxtCoverFlowArtist.Text = "";
                return;
            }

            CoverFlowAlbum focused = _coverFlowAlbums[_coverFlowIndex];
            TxtCoverFlowAlbum.Text = focused.AlbumName;
            TxtCoverFlowArtist.Text = $"{focused.Artist} - {focused.Tracks.Count} songs";
        }

        private void SetCoverFlowImage(Image image, int index)
        {
            if (_coverFlowAlbums.Count == 0)
            {
                image.Source = null;
                return;
            }

            int wrappedIndex = (index + _coverFlowAlbums.Count) % _coverFlowAlbums.Count;
            CoverFlowAlbum album = _coverFlowAlbums[wrappedIndex];

            image.Source = GetCoverFlowArtwork(album) ?? GetCoverFlowFallbackArt();
        }

        private ImageSource GetCoverFlowArtwork(CoverFlowAlbum album)
        {
            if (album == null || string.IsNullOrWhiteSpace(album.CoverArtPath))
                return null;

            string path = album.CoverArtPath;
            ImageSource cachedImage;

            if (_coverFlowArtworkCache.TryGetValue(path, out cachedImage))
            {
                _coverFlowArtworkCacheOrder.Remove(path);
                _coverFlowArtworkCacheOrder.AddLast(path);
                return cachedImage;
            }

            try
            {
                using (var file = TagLib.File.Create(path))
                {
                    if (file.Tag.Pictures == null || file.Tag.Pictures.Length == 0)
                        return null;

                    ImageSource artwork = CreateImageSource(file.Tag.Pictures[0].Data.Data);
                    if (artwork == null)
                        return null;

                    _coverFlowArtworkCache[path] = artwork;
                    _coverFlowArtworkCacheOrder.AddLast(path);

                    // Keep only the five most recently used decoded covers.
                    while (_coverFlowArtworkCacheOrder.Count > CoverFlowArtworkCacheLimit)
                    {
                        string oldestPath = _coverFlowArtworkCacheOrder.First.Value;
                        _coverFlowArtworkCacheOrder.RemoveFirst();
                        _coverFlowArtworkCache.Remove(oldestPath);
                    }

                    return artwork;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Cover Flow artwork load error: " + ex.Message);

                return null;
            }
        }

        private ImageSource _coverFlowFallbackArt;
        private ImageSource GetCoverFlowFallbackArt()
        {
            if (_coverFlowFallbackArt != null) return _coverFlowFallbackArt;

            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "appicon.png");
                var image = new BitmapImage(new Uri(iconPath));
                image.Freeze();
                _coverFlowFallbackArt = image;
            }
            catch
            {
                _coverFlowFallbackArt = null;
            }

            return _coverFlowFallbackArt;
        }

        private void CoverFlowCenter_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                PlayFocusedCoverFlowAlbum();
                e.Handled = true;
            }
        }

        private async void PlayFocusedCoverFlowAlbum()
        {
            if (_coverFlowAlbums.Count == 0)
            {
                await LoadRootPlaylistFallbackAsync();
                return;
            }

            CoverFlowAlbum focused = _coverFlowAlbums[_coverFlowIndex];
            var orderedTracks = focused.Tracks
                                      .OrderBy(t => t.TrackNumber == 0 ? uint.MaxValue : t.TrackNumber)
                                      .ThenBy(t => t.Path)
                                      .Select(t => t.Path)
                                      .ToList();

            if (orderedTracks.Count == 0)
            {
                await LoadRootPlaylistFallbackAsync();
                return;
            }

            if (!ReplacePlaylistWithOrderedFiles(orderedTracks, Root, true, true))
            {
                await LoadRootPlaylistFallbackAsync();
                return;
            }

            BackToPlayer_Click(null, null);
            PlayCurrent();
        }

        private void BtnShowSearch_Click(object sender, RoutedEventArgs e)
        {
            if (isRadioMode)
            {
                OpenRadioSearch_Click(null, null);
            }
            else
            {
                if (isLyricsMode)
                {
                    if (string.IsNullOrEmpty(currentFilePath)) return;

                    _pendingLyricsPath = currentFilePath;
                    _pendingLyricsText = TxtLyrics.Text;
                    BtnShowSearch.Visibility = Visibility.Hidden;
                }
                else
                {
                    if (playlist.Count == 0)
                    {
                        MessageBox.Show(this, "Playlist is empty.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    SearchWindow existingSearch = Application.Current.Windows.OfType<SearchWindow>().FirstOrDefault();

                    if (existingSearch != null)
                    {
                        // 3. If it exists, JUST FOCUS IT. Do not create a new one.
                        existingSearch.Activate();

                        if (existingSearch.WindowState == WindowState.Minimized)
                        {
                            existingSearch.WindowState = WindowState.Normal;
                        }
                    }
                    else
                    {
                        // 4. If it truly doesn't exist, create it.
                        SearchWindow searchWindow = new SearchWindow(this);
                        searchWindow.Owner = this;
                        searchWindow.Show();
                    }
                }
            }
        }

        private void Donate_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://ko-fi.com/exeinn";
            try
            {                
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(owner: this,
                    messageBoxText: $"Something went wrong when trying to open the link. Go to '{url}' if you would like to donate.\n\nTHANK YOU!",
                    caption: "Donate",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Information);
            }
        }

        private DispatcherTimer _sleepTimer;

        private void SleepTimer_Click(object sender, RoutedEventArgs e)
        {
            
            if (!(sender is MenuItem selectedItem)) return;

            // 1. Uncheck all items in the Sleep Timer menu
            foreach (var item in MenuSleepTimer.Items)
            {
                if (item is MenuItem mi) mi.IsChecked = false;
            }

            // 2. Check the one we just clicked
            selectedItem.IsChecked = true;

            // 3. Stop any currently running timer
            if (_sleepTimer != null)
            {
                _sleepTimer.Stop();
                _sleepTimer = null;
            }

            // 4. Get minutes from the Tag
            int minutes = int.Parse(selectedItem.Tag.ToString());

            if (minutes > 0)
            {
                if (_localHandle == 0 && _radioHandle == 0 && playlist.Count == 0)
                {
                    // If the iPod is empty, maybe don't set the timer
                    foreach (var item in MenuSleepTimer.Items)
                    {
                        if (item is MenuItem mi) mi.IsChecked = false;
                    }
                    MenuSleepOff.IsChecked = true;
                    return;
                }

                _sleepTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(minutes)
                };
                _sleepTimer.Tick += (s, args) =>
                {
                    _sleepTimer.Stop(); // Stop the main timer so it doesn't fire again

                    // START THE FADE OUT
                    FadeOutAndClose(3); // 3-second fade out
                };
                _sleepTimer.Start();
            }
            else
            {
                _sleepTimer?.Stop();     // Stops the internal timer engine
                _sleepTimer = null;     // Removes the reference for Garbage Collection
            }
        }

        private void FadeOutAndClose(double seconds)
        {
            // How often to update the volume (every 50ms for smoothness)
            double interval = 0.05;
            double totalSteps = seconds / interval;
            double volumeStep = _playerVolume / totalSteps;

            DispatcherTimer fadeTimer = new DispatcherTimer();
            fadeTimer.Interval = TimeSpan.FromSeconds(interval);

            fadeTimer.Tick += (s, e) =>
            {
                if (_playerVolume > 0)
                {
                    // Gradually lower volume
                    double newVolume = _playerVolume - volumeStep;
                    _playerVolume = Math.Max(0, newVolume);
                    ApplyBassVolume();
                }
                else
                {
                    // Volume is finally 0, stop and close
                    fadeTimer.Stop();
                    StopLocalPlayback(false);
                    StopRadio();
                    FreeBassDevices();
                    Application.Current.Shutdown();
                }
            };

            fadeTimer.Start();
        }

        private void BtnTogglePlaylists_Click(object sender, RoutedEventArgs e)
        {
            if (isLyricsMode || isCoverFlowMode)
            {
                BackToPlayer_Click(sender, e);
            }
            else
            {
                if (PlaylistsView.Visibility == Visibility.Collapsed)
                    ShowPlaylists();
                else
                    BackToPlayer_Click(null, null);
            }
        }

        private void ShowPlaylists()
        {
            isCoverFlowMode = false;

            if (IpodScreen.Visibility == Visibility.Collapsed)
                ToggleDetailedView_Click(null, null);

            // Ensure all other specialty views are hidden
            LyricsView.Visibility = Visibility.Collapsed;
            AboutView.Visibility = Visibility.Collapsed;
            CoverFlowView.Visibility = Visibility.Collapsed;

            TxtTitle.Visibility = TxtArtist.Visibility = TxtAlbum.Visibility =
            TimeNow.Visibility = TxtCount.Visibility = TimeEnd.Visibility =
            ProgressSlider.Visibility = Visibility.Collapsed;

            ImgPlaylistsBackground.Source = ImgAlbumArt.Source;
            PlaylistsView.Visibility = Visibility.Visible;
            RefreshPlaylistsList();
        }

        private void RefreshPlaylistsList()
        {
            if (!Directory.Exists(PlaylistsPath)) return;

            var playlistsData = new List<PlaylistInfo>();
            var files = Directory.GetFiles(PlaylistsPath, "*.json")
                                 .Where(f => !f.EndsWith("settings.json"));

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var obj = JObject.Parse(json);
                    int count = 0;

                    if (obj["Tracks"] != null)
                    {
                        var tracks = obj["Tracks"].ToObject<List<string>>() ?? new List<string>();
                        count = GetExistingAudioFiles(tracks).Count;
                    }
                    else if (obj["SourcePath"] != null)
                    {
                        // Count files in the directory
                        string sourcePath = obj["SourcePath"].ToString();
                        count = GetAudioFilesFromFolder(sourcePath, SearchOption.AllDirectories).Count;
                    }

                    string playlistName = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (playlistName.Equals(System.IO.Path.GetFileName(Root), StringComparison.OrdinalIgnoreCase))
                        playlistName = "All My Songs";

                    playlistsData.Add(new PlaylistInfo
                    {
                        Name = playlistName,
                        Path = file,
                        SongCount = count,
                        DateCreated = GetDateFromJson(file)
                    });
                }
                catch { /* Skip corrupt JSON */ }
            }

            LstPlaylists.ItemsSource = playlistsData.OrderByDescending(p => p.DateCreated).ToList();
        }


        private string GetSourcePathFromJson(string jsonPath)
        {
            try
            {
                const string marker = "\"SourcePath\":";
                string[] lines = File.ReadAllLines(jsonPath);
                foreach (string line in lines)
                {
                    int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
                    if (markerIndex < 0)
                        continue;

                    string value = line.Substring(markerIndex + marker.Length).Trim();
                    return value.TrimEnd(',').Trim('"').Replace("\\\\", "\\");
                }
            }
            catch { }
            return "";
        }

        private void LstPlaylists_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstPlaylists.SelectedItem is PlaylistInfo selected)
            {
                LoadPlaylistByPath(selected.Path);
                BackToPlayer_Click(null, null);
            }
        }

        private void PlaylistMenuPlay_Click(object sender, RoutedEventArgs e)
        {
            if (LstPlaylists.SelectedItem is PlaylistInfo selected)
            {
                LoadPlaylistByPath(selected.Path);
                BackToPlayer_Click(null, null);
            }
        }

        private void PlaylistMenuDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LstPlaylists.SelectedItem is PlaylistInfo selected)
            {
                try
                {
                    if (File.Exists(selected.Path))
                    {
                        File.Delete(selected.Path);
                        RefreshPlaylistsList();
                    }
                }
                catch { }
            }
        }

        private void ShowPlaylistLoading()
        {
            //ClearAlbumArt();
            TxtTitle.Text = "♪ Preparing your music...";
            TxtArtist.Text = "";
            TxtAlbum.Text = "";
        }

        private async void LoadPlaylistByPath(string jsonPath)
        {
            ShowPlaylistLoading();

            try
            {
                string json = File.ReadAllText(jsonPath);
                var obj = JObject.Parse(json);

                // 1. Collect tracks into a temporary list first to validate content
                List<string> tracksToLoad = new List<string>();

                if (obj["Tracks"] != null)
                {
                    // MANUAL PLAYLIST: Load specific files
                    var tracks = obj["Tracks"].ToObject<List<string>>() ?? new List<string>();
                    tracksToLoad = GetExistingAudioFiles(tracks);
                }
                else if (obj["SourcePath"] != null)
                {
                    // FOLDER PLAYLIST: Original logic
                    string sourcePath = obj["SourcePath"].ToString();
                    tracksToLoad = await GetAudioFilesFromFolderAsync(sourcePath, SearchOption.AllDirectories);
                }

                if (tracksToLoad.Count > 0)
                {
                    string sourceFolder = obj["SourcePath"]?.ToString();
                    if (string.IsNullOrWhiteSpace(sourceFolder))
                        sourceFolder = Root;

                    await ReplacePlaylistWithFiles(tracksToLoad, sourceFolder);
                }
                else
                {
                    await LoadRootPlaylistFallbackAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load error: {ex.Message}");
                await LoadRootPlaylistFallbackAsync();
            }
        }


        public void JumpToSong(string path)
        {
            // 1. Find the index of the file in the master playlist
            int playlistIndex = playlist.IndexOf(path);

            if (playlistIndex != -1)
            {
                // 2. Find where that specific index is located in the current shuffle sequence
                int shufflePos = shuffleOrder.IndexOf(playlistIndex);

                if (shufflePos != -1)
                {
                    // 3. Move the pointer and play
                    currentIndex = shufflePos;
                    PlayCurrent();
                }
            }
        }

        private void AddRadioStationByUrl_Click(object sender, RoutedEventArgs e)
        {
            string stationName;
            string stationUrl;
            AddRadioStationDialogAction action = ShowAddRadioStationDialog(out stationName, out stationUrl);
            if (action == AddRadioStationDialogAction.Cancel)
                return;

            var favorites = LoadRadioFavorites();
            if (!favorites.Any(s => IsRadioStationUrlMatch(s, stationUrl)))
            {
                favorites.Insert(0, new RadioStation
                {
                    Name = stationName,
                    Url = stationUrl,
                    IsUnavailable = false,
                    LastFailedUtc = null
                });
                SaveRadioFavorites(favorites);
            }

            if (action == AddRadioStationDialogAction.SaveAndPlayNow)
                PlayRadioStream(stationUrl, stationName);
        }

        private AddRadioStationDialogAction ShowAddRadioStationDialog(out string stationName, out string stationUrl)
        {
            stationName = string.Empty;
            stationUrl = string.Empty;

            var window = new AddRadioStationWindow
            {
                Owner = this,
                Topmost = Topmost
            };

            bool? result = window.ShowDialog();
            if (result == true)
            {
                stationName = window.StationName;
                stationUrl = window.StationUrl;
                return window.ActionChoice;
            }

            return AddRadioStationDialogAction.Cancel;
        }

        private void CopyRadioUrl_Click(object sender, RoutedEventArgs e)
        {
            if (!isRadioMode || string.IsNullOrWhiteSpace(currentFilePath))
            {
                ShowRadioErrorMessage("There is no radio stream URL to copy right now.");
                return;
            }

            Clipboard.SetText(currentFilePath);
        }

        private void CopyRadioNowPlaying_Click(object sender, RoutedEventArgs e)
        {
            if (!isRadioMode)
            {
                ShowRadioErrorMessage("There is no radio station playing right now.");
                return;
            }

            string nowPlaying = string.IsNullOrWhiteSpace(TxtArtist.Text)
                ? TxtTitle.Text
                : $"{TxtTitle.Text} - {TxtArtist.Text}";

            Clipboard.SetText(nowPlaying);
        }

        private void ShowRadioErrorMessage(string message)
        {
            MessageBox.Show(
                owner: this,
                messageBoxText: message,
                caption: "Internet Radio",
                button: MessageBoxButton.OK,
                icon: MessageBoxImage.Information);
        }

        private void HandleRadioStationUnavailable(string stationName, string url)
        {
            RadioUnavailableAction action = ShowRadioUnavailableDialog(stationName);

            if (action == RadioUnavailableAction.Retry)
            {
                PlayRadioStream(url, stationName, _currentRadioCandidateUrls, _currentStationFaviconUrl);
            }
            else if (action == RadioUnavailableAction.SearchAgain)
            {
                OpenRadioSearch_Click(null, null);
                radioWindow?.SearchFor(stationName);
            }
            else if (action == RadioUnavailableAction.RemoveFromFavorites)
            {
                RemoveRadioFavorite(url);
            }
        }

        private RadioUnavailableAction ShowRadioUnavailableDialog(string stationName)
        {
            RadioUnavailableAction action = RadioUnavailableAction.Dismiss;

            var window = new Window
            {
                Title = "Station Unavailable",
                Owner = this,
                Width = 420,
                Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(new TextBlock
            {
                Text = $"{stationName} is not answering right now.",
                FontWeight = FontWeights.SemiBold,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = "The stream may be offline, moved, or blocked by the current radio settings.",
                Margin = new Thickness(0, 8, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(CreateRadioUnavailableButton("Retry", () => action = RadioUnavailableAction.Retry, window));
            buttons.Children.Add(CreateRadioUnavailableButton("Search Again", () => action = RadioUnavailableAction.SearchAgain, window));
            buttons.Children.Add(CreateRadioUnavailableButton("Remove Favorite", () => action = RadioUnavailableAction.RemoveFromFavorites, window));
            buttons.Children.Add(CreateRadioUnavailableButton("Close", () => action = RadioUnavailableAction.Dismiss, window));
            panel.Children.Add(buttons);

            window.Content = panel;
            window.ShowDialog();
            return action;
        }

        private Button CreateRadioUnavailableButton(string text, Action setAction, Window window)
        {
            var button = new Button { Content = text, MinWidth = 72, Height = 28, Margin = new Thickness(6, 0, 0, 0) };
            button.Click += (s, e) =>
            {
                setAction();
                window.DialogResult = true;
            };
            return button;
        }

        private void RemoveRadioFavorite(string url)
        {
            var favorites = LoadRadioFavorites();
            favorites.RemoveAll(station => IsRadioStationUrlMatch(station, url));
            SaveRadioFavorites(favorites);
            UpdateFavoriteMenuStatus();
        }

        private void OpenRadioSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInternetAvailable())
            {
                MessageBox.Show(
                   owner: this,
                   messageBoxText: "No internet connection.",
                   caption: "Error",
                   button: MessageBoxButton.OK,
                   icon: MessageBoxImage.Error);
                return;
            }

            if (radioWindow == null)
            {
                radioWindow = new RadioWindow
                {
                    Owner = this
                };

                // Reset the reference when the window is closed
                radioWindow.Closed += (s, args) => radioWindow = null;

                radioWindow.Show();
            }
            else
            {
                // Bring the existing window to the front
                radioWindow.Activate();
            }
        }

        public async void PlayRadioStream(string url, string stationName)
        {
            try
            {
                await PlayRadioStreamAsync(url, stationName, null, null, true, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayRadioStream failed: {ex.Message}");
            }
        }

        public async void PlayRadioStream(string url, string stationName, IEnumerable<string> alternateUrls)
        {
            try
            {
                await PlayRadioStreamAsync(url, stationName, alternateUrls, null, true, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayRadioStream failed: {ex.Message}");
            }
        }

        public async void PlayRadioStream(string url, string stationName, IEnumerable<string> alternateUrls, string faviconUrl)
        {
            try
            {
                await PlayRadioStreamAsync(url, stationName, alternateUrls, faviconUrl, true, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayRadioStream failed: {ex.Message}");
            }
        }

        private async Task PlayRadioStreamAsync(string url, string stationName, IEnumerable<string> alternateUrls, string faviconUrl, bool resetReconnect, bool showUnavailableDialog)
        {
            await CommitPendingLyrics();

            StopActiveRecording();

            _pendingLyricsPath = _pendingLyricsText = _temCachedLyricsText = null;
            _lastRadioMetadata = string.Empty;
            _radioMetadataFallbackApplied = false;
            _currentRadioStationName = stationName;
            _currentRadioPrimaryUrl = url;
            _currentStationFaviconUrl = faviconUrl ?? string.Empty;

            if (resetReconnect)
            {
                _radioReconnectAttempts = 0;
                StopRadioReconnectTimer();
            }

            // 1. Stop local playback before switching to radio.
            _openedMediaPath = "";
            StopLocalPlayback(false);
            timer.Stop();
            isRadioMode = true;
            isFavoritesMode = false;
            currentFilePath = url;
            _currentRadioCandidateUrls = BuildRadioStreamCandidates(url, alternateUrls);

            UpdateFavoriteMenuStatus();

            // 2. Stop any existing BASS stream
            if (_radioHandle != 0)
            {
                Bass.BASS_ChannelStop(_radioHandle);
                Bass.BASS_StreamFree(_radioHandle);
                _radioHandle = 0;
            }

            // 3. Initial UI Update (Show "Connecting..." immediately)
            TxtTitle.Text = stationName;
            TxtArtist.Text = "Connecting...";
            TxtAlbum.Text = "Internet Radio";
            ImgAlbumArt.Source = null;
            TxtAlbumArt.Visibility = Visibility.Visible;
            TxtAlbumArt.Text = "📻";
            _ = ApplyRadioStationArtworkFallbackAsync(_currentStationFaviconUrl);
            ScheduleRadioMetadataFallback(0);
            ProgressSlider.IsEnabled = false;
            ProgressSlider.Value = 0;
            TimeNow.Text = "";
            TimeEnd.Text = "";
            UpdateUIStatus(true);
            StartMarquee(TxtTitle, TitleTransform, TitleCanvas);
            UpdateLockNowPlayingInfo();

            if (!InitBass())
            {
                StopRadioMetadataFallbackTimer();
                ShowRadioErrorMessage("Podmix could not initialize audio output for internet radio.");
                isPlaying = false;
                UpdateUIStatus(false);
                return;
            }

            ApplyRadioBufferSettings();

            RadioStreamOpenResult openResult = await OpenFirstAvailableRadioStreamAsync(_currentRadioCandidateUrls);
            if (openResult.Handle == 0)
            {
                var fallbackUrls = await GetRadioBrowserFallbackUrlsAsync(stationName);
                foreach (string fallbackUrl in fallbackUrls)
                {
                    if (!_currentRadioCandidateUrls.Any(u => string.Equals(u, fallbackUrl, StringComparison.OrdinalIgnoreCase)))
                        _currentRadioCandidateUrls.Add(fallbackUrl);
                }

                openResult = await OpenFirstAvailableRadioStreamAsync(_currentRadioCandidateUrls);
            }

            if (openResult.Handle != 0)
            {
                _radioReconnectAttempts = 0;
                _radioHandle = openResult.Handle;
                currentFilePath = openResult.Url;

                CheckInitialMetadata(_radioHandle, stationName);
                ScheduleRadioMetadataFallback(_radioHandle);

                // 5. Set up Metadata Sync
                _metaSync = new SYNCPROC(MetaSyncCallback);
                Bass.BASS_ChannelSetSync(_radioHandle, BASSSync.BASS_SYNC_META, 0, _metaSync, IntPtr.Zero);
                ApplyBassVolume();
                Bass.BASS_ChannelPlay(_radioHandle, false);

                // Track History
                AddToRecentStations(stationName, currentFilePath, _currentStationFaviconUrl);
                MarkRadioFavoriteUnavailable(url, false);

                // Start Monitoring
                InitRadioMonitor();

                isPlaying = true;
                UpdateFavoriteMenuStatus();
                UpdateDiscordPresence();
            }
            else
            {
                StopRadioMetadataFallbackTimer();
                MarkRadioFavoriteUnavailable(url, true);
                isPlaying = false;
                UpdateUIStatus(false);
                _discord.ClearPresence();

                if (showUnavailableDialog)
                    HandleRadioStationUnavailable(stationName, url);
                else if (MenuAutoReconnect.IsChecked)
                    ScheduleRadioReconnect();
            }
        }

        private List<string> BuildRadioStreamCandidates(string primaryUrl, IEnumerable<string> alternateUrls)
        {
            var candidates = new List<string>();
            AddRadioCandidate(candidates, primaryUrl);

            if (alternateUrls != null)
            {
                foreach (string alternateUrl in alternateUrls)
                    AddRadioCandidate(candidates, alternateUrl);
            }

            return candidates;
        }

        private void AddRadioCandidate(List<string> candidates, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!(bool)Config.Get("radio_allow_http") && !url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                return;

            if (!candidates.Any(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase)))
                candidates.Add(url);
        }

        private async Task<RadioStreamOpenResult> OpenFirstAvailableRadioStreamAsync(IEnumerable<string> urls)
        {
            foreach (string candidateUrl in urls)
            {
                int handle = await CreateRadioStreamHandleAsync(candidateUrl);
                if (handle != 0)
                    return new RadioStreamOpenResult { Handle = handle, Url = candidateUrl };
            }

            return new RadioStreamOpenResult();
        }

        private async Task<int> CreateRadioStreamHandleAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return 0;

            int radioDevice = Bass.BASS_GetDevice();

            return await Task.Run(() =>
            {
                Bass.BASS_SetDevice(radioDevice);
                return Bass.BASS_StreamCreateURL(url, 0, BASSFlag.BASS_DEFAULT, null, IntPtr.Zero);
            });
        }

        private async Task<List<string>> GetRadioBrowserFallbackUrlsAsync(string stationName)
        {
            var urls = new List<string>();
            if (string.IsNullOrWhiteSpace(stationName) || !IsInternetAvailable())
                return urls;

            try
            {
                if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "PodmixRadio/1.0");

                string requestUrl = $"https://all.api.radio-browser.info/json/stations/byname/{Uri.EscapeDataString(stationName)}";
                string json = await httpClient.GetStringAsync(requestUrl);
                var array = JArray.Parse(json);
                bool allowHttp = (bool)Config.Get("radio_allow_http");

                var orderedStations = array
                    .OrderByDescending(s => string.Equals(s["name"]?.ToString(), stationName, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(s => (int?)s["votes"] ?? 0);

                foreach (JToken station in orderedStations)
                {
                    AddRadioBrowserUrl(urls, station["url_resolved"]?.ToString(), allowHttp);
                    AddRadioBrowserUrl(urls, station["url"]?.ToString(), allowHttp);

                    if (urls.Count >= 8)
                        break;
                }
            }
            catch
            {
                return urls;
            }

            return urls;
        }

        private async Task<string> GetRadioBrowserFaviconUrlAsync(string stationName, string streamUrl)
        {
            if (string.IsNullOrWhiteSpace(stationName) || !IsInternetAvailable())
                return null;

            try
            {
                if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "PodmixRadio/1.0");

                string requestUrl = $"https://all.api.radio-browser.info/json/stations/byname/{Uri.EscapeDataString(stationName)}";
                string json = await httpClient.GetStringAsync(requestUrl);
                var array = JArray.Parse(json);

                JToken matchingStation = null;
                if (!string.IsNullOrWhiteSpace(streamUrl))
                {
                    matchingStation = array.FirstOrDefault(s =>
                        string.Equals(s["url_resolved"]?.ToString(), streamUrl, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s["url"]?.ToString(), streamUrl, StringComparison.OrdinalIgnoreCase));
                }

                string faviconUrl = matchingStation?["favicon"]?.ToString();
                if (!string.IsNullOrWhiteSpace(faviconUrl))
                    return faviconUrl;

                return array
                    .OrderByDescending(s => string.Equals(s["name"]?.ToString(), stationName, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(s => (int?)s["votes"] ?? 0)
                    .Select(s => s["favicon"]?.ToString())
                    .FirstOrDefault(f => !string.IsNullOrWhiteSpace(f));
            }
            catch
            {
                return null;
            }
        }

        private void AddRadioBrowserUrl(List<string> urls, string url, bool allowHttp)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!allowHttp && !url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                return;

            if (!urls.Any(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase)))
                urls.Add(url);
        }

        private async Task<BitmapImage> LoadRadioImageFromUrlAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            try
            {
                byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                return LoadImage(imageBytes);
            }
            catch
            {
                return null;
            }
        }

        private async Task<BitmapImage> LoadBestRadioStationImageAsync(string faviconUrl)
        {
            BitmapImage stationImage = await LoadRadioImageFromUrlAsync(faviconUrl);

            if (stationImage == null)
            {
                string discoveredFaviconUrl = await GetRadioBrowserFaviconUrlAsync(_currentRadioStationName, currentFilePath);
                if (!string.IsNullOrWhiteSpace(discoveredFaviconUrl))
                {
                    _currentStationFaviconUrl = discoveredFaviconUrl;
                    stationImage = await LoadRadioImageFromUrlAsync(discoveredFaviconUrl);
                }
            }

            return stationImage;
        }

        private async Task ApplyRadioStationArtworkFallbackAsync(string faviconUrl)
        {
            BitmapImage stationImage = await LoadBestRadioStationImageAsync(faviconUrl);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!isRadioMode || !string.IsNullOrWhiteSpace(_lastRadioMetadata))
                    return;

                if (stationImage != null)
                {
                    ImgAlbumArt.Source = stationImage;
                    TxtAlbumArt.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ShowRadioTextIcon();
                }
            });
        }

        private void ShowRadioTextIcon()
        {
            ImgAlbumArt.Source = null;
            TxtAlbumArt.Visibility = Visibility.Visible;
            TxtAlbumArt.Text = "📻";
        }

        private void StopRadioReconnectTimer()
        {
            if (_radioReconnectTimer != null)
                _radioReconnectTimer.Stop();
        }

        private void StopRadioMetadataFallbackTimer()
        {
            if (_radioMetadataFallbackTimer != null)
                _radioMetadataFallbackTimer.Stop();

            _radioMetadataFallbackHandle = 0;
        }

        private void ScheduleRadioMetadataFallback(int handle)
        {
            StopRadioMetadataFallbackTimer();
            _radioMetadataFallbackHandle = handle;

            if (_radioMetadataFallbackTimer == null)
            {
                _radioMetadataFallbackTimer = new DispatcherTimer();
                _radioMetadataFallbackTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        int expectedHandle = _radioMetadataFallbackHandle;
                        StopRadioMetadataFallbackTimer();

                        if (!isRadioMode || (expectedHandle != 0 && _radioHandle != expectedHandle))
                            return;

                        if (!string.IsNullOrWhiteSpace(_lastRadioMetadata))
                            return;

                        if (!ShouldApplyRadioMetadataFallback())
                            return;

                        _radioMetadataFallbackApplied = true;
                        TxtArtist.Text = "Live Stream";
                        TipArtist.Text = "Live Stream";
                        UpdateNotifyIcon($"{_currentRadioStationName} - Live Stream");
                        UpdateLockNowPlayingInfo();
                        StartMarquee(TxtArtist, ArtistTransform, ArtistCanvas);

                        await ApplyRadioStationArtworkFallbackAsync(_currentStationFaviconUrl);
                        UpdateDiscordRadioPresence();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Radio metadata fallback failed: {ex.Message}");
                    }
                };
            }

            _radioMetadataFallbackTimer.Interval = TimeSpan.FromSeconds(RadioMetadataFallbackSeconds);
            _radioMetadataFallbackTimer.Start();
        }

        private bool ShouldApplyRadioMetadataFallback()
        {
            string text = TxtArtist.Text ?? string.Empty;

            return text.IndexOf("Station unavailable", StringComparison.OrdinalIgnoreCase) < 0 &&
                   text.IndexOf("Reconnecting", StringComparison.OrdinalIgnoreCase) < 0 &&
                   text.IndexOf("Recording", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private void ScheduleRadioReconnect()
        {
            if (!isRadioMode || string.IsNullOrWhiteSpace(_currentRadioPrimaryUrl))
                return;

            if (_radioReconnectTimer != null && _radioReconnectTimer.IsEnabled)
                return;

            _radioReconnectAttempts++;

            if (_radioReconnectAttempts > MaxRadioReconnectAttempts)
            {
                MarkRadioFavoriteUnavailable(_currentRadioPrimaryUrl, true);
                isPlaying = false;
                UpdateUIStatus(false);
                TxtArtist.Text = "Station unavailable";
                UpdateLockNowPlayingInfo();
                HandleRadioStationUnavailable(_currentRadioStationName, _currentRadioPrimaryUrl);
                return;
            }

            int delaySeconds = Math.Min(30, 3 * _radioReconnectAttempts);
            TxtArtist.Text = $"Reconnecting in {delaySeconds}s...";
            UpdateLockNowPlayingInfo();

            if (_radioReconnectTimer == null)
            {
                _radioReconnectTimer = new DispatcherTimer();
                _radioReconnectTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        StopRadioReconnectTimer();
                        if (!isRadioMode || !MenuAutoReconnect.IsChecked)
                            return;

                        await PlayRadioStreamAsync(_currentRadioPrimaryUrl, _currentRadioStationName, _currentRadioCandidateUrls, _currentStationFaviconUrl, false, false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Radio reconnect failed: {ex.Message}");
                    }
                };
            }

            _radioReconnectTimer.Interval = TimeSpan.FromSeconds(delaySeconds);
            _radioReconnectTimer.Start();
        }

        private string[] GetIcyTags(IntPtr icyPtr)
        {
            List<string> tags = new List<string>();
            if (icyPtr == IntPtr.Zero)
                return tags.ToArray();

            int offset = 0;

            // Safety bounds: a malformed/missing terminator used to spin this loop
            // forever (freezing the UI) and the old code truncated the 64-bit pointer
            // by converting it to an int.
            const int MaxTags = 256;
            const int MaxBytes = 64 * 1024;

            while (tags.Count < MaxTags && offset < MaxBytes)
            {
                // Read each null-terminated string
                string tag = Marshal.PtrToStringAnsi(IntPtr.Add(icyPtr, offset));
                if (string.IsNullOrEmpty(tag))
                    break;

                tags.Add(tag);
                offset += tag.Length + 1; // Move to the next string (including null terminator)
            }

            return tags.ToArray();
        }

        private void CheckInitialMetadata(int handle, string stationName)
        {
            IntPtr icyPtr = Bass.BASS_ChannelGetTags(handle, BASSTag.BASS_TAG_ICY);
            if (icyPtr != IntPtr.Zero)
            {
                string[] tags = GetIcyTags(icyPtr);
                foreach (string tag in tags)
                {
                    if (tag.StartsWith("icy-name:", StringComparison.OrdinalIgnoreCase))
                    {
                        string station = tag.Substring(9).Trim();
                        if (!string.IsNullOrEmpty(station))
                        {
                            TxtArtist.Text = station;
                            UpdateLockNowPlayingInfo();
                            UpdateNotifyIcon(station);
                            return;
                        }
                    }
                }
            }

            // Keep the temporary text visible; the metadata fallback timer will turn it into "Live Stream".
            UpdateLockNowPlayingInfo();
            UpdateNotifyIcon(stationName);
        }

        private void UpdateNotifyIcon(string text)
        {
            if (_notifyIcon != null)
            {
                const string Branding = " | Podmix";
                const string Ellipsis = "...";
                const int MaxTotalLength = 63;

                string fullText = text + Branding;

                if (fullText.Length <= MaxTotalLength)
                {
                    _notifyIcon.Text = fullText;
                }
                else
                {
                    int allowedDetailsLength = MaxTotalLength - Branding.Length - Ellipsis.Length;
                    string truncatedDetails = text.Substring(0, allowedDetailsLength);
                    _notifyIcon.Text = $"{truncatedDetails}{Ellipsis}{Branding}";
                }
            }
        }

        private void InitRadioMonitor()
        {
            if (_radioMonitorTimer != null) return;

            _radioMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _radioMonitorTimer.Tick += (s, e) =>
            {
                if (!isRadioMode || _radioHandle == 0 || !isPlaying) return;

                var status = Bass.BASS_ChannelIsActive(_radioHandle);

                // Feature 2: Buffer Status logic
                if (status == BASSActive.BASS_ACTIVE_STALLED)
                {
                    long len = Bass.BASS_StreamGetFilePosition(_radioHandle, BASSStreamFilePosition.BASS_FILEPOS_END);
                    long buf = Bass.BASS_StreamGetFilePosition(_radioHandle, BASSStreamFilePosition.BASS_FILEPOS_BUFFER);

                    if (len > 0)
                    {
                        double percentage = (buf * 100.0) / len;
                        TxtArtist.Text = $"Buffering... {percentage:F0}%";
                        UpdateLockNowPlayingInfo();
                    }
                }
                else if (status == BASSActive.BASS_ACTIVE_PLAYING && TxtArtist.Text.Contains("Buffering"))
                {
                    // Restore to "Connecting..." or the last known metadata
                    TxtArtist.Text = string.IsNullOrEmpty(_lastRadioMetadata)
                        ? (_radioMetadataFallbackApplied ? "Live Stream" : "Connecting...")
                        : _lastRadioMetadata;
                    UpdateLockNowPlayingInfo();
                }

                // Feature 1: Auto-Reconnect logic
                if (MenuAutoReconnect.IsChecked && status == BASSActive.BASS_ACTIVE_STOPPED)
                {
                    ScheduleRadioReconnect();
                }
            };
            _radioMonitorTimer.Start();
        }

        // Feature 6: Recent Stations History logic
        private void AddToRecentStations(string name, string url, string faviconUrl)
        {
            try
            {
                var recent = LoadRecentStations();
                recent.RemoveAll(r => r.Url == url); // Remove if exists to move to top
                recent.Insert(0, new RadioStation { Name = name, Url = url, FaviconUrl = faviconUrl });

                if (recent.Count > 10) recent = recent.Take(10).ToList();

                string dir = System.IO.Path.GetDirectoryName(RadioRecentPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonConvert.SerializeObject(recent, Formatting.Indented);
                File.WriteAllText(RadioRecentPath, json);
            }
            catch { }
        }

        private List<RadioStation> LoadRecentStations()
        {
            if (!File.Exists(RadioRecentPath)) return new List<RadioStation>();
            try
            {
                string json = File.ReadAllText(RadioRecentPath);
                return JsonConvert.DeserializeObject<List<RadioStation>>(json) ?? new List<RadioStation>();
            }
            catch { return new List<RadioStation>(); }
        }

        private void MenuRadioRecent_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            MenuRadioRecent.Items.Clear();

            MenuItem item = new MenuItem { Header = "Clear History" };
            item.Click += ClearRadioHistory_Click;
            MenuRadioRecent.Items.Add(item);
            MenuRadioRecent.Items.Add(new Separator());

            var recent = LoadRecentStations();

            if (recent.Count == 0)
            {
                MenuRadioRecent.Items.Add(new MenuItem { Header = "Empty", IsEnabled = false, FontStyle = FontStyles.Italic, Foreground = Brushes.Gray });
                return;
            }

            foreach (var station in recent)
            {
                MenuItem item2 = new MenuItem { Header = station.Name, Tag = station.Url };
                item2.Click += (s, args) => PlayRadioStream(station.Url, station.Name, station.AlternateUrls, station.FaviconUrl);
                MenuRadioRecent.Items.Add(item2);
            }
        }

        private async void MetaSyncCallback(int handle, int channel, int data, IntPtr user)
        {
            // IMPORTANT: BASS invokes this delegate directly on a native audio thread.
            // There is no WPF Dispatcher and no SynchronizationContext here, so an
            // exception (especially inside the async continuation) would terminate the
            // whole process and would NOT be caught by DispatcherUnhandledException.
            // Every operation below must therefore be guarded, and all UI access must
            // be marshalled explicitly.
            try
            {
                if (channel == 0 || channel != _radioHandle)
                    return;

                IntPtr tagsPtr = Bass.BASS_ChannelGetTags(channel, BASSTag.BASS_TAG_META);
                if (tagsPtr == IntPtr.Zero)
                    return;

                string meta = Marshal.PtrToStringAnsi(tagsPtr);
                if (string.IsNullOrEmpty(meta))
                    return;

                var match = Regex.Match(meta, "StreamTitle='(.*?)';");
                if (!match.Success)
                    return;

                string nowPlaying = match.Groups[1].Value;

                // CHANGE GUARD: Return early if the metadata hasn't actually changed
                if (nowPlaying == _lastRadioMetadata) return;
                _lastRadioMetadata = nowPlaying;

                // 1. Split the "Artist - Title" string
                string artist = "Unknown Artist";
                string title = nowPlaying;
                bool hasSongArtistMetadata = false;

                if (nowPlaying.Contains(" - "))
                {
                    var parts = nowPlaying.Split(new[] { " - " }, 2, StringSplitOptions.None);
                    artist = parts[0].Trim();
                    title = parts[1].Trim();
                    hasSongArtistMetadata = !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title);
                }

                // 2. Attempt artwork in order: song art -> station image -> radio icon.
                byte[] artBytes = hasSongArtistMetadata ? await GetOnlineCoverAsync(artist, title) : null;
                BitmapImage stationImage = artBytes == null
                    ? await LoadBestRadioStationImageAsync(_currentStationFaviconUrl)
                    : null;

                // The stream can change/dispose while the artwork lookups are in flight.
                if (channel != _radioHandle)
                    return;

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return;

                // 3. Update the UI on the main thread
                _ = dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        StopRadioMetadataFallbackTimer();

                        TxtArtist.Text = nowPlaying;
                        TipArtist.Text = nowPlaying;

                        // NotifyIcon has UI thread affinity, so update it here rather
                        // than on the native BASS callback thread.
                        if (hasSongArtistMetadata)
                            UpdateNotifyIcon($"{title} - {artist}");
                        else
                            UpdateNotifyIcon(nowPlaying);

                        // Feature 4: Technical Info (Codec and Bitrate)
                        BASS_CHANNELINFO info = Bass.BASS_ChannelGetInfo(channel);
                        float bitrate = 0;
                        // Use (BASSAttribute)12 as a fallback for the bitrate attribute
                        Bass.BASS_ChannelGetAttribute(channel, (BASSAttribute)12, ref bitrate);

                        // Displays codec type and live bitrate
                        TxtAlbum.Text = $"Internet Radio ● {bitrate:F0} kbps";

                        // Feature 3: Favicon Fallback Hierarchy (Song Art -> Station Favicon -> Radio Icon)
                        bool imageLoaded = false;

                        if (artBytes != null)
                        {
                            ImgAlbumArt.Source = LoadImage(artBytes);
                            TxtAlbumArt.Visibility = Visibility.Collapsed;
                            imageLoaded = true;
                        }
                        else if (stationImage != null)
                        {
                            ImgAlbumArt.Source = stationImage;
                            TxtAlbumArt.Visibility = Visibility.Collapsed;
                            imageLoaded = true;
                        }
                        else
                        {
                            ShowRadioTextIcon();
                        }

                        // Dynamic Theming (Extract color from whatever image is currently displayed)
                        if (imageLoaded && (bool)Config.Get("extract_album_cover_color"))
                        {
                            if (ImgAlbumArt.Source is BitmapSource bitmapSource)
                            {
                                Color bodyColor = ColorFromBitmapSource(bitmapSource);
                                colorHex = $"#{bodyColor.R:X2}{bodyColor.G:X2}{bodyColor.B:X2}".ToLower();

                                Color newColor = (Color)ColorConverter.ConvertFromString(colorHex);
                                SolidColorBrush newBrush = new SolidColorBrush(newColor);

                                // Always use the opaque brush for the shared theme
                                // resources; otherwise the outer bezel can go transparent.
                                Resources["HoverColorBrush"] = newBrush;
                                Resources["SliderTrackBrush"] = newBrush;

                                IpodBody.Background = newBrush;
                                CenterPlayBtn.Background = newBrush;
                                CenterPlayBtn.Foreground = GetContrastBrush(newBrush.Color);
                                IpodScreen.Background = Brushes.Black;
                                PlaybackWheel.Fill = CalculateScreenBackground(colorHex);
                                Resources["IpodScreenBrush"] = CalculateScreenBackground(colorHex);

                                SaveViewState();
                            }
                        }

                        ShowNotification(ImgAlbumArt.Source, title, artist);
                        StartMarquee(TxtArtist, ArtistTransform, ArtistCanvas);
                        UpdateLockNowPlayingInfo();
                        UpdateDiscordRadioPresence(title, artist);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Radio metadata UI update failed: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Radio metadata callback failed: {ex.Message}");
            }
        }

        private void RecordMP3_Click(object sender, RoutedEventArgs e)
        {
            if (_radioHandle == 0) return;

            string recordingFolder = EnsureRecordingFolder();
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "MP3 Files (*.mp3)|*.mp3",
                InitialDirectory = recordingFolder,
                FileName = $"{SanitizeFileName(TxtTitle.Text)}.mp3"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // This uses the generic encoder bridge. 
                // It requires bassenc.dll in your folder and lame.exe (or bassenc_mp3.dll)
                // The command line string tells BASS how to pipe the data to the encoder.
                string outputFile = saveFileDialog.FileName;

                // Command for LAME: "-" tells it to accept input from STDIN (the BASS stream)
                string command = $"lame.exe -V2 - \"{outputFile}\"";

                _encoderHandle = BassEnc.BASS_Encode_Start(_radioHandle, command, BASSEncode.BASS_ENCODE_DEFAULT, null, IntPtr.Zero);

                if (_encoderHandle != 0)
                {
                    SetRecordingUI(true, "MP3");
                }
                else
                {
                    // If BASS_Encode_Start fails, it's usually because lame.exe is missing 
                    // or the output path has illegal characters.
                    MessageBox.Show("Failed to start MP3 encoder. Ensure lame.exe is in the app folder. Error: " + Bass.BASS_ErrorGetCode());
                }
            }
        }

        private void AutoReconnect_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                Config.Save("auto_reconnect", mi.IsChecked);

                // If the user turns it ON while the stream is currently stopped/broken,
                // you might want to trigger an immediate attempt.
                if (mi.IsChecked && isRadioMode && isPlaying)
                {
                    var status = Bass.BASS_ChannelIsActive(_radioHandle);
                    if (status == BASSActive.BASS_ACTIVE_STOPPED)
                    {
                        ScheduleRadioReconnect();
                    }
                }
            }
        }

        private void RecordWAV_Click(object sender, RoutedEventArgs e)
        {
            if (_radioHandle == 0) return;

            string recordingFolder = EnsureRecordingFolder();
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "WAV Files (*.wav)|*.wav",
                InitialDirectory = recordingFolder,
                FileName = $"{SanitizeFileName(TxtTitle.Text)}.wav"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // BASS_ENCODE_PCM automatically adds the WAV header and writes raw PCM data
                _encoderHandle = BassEnc.BASS_Encode_Start(_radioHandle, saveFileDialog.FileName, BASSEncode.BASS_ENCODE_PCM, null, IntPtr.Zero);

                if (_encoderHandle != 0)
                {
                    SetRecordingUI(true, "WAV");
                }
                else
                {
                    MessageBox.Show("Failed to start WAV encoder. Error: " + Bass.BASS_ErrorGetCode());
                }
            }
        }

        private void ChangeRecordingFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Choose where Podmix saves radio recordings.",
                SelectedPath = EnsureRecordingFolder()
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Config.Save("radio_recording_folder", dialog.SelectedPath);
                EnsureRecordingFolder();
            }
        }

        private void OpenRecordingFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = EnsureRecordingFolder();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    owner: this,
                    messageBoxText: $"Podmix could not open the recordings folder.\n\n{ex.Message}",
                    caption: "Recordings",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Information);
            }
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            StopActiveRecording();
        }

        private void StopActiveRecording()
        {
            if (_encoderHandle != 0)
            {
                BassEnc.BASS_Encode_Stop(_encoderHandle);
                _encoderHandle = 0;
                SetRecordingUI(false, string.Empty);
            }
        }

        private void SetRecordingUI(bool isRecording, string format)
        {
            MenuStopRecording.IsEnabled = isRecording;
            MenuRecordMP3.IsEnabled = MenuRecordWAV.IsEnabled = !isRecording;

            if (isRecording)
            {
                // Visual indicator in the metadata area
                TxtCount.Text = $"[•] Recording {format}...";
            }
            else
            {
                // Trigger a metadata refresh to restore the technical info/album text
                if (isRadioMode)
                {
                    // This will force the MetaSyncCallback logic to refresh the text
                    _lastRadioMetadata = string.Empty;
                }

                TxtCount.Text = "LIVE";
            }
        }

        private List<RadioStation> LoadRadioFavorites()
        {
            if (!File.Exists(RadioFavoritesPath)) return new List<RadioStation>();
            try
            {
                string json = File.ReadAllText(RadioFavoritesPath);
                return JsonConvert.DeserializeObject<List<RadioStation>>(json) ?? new List<RadioStation>();
            }
            catch { return new List<RadioStation>(); }
        }

        private void SaveRadioFavorites(List<RadioStation> stations)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(RadioFavoritesPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonConvert.SerializeObject(stations, Formatting.Indented);
                File.WriteAllText(RadioFavoritesPath, json);
            }
            catch { }
        }

        private bool IsRadioStationUrlMatch(RadioStation station, string url)
        {
            if (station == null || string.IsNullOrWhiteSpace(url))
                return false;

            if (string.Equals(station.Url, url, StringComparison.OrdinalIgnoreCase))
                return true;

            return station.AlternateUrls != null
                && station.AlternateUrls.Any(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase));
        }

        private void MarkRadioFavoriteUnavailable(string url, bool isUnavailable)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            var stations = LoadRadioFavorites();
            bool changed = false;

            foreach (var station in stations.Where(s => IsRadioStationUrlMatch(s, url)))
            {
                station.IsUnavailable = isUnavailable;
                station.LastFailedUtc = isUnavailable ? DateTime.UtcNow : (DateTime?)null;
                changed = true;
            }

            if (changed)
                SaveRadioFavorites(stations);
        }

        private void MenuRadioFavorites_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            // Clear dynamic items (keep Check Availability, helper text, and separator)
            while (MenuRadioFavorites.Items.Count > 3)
            {
                MenuRadioFavorites.Items.RemoveAt(3);
            }

            var stations = LoadRadioFavorites();
            if (stations.Count == 0)
            {
                MenuRadioFavorites.Items.Add(new MenuItem { Header = "No favorites yet", IsEnabled = false, Foreground = Brushes.Gray });
                return;
            }

            foreach (var station in stations)
            {
                string header = station.IsUnavailable ? $"{station.Name} (Unavailable)" : station.Name;
                MenuItem item = new MenuItem
                {
                    Header = header,
                    Tag = station.Url
                };

                if (station.IsUnavailable)
                {
                    item.Foreground = Brushes.Gray;
                    item.ToolTip = "This station failed the last availability check or playback attempt.";
                }

                // Standard left-click logic
                item.Click += (s, args) =>
                {
                    PlayRadioStream(station.Url, station.Name, station.AlternateUrls, station.FaviconUrl);
                };

                // Use PreviewMouseRightButtonDown to "kill" the event before it becomes a Click
                item.PreviewMouseRightButtonDown += (s, args) =>
                {
                    var currentFavs = LoadRadioFavorites();
                    currentFavs.RemoveAll(r => r.Url == station.Url);
                    SaveRadioFavorites(currentFavs);

                    // Remove from UI immediately
                    MenuRadioFavorites.Items.Remove(item);

                    if (isRadioMode && IsRadioStationUrlMatch(station, currentFilePath))
                        UpdateFavoriteMenuStatus();

                    // This prevents the "Click" event from ever firing
                    args.Handled = true;
                };

                MenuRadioFavorites.Items.Add(item);
            }
        }

        private async void CheckRadioFavoritesAvailability_Click(object sender, RoutedEventArgs e)
        {
            var stations = LoadRadioFavorites();
            if (stations.Count == 0)
            {
                MessageBox.Show(
                    owner: this,
                    messageBoxText: "You do not have any favorite radio stations yet.",
                    caption: "Radio Favorites",
                    button: MessageBoxButton.OK,
                    icon: MessageBoxImage.Information);
                return;
            }

            if (!InitBass())
            {
                ShowRadioErrorMessage("Podmix could not initialize audio output to check radio stations.");
                return;
            }

            ApplyRadioBufferSettings();

            int unavailableCount = 0;
            foreach (var station in stations)
            {
                bool isAvailable = await IsRadioStationAvailableAsync(station);
                station.IsUnavailable = !isAvailable;
                station.LastFailedUtc = isAvailable ? (DateTime?)null : DateTime.UtcNow;
                if (!isAvailable)
                    unavailableCount++;
            }

            SaveRadioFavorites(stations);
            MenuRadioFavorites_SubmenuOpened(MenuRadioFavorites, new RoutedEventArgs());

            MessageBox.Show(
                owner: this,
                messageBoxText: unavailableCount == 0
                    ? "All favorite stations answered."
                    : $"{unavailableCount} favorite station(s) look unavailable right now. Podmix marked them in the Favorites menu.",
                caption: "Radio Favorites",
                button: MessageBoxButton.OK,
                icon: MessageBoxImage.Information);
        }

        private async Task<bool> IsRadioStationAvailableAsync(RadioStation station)
        {
            if (station == null)
                return false;

            var candidates = BuildRadioStreamCandidates(station.Url, station.AlternateUrls);
            if (candidates.Count == 0)
                return false;

            RadioStreamOpenResult result = await OpenFirstAvailableRadioStreamAsync(candidates);
            if (result.Handle == 0)
                return false;

            Bass.BASS_StreamFree(result.Handle);
            return true;
        }

        private void LibraryItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            SearchMenuItem.IsEnabled
                = NewPlaylistMenuItem.IsEnabled
                = MenuAddToPlaylist.IsEnabled
                = SaveCurrentPlaylistMenuItem.IsEnabled
                = ClearQueueMenuItem.IsEnabled
                = !isRadioMode;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            BtnNext.IsEnabled
                = BtnPrev.IsEnabled
                = NextMenuItem.IsEnabled
                = PrevMenuItem.IsEnabled
                = !isRadioMode;

            MenuCopyRadioUrl.IsEnabled = isRadioMode && !string.IsNullOrWhiteSpace(currentFilePath);
            MenuCopyRadioNowPlaying.IsEnabled = isRadioMode;

            if (!isRadioMode)
            {
                // 1. CLEANUP: Remove the old "Up Next" item and Separator if they exist
                // We check if the first item is a MenuItem that starts with "Up Next"
                if (ContextMenu.Items.Count > 0 &&
                    ContextMenu.Items[0] is MenuItem oldItem &&
                    oldItem.Header.ToString().StartsWith("Up Next"))
                {
                    ContextMenu.Items.RemoveAt(0); // Remove the MenuItem
                    if (ContextMenu.Items.Count > 0 && ContextMenu.Items[0] is Separator)
                    {
                        ContextMenu.Items.RemoveAt(0); // Remove the Separator
                    }
                }

                // 2. Add the new item only if the option is enabled and we aren't in radio mode
                if (MenuShowNextUp.IsChecked && !isRadioMode && playlist.Count > 0 && shuffleOrder.Count > 0)
                {
                    int nextShufflePos = currentIndex + 1;

                    if (nextShufflePos >= shuffleOrder.Count)
                    {
                        nextShufflePos = 0;
                    }

                    int actualPlaylistIndex = shuffleOrder[nextShufflePos];
                    if (actualPlaylistIndex < 0 || actualPlaylistIndex >= playlist.Count)
                        return;

                    var nextSong = playlist[actualPlaylistIndex];

                    if (File.Exists(nextSong))
                    {
                        try
                        {
                            using (var file = TagLib.File.Create(nextSong))
                            {
                                string title = !string.IsNullOrEmpty(file.Tag.Title)
                                    ? file.Tag.Title
                                    : System.IO.Path.GetFileNameWithoutExtension(nextSong);

                                string artist = file.Tag.FirstAlbumArtist ?? file.Tag.FirstPerformer ?? "Unknown Artist";

                                MenuItem menuUpNext = new MenuItem
                                {
                                    Header = $"Up Next: {title} - {artist}",
                                    FontStyle = FontStyles.Italic,
                                    Foreground = Brushes.Gray,
                                    IsEnabled = false
                                };

                                // Insert at the very top
                                ContextMenu.Items.Insert(0, menuUpNext);
                                ContextMenu.Items.Insert(1, new Separator());
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Could not read 'Up Next' metadata: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                if (ContextMenu.Items.Count > 0 &&
                    ContextMenu.Items[0] is MenuItem oldItem &&
                    oldItem.Header.ToString().StartsWith("Up Next"))
                {
                    ContextMenu.Items.RemoveAt(0); // Remove the MenuItem
                    if (ContextMenu.Items.Count > 0 && ContextMenu.Items[0] is Separator)
                    {
                        ContextMenu.Items.RemoveAt(0); // Remove the Separator
                    }
                }
            }
        }

        private void ClearRadioHistory_Click(object sender, RoutedEventArgs e)
        {
            var fi = new FileInfo(RadioRecentPath);
            if (fi.Exists)
            {
                try 
                { 
                    fi.Delete();
                } catch { }
            }
        }

        private void RecordingMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            MenuRecordMP3.IsEnabled
                = MenuRecordWAV.IsEnabled
                = isRadioMode;
        }

        private void MenuDiscordPresence_Checked(object sender, RoutedEventArgs e)
        {
            Config.Save("discord_presence", true);
            _discord.IsEnabled = true;
            UpdateDiscordPresence();
        }

        private void MenuDiscordPresence_Unchecked(object sender, RoutedEventArgs e)
        {
            Config.Save("discord_presence", false);
            _discord.IsEnabled = false;
            _discord.ClearPresence();
        }

        private void UpdateDiscordPresence()
        {
            if (!isPlaying)
            {
                _discord.ClearPresence();
                return;
            }

            if (isRadioMode)
            {
                UpdateDiscordRadioPresence();
            }
            else
            {
                string title = TxtTitle.Text;
                string artist = TxtArtist.Text;
                string album = TxtAlbum.Text;

                if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(currentFilePath))
                {
                    title = System.IO.Path.GetFileNameWithoutExtension(currentFilePath);
                }

                if (!string.IsNullOrWhiteSpace(title))
                {
                    _discord.UpdateLocalPresence(title, artist, album);
                }
                else
                {
                    _discord.ClearPresence();
                }
            }
        }

        private void UpdateDiscordRadioPresence(string songTitle = null, string artist = null)
        {
            if (!isPlaying || !isRadioMode)
            {
                _discord.ClearPresence();
                return;
            }

            string station = _currentRadioStationName;
            if (string.IsNullOrWhiteSpace(station))
            {
                station = TxtTitle.Text;
            }

            if (string.IsNullOrWhiteSpace(songTitle))
            {
                if (!string.IsNullOrWhiteSpace(_lastRadioMetadata))
                {
                    if (_lastRadioMetadata.Contains(" - "))
                    {
                        var parts = _lastRadioMetadata.Split(new[] { " - " }, 2, StringSplitOptions.None);
                        artist = parts[0].Trim();
                        songTitle = parts[1].Trim();
                    }
                    else
                    {
                        songTitle = _lastRadioMetadata.Trim();
                    }
                }
                else if (TxtArtist.Text != "Connecting..." &&
                         !TxtArtist.Text.Contains("Buffering") &&
                         !TxtArtist.Text.Contains("Reconnecting"))
                {
                    if (TxtArtist.Text.Contains(" - "))
                    {
                        var parts = TxtArtist.Text.Split(new[] { " - " }, 2, StringSplitOptions.None);
                        artist = parts[0].Trim();
                        songTitle = parts[1].Trim();
                    }
                    else if (TxtArtist.Text != "Live Stream" && !string.IsNullOrWhiteSpace(TxtArtist.Text))
                    {
                        songTitle = TxtArtist.Text.Trim();
                    }
                }
            }

            _discord.UpdateRadioPresence(station, songTitle, artist, _currentStationFaviconUrl);
        }

        private void MenuShowNextUp_Checked(object sender, RoutedEventArgs e)
        {
            Config.Save("show_next_up", true);
        }

        private void MenuShowNextUp_Unchecked(object sender, RoutedEventArgs e)
        {
            Config.Save("show_next_up", false);
            if (ContextMenu.Items.Count > 0 &&
                ContextMenu.Items[0] is MenuItem oldItem &&
                oldItem.Header.ToString().StartsWith("Up Next"))
            {
                ContextMenu.Items.RemoveAt(0); // Remove the MenuItem
                if (ContextMenu.Items.Count > 0 && ContextMenu.Items[0] is Separator)
                {
                    ContextMenu.Items.RemoveAt(0); // Remove the Separator
                }
            }
        }

        private void TxtCount_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TxtCount.Text == "LIVE") return;
            string text = TxtCount.Text;
            if (text.Contains("K") || text.Contains("B") || text.Contains("B"))
                TxtCount.Text = $"{(currentIndex + 1)} / {playlist.Count}";
            else
                TxtCount.Text = $"{(currentIndex + 1)} / {playlist.Count.ToMetricFormat()}";
        }

        private DateTime GetDateFromJson(string jsonPath)
        {
            try
            {
                const string marker = "\"DateCreated\":";
                string[] lines = File.ReadAllLines(jsonPath);
                foreach (string line in lines)
                {
                    int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
                    if (markerIndex < 0)
                        continue;

                    string dateStr = line.Substring(markerIndex + marker.Length)
                                         .Trim().TrimEnd(',').Trim('"');
                    if (DateTime.TryParse(dateStr, out DateTime result))
                        return result;
                }
            }
            catch { }
            return DateTime.MinValue; // Fallback for old/broken files
        }

        private void AllowHTTPStreams_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                Config.Save("radio_allow_http", mi.IsChecked);
            }
        }

        private DownloadWindow download;
        private void DownloadMusic_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInternetAvailable())
            {
                MessageBox.Show(this, "No internet connection.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (download == null || !download.IsLoaded)
            {
                download = new DownloadWindow(this);
                download.Owner = this;
                download.Closed += (s, args) => download = null;
                download.Show();
            }
            else
            {
                if (download.IsVisible) download.Show();
                download.Activate();
                if (download.WindowState == WindowState.Minimized)
                {
                    download.WindowState = WindowState.Normal;
                }
            }
        }

        private void MenuIntegrate_Checked(object sender, RoutedEventArgs e)
        {
            IntegrateShellExtension();
            Config.Save("shell_integration", true);
        }

        private void MenuIntegrate_Unchecked(object sender, RoutedEventArgs e)
        {
            RemoveShellExtension();
            Config.Save("shell_integration", false);
        }

        private void MenuStats_Click(object sender, RoutedEventArgs e)
        {
            StatsWindow existingStats = Application.Current.Windows.OfType<StatsWindow>().FirstOrDefault();

            if (existingStats != null)
            {
                // 3. If it exists, JUST FOCUS IT. Do not create a new one.
                existingStats.Activate();

                if (existingStats.WindowState == WindowState.Minimized)
                {
                    existingStats.WindowState = WindowState.Normal;
                }
            }
            else
            {
                // 4. If it truly doesn't exist, create it.
                StatsWindow statsWindow = new StatsWindow(this);
                statsWindow.Owner = this;
                statsWindow.Show();
            }
        }

        private void ShowNotification(ImageSource art, string title, string artist)
        {
            if (_isFirstLoad || (this.IsActive && this.WindowState != WindowState.Minimized)) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;

            dispatcher.Invoke(() =>
            {
                string notificationTitle = string.IsNullOrWhiteSpace(title) ? "Now playing" : title;
                string notificationArtist = string.IsNullOrWhiteSpace(artist) ? "Unknown artist" : artist;

                try
                {
                    Color currentThemeColor = ((SolidColorBrush)IpodBody.Background).Color;
                    CloseActiveNotificationWindow();

                    _activeNotificationWindow = new NotificationWindow(art, notificationTitle, notificationArtist, currentThemeColor);
                    _activeNotificationWindow.Closed += (s, _) =>
                    {
                        if (ReferenceEquals(_activeNotificationWindow, s))
                            _activeNotificationWindow = null;
                    };
                    _activeNotificationWindow.Show();
                }
                catch
                {
                    ShowLegacyNotification(notificationTitle, notificationArtist);
                }
            });
        }

        private void CloseActiveNotificationWindow()
        {
            if (_activeNotificationWindow == null)
                return;

            try
            {
                _activeNotificationWindow.Close();
            }
            catch { }
            finally
            {
                _activeNotificationWindow = null;
            }
        }

        //private static bool IsWindows10OrLater()
        //{
        //    var versionInfo = new OSVERSIONINFOEX();
        //    versionInfo.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));

        //    if (RtlGetVersion(ref versionInfo) == 0)
        //    {
        //        return versionInfo.dwMajorVersion >= 10;
        //    }

        //    return Environment.OSVersion.Version.Major >= 10;
        //}

        private void ShowLegacyNotification(string title, string artist)
        {
            if (_notifyIcon == null) return;

            string message = string.IsNullOrWhiteSpace(artist)
                ? title
                : $"{artist}";

            _notifyIcon.Visible = true;
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.None;
            _notifyIcon.ShowBalloonTip(5000);

            _notifyIcon.BalloonTipClosed += (s, e) =>
            {
                _notifyIcon.Visible = false;
            };
        }

        //private Uri SaveNotificationImageToFile(BitmapSource bitmap)
        //{
        //    try
        //    {
        //        if (bitmap == null)
        //            return null;

        //        Directory.CreateDirectory(NotificationImagesFolder);

        //        string filePath = System.IO.Path.Combine(
        //            NotificationImagesFolder,
        //            "now_playing_cover.jpg");

        //        BitmapSource source = bitmap;
        //        double maxSide = Math.Max(source.PixelWidth, source.PixelHeight);
        //        if (maxSide > 256)
        //        {
        //            double scale = 256 / maxSide;
        //            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        //        }

        //        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
        //        using (var fileStream = new System.IO.FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
        //        {
        //            BitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 88 };
        //            encoder.Frames.Add(BitmapFrame.Create(converted));
        //            encoder.Save(fileStream);
        //            fileStream.Flush(true);
        //        }

        //        if (!IsNotificationImageReady(filePath))
        //            return null;

        //        // Toast XML needs a URI-style local path. Passing a raw Windows path can
        //        // reserve the image slot while Windows fails to resolve the actual file.
        //        string fileUri = new Uri(filePath, UriKind.Absolute).AbsoluteUri;
        //        return new Uri(fileUri, UriKind.Absolute);
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Notification image save failed: {ex.Message}");
        //        return null;
        //    }
        //}

        //private bool IsNotificationImageReady(string filePath)
        //{
        //    try
        //    {
        //        var info = new FileInfo(filePath);
        //        if (!info.Exists || info.Length == 0)
        //            return false;

        //        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        //        {
        //            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        //            return decoder.Frames.Count > 0 &&
        //                   decoder.Frames[0].PixelWidth > 0 &&
        //                   decoder.Frames[0].PixelHeight > 0;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Notification image verification failed: {ex.Message}");
        //        return false;
        //    }
        //}

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isDevicePoweredOff)
                ResetScreenSleepTimer();

            if (_isDevicePoweredOff || _isDeviceLocked)
            {
                switch (e.Key)
                {
                    case Key.Up:
                    case Key.VolumeUp:
                        VolumeUp_Click(null, null);
                        e.Handled = true;
                        return;
                    case Key.Down:
                    case Key.VolumeDown:
                        VolumeDown_Click(null, null);
                        e.Handled = true;
                        return;
                    case Key.M:
                    case Key.VolumeMute:
                        MenuMute.IsChecked = !MenuMute.IsChecked;
                        e.Handled = true;
                        return;
                }
            }

            if (_isDevicePoweredOff)
            {
                e.Handled = true;
                return;
            }

            if (_isDeviceLocked)
            {
                if (e.Key == Key.Space || e.Key == Key.MediaPlayPause)
                {
                    TogglePlaybackFromLockScreen();
                    e.Handled = true;
                    return;
                }

                if (IsPasscodePanelActive())
                {
                    ResetScreenSleepTimer();

                    string digit = DigitFromKey(e.Key);
                    if (digit != null)
                    {
                        AppendPasscodeDigit(digit);
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.Back)
                    {
                        RemovePasscodeDigit();
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.Enter)
                    {
                        ProcessPasscodeEntry();
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.Escape)
                    {
                        CancelPasscodeFlow();
                        e.Handled = true;
                        return;
                    }
                }
                if (IsPasscodeRequired() && _passcodeMode == PasscodeMode.Unlock)
                {
                    ShowPasscodeEntry(false);
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    UnlockDevice();
                    e.Handled = true;
                    return;
                }

                e.Handled = true;
                return;
            }

            if (CoverFlowView.Visibility == Visibility.Visible)
            {
                switch (e.Key)
                {
                    case Key.Right:
                        MoveCoverFlow(1);
                        e.Handled = true;
                        return;
                    case Key.Left:
                        MoveCoverFlow(-1);
                        e.Handled = true;
                        return;
                    case Key.Escape:
                        BackToPlayer_Click(null, null);
                        e.Handled = true;
                        return;
                }
            }

            switch (e.Key)
            {
                case Key.Space:
                    PlayPause_Click(null, null);
                    e.Handled = true; 
                    break;
                case Key.Right:
                    Next_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.Left:
                    Prev_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.Up:
                case Key.VolumeUp:
                    VolumeUp_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.Down:
                case Key.VolumeDown:
                    VolumeDown_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.M:
                case Key.VolumeMute:
                    MenuMute.IsChecked = !MenuMute.IsChecked;
                    e.Handled = true;
                    break;
                case Key.F:
                    MenuFavorite.IsChecked = !MenuFavorite.IsChecked;
                    e.Handled = true;
                    break;
                case Key.L:
                    if (isLyricsMode)
                        BackToPlayer_Click(null, null);
                    else
                        ShowLyrics_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.S:
                    OpenSearch_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.N:
                    NewPlaylist_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.R:
                    OpenRadioSearch_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.T:
                    ToggleDetailedView_Click(null, null);
                    e.Handled = true;
                    break;
                case Key.D:
                    DownloadMusic_Click(null, null);
                    e.Handled = true;
                    break;

            }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isDevicePoweredOff && IsPointerOverIpodScreen(e))
                ResetScreenSleepTimer();
        }

        private bool IsPointerOverIpodScreen(MouseEventArgs e)
        {
            if (IpodScreen == null || IpodScreen.Visibility != Visibility.Visible)
                return false;

            Point position = e.GetPosition(IpodScreen);
            return position.X >= 0 &&
                   position.Y >= 0 &&
                   position.X <= IpodScreen.ActualWidth &&
                   position.Y <= IpodScreen.ActualHeight;
        }

        #endregion
    }

    public class PlaylistInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int SongCount { get; set; }
        public DateTime DateCreated { get; set; }
        public string SongCountDisplay => $"{SongCount} songs";
    }

    public class RadioStation
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public List<string> AlternateUrls { get; set; } = new List<string>();
        public string FaviconUrl { get; set; }
        public bool IsUnavailable { get; set; }
        public DateTime? LastFailedUtc { get; set; }
    }

    public class RadioStreamOpenResult
    {
        public int Handle { get; set; }
        public string Url { get; set; }
    }

    public class CoverFlowAlbum
    {
        public string AlbumName { get; set; }
        public string Artist { get; set; }
        public string CoverArtPath { get; set; }
        public List<CoverFlowTrack> Tracks { get; } = new List<CoverFlowTrack>();
    }

    public class CoverFlowTrack
    {
        public string Path { get; set; }
        public uint TrackNumber { get; set; }
    }
}
