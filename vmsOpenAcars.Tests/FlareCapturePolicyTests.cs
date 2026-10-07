using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Cuándo se arma y se desarma la traza fina del flare**, y el tope de muestras.
    ///
    /// Las cifras de referencia son **medidas sobre la base local**
    /// (`F:\FS\vmsOpenAcars\db\landing_log.sqlite`, 41 vuelos con traza):
    ///
    /// - El muestreo de `approach_track` es de 2 s nominales y **2,25 s medidos** (mediana del paso
    ///   reconstruido con la distancia y la velocidad de cada par de muestras) → a 148 kt de media en
    ///   final, **≈ 550 ft por muestra**. En los últimos 1 500 ft al umbral solo caben **6 muestras**
    ///   (mediana del corpus; 0 en los vuelos sin traza capturada y hasta 109 en el vuelo 5).
    /// - A 1 500 ft del umbral el avión está a **107 ft AGL de mediana** con **−850 fpm** de descenso:
    ///   quedan **~3,3 s** hasta el toque.
    ///
    /// De ahí sale la decisión que fijan estos tests: **armar a 1 500 ft del umbral** (con 1 000 ft
    /// AGL como respaldo, que es el mismo umbral del criterio de estabilizado), **muestrear a 10 Hz**
    /// y **cortar 2 s después del toque**, con un tope duro de 400 muestras.
    /// </summary>
    [TestClass]
    public class FlareCapturePolicyTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc);

        // ── El armado ─────────────────────────────────────────────────────────────

        /// <summary>
        /// El caso real del vuelo 41 (SKBG→SKCG, pista 01): a **1 653,8 ft** del umbral —el primer
        /// punto de su traza que ya está en el tramo final— todavía no se captura, porque el umbral de
        /// armado son 1 500 ft; en la muestra siguiente, a **1 103,0 ft** con 131 ft AGL, **sí**. Y la
        /// barra de muestreo de la captura es la del diseño: a 10 Hz, los ~6 s que quedan hasta el
        /// toque son ~60 muestras.
        /// </summary>
        [TestMethod]
        public void CasoReal_SeArmaAlEntrarEnLosUltimos1500Ft()
        {
            var p = new FlareCapturePolicy();

            // Los dos primeros puntos reales de la traza del vuelo 41 (base local, SKCG 01).
            var antes = p.Update(T0, 147.0, 1653.8, false, null, true);
            Assert.AreEqual(FlareCaptureAction.None, antes.Action,
                "a 1 654 ft del umbral todavía no toca: la captura no puede empezar medio descenso");
            Assert.AreEqual(0, antes.SampleCount);
            Assert.IsFalse(p.EverStarted);

            var dentro = p.Update(T0.AddMilliseconds(100), 131.0, 1103.0, false, null, true);
            Assert.AreEqual(FlareCaptureAction.Start, dentro.Action);
            Assert.AreEqual("armed:threshold", dentro.Reason);
            Assert.IsTrue(dentro.KeepSampling, "la muestra del armado es la primera de la traza");
            Assert.AreEqual(1, dentro.SampleCount);
            Assert.IsTrue(p.Capturing && p.EverStarted);

            // Y el número real de muestras del tramo: a 10 Hz, desde 1 500 ft hasta el toque a
            // 150 kt (≈ 250 ft/s) salen ~6 s, o sea ~60 muestras. Con el muestreo de 2 s de
            // `approach_track` habrían sido 3: es lo que justifica la captura dedicada.
            double distFt = 1103.0;
            int samples = 1;
            var t = T0.AddMilliseconds(100);
            while (distFt > 0.0 && samples < 400)
            {
                t = t.AddMilliseconds(100);
                distFt -= 150.0 * 1.68781 * 0.1;
                var d = p.Update(t, distFt * 0.09, distFt, false, null, true);
                if (d.KeepSampling) samples++;
            }
            Assert.IsTrue(samples >= 25 && samples <= 70,
                $"los últimos ~1 500 ft a 10 Hz tienen que dar unas 60 muestras, dieron {samples}");
        }

        /// <summary>
        /// El respaldo por altura existe para cuando la geometría del umbral no está disponible (sin
        /// pista resuelta, sin NavData): **300 ft AGL**. Y **solo decide si no hay distancia** — con
        /// 1 000 ft AGL el vuelo 41 habría empezado a capturar a 1 653,8 ft del umbral, antes que el
        /// propio umbral de armado de 1 500—. Los dos tests siguientes fijan las dos mitades de eso.
        /// </summary>
        [TestMethod]
        public void SinGeometriaDelUmbral_ArmaPorAltura()
        {
            var p = new FlareCapturePolicy();

            Assert.AreEqual(FlareCaptureAction.None, p.Update(T0, 500.0, null, false, null, true).Action,
                "a 500 ft AGL y sin distancia no hay nada que decidir todavía");

            var d = p.Update(T0.AddMilliseconds(100), 295.0, null, false, null, true);
            Assert.AreEqual(FlareCaptureAction.Start, d.Action);
            Assert.AreEqual("armed:agl", d.Reason);
        }

        /// <summary>
        /// **La distancia manda**: con dato de distancia, un AGL bajo no arma por su cuenta — si lo
        /// hiciera, la captura se abriría por la puerta de atrás *antes* de los 1 500 ft.
        /// </summary>
        [TestMethod]
        public void ConDatoDeDistancia_LaAlturaNoArmaPorSuCuenta()
        {
            var p = new FlareCapturePolicy();

            // 147 ft AGL con 1 653,8 ft al umbral: es el primer punto real de la traza del vuelo 41.
            var d = p.Update(T0, 147.0, 1653.8, false, null, true);
            Assert.AreEqual(FlareCaptureAction.None, d.Action,
                "por debajo de 300 ft AGL pero fuera de los 1 500 ft al umbral: manda la distancia");
            Assert.IsFalse(p.EverStarted);
        }

        /// <summary>
        /// **Sin datos no se arma y no pasa nada**: `null` en las dos magnitudes es «no lo sé», y la
        /// regla del repo es degradar sin datos, no bloquear ni suponer. Tampoco arma fuera de las
        /// fases de aterrizaje, que es lo que impide que se abra en el rodaje o en crucero.
        /// </summary>
        [TestMethod]
        public void SinDatosOFueraDeFase_NoSeArma()
        {
            var p = new FlareCapturePolicy();
            Assert.AreEqual(FlareCaptureAction.None, p.Update(T0, null, null, false, null, true).Action);
            Assert.IsFalse(p.EverStarted, "sin dato de distancia ni de altura no hay armado que valga");

            var q = new FlareCapturePolicy();
            Assert.AreEqual(FlareCaptureAction.None,
                q.Update(T0, 300.0, 900.0, false, null, inLandingPhase: false).Action,
                "en crucero o en rodaje la captura no se abre aunque los números den");
            Assert.IsFalse(q.EverStarted);
        }

        // ── El desarme y el margen ────────────────────────────────────────────────

        /// <summary>
        /// **El toque no corta en seco**: la captura sigue <see cref="FlareCapturePolicy.PostTouchdownSec"/>
        /// segundos (2) para ver la frenada y el morro bajando, y entonces se sella. Es el final de la
        /// maniobra lo que se viene a mirar.
        /// </summary>
        [TestMethod]
        public void ElToqueDejaUnMargenDeDosSegundosYDespuesSeSella()
        {
            var p = new FlareCapturePolicy();
            p.Update(T0, 100.0, 1200.0, false, null, true);

            var td = T0.AddSeconds(5);
            var durante = p.Update(td.AddSeconds(1.0), 0.0, -300.0, true, td, true);
            Assert.AreEqual(FlareCaptureAction.Keep, durante.Action,
                "un segundo después del toque todavía se captura: es la frenada");
            Assert.IsFalse(p.Sealed);

            var fin = p.Update(td.AddSeconds(2.05), 0.0, -450.0, true, td, true);
            Assert.AreEqual(FlareCaptureAction.Stop, fin.Action);
            Assert.IsTrue(fin.Reason.StartsWith("stopped:post-touchdown"), fin.Reason);
            Assert.IsFalse(fin.KeepSampling, "la muestra del cierre no entra en la traza");
            Assert.IsTrue(p.Sealed && !p.Capturing);

            // Y ya no vuelve a armarse en ese aterrizaje.
            var despues = p.Update(td.AddSeconds(3.0), 0.0, -600.0, true, td, true);
            Assert.AreEqual(FlareCaptureAction.None, despues.Action);
        }

        /// <summary>
        /// El toque también se reconoce **sin** la detección del `TouchdownDetected` —toma suave, VS
        /// por encima del umbral del detector—: tierra y ya pasado el umbral es el mismo dato que
        /// mira la captura, no una suposición nueva.
        /// </summary>
        [TestMethod]
        public void EnTierraYPasadoElUmbral_TambienCierra()
        {
            var p = new FlareCapturePolicy();
            p.Update(T0, 80.0, 900.0, false, null, true);

            var d = p.Update(T0.AddSeconds(6), 0.0, -120.0, true, null, true);
            Assert.AreEqual(FlareCaptureAction.Stop, d.Action);
            Assert.AreEqual("stopped:on-ground", d.Reason);
        }

        // ── El tope de muestras ───────────────────────────────────────────────────

        /// <summary>
        /// **El cap existe para que el hilo de telemetría no pueda crecer sin límite.** Medido: si la
        /// captura se armara por AGL en un avión que descendiera a 500 fpm, serían ~2 minutos de
        /// muestras. Con 400 muestras a 10 Hz, 40 s, y el cierre por tope **no rompe la traza**: el
        /// recorte del buffer suelta lo viejo y conserva el final.
        /// </summary>
        [TestMethod]
        public void ElCapDeMuestras_CortaLaCapturaEnElTopeExacto()
        {
            var p = new FlareCapturePolicy();
            p.Update(T0, 900.0, 1400.0, false, null, true);

            int n = 1;
            var t = T0;
            while (n < FlareCapturePolicy.MaxSamples)
            {
                t = t.AddMilliseconds(100);
                var d = p.Update(t, 900.0, 1400.0, false, null, true);
                Assert.AreEqual(FlareCaptureAction.Keep, d.Action, $"cortó antes de tiempo en la muestra {n}");
                n = d.SampleCount;
            }

            var cap = p.Update(t.AddMilliseconds(100), 900.0, 1400.0, false, null, true);
            Assert.AreEqual(FlareCaptureAction.Stop, cap.Action);
            Assert.AreEqual("stopped:cap", cap.Reason);
            Assert.AreEqual(FlareCapturePolicy.MaxSamples, cap.SampleCount,
                "el tope es exacto: 400 muestras guardadas, no 401");
            Assert.IsTrue(p.Sealed);
        }

        /// <summary>
        /// El seguro del go-around: si nunca llega el toque, la captura no se queda abierta todo el
        /// descenso. Se cierra y **no se rearma**, porque el aterrizaje ya se intentó una vez.
        /// </summary>
        [TestMethod]
        public void SinToque_ElSeguroDeTiempoCierraLaCapturaYNoLaReabre()
        {
            var p = new FlareCapturePolicy();
            p.Update(T0, 900.0, 1400.0, false, null, true);

            var dentro = p.Update(T0.AddSeconds(44.0), 900.0, 1400.0, false, null, true);
            Assert.AreEqual(FlareCaptureAction.Keep, dentro.Action);

            var fuera = p.Update(T0.AddSeconds(FlareCapturePolicy.MaxCaptureSec + 1.0),
                                 900.0, 1400.0, false, null, true);
            Assert.AreEqual(FlareCaptureAction.Stop, fuera.Action);
            Assert.AreEqual("stopped:timeout", fuera.Reason);

            var reintento = p.Update(T0.AddSeconds(60.0), 900.0, 500.0, false, null, true);
            Assert.AreEqual(FlareCaptureAction.None, reintento.Action, "sellado es sellado");
        }

        /// <summary>Un vuelo nuevo empieza de cero: sin esto la captura anterior envenenaría la nueva.</summary>
        [TestMethod]
        public void Reset_VuelveAlEstadoInicial()
        {
            var p = new FlareCapturePolicy();
            p.Update(T0, 100.0, 1200.0, false, null, true);
            Assert.IsTrue(p.EverStarted);
            p.Reset();

            Assert.IsFalse(p.EverStarted);
            Assert.IsFalse(p.Capturing);
            Assert.IsFalse(p.Sealed);
            Assert.AreEqual(0, p.SampleCount);
            Assert.AreEqual(FlareCaptureAction.None, p.LastDecision.Action);

            // Y después del reset vuelve a armar, igual que un vuelo nuevo.
            var d = p.Update(T0, 100.0, 1200.0, false, null, true);
            Assert.AreEqual(FlareCaptureAction.Start, d.Action);
        }
    }

    /// <summary>
    /// **El almacén de las muestras del flare**: tope y hilo, las dos cosas que ya se rompieron una
    /// vez en este proyecto con `approach_track` (puntos perdidos e `IndexOutOfRange` dentro de
    /// `List.Add` por tocarlo desde dos hilos).
    /// </summary>
    [TestClass]
    public class FlareSampleBufferTests
    {
        [TestMethod]
        public void AlLlegarAlTope_SueltaLoViejoYConservaElFinal()
        {
            var buffer = new FlareSampleBuffer(3);
            for (int i = 0; i < 5; i++)
                buffer.Append(new FlareTrackPoint { DistFt = 1000.0 - i * 100.0, OnGround = false });

            Assert.AreEqual(3, buffer.Count, "el almacén nunca pasa de su tope");

            var samples = buffer.Snapshot();
            Assert.AreEqual(3, samples.Count);
            // Las últimas tres (índices 2, 3, 4 de la serie) son las que quedan: el final del flare
            // es lo que se mira, así que el recorte suelta lo más antiguo.
            Assert.AreEqual(800.0, samples[0].DistFt, 1e-9);
            Assert.AreEqual(600.0, samples[2].DistFt, 1e-9);
        }

        [TestMethod]
        public void UnTopeInutilizable_CaeAlTopeDelDiseno()
        {
            var buffer = new FlareSampleBuffer(0);
            for (int i = 0; i < FlareCapturePolicy.MaxSamples + 10; i++)
                buffer.Append(new FlareTrackPoint());

            Assert.AreEqual(FlareCapturePolicy.MaxSamples, buffer.Count,
                "un tope de 0 no puede significar «sin tope»: cae al del diseño (400)");
        }

        [TestMethod]
        public void AnadirNulo_NoRompeNiCuenta()
        {
            var buffer = new FlareSampleBuffer(10);
            Assert.AreEqual(0, buffer.Append(null));
            Assert.AreEqual(0, buffer.Count);
        }

        [TestMethod]
        public void Clear_VaciaElAlmacen()
        {
            var buffer = new FlareSampleBuffer(10);
            buffer.Append(new FlareTrackPoint());
            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
        }
    }
}
