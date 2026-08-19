using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Views
{
    public partial class ImportColumnMappingDialog : Window
    {
        private readonly List<ImportFieldMapping> _rows;

        // Solo los campos con una columna asignada (Index >= 0), listos para ImportService.ParseFile
        public Dictionary<string, int> Mapping { get; private set; } = new();

        public ImportColumnMappingDialog(List<string> headers, Dictionary<string, int> guessedMapping)
        {
            InitializeComponent();

            var options = new List<HeaderOption> { new HeaderOption(-1, "(Ninguna)") };
            for (int i = 0; i < headers.Count; i++)
            {
                var display = string.IsNullOrWhiteSpace(headers[i]) ? $"Columna {i + 1}" : headers[i];
                options.Add(new HeaderOption(i, display));
            }

            _rows = ImportService.Fields.Select(f =>
            {
                var selected = guessedMapping.TryGetValue(f.Key, out var idx)
                    ? options.First(o => o.Index == idx)
                    : options[0];
                return new ImportFieldMapping(f.Key, f.Label, f.Required, options, selected);
            }).ToList();

            ItemsList.ItemsSource = _rows;
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            var titulo = _rows.First(r => r.FieldKey == "título");
            if (titulo.SelectedOption.Index < 0)
            {
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            Mapping = _rows.Where(r => r.SelectedOption.Index >= 0)
                            .ToDictionary(r => r.FieldKey, r => r.SelectedOption.Index);

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
