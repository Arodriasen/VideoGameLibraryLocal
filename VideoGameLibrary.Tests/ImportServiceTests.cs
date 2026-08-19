using VideoGameLibrary.Models;
using VideoGameLibrary.Services;

namespace VideoGameLibrary.Tests
{
    public class ImportServiceBuildPreviewTests
    {
        private static Game NewGame(string title, string platform = "Nintendo Switch", string? barcode = null) =>
            new() { Title = title, Platform = platform, Barcode = barcode };

        [Fact]
        public void Game_sin_coincidencias_se_clasifica_como_Nuevo()
        {
            var parsed = new List<Game> { NewGame("Un juego nuevo", barcode: "1234567890123") };
            var existing = new List<Game>();

            var result = ImportService.BuildPreview(parsed, existing);

            Assert.Single(result);
            Assert.Equal(ImportItemStatus.Nuevo, result[0].Status);
        }

        [Fact]
        public void Barcode_que_ya_existe_en_la_coleccion_se_marca_YaExiste()
        {
            var parsed = new List<Game> { NewGame("Zelda", barcode: "1234567890123") };
            var existing = new List<Game> { NewGame("Zelda (ya en mi colección)", barcode: "1234567890123") };

            var result = ImportService.BuildPreview(parsed, existing);

            Assert.Equal(ImportItemStatus.YaExiste, result[0].Status);
        }

        [Fact]
        public void Sin_barcode_compara_por_titulo_y_plataforma_contra_la_coleccion()
        {
            var parsed = new List<Game> { NewGame("Mario Kart 8", "Nintendo Switch") };
            var existing = new List<Game> { NewGame("mario kart 8", "nintendo switch") }; // mayúsculas/espacios distintos

            var result = ImportService.BuildPreview(parsed, existing);

            Assert.Equal(ImportItemStatus.YaExiste, result[0].Status);
        }

        [Fact]
        public void Mismo_titulo_en_otra_plataforma_no_se_considera_duplicado()
        {
            var parsed = new List<Game> { NewGame("Hollow Knight", "PC") };
            var existing = new List<Game> { NewGame("Hollow Knight", "Nintendo Switch") };

            var result = ImportService.BuildPreview(parsed, existing);

            Assert.Equal(ImportItemStatus.Nuevo, result[0].Status);
        }

        [Fact]
        public void Barcode_repetido_dentro_del_mismo_archivo_marca_el_segundo_como_duplicado()
        {
            var parsed = new List<Game>
            {
                NewGame("Fila 1", barcode: "1111111111111"),
                NewGame("Fila 2 (mismo barcode)", barcode: "1111111111111")
            };

            var result = ImportService.BuildPreview(parsed, new List<Game>());

            Assert.Equal(ImportItemStatus.Nuevo, result[0].Status);
            Assert.Equal(ImportItemStatus.DuplicadoEnArchivo, result[1].Status);
        }

        [Fact]
        public void Titulo_y_plataforma_repetidos_sin_barcode_marca_el_segundo_como_duplicado()
        {
            var parsed = new List<Game>
            {
                NewGame("Celeste", "PC"),
                NewGame("Celeste", "PC")
            };

            var result = ImportService.BuildPreview(parsed, new List<Game>());

            Assert.Equal(ImportItemStatus.Nuevo, result[0].Status);
            Assert.Equal(ImportItemStatus.DuplicadoEnArchivo, result[1].Status);
        }

        [Fact]
        public void Orden_de_clasificacion_se_mantiene_igual_que_el_de_entrada()
        {
            var parsed = new List<Game>
            {
                NewGame("A", barcode: "111"),
                NewGame("B", barcode: "222"),
                NewGame("C", barcode: "333")
            };

            var result = ImportService.BuildPreview(parsed, new List<Game>());

            Assert.Equal(new[] { "A", "B", "C" }, result.Select(r => r.Game.Title));
        }
    }

    public class ImportServiceFindDuplicateGroupsTests
    {
        private static Game NewGame(string title, string platform = "Nintendo Switch", string? barcode = null,
            bool isWishlist = false, DateTime? addedDate = null) =>
            new()
            {
                Title = title,
                Platform = platform,
                Barcode = barcode,
                IsWishlist = isWishlist,
                AddedDate = addedDate ?? DateTime.Now
            };

        [Fact]
        public void Sin_coincidencias_no_devuelve_ningun_grupo()
        {
            var games = new List<Game> { NewGame("A", barcode: "111"), NewGame("B", barcode: "222") };

            Assert.Empty(ImportService.FindDuplicateGroups(games));
        }

        [Fact]
        public void Mismo_barcode_exacto_forma_un_grupo()
        {
            var games = new List<Game> { NewGame("Zelda"), NewGame("Zelda (copia)") };
            games[0].Barcode = "1234567890123";
            games[1].Barcode = "1234567890123";

            var groups = ImportService.FindDuplicateGroups(games);

            Assert.Single(groups);
            Assert.Equal(2, groups[0].Count);
        }

        [Fact]
        public void UPC_A_y_EAN_13_del_mismo_producto_se_agrupan_pese_al_cero_inicial()
        {
            var games = new List<Game>
            {
                NewGame("Mario Kart 8", barcode: "012345678905"), // UPC-A, 12 dígitos
                NewGame("Mario Kart 8", barcode: "0012345678905") // EAN-13 con "0" inicial
            };

            var groups = ImportService.FindDuplicateGroups(games);

            Assert.Single(groups);
        }

