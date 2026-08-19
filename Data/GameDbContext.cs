using System.IO;
using Microsoft.EntityFrameworkCore;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.Data
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
            modelBuilder.Entity<Game>()
                .HasIndex(g => g.Barcode)
                .IsUnique();
        }
    }
}
