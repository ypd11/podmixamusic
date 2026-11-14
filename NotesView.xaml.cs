
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WindowsShuffle;
using WindowsShuffle.Classes;
using WindowsShuffle.Models;

namespace WindowsShuffle.Views
{
    public partial class NotesView : UserControl
    {
        public event EventHandler BackRequested;
        public event EventHandler NewNoteRequested;

        private readonly NotesService _notesService;
        private List<Note> _notes;

        public NotesView()
        {
            InitializeComponent();

            _notesService = new NotesService();
            _notes = new List<Note>();

            LoadNotes();
        }

        private void LoadNotes()
        {
            try
            {
                _notes = _notesService.LoadNotes();

                NotesList.ItemsSource = _notes;

                bool hasNotes = _notes.Count > 0;

                TxtEmpty.Visibility = hasNotes
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                NotesScrollViewer.Visibility = hasNotes
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not load your notes.\n\n" + ex.Message,
                    "Podmix Notes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            NewNoteRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