        [Fact]
        public void Sin_barcode_agrupa_por_titulo_y_plataforma()
        {
            var games = new List<Game>
            {
                NewGame("Celeste", "PC"),
                NewGame("celeste", "pc") // mayúsculas/espacios distintos
            };

            var groups = ImportService.FindDuplicateGroups(games);

            Assert.Single(groups);
        }

        [Fact]
        public void Mismo_titulo_en_otra_plataforma_no_se_agrupa()
        {
            var games = new List<Game> { NewGame("Hollow Knight", "PC"), NewGame("Hollow Knight", "Nintendo Switch") };

            Assert.Empty(ImportService.FindDuplicateGroups(games));
        }

        [Fact]
        public void Wishlist_y_coleccion_no_se_agrupan_entre_si()
        {
            var games = new List<Game>
            {
                NewGame("Celeste", "PC", isWishlist: false),
                NewGame("Celeste", "PC", isWishlist: true)
            };

            Assert.Empty(ImportService.FindDuplicateGroups(games));
        }

        [Fact]
        public void Grupo_se_ordena_por_fecha_de_alta_ascendente()
        {
            var games = new List<Game>
            {
                NewGame("Celeste", "PC", addedDate: new DateTime(2026, 3, 1)),
                NewGame("Celeste", "PC", addedDate: new DateTime(2025, 1, 1)),
                NewGame("Celeste", "PC", addedDate: new DateTime(2025, 6, 1))
            };

            var group = Assert.Single(ImportService.FindDuplicateGroups(games));

            Assert.Equal(new DateTime(2025, 1, 1), group[0].AddedDate);
            Assert.Equal(new DateTime(2025, 6, 1), group[1].AddedDate);
            Assert.Equal(new DateTime(2026, 3, 1), group[2].AddedDate);
        }
    }

    public class ImportServiceParseCsvTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        private string WriteCsv(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            _tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var f in _tempFiles)
                if (File.Exists(f)) File.Delete(f);
        }

        [Fact]
        public void Parsea_fila_basica_con_todas_las_columnas()
        {
            var path = WriteCsv(
                "Código de Barras;Título;Plataforma;Editorial;Género;Año;Puntuación;Notas;Jugado\n" +
                "1234567890123;Super Mario Odyssey;Nintendo Switch;Nintendo;Plataformas;2017;5;Genial;Sí\n");

            var games = new ImportService().ParseFile(path);

            var g = Assert.Single(games);
            Assert.Equal("Super Mario Odyssey", g.Title);
            Assert.Equal("Nintendo Switch", g.Platform);
            Assert.Equal("Nintendo", g.Publisher);
            Assert.Equal(2017, g.Year);
            Assert.Equal(5, g.Rating);
            Assert.True(g.Played);
            Assert.Equal("123456789012" + "3", g.Barcode); // normalizado (solo dígitos)
        }

        [Fact]
        public void Columna_Etiquetas_se_lee_igual_que_Genero()
        {
            var path = WriteCsv("Título;Etiquetas\nCeleste;favorito, para vender\n");

            var games = new ImportService().ParseFile(path);

            Assert.Equal("favorito, para vender", Assert.Single(games).Tags);
        }

        [Fact]
        public void Fila_sin_titulo_se_descarta()
        {
            var path = WriteCsv(
                "Título;Plataforma\n" +
                ";PC\n" +
                "Un juego válido;PC\n");

            var games = new ImportService().ParseFile(path);

            var g = Assert.Single(games);
            Assert.Equal("Un juego válido", g.Title);
        }

        [Fact]
        public void Sin_columna_titulo_devuelve_lista_vacia()
        {
            var path = WriteCsv("Plataforma;Año\nPC;2020\n");

            var games = new ImportService().ParseFile(path);

            Assert.Empty(games);
        }

        [Fact]
        public void Columnas_en_cualquier_orden_se_identifican_por_nombre()
        {
            var path = WriteCsv(
                "Plataforma;Título\n" +
                "PC;Un juego\n");

            var games = new ImportService().ParseFile(path);

            var g = Assert.Single(games);
            Assert.Equal("Un juego", g.Title);
            Assert.Equal("PC", g.Platform);
        }

        [Theory]
        [InlineData("Sí", true)]
        [InlineData("si", true)]
        [InlineData("YES", true)]
        [InlineData("1", true)]
        [InlineData("true", true)]
        [InlineData("x", true)]
        [InlineData("No", false)]
        [InlineData("", false)]
        public void Columna_Jugado_reconoce_varios_formatos(string valor, bool esperado)
        {
            var path = WriteCsv($"Título;Jugado\nJuego;{valor}\n");

            var games = new ImportService().ParseFile(path);

            Assert.Equal(esperado, Assert.Single(games).Played);
        }

        [Fact]
        public void Año_fuera_de_rango_se_ignora()
        {
            var path = WriteCsv("Título;Año\nJuego;1899\n");

            var games = new ImportService().ParseFile(path);

            Assert.Null(Assert.Single(games).Year);
        }

        [Fact]
        public void Primera_linea_sep_se_ignora_como_cabecera()
        {
            var path = WriteCsv("sep=;\nTítulo\nJuego con sep\n");

            var games = new ImportService().ParseFile(path);

            Assert.Equal("Juego con sep", Assert.Single(games).Title);
        }

        [Fact]
        public void Campos_entre_comillas_con_punto_y_coma_no_se_parten()
        {
            var path = WriteCsv("Título;Notas\n\"Juego; edición especial\";\"Nota con ; dentro\"\n");

            var games = new ImportService().ParseFile(path);

            var g = Assert.Single(games);
            Assert.Equal("Juego; edición especial", g.Title);
            Assert.Equal("Nota con ; dentro", g.Notes);
        }
    }
}
