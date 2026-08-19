using System.Windows;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Views
{
    public partial class StatsDialog : Window
    {
        public StatsDialog(StatsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Loaded += async (_, _) => await vm.LoadAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
