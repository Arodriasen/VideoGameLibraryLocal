using VideoGameLibrary.Data;
using VideoGameLibrary.Models;
using VideoGameLibrary.Services;
using VideoGameLibrary.ViewModels;
using Xunit;

namespace VideoGameLibrary.Tests
{
    // MainViewModel crea un SnackbarMessageQueue de MaterialDesignThemes en el constructor, que
    // exige un hilo con Dispatcher de WPF -- un [Fact] normal de xUnit corre en un hilo del pool
    // sin Dispatcher y falla con "SnackbarMessageQueue must be created in a dispatcher thread".
    // [WpfFact] (Xunit.StaFact) ejecuta el test en un hilo STA con Dispatcher, como en la app real.
    public class MainViewModelTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly GameRepository _repo;
        private readonly MainViewModel _vm;

        public MainViewModelTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            _repo = new GameRepository(new GameDbContext(_dbPath));
            _vm = new MainViewModel(_repo, new GameApiService());
        }

        public void Dispose()
        {
            _repo.Dispose();
            // EF Core/Microsoft.Data.Sqlite mantienen un pool de conexiones nativo: sin esto el
            // archivo .db sigue "en uso" un instante después de Dispose() y el borrado falla.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private async Task SeedAsync(params Game[] games)
        {
            foreach (var g in games) await _repo.AddAsync(g);
            await _vm.LoadGamesAsync();
        }

        [WpfFact]
        public async Task GenreFilters_separa_generos_combinados_sin_duplicar()
        {
            // Así los guarda GameApiService cuando IGDB/RAWG devuelven varios géneros por juego
            await SeedAsync(
                new Game { Title = "A", Genre = "Acción, Aventura" },
                new Game { Title = "B", Genre = "Aventura, RPG" });

            var values = _vm.GenreFilters.Select(f => f.Value).OrderBy(v => v).ToList();

            Assert.Equal(new[] { "Acción", "Aventura", "RPG" }, values);
        }

        [WpfFact]
        public async Task Filtrar_por_un_genero_incluye_juegos_con_generos_combinados()
        {
            await SeedAsync(
                new Game { Title = "A", Genre = "Acción, Aventura" },
                new Game { Title = "B", Genre = "Aventura, RPG" },
                new Game { Title = "C", Genre = "Deportes" });

            _vm.GenreFilters.Single(f => f.Value == "Aventura").IsSelected = true;

            Assert.Equal(new[] { "A", "B" }, _vm.Games.Select(g => g.Title).OrderBy(t => t));
        }

        [WpfFact]
        public async Task OnlyMissingCover_muestra_solo_juegos_sin_portada()
        {
            await SeedAsync(
                new Game { Title = "Con portada", CoverData = new byte[] { 1, 2, 3 } },
                new Game { Title = "Sin portada", CoverData = null });

            _vm.OnlyMissingCover = true;

            Assert.Equal("Sin portada", Assert.Single(_vm.Games).Title);
        }

        [WpfFact]
        public async Task SearchText_busca_tambien_por_genero()
        {
            await SeedAsync(
                new Game { Title = "Celeste", Platform = "PC", Genre = "Plataformas" },
                new Game { Title = "Otro", Platform = "PS5", Genre = "Acción" });

            _vm.SearchText = "plataformas";

            Assert.Equal("Celeste", Assert.Single(_vm.Games).Title);
        }

        [WpfFact]
        public async Task ClearFiltersCommand_resetea_generos_plataformas_y_sin_portada()
        {
            await SeedAsync(new Game { Title = "A", Genre = "Acción", Platform = "PC", CoverData = null });

            _vm.GenreFilters.First().IsSelected = true;
            _vm.PlatformFilters.First().IsSelected = true;
            _vm.OnlyMissingCover = true;

            _vm.ClearFiltersCommand.Execute(null);

            Assert.False(_vm.OnlyMissingCover);
            Assert.DoesNotContain(_vm.GenreFilters, f => f.IsSelected);
            Assert.DoesNotContain(_vm.PlatformFilters, f => f.IsSelected);
            Assert.Single(_vm.Games);
        }

        [WpfFact]
        public async Task SortOption_Plataforma_ordena_por_plataforma_y_luego_por_titulo()
        {
            await SeedAsync(
                new Game { Title = "Z Juego", Platform = "PC" },
                new Game { Title = "A Juego", Platform = "PC" },
                new Game { Title = "Cualquiera", Platform = "Nintendo Switch" });

            _vm.SortOption = "Plataforma";

            Assert.Equal(new[] { "Cualquiera", "A Juego", "Z Juego" }, _vm.Games.Select(g => g.Title));
        }

        [WpfFact]
        public async Task IsWishlistView_separa_coleccion_y_lista_de_deseos()
        {
            await SeedAsync(
                new Game { Title = "En colección", IsWishlist = false },
                new Game { Title = "En deseos", IsWishlist = true });

            Assert.Equal("En colección", Assert.Single(_vm.Games).Title);

            _vm.ToggleWishlistViewCommand.Execute(null);

            Assert.Equal("En deseos", Assert.Single(_vm.Games).Title);
        }
    }

    // Tests puros de la utilidad estática de separar géneros, sin necesidad de instanciar
    // MainViewModel (y por tanto sin necesitar [WpfFact]/Dispatcher).
    public class MainViewModelSplitGenresTests
    {
        [Fact]
        public void SplitGenres_separa_por_comas_y_recorta_espacios()
        {
            Assert.Equal(new[] { "Acción", "Aventura", "RPG" }, MainViewModel.SplitGenres("Acción,  Aventura ,RPG"));
        }

        [Fact]
        public void SplitGenres_con_texto_vacio_no_devuelve_nada()
        {
            Assert.Empty(MainViewModel.SplitGenres(""));
        }
    }

    // Mismo criterio que SplitGenres, para las etiquetas libres del usuario.
    public class MainViewModelSplitTagsTests
    {
        [Fact]
        public void SplitTags_separa_por_comas_y_recorta_espacios()
        {
            Assert.Equal(new[] { "favorito", "para vender" }, MainViewModel.SplitTags("favorito,  para vender "));
        }

        [Fact]
        public void SplitTags_con_texto_vacio_no_devuelve_nada()
        {
            Assert.Empty(MainViewModel.SplitTags(""));
        }
    }
}
