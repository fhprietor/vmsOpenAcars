using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El eje Y del closeup del aterrizaje.**
    ///
    /// Los casos son **tramos reales** de la base local (`F:\FS\vmsOpenAcars\db\landing_log.sqlite`,
    /// 41 vuelos con traza), no geometría inventada: la X va en pies al umbral (positivo antes) y la
    /// altitud en AGL, tal cual sale de `approach_track`. Los cuatro vuelos que se usan están
    /// elegidos por lo que cada uno demuestra:
    ///
    /// | Vuelo | Ruta / pista | Tramo en ±5 000 ft | Lo que fija |
    /// |---|---|---|---|
    /// | 41 | SKBG → SKCG 01 | AGL **50 … 309** (13 muestras) | el caso normal: la Y se ajusta al tramo, no a los 2 563 ft del perfil entero |
    /// | 11 | SKCL → KJFK 22L | AGL **33 … 560** (19 muestras) | pendiente fuerte: el eje llega al dato más alto |
    /// | 35 | SKBO → SKPE 08 | AGL **0 … −19** (5 muestras) | casi plano: el **recorrido mínimo** evita amplificar el ruido |
    /// | 19 | SKCG → SKVP 05 | AGL **−61 … −375** (16 muestras) | AGL negativo (elevación de campo mal resuelta): el cero del terreno sigue dentro |
    ///
    /// El defecto que esto corrige está medido: el perfil vertical se dibujaba con la escala del
    /// **perfil completo**, y el vuelo 41 arranca a **2 563 ft AGL** (`seq_no` 0), así que el tramo
    /// final —147 ft de altitud— cabía en el 6 % de la altura del gráfico y se veía aplastado
    /// contra la línea del suelo.
    /// </summary>
    [TestClass]
    public class CloseupVerticalAxisTests
    {
        // ── Los tramos reales, tal cual salen de la base ─────────────────────────
        // (dist_ft al umbral, AGL ft). `dist_ft` es positivo antes del umbral y negativo después,
        // igual que en `flare_track` y que en el eje X del closeup.

        /// <summary>Vuelo 41 (SKBG → SKCG 01): 13 muestras dentro del encuadre ancho. La última de
        /// fuera, a 48 969 ft, es la que lleva el AGL a 2 563 ft y la que aplastaba el closeup.</summary>
        private static List<(double XFt, double AltFt)> Skcg41Wide() => new List<(double, double)>
        {
            (4874.4, 309.0), (4332.1, 280.0), (3801.8, 252.0), (3250.7, 224.0), (2724.7, 197.0),
            (2195.3, 169.0), (1653.8, 147.0), (1103.0, 131.0), ( 558.3, 119.0), (  20.7, 102.0),
            (-499.3,  80.0), (-1020.6,  60.0), (-1541.7,  50.0),
        };

        /// <summary>El AGL más alto de **toda** la traza del vuelo 41 (`seq_no` 0, a 48 969 ft).</summary>
        private const double Skcg41FullProfileMaxFt = 2563.0;

        /// <summary>Vuelo 11 (SKCL → KJFK 22L): las muestras que entran en el encuadre ancho
        /// (−4 500 … +5 000 ft). La de −4 660,3 ft cae fuera del borde derecho y **no** cuenta: el eje
        /// describe lo que se ve, no la traza entera.</summary>
        private static List<(double XFt, double AltFt)> Skcl11Wide() => new List<(double, double)>
        {
            (4542.0, 560.0), (4060.2, 527.0), (3558.9, 493.0), (3073.7, 460.0), (2563.2, 422.0),
            (2092.7, 383.0), (1584.7, 340.0), (1108.6, 305.0), ( 615.8, 273.0), ( 122.2, 243.0),
            (-353.8, 217.0), (-838.0, 191.0), (-1328.4, 161.0), (-1789.4, 130.0), (-2281.8, 100.0),
            (-2755.0, 83.0), (-3241.9, 69.0), (-3706.4, 53.0), (-4175.3, 33.0),
        };

        /// <summary>Vuelo 35 (SKBO → SKPE 08): el tramo casi plano y con AGL ya negativo en tierra.
        /// La variación real es de **19 ft** en 2 000 ft de trayectoria.</summary>
        private static List<(double XFt, double AltFt)> Skbo35Flat() => new List<(double, double)>
        {
            (1371.0, 0.0), (862.0, -18.0), (362.0, -16.0), (-157.0, -13.0), (-673.0, -19.0),
        };

        /// <summary>Vuelo 19 (SKCG → SKVP 05): todo el tramo con AGL negativo, de −61 a −375 ft.</summary>
        private static List<(double XFt, double AltFt)> Skcg19Negative() => new List<(double, double)>
        {
            (4672.9, -61.0), (4277.9, -74.0), (3878.0, -98.0), (3470.9, -133.0), (3053.5, -163.0),
            (2642.9, -188.0), (2234.7, -213.0), (1822.7, -232.0), (1419.1, -251.0), (998.4, -276.0),
            ( 591.0, -303.0), ( 197.8, -326.0), (-195.2, -344.0), (-585.7, -356.0), (-964.5, -365.0),
            (-1350.7, -375.0),
        };

        // ── El caso normal: la Y se ajusta al tramo, no al perfil entero ─────────

        /// <summary>
        /// **El caso del defecto.** Con la escala del perfil completo (0 … 2 563 ft) el tramo del
        /// closeup quedaba plano; ajustada al encuadre, el eje va de **−100 a 400 ft** con paso de
        /// **100**: la pista en el suelo, el umbral y una curva que ocupa media altura.
        /// </summary>
        [TestMethod]
        public void Skcg41_ElTramoReal_LaEscalaSeAjustaAlEncuadreYNoAlPerfilEntero()
        {
            var fit = CloseupVerticalAxis.Fit(Skcg41Wide(), 5000.0, 4500.0);

            Assert.IsTrue(fit.HasData);
            Assert.AreEqual(13, fit.SampleCount);
            Assert.AreEqual(50.0, fit.DataMinFt, 1e-9);
            Assert.AreEqual(309.0, fit.DataMaxFt, 1e-9);

            Assert.AreEqual(-100.0, fit.MinimumFt, 1e-9);
            Assert.AreEqual(400.0, fit.MaximumFt, 1e-9);
            Assert.AreEqual(100.0, fit.StepFt, 1e-9);

            // Lo que se viene a arreglar, en una aserción: el techo del eje es una fracción del AGL
            // más alto de la traza entera (2 563 ft). Si alguien vuelve a atar la Y a la traza
            // completa, esto cae.
            Assert.IsTrue(fit.MaximumFt < Skcg41FullProfileMaxFt / 5.0,
                $"la Y no puede volver a la escala del perfil entero ({fit.MaximumFt} contra {Skcg41FullProfileMaxFt})");

            // Y el dato entero cabe: ni una muestra fuera del eje.
            Assert.IsTrue(fit.MinimumFt <= fit.DataMinFt && fit.MaximumFt >= fit.DataMaxFt);
        }

        /// <summary>
        /// **El cero del terreno va dentro aunque el tramo no lo toque.** En la escala fina (±1 000
        /// ft) del vuelo 41 solo entran **3 muestras** —AGL 80 … 119—, y el eje baja igual hasta
        /// −50 para que la banda de pista y la altura real se entiendan. Esas tres muestras son
        /// también la razón de que la escala de 1 000 ft **no se ofrezca** sin la traza del flare:
        /// con 10 Hz son ~40.
        /// </summary>
        [TestMethod]
        public void EscalaFina_DelVuelo41_ConTresMuestrasDeDosSegundos_ElSueloQuedaDentro()
        {
            var fit = CloseupVerticalAxis.Fit(Skcg41Wide(),
                TouchdownCloseupGeometry.FineBeforeFt, TouchdownCloseupGeometry.FineAfterFt);

            Assert.IsTrue(fit.HasData);
            Assert.AreEqual(3, fit.SampleCount, "la traza de 2 s solo deja tres muestras en ±1 000 ft");
            Assert.AreEqual(80.0, fit.DataMinFt, 1e-9);
            Assert.AreEqual(119.0, fit.DataMaxFt, 1e-9);

            Assert.AreEqual(-50.0, fit.MinimumFt, 1e-9);
            Assert.AreEqual(150.0, fit.MaximumFt, 1e-9);
            Assert.AreEqual(50.0, fit.StepFt, 1e-9);

            Assert.IsTrue(fit.MinimumFt < 0.0, "el cero del terreno tiene que estar dentro");
            Assert.IsTrue(fit.MinimumFt < 0.0 && fit.MaximumFt > 0.0,
                "el suelo queda bajo AGL 0 y el techo por encima: la altura real se entiende");
        }

        // ── Pendiente fuerte: el eje llega al dato más alto ──────────────────────

        /// <summary>
        /// El vuelo 11 baja de **560 a 33 ft** dentro del encuadre (527 ft de recorrido): el paso sube
        /// a **250** y el eje va de **−250 a 750**, con el dato más alto dentro. Es el otro extremo:
        /// aquí el recorrido mínimo no pinta nada porque el tramo es largo de verdad.
        /// </summary>
        [TestMethod]
        public void Vuelo11_PendienteFuerte_ElEjeLlegaAlDatoMasAltoYElPasoSeEngorda()
        {
            var fit = CloseupVerticalAxis.Fit(Skcl11Wide(), 5000.0, 4500.0);

            Assert.IsTrue(fit.HasData);
            Assert.AreEqual(19, fit.SampleCount);
            Assert.AreEqual(33.0, fit.DataMinFt, 1e-9);
            Assert.AreEqual(560.0, fit.DataMaxFt, 1e-9);

            Assert.AreEqual(250.0, fit.StepFt, 1e-9, "535 ft de recorrido no caben con paso de 25");
            Assert.AreEqual(-250.0, fit.MinimumFt, 1e-9);
            Assert.AreEqual(750.0, fit.MaximumFt, 1e-9);
            Assert.IsTrue(fit.MaximumFt >= fit.DataMaxFt, "el dato más alto tiene que caber");
        }

        // ── Casi plano: aquí está el riesgo de amplificar el ruido ───────────────

        /// <summary>
        /// **El caso que justifica el recorrido mínimo.** El vuelo 35 tiene **19 ft** de variación
        /// real en el tramo (0 a −19 ft) y en buena parte es ruido de la elevación del campo. Con un
        /// eje pegado al dato, esos 19 ft se estirarían hasta llenar el gráfico entero y el ruido se
        /// leería como una maniobra. Con `MinSpanFt = 100` el eje mide **100 ft** (de −50 a 50) y la
        /// variación ocupa menos de un cuarto de la altura: se ve lo que hay, sin exagerarlo.
        /// </summary>
        [TestMethod]
        public void Vuelo35_TramoCasiPlano_ElRecorridoMinimoEvitaAmplificarElRuido()
        {
            var fit = CloseupVerticalAxis.Fit(Skbo35Flat(), 5000.0, 4500.0);

            Assert.IsTrue(fit.HasData);
            Assert.AreEqual(5, fit.SampleCount);
            Assert.AreEqual(-19.0, fit.DataMinFt, 1e-9);
            Assert.AreEqual(0.0, fit.DataMaxFt, 1e-9);

            Assert.AreEqual(-50.0, fit.MinimumFt, 1e-9);
            Assert.AreEqual(50.0, fit.MaximumFt, 1e-9);
            Assert.AreEqual(25.0, fit.StepFt, 1e-9);
            Assert.AreEqual(CloseupVerticalAxis.MinSpanFt, fit.SpanFt, 1e-9,
                "un tramo de 19 ft no puede medir 19 ft de alto");

            // La comparación explícita: el eje no puede amplificar el dato más de lo que impone el
            // recorrido mínimo. Aquí lo amplifica 100/19 ≈ 5,3, y con 200 ft habría sido el doble.
            double dataSpan = fit.DataMaxFt - fit.DataMinFt;
            Assert.IsTrue(fit.SpanFt / dataSpan <= CloseupVerticalAxis.MinSpanFt / dataSpan + 1e-9,
                "el eje solo puede crecer hasta el recorrido mínimo");
        }

        // ── AGL negativo: el cero sigue dentro ───────────────────────────────────

        /// <summary>
        /// El vuelo 19 tiene un problema de datos real: la elevación del campo quedó ~300 ft alta y
        /// **todo** el tramo va de −61 a −375 ft. El eje no puede recortar el dato, así que el suelo
        /// baja a **−400** (múltiplo del paso), y el techo se queda en **100**: el cero del terreno
        /// entra y la banda de pista no queda pegada al borde superior del área.
        /// </summary>
        [TestMethod]
        public void Vuelo19_ConAglNegativo_ElSueloBajaAlDatoYElCeroSigueDentro()
        {
            var fit = CloseupVerticalAxis.Fit(Skcg19Negative(), 5000.0, 4500.0);

            Assert.IsTrue(fit.HasData);
            Assert.AreEqual(16, fit.SampleCount);
            Assert.AreEqual(-375.0, fit.DataMinFt, 1e-9);
            Assert.AreEqual(-61.0, fit.DataMaxFt, 1e-9);

            Assert.AreEqual(100.0, fit.StepFt, 1e-9);
            Assert.AreEqual(-400.0, fit.MinimumFt, 1e-9, "el suelo baja al múltiplo del paso que contiene el dato");
            Assert.AreEqual(100.0, fit.MaximumFt, 1e-9, "el techo se queda por encima del cero del terreno");
            Assert.IsTrue(fit.MinimumFt <= fit.DataMinFt);
            Assert.IsTrue(fit.MaximumFt > 0.0, "el cero del terreno tiene que estar dentro");
        }

        // ── Sin datos: la Y se queda como estaba ────────────────────────────────

        /// <summary>
        /// **Degradar sin datos**: sin muestras, con todas fuera del encuadre o con un encuadre que
        /// no encuadra nada, el helper **no decide** (`HasData` en falso) y el formulario deja la Y
        /// como estaba. Un dato no finito tampoco es una altitud.
        /// </summary>
        [TestMethod]
        public void SinDatosEnElEncuadre_NoSeAjustaNadaYNoLanza()
        {
            var basura = new List<(double XFt, double AltFt)>
            {
                (double.NaN, 100.0),
                (500.0, double.NaN),
                (600.0, double.PositiveInfinity),
                (double.NegativeInfinity, 50.0),
            };

            foreach (var (name, fit) in new[]
                     {
                         ("sin lista",     CloseupVerticalAxis.Fit(null, 5000.0, 4500.0)),
                         ("lista vacía",   CloseupVerticalAxis.Fit(new List<(double, double)>(), 5000.0, 4500.0)),
                         ("todo fuera",    CloseupVerticalAxis.Fit(new List<(double, double)> { (6000.0, 200.0), (-6000.0, 100.0) }, 5000.0, 4500.0)),
                         ("encuadre 0/0",  CloseupVerticalAxis.Fit(Skcg41Wide(), 0.0, 0.0)),
                         ("encuadre NaN",  CloseupVerticalAxis.Fit(Skcg41Wide(), double.NaN, double.NaN)),
                         ("dato no finito", CloseupVerticalAxis.Fit(basura, 5000.0, 4500.0)),
                     })
            {
                Assert.IsFalse(fit.HasData, $"«{name}» no tiene con qué ajustar la Y");
                Assert.AreEqual(0, fit.SampleCount);
                Assert.AreEqual(0.0, fit.SpanFt, 1e-9, $"«{name}» no puede inventar un recorrido");
            }
        }

        // ── Los pasos ───────────────────────────────────────────────────────────

        /// <summary>
        /// Los pasos son los «bonitos» de la casa (25 / 50 / 100 / 250 / 500 ft) y se elige el más
        /// fino que no pase de <see cref="CloseupVerticalAxis.MaxIntervals"/> intervalos: un eje con
        /// paso de 37 ft es tan ilegible como el de miles, y uno de 25 ft sobre 500 ft de recorrido
        /// llena el gráfico de rejilla.
        /// </summary>
        [TestMethod]
        public void LosPasosSonLosBonitosYNoSePasaDeCincoIntervalos()
        {
            CollectionAssert.AreEqual(new[] { 25.0, 50.0, 100.0, 250.0, 500.0 },
                                      CloseupVerticalAxis.NiceSteps.ToArray());

            Assert.AreEqual(25.0,  CloseupVerticalAxis.StepFor(100.0), 1e-9);
            Assert.AreEqual(50.0,  CloseupVerticalAxis.StepFor(147.0), 1e-9);
            Assert.AreEqual(100.0, CloseupVerticalAxis.StepFor(259.0), 1e-9);
            Assert.AreEqual(250.0, CloseupVerticalAxis.StepFor(535.0), 1e-9);
            Assert.AreEqual(500.0, CloseupVerticalAxis.StepFor(2600.0), 1e-9);

            foreach (double span in new[] { 1.0, 19.0, 100.0, 147.0, 259.0, 314.0, 535.0, 2500.0 })
            {
                double step = CloseupVerticalAxis.StepFor(span);
                CollectionAssert.Contains(CloseupVerticalAxis.NiceSteps.ToArray(), step);
                Assert.IsTrue(span <= step * CloseupVerticalAxis.MaxIntervals + 1e-9,
                    $"un recorrido de {span} ft no puede quedar con paso de {step}");
            }

            // Un recorrido absurdo no revienta: se queda en el paso más grueso de la lista.
            Assert.AreEqual(500.0, CloseupVerticalAxis.StepFor(100000.0), 1e-9);
            Assert.AreEqual(25.0,  CloseupVerticalAxis.StepFor(0.0), 1e-9);
            Assert.AreEqual(25.0,  CloseupVerticalAxis.StepFor(double.NaN), 1e-9);
        }

        // ── Y el caso extremo: el recorrido de verdad es enorme ──────────────────

        /// <summary>
        /// Con el encuadre ancho y una traza que aún baja de 560 ft, el eje se queda en el paso más
        /// grueso sin abrirse a los miles del perfil: es la garantía de que el ajuste **nunca** se
        /// sale del encuadre aunque el redondeo hacia fuera estire el recorrido.
        /// </summary>
        [TestMethod]
        public void ElRedondeoHaciaFueraNoSeSaleDeLosLimitesDeLaListaDePasos()
        {
            var fit = CloseupVerticalAxis.Fit(Skcl11Wide(), 5000.0, 4500.0);

            Assert.IsTrue(CloseupVerticalAxis.NiceSteps.Contains(fit.StepFt));
            Assert.AreEqual(0.0, Math.Abs(fit.MinimumFt % fit.StepFt), 1e-6, "el suelo cae en la rejilla del paso");
            Assert.AreEqual(0.0, Math.Abs(fit.MaximumFt % fit.StepFt), 1e-6, "y el techo también");
            Assert.IsTrue(fit.SpanFt / fit.StepFt <= CloseupVerticalAxis.MaxIntervals + 1e-9);
        }
    }
}
