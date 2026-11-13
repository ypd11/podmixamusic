
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using System.Windows.Media.Imaging;
using WindowsShuffle.Classes;

namespace WindowsShuffle
{
    public partial class TagLookupWindow : Window
    {
        private readonly MusicBrainzService _musicBrainzService;
        private static readonly HttpClient ArtworkClient = new HttpClient();
        public PendingMetadata SelectedMetadata { get; private set; }

        private class TrackSelection
        {
            public JObject Track { get; set; }
            public uint Disc { get; set; }
        }

        // Keep compatibility with the existing TagEditorWindow call.
        public TagLookupWindow(string initialSearchText)
            : this(string.Empty, initialSearchText)
        {
        }

        public TagLookupWindow(string initialArtist, string initialTitle)
        {
            InitializeComponent();

            _musicBrainzService = new MusicBrainzService();

            TxtArtist.Text = initialArtist ?? string.Empty;
            TxtTitle.Text = initialTitle ?? string.Empty;

            TxtTitle.SelectAll();
        }


        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            string artist = TxtArtist.Text.Trim();
            string title = TxtTitle.Text.Trim();

            if (string.IsNullOrWhiteSpace(artist) ||
                string.IsNullOrWhiteSpace(title))
            {
                TxtLookupStatus.Text =
                    "Enter both an artist and a track title.";

                if (string.IsNullOrWhiteSpace(artist))
                    TxtArtist.Focus();
                else
                    TxtTitle.Focus();

                return;
            }

            BtnSearch.IsEnabled = false;
            TxtLookupStatus.Text = "Searching MusicBrainz...";
            LstResults.Items.Clear();

            try
            {
                JArray releases =
                    await _musicBrainzService.SearchAlbumReleasesAsync(
                        artist, title);

                if (releases == null || releases.Count == 0)
                {
                    AddEmptyMessage("No matching album releases found.");
                    TxtLookupStatus.Text = "No results found.";
                    return;
                }

                foreach (JObject release in releases)
                {
                    string album = release["title"] != null
                        ? release["title"].ToString()
                        : "Unknown album";

                    string releaseArtist = GetArtistName(release);

                    if (releaseArtist == "Unknown artist" &&
                        release["artist-credit"] == null)
                    {
                        releaseArtist = artist;
                    }

                    string year = string.Empty;

                    if (release["date"] != null)
                    {
                        string date = release["date"].ToString();
                        year = date.Length >= 4
                            ? date.Substring(0, 4)
                            : date;
                    }

                    string artistYear = releaseArtist;

                    if (!string.IsNullOrWhiteSpace(year))
                        artistYear += "  •  " + year;

                    // Artwork thumbnail
                    Image artwork = new Image
                    {
                        Width = 54,
                        Height = 54,
                        Stretch = Stretch.UniformToFill
                    };

                    Border artworkBorder = new Border
                    {
                        Width = 54,
                        Height = 54,
                        Background = new SolidColorBrush(
                            Color.FromRgb(43, 43, 45)),
                        BorderBrush = new SolidColorBrush(
                            Color.FromRgb(65, 65, 69)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        ClipToBounds = true,
                        Child = artwork
                    };

                    // Album information
                    StackPanel textPanel = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(11, 0, 0, 0)
                    };

                    TextBlock albumText = new TextBlock
                    {
                        Text = album,
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    TextBlock artistText = new TextBlock
                    {
                        Text = artistYear,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(
                            Color.FromRgb(155, 155, 160)),
                        Margin = new Thickness(0, 4, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };

                    textPanel.Children.Add(albumText);
                    textPanel.Children.Add(artistText);

                    StackPanel row = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    row.Children.Add(artworkBorder);
                    row.Children.Add(textPanel);

                    ListBoxItem item = new ListBoxItem
                    {
                        Content = row,
                        Tag = release,
                        Padding = new Thickness(7),
                        Margin = new Thickness(0, 2, 0, 2),
                        Foreground = Brushes.White
                    };

                    LstResults.Items.Add(item);

                    // Load artwork asynchronously.
                    string releaseId = release["id"] != null
                        ? release["id"].ToString()
                        : string.Empty;

                    _ = LoadResultArtworkAsync(releaseId, artwork);
                }

                TxtLookupStatus.Text =
                    "Found " + releases.Count + " album releases.";
            }
            catch (HttpRequestException)
            {
                TxtLookupStatus.Text =
                    "Unable to connect to MusicBrainz. Check your internet connection.";

                AddEmptyMessage("Search failed. Please try again.");
            }
            catch (TaskCanceledException)
            {
                TxtLookupStatus.Text = "The search request timed out.";
                AddEmptyMessage("Request timed out. Please try again.");
            }
            catch (Exception ex)
            {
                TxtLookupStatus.Text = "An unexpected error occurred.";
                AddEmptyMessage(ex.Message);
            }
            finally
            {
                BtnSearch.IsEnabled = true;
            }
        }

        private async void LstResults_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            ListBoxItem selectedItem = LstResults.SelectedItem as ListBoxItem;

            if (selectedItem == null || !(selectedItem.Tag is JObject))
                return;

            JObject release = (JObject)selectedItem.Tag;

            string releaseId = release["id"] != null
                ? release["id"].ToString()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(releaseId))
                return;

