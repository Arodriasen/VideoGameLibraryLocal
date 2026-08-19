using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VideoGameLibrary.Models;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Views
{
    public partial class ImportPreviewDialog : Window
    {
        private readonly List<ImportPreviewItem> _items;

        public List<Game> SelectedGames { get; private set; } = new();

        public ImportPreviewDialog(List<ImportPreviewItem> items)
        {
            InitializeComponent();
            _items = items;
            ItemsList.ItemsSource = items;

            var nuevos = items.Count(i => i.Status == Services.ImportItemStatus.Nuevo);
            var yaExiste = items.Count(i => i.Status == Services.ImportItemStatus.YaExiste);
            var duplicados = items.Count(i => i.Status == Services.ImportItemStatus.DuplicadoEnArchivo);
            TxtSummary.Text = $"{items.Count} fila(s) leídas: {nuevos} nuevo(s), {yaExiste} ya en tu colección, {duplicados} duplicado(s) en el archivo.";
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            SelectedGames = _items.Where(i => i.IsSelected).Select(i => i.Game).ToList();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
