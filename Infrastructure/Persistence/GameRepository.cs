using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VideoGameLibrary.Domain.Entities;
using VideoGameLibrary.Domain.Repositories;

namespace VideoGameLibrary.Infrastructure.Persistence
{
    public class GameRepository : IGameRepository
    {
        private readonly GameDbContext _db;

        // Un DbContext no admite dos operaciones a la vez, y este vive durante toda la sesión y lo
        // comparten todas las ventanas. Sin este semáforo, abrir p. ej. el calendario mientras la
        // lista principal aún está cargando lanzaba "A second operation was started on this
        // context instance". Ahora cada operación espera su turno en vez de fallar.
        private readonly SemaphoreSlim _gate = new(1, 1);

        public GameRepository(GameDbContext db)
        {
            _db = db;
            MigrateDatabase();
            EnsureCollectionSettingsTable();
        }

        public void Dispose() => _db.Dispose();

        private async Task<T> RunExclusiveAsync<T>(Func<Task<T>> operation)
        {
            await _gate.WaitAsync();
            try { return await operation(); }
            finally { _gate.Release(); }
        }

        private async Task RunExclusiveAsync(Func<Task> operation)
        {
            await _gate.WaitAsync();
            try { await operation(); }
            finally { _gate.Release(); }
        }

        // Aplica las migraciones de EF Core. Las bases de datos creadas con versiones anteriores de la
        // app (antes de introducir migraciones formales) se generaron con Database.EnsureCreated() y
        // columnas añadidas a mano por ALTER TABLE -- no tienen la tabla __EFMigrationsHistory. Si se
        // dejara que Migrate() actuase directamente sobre ellas, intentaría volver a crear la tabla
        // Games (ya existente, con datos reales) y fallaría. En ese caso se marca la migración inicial
        // como ya aplicada -- su esquema coincide exactamente con lo que ya hay -- sin ejecutarla; a
        // partir de ahí Migrate() aplica con normalidad el resto de migraciones.
        private void MigrateDatabase()
        {
            var historyRepository = _db.GetService<IHistoryRepository>();

            if (!historyRepository.Exists())
            {
                var gamesTableExists = _db.Database.SqlQueryRaw<string>(
                    "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'Games'").AsEnumerable().Any();

                if (gamesTableExists)
                {
                    // Los ids de migración empiezan por la fecha, así que el primero es InitialCreate
                    var initialMigrationId = _db.GetService<IMigrationsAssembly>().Migrations.Keys.First();
                    _db.Database.ExecuteSqlRaw(historyRepository.GetCreateScript());
                    _db.Database.ExecuteSqlRaw(historyRepository.GetInsertScript(new HistoryRow(initialMigrationId, "8.0.8")));
                }
            }

            _db.Database.Migrate();
        }

        // Tabla de una sola fila con el nombre visible de la colección (independiente del
        // nombre del archivo .db, para poder renombrar uno sin afectar al otro)
        private void EnsureCollectionSettingsTable()
        {
            _db.Database.ExecuteSqlRaw(
                "CREATE TABLE IF NOT EXISTS CollectionSettings (Id INTEGER PRIMARY KEY CHECK (Id = 1), Name TEXT NOT NULL DEFAULT '')");
        }

        public Task<string> GetCollectionNameAsync() => RunExclusiveAsync(async () =>
        {
            var rows = await _db.Database.SqlQueryRaw<string>("SELECT Name FROM CollectionSettings WHERE Id = 1").ToListAsync();
            return rows.FirstOrDefault() ?? string.Empty;
        });

        public Task SetCollectionNameAsync(string name) => RunExclusiveAsync(() =>
            _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO CollectionSettings (Id, Name) VALUES (1, {0}) ON CONFLICT(Id) DO UPDATE SET Name = {0}", name));

        public Task<List<Game>> GetAllAsync() => RunExclusiveAsync(() =>
            _db.Games.AsNoTracking().Where(g => g.DeletedDate == null).OrderBy(g => g.Title).ToListAsync());

        public Task<Game?> GetByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrEmpty(barcode)) return Task.FromResult<Game?>(null);
            return RunExclusiveAsync(() => _db.Games.FirstOrDefaultAsync(g => g.Barcode == barcode && g.DeletedDate == null));
        }

