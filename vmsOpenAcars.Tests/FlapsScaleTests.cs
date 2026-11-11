using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La contradicción de flaps del PIREP real `D0Wy9Pa7GNB5pYEo`** (AT76), en el mismo segundo
    /// del toque:
    ///
    /// ```
    /// 01:36:32  Flaps: 40% | Spoilers: 0%        ← DecodeFlaps: grados de Boeing con un «%» detrás
    /// 01:36:32  … FLAPS 100%                     ← 0x0BDC / 16383 × 100
    /// ```
    ///
    /// Y en la aproximación, hacia atrás: `0% → 40%` (01:32:15), `40% → 25%` (01:32:20) y
    /// `25% → 40%` (01:32:37). En el ATR las únicas posiciones son **15 % y 30 %**, así que la escala
    /// estirada de `DecodeFlaps` (15 → banda 25, 30 → banda 40) explica el log entero.
    ///
    /// Lo que se fija aquí: **una sola escala**, la del offset, que ya viene normalizada contra el
    /// recorrido real de cada avión (el addon declara su fondo de escala en los 16 383); y que el
    /// **umbral del 50 % no se ha movido**, porque con esta escala el ATR sí lo alcanza (detente de
    /// 30 = 100 %).
    /// </summary>
    [TestClass]
    public class FlapsScaleTests
    {
        [TestMethod]
        public void ElFondoDeEscalaEsElCienPorCiento()
        {
            Assert.AreEqual(0.0, FlapsScale.PercentOfTravel(0), 1e-9);
            Assert.AreEqual(100.0, FlapsScale.PercentOfTravel(16383), 1e-9);
            Assert.AreEqual(50.0, FlapsScale.PercentOfTravel(8191.5), 0.01);
        }

        [TestMethod]
        public void EnElAtrConFlapsALaToma_ElRecorridoEsEl100()
        {
            // Es la cifra que la línea del aterrizaje imprimía —`FLAPS 100%`— y la que el bloque del
            // toque llamaba «40 %»: la misma lectura, dos decodificadores.
            double atTouchdown = FlapsScale.PercentOfTravel(16383);

            Assert.AreEqual(100.0, atTouchdown, 1e-9);
            Assert.IsTrue(atTouchdown >= FlapsScale.StabilizedMinPercent,
                "con el mando a fondo el ATR pasa la puerta del 50 %: la puerta no mentía");
        }

        [TestMethod]
        public void LaEscalaEstaNormalizadaContraElRecorridoDelAvion()
        {
            // El detente de 15 del ATR ronda la mitad del recorrido y el de 30 lo llena: 15 y 30 son
            // *posiciones de flap*, no porcentajes del mando, y por eso no se comparan con el 50 %.
            double detent15 = FlapsScale.PercentOfTravel(8191.5);
            double detent30 = FlapsScale.PercentOfTravel(16383);

            Assert.AreEqual(50.0, detent15, 0.01);
            Assert.AreEqual(100.0, detent30, 1e-9);
            Assert.IsTrue(detent15 >= FlapsScale.StabilizedMinPercent,
                "a media carrera el 50 % se cumple (y no hace falta bajarlo)");
        }

        [TestMethod]
        public void ElUmbralDelScoringNoSeToca()
        {
            // Se conserva el 50 % histórico: un cambio de escala no puede mover puntuaciones por sí
            // mismo. Este test existe para que bajarlo sea una decisión explícita y no un descuido.
            Assert.AreEqual(50.0, FlapsScale.StabilizedMinPercent, 1e-9);
        }

        [TestMethod]
        public void ElMandoNoEscribeUnaLineaPorPunto()
        {
            // El mando se mueve en pasos de 1 de 16 383 (0,006 %): comparar el porcentaje directo
            // escribiría una línea por punto. La banda es el cuantizador del log, no un valor que se
            // publique.
            int band = FlapsScale.BandKey(FlapsScale.PercentOfTravel(14000));
            Assert.AreEqual(band, FlapsScale.BandKey(FlapsScale.PercentOfTravel(14001)));
            Assert.AreEqual(band, FlapsScale.BandKey(FlapsScale.PercentOfTravel(14200)));

            // Y las bandas siguen el recorrido de arriba abajo, sin huecos ni saltos hacia atrás.
            Assert.AreEqual(0, FlapsScale.BandKey(0.0));
            Assert.AreEqual(2, FlapsScale.BandKey(25.0));
            Assert.AreEqual(4, FlapsScale.BandKey(40.0));
            Assert.AreEqual(7, FlapsScale.BandKey(100.0));
            Assert.IsTrue(FlapsScale.BandKey(25.0) < FlapsScale.BandKey(40.0),
                "el orden de las bandas es el del mando: el log ya no puede ir hacia atrás");
        }

        [TestMethod]
        public void FueraDeRangoSeRecorta()
        {
            Assert.AreEqual(0.0, FlapsScale.PercentOfTravel(-1), 1e-9);
            Assert.AreEqual(0.0, FlapsScale.PercentOfTravel(double.NaN), 1e-9);
            Assert.AreEqual(100.0, FlapsScale.PercentOfTravel(20000), 1e-9);
        }
    }
}
