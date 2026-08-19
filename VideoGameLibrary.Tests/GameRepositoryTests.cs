using Microsoft.EntityFrameworkCore;
using VideoGameLibrary.Data;
using VideoGameLibrary.Models;
using VideoGameLibrary.Services;

namespace VideoGameLibrary.Tests
{
    // Cada test usa su propio archivo SQLite temporal (no una BD compartida) para poder
    // correr en paralelo sin interferirse y dejar el disco limpio al terminar.
    public class GameRepositoryTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly GameDbContext _db;
        private readonly GameRepository _repo;

        public GameRepositoryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            _db = new GameDbContext(_dbPath);
            _repo = new GameRepository(_db);
        }

        public void Dispose()
        {
            _repo.Dispose();
            // EF Core/Microsoft.Data.Sqlite mantienen un pool de conexiones nativo: sin esto el
            // archivo .db sigue "en uso" un instante después de Dispose() y el borrado falla.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private static Game NewGame(string title, string? barcode = null, string platform = "Nintendo Switch") =>
            new() { Title = title, Barcode = barcode, Platform = platform };

        [Fact]
        public async Task AddAsync_guarda_y_GetAllAsync_lo_devuelve()
        {
            await _repo.AddAsync(NewGame("Zelda"));

            var all = await _repo.GetAllAsync();

            Assert.Single(all);
            Assert.Equal("Zelda", all[0].Title);
        }

        [Fact]
        public async Task AddAsync_con_barcode_duplicado_lanza_DbUpdateException()
        {
            await _repo.AddAsync(NewGame("Juego A", barcode: "111"));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => _repo.AddAsync(NewGame("Juego B", barcode: "111")));
        }

        [Fact]
        public async Task AddAsync_tras_barcode_duplicado_fallido_no_bloquea_operaciones_posteriores()
        {
            await _repo.AddAsync(NewGame("Juego A", barcode: "111"));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => _repo.AddAsync(NewGame("Juego B", barcode: "111")));

            // Antes de soltar del ChangeTracker la entidad fallida, este SaveChangesAsync
            // -- sin relación alguna con el duplicado -- también fallaba con el mismo
            // UNIQUE constraint, porque EF reintentaba guardar la entidad fallida junto a este.
            await _repo.AddAsync(NewGame("Juego C", barcode: "222"));

            var all = await _repo.GetAllAsync();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public async Task DeleteAsync_es_borrado_suave_el_juego_pasa_a_la_papelera()
        {
            await _repo.AddAsync(NewGame("Celeste"));
            var id = (await _repo.GetAllAsync()).Single().Id;

            await _repo.DeleteAsync(id);

            Assert.Empty(await _repo.GetAllAsync());
            var trash = await _repo.GetTrashAsync();
            Assert.Equal("Celeste", Assert.Single(trash).Title);
        }

        [Fact]
        public async Task RestoreAsync_devuelve_el_juego_de_la_papelera_a_la_coleccion()
        {
            await _repo.AddAsync(NewGame("Hollow Knight"));
            var id = (await _repo.GetAllAsync()).Single().Id;
            await _repo.DeleteAsync(id);

            await _repo.RestoreAsync(id);

            Assert.Single(await _repo.GetAllAsync());
            Assert.Empty(await _repo.GetTrashAsync());
        }

        [Fact]
        public async Task PermanentlyDeleteAsync_borra_de_verdad_sin_pasar_por_la_papelera()
        {
            await _repo.AddAsync(NewGame("Juego temporal"));
            var id = (await _repo.GetAllAsync()).Single().Id;

            await _repo.PermanentlyDeleteAsync(id);

            Assert.Empty(await _repo.GetAllAsync());
            Assert.Empty(await _repo.GetTrashAsync());
        }

        [Fact]
        public async Task PurgeExpiredTrashAsync_borra_solo_lo_que_supera_el_periodo_de_retencion()
        {
            await _repo.AddAsync(NewGame("Reciente"));
            await _repo.AddAsync(NewGame("Antiguo"));
            var all = await _repo.GetAllAsync();
            var recienteId = all.First(g => g.Title == "Reciente").Id;
            var antiguoId = all.First(g => g.Title == "Antiguo").Id;

            await _repo.DeleteAsync(recienteId);
            await _repo.DeleteAsync(antiguoId);

            // Simula que "Antiguo" lleva más días en la papelera que el periodo de retención
            var antiguo = await _db.Games.FindAsync(antiguoId);
            antiguo!.DeletedDate = DateTime.Now.AddDays(-(GameRepository.TrashRetentionDays + 1));
            await _db.SaveChangesAsync();

            var purged = await _repo.PurgeExpiredTrashAsync();

            Assert.Equal(1, purged);
            var trash = await _repo.GetTrashAsync();
            Assert.Equal("Reciente", Assert.Single(trash).Title);
        }

        [Fact]
        public async Task UpdateAsync_preserva_la_fecha_de_alta_original()
        {
            await _repo.AddAsync(NewGame("Juego"));
            var original = (await _repo.GetAllAsync()).Single();
            var addedDate = original.AddedDate;

            // El diálogo de edición nunca conoce la fecha de alta real: siempre trae el valor
            // por defecto (ahora). UpdateAsync debe ignorarlo y conservar el original.
            original.Title = "Juego editado";
            original.AddedDate = DateTime.Now.AddYears(10);
            await _repo.UpdateAsync(original);

            var updated = (await _repo.GetAllAsync()).Single();
            Assert.Equal("Juego editado", updated.Title);
            Assert.Equal(addedDate, updated.AddedDate, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetByBarcodeAsync_no_encuentra_juegos_en_la_papelera()
        {
            await _repo.AddAsync(NewGame("Juego", barcode: "999"));
            var id = (await _repo.GetAllAsync()).Single().Id;
            await _repo.DeleteAsync(id);

            var found = await _repo.GetByBarcodeAsync("999");

            Assert.Null(found);
        }

        [Fact]
        public async Task ImportAsync_cuenta_anadidos_y_duplicados_sin_descartar_el_resto_del_lote()
        {
            await _repo.AddAsync(NewGame("Ya existente", barcode: "AAA"));

            var batch = new List<Game>
            {
                NewGame("Nuevo 1", barcode: "BBB"),
                NewGame("Duplicado", barcode: "AAA"), // choca con el ya existente
                NewGame("Nuevo 2", barcode: "CCC"),
            };

            var (added, duplicates) = await _repo.ImportAsync(batch);

            Assert.Equal(2, added);
            Assert.Equal(1, duplicates);
            Assert.Equal(3, (await _repo.GetAllAsync()).Count); // 1 original + 2 nuevos
        }

        [Fact]
        public async Task GetCollectionNameAsync_y_SetCollectionNameAsync_persisten_el_nombre()
        {
            Assert.Equal(string.Empty, await _repo.GetCollectionNameAsync());

            await _repo.SetCollectionNameAsync("Mis juegos de PS5");
            Assert.Equal("Mis juegos de PS5", await _repo.GetCollectionNameAsync());

            await _repo.SetCollectionNameAsync("Renombrada");
            Assert.Equal("Renombrada", await _repo.GetCollectionNameAsync());
        }
    }
}
