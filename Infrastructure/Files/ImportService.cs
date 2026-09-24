using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using VideoGameLibrary.Application.Abstractions;
using VideoGameLibrary.Application.Games;
using VideoGameLibrary.Application.Import;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Infrastructure.Files
{
    // Lee un CSV (separado por ";", mismo formato que ExportService) o un Excel (.xlsx)
    // con una fila de cabecera. Qué columna del archivo corresponde a qué campo lo decide
    // el usuario en ImportColumnMappingDialog — con una sugerencia automática por nombre de
    // cabecera (ImportPreviewBuilder.GuessMapping) como punto de partida, así funciona igual de
    // bien con archivos que exporta esta misma app y con archivos de otras apps que usen otros
    // nombres de columna. Única columna obligatoria: "Título". La parte de reglas de negocio sin
    // I/O (vista previa, duplicados, catálogo de campos) vive en Application/Import — esta clase
    // se queda solo con la lectura real de disco.
    public class ImportService : IImportService
    {
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
            => ParseFile(filePath, ImportPreviewBuilder.GuessMapping(ReadHeaders(filePath)));

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
            game.Barcode = string.IsNullOrWhiteSpace(barcode) ? null : BarcodeUtils.NormalizeBarcode(barcode);

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
