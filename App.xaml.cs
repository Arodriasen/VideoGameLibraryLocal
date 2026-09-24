using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Domain.Repositories;
using VideoGameLibrary.Infrastructure.ExternalApis;
using VideoGameLibrary.Infrastructure.Logging;
using VideoGameLibrary.Infrastructure.Persistence;
using VideoGameLibrary.Presentation.Services;
using VideoGameLibrary.Presentation.ViewModels;
using VideoGameLibrary.Presentation.Views;

namespace VideoGameLibrary
{
    // Composition root: el único sitio del proyecto que conoce las cuatro capas a la vez y las
    // conecta. Domain/Application no dependen de nada de aquí; Infrastructure implementa las
    // interfaces de Application; Presentation solo ve esas interfaces (vía las propiedades
    // estáticas de abajo), nunca los tipos concretos de Infrastructure.
    public partial class App : System.Windows.Application
    {
        public static IGameRepository Repository { get; private set; } = null!;
        private static IGameApiService _apiService = null!;
        public static IGameApiService ApiService => _apiService;
        public static IAppDialogService DialogService { get; } = new AppDialogService();
        public static IImportService ImportService { get; } = new Infrastructure.Files.ImportService();
        public static IExportService ExportService { get; } = new Infrastructure.Files.ExportService();
        public static bool IsDarkTheme { get; private set; }
        public static string CurrentDatabasePath { get; private set; } = string.Empty;

