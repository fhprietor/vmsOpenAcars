using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Componentes del viento del aterrizaje** (`Helpers/WindComponents`).
    ///
    /// Los datos de pista son **reales** y salen del propio caché de NavData que usa el cliente
    /// (`NavData_cache.sqlite`, `airport_entries` de SKBO y SKBG con su `mag_var`): las coordenadas
    /// del umbral y del extremo son las que se usan aquí para derivar el rumbo **verdadero** del eje.
    /// Los METAR son reales también, copiados del logbook local del piloto
    /// (`landing_log.sqlite`, vuelos 36 y 40), no inventados.
    ///
    /// Lo que se fija, y por qué:
    ///  - **La referencia.** El viento se da **de donde viene** y el eje es **verdadero**. SKBO 14R
    ///    tiene `heading = 135.8` **magnético** con `mag_var = -8.59`, o sea **127,2 verdadero**;
    ///    descomponer contra el magnético metería 8,6° de error (en KBOS serían 13,7°).
    ///  - **El signo de la componente cruzada**: positiva **desde la derecha**, negativa desde la
    ///    izquierda. Se comprueba en los dos lados con 90° exactos.
    ///  - **En cara / en cola**: la cola es la componente en cara negativa, y hay un caso real
    ///    (SKBG 17 con el METAR de las 00:00Z del 05/09).
    ///  - **Degradar sin datos**: sin viento o sin rumbo de pista no se inventa un 0 que significaría
    ///    «viento del norte» o «cruzada nula».
    /// </summary>
    [TestClass]
    public class WindComponentsTests
    {
        // ── Datos reales ──────────────────────────────────────────────────────────

        /// <summary>SKBO 14R, del caché de NavData: umbral y extremo WGS-84 reales.</summary>
        private const double Skbo14RThresholdLat = 4.7104945182800293;
        private const double Skbo14RThresholdLon = -74.169158935546875;
        private const double Skbo14REndLat       = 4.6898183491432217;
        private const double Skbo14REndLon       = -74.14185798276722;

        /// <summary>SKBG 17 (Palonegro), del mismo caché.</summary>
        private const double Skbg17ThresholdLat = 7.136549472808838;
        private const double Skbg17ThresholdLon = -73.18855285644531;
        private const double Skbg17EndLat       = 7.116490283525257;
        private const double Skbg17EndLon       = -73.18100729738421;

        /// <summary>Rumbo verdadero de SKBO 14R derivado de las coordenadas reales.</summary>
        private static readonly double Skbo14RTrue =
            GeoMath.BearingDeg(Skbo14RThresholdLat, Skbo14RThresholdLon, Skbo14REndLat, Skbo14REndLon);

        /// <summary>Rumbo verdadero de SKBG 17.</summary>
        private static readonly double Skbg17True =
            GeoMath.BearingDeg(Skbg17ThresholdLat, Skbg17ThresholdLon, Skbg17EndLat, Skbg17EndLon);

        // METAR reales del logbook local del piloto.
        private const string MetarSkbo = // vuelo 36, SKPE→SKBO 14R, 25/09/2026
            "METAR SKBO 250100Z 01003KT 350V110 9999 BKN080 15/11 Q1028 NOSIG";
        private const string MetarSkbg = // vuelo 40, SKRG→SKBG, 05/09/2026 (00:00Z)
            "METAR SKBG 050000Z 32003KT 280V010 9999 SCT020 23/20 Q1015";

        // ── La referencia: magnético vs verdadero ─────────────────────────────────

        [TestMethod]
        public void ElEjeDeSkbo14R_EsVerdaderoYNoElMagneticoQuePublicaNavData()
        {
            // 135,8 magnético con mag_var -8,59 (el dato que publica /runways/) → 127,21 verdadero.
            // El rumbo verdadero derivado de las coordenadas tiene que caer ahí, no en 135,8.
            Assert.AreEqual(127.23, Skbo14RTrue, 0.05,
                $"SKBO 14R: el eje verdadero es 127,2 y salió {Skbo14RTrue:F2}");
            Assert.AreEqual(127.21, 135.8 + (-8.59), 0.01);
            Assert.IsTrue(Math.Abs(Skbo14RTrue - 135.8) > 8.0,
                "el magnético está a más de 8° del verdadero: usar el equivocado se ve en la cruzada");
        }

        [TestMethod]
        public void ElEjeDeSkbg17_EsElVerdaderoDeSusCoordenadasReales()
        {
            // 168,8 magnético con mag_var -9,24 → 159,56 verdadero.
            Assert.AreEqual(159.53, Skbg17True, 0.05,
                $"SKBG 17: el eje verdadero es 159,5 y salió {Skbg17True:F2}");
        }

        // ── Casos reales ──────────────────────────────────────────────────────────

        [TestMethod]
        public void CasoRealSkbo14R_ElMetarDeLas0100ZDaVientoEnColaYCruzadoPorLaIzquierda()
        {
            // METAR real: 01003KT. Eje 14R verdadero 127,2 → el viento entra por detrás y por la
            // izquierda. θ = 010 − 127,2 = −117,2.
            var c = WindComponents.Compute(10, 3, null, Skbo14RTrue);

            Assert.IsTrue(c.Available);
            Assert.AreEqual(-1.37, c.HeadwindKt,  0.02, "3 kt a 117° del morro es viento en cola");
            Assert.AreEqual(-2.67, c.CrosswindKt, 0.02, "y la cruzada llega desde la IZQUIERDA");
            Assert.IsTrue(c.CrosswindKt < 0);
        }

        [TestMethod]
        public void CasoRealSkbg17_ElVientoEnColaSeVeConSignoNegativo()
        {
            // METAR real de SKBG: 32003KT. Eje 17 verdadero 159,5 → θ = +160,5, casi de cola.
            var c = WindComponents.Compute(320, 3, null, Skbg17True);

            Assert.IsTrue(c.Available);
            Assert.AreEqual(-2.83, c.HeadwindKt,  0.02, "casi de cola: componente en cara negativa");
            Assert.AreEqual( 1.00, c.CrosswindKt, 0.02, "y la poca cruzada que queda es por la derecha");
        }

        // ── Casos límite, sobre el eje real de SKBO 14R ───────────────────────────
        // La dirección del viento se pone exactamente en el eje, a 90° y a 180° porque es lo único
        // que aísla la regla del signo: el aeropuerto y la pista son los reales.

        [TestMethod]
        public void VientoEnElEje_LaCruzadaEsCeroYTodoEsCara()
        {
            var c = WindComponents.Compute(Skbo14RTrue, 12, null, Skbo14RTrue);

            Assert.AreEqual(12.0, c.HeadwindKt,  1e-9);
            Assert.AreEqual( 0.0, c.CrosswindKt, 1e-9);
            Assert.IsFalse(c.CrosswindKt > 0 || c.CrosswindKt < 0);
        }

        [TestMethod]
        public void VientoA90PorLaDerecha_LaCruzadaEsPositiva()
        {
            // 127,2 + 90 = 217,2: el viento llega por la derecha de la pista.
            var c = WindComponents.Compute(Skbo14RTrue + 90, 12, null, Skbo14RTrue);

            Assert.AreEqual(0.0, c.HeadwindKt,  1e-9);
            Assert.AreEqual(12.0, c.CrosswindKt, 1e-9);
            Assert.IsTrue(c.CrosswindKt > 0, "cruzada positiva = desde la DERECHA");
        }

        [TestMethod]
        public void VientoA90PorLaIzquierda_LaCruzadaEsNegativa()
        {
            // 127,2 − 90 = 37,2: el viento llega por la izquierda. Es el lado que fija el signo.
            var c = WindComponents.Compute(Skbo14RTrue - 90, 12, null, Skbo14RTrue);

            Assert.AreEqual(-12.0, c.CrosswindKt, 1e-9);
            Assert.IsTrue(c.CrosswindKt < 0, "cruzada negativa = desde la IZQUIERDA");
        }

        [TestMethod]
        public void VientoEnCola_LaComponenteEnCaraEsNegativaYLaCruzadaCero()
        {
            var c = WindComponents.Compute(Skbo14RTrue + 180, 12, null, Skbo14RTrue);

            Assert.AreEqual(-12.0, c.HeadwindKt, 1e-9);
            Assert.AreEqual(  0.0, c.CrosswindKt, 1e-9);
        }

        [TestMethod]
        public void LaRachaNoEntraEnLasComponentes_PeroSeConservaParaPublicarla()
        {
            // 09008G19KT: las componentes salen del viento MEDIO (8 kt), no de la racha; la racha se
            // arrastra para poder escribir `WIND 090/08G19`.
            var c = WindComponents.Compute(90, 8, 19, Skbo14RTrue);

            Assert.AreEqual(8 * Math.Cos(WindComponents.DeltaDeg(90, Skbo14RTrue) * Math.PI / 180.0),
                            c.HeadwindKt, 1e-9);
            Assert.AreEqual(19.0, c.GustKt.Value, 1e-9);
            Assert.AreEqual("090/08G19", WindComponents.FormatRaw(c));
        }

        // ── Degradar sin datos ────────────────────────────────────────────────────

        [TestMethod]
        public void SinViento_NoSeInventanComponentes()
        {
            var c = WindComponents.Compute(null, null, null, Skbo14RTrue);

            Assert.IsFalse(c.Available);
            Assert.IsFalse(c.Calm);
            Assert.AreEqual(0.0, c.HeadwindKt);
            Assert.AreEqual(0.0, c.CrosswindKt);
            Assert.AreEqual("—", WindComponents.Format(c));
        }

        [TestMethod]
        public void SinRumboDePista_NoSeInventaUnNorte()
        {
            // Sin eje no hay descomposición. Y no se supone 0 (= norte), que daría una cruzada falsa:
            // se conserva el viento en crudo para poder publicarlo, pero sin componentes.
            var c = WindComponents.Compute(320, 12, 20, null);

            Assert.IsFalse(c.Available);
            Assert.AreEqual(0.0, c.HeadwindKt);
            Assert.AreEqual(0.0, c.CrosswindKt);
            Assert.AreEqual(320, c.WindDirDeg.Value, 1e-9);
            Assert.AreEqual(12,  c.WindSpeedKt.Value, 1e-9);
            Assert.AreEqual("320/12G20", WindComponents.FormatRaw(c));
            Assert.AreEqual("—", WindComponents.Format(c));
        }

        [TestMethod]
        public void DireccionVariable_VrbDelMetar_NoTieneComponentes()
        {
            // `VRB01KT` (el METAR real de SKCG del vuelo 41) no da dirección: se representa con null y
            // degrada. La intensidad sí se conserva.
            var c = WindComponents.Compute(null, 1, null, Skbo14RTrue);

            Assert.IsFalse(c.Available);
            Assert.AreEqual(1, c.WindSpeedKt.Value, 1e-9);
        }

        [TestMethod]
        public void Calma_EsCeroDeVerdadYNoUnHueco()
        {
            // 0/0 es «no sopla», no «no lo sé»: las componentes son 0 y el lado no significa nada.
            var c = WindComponents.Compute(0, 0, null, Skbo14RTrue);

            Assert.IsTrue(c.Available);
            Assert.IsTrue(c.Calm);
            Assert.AreEqual(0.0, c.HeadwindKt);
            Assert.AreEqual(0.0, c.CrosswindKt);
            Assert.AreEqual("CALM", WindComponents.Format(c));
        }

        [TestMethod]
        public void UnValorNoFinito_DegradaEnVezDePropagarNan()
        {
            Assert.IsFalse(WindComponents.Compute(double.NaN, 12, null, Skbo14RTrue).Available);
            Assert.IsFalse(WindComponents.Compute(320, double.NaN, null, Skbo14RTrue).Available);
            Assert.IsFalse(WindComponents.Compute(320, 12, null, double.NaN).Available);
        }

        // ── Formato ───────────────────────────────────────────────────────────────

        [TestMethod]
        public void ElFormatoPublicaElSignoDeLaCruzadaYNoElDeLaCola()
        {
            var derecha = WindComponents.Compute(Skbo14RTrue + 90, 5, null, Skbo14RTrue);
            var izquierda = WindComponents.Compute(Skbo14RTrue - 90, 5, null, Skbo14RTrue);
            var cola = WindComponents.Compute(Skbo14RTrue + 180, 11, null, Skbo14RTrue);

            Assert.AreEqual("HW 0 XW +5", WindComponents.Format(derecha));
            Assert.AreEqual("HW 0 XW -5", WindComponents.Format(izquierda));
            Assert.AreEqual("HW -11 XW 0", WindComponents.Format(cola));
        }

        [TestMethod]
        public void LaDireccionSePublicaATresDigitos()
        {
            var c = WindComponents.Compute(10, 3, null, Skbo14RTrue);
            Assert.AreEqual("010/03", WindComponents.FormatRaw(c));

            // 360° se publica como 000, que es la misma dirección.
            var norte = WindComponents.Compute(360, 12, null, Skbo14RTrue);
            Assert.AreEqual("000/12", WindComponents.FormatRaw(norte));
        }
    }

    /// <summary>
    /// **Hora de observación del METAR** (`Helpers/MetarObservationTime`), leída del grupo `ddHHMMZ`
    /// del texto en crudo. Los METAR son los reales del logbook local del piloto.
    ///
    /// El caso que importa —y el único ambiguo— es el **cambio de mes**: el grupo solo trae el día,
    /// así que un `302350Z` capturado a las 00:20 del día 1 es del mes **anterior**. Se resuelve del
    /// lado conservador (dato de hace minutos, no de dentro de un mes).
    /// </summary>
    [TestClass]
    public class MetarObservationTimeTests
    {
        [TestMethod]
        public void MetarRealDeSkbo_DevuelveSuHoraDeObservacion()
        {
            var captured = new DateTime(2026, 9, 25, 1, 46, 0, DateTimeKind.Utc);
            var obs = MetarObservationTime.Parse(
                "METAR SKBO 250100Z 01003KT 350V110 9999 BKN080 15/11 Q1028 NOSIG", captured);

            Assert.AreEqual(new DateTime(2026, 9, 25, 1, 0, 0, DateTimeKind.Utc), obs);
        }

        [TestMethod]
        public void MetarRealDeSkpe_ConRachaYRemark()
        {
            var captured = new DateTime(2026, 9, 24, 22, 5, 0, DateTimeKind.Utc);
            var obs = MetarObservationTime.Parse(
                "METAR SKPE 242100Z 09008G19KT 040V140 9999 FEW030TCU SCT050 28/19 Q1014 RMK TCU/E",
                captured);

            Assert.AreEqual(new DateTime(2026, 9, 24, 21, 0, 0, DateTimeKind.Utc), obs);
        }

        [TestMethod]
        public void CambioDeMes_UnaObservacionDeFinDeMesEsDelMesAnterior()
        {
            // 23:50Z del día 30, capturado a las 00:20Z del día 1: es el MISMO instante, un mes antes
            // en la cuenta ingenua. Sin esto, la hora de observación quedaría 30 días en el futuro.
            var captured = new DateTime(2026, 10, 1, 0, 20, 0, DateTimeKind.Utc);
            var obs = MetarObservationTime.Parse(
                "METAR SKCG 302350Z 32012KT 9999 SCT020 28/24 Q1009", captured);

            Assert.AreEqual(new DateTime(2026, 9, 30, 23, 50, 0, DateTimeKind.Utc), obs);
        }

        [TestMethod]
        public void SinGrupoDeHora_DevuelveNull()
        {
            Assert.IsNull(MetarObservationTime.Parse(null, DateTime.UtcNow));
            Assert.IsNull(MetarObservationTime.Parse("", DateTime.UtcNow));
            Assert.IsNull(MetarObservationTime.Parse("METAR SKBO 01003KT 9999 Q1028", DateTime.UtcNow));
            // Un `350V110` o un `BKN080` parecidos pero no son la hora.
            Assert.IsNull(MetarObservationTime.Parse("METAR SKBO 350V110 BKN080 Q1028", DateTime.UtcNow));
            // Un día imposible no se acepta.
            Assert.IsNull(MetarObservationTime.Parse("METAR SKBO 991200Z 01003KT", DateTime.UtcNow));
        }
    }

    /// <summary>
    /// **La línea de meteo del aterrizaje** (`Helpers/LandingWeatherLine`): el texto compacto que va
    /// al `notes` del PIREP. Es texto libre a propósito — un campo de `pirep_fields` nuevo exige que
    /// phpVMS lo cree y una clave desconocida se ignora **en silencio**.
    ///
    /// El caso de referencia es el real: SKBO 14R con el METAR del vuelo 36 del logbook del piloto.
    /// </summary>
    [TestClass]
    public class LandingWeatherLineTests
    {
        private const double Skbo14RTrue = 127.231532474263;
        private const string MetarSkbo =
            "METAR SKBO 250100Z 01003KT 350V110 9999 BKN080 15/11 Q1028 NOSIG";

        [TestMethod]
        public void LaLineaRealDelPirep_LlevaMetarVientoYComponentes()
        {
            var wind = WindComponents.Compute(10, 3, null, Skbo14RTrue);
            string line = LandingWeatherLine.Build(MetarSkbo, null, wind);

            Assert.AreEqual(
                "WX LANDING: " + MetarSkbo + " | WIND 010/03 | HW -1 XW -3",
                line);
        }

        [TestMethod]
        public void SinRumboDePista_SePublicaElVientoCrudoPeroNoLasComponentes()
        {
            var wind = WindComponents.Compute(320, 12, 20, null);
            string line = LandingWeatherLine.Build(MetarSkbo, null, wind);

            Assert.AreEqual("WX LANDING: " + MetarSkbo + " | WIND 320/12G20", line);
            Assert.IsFalse(line.Contains("HW "), "sin eje no hay componentes que publicar");
        }

        [TestMethod]
        public void SinViento_SePublicaSoloElMetar()
        {
            var wind = WindComponents.Compute(null, null, null, Skbo14RTrue);
            Assert.AreEqual("WX LANDING: " + MetarSkbo,
                            LandingWeatherLine.Build(MetarSkbo, null, wind));
        }

        [TestMethod]
        public void SinMetar_SePublicaSoloElViento()
        {
            var wind = WindComponents.Compute(10, 3, null, Skbo14RTrue);
            Assert.AreEqual("WX LANDING: WIND 010/03 | HW -1 XW -3",
                            LandingWeatherLine.Build(null, null, wind));
        }

        [TestMethod]
        public void SinNada_NoSeAnadeLineaAlNotes()
        {
            var wind = WindComponents.Compute(null, null, null, null);
            Assert.IsNull(LandingWeatherLine.Build(null, null, wind));
            Assert.IsNull(LandingWeatherLine.Build("   ", null, wind));
        }

        [TestMethod]
        public void ElSignoDeLaCruzadaSeLeePalabra()
        {
            var derecha   = WindComponents.Compute(Skbo14RTrue + 90, 8, null, Skbo14RTrue);
            var izquierda = WindComponents.Compute(Skbo14RTrue - 90, 8, null, Skbo14RTrue);

            StringAssert.Contains(LandingWeatherLine.Display("14R", derecha), "from the right");
            StringAssert.Contains(LandingWeatherLine.Display("14R", izquierda), "from the left");
        }

        [TestMethod]
        public void LaLineaDePantalla_DiceLoMismoQueElPirep()
        {
            var wind = WindComponents.Compute(10, 3, null, Skbo14RTrue);
            string display = LandingWeatherLine.Display("14R", wind);

            StringAssert.Contains(display, "WIND 010/03 kt");
            StringAssert.Contains(display, "RWY 14R TRUE 127°");
            StringAssert.Contains(display, "TAILWIND 1.4 kt");
            StringAssert.Contains(display, "CROSSWIND 2.7 kt (from the left)");
            // Sin datos no se inventa: ni componentes ni lado.
            StringAssert.Contains(
                LandingWeatherLine.Display("14R", WindComponents.Compute(null, null, null, Skbo14RTrue)),
                "no wind recorded");
        }
    }
}
