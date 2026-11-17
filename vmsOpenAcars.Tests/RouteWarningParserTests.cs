using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **La lectura del `route_warning` del prefile.** Es un dato de fuera —lo escribe phpVMS— y su
    /// forma está acordada desde el mensaje del 16/11/2026:
    ///
    /// <code>
    /// "route_warning": { "area": "SKR11 / BARRANQUILLA", "limits": "GND–FL999",
    ///                    "leg": "GIKPU–PADUD", "action": "revisar_ruta" }
    /// </code>
    ///
    /// Dos cosas que la prueba protege: que **`null` signifique «no hay nada que avisar»** (ruta
    /// limpia, sin OFP o área `Danger`) y que una forma inesperada **no rompa el prefile** —el aviso
    /// nunca puede ser la causa de que un vuelo no empiece—. Ver `RouteWarningParser`.
    /// </summary>
    [TestClass]
    public class RouteWarningParserTests
    {
        [TestMethod]
        public void LeeElAvisoDelContrato()
        {
            var json = JObject.Parse(@"{
                ""area"": ""SKR11 / BARRANQUILLA"",
                ""limits"": ""GND–FL999"",
                ""leg"": ""GIKPU–PADUD"",
                ""action"": ""revisar_ruta""
            }");

            var warning = RouteWarningParser.Parse(json);

            Assert.IsNotNull(warning);
            Assert.AreEqual("SKR11 / BARRANQUILLA", warning.Area);
            Assert.AreEqual("GND–FL999", warning.Limits);
            Assert.AreEqual("GIKPU–PADUD", warning.Leg);
            Assert.AreEqual("revisar_ruta", warning.Action);
            Assert.IsTrue(warning.FromServer, "vino del servidor");
            Assert.AreEqual("SKR11 / BARRANQUILLA (GND–FL999) GIKPU–PADUD", warning.Describe());
        }

        [TestMethod]
        public void UnAvisoAusenteONuloEsQueNoHayNadaQueAvisar()
        {
            Assert.IsNull(RouteWarningParser.Parse(null));
            Assert.IsNull(RouteWarningParser.Parse(JValue.CreateNull()));
            Assert.IsNull(RouteWarningParser.Parse(JObject.Parse("{}")),
                "un objeto sin ninguno de los cuatro campos no es un cruce");
            Assert.IsNull(RouteWarningParser.Parse(new JObject { ["note"] = "hola" }));
        }

        [TestMethod]
        public void UnaListaSeDegradaASuPrimerAviso()
        {
            var json = JArray.Parse(@"[ null, { ""area"": ""SKEZ"", ""limits"": ""SFC–FL100"" } ]");

            var warning = RouteWarningParser.Parse(json);

            Assert.IsNotNull(warning);
            Assert.AreEqual("SKEZ", warning.Area);
        }

        [TestMethod]
        public void LosCamposEnBlancoNoCuentan()
        {
            var warning = RouteWarningParser.Parse(JObject.Parse(@"{ ""area"": ""  "", ""leg"": ""  "" }"));
            Assert.IsNull(warning, "sin datos no hay aviso: la UI no puede enseñar nada útil");

            var onlyArea = RouteWarningParser.Parse(JObject.Parse(@"{ ""area"": ""SKR11 / BARRANQUILLA"" }"));
            Assert.IsNotNull(onlyArea);
            Assert.IsNull(onlyArea.Limits);
            Assert.AreEqual("SKR11 / BARRANQUILLA", onlyArea.Describe(),
                "sin límites ni tramo se sigue diciendo el área");
        }

        [TestMethod]
        public void UnaFormaInesperadaNoLanza()
        {
            // El prefile NUNCA puede fallar por el aviso: si el servidor manda otra cosa, se ignora.
            Assert.IsNull(RouteWarningParser.Parse(new JValue("SKR11")));
            Assert.IsNull(RouteWarningParser.Parse(new JValue(42)));
            Assert.IsNull(RouteWarningParser.Parse(new JObject { ["area"] = new JObject() }));
        }
    }
}
