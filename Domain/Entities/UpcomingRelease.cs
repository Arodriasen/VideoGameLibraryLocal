using System;

namespace VideoGameLibrary.Domain.Entities
{
    // Una entrada del calendario de próximos lanzamientos (IGDB release_dates) —
    // no reutiliza Game porque representa fecha+plataforma de un lanzamiento, no un juego catalogado.
    public class UpcomingRelease
    {
        public string Title { get; set; } = string.Empty;

        // Puede combinar varias plataformas separadas por coma (igual que Game.Platform) cuando el
        // mismo juego sale el mismo día en más de una — ver GameApiService.GetUpcomingReleasesAsync.
        public string PlatformName { get; set; } = string.Empty;
        public DateTime ReleaseDateUtc { get; set; }
        public string? CoverUrl { get; set; }
    }
}
