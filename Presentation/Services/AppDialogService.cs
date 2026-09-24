using System.Threading.Tasks;
using MaterialDesignThemes.Wpf;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Presentation.Views;

namespace VideoGameLibrary.Presentation.Services
{
    // Envoltorio fino sobre DialogHost para mostrar avisos con el estilo Material de la app en
    // vez del MessageBox.Show nativo de Windows. La Window que llame necesita tener su propio
    // <md:DialogHost Identifier="..."> en el XAML raíz; ese identificador es el que se pasa aquí.
    public class AppDialogService : IAppDialogService
    {
        public Task ShowInfoAsync(string identifier, string message, string title = "Información")
            => Show(identifier, message, title, AppDialogSeverity.Info);

        public Task ShowWarningAsync(string identifier, string message, string title = "Aviso")
            => Show(identifier, message, title, AppDialogSeverity.Warning);

        public Task ShowErrorAsync(string identifier, string message, string title = "Error")
            => Show(identifier, message, title, AppDialogSeverity.Error);

        // Devuelve true solo si se pulsó "SÍ" (ver AppConfirmDialog, CommandParameter "True"/"False").
        public async Task<bool> ShowConfirmAsync(string identifier, string message, string title = "Confirmar")
        {
            var result = await DialogHost.Show(new AppConfirmDialog(message, title), identifier);
            return result is string s && s == "True";
        }

        private static Task Show(string identifier, string message, string title, AppDialogSeverity severity)
            => DialogHost.Show(new AppMessageDialog(message, title, severity), identifier);
    }
}
