using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.Services
{
    public class GameApiService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        static GameApiService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "VideoGameLibrary/1.0 (contact@videogamelibrary.app)");
        }

        // Claves de API, todas opcionales — si están vacías esa fuente se salta sin error
        private string _scanDexToken;
        private string _igdbClientId;
        private string _igdbClientSecret;
        private string _rawgApiKey;
        private string _theGamesDbApiKey;

        public GameApiService(string scanDexToken = "", string igdbClientId = "", string igdbClientSecret = "",
                               string rawgApiKey = "", string theGamesDbApiKey = "")
        {
            _scanDexToken = scanDexToken;
            _igdbClientId = igdbClientId;
            _igdbClientSecret = igdbClientSecret;
            _rawgApiKey = rawgApiKey;
            _theGamesDbApiKey = theGamesDbApiKey;
        }

        // Permite aplicar claves nuevas sin reiniciar la app (usado desde la ventana de Ajustes)
        public void UpdateKeys(string scanDexToken, string igdbClientId, string igdbClientSecret,
                                string rawgApiKey, string theGamesDbApiKey)
        {
            _scanDexToken = scanDexToken;
            _igdbClientId = igdbClientId;
            _igdbClientSecret = igdbClientSecret;
            _rawgApiKey = rawgApiKey;
            _theGamesDbApiKey = theGamesDbApiKey;
        }

        // Un código de barras normalmente resuelve a un único producto, pero
        // en casos raros (reediciones, bundles, colisiones entre tiendas) puede
        // haber más de un candidato — el llamador decide cómo resolver la ambigüedad.
        private const int MaxCandidates = 6;

        // ── Punto de entrada principal ────────────────────────────────────────

        public async Task<List<Game>> SearchCandidatesByBarcodeAsync(string rawBarcode, CancellationToken ct = default)
        {
            var barcode = NormalizeBarcode(rawBarcode);
            if (string.IsNullOrEmpty(barcode)) return new List<Game>();

            var variants = GetBarcodeVariants(barcode);
            var candidates = new List<Game>();

            // Fase A — resolución por código de barras, prioridad fija: ScanDex primero
            if (!string.IsNullOrEmpty(_scanDexToken))
            {
                foreach (var variant in variants)
                {
                    var game = await SearchScanDexAsync(variant, ct);
                    if (game != null) { candidates.Add(game); break; }
                }
            }

            if (candidates.Count == 0)
            {
                foreach (var variant in variants)
                {
                    var items = await SearchUpcItemDbCandidatesAsync(variant, ct);
                    if (items.Count > 0) { candidates.AddRange(items); break; }
                }
            }

            // Fase B — enriquecimiento por nombre de cada candidato, solo rellena huecos
            foreach (var candidate in candidates)
            {
                candidate.Barcode = barcode;
                await EnrichFromNameAsync(candidate, ct);
            }

            return candidates;
        }

        // ── Fase A: fuentes que aceptan código de barras directamente ──────────

        // ScanDex — base de datos de códigos de barras específica de videojuegos, con metadatos IGDB
        private async Task<Game?> SearchScanDexAsync(string barcode, CancellationToken ct)
        {
            try
            {
                var url = $"https://scandex.gamery.app/api/v2/lookup?value={barcode}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", _scanDexToken);

                var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await response.Content.ReadAsStringAsync(ct));
                var igdbMeta = json["igdb_metadata"];
                if (igdbMeta == null) return null;

                var game = new Game { Barcode = barcode };
                game.Title = igdbMeta["name"]?.ToString() ?? string.Empty;
                game.Platform = igdbMeta["platform"]?["name"]?.ToString() ?? string.Empty;

                return string.IsNullOrEmpty(game.Title) ? null : game;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"ScanDex — búsqueda por código de barras {barcode}", ex);
                return null;
            }
        }

        // UPCitemdb — base de datos genérica de códigos de barras, sin registro.
        // Un mismo UPC puede listar varios productos (reediciones, bundles) — se devuelven todos.
        private async Task<List<Game>> SearchUpcItemDbCandidatesAsync(string barcode, CancellationToken ct)
        {
            try
            {
                var url = $"https://api.upcitemdb.com/prod/trial/lookup?upc={barcode}";
                var response = await _http.GetStringAsync(url, ct);
                var json = JObject.Parse(response);

                var items = json["items"] as JArray;
                if (items == null) return new List<Game>();

                var result = new List<Game>();
                foreach (var item in items.Take(MaxCandidates))
                {
                    var title = item["title"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(title)) continue;

                    var images = item["images"] as JArray;
                    result.Add(new Game
                    {
                        Barcode = barcode,
                        Title = title,
                        CoverUrl = images?.FirstOrDefault()?.ToString() ?? string.Empty
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"UPCitemdb — búsqueda por código de barras {barcode}", ex);
                return new List<Game>();
            }
        }

        // Búsqueda manual por título cuando el código de barras no da resultado, o desde el
        // diálogo "Buscar por nombre" — un título puede coincidir con varios juegos distintos.
        public async Task<List<Game>> SearchByNameCandidatesAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return new List<Game>();

            var trimmed = name.Trim();

            var fromIgdb = await SearchIgdbCandidatesAsync(trimmed, ct);
            if (fromIgdb.Count > 0) return fromIgdb;

            return await SearchRawgCandidatesAsync(trimmed, ct);
        }

        // ── Fase B: enriquecimiento por nombre, solo rellena huecos ────────────

        private async Task EnrichFromNameAsync(Game game, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(game.Title)) return;

            if (!await EnrichFromIgdbAsync(game, ct))
                if (!await EnrichFromRawgAsync(game, ct))
                    await EnrichFromTheGamesDbAsync(game, ct);

            if (!string.IsNullOrEmpty(game.CoverUrl) && (game.CoverData == null || game.CoverData.Length == 0))
                game.CoverData = await DownloadCoverAsync(game.CoverUrl, ct);
        }

        private string? _igdbToken;
        private DateTime _igdbTokenExpiryUtc = DateTime.MinValue;

        private async Task<string?> GetIgdbTokenAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_igdbClientId) || string.IsNullOrEmpty(_igdbClientSecret)) return null;
            if (_igdbToken != null && DateTime.UtcNow < _igdbTokenExpiryUtc) return _igdbToken;

            try
            {
                var url = $"https://id.twitch.tv/oauth2/token?client_id={_igdbClientId}&client_secret={_igdbClientSecret}&grant_type=client_credentials";
                var response = await _http.PostAsync(url, null, ct);
                var json = JObject.Parse(await response.Content.ReadAsStringAsync(ct));

                _igdbToken = json["access_token"]?.ToString();
                var expiresIn = json["expires_in"]?.Value<int>() ?? 0;
                _igdbTokenExpiryUtc = DateTime.UtcNow.AddSeconds(Math.Max(expiresIn - 60, 0));

                return _igdbToken;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError("IGDB — obtención de token OAuth", ex);
                return null;
            }
        }

        // IGDB — no busca por código de barras, pero da la mejor portada/género/plataforma por nombre
        private async Task<bool> EnrichFromIgdbAsync(Game game, CancellationToken ct)
        {
            try
            {
                var token = await GetIgdbTokenAsync(ct);
                if (token == null) return false;

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
                request.Headers.Add("Client-ID", _igdbClientId);
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Content = new StringContent(
                    $"search \"{game.Title}\"; fields name,cover.url,genres.name,platforms.name,first_release_date; limit 1;");

                var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return false;

                var json = JArray.Parse(await response.Content.ReadAsStringAsync(ct));
                if (json.Count == 0) return false;

                var result = json[0]!;
                bool filled = false;

                if (string.IsNullOrEmpty(game.CoverUrl))
                {
                    var coverUrl = result["cover"]?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        game.CoverUrl = "https:" + coverUrl.Replace("t_thumb", "t_cover_big");
                        filled = true;
                    }
                }

                if (string.IsNullOrEmpty(game.Genre))
                {
                    var genres = result["genres"] as JArray;
                    if (genres?.Count > 0)
                    {
                        game.Genre = string.Join(", ", genres.Select(g => g["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));
                        filled = true;
                    }
                }

                if (string.IsNullOrEmpty(game.Platform))
                {
                    var platforms = result["platforms"] as JArray;
                    if (platforms?.Count > 0)
                    {
                        game.Platform = string.Join(", ", platforms.Select(p => p["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));
                        filled = true;
                    }
                }

                if (!game.Year.HasValue)
                {
                    var releaseDate = result["first_release_date"]?.Value<long?>();
                    if (releaseDate.HasValue)
                    {
                        game.Year = DateTimeOffset.FromUnixTimeSeconds(releaseDate.Value).Year;
                        filled = true;
                    }
                }

                return filled;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"IGDB — enriquecimiento por nombre \"{game.Title}\"", ex);
                return false;
            }
        }

        // IGDB — búsqueda por nombre que devuelve varios candidatos (a diferencia de
        // EnrichFromIgdbAsync, que solo rellena huecos de un juego ya identificado)
        private async Task<List<Game>> SearchIgdbCandidatesAsync(string name, CancellationToken ct)
        {
            try
            {
                var token = await GetIgdbTokenAsync(ct);
                if (token == null) return new List<Game>();

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
                request.Headers.Add("Client-ID", _igdbClientId);
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Content = new StringContent(
                    $"search \"{name}\"; fields name,cover.url,genres.name,platforms.name,first_release_date; limit {MaxCandidates};");

                var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return new List<Game>();

                var json = JArray.Parse(await response.Content.ReadAsStringAsync(ct));
                var result = new List<Game>();

                foreach (var item in json)
                {
                    var title = item["name"]?.ToString();
                    if (string.IsNullOrEmpty(title)) continue;

                    var game = new Game { Title = title };

                    var coverUrl = item["cover"]?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(coverUrl))
                        game.CoverUrl = "https:" + coverUrl.Replace("t_thumb", "t_cover_big");

                    var genres = item["genres"] as JArray;
                    if (genres?.Count > 0)
                        game.Genre = string.Join(", ", genres.Select(g => g["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));

                    var platforms = item["platforms"] as JArray;
                    if (platforms?.Count > 0)
                        game.Platform = string.Join(", ", platforms.Select(p => p["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));

                    var releaseDate = item["first_release_date"]?.Value<long?>();
                    if (releaseDate.HasValue)
                        game.Year = DateTimeOffset.FromUnixTimeSeconds(releaseDate.Value).Year;

                    result.Add(game);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"IGDB — búsqueda por nombre \"{name}\"", ex);
                return new List<Game>();
            }
        }

        // RAWG — segunda fuente de enriquecimiento por nombre
        private async Task<bool> EnrichFromRawgAsync(Game game, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(_rawgApiKey)) return false;

                var url = $"https://api.rawg.io/api/games?search={Uri.EscapeDataString(game.Title)}&key={_rawgApiKey}&page_size=1";
                var response = await _http.GetStringAsync(url, ct);
                var json = JObject.Parse(response);

                var results = json["results"] as JArray;
                if (results == null || results.Count == 0) return false;

                var result = results[0]!;
                bool filled = false;

                if (string.IsNullOrEmpty(game.CoverUrl))
                {
                    var image = result["background_image"]?.ToString();
                    if (!string.IsNullOrEmpty(image)) { game.CoverUrl = image; filled = true; }
                }

                if (string.IsNullOrEmpty(game.Genre))
                {
                    var genres = result["genres"] as JArray;
                    if (genres?.Count > 0)
                    {
                        game.Genre = string.Join(", ", genres.Select(g => g["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));
                        filled = true;
                    }
                }

                if (string.IsNullOrEmpty(game.Platform))
                {
                    var platforms = result["platforms"] as JArray;
                    if (platforms?.Count > 0)
                    {
                        game.Platform = string.Join(", ", platforms.Select(p => p["platform"]?["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));
                        filled = true;
                    }
                }

                if (!game.Year.HasValue)
                {
                    var released = result["released"]?.ToString();
                    game.Year = ExtractYear(released ?? "");
                    if (game.Year.HasValue) filled = true;
                }

                return filled;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"RAWG — enriquecimiento por nombre \"{game.Title}\"", ex);
                return false;
            }
        }

        // RAWG — búsqueda por nombre que devuelve varios candidatos (fallback si IGDB no da resultados)
        private async Task<List<Game>> SearchRawgCandidatesAsync(string name, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(_rawgApiKey)) return new List<Game>();

                var url = $"https://api.rawg.io/api/games?search={Uri.EscapeDataString(name)}&key={_rawgApiKey}&page_size={MaxCandidates}";
                var response = await _http.GetStringAsync(url, ct);
                var json = JObject.Parse(response);

                var results = json["results"] as JArray;
                if (results == null) return new List<Game>();

                var list = new List<Game>();
                foreach (var item in results)
                {
                    var title = item["name"]?.ToString();
                    if (string.IsNullOrEmpty(title)) continue;

                    var game = new Game { Title = title };

                    var image = item["background_image"]?.ToString();
                    if (!string.IsNullOrEmpty(image)) game.CoverUrl = image;

                    var genres = item["genres"] as JArray;
                    if (genres?.Count > 0)
                        game.Genre = string.Join(", ", genres.Select(g => g["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));

                    var platforms = item["platforms"] as JArray;
                    if (platforms?.Count > 0)
                        game.Platform = string.Join(", ", platforms.Select(p => p["platform"]?["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)));

                    game.Year = ExtractYear(item["released"]?.ToString() ?? "");

                    list.Add(game);
                }
                return list;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"RAWG — búsqueda por nombre \"{name}\"", ex);
                return new List<Game>();
            }
        }

        // TheGamesDB — último recurso, solo portada (su búsqueda por código de barras está rota)
        private async Task<bool> EnrichFromTheGamesDbAsync(Game game, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(_theGamesDbApiKey) || !string.IsNullOrEmpty(game.CoverUrl)) return false;

                var url = $"https://api.thegamesdb.net/v1/Games/ByGameName?apikey={_theGamesDbApiKey}&name={Uri.EscapeDataString(game.Title)}&include=boxart";
                var response = await _http.GetStringAsync(url, ct);
                var json = JObject.Parse(response);

                var games = json["data"]?["games"] as JArray;
                var gameId = games?.FirstOrDefault()?["id"]?.ToString();
                if (string.IsNullOrEmpty(gameId)) return false;

                var baseUrl = json["include"]?["boxart"]?["base_url"]?["original"]?.ToString();
                var boxartData = json["include"]?["boxart"]?["data"]?[gameId] as JArray;
                var boxartFile = boxartData?.FirstOrDefault(b => b["side"]?.ToString() == "front")?["filename"]?.ToString()
                                  ?? boxartData?.FirstOrDefault()?["filename"]?.ToString();

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(boxartFile)) return false;

                game.CoverUrl = baseUrl + boxartFile;
                return true;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"TheGamesDB — enriquecimiento por nombre \"{game.Title}\"", ex);
                return false;
            }
        }

        // ── Normalización y variantes de código de barras ───────────────────────

        public static string NormalizeBarcode(string raw)
        {
            return new string(raw.Where(char.IsDigit).ToArray());
        }

        // UPC-A (12 dígitos) y EAN-13 (13 dígitos, común en juegos PAL/España) son el mismo
        // código salvo un "0" inicial — se prueban ambas formas contra cada API.
        // internal (no private) solo para poder testearlo desde VideoGameLibrary.Tests.
        internal static List<string> GetBarcodeVariants(string barcode)
        {
            var variants = new List<string> { barcode };

            if (barcode.Length == 12)
                variants.Add("0" + barcode);
            else if (barcode.Length == 13 && barcode.StartsWith("0"))
                variants.Add(barcode[1..]);

            return variants;
        }

        // ── Descarga de portada ───────────────────────────────────────────────

        public async Task<byte[]?> DownloadCoverAsync(string url, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                return await _http.GetByteArrayAsync(url, ct);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                LoggingService.LogError($"Descarga de portada {url}", ex);
                return null;
            }
        }

        // ── Utilidades ────────────────────────────────────────────────────────

        // internal (no private) solo para poder testearlo desde VideoGameLibrary.Tests.
        internal static int? ExtractYear(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var digits = new string(text.Where(char.IsDigit).ToArray());
            for (int i = 0; i <= digits.Length - 4; i++)
            {
                if (int.TryParse(digits.Substring(i, 4), out int y) && y >= 1970 && y <= DateTime.Now.Year + 1)
                    return y;
            }
            return null;
        }
    }
}
