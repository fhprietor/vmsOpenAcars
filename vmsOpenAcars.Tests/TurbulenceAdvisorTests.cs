using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Cuándo se anuncia la turbulencia y cuándo no.
    ///
    /// El caso que manda es el **PIREP real `5ow69vzDNW5ZMayB`** (KSEA→SKRG, 21/10/2026, v0.9.49):
    /// el cliente anunció turbulencia a las 13:17:54 y «cabina libre» a las 13:19:12, **dos minutos
    /// después del despegue y a 5 200 ft subiendo a 1 800 fpm**. Lo que el avión marcaba era
    /// **`g 0,83`** y **`VS 5221 fpm`**: un ascenso acelerando. Con el detector anterior —muestras
    /// con `|g − 1| ≥ 0,15` y reversos del VS— eso encajaba perfecto: g desviado son *todos* los
    /// muestreos durante minutos, así que declaraba turbulencia permanente mientras duró la subida.
    ///
    /// Los tests de aquí abajo fijan la regla nueva: **la turbulencia es oscilación**, un desvío
    /// sostenido no es nada. La serie del ascenso usa el perfil **real** de VS de las filas `ICL` de
    /// ese PIREP y el `g` que el cliente registró en el aviso (0,83 sostenido): no hay g por posición
    /// en el PIREP, y ese 0,83 es exactamente lo que disparó el fallo.
    /// </summary>
    [TestClass]
    public class TurbulenceAdvisorTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 21, 13, 17, 9, DateTimeKind.Utc);

        /// <summary>Perfil real del ascenso (filas `ICL` del PIREP), en fpm y una cada ~15 s.</summary>
        private static readonly double[] RealClimbVs =
        {
            3344, 5536, 1801, 717, 842, 1684, 2065, 1802, 1794, 2239,
            2604, 1672, 2128, 2165, 2887, 2641, 1689
        };

        private sealed class Runner
        {
            internal const double Dt = 0.25;   // el bucle real va a 20 Hz; 4 Hz basta para la política

            internal readonly TurbulenceAdvisor Advisor = new TurbulenceAdvisor();
            internal DateTime Now = T0;
            internal readonly List<TurbulenceCallout> Calls = new List<TurbulenceCallout>();

            internal void One(double g, bool airborne = true, double agl = 30000)
            {
                Now = Now.AddSeconds(Dt);
                TurbulenceCallout call = Advisor.Feed(Now, g, airborne, agl);
                if (call != TurbulenceCallout.None) Calls.Add(call);
            }

            /// <summary>Mantiene un valor de g durante N segundos.</summary>
            internal void Hold(double seconds, double g, bool airborne = true, double agl = 30000)
            {
                int steps = Math.Max(1, (int)Math.Round(seconds / Dt));
                for (int i = 0; i < steps; i++) One(g, airborne, agl);
            }

            /// <summary>Rampa lineal: es lo que hace un ascenso o un cambio de nivel.</summary>
            internal void Ramp(double seconds, double from, double to)
            {
                int steps = Math.Max(1, (int)Math.Round(seconds / Dt));
                for (int i = 1; i <= steps; i++) One(from + (to - from) * i / steps);
            }

            /// <summary>Oscilación: medio ciclo arriba y medio abajo con el semiperíodo dado.</summary>
            internal void Oscillate(double seconds, double halfPeriod = 1.0, double high = 1.25, double low = 0.85)
            {
                double t = 0;
                bool up = true;
                while (t < seconds)
                {
                    double leg = Math.Min(halfPeriod, seconds - t);
                    Hold(leg, up ? high : low);
                    t += leg;
                    up = !up;
                }
            }

            internal int Count(TurbulenceCallout kind) => Calls.Count(c => c == kind);
        }

        [TestMethod]
        public void ElAscensoDelPirepRealNoEsTurbulencia()
        {
            // 871 → 4 700 ft subiendo, con el g = 0,83 que el cliente registró al disparar el aviso
            // falso y el ruido del sensor encima. El VS real va en RealClimbVs: ya no es una entrada
            // del detector, y ese es justamente el arreglo.
            var run = new Runner();
            var noise = new Random(20261021);

            Assert.AreEqual(17, RealClimbVs.Length, "Las filas ICL del PIREP, ni una más");

            run.Hold(10, 1.00);                                   // rodaje y rotación
            for (int i = 0; i < RealClimbVs.Length; i++)
                run.Hold(15, 0.83 + (noise.NextDouble() - 0.5) * 0.04);   // 0,83 ± 0,02

            Assert.AreEqual(0, run.Calls.Count,
                "Un ascenso acelerando (g desviado de forma sostenida, VS saltando entre 717 y 5 536 fpm) " +
                "no es una zona de turbulencia: es el fallo que se corrigió");
        }

        [TestMethod]
        public void UnDesvioSostenidoDeGNoEsUnaZona()
        {
            // El error de raíz, aislado: g fuera de la banda un minuto entero. Antes eran 240
            // muestras «desviadas» y turbulencia permanente; ahora no hay ni una oscilación.
            var run = new Runner();
            run.Hold(60, 0.80);
            Assert.AreEqual(0, run.Calls.Count);
        }

        [TestMethod]
        public void CambiarDeNivelTampocoEsTurbulencia()
        {
            // Lo que hace un «level-off»: g sube a 1,15, baja a 0,90 y vuelve. Un cruce de banda
            // como mucho, muy lejos de los tres en seis segundos.
            var run = new Runner();
            run.Ramp(10, 1.00, 1.15);
            run.Ramp(10, 1.15, 0.90);
            run.Ramp(10, 0.90, 1.00);

            Assert.AreEqual(0, run.Calls.Count);
        }

        [TestMethod]
        public void UnGolpeAisladoNoEsUnaZona()
        {
            var run = new Runner();
            run.Ramp(1.0, 1.00, 1.40);   // un bache seco
            run.Ramp(1.0, 1.40, 1.00);
            run.Hold(30, 1.00);

            Assert.AreEqual(0, run.Calls.Count,
                "Un solo golpe no da los tres cruces de banda: no se anuncia una zona");
        }

        [TestMethod]
        public void LaTurbulenciaDeVerdadOscilaYSeAnunciaUnaVez()
        {
            var run = new Runner();
            run.Hold(20, 1.00);
            run.Oscillate(20);           // ±0,25 g a 0,5 Hz, como un chop de verdad

            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset),
                "Una zona de turbulencia se anuncia una vez, no en cada golpe");
            Assert.AreEqual(0, run.Count(TurbulenceCallout.Cleared), "Todavía no ha terminado");
        }

        [TestMethod]
        public void EnElSueloONniBajoNoSeAnuncia()
        {
            var ground = new Runner();
            for (int i = 0; i < 240; i++) ground.One(i % 2 == 0 ? 1.30 : 0.70, airborne: false, agl: 0);
            Assert.AreEqual(0, ground.Calls.Count, "Con el avión en tierra no hay anuncio de cabina");

            var low = new Runner();
            for (int i = 0; i < 240; i++) low.One(i % 2 == 0 ? 1.30 : 0.70, airborne: true, agl: 500);
            Assert.AreEqual(0, low.Calls.Count, "A 500 ft AGL se vuela, no se anuncia turbulencia");
        }

        [TestMethod]
        public void SinDatoDeGNoSeAnunciaNada()
        {
            // Si el addon no escribe 0x11BA, el valor llega 0 y no oscila nunca: se calla.
            var run = new Runner();
            run.Hold(120, 0.0);
            Assert.AreEqual(0, run.Calls.Count);
        }

        [TestMethod]
        public void ElFinalSeAnunciaSoloTrasUnMinutoSinOscilar()
        {
            var run = new Runner();
            run.Hold(10, 1.00);
            run.Oscillate(10);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset));

            run.Hold(20, 1.00);
            Assert.AreEqual(0, run.Count(TurbulenceCallout.Cleared),
                "Menos de un minuto liso no cierra la zona");

            run.Hold(50, 1.00);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Cleared));
        }

        [TestMethod]
        public void ElEnfriamientoEvitaUnAnuncioPorCadaZona()
        {
            var run = new Runner();
            run.Hold(10, 1.00);
            run.Oscillate(10);                                // Onset
            run.Hold(TurbulenceAdvisor.ClearSec + 5, 1.00);   // Cleared

            run.Oscillate(10);                                // segunda zona, dentro del enfriamiento
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset),
                "10 minutos de enfriamiento: una zona larga no se anuncia seis veces");

            run.Hold(TurbulenceAdvisor.CooldownSec, 1.00);
            run.Oscillate(10);
            Assert.AreEqual(2, run.Count(TurbulenceCallout.Onset));
        }

        [TestMethod]
        public void ResetDejaElEstadoComoUnVueloNuevo()
        {
            var run = new Runner();
            run.Hold(10, 1.00);
            run.Oscillate(10);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset));

            run.Advisor.Reset();
            run.Calls.Clear();

            run.Hold(TurbulenceAdvisor.ClearSec + 10, 1.00);
            Assert.AreEqual(0, run.Count(TurbulenceCallout.Cleared),
                "Tras el reset no queda nada que cerrar");

            run.Oscillate(10);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset),
                "Y el enfriamiento del vuelo anterior tampoco se arrastra");
        }
    }
}
