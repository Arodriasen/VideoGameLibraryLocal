using VideoGameLibrary.ViewModels;

namespace VideoGameLibrary.Tests
{
    // GameViewModel no toca MaterialDesignThemes (a diferencia de MainViewModel), así que no
    // necesita [WpfFact]/Dispatcher — un [Fact] normal basta.
    public class GameViewModelCardTagsTests
    {
        [Fact]
        public void Sin_etiquetas_no_hay_visibles_ni_ocultas()
        {
            var vm = new GameViewModel { Tags = "" };

            Assert.Empty(vm.CardVisibleTags);
            Assert.Equal(0, vm.CardHiddenTagCount);
            Assert.False(vm.HasHiddenTags);
        }

        [Fact]
        public void Pocas_etiquetas_cortas_caben_todas()
        {
            var vm = new GameViewModel { Tags = "favorito, rpg" };

            Assert.Equal(new[] { "favorito", "rpg" }, vm.CardVisibleTags);
            Assert.False(vm.HasHiddenTags);
        }

        [Fact]
        public void La_primera_etiqueta_se_muestra_siempre_aunque_supere_el_presupuesto_ella_sola()
        {
            var vm = new GameViewModel { Tags = "una etiqueta bastante larga de verdad, otra" };

            Assert.Single(vm.CardVisibleTags);
            Assert.Equal("una etiqueta bastante larga de verdad", vm.CardVisibleTags[0]);
            Assert.True(vm.HasHiddenTags);
            Assert.Equal(1, vm.CardHiddenTagCount);
        }

        [Fact]
        public void Etiquetas_que_no_caben_se_cuentan_como_ocultas()
        {
            var vm = new GameViewModel { Tags = "para vender, prestado, favorito, rpg, multijugador" };

            Assert.True(vm.CardVisibleTags.Count < 5);
            Assert.Equal(5 - vm.CardVisibleTags.Count, vm.CardHiddenTagCount);
            Assert.True(vm.HasHiddenTags);
        }

        [Fact]
        public void TagList_completa_no_se_recorta_a_diferencia_de_CardVisibleTags()
        {
            var vm = new GameViewModel { Tags = "para vender, prestado, favorito, rpg, multijugador" };

            Assert.Equal(5, vm.TagList.Count);
        }
    }
}
