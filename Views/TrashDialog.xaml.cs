using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VideoGameLibrary.Models;
using VideoGameLibrary.Services;

namespace VideoGameLibrary.Views
{
    public partial class TrashDialog : Window
    {
        // Se marca cuando algo cambia (restaurar / eliminar) para que MainWindow sepa si debe recargar
        public bool Changed { get; private set; }

        public TrashDialog()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            var trash = await App.Repository.GetTrashAsync();
            ItemsList.ItemsSource = trash;
            TxtEmpty.Visibility = trash.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnEmptyTrash.IsEnabled = trash.Count > 0;
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not Game game) return;

            try
            {
                await App.Repository.RestoreAsync(game.Id);
                Changed = true;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Restaurar juego de la papelera \"{game.Title}\"", ex);
                await AppDialogService.ShowErrorAsync("TrashDialogHost", $"No se ha podido restaurar el juego:\n{ex.Message}");
            }
        }

        private async void BtnDeleteForever_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not Game game) return;

            var confirmed = await AppDialogService.ShowConfirmAsync("TrashDialogHost",
                $"¿Eliminar \"{game.Title}\" definitivamente? Esta acción no se puede deshacer.",
                "Eliminar definitivamente");
            if (!confirmed) return;

            try
            {
                await App.Repository.PermanentlyDeleteAsync(game.Id);
                Changed = true;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Eliminar definitivamente \"{game.Title}\"", ex);
                await AppDialogService.ShowErrorAsync("TrashDialogHost", $"No se ha podido eliminar el juego:\n{ex.Message}");
            }
        }

        private async void BtnEmptyTrash_Click(object sender, RoutedEventArgs e)
        {
            var trash = await App.Repository.GetTrashAsync();
            if (trash.Count == 0) return;

            var confirmed = await AppDialogService.ShowConfirmAsync("TrashDialogHost",
                $"¿Vaciar la papelera? Se eliminarán definitivamente {trash.Count} juego(s). Esta acción no se puede deshacer.",
                "Vaciar papelera");
            if (!confirmed) return;

            try
            {
                foreach (var game in trash)
                    await App.Repository.PermanentlyDeleteAsync(game.Id);

                Changed = true;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Vaciar papelera", ex);
                await AppDialogService.ShowErrorAsync("TrashDialogHost", $"No se ha podido vaciar la papelera:\n{ex.Message}");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
