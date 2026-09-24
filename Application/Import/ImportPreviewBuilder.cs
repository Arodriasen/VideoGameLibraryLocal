using System;
using System.Collections.Generic;
using System.Linq;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Application.Import
{
    // Reglas de negocio puras sobre listas de Game ya en memoria -- no tocan disco ni red, por
    // eso viven en Application y no en Infrastructure/Files (donde sí vive la lectura real de
    // archivos CSV/Excel, ver ImportService). Incluye también el catálogo de campos y la
    // sugerencia de mapeo (GuessMapping), que son igual de puros.
    public static class ImportPreviewBuilder
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
    }
}
