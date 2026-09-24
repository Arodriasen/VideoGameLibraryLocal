using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VideoGameLibrary.Presentation.ViewModels;

namespace VideoGameLibrary.Presentation.Views
{
    public partial class CalendarDialog : Window
    {
        private CalendarViewModel Vm => (CalendarViewModel)DataContext;

        public bool Changed => Vm.HasChanges;

        public CalendarDialog(CalendarViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Loaded += async (_, _) => await vm.InitializeAsync();
        }

        private void DayCell_Click(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is CalendarDayCell cell)
                Vm.SelectDayCommand.Execute(cell);
        }

        private async void BtnToggleWishlist_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is UpcomingReleaseItem item)
                await Vm.ToggleWishlistAsync(item);
        }

        private async void BtnToggleCollection_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is UpcomingReleaseItem item)
                await Vm.ToggleCollectionAsync(item);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
