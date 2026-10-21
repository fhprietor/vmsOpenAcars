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
    /// **No hay serie real grabada de turbulencia en el corpus** (el `landing_log.sqlite` de esta
    /// máquina está vacío), así que las series de estos tests son construidas. Lo que sí es real es
    /// el ancla de los umbrales: el proyecto ya tiene medido que **~100 fpm de ruido de VS es
    /// turbulencia leve** —es la cifra con la que la máquina de fases evita falsas transiciones—,
    /// y por eso un golpe cuenta a partir de 150 fpm y el ruido de 100 fpm **no** dispara. Ese caso
    /// está aquí abajo (`ElRuidoDeCienPiesPorMinutoNoEsTurbulencia`) y es el que fija el contrato.
    ///
    /// Los tiempos se miden con reloj inyectado, así que los tests no esperan.
    /// </summary>
    [TestClass]
    public class TurbulenceAdvisorTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        private sealed class Runner
        {
            internal readonly TurbulenceAdvisor Advisor = new TurbulenceAdvisor();
            internal DateTime Now = T0;
            internal readonly List<TurbulenceCallout> Calls = new List<TurbulenceCallout>();

            internal void Step(double seconds, double g, double vs, bool airborne = true, double agl = 30000)
            {
                Now = Now.AddSeconds(seconds);
                TurbulenceCallout callout = Advisor.Feed(Now, g, vs, airborne, agl);
                if (callout != TurbulenceCallout.None) Calls.Add(callout);
            }

            /// <summary>Vuelo liso: g = 1 y VS estable.</summary>
            internal void Calm(double seconds)
            {
                for (double t = 0; t < seconds; t += 0.5) Step(0.5, 1.0, -1800);
            }

            /// <summary>Turbulencia: g oscila ±0,3 y el VS da golpes de 400 fpm.</summary>
            internal void Turbulent(double seconds)
            {
                bool high = false;
                for (double t = 0; t < seconds; t += 0.5)
                {
                    high = !high;
                    Step(0.5, high ? 1.30 : 0.98, high ? -1500 : -1900);
                }
            }

            internal int Count(TurbulenceCallout kind) => Calls.Count(c => c == kind);
        }

        [TestMethod]
        public void UnVueloLisoNoAnunciaNada()
        {
            var run = new Runner();
            run.Calm(300);
            Assert.AreEqual(0, run.Calls.Count, "Dos minutos y medio de vuelo liso no son noticia");
        }

        [TestMethod]
        public void LaTurbulenciaSostenidaSeAnunciaUnaSolaVez()
        {
            var run = new Runner();
            run.Calm(30);
            run.Turbulent(20);

            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset),
                "Una zona de turbulencia se anuncia una vez, no en cada golpe");
            Assert.AreEqual(0, run.Count(TurbulenceCallout.Cleared), "Todavía no ha terminado");
        }

        [TestMethod]
        public void UnBacheCortoNoAnuncia()
        {
            var run = new Runner();
            run.Calm(30);
            // 3 s de golpes: dan 5 reversiones y 3 muestras con g fuera de 1, por debajo de las 6 y
            // las 4 que pide la ventana de 6 s. Antes de la corrección, esos 4 golpes se quedaban
            // 20 s en la ventana y el bache corto acababa anunciándose.
            run.Turbulent(3.0);
            run.Calm(10);

            Assert.AreEqual(0, run.Calls.Count,
                "Un bache corto no es una zona: no se habla");
        }

        [TestMethod]
        public void EnElSueloONniBajoNoSeAnuncia()
        {
            var run = new Runner();
            for (int i = 0; i < 60; i++) run.Step(0.5, 1.4, -1200, airborne: false, agl: 0);
            Assert.AreEqual(0, run.Calls.Count, "Con el avión en tierra no hay anuncio de cabina");

            var low = new Runner();
            for (int i = 0; i < 60; i++) low.Step(0.5, 1.4, -1200, airborne: true, agl: 500);
            Assert.AreEqual(0, low.Calls.Count, "A 500 ft AGL se vuela, no se anuncia turbulencia");
        }

        [TestMethod]
        public void ElFinalSeAnunciaSoloTrasUnMinutoLiso()
        {
            var run = new Runner();
            run.Calm(30);
            run.Turbulent(20);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset));

            // Una tregua corta no es el final de nada: sigue sin anunciarse la salida.
            run.Calm(TurbulenceAdvisor.ClearSec - 10);
            Assert.AreEqual(0, run.Count(TurbulenceCallout.Cleared),
                "Menos de un minuto liso no cierra la zona");

            run.Calm(20);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Cleared));
        }

        [TestMethod]
        public void ElEnfriamientoEvitaUnAnuncioPorCadaZona()
        {
            var run = new Runner();
            run.Calm(30);
            run.Turbulent(20);                      // Onset
            run.Calm(TurbulenceAdvisor.ClearSec + 10);   // Cleared

            run.Turbulent(20);                      // segunda zona, dentro del enfriamiento
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset),
                "10 minutos de enfriamiento: una zona larga no se anuncia seis veces");

            // Pasado el enfriamiento desde el primer aviso, la siguiente zona sí se anuncia.
            run.Calm(TurbulenceAdvisor.CooldownSec);
            run.Turbulent(20);
            Assert.AreEqual(2, run.Count(TurbulenceCallout.Onset));
        }

        [TestMethod]
        public void ElRuidoDeCienPiesPorMinutoNoEsTurbulencia()
        {
            // El ancla real: el proyecto ya documenta ~100 fpm de ruido como turbulencia leve que
            // NO debe mover nada. Aquí se comprueba que tampoco dispara un anuncio de cabina.
            var run = new Runner();
            bool up = false;
            for (int i = 0; i < 240; i++)
            {
                up = !up;
                run.Step(0.5, 1.0, up ? -1700 : -1800);
            }

            Assert.AreEqual(0, run.Calls.Count,
                "100 fpm de vaivén es ruido de fondo, no una zona de turbulencia");
        }

        [TestMethod]
        public void SoloLaTasaVerticalBastaParaDetectarla()
        {
            // Factor de carga perfecto y VS dando golpes: es turbulencia igual (el pasajero la nota).
            var run = new Runner();
            run.Calm(20);
            bool up = false;
            for (int i = 0; i < 40; i++)
            {
                up = !up;
                run.Step(0.5, 1.0, up ? -1300 : -1900);   // delta 600 fpm, alternando
            }

            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset));
        }

        [TestMethod]
        public void ResetDejaElEstadoComoUnVueloNuevo()
        {
            var run = new Runner();
            run.Calm(30);
            run.Turbulent(20);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset));

            run.Advisor.Reset();
            run.Calls.Clear();

            run.Calm(TurbulenceAdvisor.ClearSec + 10);
            Assert.AreEqual(0, run.Count(TurbulenceCallout.Cleared),
                "Tras el reset no queda nada que cerrar");

            run.Turbulent(20);
            Assert.AreEqual(1, run.Count(TurbulenceCallout.Onset),
                "Y el enfriamiento del vuelo anterior tampoco se arrastra");
        }
    }
}
