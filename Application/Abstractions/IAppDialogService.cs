using System.Threading.Tasks;

namespace VideoGameLibrary.Application.Abstractions
{
    public interface IAppDialogService
    {
        Task ShowInfoAsync(string identifier, string message, string title = "Información");
        Task ShowWarningAsync(string identifier, string message, string title = "Aviso");
        Task ShowErrorAsync(string identifier, string message, string title = "Error");

        // Devuelve true solo si se pulsó "SÍ".
        Task<bool> ShowConfirmAsync(string identifier, string message, string title = "Confirmar");
    }
}
