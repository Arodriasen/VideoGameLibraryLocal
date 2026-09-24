using System.Windows;
using VideoGameLibrary.Presentation.ViewModels;

namespace VideoGameLibrary.Presentation.Views
{
    public partial class GameDetailDialog : Window
    {
        public GameDetailDialog(GameViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
