
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsShuffle.Classes;
using WindowsShuffle.Models;

namespace WindowsShuffle.Views
{
    public partial class NotesView : UserControl
    {
        public event EventHandler BackRequested;

        private readonly NotesService _notesService;
        private List<Note> _notes;

        private Note _currentNote;
        private bool _isLoadingEditor;
        public bool IsEditorOpen
        {
            get
            {
                return EditorPanel.Visibility == Visibility.Visible;
            }
        }

        private readonly DispatcherTimer _saveTimer;

        public NotesView()
        {
            InitializeComponent();

            _notesService = new NotesService();
            _notes = new List<Note>();

            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(600);
            _saveTimer.Tick += SaveTimer_Tick;

            LoadNotes();
        }

        // LOAD NOTES

        private void LoadNotes()
        {
            try
            {
                _notes = _notesService.LoadNotes();

                RefreshNotesList();
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

        private void RefreshNotesList()
        {
            _notes = _notes
                .OrderByDescending(n => n.DateModified)
                .ToList();

            NotesList.ItemsSource = null;
            NotesList.ItemsSource = _notes;

            bool hasNotes = _notes.Count > 0;

            TxtEmpty.Visibility = hasNotes
                ? Visibility.Collapsed
                : Visibility.Visible;

            NotesScrollViewer.Visibility = hasNotes
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // CREATE NOTE

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            Note note = new Note();

            _notes.Insert(0, note);

            try
            {
                _notesService.SaveNotes(_notes);
            }
            catch (Exception ex)
            {
                _notes.Remove(note);

                MessageBox.Show(
                    "Could not create the note.\n\n" + ex.Message,
                    "Podmix Notes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            OpenEditor(note);
        }

        // OPEN EXISTING NOTE

        private void NoteItem_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
                return;

            Note note = button.DataContext as Note;

            if (note == null)
                return;

            OpenEditor(note);
        }

        // EDITOR

        private void OpenEditor(Note note)
        {
            _saveTimer.Stop();

            _currentNote = note;

            _isLoadingEditor = true;

            TxtNoteTitle.Text = note.Title ?? string.Empty;
            TxtNoteContent.Text = note.Content ?? string.Empty;

            TxtSaveStatus.Text = "Saved";
            TxtSaveStatus.Foreground =
                System.Windows.Media.Brushes.Gray;

            _isLoadingEditor = false;

            ListHeader.Visibility = Visibility.Collapsed;
            ListPanel.Visibility = Visibility.Collapsed;
            ListFooter.Visibility = Visibility.Collapsed;

            EditorHeader.Visibility = Visibility.Visible;
            EditorPanel.Visibility = Visibility.Visible;
            EditorFooter.Visibility = Visibility.Visible;

            TxtNoteTitle.Focus();
            TxtNoteTitle.SelectAll();
        }

        // AUTO SAVE

        private void NoteTextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_isLoadingEditor || _currentNote == null)
                return;

            TxtSaveStatus.Text = "Saving...";
            TxtSaveStatus.Foreground =
                System.Windows.Media.Brushes.LightGray;

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveTimer_Tick(object sender, EventArgs e)
        {
            _saveTimer.Stop();
            SaveCurrentNote();
        }

        private void SaveCurrentNote()
        {
            if (_currentNote == null)
                return;

            _currentNote.Title = TxtNoteTitle.Text.Trim();
            _currentNote.Content = TxtNoteContent.Text;
            _currentNote.DateModified = DateTime.Now;

            try
            {
                _notesService.SaveNotes(_notes);

                TxtSaveStatus.Text = "Saved";
                TxtSaveStatus.Foreground =
                    System.Windows.Media.Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                TxtSaveStatus.Text = "Save failed";
                TxtSaveStatus.Foreground =
                    System.Windows.Media.Brushes.IndianRed;

                System.Diagnostics.Debug.WriteLine(
                    "Notes save error: " + ex.Message);
            }
        }

        // RETURN TO NOTES LIST

        private void EditorBack_Click(
            object sender,
            RoutedEventArgs e)
        {
            _saveTimer.Stop();

            SaveCurrentNote();

            _currentNote = null;

            EditorHeader.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Collapsed;
            EditorFooter.Visibility = Visibility.Collapsed;

            ListHeader.Visibility = Visibility.Visible;
            ListPanel.Visibility = Visibility.Visible;
            ListFooter.Visibility = Visibility.Visible;

            RefreshNotesList();
        }

        // DELETE NOTE

        private void DeleteNote_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_currentNote == null)
                return;

            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to delete this note?",
                "Delete Note",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            _saveTimer.Stop();

            _notes.Remove(_currentNote);

            try
            {
                _notesService.SaveNotes(_notes);

                _currentNote = null;

                EditorHeader.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Collapsed;
                EditorFooter.Visibility = Visibility.Collapsed;

                ListHeader.Visibility = Visibility.Visible;
                ListPanel.Visibility = Visibility.Visible;
                ListFooter.Visibility = Visibility.Visible;

                RefreshNotesList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not delete the note.\n\n" + ex.Message,
                    "Podmix Notes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                LoadNotes();
            }
        }

        // BACK TO APPS

        private void Back_Click(
            object sender,
            RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
