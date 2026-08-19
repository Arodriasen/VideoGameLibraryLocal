using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.Views
{
    public partial class GameCandidatePickerDialog : Window
    {
        public Game? SelectedGame { get; private set; }

        public GameCandidatePickerDialog(List<Game> candidates)
        {
            InitializeComponent();
            ItemsList.ItemsSource = candidates;
        }

        private void Candidate_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SelectedGame = (Game)((FrameworkElement)sender).Tag;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
