
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WindowsShuffle.Classes;

namespace WindowsShuffle
{
    public partial class TagEditorWindow : Window
    {
        private readonly string _filePath;
        private readonly TagEditorService _tagEditorService;

        private byte[] _artworkData;
        private string _artworkMimeType;
        private bool _artworkChanged;

        public TagEditorWindow(
            string filePath,
            TagEditorService tagEditorService)
        {
            InitializeComponent();

            _filePath = filePath;
            _tagEditorService = tagEditorService;

            LoadTrackTags();
        }

        private void LoadTrackTags()
        {
            if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath))
            {
                TxtStatus.Text = "The selected music file could not be found.";
                return;
            }

            try
            {
                PendingMetadata pending;

                // Pending changes always take priority over disk tags.
                if (_tagEditorService.TryGetPendingMetadata(_filePath, out pending))
                {
                    PopulateFields(pending);
                    TxtStatus.Text = "Loaded your pending changes.";
                    return;
                }

                using (var file = TagLib.File.Create(_filePath))
                {
                    var tag = file.Tag;

                    TxtTitle.Text = tag.Title ?? string.Empty;

                    TxtArtist.Text = tag.Performers != null
                        ? string.Join(", ", tag.Performers)
                        : string.Empty;

                    TxtAlbum.Text = tag.Album ?? string.Empty;

                    TxtAlbumArtist.Text = tag.AlbumArtists != null
                        ? string.Join(", ", tag.AlbumArtists)
                        : string.Empty;

                    TxtGenre.Text = tag.Genres != null
                        ? string.Join(", ", tag.Genres)
                        : string.Empty;

                    TxtComposer.Text = tag.Composers != null
                        ? string.Join(", ", tag.Composers)
                        : string.Empty;

                    TxtYear.Text = tag.Year > 0
                        ? tag.Year.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                    TxtTrack.Text = tag.Track > 0
                        ? tag.Track.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                    TxtDisc.Text = tag.Disc > 0
                        ? tag.Disc.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                    TxtBpm.Text = tag.BeatsPerMinute > 0
                        ? tag.BeatsPerMinute.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;

                    TxtComment.Text = tag.Comment ?? string.Empty;
                    TxtLyrics.Text = tag.Lyrics ?? string.Empty;

                    LoadArtwork(tag.Pictures);

                    TxtStatus.Text = "Tags loaded successfully.";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Unable to read this file's metadata.";

                MessageBox.Show(
                    "Podmix couldn't read the tags from this file.\n\n" + ex.Message,
                    "Tag Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void PopulateFields(PendingMetadata metadata)
        {
            TxtTitle.Text = metadata.Title ?? string.Empty;
            TxtArtist.Text = metadata.Artist ?? string.Empty;
            TxtAlbum.Text = metadata.Album ?? string.Empty;
            TxtAlbumArtist.Text = metadata.AlbumArtist ?? string.Empty;
            TxtGenre.Text = metadata.Genre ?? string.Empty;
            TxtComposer.Text = metadata.Composer ?? string.Empty;

            TxtYear.Text = FormatNumber(metadata.Year);
            TxtTrack.Text = FormatNumber(metadata.Track);
            TxtDisc.Text = FormatNumber(metadata.Disc);
            TxtBpm.Text = FormatNumber(metadata.Bpm);

            TxtComment.Text = metadata.Comment ?? string.Empty;
            TxtLyrics.Text = metadata.Lyrics ?? string.Empty;

            _artworkData = metadata.ArtworkData;
            _artworkMimeType = metadata.ArtworkMimeType;
            _artworkChanged = metadata.ArtworkChanged;

            LoadArtwork(_artworkData);
        }

        private string FormatNumber(uint value)
        {
            return value > 0
                ? value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private uint ParseNumber(string value)
        {
            uint result;

            if (uint.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result))
            {
                return result;
            }

            return 0;
        }

        private void LoadArtwork(TagLib.IPicture[] pictures)
        {
            ImgArtwork.Source = null;
            _artworkData = null;
            _artworkMimeType = null;

            if (pictures == null || pictures.Length == 0)
            {
                TxtArtworkStatus.Text = "No artwork found";
                return;
            }

            try
            {
                _artworkData = pictures[0].Data.Data;
                _artworkMimeType = pictures[0].MimeType;

                LoadArtwork(_artworkData);
            }
            catch
            {
                ImgArtwork.Source = null;
                _artworkData = null;
                _artworkMimeType = null;
                TxtArtworkStatus.Text = "Artwork couldn't be loaded";
            }
        }

        private void LoadArtwork(byte[] imageData)
        {
            _artworkChanged = false;
            ImgArtwork.Source = null;

            if (imageData == null || imageData.Length == 0)
            {
                TxtArtworkStatus.Text = "No artwork found";
                return;
            }

            try
            {
                using (var stream = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ImgArtwork.Source = bitmap;
                }

                TxtArtworkStatus.Text = "Current cover";
            }
            catch
            {
                ImgArtwork.Source = null;
                TxtArtworkStatus.Text = "Artwork couldn't be loaded";
            }
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

        private void LookupOnline_Click(object sender, RoutedEventArgs e)
        {
            TagLookupWindow lookupWindow = new TagLookupWindow(
                TxtArtist.Text,
                TxtTitle.Text)
            {
                Owner = this
            };

            if (lookupWindow.ShowDialog() != true ||
                lookupWindow.SelectedMetadata == null)
            {
                return;
            }

            PendingMetadata metadata = lookupWindow.SelectedMetadata;

            TxtTitle.Text = metadata.Title;
            TxtArtist.Text = metadata.Artist;
            TxtAlbum.Text = metadata.Album;
            TxtAlbumArtist.Text = metadata.AlbumArtist;

            if (metadata.Year > 0)
                TxtYear.Text = metadata.Year.ToString();

            if (metadata.Track > 0)
                TxtTrack.Text = metadata.Track.ToString();

            if (metadata.Disc > 0)
                TxtDisc.Text = metadata.Disc.ToString();

            TxtStatus.Text = "Online tags loaded. Review before saving.";

            if (metadata.ArtworkChanged &&
                metadata.ArtworkData != null &&
                metadata.ArtworkData.Length > 0)
            {
                _artworkData = metadata.ArtworkData;
                _artworkMimeType = metadata.ArtworkMimeType;

                MessageBox.Show(
                    "Artwork received: " +
                    (_artworkData == null ? "NULL" : _artworkData.Length + " bytes"),
                    "Tag Editor Test");

                LoadArtwork(_artworkData);

                _artworkChanged = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ChangeArtwork_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Album Artwork",
                Filter =
                    "Image files (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|" +
                    "JPEG images (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                    "PNG images (*.png)|*.png|" +
                    "All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                byte[] imageData = File.ReadAllBytes(dialog.FileName);

                BitmapImage bitmap;

                using (var stream = new MemoryStream(imageData))
                {
                    bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                string extension = Path.GetExtension(dialog.FileName)
                    .ToLowerInvariant();

                string mimeType;

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        mimeType = "image/jpeg";
                        break;

                    case ".png":
                        mimeType = "image/png";
                        break;

                    case ".bmp":
                        mimeType = "image/bmp";
                        break;

                    case ".gif":
                        mimeType = "image/gif";
                        break;

                    case ".tif":
                    case ".tiff":
                        mimeType = "image/tiff";
                        break;

                    default:
                        throw new InvalidDataException("Unsupported image format.");
                }

                // Stage the new artwork in memory.
                _artworkData = imageData;
                _artworkMimeType = mimeType;
                _artworkChanged = true;

                // Update the preview immediately.
                ImgArtwork.Source = bitmap;

                TxtArtworkStatus.Text = "New cover selected";
                TxtStatus.Text = "New artwork staged. Click Save Changes to keep it.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Podmix couldn't load this image.\n\n" + ex.Message,
                    "Tag Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RemoveArtwork_Click(object sender, RoutedEventArgs e)
        {
            _artworkChanged = true;
            _artworkData = null;
            _artworkMimeType = null;
            ImgArtwork.Source = null;

            TxtArtworkStatus.Text = "Artwork will be removed";
            TxtStatus.Text = "Artwork removal staged. Click Save Changes to keep it.";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var metadata = new PendingMetadata
                {
                    Title = TxtTitle.Text,
                    Artist = TxtArtist.Text,
                    Album = TxtAlbum.Text,
                    AlbumArtist = TxtAlbumArtist.Text,
                    Genre = TxtGenre.Text,
                    Composer = TxtComposer.Text,

                    Year = ParseNumber(TxtYear.Text),
                    Track = ParseNumber(TxtTrack.Text),
                    Disc = ParseNumber(TxtDisc.Text),
                    Bpm = ParseNumber(TxtBpm.Text),

                    Comment = TxtComment.Text,
                    Lyrics = TxtLyrics.Text,

                    ArtworkData = _artworkData,
                    ArtworkMimeType = _artworkMimeType,
                    ArtworkChanged = _artworkChanged
                };

                _tagEditorService.StageMetadata(_filePath, metadata);

                TxtStatus.Text = "Changes saved temporarily.";

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Unable to stage metadata.";

                MessageBox.Show(
                    "Podmix couldn't save these changes temporarily.\n\n" + ex.Message,
                    "Tag Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
