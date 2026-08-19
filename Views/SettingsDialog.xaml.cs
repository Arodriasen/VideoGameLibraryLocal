using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Views
{
    public partial class SettingsDialog : Window
    {
        // A partir de este número de días sin copia de seguridad, el aviso se muestra en naranja
        private const int BackupWarningDays = 30;

        public SettingsDialog(bool firstRun = false)
        {
            InitializeComponent();

            var config = App.LoadConfig();
            TxtScanDex.Text = config.ScanDexToken;
            TxtIgdbClientId.Text = config.IgdbClientId;
            TxtIgdbClientSecret.Text = config.IgdbClientSecret;
            TxtRawg.Text = config.RawgApiKey;
            TxtGamesDb.Text = config.TheGamesDbApiKey;

            if (firstRun)
            {
                TxtIntro.Text = "Bienvenido a Mi Colección de Juegos. Antes de empezar, puedes introducir tus claves de API " +
                                 "para que el escaneo de códigos de barras encuentre título, portada y datos automáticamente. " +
                                 "Son opcionales: puedes dejarlas en blanco y añadirlas más tarde desde Ajustes.";
                BtnCancel.Content = "OMITIR POR AHORA";

                // Aún no hay ninguna base de datos abierta en este punto del arranque
                SwitchDbSeparator.Visibility = Visibility.Collapsed;
                SwitchDbSection.Visibility = Visibility.Collapsed;
                MaintenanceSeparator.Visibility = Visibility.Collapsed;
                MaintenanceSection.Visibility = Visibility.Collapsed;
                ImportSeparator.Visibility = Visibility.Collapsed;
                ImportSection.Visibility = Visibility.Collapsed;
            }
            else
            {
                Loaded += async (_, _) => TxtCollectionName.Text = await App.Repository.GetCollectionNameAsync();
                UpdateLastBackupText(config.LastBackupUtc);
            }
        }

        private void UpdateLastBackupText(DateTime? lastBackupUtc)
        {
            if (lastBackupUtc == null)
            {
                TxtLastBackup.Text = "Todavía no se ha guardado ninguna copia de seguridad.";
                TxtLastBackup.Foreground = Brushes.OrangeRed;
                return;
            }

            var local = lastBackupUtc.Value.ToLocalTime();
            var days = (int)(DateTime.Now.Date - local.Date).TotalDays;
            var when = days switch
            {
                0 => "hoy",
                1 => "hace 1 día",
                _ => $"hace {days} días"
            };

            TxtLastBackup.Text = $"Última copia de seguridad: {when} ({local:dd/MM/yyyy}).";
            TxtLastBackup.Foreground = days > BackupWarningDays
                ? Brushes.OrangeRed
                : (Brush)FindResource("MaterialDesignBodyLight");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            App.SaveApiKeys(
                TxtScanDex.Text.Trim(),
                TxtIgdbClientId.Text.Trim(),
                TxtIgdbClientSecret.Text.Trim(),
                TxtRawg.Text.Trim(),
                TxtGamesDb.Text.Trim());

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private async void BtnSaveCollectionName_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await App.Repository.SetCollectionNameAsync(TxtCollectionName.Text.Trim());
                DialogResult = true; // cierra Ajustes; MainWindow recarga y refresca el título
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Guardar nombre de la colección", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido guardar el nombre:\n{ex.Message}");
            }
        }

        private async void BtnRenameDbFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Renombrar archivo de la colección",
                Filter = "Base de datos (*.db)|*.db",
                DefaultExt = ".db",
                FileName = Path.GetFileNameWithoutExtension(App.CurrentDatabasePath),
                InitialDirectory = Path.GetDirectoryName(App.CurrentDatabasePath)
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var newPath = await App.RenameDatabaseFileAsync(dlg.FileName);
                if (newPath != null)
                {
                    await AppDialogService.ShowInfoAsync("SettingsDialogHost", $"Archivo renombrado a:\n{newPath}", "Renombrar archivo");
                    DialogResult = true; // cierra Ajustes; MainWindow detecta el cambio de repositorio y recarga
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Renombrar archivo .db", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido renombrar el archivo:\n{ex.Message}");
            }
        }

        private async void BtnSwitchDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await App.SwitchDatabaseInteractiveAsync())
                    DialogResult = true; // cierra Ajustes; MainWindow detecta el cambio y recarga
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Cambiar de colección", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido abrir esa colección:\n{ex.Message}");
            }
        }

        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Guardar copia de seguridad",
                Filter = "Base de datos (*.db)|*.db",
                DefaultExt = ".db",
                FileName = $"{Path.GetFileNameWithoutExtension(App.CurrentDatabasePath)}_backup_{DateTime.Now:yyyyMMdd}.db"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // VACUUM INTO exige que el archivo destino no exista todavía; SaveFileDialog ya
                // confirmó con el usuario si quería sobrescribir, así que aquí solo se aplica.
                if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                App.Repository.BackupTo(dlg.FileName);

                var now = DateTime.UtcNow;
                App.SaveLastBackupDate(now);
                UpdateLastBackupText(now);

                await AppDialogService.ShowInfoAsync("SettingsDialogHost", $"Copia de seguridad guardada en:\n{dlg.FileName}", "Guardar copia de seguridad");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Guardar copia de seguridad", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido guardar la copia de seguridad:\n{ex.Message}");
            }
        }

        private async void BtnVacuum_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Repository.Vacuum();
                await AppDialogService.ShowInfoAsync("SettingsDialogHost", "Base de datos compactada correctamente.", "Compactar base de datos");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Compactar base de datos (VACUUM)", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido compactar la base de datos:\n{ex.Message}");
            }
        }

        private async void BtnFindDuplicates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var all = await App.Repository.GetAllAsync();
                var groups = ImportService.FindDuplicateGroups(all);

                if (groups.Count == 0)
                {
                    await AppDialogService.ShowInfoAsync("SettingsDialogHost",
                        "No se han encontrado posibles duplicados en tu colección.", "Buscar duplicados");
                    return;
                }

                var dlg = new DuplicatesDialog(groups) { Owner = this };
                if (dlg.ShowDialog() != true || dlg.SelectedIds.Count == 0) return;

                foreach (var id in dlg.SelectedIds)
                    await App.Repository.DeleteAsync(id);

                await AppDialogService.ShowInfoAsync("SettingsDialogHost",
                    $"{dlg.SelectedIds.Count} juego(s) enviados a la papelera. Puedes restaurarlos desde ahí si te equivocaste.",
                    "Buscar duplicados");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Buscar duplicados en la colección", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido completar la búsqueda:\n{ex.Message}");
            }
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Importar colección",
                Filter = "CSV o Excel (*.csv;*.xlsx)|*.csv;*.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var importer = new ImportService();
                var headers = importer.ReadHeaders(dlg.FileName);
                if (headers.Count == 0)
                {
                    await AppDialogService.ShowWarningAsync("SettingsDialogHost",
                        "No se ha podido leer ninguna cabecera de columna en el archivo.", "Importar colección");
                    return;
                }

                var guessed = ImportService.GuessMapping(headers);
                var mappingDlg = new ImportColumnMappingDialog(headers, guessed) { Owner = this };
                if (mappingDlg.ShowDialog() != true) return;

                var parsed = importer.ParseFile(dlg.FileName, mappingDlg.Mapping);

                if (parsed.Count == 0)
                {
                    await AppDialogService.ShowWarningAsync("SettingsDialogHost",
                        "No se ha encontrado ninguna fila válida. Revisa que el archivo tenga una fila de cabecera y una columna \"Título\".",
                        "Importar colección");
                    return;
                }

                var existing = await App.Repository.GetAllAsync();
                var classified = ImportService.BuildPreview(parsed, existing);
                var previewItems = classified.Select(c => new ImportPreviewItem(c.Game, c.Status)).ToList();

                var previewDlg = new ImportPreviewDialog(previewItems) { Owner = this };
                if (previewDlg.ShowDialog() != true) return;

                if (previewDlg.SelectedGames.Count == 0)
                {
                    await AppDialogService.ShowInfoAsync("SettingsDialogHost",
                        "No se ha seleccionado ningún juego para importar.", "Importar colección");
                    return;
                }

                var (added, duplicates) = await App.Repository.ImportAsync(previewDlg.SelectedGames);

                var msg = $"Importación completada.\n\nAñadidos: {added}";
                if (duplicates > 0) msg += $"\nOmitidos por conflicto en la base de datos: {duplicates}";

                await AppDialogService.ShowInfoAsync("SettingsDialogHost", msg, "Importar colección");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Importar colección desde CSV/Excel", ex);
                await AppDialogService.ShowErrorAsync("SettingsDialogHost", $"No se ha podido importar el archivo:\n{ex.Message}");
            }
        }
    }
}
