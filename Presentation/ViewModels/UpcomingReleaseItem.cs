using CommunityToolkit.Mvvm.ComponentModel;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Presentation.ViewModels
{
    // Envuelve un UpcomingRelease (DTO plano de IGDB) con el estado de "ya añadido" que necesita
    // la UI del calendario — no vive en Models porque es puramente de presentación, igual que
    // CalendarDayCell envuelve la fecha con IsSelected. La misma instancia se reutiliza mientras
    // el mes siga en caché (ver CalendarViewModel._releaseItemCache), así que el estado sobrevive
    // a un cambio de filtro de plataformas o a volver a un mes ya visitado.
    public partial class UpcomingReleaseItem : ObservableObject
    {
        public UpcomingRelease Release { get; }

        public string Title => Release.Title;
        public string PlatformName => Release.PlatformName;
        public string? CoverUrl => Release.CoverUrl;

        [ObservableProperty] private bool _isInWishlist;
        [ObservableProperty] private bool _isInCollection;

        // Id del Game creado al añadir desde el calendario — hace falta para poder quitarlo (un
        // segundo clic sobre el mismo botón) sin tener que volver a buscarlo por título/plataforma.
        public int? WishlistGameId { get; set; }
        public int? CollectionGameId { get; set; }

        public UpcomingReleaseItem(UpcomingRelease release)
        {
            Release = release;
        }
    }
}
