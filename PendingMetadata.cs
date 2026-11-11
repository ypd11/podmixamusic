
namespace WindowsShuffle.Classes
{
    public class PendingMetadata
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string AlbumArtist { get; set; }
        public string Genre { get; set; }
        public string Composer { get; set; }

        public uint Year { get; set; }
        public uint Track { get; set; }
        public uint Disc { get; set; }
        public uint Bpm { get; set; }

        public string Comment { get; set; }
        public string Lyrics { get; set; }

        // Album artwork stored in memory.
        public byte[] ArtworkData { get; set; }
        public string ArtworkMimeType { get; set; }

        public PendingMetadata()
        {
            Title = string.Empty;
            Artist = string.Empty;
            Album = string.Empty;
            AlbumArtist = string.Empty;
            Genre = string.Empty;
            Composer = string.Empty;

            Comment = string.Empty;
            Lyrics = string.Empty;

            ArtworkData = null;
            ArtworkMimeType = null;
        }
    }
}

