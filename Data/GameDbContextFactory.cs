using Microsoft.EntityFrameworkCore.Design;

namespace VideoGameLibrary.Data
{
    // Solo la usan las herramientas de EF Core en tiempo de diseño (dotnet ef migrations add/...),
    // que necesitan crear un GameDbContext sin pasar por App.xaml.cs. La ruta no abre ningún
    // archivo real, solo hace falta para poder construir el contexto y leer el modelo.
    public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext(string[] args) => new GameDbContext("design-time.db");
    }
}