        public Task AddAsync(Game game) => RunExclusiveAsync(async () =>
        {
            _db.Games.Add(game);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // El _db vive durante toda la sesión de la app (ver comentario en MigrateDatabase):
                // si no se suelta aquí, la entidad fallida se queda en Added para siempre y revienta
                // el SIGUIENTE SaveChangesAsync de cualquier operación, aunque no tenga nada que ver
                // (por eso un "código duplicado" al añadir podía bloquear luego un borrado normal).
                _db.Entry(game).State = EntityState.Detached;
                throw;
            }
        });

        public Task UpdateAsync(Game game) => RunExclusiveAsync(async () =>
        {
            // El DbContext vive durante toda la sesión de la app, así que una edición anterior
            // del mismo juego puede seguir bajo seguimiento con otra instancia distinta.
            // Hay que soltarla antes de adjuntar la nueva o EF Core lanza un conflicto de clave.
            var tracked = _db.ChangeTracker.Entries<Game>()
                .FirstOrDefault(e => e.Entity.Id == game.Id && e.Entity != game);
            if (tracked != null)
                tracked.State = EntityState.Detached;

            // El diálogo de edición no conoce la fecha de alta original (siempre trae el valor
            // por defecto = ahora); hay que preservarla o cada edición la pisaría con la fecha actual.
            game.AddedDate = await _db.Games.AsNoTracking()
                .Where(g => g.Id == game.Id)
                .Select(g => g.AddedDate)
                .FirstOrDefaultAsync();

            _db.Games.Update(game);
            await _db.SaveChangesAsync();
        });

        // Borrado suave: el juego pasa a la papelera (ver GetTrashAsync) en vez de borrarse
        // de verdad, para poder recuperarlo. PurgeExpiredTrashAsync limpia lo antiguo.
        public Task DeleteAsync(int id) => RunExclusiveAsync(async () =>
        {
            var game = await _db.Games.FindAsync(id);
            if (game != null)
            {
                game.DeletedDate = DateTime.Now;
                await _db.SaveChangesAsync();
            }
        });

        public Task RestoreAsync(int id) => RunExclusiveAsync(async () =>
        {
            var game = await _db.Games.FindAsync(id);
            if (game != null)
            {
                game.DeletedDate = null;
                await _db.SaveChangesAsync();
            }
        });

        public Task<List<Game>> GetTrashAsync() => RunExclusiveAsync(() =>
            _db.Games.AsNoTracking()
                .Where(g => g.DeletedDate != null)
                .OrderByDescending(g => g.DeletedDate)
                .ToListAsync());

        // Borrado real, sin paso por la papelera — usado por "Eliminar definitivamente",
        // "Vaciar papelera" y por la limpieza automática de la papelera caducada.
        public Task PermanentlyDeleteAsync(int id) => RunExclusiveAsync(async () =>
        {
            var game = await _db.Games.FindAsync(id);
            if (game != null)
            {
                _db.Games.Remove(game);
                await _db.SaveChangesAsync();
            }
        });

        // Se llama al arrancar la app: borra de verdad lo que lleva más de retentionDays en la papelera
        public Task<int> PurgeExpiredTrashAsync(int retentionDays = IGameRepository.TrashRetentionDays) => RunExclusiveAsync(async () =>
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            var expired = await _db.Games.Where(g => g.DeletedDate != null && g.DeletedDate < cutoff).ToListAsync();
            if (expired.Count == 0) return 0;

            _db.Games.RemoveRange(expired);
            await _db.SaveChangesAsync();
            return expired.Count;
        });

        // Inserta varios juegos importados de golpe. Cada uno se guarda por separado para que
        // un código de barras duplicado no descarte el resto del lote; la entidad fallida se
        // suelta del seguimiento del contexto (si no, EF reintentaría guardarla en cada fila siguiente).
        public Task<(int Added, int Duplicates)> ImportAsync(IEnumerable<Game> games) => RunExclusiveAsync(async () =>
        {
            int added = 0, duplicates = 0;

            foreach (var game in games)
            {
                _db.Games.Add(game);
                try
                {
                    await _db.SaveChangesAsync();
                    added++;
                }
                catch (DbUpdateException)
                {
                    _db.Entry(game).State = EntityState.Detached;
                    duplicates++;
                }
            }

            return (added, duplicates);
        });

        // Compacta el archivo .db, eliminando físicamente los restos de los registros borrados.
        // Reescribe el archivo entero — se deja como acción manual (ver Ajustes) en vez de automática
        // para que no penalice el rendimiento si la colección crece mucho.
        public Task VacuumAsync() => RunExclusiveAsync(() => _db.Database.ExecuteSqlRawAsync("VACUUM"));

        // Copia de seguridad consistente del .db mientras la app lo sigue usando: VACUUM INTO es
        // el mecanismo nativo de SQLite para esto (usa su propio backup API por debajo), más seguro
        // que copiar el archivo a nivel de sistema de ficheros con la conexión todavía abierta.
        // Como efecto secundario también compacta la copia (no el archivo original).
        public Task BackupToAsync(string destinationPath) => RunExclusiveAsync(() =>
            _db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", destinationPath));
    }
}
