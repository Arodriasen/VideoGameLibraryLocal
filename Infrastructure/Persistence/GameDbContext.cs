using Microsoft.EntityFrameworkCore;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Infrastructure.Persistence
{
    public class GameDbContext : DbContext
    {
        private readonly string _dbPath;

        public GameDbContext(string dbPath)
        {
            _dbPath = dbPath;
        }

        public DbSet<Game> Games { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Índice único FILTRADO (índice parcial en SQLite): solo cuenta entre juegos activos
            // ("DeletedDate" IS NULL). Sin el filtro, un juego borrado (borrado suave -- sigue en la
            // fila, solo pasa a la papelera) seguía "ocupando" su código de barras, y volver a
            // escanearlo antes de vaciar la papelera o de que caduque (7 días) hacía fallar el
            // guardado con un DbUpdateException de violación de índice único, aunque
            // GetByBarcodeAsync (el aviso de "ya lo tienes") sí ignora la papelera -- las dos
            // comprobaciones no eran consistentes entre sí. SQLite ya permite varias filas NULL en
            // un índice único normal, así que no hace falta nada más para los juegos sin Barcode.
            modelBuilder.Entity<Game>()
                .HasIndex(g => g.Barcode)
                .IsUnique()
                .HasFilter("\"DeletedDate\" IS NULL");
        }
    }
}
