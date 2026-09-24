using System.Collections.Generic;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Application.Abstractions
{
    public interface IImportService
    {
        List<string> ReadHeaders(string filePath);

        // mapping: clave de campo (p.ej. "título") -> índice de columna en el archivo (0-based).
        List<Game> ParseFile(string filePath, Dictionary<string, int> mapping);

        // Atajo: adivina el mapeo por nombre de cabecera y parsea directamente.
        List<Game> ParseFile(string filePath);
    }
}
