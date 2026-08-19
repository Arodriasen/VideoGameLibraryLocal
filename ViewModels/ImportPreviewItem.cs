using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoGameLibrary.Models;
using VideoGameLibrary.Services;

namespace VideoGameLibrary.ViewModels
{
    public partial class ImportPreviewItem : ObservableObject
    {
        public Game Game { get; }
        public ImportItemStatus Status { get; }

        [ObservableProperty] private bool _isSelected;

        public string Title => Game.Title;
        public string Platform => Game.Platform;
        public int? Year => Game.Year;

        public string StatusText => Status switch
        {
            ImportItemStatus.Nuevo => "Nuevo",
            ImportItemStatus.YaExiste => "Ya existe en tu colección",
            ImportItemStatus.DuplicadoEnArchivo => "Duplicado en el archivo",
            _ => string.Empty
        };

        public Brush StatusBrush => Status switch
        {
            ImportItemStatus.Nuevo => Brushes.MediumSeaGreen,
            ImportItemStatus.YaExiste => Brushes.DarkOrange,
            ImportItemStatus.DuplicadoEnArchivo => Brushes.DarkOrange,
            _ => Brushes.Gray
        };

        public ImportPreviewItem(Game game, ImportItemStatus status)
        {
            Game = game;
            Status = status;
            IsSelected = status == ImportItemStatus.Nuevo;
        }
    }
}