        // Carpeta propia, distinta de la de VideoGameLibrary (versión Neon): antes compartían
        // config.json y, como cada app solo escribe sus propios campos, al guardar se borraban
        // los ajustes de la otra (ruta del .db y fecha de copia aquí, cadena de conexión allí).
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoGameLibraryLocal");
        private static readonly string ConfigFile = Path.Combine(ConfigFolder, "config.json");
        private static readonly string LegacyConfigFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoGameLibrary", "config.json");

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LoggingService.LogError("Excepción no controlada (hilo de interfaz)", e.Exception);
            MessageBox.Show(
                $"Ha ocurrido un error inesperado:\n\n{e.Exception.Message}\n\nSe ha guardado el detalle en el registro de errores.",
                "Error inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LoggingService.LogError("Excepción no controlada (AppDomain)", ex);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LoggingService.LogError("Excepción no observada en tarea en segundo plano", e.Exception);
            e.SetObserved();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                LoggingService.PurgeOldLogs();
                MigrateLegacyConfig();

                var config = LoadConfig();
                IsDarkTheme = config.DarkTheme;
                ApplyTheme(IsDarkTheme);

                if (string.IsNullOrEmpty(config.ScanDexToken) && string.IsNullOrEmpty(config.IgdbClientId) &&
                    string.IsNullOrEmpty(config.IgdbClientSecret) && string.IsNullOrEmpty(config.RawgApiKey) &&
                    string.IsNullOrEmpty(config.TheGamesDbApiKey))
                {
                    new SettingsDialog(firstRun: true).ShowDialog();
                    config = LoadConfig();
                }

                var dbPath = GetOrSelectDatabase();
                if (dbPath == null)
                {
                    Shutdown();
                    return;
                }

                var db = new GameDbContext(dbPath);
                Repository = new GameRepository(db);
                CurrentDatabasePath = dbPath;
                await Repository.PurgeExpiredTrashAsync();

                _apiService = new GameApiService(
                    config.ScanDexToken,
                    config.IgdbClientId,
                    config.IgdbClientSecret,
                    config.RawgApiKey,
                    config.TheGamesDbApiKey);

                var mainVm = new MainViewModel(Repository, _apiService);
                var mainWindow = new MainWindow(mainVm);

                // App.xaml usa ShutdownMode="OnExplicitShutdown" para que cerrar el diálogo de
                // Ajustes del primer arranque (única ventana abierta en ese momento) no cierre la
                // app entera antes de llegar aquí (era exactamente lo que pasaba con el valor por
                // defecto OnLastWindowClose). A partir de aquí, MainWindow pasa a comportarse como
                // siempre: cerrarla cierra la app.
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;

                mainWindow.Show();

                _ = CheckForUpdatesAsync(mainVm);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error al iniciar la aplicación", ex);
                MessageBox.Show(
                    $"Error al iniciar la aplicación:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "Error de inicio", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static async Task CheckForUpdatesAsync(MainViewModel mainVm)
        {
            var update = await new UpdateCheckService().CheckForUpdateAsync();
            if (update == null) return;

            mainVm.SnackbarMessageQueue.Enqueue(
                $"Hay una nueva versión disponible ({update.Version}).",
                "DESCARGAR",
                () => Process.Start(new ProcessStartInfo(update.Url) { UseShellExecute = true }));
        }

        private static string? GetOrSelectDatabase()
        {
            var lastDb = LoadConfig().LastDatabasePath;

            if (!string.IsNullOrEmpty(lastDb) && File.Exists(lastDb))
                return lastDb;

            return PromptForDatabase();
        }

        // Cambia de colección sin reiniciar la app: crea un repositorio nuevo apuntando al
        // .db elegido y lo deja como el activo. Quien llame es responsable de refrescar la UI.
        public static async Task<bool> SwitchDatabaseInteractiveAsync()
        {
            var path = PromptForDatabase();
            if (path == null) return false;

            await SwitchDatabaseAsync(path);
            return true;
        }

        public static async Task SwitchDatabaseAsync(string path)
        {
            var db = new GameDbContext(path);
            Repository = new GameRepository(db);
            CurrentDatabasePath = path;
            await Repository.PurgeExpiredTrashAsync();
            SaveLastPath(path);
        }

        // Renombra (o mueve) el archivo .db actual. Hay que soltar el archivo antes de moverlo:
        // Dispose() cierra la conexión, pero Microsoft.Data.Sqlite mantiene un pool de conexiones
        // que puede seguir reteniendo el archivo hasta que se limpia explícitamente.
        public static Task<string?> RenameDatabaseFileAsync(string newFileNameOrPath)
        {
            var fileName = newFileNameOrPath.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                ? newFileNameOrPath
                : newFileNameOrPath + ".db";
            var newPath = Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(Path.GetDirectoryName(CurrentDatabasePath)!, fileName);

            if (string.Equals(newPath, CurrentDatabasePath, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<string?>(null);

            if (File.Exists(newPath))
                throw new IOException("Ya existe un archivo con ese nombre.");

            Repository.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Move(CurrentDatabasePath, newPath);

            var db = new GameDbContext(newPath);
            Repository = new GameRepository(db);
            CurrentDatabasePath = newPath;
            SaveLastPath(newPath);

            return Task.FromResult<string?>(newPath);
        }

        private static string? PromptForDatabase()
        {
            var result = MessageBox.Show(
                "¿Quieres abrir una colección existente o crear una nueva?\n\n" +
                "Sí → Abrir existente (.db)\nNo → Crear nueva",
                "Mi Colección de Juegos — Seleccionar base de datos",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return null;

            if (result == MessageBoxResult.Yes)
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Abrir colección existente",
                    Filter = "Base de datos (*.db)|*.db",
                    DefaultExt = ".db"
                };
                if (dlg.ShowDialog() != true) return null;
                SaveLastPath(dlg.FileName);
                return dlg.FileName;
            }
            else
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Crear nueva colección",
                    Filter = "Base de datos (*.db)|*.db",
                    DefaultExt = ".db",
                    FileName = "MiColeccionJuegos"
                };
                if (dlg.ShowDialog() != true) return null;
                SaveLastPath(dlg.FileName);
                return dlg.FileName;
            }
        }

        // Primer arranque tras separar la carpeta de configuración: copia el config.json de la
        // carpeta compartida para no perder claves de API, tema ni la ruta del .db. Se copia tal
        // cual (los valores DPAPI siguen siendo válidos para el mismo usuario de Windows); si
        // trae campos de la versión Neon, se ignoran al leer y desaparecen en el siguiente
        // guardado. El original no se toca, porque la versión Neon lo sigue usando.
        private static void MigrateLegacyConfig()
        {
            try
            {
                if (File.Exists(ConfigFile) || !File.Exists(LegacyConfigFile)) return;

                Directory.CreateDirectory(ConfigFolder);
                File.Copy(LegacyConfigFile, ConfigFile);
            }
            catch (Exception ex) { LoggingService.LogError("Copiar la configuración de la carpeta anterior", ex); }
        }

        public static AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return new AppConfig();
                var json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                // Las claves se guardan cifradas (ver PersistConfig); aquí se descifran para
                // que el resto de la app siga trabajando con el texto plano en memoria.
                config.ScanDexToken = Unprotect(config.ScanDexToken);
                config.IgdbClientId = Unprotect(config.IgdbClientId);
                config.IgdbClientSecret = Unprotect(config.IgdbClientSecret);
                config.RawgApiKey = Unprotect(config.RawgApiKey);
                config.TheGamesDbApiKey = Unprotect(config.TheGamesDbApiKey);
                return config;
            }
            catch
            {
                return new AppConfig();
            }
        }

