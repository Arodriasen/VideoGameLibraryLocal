using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using VideoGameLibrary.Models;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Views
{
    public partial class GameEditDialog : Window
    {
        private GameEditViewModel Vm => (GameEditViewModel)DataContext;

        public GameEditDialog(GameEditViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseDialog = () => DialogResult = vm.Saved;
            vm.PickCandidate = ShowCandidatePickerAsync;
            Loaded += (_, _) => TxtBarcode.Focus();
            Closed += (_, _) => vm.CancelPendingOperations();
        }

        private Task<Game?> ShowCandidatePickerAsync(List<Game> candidates)
        {
            var dialog = new GameCandidatePickerDialog(candidates) { Owner = this };
            var result = dialog.ShowDialog();
            return Task.FromResult(result == true ? dialog.SelectedGame : null);
        }

        private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Vm.SearchCommand.Execute(null);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnChangeCover_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Seleccionar portada"
            };
            if (dlg.ShowDialog() == true)
            {
                Vm.CoverData = File.ReadAllBytes(dlg.FileName);
            }
        }
    }
}
