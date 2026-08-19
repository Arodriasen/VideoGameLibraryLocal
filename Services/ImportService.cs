using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.Services
{
    public enum ImportItemStatus { Nuevo, YaExiste, DuplicadoEnArchivo }

    // Lee un CSV (separado por ";", mismo formato que ExportService) o un Excel (.xlsx)
    // con una fila de cabecera. Qué columna del archivo corresponde a qué campo lo decide
    // el usuario en ImportColumnMappingDialog — con una sugerencia automática por nombre de
    // cabecera (GuessMapping) como punto de partida, así funciona igual de bien con archivos
    // que exporta esta misma app y con archivos de otras apps que usen otros nombres de columna.
    // Única columna obligatoria: "Título".
    public class ImportService
    {
        // Clave interna, etiqueta visible, y si es obligatorio para poder importar la fila.
        public static readonly (string Key, string Label, bool Required)[] Fields =
        {
            ("código de barras", "Código de Barras", false),
            ("título", "Título", true),
            ("plataforma", "Plataforma", false),
            ("editorial", "Editorial", false),
            ("género", "Género", false),
            ("etiquetas", "Etiquetas", false),
            ("año", "Año", false),
            ("puntuación", "Puntuación", false),
            ("notas", "Notas", false),
            ("jugado", "Jugado", false),
        };

        public List<string> ReadHeaders(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".csv", System.StringComparison.OrdinalIgnoreCase)
                ? ReadCsvHeaders(filePath)
                : ReadExcelHeaders(filePath);
        }

        private static List<string> ReadCsvHeaders(string filePath)
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length == 0) return new List<string>();

            int start = lines[0].TrimStart().StartsWith("sep=", System.StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return start < lines.Length ? SplitCsvLine(lines[start]) : new List<string>();
        }

        private static List<string> ReadExcelHeaders(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();

            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var headers = new List<string>();
            for (int col = 1; col <= lastCol; col++)
                headers.Add(sheet.Cell(1, col).GetString());
            return headers;
        }

        // Sugerencia automática: empareja cabeceras cuyo texto coincide exactamente (sin
        // mayúsculas) con una de las claves reconocidas. El usuario la confirma o la corrige
        // a mano en ImportColumnMappingDialog antes de importar nada.
        public static Dictionary<string, int> GuessMapping(List<string> headers)
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
            {
                var key = headers[i].Trim().ToLowerInvariant();
                if (Fields.Any(f => f.Key == key) && !map.ContainsKey(key))
                    map[key] = i;
            }
            return map;
        }

        // mapping: clave de campo (p.ej. "título") -> índice de columna en el archivo (0-based).
        // Los campos ausentes del diccionario se dejan vacíos.
        public List<Game> ParseFile(string filePath, Dictionary<string, int> mapping)
        {
            return Path.GetExtension(filePath).Equals(".csv", System.StringComparison.OrdinalIgnoreCase)
                ? ParseCsv(filePath, mapping)
                : ParseExcel(filePath, mapping);
        }

        // Atajo cuando no hace falta preguntar al usuario: adivina el mapeo por nombre de
        // cabecera y parsea directamente. Útil para archivos ya generados por esta misma app.
        public List<Game> ParseFile(string filePath)
            => ParseFile(filePath, GuessMapping(ReadHeaders(filePath)));

        private List<Game> ParseCsv(string filePath, Dictionary<string, int> mapping)
        {
            var result = new List<Game>();
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length == 0) return result;

            int start = lines[0].TrimStart().StartsWith("sep=", System.StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (start >= lines.Length) return result;
            if (!mapping.ContainsKey("título")) return result;

            for (int i = start + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var fields = SplitCsvLine(lines[i]);
                var game = BuildGame(field => GetField(fields, mapping, field));
                if (game != null) result.Add(game);
            }

            return result;
        }

        private List<Game> ParseExcel(string filePath, Dictionary<string, int> mapping)
        {
            var result = new List<Game>();
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();

            if (!mapping.ContainsKey("título")) return result;

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (int row = 2; row <= lastRow; row++)
            {
                int currentRow = row;
                var game = BuildGame(field =>
                    mapping.TryGetValue(field, out var col) ? sheet.Cell(currentRow, col + 1).GetString() : null);
                if (game != null) result.Add(game);
            }

            return result;
        }

        // ── Construcción de un Game a partir de un lector de campos por clave de campo ──

        private static Game? BuildGame(System.Func<string, string?> field)
        {
            var title = field("título")?.Trim();
            if (string.IsNullOrWhiteSpace(title)) return null;

            var game = new Game { Title = title };

            var barcode = field("código de barras")?.Trim();
            game.Barcode = string.IsNullOrWhiteSpace(barcode) ? null : GameApiService.NormalizeBarcode(barcode);

            game.Platform = field("plataforma")?.Trim() ?? string.Empty;
            game.Publisher = field("editorial")?.Trim() ?? string.Empty;
            game.Genre = field("género")?.Trim() ?? string.Empty;
            game.Tags = field("etiquetas")?.Trim() ?? string.Empty;
            game.Notes = field("notas")?.Trim() ?? string.Empty;
            game.Year = ParseYear(field("año"));
            game.Rating = ParseRating(field("puntuación"));
            game.Played = ParsePlayed(field("jugado"));

            return game;
        }

        private static int? ParseYear(string? text)
            => int.TryParse(text, out var y) && y >= 1970 && y <= 2100 ? y : null;

        private static int ParseRating(string? text)
            => int.TryParse(text, out var r) && r >= 0 && r <= 5 ? r : 0;

        private static bool ParsePlayed(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var v = text.Trim().ToLowerInvariant();
            return v is "sí" or "si" or "yes" or "1" or "true" or "x";
        }

        // ── Vista previa: clasifica cada fila del archivo antes de importar nada ───────────
        // "Ya existe" compara por código de barras contra la colección actual, o por título+plataforma
        // cuando la fila no trae código de barras (el índice único de la BD solo cubre el barcode).
        // "Duplicado en el archivo" detecta repeticiones dentro del propio archivo importado.
        public static List<(Game Game, ImportItemStatus Status)> BuildPreview(List<Game> parsed, List<Game> existing)
        {
            var existingBarcodes = new HashSet<string>(
                existing.Where(g => !string.IsNullOrEmpty(g.Barcode)).Select(g => g.Barcode!),
                StringComparer.OrdinalIgnoreCase);
            var existingTitleKeys = new HashSet<string>(existing.Select(g => TitleKey(g.Title, g.Platform)));

            var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTitleKeys = new HashSet<string>();

            var result = new List<(Game, ImportItemStatus)>();
            foreach (var game in parsed)
            {
                var status = Classify(game, existingBarcodes, existingTitleKeys, seenBarcodes, seenTitleKeys);
                result.Add((game, status));
            }
            return result;
        }

        private static ImportItemStatus Classify(Game game, HashSet<string> existingBarcodes,
            HashSet<string> existingTitleKeys, HashSet<string> seenBarcodes, HashSet<string> seenTitleKeys)
        {
            if (!string.IsNullOrEmpty(game.Barcode))
            {
                if (existingBarcodes.Contains(game.Barcode)) return ImportItemStatus.YaExiste;
                return seenBarcodes.Add(game.Barcode) ? ImportItemStatus.Nuevo : ImportItemStatus.DuplicadoEnArchivo;
            }

            var titleKey = TitleKey(game.Title, game.Platform);
            if (existingTitleKeys.Contains(titleKey)) return ImportItemStatus.YaExiste;
            return seenTitleKeys.Add(titleKey) ? ImportItemStatus.Nuevo : ImportItemStatus.DuplicadoEnArchivo;
        }

        private static string TitleKey(string title, string platform) =>
            $"{title.Trim().ToLowerInvariant()}|{platform.Trim().ToLowerInvariant()}";

        // ── Duplicados dentro de la colección ya guardada (a diferencia de BuildPreview, que
        // compara un archivo a importar contra la colección, esto compara la colección consigo
        // misma) ──────────────────────────────────────────────────────────────────────────────
        // Mismo criterio de coincidencia que BuildPreview: código de barras (normalizando
        // UPC-A/EAN-13, que son el mismo producto salvo un "0" inicial) o título+plataforma si
        // no hay código de barras. La wishlist y la colección se comparan por separado, ya que
        // tener el mismo juego en ambas a la vez es un estado válido (pendiente de mover), no un
        // duplicado. Cada grupo se devuelve ordenado por fecha de alta (el más antiguo primero).
        public static List<List<Game>> FindDuplicateGroups(List<Game> games)
        {
            return games
                .GroupBy(g => (g.IsWishlist, Key: !string.IsNullOrEmpty(g.Barcode)
                    ? "B:" + CanonicalBarcode(g.Barcode!)
                    : "T:" + TitleKey(g.Title, g.Platform)))
                .Where(group => group.Count() > 1)
                .Select(group => group.OrderBy(g => g.AddedDate).ToList())
                .ToList();
        }

        private static string CanonicalBarcode(string barcode) =>
            barcode.Length == 13 && barcode.StartsWith("0") ? barcode[1..] : barcode;

        private static string? GetField(List<string> fields, Dictionary<string, int> mapping, string fieldKey)
            => mapping.TryGetValue(fieldKey, out var idx) && idx < fields.Count ? fields[idx] : null;

        // ── CSV: separado por ";", con comillas dobles para escapar (mismo formato que ExportService) ──

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ';') { fields.Add(current.ToString()); current.Clear(); }
                    else current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }
    }
}
