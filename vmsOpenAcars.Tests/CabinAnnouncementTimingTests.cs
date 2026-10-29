using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **La oportunidad de los dos anuncios de cabina que llegaban tarde.** El piloto lo reportó con
    /// el PIREP `r4JNk2b2bBMJd8lD` (B738, SKCC→SKUC, 29/10/2026): el PA de pushback sonaba con el
    /// avión ya empujado y el de desarmar toboganes con el avión todavía frenando en la pista.
    ///
    /// Los tiempos del registro de ese vuelo son los casos de estos tests:
    /// `01:54:34` freno liberado · `01:54:42` beacon · `01:54:47` `── PUSHBACK ──` · `01:55:24` freno
    /// puesto (fin del empuje) — y en la llegada: `02:38:28` toque a **145 kt** · `02:38:29`
    /// `── AFTER LANDING ──` · `02:38:51` `── TAXI IN ──` (la fase entra a **40 kt**, aún en pista).
    /// </summary>
    [TestClass]
    public class CabinAnnouncementTimingTests
    {
        // ── Pushback: se anuncia ANTES de moverse ────────────────────────────────

        [TestMethod]
        public void ElPushbackSeAnunciaEstandoAunEnElPuesto()
        {
            // 01:54:34 y 01:54:42 del vuelo real: el avión sigue parado (fase Boarding).
            Assert.IsTrue(CabinPushbackPolicy.AnnounceBeforePush(FlightPhase.Boarding, false),
                "con el beacon o el freno liberado y el avión aún en el puesto, es el momento");
        }

        [TestMethod]
        public void ElPushbackNoSeRepite()
        {
            Assert.IsFalse(CabinPushbackPolicy.AnnounceBeforePush(FlightPhase.Boarding, true),
                "una vez dicho, no se vuelve a decir");
        }

        [TestMethod]
        public void YaEnPushbackElAvisoNoEsTemprano()
        {
            // La fase `Pushback` solo se declara con ~11 s de movimiento: aquí el anuncio ya llega
            // con el avión empujado, así que no cuenta como el aviso temprano.
            Assert.IsFalse(CabinPushbackPolicy.AnnounceBeforePush(FlightPhase.Pushback, false));
        }

        [TestMethod]
        public void LaEntradaEnFaseEsLaRed()
        {
            // El vuelo que arrancó con el empuje ya en curso (`rMBVEKj5PZzx0pX9`) no tiene señal
            // temprana: mejor un aviso tarde que ninguno.
            Assert.IsTrue(CabinPushbackPolicy.AnnounceOnPhaseEntry(false));
            Assert.IsFalse(CabinPushbackPolicy.AnnounceOnPhaseEntry(true),
                "pero no repite el que ya se dijo antes de moverse");
        }

        // ── Llegada: cuando ya no está sobre la pista ────────────────────────────

        [TestMethod]
        public void EnElToqueNoSeAnunciaNada()
        {
            // 02:38:28: 145 kt y las ruedas en la pista.
            Assert.IsFalse(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.AfterLanding, 145, true));
        }

        [TestMethod]
        public void RodandoPorLaPistaTampoco()
        {
            // 02:38:51: la fase ya es `TaxiIn` porque la máquina usa GS < 40 kt, pero sigue en pista.
            Assert.IsFalse(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.TaxiIn, 40, true),
                "la fase TaxiIn no prueba que el avión haya despejado la pista");
            Assert.IsFalse(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.TaxiIn, 25, true));
        }

        [TestMethod]
        public void AlDespejarLaPistaSi()
        {
            Assert.IsTrue(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.AfterLanding, 30, false));
            Assert.IsTrue(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.TaxiIn, 20, false));
        }

        [TestMethod]
        public void SinDatoDePistaSeAnunciaIgual()
        {
            // Sin NavData o sin aeropuerto de llegada no se puede saber: la regla degrada hacia el
            // piloto informado, no hacia el silencio.
            Assert.IsTrue(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.TaxiIn, 25, null));
        }

        [TestMethod]
        public void FueraDeLasFasesDeLlegadaNoSeAnuncia()
        {
            Assert.IsFalse(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.Enroute, 250, false),
                "no es una llegada");
            Assert.IsFalse(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.Boarding, 0, false),
                "ni el principio del vuelo");
            Assert.IsFalse(CabinRunwayExitPolicy.IsClearOfRunway(FlightPhase.TaxiOut, 15, false),
                "el rodaje de salida tiene su propio anuncio");
        }
    }
}
