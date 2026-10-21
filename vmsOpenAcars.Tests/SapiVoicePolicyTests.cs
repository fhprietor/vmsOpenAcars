using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Elección del idioma de locución según las voces instaladas.
    ///
    /// El caso que motiva la regla: un Windows en inglés sin el paquete de idioma español tiene
    /// `en-US` y ninguna voz `es-*`. Antes, `SelectVoiceByHints(es-ES)` fallaba y la voz por defecto
    /// leía **texto español con fonética inglesa**. Ahora se cambia el **texto**, no la voz: se
    /// locuta el anuncio inglés. Y al revés, con solo voz española se locuta en español.
    ///
    /// Nunca devuelve un idioma sin plantilla: solo `es` o `en`.
    /// </summary>
    [TestClass]
    public class SapiVoicePolicyTests
    {
        [TestMethod]
        public void ConVozDelIdiomaPedidoSeRespetaElIdioma()
        {
            Assert.AreEqual("es", SapiVoicePolicy.ResolveLanguage("es", new[] { "es-ES", "en-US" }));
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("en", new[] { "es-ES", "en-US" }));
        }

        [TestMethod]
        public void SinVozDelIdiomaPedidoSeCambiaElTextoAlOtroIdioma()
        {
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("es", new[] { "en-US" }),
                "Windows en inglés sin voz española: se locuta el anuncio inglés, no español con fonética inglesa");
            Assert.AreEqual("es", SapiVoicePolicy.ResolveLanguage("en", new[] { "es-ES" }),
                "Windows en español sin voz inglesa: se locuta el anuncio español");
        }

        [TestMethod]
        public void SeAceptanVariantesRegionalesYMayusculas()
        {
            Assert.AreEqual("es", SapiVoicePolicy.ResolveLanguage("es", new[] { "ES-MX" }));
            Assert.AreEqual("es", SapiVoicePolicy.ResolveLanguage("es", new[] { " es-co " }));
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("en", new[] { "EN-GB" }));
        }

        [TestMethod]
        public void SinListaDeVocesSeRespetaElIdiomaPedido()
        {
            // No se pudo enumerar: es el mejor esfuerzo, y callar por una suposición sería peor.
            Assert.AreEqual("es", SapiVoicePolicy.ResolveLanguage("es", null));
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("en", null));
            Assert.AreEqual("es", SapiVoicePolicy.ResolveLanguage("es", new string[0]));
        }

        [TestMethod]
        public void LasEntradasVaciasDeLaListaSeIgnoran()
        {
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("es", new[] { "", "  ", null, "en-US" }));
        }

        [TestMethod]
        public void UnIdiomaSinPlantillaCaeAIngles()
        {
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("pt", new[] { "en-US" }),
                "Solo hay plantillas en español e inglés");
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("de", new[] { "de-DE" }));
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage(null, new[] { "en-US" }));
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("", new[] { "en-US" }));
        }

        [TestMethod]
        public void SoloHayVocesDeOtroIdioma_SeLocutaElPedidoConLaVozPorDefecto()
        {
            // Tercer caso: ni español ni inglés instalados. Se locuta en el idioma pedido con la voz
            // por defecto y `CabinVoice` lo avisa en el log, porque va a sonar raro.
            Assert.AreEqual("es", SapiVoicePolicy.ResolveLanguage("es", new[] { "pt-BR" }));
            Assert.AreEqual("en", SapiVoicePolicy.ResolveLanguage("en", new[] { "pt-BR" }));
        }

        [TestMethod]
        public void NuncaDevuelveUnIdiomaSinPlantilla()
        {
            var lists = new[]
            {
                null, new string[0], new[] { "es-ES" }, new[] { "en-US" }, new[] { "pt-BR" },
                new[] { "fr-FR", "de-DE" }, new[] { "es-MX", "en-GB" }, new[] { "", null }
            };
            foreach (var list in lists)
                foreach (string requested in new[] { null, "", "es", "en", "pt", "fr", "xx" })
                {
                    string resolved = SapiVoicePolicy.ResolveLanguage(requested, list);
                    Assert.IsTrue(resolved == "es" || resolved == "en",
                        $"Resolvió '{resolved}' para pedido '{requested}'");
                }
        }

        [TestMethod]
        public void ElCatalogoDeIdiomasSoportadosSonSoloDos()
        {
            CollectionAssert.AreEquivalent(new[] { "es", "en" }, SapiVoicePolicy.SupportedLanguages);
        }
    }
}
