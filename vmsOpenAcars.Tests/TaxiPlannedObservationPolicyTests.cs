using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **La ruta propuesta (`planned`) no se publica mientras NavData no tenga su almacén**
    /// (`TaxiPlannedObservationPolicy`).
    ///
    /// La regla existe porque el validador de ingesta de NavData **rechaza hoy** el cuerpo de
    /// `POST /taxi-routes/observations` por dos motivos exactos: `missing_stand` (el `planned` no
    /// lleva `stand`) y `route_too_short` (`route` es obligatorio con ≥2 calles y la propuesta
    /// **no** puede ir en `route`). Hasta que monten su almacén `TaxiRoutePlanned`, cada vuelo
    /// guiado mandaba una petición condenada.
    ///
    /// El test fija **los dos lados**: apagado no se publica **ni con la ruta perfecta** —que es
    /// lo que garantiza que no se sale a la red—, y encendido sigue exigiendo una propuesta
    /// utilizable, porque sin ruta o con menos de dos puntos el cuerpo no describe un camino.
    /// </summary>
    [TestClass]
    public class TaxiPlannedObservationPolicyTests
    {
        // Una polilínea de una ruta real ronda las decenas de vértices; para la decisión basta
        // saber que son ≥ 2, así que no se inventan coordenadas: la política no las mira.
        private const int UsablePolyline = 2;
        private const int LongPolyline   = 40;

        // ── El interruptor: apagado por defecto ──────────────────────────────────

        [TestMethod]
        public void Apagado_NoSePublica_AunqueLaRutaSeaPerfecta()
        {
            // El caso que importa: con la clave ausente o en false, ni siquiera una propuesta
            // impecable sale a la red. Es la razón de ser del interruptor.
            Assert.IsFalse(TaxiPlannedObservationPolicy.ShouldPublish(false, true, LongPolyline),
                           "apagado no se toca la red, por buena que sea la ruta");
        }

        [TestMethod]
        public void Apagado_NoSePublica_NiSiquieraConElMinimoDePuntos()
        {
            Assert.IsFalse(TaxiPlannedObservationPolicy.ShouldPublish(false, true, UsablePolyline));
        }

        [TestMethod]
        public void Apagado_NoSePublica_SinRuta()
        {
            Assert.IsFalse(TaxiPlannedObservationPolicy.ShouldPublish(false, false, 0));
        }

        // ── Encendido: sigue exigiendo una propuesta utilizable ──────────────────

        [TestMethod]
        public void Encendido_ConRutaUsable_SePublica()
        {
            Assert.IsTrue(TaxiPlannedObservationPolicy.ShouldPublish(true, true, UsablePolyline),
                          "dos puntos ya describen un camino");
            Assert.IsTrue(TaxiPlannedObservationPolicy.ShouldPublish(true, true, LongPolyline));
        }

        [TestMethod]
        public void Encendido_SinRutaEncontrada_NoSePublica()
        {
            // Degrada sin datos: el grafo no encontró camino (o el destino quedó en otro
            // componente), así que no hay nada que publicar. Sin cuerpo no hay POST.
            Assert.IsFalse(TaxiPlannedObservationPolicy.ShouldPublish(true, false, 0));
        }

        [TestMethod]
        public void Encendido_ConMenosDeDosPuntos_NoSePublica()
        {
            // Un punto no es una ruta: el guardia de siempre, que no se afloja al encender.
            Assert.IsFalse(TaxiPlannedObservationPolicy.ShouldPublish(true, true, 0));
            Assert.IsFalse(TaxiPlannedObservationPolicy.ShouldPublish(true, true, 1));
        }

        [TestMethod]
        public void ElMinimoDePuntosEsDos()
        {
            Assert.AreEqual(2, TaxiPlannedObservationPolicy.MinPolylinePoints);
        }
    }
}