        // Único punto de escritura de config.json: cifra las claves de API con DPAPI
        // (ligado al usuario de Windows actual) antes de guardar. Las instalaciones que
        // vengan de una versión anterior tenían las claves en texto plano en el archivo;
        // Unprotect las detecta como no cifradas, las deja pasar tal cual, y al llamar aquí
        // de nuevo (el siguiente guardado, del tipo que sea) quedan cifradas sin más pasos.
        private static void PersistConfig(AppConfig config)
        {
            var toStore = new AppConfig
            {
                LastDatabasePath = config.LastDatabasePath,
                ScanDexToken = Protect(config.ScanDexToken),
                IgdbClientId = Protect(config.IgdbClientId),
                IgdbClientSecret = Protect(config.IgdbClientSecret),
                RawgApiKey = Protect(config.RawgApiKey),
                TheGamesDbApiKey = Protect(config.TheGamesDbApiKey),
                DarkTheme = config.DarkTheme,
                LastBackupUtc = config.LastBackupUtc
            };

            Directory.CreateDirectory(ConfigFolder);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(toStore));
        }

        internal static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        // Si el valor no es un blob DPAPI válido (p.ej. una clave en texto plano guardada por
        // una versión anterior de la app), se devuelve tal cual en vez de fallar.
        internal static string Unprotect(string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue)) return string.Empty;
            try
            {
                var decrypted = ProtectedData.Unprotect(Convert.FromBase64String(storedValue), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return storedValue;
            }
        }

        public static void SaveApiKeys(string scanDexToken, string igdbClientId, string igdbClientSecret,
                                        string rawgApiKey, string theGamesDbApiKey)
        {
            var config = LoadConfig();
            config.ScanDexToken = scanDexToken;
            config.IgdbClientId = igdbClientId;
            config.IgdbClientSecret = igdbClientSecret;
            config.RawgApiKey = rawgApiKey;
            config.TheGamesDbApiKey = theGamesDbApiKey;

            PersistConfig(config);

            _apiService?.UpdateKeys(scanDexToken, igdbClientId, igdbClientSecret, rawgApiKey, theGamesDbApiKey);
        }

        public static void SaveLastBackupDate(DateTime utc)
        {
            try
            {
                var config = LoadConfig();
                config.LastBackupUtc = utc;
                PersistConfig(config);
            }
            catch (Exception ex) { LoggingService.LogError("Guardar fecha de la última copia de seguridad", ex); }
        }

        private static void SaveLastPath(string path)
        {
            try
            {
                var config = LoadConfig();
                config.LastDatabasePath = path;
                PersistConfig(config);
            }
            catch (Exception ex) { LoggingService.LogError("Guardar ruta de la última base de datos", ex); }
        }

        public static GameEditViewModel GetEditViewModel()
            => new GameEditViewModel(_apiService);

        public static void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyTheme(IsDarkTheme);
            SaveThemePreference(IsDarkTheme);
        }

        private static void ApplyTheme(bool dark)
        {
            var paletteHelper = new PaletteHelper();
            var currentTheme = paletteHelper.GetTheme();
            var newTheme = Theme.Create(
                dark ? BaseTheme.Dark : BaseTheme.Light,
                currentTheme.PrimaryMid.Color,
                currentTheme.SecondaryMid.Color);
            paletteHelper.SetTheme(newTheme);
        }

        private static void SaveThemePreference(bool dark)
        {
            try
            {
                var config = LoadConfig();
                config.DarkTheme = dark;
                PersistConfig(config);
            }
            catch (Exception ex) { LoggingService.LogError("Guardar preferencia de tema", ex); }
        }

        public class AppConfig
        {
            public string LastDatabasePath { get; set; } = string.Empty;
            public string ScanDexToken { get; set; } = string.Empty;
            public string IgdbClientId { get; set; } = string.Empty;
            public string IgdbClientSecret { get; set; } = string.Empty;
            public string RawgApiKey { get; set; } = string.Empty;
            public string TheGamesDbApiKey { get; set; } = string.Empty;
            public bool DarkTheme { get; set; }
            public DateTime? LastBackupUtc { get; set; }
        }
    }
}
