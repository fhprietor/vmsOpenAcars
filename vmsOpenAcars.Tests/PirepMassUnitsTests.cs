using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Unidad de masa del payload del PIREP. phpVMS guarda la masa en LIBRAS —lo dice su
    /// `config/phpvms.php` (`internal_units.fuel/mass = 'lbs'`), el metadato de su API
    /// (`internalUnit: "lbs"`) y su propio autor en el foro («Units in ACARS are just in lbs»)— y
    /// **no convierte** un número suelto: lo escribe en la columna y lo lee como libras. Nosotros
    /// calculamos en kg, así que el payload tiene que ir multiplicado por 2,20462.
    ///
    /// Los números son de un vuelo real, el PIREP `9E20We81wlBgp3Nj` (SKRG→SKBG, B38M,
    /// 05/10/2026), del que se leyeron las posiciones por la API:
    ///   - telemetría de posiciones (ya en libras): 9797 lbs al inicio, 6519 lbs al final;
    ///   - `block_fuel` enviado antes del arreglo: 4444 (kg) → phpVMS lo devolvió como
    ///     `{kg: 2015,76, lbs: 4444}`, es decir 2,20462 veces por debajo;
    ///   - `fuel_used` enviado: 1488 (kg) → `{kg: 674,95, lbs: 1488}`.
    /// </summary>
    [TestClass]
    public class PirepMassUnitsTests
    {
        /// <summary>Tolerancia para las comprobaciones de ida y vuelta con dobles.</summary>
        private const double Tolerance = 1e-9;

        [TestMethod]
        public void TheConstant_IsTheFigureTheAirlineGave()
        {
            // 2,20462, la cifra de la denuncia: si alguien la «ajusta», el PIREP vuelve a quedar
            // descuadrado en la misma proporción.
            Assert.AreEqual(2.20462, PirepMassUnits.LbsPerKg, 1e-5);
        }

        [TestMethod]
        public void RoundTrip_KgToLbsAndBack_ReturnsTheSameKg()
        {
            // Ida y vuelta del caso real y de valores extremos (0 y cargas grandes).
            foreach (double kg in new[] { 0.0, 1.0, 4444.0, 1488.0, 5300.0, 63558.0, 123456.78 })
            {
                double lbs = PirepMassUnits.KgToLbs(kg);
                Assert.AreEqual(kg, PirepMassUnits.LbsToKg(lbs), Math.Max(Tolerance, Math.Abs(kg) * Tolerance),
                    $"ida y vuelta descuadrada para {kg} kg");
            }
        }

        [TestMethod]
        public void ZeroStaysZero_ThePrefileFuelUsedGoesAsZero()
        {
            // El prefile manda `fuel_used = 0`; convertir no puede inventarse un valor.
            Assert.AreEqual(0.0, PirepMassUnits.PayloadLbs(0.0));
        }

        [TestMethod]
        public void TheRealFlight_BlockFuelGoesOutAsTheSameLbsTheTelemetryAlreadySent()
        {
            // 4444 kg de rampa son 9797 lbs: exactamente el `fuel` de la PRIMERA posición del mismo
            // vuelo (BST, 01:13:10), que ya viajaba en libras porque sale del offset 0x126C sin
            // convertir. Si el payload no da 9797, las dos vías están en unidades distintas.
            Assert.AreEqual(9797.0, PirepMassUnits.PayloadLbs(4444.0));
        }

        [TestMethod]
        public void TheRealFlight_FuelUsedMatchesTheDropMeasuredInTheTelemetry()
        {
            // 1488 kg de consumo son 3280 lbs. La caída medida en las posiciones del mismo vuelo
            // —9797 lbs al inicio, 6519 lbs en ARR— son 3278 lbs: 2 lbs de diferencia, que es el
            // último sondeo de telemetría frente al valor del PIREP. Con el número sin convertir
            // (1488) phpVMS registraba 675 kg de consumo en vez de 1488.
            double expectedBurnLbs = 9797.0 - 6519.0;
            Assert.AreEqual(3280.0, PirepMassUnits.PayloadLbs(1488.0));
            Assert.IsTrue(Math.Abs(PirepMassUnits.PayloadLbs(1488.0) - expectedBurnLbs) <= 2.0,
                "el consumo convertido debe caer dentro de 2 lbs de la caída medida en las posiciones");
        }

        [TestMethod]
        public void TheApiRoundedItBack_AsPhpVmsShowedTheUnconvertedNumber()
        {
            // Comprobación del error denunciado, del derecho y del revés: 4444 sin convertir, leído
            // por phpVMS como libras, son 2015,76 kg — el valor que devolvió su API.
            Assert.AreEqual(2015.76, PirepMassUnits.LbsToKg(4444.0), 0.01);
        }
    }
}
