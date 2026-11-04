using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El resumen de viento de la final** (`Helpers/FinalWindSummary`): máximos de cruzada y de
    /// cola de la traza de aproximación, con la distancia al umbral donde ocurrieron.
    ///
    /// Es la regla que decide qué se enseña en la franja «VIENTO EN LA FINAL» del análisis del
    /// aterrizaje. La descomposición no se prueba aquí —ya la fija `WindComponentsTests`—; lo que se
    /// fija es la **agregación**: puntos sin dato ignorados, hueco distinto de calma, cola como
    /// magnitud positiva y distancia nunca negativa pasada la vertical del umbral.
    ///
    /// Se usa el rumbo **verdadero** real de la 14R de SKBO (`127,2315°`), el mismo que usan
    /// `WindComponentsTests` y `PirepLandingWeatherTests`, para no inventar geometría.
    /// </summary>
    [TestClass]
    public class FinalWindSummaryTests
    {
        /// <summary>Rumbo verdadero de la 14R de SKBO con el que se guardó el viento del toque.</summary>
        private const double Skbo14RTrue = 127.231532474263;

        private static ApproachTrackPoint Punto(double distNm, double? dir, double? speed)
            => new ApproachTrackPoint { DistNm = distNm, WindDirDeg = dir, WindSpeedKt = speed };

        /// <summary>
        /// Con viento en la traza, el resumen se queda con el **máximo** de cada componente y con su
        /// distancia. Los tres puntos usan el rumbo real de la 14R: uno de cara (cruzada 0), uno
        /// desde la derecha (cruzada +14) y uno en cola (cola 6).
        /// </summary>
        [TestMethod]
        public void ConPuntosDeViento_SeQuedaConElMaximoDeCadaComponenteYSuDistancia()
        {
            var track = new List<ApproachTrackPoint>
            {
                Punto(4.0, Skbo14RTrue,             10),   // de cara: cruzada 0
                Punto(3.2, Skbo14RTrue + 90,        14),   // desde la derecha: cruzada 14
                Punto(1.8, Skbo14RTrue + 180,        6),   // de cola: cola 6
            };

            var resumen = FinalWindSummary.Compute(track, Skbo14RTrue);

            Assert.IsTrue(resumen.HasWind, "hay tres lecturas de viento: la franja se tiene que ver");
            Assert.IsFalse(resumen.AllCalm);
            Assert.AreEqual(3, resumen.WindSamples);

            Assert.AreEqual(14.0, resumen.MaxCrosswindKt, 1e-6, "la cruzada máxima son los 14 kt");
            Assert.AreEqual(3.2,  resumen.MaxCrosswindDistNm, 1e-6, "ocurrió a 3,2 NM del umbral");

            Assert.IsTrue(resumen.HasTailwind, "el punto en cola aporta el máximo de cola");
            Assert.AreEqual(6.0, resumen.MaxTailwindKt, 1e-6);
            Assert.AreEqual(1.8, resumen.MaxTailwindDistNm, 1e-6);
        }

        /// <summary>
        /// Los puntos con NULL son **huecos**, no calma: se ignoran para los máximos y no arrastran
        /// la distancia. Es el caso de los vuelos que mezclan tramos con lectura de FSUIPC y tramos
        /// sin ella (o de un addon que deja de publicar el viento).
        /// </summary>
        [TestMethod]
        public void LosPuntosSinDato_SeIgnoranYNoCuentanComoCalma()
        {
            var track = new List<ApproachTrackPoint>
            {
                Punto(5.0, null, null),                  // sin dato: no cuenta
                Punto(4.0, Skbo14RTrue + 90, 9),         // cruzada 9 @ 4,0
                Punto(3.0, null, 12),                    // media lectura: tampoco es dato
                Punto(2.0, Skbo14RTrue + 180, 4),        // cola 4 @ 2,0
                Punto(1.0, null, null),
            };

            var resumen = FinalWindSummary.Compute(track, Skbo14RTrue);

            Assert.IsTrue(resumen.HasWind);
            Assert.IsFalse(resumen.AllCalm, "un hueco no puede convertir el vuelo en calma");
            Assert.AreEqual(2, resumen.WindSamples, "solo dos puntos traían dirección y velocidad");
            Assert.AreEqual(9.0, resumen.MaxCrosswindKt, 1e-6);
            Assert.AreEqual(4.0, resumen.MaxCrosswindDistNm, 1e-6);
            Assert.AreEqual(4.0, resumen.MaxTailwindKt, 1e-6);
            Assert.AreEqual(2.0, resumen.MaxTailwindDistNm, 1e-6);
        }

        /// <summary>
        /// Sin ningún punto con viento **no hay resumen**: la franja no se pinta. Es el caso de los
        /// vuelos anteriores a `approach_track.wind_*` y de las trazas muestreadas con FSUIPC
        /// desconectado; no puede salir una línea con ceros.
        /// </summary>
        [TestMethod]
        public void SinNingunDato_NoHayResumen()
        {
            var soloHuecos = new List<ApproachTrackPoint>
            {
                Punto(5.0, null, null),
                Punto(3.0, null, null),
                Punto(1.0, null, null),
            };

            var conHuecos = FinalWindSummary.Compute(soloHuecos, Skbo14RTrue);
            Assert.IsFalse(conHuecos.HasWind, "sin dato no se inventa un resumen");
            Assert.AreEqual(0, conHuecos.WindSamples);
            Assert.IsFalse(conHuecos.HasTailwind);

            var vacio = FinalWindSummary.Compute(new List<ApproachTrackPoint>(), Skbo14RTrue);
            Assert.IsFalse(vacio.HasWind);

            var nulo = FinalWindSummary.Compute(null, Skbo14RTrue);
            Assert.IsFalse(nulo.HasWind, "una traza ausente tampoco puede tumbar el análisis");
        }

        /// <summary>
        /// **La calma es un dato, no un hueco.** Un punto con 0 kt y dirección publicada (aquí 250)
        /// llega a la base como 0/250 —el centinela 0/0 se descarta antes de guardar— y tiene que
        /// producir un resumen: la franja dice «calma». Es justo lo contrario de los puntos NULL del
        /// test anterior, y por eso se distinguen.
        /// </summary>
        [TestMethod]
        public void VientoEnCalma_EsUnDatoValidoYNoUnHueco()
        {
            var track = new List<ApproachTrackPoint>
            {
                Punto(4.0, 250, 0),
                Punto(2.0, 250, 0),
            };

            var resumen = FinalWindSummary.Compute(track, Skbo14RTrue);

            Assert.IsTrue(resumen.HasWind, "0 kt es una observación de calma, no la ausencia de dato");
            Assert.IsTrue(resumen.AllCalm);
            Assert.AreEqual(2, resumen.WindSamples);
            Assert.AreEqual(0.0, resumen.MaxCrosswindKt, 1e-9);
            Assert.IsFalse(resumen.HasTailwind, "en calma no hay cola");
        }

        /// <summary>
        /// **El máximo de cola es una magnitud.** En el convenio de `WindComponents` la cola llega
        /// como cara **negativa** (`HW -6`), así que el resumen tiene que quedarse con 6 y no con
        /// −6, y no puede confundir el máximo de cara (30 kt) con el de cola: comparar los valores
        /// con signo elegiría la cara más fuerte como si fuera cola.
        /// </summary>
        [TestMethod]
        public void LaColaMaxima_SeGuardaEnPositivoYNoSeConfundeConLaCara()
        {
            var track = new List<ApproachTrackPoint>
            {
                Punto(4.0, Skbo14RTrue,        30),   // cara 30: no es cola
                Punto(2.5, Skbo14RTrue + 180,   9),   // cola 9
                Punto(1.8, Skbo14RTrue + 180,   6),   // cola 6
            };

            var resumen = FinalWindSummary.Compute(track, Skbo14RTrue);

            Assert.AreEqual(9.0, resumen.MaxTailwindKt, 1e-6,
                            "la cola máxima es la mayor magnitud negativa, en positivo");
            Assert.AreEqual(2.5, resumen.MaxTailwindDistNm, 1e-6);
            Assert.IsTrue(resumen.MaxTailwindKt > 0.0,
                          "la cola máxima se guarda como magnitud positiva, no como −9");
        }

        /// <summary>
        /// Pasado el umbral la traza sigue un par de segundos y `DistNm` puede ser negativo. La
        /// distancia del resumen se publica al umbral: «−0,1 NM» no significa nada para el piloto y
        /// sería una distancia a la que no se puede ir.
        /// </summary>
        [TestMethod]
        public void PasadoElUmbral_LaDistanciaSeQuedaEnCero()
        {
            var track = new List<ApproachTrackPoint>
            {
                Punto(-0.2, Skbo14RTrue + 180, 7),
            };

            var resumen = FinalWindSummary.Compute(track, Skbo14RTrue);

            Assert.AreEqual(7.0, resumen.MaxTailwindKt, 1e-6);
            Assert.AreEqual(0.0, resumen.MaxTailwindDistNm, 1e-9,
                            "la distancia al umbral nunca es negativa");
        }

        /// <summary>
        /// Sin rumbo de pista no se pueden descomponer las componentes, y sin componentes no hay
        /// resumen que enseñar: el formulario degrada y **no** pinta la franja. Es el caso de un
        /// vuelo con viento en la traza pero sin `landing_runway_true_deg`.
        /// </summary>
        [TestMethod]
        public void SinRumboDePista_NoHayResumen()
        {
            var track = new List<ApproachTrackPoint>
            {
                Punto(3.0, 200, 12),
            };

            var resumen = FinalWindSummary.Compute(track, null);

            Assert.IsFalse(resumen.HasWind);
            Assert.AreEqual(0, resumen.WindSamples);
        }

        /// <summary>
        /// El centinela del muestreo: FSUIPC publica **0/0** cuando no tiene viento y también deja
        /// las dos propiedades a 0 al desconectarse. Ese 0/0 se guarda como NULL en la traza —un
        /// «norte en calma» inventado mentiría sobre todo el descenso—, pero una lectura real de
        /// 0 kt con dirección distinta de 0 **sí** es calma y se guarda.
        /// </summary>
        [TestMethod]
        public void ElCentinelaCeroCero_SeGuardaComoHuecoYNoComoCalma()
        {
            Assert.IsTrue(FinalWindSummary.IsMissingSample(true, 0, 0),
                          "0/0 es el centinela de «sin dato» del simulador");
            Assert.IsTrue(FinalWindSummary.IsMissingSample(false, 12, 250),
                          "sin conexión no hay lectura que guardar");
            Assert.IsFalse(FinalWindSummary.IsMissingSample(true, 0, 250),
                           "0 kt con dirección es calma observada: es un dato");
            Assert.IsFalse(FinalWindSummary.IsMissingSample(true, 12, 250));
        }
    }
}
