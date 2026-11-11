using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Qué G decide la nota del aterrizaje**, con las dos cifras del PIREP real
    /// `D0Wy9Pa7GNB5pYEo` (AT76, MSFS 2024, SKPE→SKMD, cliente v0.9.67):
    ///
    /// ```
    /// 01:36:32  G-Force: 1.32g (Normal)                 ← acelerómetro 0x11BA
    /// 01:36:32  Landing recorded: -346 fpm, 1.85 G …    ← tabla del descenso
    /// 01:43:04  −15 pts: G-Force (1.85g at touchdown)
    /// ```
    ///
    /// **El que decide es el medido.** Con **1,32 g** el escalón del scoring es `≤1.5g = 0 puntos`;
    /// con **1,85 g** es `>1.7g = 15 puntos`. Y la física está del lado del medido: un contacto de
    /// **−346 fpm** es un firme normal, no uno de 1,85 g. El vuelo anterior `nROPdk4GxYY5AOy1`
    /// registró **1,05 G** sin penalización —también salido de la tabla, `vs <= 90`—, así que el
    /// defecto venía de lejos y no era un caso aislado.
    ///
    /// Lo que se fija aquí es la **ventana**: el pico de la lectura que termina en el contacto, no el
    /// de todo el vuelo (que era lo que hacía `_peakGforceApproach` y podía coronar una turbulencia
    /// del ascenso) y no el de después del contacto (que ya es el rebote).
    /// </summary>
    [TestClass]
    public class TouchdownGForceWindowTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 10, 1, 36, 30, DateTimeKind.Utc);

        [TestMethod]
        public void ElToqueReal_Da132_YNoEl1_85DeLaTabla()
        {
            var window = new TouchdownGForceWindow();

            // La final del ATR, a 20 Hz: el sensor va plano en 1,00–1,05 g y sube en el contacto.
            double[] approach = { 1.00, 1.01, 1.00, 1.02, 1.01, 1.03, 1.02, 1.05, 1.08, 1.12,
                                  1.18, 1.24, 1.28, 1.30, 1.32 };
            for (int i = 0; i < approach.Length; i++)
                window.Observe(approach[i], T0.AddSeconds(i * 0.05));

            // El frame del contacto ya está en tierra, así que no entra en la ventana: se suma aparte,
            // que es lo que hace `HandleTouchdown` con `Math.Max(Peak, CurrentGForce)`.
            double touchdownG = Math.Max(window.Peak, 1.32);

            Assert.AreEqual(1.32, touchdownG, 0.001,
                "el G del toque es el que midió el avión");
            Assert.IsTrue(touchdownG <= 1.5,
                "1,32 g cae en el escalón ≤1.5g del scoring: 0 puntos, no los 15 del 1,85");
            Assert.IsTrue(touchdownG < 1.7,
                "y no cruza el umbral de 1,7 g que costaba 15 puntos");
        }

        [TestMethod]
        public void UnaRachaDelAscenso_NoEsElGDelAterrizaje()
        {
            var window = new TouchdownGForceWindow();

            // 1,60 g cuatro segundos antes del contacto (una racha en el descenso)…
            window.Observe(1.60, T0);
            window.Observe(1.02, T0.AddSeconds(1.0));

            // …y la final tranquila, que es la que toca.
            window.Observe(1.05, T0.AddSeconds(3.95));
            window.Observe(1.20, T0.AddSeconds(4.0));

            Assert.AreEqual(1.20, window.Peak, 0.001,
                "la ventana es de 1 s: la racha de hace 4 s ya no está");
        }

        [TestMethod]
        public void LaVentanaOlvidaLoQueQuedaFuera()
        {
            var window = new TouchdownGForceWindow();
            window.Observe(1.90, T0);
            Assert.AreEqual(1.90, window.Peak, 0.001);

            // Exactamente un segundo después, la muestra vieja sigue dentro (borde incluido)…
            window.Observe(1.10, T0.AddSeconds(TouchdownGForceWindow.WindowSeconds));
            Assert.AreEqual(1.90, window.Peak, 0.001);

            // …y un pelo más tarde ya no.
            window.Observe(1.10, T0.AddSeconds(TouchdownGForceWindow.WindowSeconds + 0.1));
            Assert.AreEqual(1.10, window.Peak, 0.001);
            Assert.AreEqual(2, window.Count, "quedan la del borde y la nueva");
        }

        [TestMethod]
        public void SinMuestras_NoHayMedida()
        {
            var window = new TouchdownGForceWindow();

            // 0 es «no medido», no 1 g: el scoring omite el criterio en vez de inventar.
            Assert.AreEqual(0.0, window.Peak, 1e-9);
            Assert.AreEqual(0, window.Count);

            // Y una lectura imposible tampoco entra.
            window.Observe(0.0, T0);
            window.Observe(double.NaN, T0);
            window.Observe(-0.5, T0);
            Assert.AreEqual(0.0, window.Peak, 1e-9);
        }

        [TestMethod]
        public void ElDespegueSueltaLaVentana()
        {
            var window = new TouchdownGForceWindow();
            window.Observe(1.32, T0);
            window.Reset();

            Assert.AreEqual(0.0, window.Peak, 1e-9,
                "un touch-and-go no puede heredar el toque del que acaba de despegar");
        }
    }
}