            TxtLookupStatus.Text = "Loading album tracks...";
            LstPreviewTracks.Items.Clear();

            try
            {
                JObject details =
                    await _musicBrainzService.GetReleaseDetailsAsync(releaseId);

                JArray media = details["media"] as JArray;

                if (media == null || media.Count == 0)
                {
                    TxtLookupStatus.Text = "No track listing available.";
                    return;
                }

                string matchedTitle = release["matched-recording-title"] != null
                    ? release["matched-recording-title"].ToString()
                    : TxtTitle.Text.Trim();

                foreach (JObject medium in media)
                {
                    JArray tracks = medium["tracks"] as JArray;

                    if (tracks == null)
                        continue;

                    foreach (JObject track in tracks)
                    {
                        string trackTitle = track["title"] != null
                            ? track["title"].ToString()
                            : "Unknown track";

                        string position = track["number"] != null
                            ? track["number"].ToString()
                            : string.Empty;

                        bool isMatch = string.Equals(
                            trackTitle.Trim(),
                            matchedTitle.Trim(),
                            StringComparison.OrdinalIgnoreCase);

                        uint discNumber = 1;

                        if (medium["position"] != null)
                        {
                            uint.TryParse(medium["position"].ToString(), out discNumber);
                        }

                        ListBoxItem trackItem = new ListBoxItem
                        {
                            Content = position + "  " + trackTitle,
                            Tag = new TrackSelection
                            {
                                Track = track,
                                Disc = discNumber
                            },
                            Padding = new Thickness(5, 3, 5, 3),
                            Foreground = isMatch
                                ? Brushes.DeepSkyBlue
                                : Brushes.LightGray,
                            FontWeight = isMatch
                                ? FontWeights.SemiBold
                                : FontWeights.Normal,
                            IsSelected = isMatch
                        };

                        LstPreviewTracks.Items.Add(trackItem);
                    }
                }

                TxtLookupStatus.Text =
                    "Loaded " + LstPreviewTracks.Items.Count + " tracks.";
            }
            catch (Exception ex)
            {
                TxtLookupStatus.Text =
                    "Unable to load album tracks: " + ex.Message;
            }
        }

        private uint ParseUInt(string value)
        {
            uint result;

            return uint.TryParse(value, out result)
                ? result
                : 0;
        }

