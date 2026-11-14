using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WindowsShuffle.Classes;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

namespace WindowsShuffle
{
    public partial class DownloadWindow : Window
    {
        private readonly YoutubeClient _youtube = new YoutubeClient();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3);

        private IAsyncEnumerator<VideoSearchResult> _searchEnumerator;

        public ObservableCollection<DownloadItem> Downloads { get; set; } = new ObservableCollection<DownloadItem>();
        public ObservableCollection<SearchResultItem> SearchResults { get; set; } = new ObservableCollection<SearchResultItem>();

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
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private async void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await PerformSearch();
        }

        private async void DoSearch_Click(object sender, RoutedEventArgs e) => await PerformSearch();

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtSearchStatus == null || BtnAddToQueue == null) return;

            // Fix 1: Clear results immediately when switching modes
            SearchResults.Clear();
            ResultsView.Visibility = Visibility.Collapsed;
            BtnAddToQueue.Visibility = Visibility.Collapsed;
            BtnLoadMore.Visibility = Visibility.Collapsed;

            if (_searchEnumerator != null)
            {
                _ = _searchEnumerator.DisposeAsync();
                _searchEnumerator = null;
            }

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

        private async Task PerformSearch()
        {
            if (!main.IsInternetAvailable())
            {
                MessageBox.Show(this, "No internet connection.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string query = SearchInput.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            try
            {
                SearchInput.IsEnabled = false;
                TxtSearchStatus.Text = "Searching...";
                SearchResults.Clear();
                BtnLoadMore.Visibility = Visibility.Collapsed;

                if (_searchEnumerator != null)
                {
                    await _searchEnumerator.DisposeAsync();
                    _searchEnumerator = null;
                }

                if (RadPlaylist.IsChecked == true)
                {
                    try
                    {
                        var playlist = await _youtube.Playlists.GetAsync(query);
                        var videos = _youtube.Playlists.GetVideosAsync(playlist.Id);
                        var enumerator = videos.GetAsyncEnumerator();

                        try
                        {
                            while (await enumerator.MoveNextAsync())
                            {
                                var video = enumerator.Current;
                                SearchResults.Add(new SearchResultItem
                                {
                                    VideoId = video.Id,
                                    Title = video.Title,
                                    Author = video.Author.ChannelTitle,
                                    ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
                                    Duration = video.Duration?.ToString(@"mm\:ss") ?? "--:--"
                                });
                            }
                        }
                        finally { await enumerator.DisposeAsync(); }

                        TxtSearchStatus.Tag = playlist.Title;
                        TxtSearchStatus.Text = $"Found Playlist: {playlist.Title} ({SearchResults.Count} songs)";
                    }
                    catch
                    {
                        TxtSearchStatus.Text = "Invalid playlist URL or private playlist.";
                        return;
                    }
                }
                else
                {
                    // Fix 2: Initialize pagination for songs
                    _searchEnumerator = _youtube.Search.GetVideosAsync(query).GetAsyncEnumerator();
                    await FetchNextBatch();
                }

                ResultsView.Visibility = Visibility.Visible;
                BtnAddToQueue.Visibility = Visibility.Visible;
                ListDownloads.Visibility = Visibility.Collapsed;
                BtnViewToggle.Content = "Show Queue";
            }
            catch (Exception ex)
            {
                TxtSearchStatus.Text = "Search failed.";
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                SearchInput.IsEnabled = true;
            }
        }

        private async void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            BtnLoadMore.IsEnabled = false;
            try
            {
                await FetchNextBatch();
            }
            catch (Exception ex)
            {
                TxtSearchStatus.Text = "Could not load more results.";
                System.Diagnostics.Debug.WriteLine($"Load more failed: {ex.Message}");
            }
            finally
            {
                BtnLoadMore.IsEnabled = true;
            }
        }

        private async Task FetchNextBatch()
        {
            if (_searchEnumerator == null) return;

            int count = 0;
            bool hasMore = false;

            while (count < 20) // Load 20 results per "page"
            {
                if (await _searchEnumerator.MoveNextAsync())
                {
                    var video = _searchEnumerator.Current;
                    SearchResults.Add(new SearchResultItem
                    {
                        VideoId = video.Id,
                        Title = video.Title,
                        Author = video.Author.ChannelTitle,
                        ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
                        Duration = video.Duration?.ToString(@"mm\:ss") ?? "--:--"
                    });
                    count++;
                    hasMore = true;
                }
                else
                {
                    hasMore = false;
                    break;
                }
            }

            BtnLoadMore.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;
            TxtSearchStatus.Text = $"Displaying {SearchResults.Count} results found.";
        }

        private void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            if (RadPlaylist.IsChecked == true)
            {
                if (SearchResults.Count == 0) return;

                var playlistItem = new DownloadItem
                {
                    IsPlaylist = true,
                    Title = TxtSearchStatus.Tag?.ToString() ?? "Untitled Playlist",
                    Author = "YouTube Playlist",
                    ThumbnailUrl = SearchResults.FirstOrDefault()?.ThumbnailUrl,
                    PlaylistItems = SearchResults.ToList(),
                    ShowDownloadButton = false // Start immediately
                };

                Downloads.Add(playlistItem);
                _ = ProcessDownload(playlistItem);
            }
            else
            {
                var selected = ListResults.SelectedItems.Cast<SearchResultItem>().ToList();
                if (selected.Count == 0) return;

                foreach (var res in selected)
                {
                    if (Downloads.Any(d => d.VideoId == res.VideoId)) continue;
                    Downloads.Add(new DownloadItem
                    {
                        VideoId = res.VideoId,
                        Title = res.Title,
                        Author = res.Author,
                        ThumbnailUrl = res.ThumbnailUrl
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

        //private async Task ProcessDownload(DownloadItem item)
        //{
        //    item.IsDownloading = true;
        //    try { await _semaphore.WaitAsync(item.Cts.Token); }
        //    catch (OperationCanceledException) { item.StatusText = "Canceled"; item.IsDownloading = false; return; }

        //    try
        //    {
        //        string baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Podmix Downloads");

        //        // Ensure the main downloads folder exists
        //        if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

        //        if (item.IsPlaylist)
        //        {
        //            // Sanitize playlist name for folder creation
        //            string sanitizedPlaylistName = string.Join("_", item.Title.Split(Path.GetInvalidFileNameChars()));
        //            string playlistFolder = Path.Combine(baseFolder, sanitizedPlaylistName);

        //            // Create the playlist subfolder before starting downloads
        //            if (!Directory.Exists(playlistFolder)) Directory.CreateDirectory(playlistFolder);

        //            item.CompletedTracks = 0;
        //            foreach (var video in item.PlaylistItems)
        //            {
        //                if (item.Cts.Token.IsCancellationRequested) break;

        //                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.VideoId);
        //                var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        //                if (streamInfo == null) continue;

        //                string fileName = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars())) + ".m4a";
        //                string fullPath = Path.Combine(playlistFolder, fileName);

        //                // Progress handler that includes both the track count and current track percentage
        //                var progress = new Progress<double>(p =>
        //                {
        //                    item.StatusText = $"Downloading {item.CompletedTracks + 1}/{item.TotalTracks} ({(p * 100):F0}%)...";
        //                });

        //                await _youtube.Videos.Streams.DownloadAsync(streamInfo, fullPath, progress, item.Cts.Token);

        //                item.CompletedTracks++;
        //            }
        //            item.StatusText = item.Cts.Token.IsCancellationRequested ? "Canceled" : "Completed";
        //        }
        //        else
        //        {
        //            // Single Song Logic
        //            item.StatusText = "Connecting...";
        //            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(item.VideoId);
        //            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        //            if (streamInfo == null) { item.StatusText = "Stream Error"; return; }

        //            string fileName = string.Join("_", item.Title.Split(Path.GetInvalidFileNameChars())) + ".m4a";
        //            var progress = new Progress<double>(p => item.StatusText = $"Downloading... {(p * 100):F0}%");
        //            await _youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(baseFolder, fileName), progress, item.Cts.Token);
        //            item.StatusText = "Completed";
        //        }
        //        item.IsCompleted = true;
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        item.StatusText = "Canceled";
        //    }
        //    catch (Exception ex)
        //    {
        //        // This will show the actual error message in your UI
        //        item.StatusText = "Failed to download. Make sure the content is Public or Unlisted.";
        //        item.ShowDownloadButton = !item.IsPlaylist;

        //        // Also logs the full technical details to your Visual Studio Output window
        //        Clipboard.SetText($"Download Error for {item.Title}: {ex}");
        //    }
        //    finally { item.IsDownloading = false; _semaphore.Release(); }
        //}

        private async Task ProcessDownload(DownloadItem item)
        {
            item.IsDownloading = true;
            try { await _semaphore.WaitAsync(item.Cts.Token); }
            catch (OperationCanceledException) { item.StatusText = "Canceled"; item.IsDownloading = false; return; }

            try
            {
                string baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Podmix Downloads");
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

                if (!FfmpegBootstrapper.IsAvailable())
                {
                    item.StatusText = "Preparing converter...";
                    await FfmpegBootstrapper.EnsureAvailableAsync();
                }

                string ffmpegPath = FfmpegBootstrapper.FfmpegPath;

                if (item.IsPlaylist)
                {
                    string sanitizedPlaylistName = string.Join("_", item.Title.Split(Path.GetInvalidFileNameChars()));
                    string playlistFolder = Path.Combine(baseFolder, sanitizedPlaylistName);
                    if (!Directory.Exists(playlistFolder)) Directory.CreateDirectory(playlistFolder);

                    item.CompletedTracks = 0;
                    foreach (var video in item.PlaylistItems)
                    {
                        if (item.Cts.Token.IsCancellationRequested) break;

                        var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.VideoId);
                        var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                        if (streamInfo == null) continue;

                        string sanitizedTitle = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
                        string m4aPath = Path.Combine(playlistFolder, sanitizedTitle + ".m4a");
                        string mp3Path = Path.Combine(playlistFolder, sanitizedTitle + ".mp3");

                        var progress = new Progress<double>(p =>
                        {
                            item.StatusText = $"Downloading... {item.CompletedTracks + 1}/{item.TotalTracks} ({(p * 100):F0}%)...";
                        });

                        await _youtube.Videos.Streams.DownloadAsync(streamInfo, m4aPath, progress, item.Cts.Token);

                        // Convert to MP3
                        if (File.Exists(ffmpegPath))
                        {
                            item.StatusText = $"Converting... {item.CompletedTracks + 1}/{item.TotalTracks}...";
                            await ConvertToMp3(ffmpegPath, m4aPath, mp3Path);
                        }

                        item.CompletedTracks++;
                    }
                    item.StatusText = item.Cts.Token.IsCancellationRequested ? "Canceled" : "Completed";
                }
                else
                {
                    item.StatusText = "Connecting...";
                    var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(item.VideoId);
                    var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    if (streamInfo == null) { item.StatusText = "Stream Error"; return; }

                    string sanitizedTitle = string.Join("_", item.Title.Split(Path.GetInvalidFileNameChars()));
                    string m4aPath = Path.Combine(baseFolder, sanitizedTitle + ".m4a");
                    string mp3Path = Path.Combine(baseFolder, sanitizedTitle + ".mp3");

                    var progress = new Progress<double>(p => item.StatusText = $"Downloading... {(p * 100):F0}%");
                    await _youtube.Videos.Streams.DownloadAsync(streamInfo, m4aPath, progress, item.Cts.Token);

                    // Convert to MP3
                    if (File.Exists(ffmpegPath))
                    {
                        item.StatusText = "Converting to MP3...";
                        await ConvertToMp3(ffmpegPath, m4aPath, mp3Path);
                    }

                    item.StatusText = "Completed";
                }
                item.IsCompleted = true;
            }
            catch (OperationCanceledException)
            {
                item.StatusText = "Canceled";
            }
            catch (Exception ex)
            {
                item.StatusText = "Failed to download.";
                item.ShowDownloadButton = !item.IsPlaylist;
                try { Clipboard.SetText($"Download Error for {item.Title}: {ex}"); } catch { }
            }
            finally { item.IsDownloading = false; _semaphore.Release(); }
        }

        private async Task ConvertToMp3(string ffmpegPath, string inputPath, string outputPath)
        {
            // -i: input file
            // -codec:a libmp3lame: use the MP3 encoder
            // -qscale:a 2: high quality VBR (approx 190-250 kbps)
            // -y: overwrite output if exists
            string args = $"-i \"{inputPath}\" -codec:a libmp3lame -qscale:a 2 \"{outputPath}\" -y";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true // FFmpeg outputs logs to error stream
            };

            using (Process process = new Process { StartInfo = psi })
            {
                process.Start();
                await Task.Run(() => process.WaitForExit());

                // Delete the original m4a file if conversion was successful
                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    try { File.Delete(inputPath); } catch { }
                }
            }
        }

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DownloadItem item)
            {
                if (item.IsDownloading || item.IsCompleted) return;
                item.ShowDownloadButton = false;
                await ProcessDownload(item);
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DownloadItem item)
            {
                item.Cts.Cancel();
                // If not actively downloading or already finished/canceled, remove from list
                if (!item.IsDownloading || item.IsCompleted || item.StatusText == "Canceled")
                {
                    Downloads.Remove(item);
                }
            }
        }

        private void ClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = Downloads.Where(d => d.IsCompleted || d.StatusText == "Canceled" || d.StatusText == "Error").ToList();
            foreach (var item in toRemove) Downloads.Remove(item);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Podmix Downloads");
            if (Directory.Exists(path)) Process.Start("explorer.exe", path);
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
