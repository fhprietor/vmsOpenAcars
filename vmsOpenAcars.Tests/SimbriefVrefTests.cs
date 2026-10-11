using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El Vref del plan** (`tlr.landing.distance_dry.speeds_vref`), que phpVMS pidió el 11/10/2026
    /// para poder juzgar la velocidad del umbral: sin él, «IAS 147 kt en la toma» no se puede juzgar
    /// ni por el instructor ni por el modelo.
    ///
    /// El mapeo del OFP vive dentro de `FetchAndParseOFP`, que **sale a la red**, así que la ruta se
    /// prueba aquí contra el **texto real** del OFP del piloto (el trozo del análisis de aterrizaje
    /// del plan SKRG→KATL del 11/10/2026). El motivo de que esto exista: si alguien mueve la ruta y el
    /// campo deja de leerse, el plan sigue cargando y `vref_kt` se queda en `null` **en silencio**
    /// —el campo es opcional, así que no falla nada—. Es el fallo mudo que este test impide.
    /// </summary>
    [TestClass]
    public class SimbriefVrefTests
    {
        /// <summary>El trozo real del OFP, con los valores tal cual los publica SimBrief (texto).</summary>
        private const string TlrReal = @"{ ""tlr"": { ""landing"": {
            ""conditions"": { ""planned_weight"": ""247261"", ""wind_speed"": ""7"" },
            ""distance_dry"": { ""weight"": ""248000"", ""speeds_vref"": ""148"" },
            ""distance_wet"": { ""weight"": ""248000"", ""speeds_vref"": ""148"" } } } }";

        [TestMethod]
        public void ElVrefSaleDelTlrDelPlanReal()
        {
            // El propio informe confirma que el valor es del peso y no de catálogo:
            // LDW 2400 → 146 kt, 2440 → 147, 2480 → 148, 2520 → 150, 2560 → 151.
            Assert.AreEqual(148, SimbriefEnhancedService.VrefFromOfpJson(TlrReal));
        }

        [TestMethod]
        public void SinTlr_OSinElCampo_NoSeInventaUnVref()
        {
            // Cada forma de no tenerlo tiene que dar `null` —**nunca 0**, que sería una velocidad—:
            // sin sección `tlr` (un plan sin análisis de aterrizaje), sin `speeds_vref`, con el campo
            // vacío, sin json, y con un json que no se puede leer.
            Assert.IsNull(SimbriefEnhancedService.VrefFromOfpJson(@"{ ""general"": {} }"), "sin tlr");
            Assert.IsNull(SimbriefEnhancedService.VrefFromOfpJson(
                @"{ ""tlr"": { ""landing"": { ""distance_dry"": { ""weight"": ""248000"" } } } }"),
                "sin speeds_vref");
            Assert.IsNull(SimbriefEnhancedService.VrefFromOfpJson(
                @"{ ""tlr"": { ""landing"": { ""distance_dry"": { ""speeds_vref"": """" } } } }"),
                "con el campo vacío");
            Assert.IsNull(SimbriefEnhancedService.VrefFromOfpJson(null), "sin json");
            Assert.IsNull(SimbriefEnhancedService.VrefFromOfpJson("no soy json"), "json ilegible");
        }
    }
}
