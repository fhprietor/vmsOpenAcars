using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Un aviso de backtrack por maniobra.** Caso real: PIREP **`ez9OqzRnYxbnGLR8`** (B738 PMDG,
    /// MSFS 2024, SKBO→SKBG).
    ///
    /// ```
    /// 02:26:15  ↩️ BACKTRACK RWY 17
    /// 02:26:39  ↩️ BACKTRACK RWY 17     ← la MISMA maniobra, 24 s después
    /// ```
    ///
    /// Eran dos avisos porque el seguimiento de rodaje tiene dos ramas —entrada en pista y
    /// llegada— y cada una llevaba su propia bandera: al pasar la fase a `TaxiIn` con el avión
    /// todavía en la pista, la segunda rama veía «entrada nueva». Ahora el estado es uno y se
    /// re-arma saliendo de la pista, que es lo que separa una maniobra de la siguiente.
    /// </summary>
    [TestClass]
    public class BacktrackCalloutTests
    {
        [TestMethod]
        public void LaMismaEntradaEnPista_SeAnunciaUnaVez()
        {
            var callout = new BacktrackCallout();

            // 02:26:15 — el avión acaba de entrar en la 17 retrocediendo.
            Assert.IsTrue(callout.ShouldAnnounce(confirmedOnRunway: true, isBacktrack: true),
                "la maniobra se anuncia al entrar");

            // 02:26:39 — sigue en la pista, ahora en fase de llegada: es la misma maniobra.
            Assert.IsFalse(callout.ShouldAnnounce(confirmedOnRunway: true, isBacktrack: true),
                "24 s después la maniobra no es otra: no se repite el aviso");
        }

        /// <summary>
        /// Salir de la pista re-arma el aviso: dos maniobras distintas (entrar, salir, volver a
        /// entrar retrocediendo) son dos avisos.
        /// </summary>
        [TestMethod]
        public void SalirDeLaPista_RearmaElAviso()
        {
            var callout = new BacktrackCallout();

            Assert.IsTrue (callout.ShouldAnnounce(true, true));
            Assert.IsFalse(callout.ShouldAnnounce(true, true));
            Assert.IsFalse(callout.ShouldAnnounce(confirmedOnRunway: false, isBacktrack: false));
            Assert.IsTrue (callout.ShouldAnnounce(true, true), "otra entrada en pista es otra maniobra");
        }

        /// <summary>
        /// Entrar en la pista **sin** retroceder no consume el aviso: si luego el avión gira y
        /// retrocede, se anuncia entonces (es cuando la maniobra existe).
        /// </summary>
        [TestMethod]
        public void EntrarSinRetroceder_NoConsumeElAviso()
        {
            var callout = new BacktrackCallout();

            Assert.IsFalse(callout.ShouldAnnounce(true, isBacktrack: false),
                "entrar derecho no es un backtrack");
            Assert.IsTrue (callout.ShouldAnnounce(true, isBacktrack: true),
                "cuando gira y retrocede, sí");
        }

        [TestMethod]
        public void Reset_DejaElEstadoComoUnVueloNuevo()
        {
            var callout = new BacktrackCallout();
            Assert.IsTrue(callout.ShouldAnnounce(true, true));

            callout.Reset();

            Assert.IsTrue(callout.ShouldAnnounce(true, true), "vuelo nuevo, maniobra nueva");
        }
    }
}
