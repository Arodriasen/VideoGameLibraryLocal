using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace VideoGameLibrary.Services
{
    public record UpdateInfo(string Version, string Url);

    // Comprueba en GitHub Releases si hay una versión más nueva que la instalada.
    // No requiere token (API pública de GitHub) y falla en silencio si no hay red o el repo aún no tiene releases.
    public static class UpdateCheckService
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/Arodriasen/VideoGameLibrary/releases/latest";

        // Instancia compartida (mismo patrón que GameApiService._http) en vez de una nueva por
        // llamada -- aquí solo se llama una vez al arrancar, pero crear un HttpClient por llamada
        // es el antipatrón clásico que agota sockets si el código se reutiliza en otro sitio.
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        static UpdateCheckService()
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VideoGameLibrary", GetCurrentVersion().ToString()));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var response = await _http.GetAsync(ReleasesApiUrl);
                if (!response.IsSuccessStatusCode) return null; // repo sin releases todavía (404) u otro fallo puntual

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var tag = doc.RootElement.GetProperty("tag_name").GetString();
                var url = doc.RootElement.GetProperty("html_url").GetString();
                if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(url)) return null;

                var latest = ParseVersion(tag);
                if (latest == null || latest <= GetCurrentVersion()) return null;

                return new UpdateInfo(tag, url);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Comprobación de actualizaciones", ex);
                return null;
            }
        }

        private static Version GetCurrentVersion()
            => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        private static Version? ParseVersion(string tag)
        {
            var cleaned = tag.TrimStart('v', 'V');
            return Version.TryParse(cleaned, out var v) ? v : null;
        }
    }
}
