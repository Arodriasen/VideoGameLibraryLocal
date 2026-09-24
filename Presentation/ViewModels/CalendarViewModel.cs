using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Application.Platforms;
using VideoGameLibrary.Domain.Entities;
using VideoGameLibrary.Domain.Repositories;

namespace VideoGameLibrary.Presentation.ViewModels
{
    public partial class CalendarViewModel : ObservableObject
    {
        private readonly IGameRepository _repo;
        private readonly IGameApiService _api;

        // Caché en memoria por mes visitado — navegar entre meses ya vistos, o cambiar el filtro
        // de plataformas, no repite la llamada a IGDB.
        private readonly Dictionary<(int Year, int Month), List<UpcomingRelease>> _monthCache = new();

        // Un UpcomingReleaseItem por UpcomingRelease (identidad por referencia) — así el estado
        // "ya añadido a la lista de deseos/colección" sobrevive a RebuildDaysFromCache (se llama
        // cada vez que cambia el filtro de plataformas o se vuelve a un mes ya visitado, y
        // reconstruye Days.Releases desde cero a partir de los mismos UpcomingRelease en caché).
        private readonly Dictionary<UpcomingRelease, UpcomingReleaseItem> _releaseItemCache = new();

        [ObservableProperty] private bool _isLoading = true;
        [ObservableProperty] private int _year;
        [ObservableProperty] private int _month;
        [ObservableProperty] private string _monthLabel = string.Empty;
        [ObservableProperty] private CalendarDayCell? _selectedDay;

        public ObservableCollection<CalendarDayCell> Days { get; } = new();
        public ObservableCollection<FilterOption> PlatformFilters { get; } = new();
        public ISnackbarMessageQueue SnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

        // Se pone a true en cuanto se añade algún lanzamiento a la colección o a la lista de
        // deseos — MainWindow lo usa (vía CalendarDialog.Changed) para saber si hace falta recargar
        // la lista principal al cerrar este diálogo, igual que ya hace TrashDialog.Changed.
        public bool HasChanges { get; private set; }

        // Solo se selecciona hoy automáticamente la primera vez que se abre el calendario — al
        // navegar de mes con Prev/Next no se fuerza ninguna selección.
        private bool _hasSelectedInitialDay;

        public CalendarViewModel(IGameRepository repo, IGameApiService api)
        {
            _repo = repo;
            _api = api;
            Year = DateTime.Today.Year;
            Month = DateTime.Today.Month;
        }

        public async Task InitializeAsync()
        {
            var games = await _repo.GetAllAsync();
            BuildExistingGameIndex(games);
            BuildPlatformFilters(games);
            await LoadMonthAsync();
        }

        // El filtro ofrece todas las plataformas conocidas (IgdbPlatformMap.CanonicalPlatforms),
        // pero solo empiezan marcadas las que el usuario tiene realmente en la colección que está
        // viendo ahora mismo — el resto quedan disponibles para añadirlas a mano si hace falta.
        private void BuildPlatformFilters(List<Game> games)
        {
            var ownedPlatformIds = games
                .SelectMany(g => SplitPlatforms(g.Platform))
                .Where(p => IgdbPlatformMap.TryGetId(p, out _))
                .Select(p => { IgdbPlatformMap.TryGetId(p, out var id); return id; })
                .ToHashSet();

            PlatformFilters.Clear();
            foreach (var (name, id) in IgdbPlatformMap.CanonicalPlatforms)
            {
                PlatformFilters.Add(new FilterOption
                {
                    Value = name,
                    IsSelected = ownedPlatformIds.Contains(id),
                    OnChanged = RebuildDaysFromCache
                });
            }
        }

        // Índice título+plataforma → Id, para que un lanzamiento que el usuario YA tiene en su
        // lista de deseos o colección (por cualquier vía: escaneo, importación, o una sesión previa
        // del propio calendario) salga marcado desde el primer momento, no solo tras pulsar el
        // botón en esta sesión. Se construye una sola vez al abrir el calendario — un juego solo
        // puede añadirse mientras el diálogo está abierto a través de estos mismos botones (el
        // diálogo es modal), así que el índice no se queda desactualizado durante la sesión.
        private Dictionary<(string Title, string Platform), int> _existingWishlistIds = new();
        private Dictionary<(string Title, string Platform), int> _existingCollectionIds = new();

        private void BuildExistingGameIndex(List<Game> games)
        {
            _existingWishlistIds = BuildTitlePlatformIndex(games.Where(g => g.IsWishlist));
            _existingCollectionIds = BuildTitlePlatformIndex(games.Where(g => !g.IsWishlist));
        }

