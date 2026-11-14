
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindowsShuffle.Models;

namespace WindowsShuffle.Classes
{
    public class NotesService
    {
        private readonly string _notesDirectory;
        private readonly string _notesFile;

        public NotesService()
        {
            _notesDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "Podmix");

            _notesFile = Path.Combine(
                _notesDirectory,
                "notes.json");
        }

        public List<Note> LoadNotes()
        {
            if (!File.Exists(_notesFile))
                return new List<Note>();

            string json = File.ReadAllText(_notesFile);

            List<Note> notes =
                JsonConvert.DeserializeObject<List<Note>>(json);

            if (notes == null)
                return new List<Note>();

            return notes
                .Where(n => n != null)
                .OrderByDescending(n => n.DateModified)
                .ToList();
        }

        public void SaveNotes(IEnumerable<Note> notes)
        {
            Directory.CreateDirectory(_notesDirectory);

            string json = JsonConvert.SerializeObject(
                notes,
                Formatting.Indented);

            string tempFile = _notesFile + ".tmp";

            File.WriteAllText(tempFile, json);

            if (File.Exists(_notesFile))
            {
                File.Replace(tempFile, _notesFile, null);
            }
            else
            {
                File.Move(tempFile, _notesFile);
            }
        }
    }
}

