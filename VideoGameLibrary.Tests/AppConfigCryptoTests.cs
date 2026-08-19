namespace VideoGameLibrary.Tests
{
    // Comprueba el cifrado DPAPI de las claves de API en config.json (ver App.Protect/Unprotect).
    public class AppConfigCryptoTests
    {
        [Fact]
        public void Protect_Unprotect_recupera_el_valor_original()
        {
            var original = "clave-secreta-de-prueba-123";

            var cifrado = App.Protect(original);
            var descifrado = App.Unprotect(cifrado);

            Assert.Equal(original, descifrado);
        }

        [Fact]
        public void Protect_no_deja_el_valor_en_texto_plano()
        {
            var original = "clave-secreta-de-prueba-123";

            var cifrado = App.Protect(original);

            Assert.NotEqual(original, cifrado);
            Assert.DoesNotContain(original, cifrado);
        }

        [Fact]
        public void Protect_de_cadena_vacia_devuelve_cadena_vacia()
        {
            Assert.Equal(string.Empty, App.Protect(string.Empty));
        }

        [Fact]
        public void Unprotect_de_cadena_vacia_devuelve_cadena_vacia()
        {
            Assert.Equal(string.Empty, App.Unprotect(string.Empty));
        }

        // Migración desde versiones anteriores de la app: config.json podía tener la clave
        // en texto plano (no es un blob DPAPI válido) -- debe devolverse tal cual, sin fallar.
        [Fact]
        public void Unprotect_de_texto_plano_heredado_lo_devuelve_sin_cambios()
        {
            var textoPlanoAntiguo = "una-clave-guardada-antes-del-cifrado";

            Assert.Equal(textoPlanoAntiguo, App.Unprotect(textoPlanoAntiguo));
        }
    }
}
