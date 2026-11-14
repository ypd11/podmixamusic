
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using WindowsShuffle.Classes;

namespace WindowsShuffle
{
    public partial class DownloadWindow : Window
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3);

        private readonly string _ytDlpPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");

        private readonly List<SearchResultItem> _searchCache =
            new List<SearchResultItem>();

        private int _searchOffset = 0;
        private const int SearchPageSize = 20;

        public ObservableCollection<DownloadItem> Downloads { get; set; }
            = new ObservableCollection<DownloadItem>();

        public ObservableCollection<SearchResultItem> SearchResults { get; set; }
            = new ObservableCollection<SearchResultItem>();

        private MainWindow main;

        public DownloadWindow(MainWindow main)
        {
            InitializeComponent();

            this.main = main;
            Topmost = main.Topmost;

            ListDownloads.ItemsSource = Downloads;
            ListResults.ItemsSource = SearchResults;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await PerformSearch();
        }

        private async void DoSearch_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtSearchStatus == null || BtnAddToQueue == null)
                return;

            SearchResults.Clear();
            _searchCache.Clear();
            _searchOffset = 0;

            ResultsView.Visibility = Visibility.Collapsed;
            BtnAddToQueue.Visibility = Visibility.Collapsed;
            BtnLoadMore.Visibility = Visibility.Collapsed;

            if (RadPlaylist.IsChecked == true)
            {
                TxtSearchStatus.Text = "Paste Playlist Link";
                BtnAddToQueue.Content = "Download Playlist";
            }
            else
            {
                TxtSearchStatus.Text = "Search YouTube";
                BtnAddToQueue.Content = "Add Selected to Queue";
            }
        }

        // ---------------------------------------------------------
        // SEARCH
        // ---------------------------------------------------------

        private async Task PerformSearch()
        {
            if (!main.IsInternetAvailable())
            {
                MessageBox.Show(
                    this,
                    "No internet connection.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            if (!File.Exists(_ytDlpPath))
            {
                MessageBox.Show(
                    this,
                    "yt-dlp.exe was not found.\n\nPlace yt-dlp.exe in the same folder as Podmix.exe.",
                    "Missing yt-dlp",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            string query = SearchInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
                return;

            try
            {
                SearchInput.IsEnabled = false;
                TxtSearchStatus.Text = "Searching...";

                SearchResults.Clear();
                _searchCache.Clear();
                _searchOffset = 0;

                JObject json;

                if (RadPlaylist.IsChecked == true)
                {
                    string arguments =
                        "--dump-single-json --flat-playlist --no-warnings --ignore-errors " +
                        QuoteArgument(query);

                    string output = await RunYtDlpAsync(
                        arguments,
                        CancellationToken.None,
                        null);

                    json = JObject.Parse(output);

                    string playlistTitle =
                        (string)json["title"] ?? "YouTube Playlist";

                    JArray entries = json["entries"] as JArray;

                    if (entries == null || entries.Count == 0)
                    {
                        TxtSearchStatus.Text = "No videos found in this playlist.";
                        return;
                    }

                    foreach (JToken entry in entries)
                    {
                        SearchResultItem result = ParseSearchResult(entry);

                        if (result != null)
                            SearchResults.Add(result);
                    }

                    TxtSearchStatus.Tag = playlistTitle;

                    TxtSearchStatus.Text =
                        "Found Playlist: " + playlistTitle +
                        " (" + SearchResults.Count + " songs)";
                }
                else
                {
                    string arguments =
                        "--dump-single-json --flat-playlist --no-warnings " +
                        "--playlist-end 100 " +
                        QuoteArgument("ytsearch100:" + query);

                    string output = await RunYtDlpAsync(
                        arguments,
                        CancellationToken.None,
                        null);

                    json = JObject.Parse(output);

                    JArray entries = json["entries"] as JArray;

                    if (entries == null || entries.Count == 0)
                    {
                        TxtSearchStatus.Text = "No results found.";
                        return;
                    }

                    foreach (JToken entry in entries)
                    {
                        SearchResultItem result = ParseSearchResult(entry);

                        if (result != null)
                            _searchCache.Add(result);
                    }

                    DisplayNextSearchPage();
                }

                ResultsView.Visibility = Visibility.Visible;
                BtnAddToQueue.Visibility = Visibility.Visible;

                ListDownloads.Visibility = Visibility.Collapsed;
                BtnViewToggle.Content = "Show Queue";
            }
            catch (Exception ex)
            {
                TxtSearchStatus.Text = "Search failed.";

                MessageBox.Show(
                    this,
                    "Error: " + ex.Message,
                    "Search Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SearchInput.IsEnabled = true;
            }
        }

        private SearchResultItem ParseSearchResult(JToken entry)
        {
            if (entry == null)
                return null;

            string id = (string)entry["id"];

            if (string.IsNullOrWhiteSpace(id))
                return null;

            string title = (string)entry["title"] ?? "Unknown Title";

            string author =
                (string)entry["uploader"] ??
                (string)entry["channel"] ??
                (string)entry["uploader_id"] ??
                "Unknown Artist";

            string thumbnail = (string)entry["thumbnail"];

            if (string.IsNullOrWhiteSpace(thumbnail))
            {
                JArray thumbnails = entry["thumbnails"] as JArray;

                if (thumbnails != null && thumbnails.Count > 0)
                    thumbnail = (string)thumbnails.Last()["url"];
            }

            string duration = "--:--";

            double seconds;

            if (double.TryParse(
                (string)entry["duration"],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out seconds))
            {
                duration = TimeSpan.FromSeconds(seconds).ToString(
                    seconds >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");
            }

            return new SearchResultItem
            {
                VideoId = id,
                Title = title,
                Author = author,
                ThumbnailUrl = thumbnail,
                Duration = duration
            };
        }

        private void DisplayNextSearchPage()
        {
            int end = Math.Min(
                _searchOffset + SearchPageSize,
                _searchCache.Count);

            for (int i = _searchOffset; i < end; i++)
                SearchResults.Add(_searchCache[i]);

            _searchOffset = end;

            BtnLoadMore.Visibility =
                _searchOffset < _searchCache.Count
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            TxtSearchStatus.Text =
                "Displaying " + SearchResults.Count +
                " results found.";
        }

        private void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            DisplayNextSearchPage();
        }

        // ---------------------------------------------------------
        // QUEUE
        // ---------------------------------------------------------

        private void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            if (RadPlaylist.IsChecked == true)
            {
                if (SearchResults.Count == 0)
                    return;

                var playlistItem = new DownloadItem
                {
                    IsPlaylist = true,
                    PlaylistUrl = SearchInput.Text.Trim(),
                    Title = TxtSearchStatus.Tag?.ToString() ?? "Untitled Playlist",
                    Author = "YouTube Playlist",
                    ThumbnailUrl = SearchResults.FirstOrDefault()?.ThumbnailUrl,
                    PlaylistItems = SearchResults.ToList(),
                    ShowDownloadButton = false
                };

                Downloads.Add(playlistItem);

                _ = ProcessDownload(playlistItem);
            }
            else
            {
                var selected = ListResults.SelectedItems
                    .Cast<SearchResultItem>()
                    .ToList();

                if (selected.Count == 0)
                    return;

                foreach (var result in selected)
                {
                    if (Downloads.Any(d => d.VideoId == result.VideoId))
                        continue;

                    Downloads.Add(new DownloadItem
                    {
                        VideoId = result.VideoId,
                        Title = result.Title,
                        Author = result.Author,
                        ThumbnailUrl = result.ThumbnailUrl
                    });
                }
            }

            ListResults.UnselectAll();

            ResultsView.Visibility = Visibility.Collapsed;
            ListDownloads.Visibility = Visibility.Visible;

            BtnViewToggle.Content = "Show Results";
        }

        private void ToggleView_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsView.Visibility == Visibility.Visible)
            {
                ResultsView.Visibility = Visibility.Collapsed;
                ListDownloads.Visibility = Visibility.Visible;
                BtnViewToggle.Content = "Show Results";
            }
            else
            {
                ResultsView.Visibility = Visibility.Visible;
                ListDownloads.Visibility = Visibility.Collapsed;
                BtnViewToggle.Content = "Show Queue";
            }
        }

        // ---------------------------------------------------------
        // DOWNLOADS
        // ---------------------------------------------------------

        private async Task ProcessDownload(DownloadItem item)
        {
            bool semaphoreAcquired = false;

            item.IsDownloading = true;

            try
            {
                await _semaphore.WaitAsync(item.Cts.Token);
                semaphoreAcquired = true;

                if (!File.Exists(_ytDlpPath))
                    throw new FileNotFoundException(
                        "yt-dlp.exe was not found beside Podmix.exe.");

                string baseFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    "Podmix Downloads");

                Directory.CreateDirectory(baseFolder);

                if (!FfmpegBootstrapper.IsAvailable())
                {
                    item.StatusText = "Preparing converter...";

                    await FfmpegBootstrapper.EnsureAvailableAsync();

                    if (!FfmpegBootstrapper.IsAvailable())
                        throw new Exception("FFmpeg could not be installed.");
                }

                string arguments;

                if (item.IsPlaylist)
                {
                    if (string.IsNullOrWhiteSpace(item.PlaylistUrl))
                        throw new Exception("The playlist URL is missing.");

                    string sanitizedName = SanitizeFileName(item.Title);

                    string playlistFolder = Path.Combine(
                        baseFolder,
                        sanitizedName);

                    Directory.CreateDirectory(playlistFolder);

                    item.CompletedTracks = 0;

                    string outputTemplate = Path.Combine(
                        playlistFolder,
                        "%(playlist_index)02d - %(title)s.%(ext)s");

                    arguments =
                        "-x " +
                        "--audio-format mp3 " +
                        "--audio-quality 320K " +
                        "--add-metadata " +
                        "--embed-thumbnail " +
                        "--progress " +
                        "--newline " +
                        "--yes-playlist " +
                        "--ignore-errors " +
                        "--ffmpeg-location " + QuoteArgument(
                            Path.GetDirectoryName(FfmpegBootstrapper.FfmpegPath)) + " " +
                        "-o " + QuoteArgument(outputTemplate) + " " +
                        "--print " + QuoteArgument("after_move:__PODMIX_DONE__") + " " +
                        QuoteArgument(item.PlaylistUrl);
                }
                else
                {
                    string videoUrl =
                        "https://www.youtube.com/watch?v=" +
                        Uri.EscapeDataString(item.VideoId);

                    string outputTemplate = Path.Combine(
                        baseFolder,
                        "%(title)s.%(ext)s");

                    arguments =
                        "-x " +
                        "--audio-format mp3 " +
                        "--audio-quality 320K " +
                        "--add-metadata " +
                        "--embed-thumbnail " +
                        "--newline " +
                        "--no-playlist " +
                        "--ffmpeg-location " + QuoteArgument(
                            Path.GetDirectoryName(FfmpegBootstrapper.FfmpegPath)) + " " +
                        "-o " + QuoteArgument(outputTemplate) + " " +
                        "--print " + QuoteArgument("after_move:__PODMIX_DONE__") + " " +
                        QuoteArgument(videoUrl);
                }

                item.StatusText = "Connecting...";

                StringBuilder errorOutput = new StringBuilder();

                await RunYtDlpAsync(
                    arguments,
                    item.Cts.Token,
                    line =>
                    {
                        if (line.Contains("__PODMIX_DONE__"))
                        {
                            item.CompletedTracks++;
                            return;
                        }

                        Match match = Regex.Match(
                            line,
                            @"\[download\]\s+(\d+(?:\.\d+)?)%");

                        if (match.Success)
                        {
                            string percentage = match.Groups[1].Value;

                            if (item.IsPlaylist)
                            {
                                item.StatusText =
                                    "Downloading " +
                                    Math.Min(item.CompletedTracks + 1, item.TotalTracks) +
                                    "/" + item.TotalTracks +
                                    " (" + percentage + "%)...";
                            }
                            else
                            {
                                item.StatusText =
                                    "Downloading... " + percentage + "%";
                            }
                        }
                        else if (line.Contains("[ExtractAudio]") ||
                                 line.Contains("[EmbedThumbnail]") ||
                                 line.Contains("[Metadata]"))
                        {
                            item.StatusText = "Processing audio...";
                        }
                    },
                    line =>
                    {
                        errorOutput.AppendLine(line);

                        Match match = Regex.Match(
                            line,
                            @"\[download\]\s+(\d+(?:\.\d+)?)%");

                        if (match.Success)
                        {
                            string percentage = match.Groups[1].Value;

                            if (item.IsPlaylist)
                            {
                                item.StatusText =
                                    "Downloading " +
                                    Math.Min(item.CompletedTracks + 1, item.TotalTracks) +
                                    "/" + item.TotalTracks +
                                    " (" + percentage + "%)...";
                            }
                            else
                            {
                                item.StatusText =
                                    "Downloading... " + percentage + "%";
                            }
                        }
                    });

                if (item.Cts.Token.IsCancellationRequested)
                {
                    item.StatusText = "Canceled";
                    return;
                }

                item.IsCompleted = true;
                item.CompletedTracks = item.TotalTracks;
                item.StatusText = "Completed";
            }
            catch (OperationCanceledException)
            {
                item.StatusText = "Canceled";
            }
            catch (Exception ex)
            {
                item.StatusText = "Failed to download.";
                item.ShowDownloadButton = !item.IsPlaylist;

                try
                {
                    Clipboard.SetText(
                        "Download Error for " + item.Title + ":\n" + ex);
                }
                catch { }
            }
            finally
            {
                item.IsDownloading = false;

                if (semaphoreAcquired)
                    _semaphore.Release();
            }
        }

        // ---------------------------------------------------------
        // YT-DLP PROCESS
        // ---------------------------------------------------------


        private async Task<string> RunYtDlpAsync(
            string arguments,
            CancellationToken cancellationToken,
            Action<string> outputHandler,
            Action<string> errorHandler = null)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = arguments,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            StringBuilder standardOutput = new StringBuilder();

            using (Process process = new Process())
            {
                process.StartInfo = psi;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null) return;

                    standardOutput.AppendLine(e.Data);

                    if (outputHandler != null)
                        outputHandler(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null && errorHandler != null)
                        errorHandler(e.Data);
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (CancellationTokenRegistration registration =
                    cancellationToken.Register(() =>
                    {
                        try
                        {
                            if (!process.HasExited)
                                process.Kill();
                        }
                        catch { }
                    }))
                {
                    await Task.Run(() =>
                    {
                        process.WaitForExit();
                        process.WaitForExit();
                    });
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (process.ExitCode != 0)
                    throw new Exception("yt-dlp exited with code " + process.ExitCode);

                return standardOutput.ToString();
            }
        }


        private string QuoteArgument(string argument)
        {
            if (argument == null)
                return "\"\"";

            return "\"" + argument.Replace("\\", "\\\\")
                                  .Replace("\"", "\\\"") + "\"";
        }

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "YouTube Playlist";

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name.Trim();
        }

        // ---------------------------------------------------------
        // QUEUE BUTTONS
        // ---------------------------------------------------------

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DownloadItem item)
            {
                if (item.IsDownloading || item.IsCompleted)
                    return;

                item.ShowDownloadButton = false;

                await ProcessDownload(item);
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DownloadItem item)
            {
                item.Cts.Cancel();

                if (!item.IsDownloading ||
                    item.IsCompleted ||
                    item.StatusText == "Canceled")
                {
                    Downloads.Remove(item);
                }
            }
        }

        private void ClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = Downloads.Where(d =>
                d.IsCompleted ||
                d.StatusText == "Canceled" ||
                d.StatusText == "Error").ToList();

            foreach (var item in toRemove)
                Downloads.Remove(item);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                "Podmix Downloads");

            if (Directory.Exists(path))
                Process.Start("explorer.exe", path);
        }
    }

    public class SearchResultItem
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Duration { get; set; }
    }
}
