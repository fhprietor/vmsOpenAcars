using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Un aviso del RAAS, un registro.** Medido en el PIREP real `D0Wy9Pa7GNB5pYEo`:
    ///
    /// ```
    /// 01:03:09  RAAS: APPROACHING RUNWAY 26
    /// 01:03:09  🛫 APPROACHING RUNWAY 26
    /// ```
    ///
    /// y lo mismo con los otros tres avisos (`HOLD SHORT`, `OFF ROUTE`, `TAXI ROUTE COMPLETE`).
    ///
    /// La causa son **dos caminos** que escriben el mismo aviso: `UIService.AddLog` espeja al PIREP
    /// toda línea del log mientras hay vuelo abierto (`POST /acars/position`, `type = 2`), y
    /// `FlightManager.LogRaasCallout` manda el aviso por `/acars/logs` con el prefijo `RAAS:` que
    /// phpVMS distingue de las filas `CHK`. Lo que sobra es el espejo.
    ///
    /// **La voz no se toca**: solo hay una llamada a `RaasVoice.Speak` por aviso
    /// (`TelemetryCoordinator.EvaluateRaas`), así que no se locutaba dos veces. Esto es solo el log.
    /// </summary>
    [TestClass]
    public class AcarsLogMirrorTests
    {
        [TestMethod]
        public void LosCuatroAvisosDelRaas_NoSeEspejan()
        {
            // Las cuatro frases reales del vuelo, con el prefijo de la línea local.
            string[] callouts =
            {
                "🎙️ RAAS: APPROACHING RUNWAY 26",
                "🎙️ RAAS: HOLD SHORT RUNWAY 26",
                "🎙️ RAAS: OFF ROUTE, BACK TO TAXIWAY K1",
                "🎙️ RAAS: TAXI ROUTE COMPLETE",
            };

            foreach (string line in callouts)
                Assert.IsFalse(AcarsLogMirror.ShouldMirror(line),
                    $"«{line}» ya viaja por /acars/logs: espejarlo lo registra dos veces");
        }

        [TestMethod]
        public void ElRestoDelLogSigueEspejandose()
        {
            // La guía de rodaje no es un aviso del RAAS y no tiene otro camino: se sigue espejando.
            Assert.IsTrue(AcarsLogMirror.ShouldMirror(
                "🎙️ GUÍA DE RODAJE: PISTA 26 · ruta K1"));
            Assert.IsTrue(AcarsLogMirror.ShouldMirror(
                "🎙️ Nuevo punto de rodaje: ruta recalculada desde la posición actual"));

            // Y lo de siempre: fases, aterrizaje, avisos de voz del RAAS que no son un callout.
            Assert.IsTrue(AcarsLogMirror.ShouldMirror("── TAXI OUT ──"));
            Assert.IsTrue(AcarsLogMirror.ShouldMirror("🛬 Aterrizaje registrado: -346 fpm, 1.32 G"));
            Assert.IsTrue(AcarsLogMirror.ShouldMirror("⚠️ RAAS: sin voz SAPI en este equipo"));
        }

        [TestMethod]
        public void ElPrefijoSeReconoceConEspaciosDelante()
        {
            Assert.IsFalse(AcarsLogMirror.ShouldMirror("  🎙️ RAAS: APPROACHING RUNWAY 26"));
        }

        [TestMethod]
        public void UnaLineaVaciaNoSeManda()
        {
            Assert.IsFalse(AcarsLogMirror.ShouldMirror(null));
            Assert.IsFalse(AcarsLogMirror.ShouldMirror(""));
        }
    }
}
