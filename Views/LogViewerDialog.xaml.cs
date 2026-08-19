using System.Diagnostics;
using System.IO;
using System.Windows;
using VideoGameLibrary.Services;

namespace VideoGameLibrary.Views
{
    public partial class LogViewerDialog : Window
    {
        public LogViewerDialog()
        {
            InitializeComponent();
            LoadLog();
        }

        private void LoadLog()
        {
            var text = LoggingService.ReadAllLogsText();
            bool hasContent = !string.IsNullOrWhiteSpace(text);

            TxtLog.Text = text;
            TxtLog.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;
            TxtEmpty.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;

            TxtLog.CaretIndex = TxtLog.Text.Length;
            TxtLog.ScrollToEnd();
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(LoggingService.LogFolderPath);
            Process.Start(new ProcessStartInfo(LoggingService.LogFolderPath) { UseShellExecute = true });
        }

        private async void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = await AppDialogService.ShowConfirmAsync("LogViewerDialogHost",
                "¿Borrar todo el registro de errores? Esta acción no se puede deshacer.",
                "Limpiar registro");

            if (!confirmed) return;

            LoggingService.ClearLogs();
            LoadLog();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