        private static Dictionary<(string, string), int> BuildTitlePlatformIndex(IEnumerable<Game> games)
        {
            var index = new Dictionary<(string, string), int>();
            foreach (var g in games)
            {
                foreach (var platform in SplitPlatforms(g.Platform))
                {
                    var key = TitlePlatformKey(g.Title, platform);
                    if (!index.ContainsKey(key)) index[key] = g.Id;
                }
            }
            return index;
        }

        private static (string, string) TitlePlatformKey(string title, string platform) =>
            (title.Trim().ToLowerInvariant(), platform.Trim().ToLowerInvariant());

        // Igual que MainViewModel.SplitGenres — Platform puede venir como varios nombres separados
        // por coma cuando un juego tiene ediciones multi-plataforma.
        private static IEnumerable<string> SplitPlatforms(string platform) =>
            platform.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        [RelayCommand]
        private async Task PrevMonth()
        {
            Month--;
            if (Month < 1) { Month = 12; Year--; }
            await LoadMonthAsync();
        }

        [RelayCommand]
        private async Task NextMonth()
        {
            Month++;
            if (Month > 12) { Month = 1; Year++; }
            await LoadMonthAsync();
        }

        [RelayCommand]
        private void SelectDay(CalendarDayCell cell)
        {
            if (SelectedDay != null) SelectedDay.IsSelected = false;
            SelectedDay = cell;
            cell.IsSelected = true;
        }

        // Los dos botones del calendario (corazón/carrito) son toggles: primer clic añade,
        // segundo clic quita — igual de reversible que cualquier borrado normal de la app (va a la
        // papelera, no se pierde). Añadir desde aquí da un juego con datos parciales (sin código de
        // barras ni género — IGDB release_dates no los trae) a propósito: el usuario sabe que puede
        // completarlos luego editando el juego, igual que con cualquier alta manual.
        public async Task ToggleWishlistAsync(UpcomingReleaseItem item)
        {
            if (item.IsInWishlist)
            {
                var id = item.WishlistGameId;
                // Se desmarca ANTES del await para que un doble clic mientras se borra no repita
                // la operación (mismo motivo que al marcar al añadir, ver rama de abajo).
                item.IsInWishlist = false;
                item.WishlistGameId = null;
                if (!id.HasValue) return;

                try
                {
                    await RemoveReleaseAsync(id.Value, item.Title, isWishlist: true);
                }
                catch
                {
                    item.IsInWishlist = true;
                    item.WishlistGameId = id;
                    throw;
                }
            }
            else
            {
                item.IsInWishlist = true;
                try
                {
                    item.WishlistGameId = await AddReleaseAsync(item.Release, isWishlist: true);
                }
                catch
                {
                    item.IsInWishlist = false;
                    item.WishlistGameId = null;
                    throw;
                }
            }
        }

        public async Task ToggleCollectionAsync(UpcomingReleaseItem item)
        {
            if (item.IsInCollection)
            {
                var id = item.CollectionGameId;
                item.IsInCollection = false;
                item.CollectionGameId = null;
                if (!id.HasValue) return;

                try
                {
                    await RemoveReleaseAsync(id.Value, item.Title, isWishlist: false);
                }
                catch
                {
                    item.IsInCollection = true;
                    item.CollectionGameId = id;
                    throw;
                }
            }
            else
            {
                item.IsInCollection = true;
                try
                {
                    item.CollectionGameId = await AddReleaseAsync(item.Release, isWishlist: false);
                }
                catch
                {
                    item.IsInCollection = false;
                    item.CollectionGameId = null;
                    throw;
                }
            }
        }

        private async Task<int> AddReleaseAsync(UpcomingRelease release, bool isWishlist)
        {
            // El CoverUrl del release viene recortado a "t_cover_small" (miniatura del calendario,
            // ver GameApiService.GetUpcomingReleasesAsync) — para guardarlo en la colección hace
            // falta la versión grande, si no la portada se ve pixelada en las tarjetas/fichas normales.
            var highResCoverUrl = release.CoverUrl?.Replace("t_cover_small", "t_cover_big");

            var game = new Game
            {
                Title = release.Title,
                Platform = release.PlatformName,
                Year = release.ReleaseDateUtc.Year,
                CoverUrl = highResCoverUrl ?? string.Empty,
                IsWishlist = isWishlist
            };

            if (!string.IsNullOrEmpty(highResCoverUrl))
                game.CoverData = await _api.DownloadCoverAsync(highResCoverUrl);

            await _repo.AddAsync(game);
            HasChanges = true;

            var where = isWishlist ? "tu lista de deseos" : "tu colección";
            SnackbarMessageQueue.Enqueue($"\"{game.Title}\" añadido a {where}.");

            return game.Id;
        }

