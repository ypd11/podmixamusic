
using System;
using System.Windows;
using System.Windows.Controls;

namespace WindowsShuffle.Views
{
    public partial class NotesView : UserControl
    {
        public event EventHandler BackRequested;

        public NotesView()
        {
            InitializeComponent();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
