using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoGameLibrary.Application.Games
{
    // Utilidades puras sobre texto (códigos de barras, años) sin ninguna dependencia externa —
    // usadas tanto por Infrastructure (GameApiService, ImportService) como directamente por
    // Presentation (GameEditViewModel, MainViewModel) para no tener que ir a buscarlas a través
    // de GameApiService, que sí depende de red/claves de API.
    public static class BarcodeUtils
    {
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
