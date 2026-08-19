using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _barcode = string.Empty;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private string _platform = string.Empty;
        [ObservableProperty] private string _publisher = string.Empty;
        [ObservableProperty] private string _genre = string.Empty;
        [ObservableProperty] private string _tags = string.Empty;
        [ObservableProperty] private int? _year;
        [ObservableProperty] private string _coverUrl = string.Empty;
        [ObservableProperty] private byte[]? _coverData;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private int _rating;
        [ObservableProperty] private bool _played;
        [ObservableProperty] private bool _isWishlist;
        [ObservableProperty] private bool _isSelected;

        // Notifica al MainViewModel para recalcular el contador de seleccionados
        public Action? OnSelectionChanged { get; set; }
        partial void OnIsSelectedChanged(bool value) => OnSelectionChanged?.Invoke();

        // Para los chips de la ficha y de la vista de lista, que sí pueden mostrar todas las
        // etiquetas (la ficha no tiene límite de ancho, la lista envuelve en varias líneas).
        public List<string> TagList => MainViewModel.SplitTags(Tags).ToList();

        // La tarjeta de la cuadrícula sí tiene un ancho fijo (160px) y no puede envolver texto sin
        // romper la altura uniforme que exige VirtualizingWrapPanel (ver comentario en MainWindow.xaml).
        // En vez de cortar una etiqueta a mitad de palabra cuando no cabe, se calcula cuántas
        // etiquetas completas caben en un presupuesto aproximado de caracteres y el resto se resume
        // en un "+N" — la lista completa siempre está disponible en la ficha de detalle.
        private const int CardTagCharBudget = 20;

        public List<string> CardVisibleTags
        {
            get
            {
                var all = TagList;
                if (all.Count == 0) return all;

                // La primera etiqueta se muestra siempre (recortada con "..." si hiciera falta,
                // ver TextTrimming en MainWindow.xaml) para que la tarjeta nunca se quede sin ninguna.
                var visible = new List<string> { all[0] };
                var used = all[0].Length;
                for (int i = 1; i < all.Count; i++)
                {
                    var cost = all[i].Length + 2; // +2 aproxima el separador entre chips
                    if (used + cost > CardTagCharBudget) break;
                    visible.Add(all[i]);
                    used += cost;
                }
                return visible;
            }
        }

        public int CardHiddenTagCount => TagList.Count - CardVisibleTags.Count;
        public bool HasHiddenTags => CardHiddenTagCount > 0;

        // Se recalcula cuando cambia Tags (p.ej. tras editar el juego)
        partial void OnTagsChanged(string value)
        {
            OnPropertyChanged(nameof(TagList));
            OnPropertyChanged(nameof(CardVisibleTags));
            OnPropertyChanged(nameof(CardHiddenTagCount));
            OnPropertyChanged(nameof(HasHiddenTags));
        }

        public static GameViewModel FromModel(Game g) => new()
        {
            Id = g.Id,
            Barcode = g.Barcode ?? string.Empty,
            Title = g.Title,
            Platform = g.Platform,
            Publisher = g.Publisher,
            Genre = g.Genre,
            Tags = g.Tags,
            Year = g.Year,
            CoverUrl = g.CoverUrl,
            CoverData = g.CoverData,
            Notes = g.Notes,
            Rating = g.Rating,
            Played = g.Played,
            IsWishlist = g.IsWishlist
        };

        public Game ToModel() => new()
        {
            Id = Id,
            Barcode = string.IsNullOrEmpty(Barcode) ? null : Barcode,
            Title = Title,
            Platform = Platform,
            Publisher = Publisher,
            Genre = Genre,
            Tags = Tags,
            Year = Year,
            CoverUrl = CoverUrl,
            CoverData = CoverData,
            Notes = Notes,
            Rating = Rating,
            Played = Played,
            IsWishlist = IsWishlist
        };
    }
}
