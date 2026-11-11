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

        // ── El peso al que corresponde el Vref ────────────────────────────────────

        /// <summary>
        /// **El peso de planeo del TLR, en libras**, que es lo que hace auditable la ventana de
        /// velocidad de la compuerta. El bloque real trae `planned_weight = 247261`, y ese número está
        /// en **kg**: la tabla del propio informe va en `LDW 2400 → 146 · 2480 → 148`, o sea cientos de
        /// kg, y 247 261 lb serían 112 t (imposible para un avión con Vref 148 a 248 t).
        ///
        /// El plan del fixture **no trae `params.units`**, así que se aplica el mismo defecto que el
        /// resto del mapeo: KG. Es el caso que más importa, porque un plan sin el campo no puede
        /// publicar el peso 2,2 veces por debajo en silencio.
        /// </summary>
        [TestMethod]
        public void ElPesoDePlaneoDelTlr_SaleEnLibras()
        {
            double? lbs = SimbriefEnhancedService.PlannedWeightLbsFromOfpJson(TlrReal);

            Assert.IsTrue(lbs.HasValue, "el TLR del fixture trae planned_weight");
            Assert.AreEqual(545116.5, lbs.Value, 2.0,
                "247 261 kg en libras; la tabla del informe (2400/2480/2560) confirma que es kg");
        }

        /// <summary>Un plan en libras publica el número **tal cual**: no se convierte dos veces.</summary>
        [TestMethod]
        public void UnPlanEnLibras_NoSeConvierte()
        {
            const string enLibras = @"{ ""params"": { ""units"": ""LBS"" },
                ""tlr"": { ""landing"": { ""conditions"": { ""planned_weight"": ""400100"" } } } }";

            Assert.AreEqual(400100.0, SimbriefEnhancedService.PlannedWeightLbsFromOfpJson(enLibras).Value, 0.01,
                "400.100 lb es el peso del A339 del PIREP que abrió esto");
        }

        /// <summary>Sin TLR, sin campo o con un valor que no crece: `null`, nunca 0.</summary>
        [TestMethod]
        public void SinPesoDePlaneo_NoSeInventa()
        {
            Assert.IsNull(SimbriefEnhancedService.PlannedWeightLbsFromOfpJson(@"{ ""general"": {} }"), "sin tlr");
            Assert.IsNull(SimbriefEnhancedService.PlannedWeightLbsFromOfpJson(
                @"{ ""tlr"": { ""landing"": { ""conditions"": { } } } }"), "sin planned_weight");
            Assert.IsNull(SimbriefEnhancedService.PlannedWeightLbsFromOfpJson(
                @"{ ""tlr"": { ""landing"": { ""conditions"": { ""planned_weight"": ""0"" } } } }"), "cero");
            Assert.IsNull(SimbriefEnhancedService.PlannedWeightLbsFromOfpJson(null), "sin json");
            Assert.IsNull(SimbriefEnhancedService.PlannedWeightLbsFromOfpJson("no soy json"), "json ilegible");
        }
    }
}
