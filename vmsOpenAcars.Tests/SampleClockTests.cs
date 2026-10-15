using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Qué hueco entre dos ciclos de telemetría se puede sumar.** Lo comparten los dos contadores de
    /// tiempo del vuelo —el rodaje con un solo motor y la fase atascada—, así que una regla equivocada
    /// aquí mueve las dos cosas a la vez.
    ///
    /// El sondeo del cliente es de **20 Hz** (`polling_interval_ms = 50`), o sea 0,05 s por ciclo: un
    /// hueco de segundos ya son decenas de ciclos perdidos, y uno de horas es el portátil suspendido a
    /// mitad de vuelo. Contarlo regalaría minutos de rodaje que no se volaron, y con ellos una
    /// bonificación.
    /// </summary>
    [TestClass]
    public class SampleClockTests
    {
        [TestMethod]
        public void ElHuecoDeUnCicloNormal_SeSumaEntero()
        {
            Assert.AreEqual(0.05, SampleClock.CountableSeconds(0.05), 1e-9);
            Assert.AreEqual(1.00, SampleClock.CountableSeconds(1.00), 1e-9);
        }

        [TestMethod]
        public void ElHuecoDelTope_EntraYElDeJustoDespues_No()
        {
            Assert.AreEqual(SampleClock.MaxGapSeconds, SampleClock.CountableSeconds(SampleClock.MaxGapSeconds), 1e-9,
                "el tope es el hueco máximo aceptable: entra");
            Assert.AreEqual(0.0, SampleClock.CountableSeconds(SampleClock.MaxGapSeconds + 0.01), 1e-9);
        }

        [TestMethod]
        public void UnaSuspensionDelEquipoNoEsTiempoDeVuelo()
        {
            Assert.AreEqual(0.0, SampleClock.CountableSeconds(3600.0), 1e-9,
                "una hora entre dos ciclos no es una hora de rodaje");
        }

        [TestMethod]
        public void UnRelojQueRetrocede_TampocoSuma()
        {
            Assert.AreEqual(0.0, SampleClock.CountableSeconds(0.0),  1e-9);
            Assert.AreEqual(0.0, SampleClock.CountableSeconds(-1.0), 1e-9);
        }
    }
}
