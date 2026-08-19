using CommunityToolkit.Mvvm.ComponentModel;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.ViewModels
{
    public partial class DuplicateCandidateItem : ObservableObject
    {
        public Game Game { get; }
        public bool IsGroupStart { get; }
        public int GroupCount { get; }

        [ObservableProperty] private bool _isSelected;

        public string Title => Game.Title;
        public string Platform => Game.Platform;
        public string BarcodeText => string.IsNullOrEmpty(Game.Barcode) ? "sin código de barras" : Game.Barcode;
        public string AddedDateText => $"añadido el {Game.AddedDate:dd/MM/yyyy}";
        public string GroupHeaderText => $"Posible duplicado — {GroupCount} copias";

        public DuplicateCandidateItem(Game game, bool isSelected, bool isGroupStart, int groupCount)
        {
            Game = game;
            IsSelected = isSelected;
            IsGroupStart = isGroupStart;
            GroupCount = groupCount;
        }
    }
}
