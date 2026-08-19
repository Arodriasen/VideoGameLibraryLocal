using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace VideoGameLibrary.Views
{
    public enum AppDialogSeverity { Info, Warning, Error }

    // Contenido mostrado dentro de un md:DialogHost (ver AppDialogService) como alternativa
    // con el estilo Material de la app al MessageBox.Show nativo de Windows.
    public partial class AppMessageDialog : UserControl
    {
        private static readonly Brush InfoBrush = (Brush)new BrushConverter().ConvertFromString("#2196F3")!;
        private static readonly Brush WarningBrush = (Brush)new BrushConverter().ConvertFromString("#FFA000")!;
        private static readonly Brush ErrorBrush = (Brush)new BrushConverter().ConvertFromString("#E53935")!;

        public AppMessageDialog(string message, string title, AppDialogSeverity severity)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;

            (Icon.Kind, Icon.Foreground) = severity switch
            {
                AppDialogSeverity.Warning => (PackIconKind.AlertCircleOutline, WarningBrush),
                AppDialogSeverity.Error => (PackIconKind.CloseCircleOutline, ErrorBrush),
                _ => (PackIconKind.CheckCircleOutline, InfoBrush)
            };
        }
    }
}
