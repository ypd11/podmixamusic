using System;
using System.Windows;
using System.Windows.Input;

namespace WindowsShuffle
{
    public partial class TagLookupWindow : Window
    {
        public TagLookupWindow(string initialSearchText)
        {
            InitializeComponent();

            TxtSearch.Text = initialSearchText ?? string.Empty;
            TxtSearch.SelectAll();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                TxtLookupStatus.Text = "Enter a track, artist, or album to search.";
                TxtSearch.Focus();
                return;
            }

            // The online search service will be connected in the next step.
            TxtLookupStatus.Text = "Search interface ready. Online search will be connected next.";
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
