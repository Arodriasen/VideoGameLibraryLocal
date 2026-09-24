using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Application.Abstractions
{
    public interface IGameApiService
    {
        // Permite aplicar claves nuevas sin reiniciar la app (usado desde la ventana de Ajustes)
        void UpdateKeys(string scanDexToken, string igdbClientId, string igdbClientSecret,
                         string rawgApiKey, string theGamesDbApiKey);

        Task<List<Game>> SearchCandidatesByBarcodeAsync(string rawBarcode, CancellationToken ct = default);
        Task<List<Game>> SearchByNameCandidatesAsync(string name, CancellationToken ct = default);

        Task<List<UpcomingRelease>> GetUpcomingReleasesAsync(
            DateTime monthStartUtc, DateTime monthEndUtc, IEnumerable<int>? platformIds, CancellationToken ct = default);

        Task<byte[]?> DownloadCoverAsync(string url, CancellationToken ct = default);
    }
}
