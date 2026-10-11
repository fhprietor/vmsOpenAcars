using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Core.Flight;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El toque que se publica y se puntúa no puede cambiar después de copiarlo.**
    ///
    /// Lo demostró el log real, no una hipótesis: el vuelo 20 quedó con **+156 fpm** en el log local
    /// y **−155** en el PIREP, y el 23 con **+34** contra **−26**. La causa es que el toque vivo
    /// (`Fpm`, `GForce`, `Pitch`, `Bank`) **sí** se puede sobrescribir: `ResetRunwayData()` —el
    /// rearme del touch-and-go, o uno espurio del detector— pone `Captured = false` y el siguiente
    /// ciclo de 20 Hz vuelve a capturar. Y entre la copia del registro, que va **antes** del `await`
    /// de `FilePirep`, y los dos sitios donde se puntúa y se arma el payload hay `await`s en los que
    /// la telemetría sigue corriendo. El vuelo 40, que no se rearmó, coincide en los dos sitios
    /// (**+74**), y eso es lo que fija el comportamiento correcto.
    /// </summary>
    [TestClass]
    public class TouchdownStateTests
    {
        [TestMethod]
        public void ElToqueCongeladoNoCambiaAunqueSeRearmeLaCaptura()
        {
            var td = new TouchdownState();
            td.Capture(156, 4.4, 0.1, 1.28, 4.69788, -74.13721, 339);   // el toque del vuelo 20

            var congelado = td.FreezeIfNeeded();                        // la copia del registro

            td.ResetRunwayData();                                       // rearme del detector…
            td.Capture(-155, 3.0, 0.2, 1.10, 4.69788, -74.13721, 340);  // …y segunda captura

            Assert.AreEqual(156, congelado.Fpm,
                            "lo que va al payload y a la puntuación no puede moverse");
            Assert.AreEqual(-155, td.Fpm,
                            "el toque vivo sí se movió: es justo lo que hay que congelar");
        }

        [TestMethod]
        public void CongelarDosVecesDevuelveLaMismaLectura()
        {
            var td = new TouchdownState();
            td.Capture(74, 4.4, 0.1, 1.05, 7.0, -73.0, 339);            // el toque del vuelo 40

            var primera  = td.FreezeIfNeeded();
            var segunda  = td.FreezeIfNeeded();

            Assert.AreSame(primera, segunda, "el primer congelado es el que manda");
            Assert.AreEqual(74, segunda.Fpm);
        }

        [TestMethod]
        public void ElRearmeDelTouchAndGoSueltaElToqueParaElSiguienteAterrizaje()
        {
            var td = new TouchdownState();
            td.Capture(156, 4.4, 0.1, 1.28, 4.69788, -74.13721, 339);
            td.FreezeIfNeeded();

            td.ResetRunwayData();                                       // touch-and-go de verdad

            Assert.IsNull(td.Frozen,
                          "el toque del ciclo anterior no puede informarse como el nuevo");

            td.Capture(-155, 3.0, 0.2, 1.10, 4.69788, -74.13721, 340);
            Assert.AreEqual(-155, td.FreezeIfNeeded().Fpm,
                            "el ciclo nuevo se puntúa y se publica con SU toque");
        }

        [TestMethod]
        public void ElResetDelVueloNoDejaToqueCongelado()
        {
            var td = new TouchdownState();
            td.Capture(156, 4.4, 0.1, 1.28, 4.69788, -74.13721, 339);
            td.FreezeIfNeeded();

            td.Reset();

            Assert.IsNull(td.Frozen, "un vuelo nuevo no puede heredar el toque del anterior");
            Assert.IsNull(td.Fpm);
            Assert.IsFalse(td.Captured);
        }

        [TestMethod]
        public void SinCapturaLaLecturaCongeladaLoDice()
        {
            // El caso que NO puede parecer una medida: sin toque capturado el `Captured` va a false y
            // el `Fpm` a null, que es lo que `PirepBuilder` convierte en el centinela y luego en 0.
            var td = new TouchdownState();

            var congelado = td.FreezeIfNeeded();

            Assert.IsFalse(congelado.Captured);
            Assert.IsNull(congelado.Fpm);
        }
    }
}
