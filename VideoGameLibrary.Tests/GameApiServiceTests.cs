using VideoGameLibrary.Services;

namespace VideoGameLibrary.Tests
{
    public class GameApiServiceTests
    {
        [Theory]
        [InlineData("1234567890123", "1234567890123")]
        [InlineData("123-456-789-0123", "1234567890123")] // guiones sueltos, como puede pegar un lector
        [InlineData(" 1234 5678 9012 3 ", "1234567890123")]
        [InlineData("", "")]
        public void NormalizeBarcode_deja_solo_digitos(string raw, string esperado)
        {
            Assert.Equal(esperado, GameApiService.NormalizeBarcode(raw));
        }

        [Fact]
        public void GetBarcodeVariants_UPC_A_de_12_digitos_añade_variante_EAN_13_con_cero_delante()
        {
            var variants = GameApiService.GetBarcodeVariants("012345678905");

            Assert.Equal(new[] { "012345678905", "0012345678905" }, variants);
        }

        [Fact]
        public void GetBarcodeVariants_EAN_13_con_cero_inicial_añade_variante_UPC_A_de_12_digitos()
        {
            var variants = GameApiService.GetBarcodeVariants("0012345678905");

            Assert.Equal(new[] { "0012345678905", "012345678905" }, variants);
        }

        [Fact]
        public void GetBarcodeVariants_EAN_13_sin_cero_inicial_no_añade_variantes()
        {
            var variants = GameApiService.GetBarcodeVariants("1234567890128");

            Assert.Equal(new[] { "1234567890128" }, variants);
        }

        [Fact]
        public void GetBarcodeVariants_longitud_no_estandar_no_añade_variantes()
        {
            var variants = GameApiService.GetBarcodeVariants("12345");

            Assert.Equal(new[] { "12345" }, variants);
        }

        [Theory]
        [InlineData("Lanzado en 2017 para Switch", 2017)]
        [InlineData("(c) 1998 Nintendo", 1998)]
        [InlineData("Sin ningún año aquí", null)]
        [InlineData("", null)]
        [InlineData("Año 1899, demasiado antiguo", null)] // fuera de rango (< 1970)
        public void ExtractYear_encuentra_el_primer_año_valido_de_4_digitos(string texto, int? esperado)
        {
            Assert.Equal(esperado, GameApiService.ExtractYear(texto));
        }

        [Fact]
        public void ExtractYear_no_acepta_años_futuros_mas_alla_del_proximo_año()
        {
            var futuro = DateTime.Now.Year + 5;
            Assert.Null(GameApiService.ExtractYear($"Previsto para {futuro}"));
        }
    }
}
