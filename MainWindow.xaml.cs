using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using VideoGameLibrary.Models;
using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;
using VideoGameLibrary.Views;

namespace VideoGameLibrary
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            UpdateThemeIcon();
            ApplyFabColors();
            vm.PickCandidate = ShowCandidatePickerAsync;
            Loaded += async (_, _) =>
            {
                await vm.LoadGamesAsync();
                TxtQuickScan.Focus();
            };
        }

        // Se lee el color real del tema (el mismo que usa la barra superior) en vez de una clave
        // de recurso, para garantizar que el botón flotante coincide exactamente con el header.
        private void ApplyFabColors()
        {
            var primaryMid = new PaletteHelper().GetTheme().PrimaryMid.Color;
            var brush = new SolidColorBrush(primaryMid);

            FabMain.Background = brush;
            FabTrash.Background = brush;
            FabStats.Background = brush;
            FabSettings.Background = brush;
            FabWishlist.Background = brush;
            FabShortcuts.Background = brush;
        }

        private Task<Game?> ShowCandidatePickerAsync(List<Game> candidates)
        {
            var dialog = new GameCandidatePickerDialog(candidates) { Owner = this };
            var result = dialog.ShowDialog();
            return Task.FromResult(result == true ? dialog.SelectedGame : null);
        }

        private async void TxtQuickScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            await Vm.QuickAddByBarcodeAsync();
            TxtQuickScan.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TxtSearch.Focus();
                TxtSearch.SelectAll();
                e.Handled = true;
            }
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            App.ToggleTheme();
            UpdateThemeIcon();
        }

        private void UpdateThemeIcon()
        {
            ThemeIcon.Kind = App.IsDarkTheme ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
        }

        private async void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var repoBefore = App.Repository;
            var dialog = new SettingsDialog { Owner = this };
            dialog.ShowDialog();

            if (!ReferenceEquals(App.Repository, repoBefore))
            {
                // Se ha cambiado de colección: hace falta un ViewModel nuevo apuntando al repositorio nuevo
                var newVm = new MainViewModel(App.Repository, App.ApiService) { PickCandidate = ShowCandidatePickerAsync };
                DataContext = newVm;
            }

            await Vm.LoadGamesAsync(); // recarga: por el cambio de colección, o por una importación desde Ajustes
        }

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LogViewerDialog { Owner = this };
            dialog.ShowDialog();
        }

        private async void BtnTrash_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TrashDialog { Owner = this };
            dialog.ShowDialog();

            if (dialog.Changed)
                await Vm.LoadGamesAsync(); // por si se ha restaurado algo
        }

        private void BtnStats_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new StatsDialog(new StatsViewModel(App.Repository)) { Owner = this };
            dialog.ShowDialog();
        }

        private void FabToggle_Click(object sender, MouseButtonEventArgs e)
        {
            Vm.IsFabOpen = !Vm.IsFabOpen;
        }

        private void FabOption_Click(object sender, MouseButtonEventArgs e)
        {
            Vm.IsFabOpen = false;

            switch (((FrameworkElement)sender).Tag as string)
            {
                case "wishlist":
                    Vm.ToggleWishlistViewCommand.Execute(null);
                    break;
                case "stats":
                    BtnStats_Click(sender, e);
                    break;
                case "trash":
                    BtnTrash_Click(sender, e);
                    break;
                case "settings":
                    BtnSettings_Click(sender, e);
                    break;
                case "shortcuts":
                    new ShortcutsDialog { Owner = this }.ShowDialog();
                    break;
            }
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var editVm = App.GetEditViewModel();
            editVm.IsWishlist = Vm.IsWishlistView; // añadir desde la lista de deseos marca el juego como deseado
            var dialog = new GameEditDialog(editVm) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                var game = editVm.ToGame();
                try
                {
                    await Vm.SaveGameAsync(game);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                {
                    LoggingService.LogError("Guardar juego — código de barras duplicado", ex);
                    await AppDialogService.ShowWarningAsync("MainDialogHost",
                        "Ya existe un juego con ese código de barras (puede estar en la papelera). Revisa la papelera o usa otro código.",
                        "Código de barras duplicado");
                }
            }
        }

        private void BtnView_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not ViewModels.GameViewModel gvm) return;

            var dialog = new GameDetailDialog(gvm) { Owner = this };
            dialog.ShowDialog();
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not ViewModels.GameViewModel gvm) return;

            var editVm = App.GetEditViewModel();
            editVm.LoadFromGame(gvm.ToModel());
            var dialog = new GameEditDialog(editVm) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                var game = editVm.ToGame();
                game.Id = gvm.Id;
                await Vm.SaveGameAsync(game);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not ViewModels.GameViewModel gvm) return;

            try
            {
                await Vm.DeleteGameAsync(gvm);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Eliminar juego \"{gvm.Title}\"", ex);
                await AppDialogService.ShowErrorAsync("MainDialogHost", $"Error al eliminar el juego:\n{ex.Message}");
            }
        }

        private async void BtnMoveToCollection_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is not ViewModels.GameViewModel gvm) return;

            try
            {
                await Vm.MoveToCollectionAsync(gvm);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Mover a la colección \"{gvm.Title}\"", ex);
                await AppDialogService.ShowErrorAsync("MainDialogHost", $"Error al mover el juego a la colección:\n{ex.Message}");
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e) => ShowExportMenu(sender, selectionOnly: false);

        private void BtnExportSelected_Click(object sender, RoutedEventArgs e) => ShowExportMenu(sender, selectionOnly: true);

        private void ShowExportMenu(object sender, bool selectionOnly)
        {
            var menu = new ContextMenu();

            var itemExcel = new MenuItem { Header = "Exportar a Excel (.xlsx)" };
            itemExcel.Click += async (_, _) => await ExportAsync("xlsx", selectionOnly);

            var itemCsv = new MenuItem { Header = "Exportar a CSV" };
            itemCsv.Click += async (_, _) => await ExportAsync("csv", selectionOnly);

            menu.Items.Add(itemExcel);
            menu.Items.Add(itemCsv);
            menu.PlacementTarget = (Button)sender;
            menu.IsOpen = true;
        }

        private async System.Threading.Tasks.Task ExportAsync(string format, bool selectionOnly)
        {
            var dlg = new SaveFileDialog();

            if (format == "xlsx")
            {
                dlg.Filter = "Excel (*.xlsx)|*.xlsx";
                dlg.FileName = "coleccion.xlsx";
            }
            else
            {
                dlg.Filter = "CSV (*.csv)|*.csv";
                dlg.FileName = "coleccion.csv";
            }

            if (dlg.ShowDialog() != true) return;

            var games = selectionOnly
                ? Vm.Games.Where(g => g.IsSelected).Select(g => g.ToModel()).ToList()
                : (await App.Repository.GetAllAsync()).Where(g => g.IsWishlist == Vm.IsWishlistView).ToList();

            var exporter = new Services.ExportService();

            if (format == "xlsx")
                exporter.ExportToExcel(games, dlg.FileName);
            else
                exporter.ExportToCsv(games, dlg.FileName);

            await AppDialogService.ShowInfoAsync("MainDialogHost", $"Exportación completada: {dlg.FileName}", "Exportar");
        }

        private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (Vm.SelectedCount == 0) return;

            try
            {
                // Borrado suave, igual que un juego individual: van a la papelera (7 días) y se
                // pueden deshacer desde el propio snackbar, sin diálogo de confirmación bloqueante.
                var ids = await Vm.DeleteSelectedAsync();
                Vm.SnackbarMessageQueue.Enqueue($"{ids.Count} juego(s) eliminado(s).", "DESHACER",
                    async () => await Vm.UndoDeleteSelectedAsync(ids));
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Eliminar juegos seleccionados", ex);
                await AppDialogService.ShowErrorAsync("MainDialogHost", $"Error al eliminar los juegos:\n{ex.Message}");
            }
        }
    }
}
