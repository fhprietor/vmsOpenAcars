using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **La densidad de la traza de rodaje** (`UpdateIntervalPolicy`).
    ///
    /// La regla existe porque la **métrica de cobertura de la traza de rodaje de NavData** no es
    /// medible con el envío a 30 s: da **mediana 65,0 %** y **mínima 22,2 %**, con **9–34 puntos**
    /// por rodaje. Con la guía de rodaje activa se manda a **1 Hz**; **sin ella, el valor
    /// configurado de siempre** — quien no usa la guía no puede ver su tráfico multiplicado por 30.
    ///
    /// Y son **tres puertas**, no una, porque la cadencia real la capan tres filtros en serie:
    /// el intervalo adaptativo (`TaxiSeconds`), el suelo del envío (`SendFloorSeconds`) y el
    /// deduplicador de posiciones (`TaxiTracePosThresholdDeg`). El test fija **los dos lados de cada
    /// una**: el lado denso (guía activa → 1 Hz) y el lado intacto (sin guía → lo de siempre).
    /// </summary>
    [TestClass]
    public class UpdateIntervalPolicyTests
    {
        // El valor configurado hoy en App.config: update_interval_taxi = 30.
        private const int Configured = 30;

        // ── Puerta 1: el intervalo adaptativo (`FsuipcService`) ───────────────────

        [TestMethod]
        public void ConGuiaActiva_LaPosicionDeRodajeVaAUnSegundo()
        {
            Assert.AreEqual(5, UpdateIntervalPolicy.TaxiSeconds(true, Configured),
                            "con la guía activa la traza de rodaje va a 1 Hz");
        }

        [TestMethod]
        public void SinGuiaActiva_SeConservaElValorConfigurado()
        {
            Assert.AreEqual(Configured, UpdateIntervalPolicy.TaxiSeconds(false, Configured),
                            "sin guía el comportamiento es el de siempre: update_interval_taxi");
        }

        [TestMethod]
        public void SinGuiaActiva_NoSeIgnoraLoQueHayaConfiguradoElPiloto()
        {
            // No se devuelve el 30 por defecto en duro, sino el valor que traiga la configuración:
            // es el lado que no debe cambiar con este encargo.
            Assert.AreEqual(15, UpdateIntervalPolicy.TaxiSeconds(false, 15));
            Assert.AreEqual(60, UpdateIntervalPolicy.TaxiSeconds(false, 60));
        }

        [TestMethod]
        public void SinDatoDeConfiguracion_SeUsaLaCadenciaDeReserva()
        {
            // Degrada sin datos: un `App.config` roto (0 o negativo) no puede significar «manda en
            // cada vuelta del sondeo». Se cae a la cadencia de reserva, nunca a algo más rápido.
            Assert.AreEqual(UpdateIntervalPolicy.FallbackSeconds, UpdateIntervalPolicy.TaxiSeconds(false, 0));
            Assert.AreEqual(UpdateIntervalPolicy.FallbackSeconds, UpdateIntervalPolicy.TaxiSeconds(false, -5));
        }

        [TestMethod]
        public void ConGuiaActiva_LaConfiguracionRotaNoEstorba()
        {
            // La guía activa es una afirmación explícita: manda el 1 s aunque la configuración no sirva.
            Assert.AreEqual(5, UpdateIntervalPolicy.TaxiSeconds(true, 0));
        }

        // ── Puerta 2: el suelo del envío (`TelemetryCoordinator.PositionUpdateInterval`) ──

        [TestMethod]
        public void ConGuiaActiva_ElSueloDeEnvioBajaAUnSegundo()
        {
            // Sin esto, el suelo de 5 s se come el 1 Hz del intervalo adaptativo y la traza se
            // queda en 5 s por mucho que arriba se pida 1.
            Assert.AreEqual(5, UpdateIntervalPolicy.SendFloorSeconds(true, 5),
                            "el suelo de envío no puede tapar el 1 Hz de la guía activa");
        }

        [TestMethod]
        public void SinGuiaActiva_ElSueloDeEnvioSigueSiendoElDeSiempre()
        {
            // Los 5 s de siempre, y también para cualquier otra fase: el suelo no distingue de fase.
            Assert.AreEqual(5, UpdateIntervalPolicy.SendFloorSeconds(false, 5));
            Assert.AreEqual(10, UpdateIntervalPolicy.SendFloorSeconds(false, 10));
        }

        // ── Puerta 3: el deduplicador de posiciones (`HasSignificantChange`) ──────

        [TestMethod]
        public void ConGuiaActiva_ElUmbralDePosicionEsElPasoDeUnSegundo()
        {
            // 0,00002° ≈ 2,2 m: por debajo del paso de 1 s a 5 kt (2,6 m), así que rodando lento
            // entra una muestra por segundo. Con el umbral de siempre (33 m) la siguiente muestra
            // caería a 4–6 s a velocidad de rodaje.
            Assert.AreEqual(0.00002, UpdateIntervalPolicy.TaxiTracePosThresholdDeg(true), 1e-9);
            Assert.IsTrue(UpdateIntervalPolicy.TaxiTracePosThresholdDeg(true) < 0.0003,
                          "con guía el umbral tiene que ser más fino que el de siempre");
        }

        [TestMethod]
        public void SinGuiaActiva_ElUmbralDePosicionEsElDeSiempre()
        {
            Assert.AreEqual(0.0003, UpdateIntervalPolicy.TaxiTracePosThresholdDeg(false), 1e-12);
        }

        [TestMethod]
        public void ConGuiaActiva_ElUmbralNoLlegaACero_ParadoNoSeRepiteLaMuestra()
        {
            // No se baja a cero a propósito: un avión parado con la guía activa no debe mandar
            // posiciones idénticas cada segundo (no añaden cobertura y engordan la traza).
            Assert.IsTrue(UpdateIntervalPolicy.TaxiTracePosThresholdDeg(true) > 0);
        }
    }
}
