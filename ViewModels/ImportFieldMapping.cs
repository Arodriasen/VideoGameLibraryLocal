using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoGameLibrary.ViewModels
{
    // Una columna del archivo, para el ComboBox de ImportColumnMappingDialog.
    // Index = -1 representa "(Ninguna)" (el campo se deja vacío al importar).
    public class HeaderOption
    {
        public int Index { get; }
        public string Display { get; }

        public HeaderOption(int index, string display)
        {
            Index = index;
            Display = display;
        }

        public override string ToString() => Display;
    }

    // Fila de ImportColumnMappingDialog: qué columna del archivo corresponde a un campo de Game.
    public partial class ImportFieldMapping : ObservableObject
    {
        public string FieldKey { get; }
        public string Label { get; }
        public bool Required { get; }
        public string DisplayLabel { get; }
        public List<HeaderOption> Options { get; }

        [ObservableProperty] private HeaderOption _selectedOption;

        public ImportFieldMapping(string fieldKey, string label, bool required, List<HeaderOption> options, HeaderOption selectedOption)
        {
            FieldKey = fieldKey;
            Label = label;
            Required = required;
            DisplayLabel = required ? label + " *" : label;
            Options = options;
            _selectedOption = selectedOption;
        }
    }
}
