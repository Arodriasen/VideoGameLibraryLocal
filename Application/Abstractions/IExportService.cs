using System.Collections.Generic;
using VideoGameLibrary.Domain.Entities;

namespace VideoGameLibrary.Application.Abstractions
{
    public interface IExportService
    {
        void ExportToExcel(IEnumerable<Game> games, string filePath);
        void ExportToCsv(IEnumerable<Game> games, string filePath);
    }
}
