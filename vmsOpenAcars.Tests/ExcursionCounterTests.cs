using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Una desviación = un punto. El caso es el PIREP real **`5ow69vzDNW5ZMayB`** (SKRG, 21/10/2026):
    /// el avión se apartó **−5,1°** del CRS del localizador bajo 500 ft durante **un segundo y medio**
    /// —rumbo 355° contra CRS 0°— y el registro publicó **dos** líneas idénticas (20:25:12 y 20:25:13)
    /// con **−2 pts** de castigo, porque el conteo sumaba uno por ciclo de 20 Hz con tope 2.
    ///
    /// Ahora se cuenta por **situación**: hace falta medio segundo fuera de tolerancia para darla por
    /// real, y el contador solo se re-arma cuando el avión vuelve dentro.
    /// </summary>
    [TestClass]
    public class ExcursionCounterTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 21, 20, 25, 12, DateTimeKind.Utc);
        private const double Dt = 0.05;   // 20 Hz, el ciclo real

        private static DateTime At(double seconds) => T0.AddSeconds(seconds);

        [TestMethod]
        public void LaDesviacionRealCuentaUnaVez()
        {
            // 355° contra CRS 0° = −5,1°, fuera de la tolerancia de 5°, de 20:25:12 a 20:25:13,5.
            var counter = new ExcursionCounter();
            int count = 0;

            for (double t = 0; t <= 1.5; t += Dt)
                if (counter.Update(At(t), outOfTolerance: true)) count++;

            Assert.AreEqual(1, count,
                "Un apartamiento de 1,5 s es UNA desviación: el tope de 2 no puede gastarse en ella");
        }

        [TestMethod]
        public void DosApartamientosSeparadosCuentanDos()
        {
            var counter = new ExcursionCounter();
            int count = 0;

            for (double t = 0; t <= 1.0; t += Dt)              // primera desviación
                if (counter.Update(At(t), true)) count++;
            for (double t = 1.0; t <= 2.0; t += Dt)            // vuelve al eje
                if (counter.Update(At(t), false)) count++;
            for (double t = 2.0; t <= 3.0; t += Dt)            // se vuelve a apartar
                if (counter.Update(At(t), true)) count++;

            Assert.AreEqual(2, count, "Dos desviaciones distintas son dos puntos (y el tope es 2)");
        }

        [TestMethod]
        public void UnFrameSueltoNoGastaUnaDesviacion()
        {
            var counter = new ExcursionCounter();
            int count = 0;

            if (counter.Update(At(0), true)) count++;          // una sola muestra de 50 ms
            if (counter.Update(At(Dt), false)) count++;

            Assert.AreEqual(0, count, "Medio segundo de exigencia filtra el parpadeo del simulador");
        }

        [TestMethod]
        public void MedioSegundoExactoYaCuenta()
        {
            var counter = new ExcursionCounter();
            int count = 0;

            for (double t = 0; t <= ExcursionCounter.MinDurationSec + 1e-9; t += Dt)
                if (counter.Update(At(t), true)) count++;

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void FueraDeToleranciaDiezSegundosSigueSiendoUna()
        {
            var counter = new ExcursionCounter();
            int count = 0;

            for (double t = 0; t <= 10; t += Dt)
                if (counter.Update(At(t), true)) count++;

            Assert.AreEqual(1, count, "Mientras no vuelva, es la misma desviación");
        }

        [TestMethod]
        public void ResetDejaContarDesdeCero()
        {
            var counter = new ExcursionCounter();
            int count = 0;

            for (double t = 0; t <= 1.0; t += Dt)
                if (counter.Update(At(t), true)) count++;

            counter.Reset();                                    // aproximación nueva
            for (double t = 0; t <= 1.0; t += Dt)
                if (counter.Update(At(5 + t), true)) count++;

            Assert.AreEqual(2, count);
        }
    }
}
