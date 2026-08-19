using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoGameLibrary.Models;
using VideoGameLibrary.Services;

namespace VideoGameLibrary.ViewModels
{
    public partial class GameEditViewModel : ObservableObject
    {
        private readonly GameApiService _api;

        // Cancela cualquier búsqueda/descarga en curso si el diálogo se cierra antes de que termine
        // (p.ej. el usuario pulsa Cancelar mientras espera resultados) — evita seguir consumiendo la
        // API en segundo plano y, sobre todo, evita que ResolveCandidateAsync intente abrir el selector
        // de candidatos con Owner apuntando a una ventana ya cerrada.
        private readonly CancellationTokenSource _cts = new();

        [ObservableProperty] private string _dialogTitle = "Añadir juego";
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
        [ObservableProperty] private bool _isSearching;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _canPickAgain;

        public int GameId { get; set; }
        public bool Saved { get; private set; }
        public System.Action? CloseDialog { get; set; }

        // La View resuelve la ambigüedad mostrando un selector; null si el usuario cancela
        public Func<List<Game>, Task<Game?>>? PickCandidate { get; set; }

        // Se recuerda la última búsqueda con varios resultados para poder reabrir el
        // selector sin repetir la llamada a la API si el usuario se equivocó al elegir
        private List<Game>? _lastCandidates;
        private Func<Game, Task>? _lastApply;

        public GameEditViewModel(GameApiService api)
        {
            _api = api;
        }

        public void LoadFromGame(Game game)
        {
            DialogTitle = "Editar juego";
            GameId = game.Id;
            Barcode = game.Barcode ?? string.Empty;
            Title = game.Title;
            Platform = game.Platform;
            Publisher = game.Publisher;
            Genre = game.Genre;
            Tags = game.Tags;
            Year = game.Year;
            CoverUrl = game.CoverUrl;
            CoverData = game.CoverData;
            Notes = game.Notes;
            Rating = game.Rating;
            Played = game.Played;
            IsWishlist = game.IsWishlist;
        }

        [RelayCommand]
        private void SetRating(string value)
        {
            var parsed = int.Parse(value);
            Rating = Rating == parsed ? 0 : parsed;
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            ErrorMessage = string.Empty;
            var barcode = GameApiService.NormalizeBarcode(Barcode);
            if (string.IsNullOrEmpty(barcode))
            {
                ErrorMessage = "Introduce un código de barras.";
                return;
            }

            try
            {
                IsSearching = true;
                var candidates = await _api.SearchCandidatesByBarcodeAsync(barcode, _cts.Token);
                IsSearching = false;

                var game = await ResolveCandidateAsync(candidates,
                    "Código de barras no encontrado. Escribe el título y pulsa \"Buscar por nombre\".",
                    ApplyBarcodeResultAsync);
                if (game != null) await ApplyBarcodeResultAsync(game);
            }
            catch (OperationCanceledException)
            {
                // El diálogo se cerró mientras la búsqueda estaba en curso — nada que actualizar.
            }
        }

        [RelayCommand]
        public async Task SearchByNameAsync()
        {
            ErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "Escribe un título para buscar.";
                return;
            }

            try
            {
                IsSearching = true;
                var candidates = await _api.SearchByNameCandidatesAsync(Title, _cts.Token);
                IsSearching = false;

                var game = await ResolveCandidateAsync(candidates, "No se encontró información para ese título.",
                    ApplyNameResultAsync);
                if (game != null) await ApplyNameResultAsync(game);
            }
            catch (OperationCanceledException)
            {
                // El diálogo se cerró mientras la búsqueda estaba en curso — nada que actualizar.
            }
        }

        // Sobrescribe los campos con el resultado de la búsqueda por código de barras
        private async Task ApplyBarcodeResultAsync(Game game)
        {
            Title = game.Title;
            Platform = game.Platform;
            Publisher = game.Publisher;
            Genre = game.Genre;
            Year = game.Year;
            CoverUrl = game.CoverUrl;

            if (!string.IsNullOrEmpty(game.CoverUrl))
            {
                IsSearching = true;
                CoverData = await _api.DownloadCoverAsync(game.CoverUrl, _cts.Token);
                IsSearching = false;
            }
            else
            {
                CoverData = null;
            }
        }

        // Solo rellena huecos con el resultado de la búsqueda manual por nombre
        private async Task ApplyNameResultAsync(Game game)
        {
            if (!string.IsNullOrEmpty(game.Platform)) Platform = game.Platform;
            if (!string.IsNullOrEmpty(game.Genre)) Genre = game.Genre;
            if (game.Year.HasValue) Year = game.Year;

            // Se compara con la URL anterior (no solo con CoverData == null) para que, al
            // "Elegir otro resultado", sí se vuelva a descargar aunque ya hubiera una portada
            if (!string.IsNullOrEmpty(game.CoverUrl) && game.CoverUrl != CoverUrl)
            {
                CoverUrl = game.CoverUrl;
                IsSearching = true;
                CoverData = await _api.DownloadCoverAsync(CoverUrl, _cts.Token);
                IsSearching = false;
            }
        }

        // Si hay un único resultado se usa directamente; con varios, se le pregunta al
        // usuario cuál es (vía PickCandidate, resuelto por la View con un diálogo selector).
        // La lista y la forma de aplicarla se recuerdan para permitir "Elegir otro resultado".
        private async Task<Game?> ResolveCandidateAsync(List<Game> candidates, string notFoundMessage, Func<Game, Task> apply)
        {
            if (candidates.Count == 0)
            {
                ErrorMessage = notFoundMessage;
                CanPickAgain = false;
                return null;
            }

            _lastCandidates = candidates;
            _lastApply = apply;
            CanPickAgain = candidates.Count > 1;

            if (candidates.Count == 1)
                return candidates[0];

            var picked = PickCandidate != null ? await PickCandidate(candidates) : candidates[0];
            if (picked == null)
                ErrorMessage = "Selección cancelada.";

            return picked;
        }

        [RelayCommand]
        private async Task PickAgainAsync()
        {
            if (_lastCandidates == null || _lastApply == null || PickCandidate == null) return;

            try
            {
                var picked = await PickCandidate(_lastCandidates);
                if (picked != null) await _lastApply(picked);
            }
            catch (OperationCanceledException)
            {
                // El diálogo se cerró mientras se descargaba la portada del candidato elegido.
            }
        }

        // Llamado por la View al cerrarse (Cancelar, Guardar, X o Alt+F4) para no dejar una
        // búsqueda o descarga de portada corriendo en segundo plano contra un diálogo ya cerrado.
        public void CancelPendingOperations() => _cts.Cancel();

        [RelayCommand]
        private void Save()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "El título es obligatorio.";
                return;
            }

            Saved = true;
            CloseDialog?.Invoke();
        }

        public Game ToGame()
        {
            var cleanBarcode = GameApiService.NormalizeBarcode(Barcode);
            return new()
            {
                Id = GameId,
                Barcode = string.IsNullOrEmpty(cleanBarcode) ? null : cleanBarcode,
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
}
