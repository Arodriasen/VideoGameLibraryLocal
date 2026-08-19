using System.Windows;

namespace VideoGameLibrary.Views
{
    public partial class ShortcutsDialog : Window
    {
        public ShortcutsDialog()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