        // Borrado suave (papelera), igual que MainViewModel.DeleteGameAsync — quitar desde el
        // calendario no debería ser más destructivo que borrar un juego desde la lista principal.
        private async Task RemoveReleaseAsync(int gameId, string title, bool isWishlist)
        {
            await _repo.DeleteAsync(gameId);
            HasChanges = true;

            var where = isWishlist ? "tu lista de deseos" : "tu colección";
            SnackbarMessageQueue.Enqueue($"\"{title}\" quitado de {where}.");
        }

        private async Task LoadMonthAsync()
        {
            IsLoading = true;
            MonthLabel = new DateTime(Year, Month, 1).ToString("MMMM yyyy", new CultureInfo("es-ES"));

            if (!_monthCache.TryGetValue((Year, Month), out var releases))
            {
                // Se pide siempre el mes completo restringido a las plataformas de
                // IgdbPlatformMap (no las que el usuario tenga marcadas — el filtro de checkboxes
                // se aplica después, en memoria, en RebuildDaysFromCache, así que cambiarlo no
                // dispara una llamada nueva a IGDB). Restringir por plataforma aquí no es solo una
                // optimización: sin esto, "todas las plataformas" de un mes normal supera con
                // facilidad el límite de 500 de IGDB (contando móvil, arcade, regionales...), y el
                // recorte deja de ser estable entre llamadas — el mismo día podía mostrar
                // lanzamientos distintos cada vez que se reabría el calendario.
                var monthStart = new DateTime(Year, Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1).AddSeconds(-1);
                var allCanonicalPlatformIds = IgdbPlatformMap.CanonicalPlatforms.Select(p => p.Id);
                releases = await _api.GetUpcomingReleasesAsync(monthStart, monthEnd, allCanonicalPlatformIds);
                _monthCache[(Year, Month)] = releases;
            }

            RebuildDaysFromCache();

            if (!_hasSelectedInitialDay)
            {
                _hasSelectedInitialDay = true;
                var todayCell = Days.FirstOrDefault(d => d.IsToday);
                if (todayCell != null) SelectDay(todayCell);
            }

            IsLoading = false;
        }

        // Se llama tanto tras cargar un mes nuevo como al cambiar el filtro de plataformas —
        // el mes completo (sin filtrar) ya está en caché, así que filtrar no vuelve a llamar a IGDB.
        private void RebuildDaysFromCache()
        {
            if (!_monthCache.TryGetValue((Year, Month), out var releases)) return;

            var selectedPlatforms = PlatformFilters.Where(f => f.IsSelected).Select(f => f.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            // PlatformName puede combinar varias plataformas (mismo juego, mismo día, varias
            // plataformas a la vez) — basta con que UNA de ellas esté marcada en el filtro.
            var filtered = selectedPlatforms.Count == 0
                ? releases
                : releases.Where(r => SplitPlatforms(r.PlatformName).Any(selectedPlatforms.Contains)).ToList();

            var byDay = filtered.ToLookup(r => r.ReleaseDateUtc.Date);
            var previouslySelectedDate = SelectedDay?.Date;

            var firstOfMonth = new DateTime(Year, Month, 1);
            // La semana empieza en lunes: DayOfWeek.Sunday=0 hay que tratarlo como el último día.
            var offset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
            var gridStart = firstOfMonth.AddDays(-offset);

            Days.Clear();
            SelectedDay = null;
            for (int i = 0; i < 42; i++)
            {
                var date = gridStart.AddDays(i);
                var cell = new CalendarDayCell
                {
                    Date = date,
                    IsCurrentMonth = date.Month == Month,
                    IsToday = date.Date == DateTime.Today,
                    Releases = byDay[date.Date].Select(GetOrCreateItem).ToList()
                };

                if (date == previouslySelectedDate)
                {
                    cell.IsSelected = true;
                    SelectedDay = cell;
                }

                Days.Add(cell);
            }
        }

        private UpcomingReleaseItem GetOrCreateItem(UpcomingRelease release)
        {
            if (!_releaseItemCache.TryGetValue(release, out var item))
            {
                item = new UpcomingReleaseItem(release);

                // PlatformName puede combinar varias plataformas — basta con que el juego ya esté
                // en la colección/deseos para UNA de ellas para dar por buena la coincidencia.
                foreach (var platform in SplitPlatforms(release.PlatformName))
                {
                    var key = TitlePlatformKey(release.Title, platform);
                    if (!item.IsInWishlist && _existingWishlistIds.TryGetValue(key, out var wishlistId))
                    {
                        item.IsInWishlist = true;
                        item.WishlistGameId = wishlistId;
                    }
                    if (!item.IsInCollection && _existingCollectionIds.TryGetValue(key, out var collectionId))
                    {
                        item.IsInCollection = true;
                        item.CollectionGameId = collectionId;
                    }
                }

                _releaseItemCache[release] = item;
            }
            return item;
        }
    }
}
