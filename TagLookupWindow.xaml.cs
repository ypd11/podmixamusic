
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace WindowsShuffle
{
    public partial class TagLookupWindow : Window
    {
        private readonly MusicBrainzService _musicBrainzService;

        public TagLookupWindow(string initialSearchText)
        {
            InitializeComponent();

            _musicBrainzService = new MusicBrainzService();

            TxtSearch.Text = initialSearchText ?? string.Empty;
            TxtSearch.SelectAll();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            string query = TxtSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                TxtLookupStatus.Text = "Enter a track, artist, or album to search.";
                TxtSearch.Focus();
                return;
            }

            BtnSearch.IsEnabled = false;
            TxtLookupStatus.Text = "Searching MusicBrainz...";
            LstResults.Items.Clear();

            try
            {
                JArray recordings =
                    await _musicBrainzService.SearchRecordingsAsync(query);

                if (recordings.Count == 0)
                {
                    AddEmptyMessage("No matching recordings found.");
                    TxtLookupStatus.Text = "No results found.";
                    return;
                }

                foreach (JObject recording in recordings)
                {
                    string title = recording["title"] != null
                        ? recording["title"].ToString()
                        : "Unknown title";

                    string artist = GetArtistName(recording);

                    string displayText = title + "\n" + artist;

                    ListBoxItem item = new ListBoxItem
                    {
                        Content = displayText,
                        Tag = recording,
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 2, 0, 2),
                        Foreground = System.Windows.Media.Brushes.White
                    };

                    LstResults.Items.Add(item);
                }

                TxtLookupStatus.Text =
                    "Found " + recordings.Count + " matching recordings.";
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

        private string GetArtistName(JObject recording)
        {
            JArray artistCredits = recording["artist-credit"] as JArray;

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
                Foreground = System.Windows.Media.Brushes.Gray,
                Padding = new Thickness(8)
            };

            LstResults.Items.Add(item);
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Search_Click(sender, new RoutedEventArgs());
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