        private void LstPreviewTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnApplyTags.IsEnabled =
                LstPreviewTracks.SelectedItem is ListBoxItem &&
                ((ListBoxItem)LstPreviewTracks.SelectedItem).Tag is TrackSelection;
        }

        private async Task LoadResultArtworkAsync(
            string releaseId,
            Image targetImage)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
                return;

            string artworkUrl =
                "https://coverartarchive.org/release/" +
                Uri.EscapeDataString(releaseId) +
                "/front-250";

            try
            {
                byte[] imageData =
                    await ArtworkClient.GetByteArrayAsync(artworkUrl);

                BitmapImage bitmap = new BitmapImage();

                using (System.IO.MemoryStream stream =
                    new System.IO.MemoryStream(imageData))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                targetImage.Source = bitmap;
            }
            catch
            {
                // Keep the dark placeholder if artwork is unavailable.
            }
        }

        private string GetArtistName(JObject item)
        {
            JArray artistCredits = item["artist-credit"] as JArray;

            if (artistCredits == null || artistCredits.Count == 0)
                return "Unknown artist";

            string artistName = string.Empty;

            foreach (JToken credit in artistCredits)
            {
                if (credit.Type == JTokenType.Object)
                {
                    JToken name = credit["name"];

                    if (name == null && credit["artist"] != null)
                        name = credit["artist"]["name"];

                    if (name != null)
                        artistName += name.ToString();
                }
                else if (credit.Type == JTokenType.String)
                {
                    artistName += credit.ToString();
                }
            }

            return string.IsNullOrWhiteSpace(artistName)
                ? "Unknown artist"
                : artistName;
        }

        private void AddEmptyMessage(string message)
        {
            LstResults.Items.Clear();

            ListBoxItem item = new ListBoxItem
            {
                Content = message,
                IsEnabled = false,
                Foreground = Brushes.Gray,
                Padding = new Thickness(8)
            };

            LstResults.Items.Add(item);
        }

        private async Task<byte[]> DownloadArtworkAsync(string releaseId)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
                return null;

            string artworkUrl =
                "https://coverartarchive.org/release/" +
                Uri.EscapeDataString(releaseId) +
                "/front";

            try
            {
                return await ArtworkClient.GetByteArrayAsync(artworkUrl);
            }
            catch
            {
                return null;
            }
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Search_Click(sender, new RoutedEventArgs());
        }

        private async void ApplyTags_Click(object sender, RoutedEventArgs e)
        {
            ListBoxItem albumItem =
                LstResults.SelectedItem as ListBoxItem;

            ListBoxItem trackItem =
                LstPreviewTracks.SelectedItem as ListBoxItem;

            if (albumItem == null || trackItem == null)
                return;

            JObject release = albumItem.Tag as JObject;
            TrackSelection selection = trackItem.Tag as TrackSelection;

            if (release == null || selection == null)
                return;

            JObject track = selection.Track;

            string title = track["title"] != null
                ? track["title"].ToString()
                : string.Empty;

            string artist = GetArtistName(track);

            if (artist == "Unknown artist" &&
                track["recording"] != null)
            {
                artist = GetArtistName(
                    track["recording"] as JObject);
            }

            string albumArtist = GetArtistName(release);

            if (artist == "Unknown artist")
                artist = albumArtist;

            string album = release["title"] != null
                ? release["title"].ToString()
                : string.Empty;

            string year = release["date"] != null
                ? release["date"].ToString()
                : string.Empty;

            string trackNumber = track["number"] != null
                ? track["number"].ToString()
                : string.Empty;

            SelectedMetadata = new PendingMetadata
            {
                Title = title,
                Artist = artist,
                Album = album,
                AlbumArtist = albumArtist,
                Year = ParseUInt(
                    year.Length >= 4 ? year.Substring(0, 4) : year),
                Track = ParseUInt(trackNumber),
                Disc = selection.Disc
            };

            DialogResult = true;
            string releaseId = release["id"] != null
                ? release["id"].ToString()
                : string.Empty;

            byte[] artwork = await DownloadArtworkAsync(releaseId);
            MessageBox.Show(
    artwork == null
        ? "Artwork download failed or no cover is available."
        : "Artwork downloaded successfully: " + artwork.Length + " bytes.",
    "Artwork Test");

            if (artwork != null && artwork.Length > 0)
            {
                SelectedMetadata.ArtworkData = artwork;
                SelectedMetadata.ArtworkMimeType = "image/jpeg";
                SelectedMetadata.ArtworkChanged = true;
            }

            Close();
        }

        private void Window_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag interruptions.
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
