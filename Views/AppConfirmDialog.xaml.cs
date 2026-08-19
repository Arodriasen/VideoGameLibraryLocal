using System.Windows.Controls;

namespace VideoGameLibrary.Views
{
    // Confirmación Sí/No con el estilo Material de la app, mostrada dentro de un md:DialogHost
    // (ver AppDialogService.ShowConfirmAsync) en vez del MessageBox.Show nativo de Windows.
    public partial class AppConfirmDialog : UserControl
    {
        public AppConfirmDialog(string message, string title)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
        }
    }
}
