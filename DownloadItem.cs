using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace WindowsShuffle
{
    public class DownloadItem : INotifyPropertyChanged
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string ThumbnailUrl { get; set; }
        public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();

        // Playlist specific
        public bool IsPlaylist { get; set; }
        public List<SearchResultItem> PlaylistItems { get; set; } = new List<SearchResultItem>();

        private int _completedTracks;
        public int CompletedTracks
        {
            get => _completedTracks;
            set { _completedTracks = value; OnPropertyChanged(); }
        }

        public int TotalTracks => IsPlaylist ? PlaylistItems.Count : 1;

        private string _statusText = "Waiting for start...";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private bool _showDownloadButton = true;
        public bool ShowDownloadButton
        {
            get => _showDownloadButton;
            set { _showDownloadButton = value; OnPropertyChanged(); }
        }

        public bool IsCompleted { get; set; }
        public bool IsDownloading { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
