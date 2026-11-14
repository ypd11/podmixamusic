
using System;

namespace WindowsShuffle.Models
{
    public class Note
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        public string Content { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public Note()
        {
            Id = Guid.NewGuid();
            Title = "Untitled";
            Content = string.Empty;
            DateCreated = DateTime.Now;
            DateModified = DateTime.Now;
        }
    }
}

