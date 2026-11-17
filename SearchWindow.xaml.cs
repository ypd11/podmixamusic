using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsShuffle
{
    public partial class SearchWindow : Window
    {
        readonly MainWindow main;
        private List<SongResult> _libraryCache = new List<SongResult>();
        private int _searchVersion = 0;
        private const int MaxVisibleResults = 80;

        public SearchWindow(MainWindow main)
        {
            InitializeComponent();

            this.main = main;
            Topmost = main.Topmost;
            SearchInput.Focus();

            LoadLibraryCache();
        }

        //private void LoadLibraryCache()
        //{
        //    _libraryCache = main.playlist
        //        .Where(File.Exists)
        //        .Select((file, index) => CreatePathOnlyResult(file, index + 1))
        //        .OrderBy(s => s.Title)
        //        .ToList();

        //    TxtNoResults.Text = "Search songs in current playlist";
        //    TxtNoResults.Visibility = Visibility.Visible;
        //}

        private async void LoadLibraryCache()
        {
            TxtNoResults.Text = "Loading music collection...";
            TxtNoResults.Visibility = Visibility.Visible;

            try
            {
                var files = await main.GetLibraryAudioFilesAsync();

                _libraryCache = await Task.Run(() =>
                {
                    return files
                        .Where(File.Exists)
                        .Select((file, index) =>
                            LoadSongMetadataForIndex(
                                CreatePathOnlyResult(file, index + 1)))
                        .OrderBy(s => s.Title)
                        .ToList();
                });

                TxtNoResults.Text = _libraryCache.Count > 0
                    ? "Search songs, albums or artists"
                    : "No music found in selected folder";

                TxtNoResults.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                TxtNoResults.Text = "Unable to load music collection";
                TxtNoResults.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private SongResult CreatePathOnlyResult(string file, int index)
        {
            string fileName = Path.GetFileName(file);
            return new SongResult
            {
                Index = index,
                Title = Path.GetFileNameWithoutExtension(file),
                Artist = "Unknown Artist",
                Album = "Unknown Album",
                Duration = "--:--",
                FileName = fileName,
                FullPath = file,
                FolderPath = Path.GetDirectoryName(file),
                AlbumArt = null,
                IsMetadataLoaded = false
            };
        }

        private SongResult LoadSongMetadataForIndex(SongResult fallback)
        {
            try
            {
                using (var tfile = TagLib.File.Create(fallback.FullPath))
                {
                    string artist =
                        tfile.Tag.FirstAlbumArtist ??
                        tfile.Tag.FirstPerformer ??
                        "Unknown Artist";

                    if (string.IsNullOrWhiteSpace(artist))
                        artist = "Unknown Artist";

                    string album = tfile.Tag.Album;

                    if (string.IsNullOrWhiteSpace(album))
                        album = "Unknown Album";

                    return new SongResult
                    {
                        Index = fallback.Index,
                        Title = !string.IsNullOrWhiteSpace(tfile.Tag.Title)
                            ? tfile.Tag.Title
                            : fallback.Title,
                        Artist = artist,
                        Album = album,
                        Duration = tfile.Properties.Duration.ToString(@"mm\:ss"),
                        FileName = fallback.FileName,
                        FullPath = fallback.FullPath,
                        FolderPath = fallback.FolderPath,
                        AlbumArt = null,
                        IsMetadataLoaded = true
                    };
                }
            }
            catch
            {
                fallback.IsMetadataLoaded = true;
                return fallback;
            }
        }

        //private async Task PerformSearch()
        //{
        //    string query = SearchInput.Text.ToLower().Trim();
        //    if (string.IsNullOrEmpty(query)) return;

        //    int searchVersion = ++_searchVersion;

        //    ListResults.Items.Clear();
        //    TxtNoResults.Visibility = Visibility.Collapsed;

        //    // Fast in-memory search
        //    var results = _libraryCache.Where(s =>
        //        (s.FileName?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
        //        (s.Title?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
        //        (s.Artist?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
        //        (s.Album?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
        //    ).Take(MaxVisibleResults).ToList();

        //    if (results.Any())
        //    {
        //        foreach (var item in results)
        //        {
        //            ListResults.Items.Add(item);
        //        }

        //        await EnrichVisibleResultsAsync(results, searchVersion);
        //    }
        //    else
        //    {
        //        TxtNoResults.Text = "Nothing found";
        //        TxtNoResults.Visibility = Visibility.Visible;
        //    }
        //}


        private async Task PerformSearch()
        {
            string query = SearchInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
                return;

            int searchVersion = ++_searchVersion;

            ArtistResults.Items.Clear();
            AlbumResults.Items.Clear();
            ListResults.Items.Clear();

            ArtistsSection.Visibility = Visibility.Collapsed;
            AlbumsSection.Visibility = Visibility.Collapsed;
            SongsSection.Visibility = Visibility.Collapsed;
            TxtNoResults.Visibility = Visibility.Collapsed;

            // Find tracks matching the search in any metadata field.
            var matchingTracks = _libraryCache.Where(s =>
                (s.FileName != null && s.FileName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (s.Title != null && s.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (s.Artist != null && s.Artist.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (s.Album != null && s.Album.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();

            // Artists
            var artists = matchingTracks
                .Where(s => !string.IsNullOrWhiteSpace(s.Artist) &&
                            s.Artist != "Unknown Artist")
                .GroupBy(s => s.Artist.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new SearchGroupResult
                {
                    Name = g.Key,
                    Subtitle = g.Count() + (g.Count() == 1 ? " song" : " songs"),
                    Tracks = g.ToList()
                })
                .OrderBy(a => a.Name)
                .Take(30)
                .ToList();

            // Albums (album + artist keeps identically named albums separate)
            var albums = matchingTracks
                .Where(s => !string.IsNullOrWhiteSpace(s.Album) &&
                            s.Album != "Unknown Album")
                .GroupBy(s => new
                {
                    Album = s.Album.Trim(),
                    Artist = s.Artist.Trim()
                })
                .Select(g => new SearchGroupResult
                {
                    Name = g.Key.Album,
                    Subtitle = g.Key.Artist,
                    Tracks = g.ToList()
                })
                .OrderBy(a => a.Name)
                .Take(30)
                .ToList();

            // Songs matching title or filename
            var songs = matchingTracks
                .Where(s =>
                    (s.Title != null && s.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (s.FileName != null && s.FileName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(MaxVisibleResults)
                .ToList();

            foreach (var artist in artists)
                ArtistResults.Items.Add(artist);

            foreach (var album in albums)
                AlbumResults.Items.Add(album);

            foreach (var song in songs)
                ListResults.Items.Add(song);

            ArtistsSection.Visibility = artists.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;

            AlbumsSection.Visibility = albums.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;

            SongsSection.Visibility = songs.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;

            bool hasResults = artists.Count > 0 || albums.Count > 0 || songs.Count > 0;

            if (!hasResults)
            {
                TxtNoResults.Text = "Nothing found";
                TxtNoResults.Visibility = Visibility.Visible;
            }

            if (songs.Count > 0)
                await EnrichVisibleResultsAsync(songs, searchVersion);
        }


        //private async Task EnrichVisibleResultsAsync(List<SongResult> results, int searchVersion)
        //{
        //    foreach (var result in results.Where(r => !r.IsMetadataLoaded).ToList())
        //    {
        //        if (searchVersion != _searchVersion || !IsVisible)
        //            return;

        //        SongResult enriched = await Task.Run(() => LoadSongMetadata(result));

        //        if (searchVersion != _searchVersion || !IsVisible)
        //            return;

        //        result.Title = enriched.Title;
        //        result.Artist = enriched.Artist;
        //        result.Album = enriched.Album;
        //        result.Duration = enriched.Duration;
        //        result.AlbumArt = enriched.AlbumArt;
        //        result.IsMetadataLoaded = true;
        //    }
        //}

        private async Task EnrichVisibleResultsAsync(List<SongResult> results, int searchVersion)
        {
            foreach (var result in results.Where(r => !r.IsArtworkLoaded).ToList())
            {
                if (searchVersion != _searchVersion || !IsVisible)
                    return;

                ImageSource artwork = await Task.Run(() =>
                    LoadSongArtwork(result.FullPath));

                if (searchVersion != _searchVersion || !IsVisible)
                    return;

                result.AlbumArt = artwork;
                result.IsArtworkLoaded = true;
            }
        }

        private ImageSource LoadSongArtwork(string filePath)
        {
            try
            {
                using (var tfile = TagLib.File.Create(filePath))
                {
                    if (tfile.Tag.Pictures != null &&
                        tfile.Tag.Pictures.Length > 0)
                    {
                        return LoadImage(tfile.Tag.Pictures[0].Data.Data);
                    }
                }
            }
            catch { }

            return null;
        }

        private SongResult LoadSongMetadata(SongResult fallback)
        {
            try
            {
                using (var tfile = TagLib.File.Create(fallback.FullPath))
                {
                    return new SongResult
                    {
                        Index = fallback.Index,
                        Title = !string.IsNullOrWhiteSpace(tfile.Tag.Title) ? tfile.Tag.Title : fallback.Title,
                        Artist = tfile.Tag.FirstAlbumArtist ?? tfile.Tag.FirstPerformer ?? "Unknown Artist",
                        Album = !string.IsNullOrWhiteSpace(tfile.Tag.Album) ? tfile.Tag.Album : "Unknown Album",
                        Duration = tfile.Properties.Duration.ToString(@"mm\:ss"),
                        FileName = fallback.FileName,
                        FullPath = fallback.FullPath,
                        FolderPath = fallback.FolderPath,
                        AlbumArt = tfile.Tag.Pictures != null && tfile.Tag.Pictures.Length > 0
                            ? LoadImage(tfile.Tag.Pictures[0].Data.Data)
                            : null,
                        IsMetadataLoaded = true
                    };
                }
            }
            catch
            {
                fallback.IsMetadataLoaded = true;
                return fallback;
            }
        }

        private static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            try
            {
                var image = new BitmapImage();
                using (var mem = new MemoryStream(imageData))
                {
                    image.BeginInit();
                    image.DecodePixelWidth = 50;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = mem;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch { return null; }
        }

        private async void DoSearch_Click(object sender, RoutedEventArgs e) => await PerformSearch();

        private async void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await PerformSearch();
        }

        private void ListResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListResults.SelectedItem is SongResult selectedSong)
            {
                main.JumpToSong(selectedSong.FullPath);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void SearchInput_KeyDown_1(object sender, KeyEventArgs e)
        {

        }
    }

    public class SongResult : INotifyPropertyChanged
    {
        private string _title;
        private string _artist;
        private string _album;
        private string _duration;
        private ImageSource _albumArt;
        public bool IsArtworkLoaded { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get; set; }
        public string Title { get { return _title; } set { if (_title != value) { _title = value; OnPropertyChanged(nameof(Title)); } } }
        public string Artist { get { return _artist; } set { if (_artist != value) { _artist = value; OnPropertyChanged(nameof(Artist)); } } }
        public string Album { get { return _album; } set { if (_album != value) { _album = value; OnPropertyChanged(nameof(Album)); } } }
        public string Duration { get { return _duration; } set { if (_duration != value) { _duration = value; OnPropertyChanged(nameof(Duration)); } } }
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string FolderPath { get; set; }
        public ImageSource AlbumArt { get { return _albumArt; } set { if (_albumArt != value) { _albumArt = value; OnPropertyChanged(nameof(AlbumArt)); } } }
        public bool IsMetadataLoaded { get; set; }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class SearchGroupResult
    {
        public string Name { get; set; }
        public string Subtitle { get; set; }
        public List<SongResult> Tracks { get; set; }
    }

}
