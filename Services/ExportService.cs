using System.Collections.Generic;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using VideoGameLibrary.Models;

namespace VideoGameLibrary.Services
{
    public class ExportService
    {
        public void ExportToExcel(IEnumerable<Game> games, string filePath)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Colección");

            string[] headers = { "Código de Barras", "Título", "Plataforma", "Editorial", "Género", "Etiquetas", "Año", "Puntuación", "Notas", "Fecha Añadido" };
            for (int col = 0; col < headers.Length; col++)
            {
                sheet.Cell(1, col + 1).Value = headers[col];
                sheet.Cell(1, col + 1).Style.Font.Bold = true;
            }

            int row = 2;
            foreach (var g in games)
            {
                sheet.Cell(row, 1).Value = g.Barcode ?? string.Empty;
                sheet.Cell(row, 2).Value = g.Title;
                sheet.Cell(row, 3).Value = g.Platform;
                sheet.Cell(row, 4).Value = g.Publisher;
                sheet.Cell(row, 5).Value = g.Genre;
                sheet.Cell(row, 6).Value = g.Tags;
                if (g.Year.HasValue)
                    sheet.Cell(row, 7).Value = g.Year.Value;
                if (g.Rating > 0)
                    sheet.Cell(row, 8).Value = g.Rating;
                sheet.Cell(row, 9).Value = g.Notes;
                sheet.Cell(row, 10).Value = g.AddedDate.ToString("dd/MM/yyyy");
                row++;
            }

            sheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        public void ExportToCsv(IEnumerable<Game> games, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("sep=;");
            sb.AppendLine("Código de Barras;Título;Plataforma;Editorial;Género;Etiquetas;Año;Puntuación;Notas;Fecha Añadido");

            foreach (var g in games)
            {
                var rating = g.Rating > 0 ? g.Rating.ToString() : "";
                sb.AppendLine($"\"{Escape(g.Barcode)}\";\"{Escape(g.Title)}\";\"{Escape(g.Platform)}\";\"{Escape(g.Publisher)}\";\"{Escape(g.Genre)}\";\"{Escape(g.Tags)}\";\"{Escape(g.Year?.ToString())}\";\"{rating}\";\"{Escape(g.Notes)}\";\"{g.AddedDate:dd/MM/yyyy}\"");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string Escape(string? value)
        {
            return value?.Replace("\"", "\"\"") ?? string.Empty;
        }
    }
}
