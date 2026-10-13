using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **La puerta que decide si se pregunta a NavData por el aeropuerto de aproximación cercano**
    /// (`DiversionQueryGate`), con los datos reales del vuelo que la rompió: `wk0896ajpxKalPrp`,
    /// SKBQ→SKCL, `vmsOpenACars/0.9.40`, que **alternó a SKMR** (Montería, 41 ft) sin que el cliente
    /// lo detectara —el PIREP quedó con `arr_airport_id = SKCL` y el QNH se comparó contra SKCL—.
    ///
    /// La condición de entonces era `computedAgl > 1000`, y ese AGL se mide contra la elevación del
    /// destino **planeado** (SKCL, 3 162 ft) mientras no haya desvío confirmado: a 3 181 ft MSL el
    /// AGL salía **19 ft** —y negativo, de **−43 a −3 111 ft**, en el 100 % de los sondeos de las dos
    /// finales—, así que la consulta no se hizo ni una vez y SKMR nunca llegó a ser candidato.
    ///
    /// El **test principal** es el caso real: 3 181 ft MSL con destino SKCL ⇒ la puerta **abierta**.
    /// Con el código viejo (`computedAgl > 1000` con 19 ft de AGL) ese test **falla**.
    /// </summary>
    [TestClass]
    public class DiversionQueryGateTests
    {
        // ── Datos reales del vuelo `wk0896ajpxKalPrp` ────────────────────────────

        /// <summary>Altitud del avión en el momento de la medida (log de las 04:11:12).</summary>
        private const double RealAltitudeMslFt = 3181.0;

        /// <summary>Elevación de SKCL, el destino planeado: la referencia equivocada.</summary>
        private const double SkclElevationFt = 3162.0;

        /// <summary>Elevación de SKMR, el campo al que realmente se alternó (41 ft).</summary>
        private const double SkmrElevationFt = 41.0;

        /// <summary>El AGL que el cliente calculó con la referencia del destino planeado: 3181 − 3162.</summary>
        private const double RealComputedAglFt = RealAltitudeMslFt - SkclElevationFt;   // 19 ft

        /// <summary>Cadencia del sondeo (5 s), el throttle de siempre.</summary>
        private const double ThrottleSec = 5.0;

        // ── El caso real: la puerta tiene que estar ABIERTA ──────────────────────

        /// <summary>
        /// **El test del caso real.** Aeronave a 3 181 ft MSL, destino planeado SKCL (3 162 ft) ⇒
        /// `computedAgl` = 19 ft. La puerta debe abrirse: con el código viejo (`computedAgl > 1000`)
        /// quedaba cerrada, que es exactamente el fallo medido.
        /// </summary>
        [TestMethod]
        public void CasoReal_3181FtMslConDestinoSKCL_LaPuertaEstaAbierta()
        {
            Assert.AreEqual(19.0, RealComputedAglFt, 1e-9,
                            "3 181 ft MSL contra los 3 162 ft de SKCL son 19 ft de AGL");

            bool abierta = DiversionQueryGate.ShouldQuery(
                navAvailable: true, reconfirming: false,
                computedAglFt: RealComputedAglFt, altitudeMslFt: RealAltitudeMslFt,
                secondsSinceLastQuery: ThrottleSec);

            Assert.IsTrue(abierta,
                "a 3 181 ft MSL (muy por debajo de los 10 000 ft del techo) hay que preguntar: " +
                "el avión estaba en final a SKMR y con la puerta cerrada el PIREP quedó con SKCL");

            // La condición vieja, para que el fallo quede fijado en el propio test: si el helper
            // volviera a colgar de este término, el caso real se cerraría otra vez.
            Assert.IsFalse(RealComputedAglFt > 1000.0,
                "el AGL de la referencia equivocada es 19 ft: `computedAgl > 1000` NO se cumple");
        }

        /// <summary>
        /// La medición de las dos finales, sondeo a sondeo: con la referencia del destino planeado el
        /// AGL salió **negativo en el 100 % de las muestras** (de −43 a −3 111 ft). Ninguna de esas
        /// muestras abre la condición vieja, y **todas** tienen que abrir la nueva (el avión estaba
        /// entre 51 y 1 067 ft MSL, muy por debajo del techo).
        /// </summary>
        [TestMethod]
        public void CasoReal_ElAglNegativoDeLasDosFinales_YaNoBloquea()
        {
            // El rango medido: −43 ft (ya por debajo de la elevación de SKCL) a −3 111 ft.
            double[] aglMedidos = { -43.0, -3111.0 };

            foreach (double agl in aglMedidos)
            {
                Assert.IsFalse(agl > 1000.0,
                    $"el AGL medido {agl:F0} ft no cumple la condición vieja (por eso no se llamó nunca)");

                Assert.IsTrue(DiversionQueryGate.ShouldQuery(
                        true, false, agl, RealAltitudeMslFt, ThrottleSec),
                    $"con el avión a {RealAltitudeMslFt:F0} ft MSL y el AGL a {agl:F0} ft, la puerta " +
                    "tiene que estar abierta: el campo real está 3 121 ft por debajo de SKCL");
            }
        }

        // ── El throttle: ni en crucero ni a destiempo ────────────────────────────

        /// <summary>
        /// **A FL350 la puerta está cerrada.** Es el throttle que se conserva: a 35 000 ft MSL ningún
        /// aeropuerto cercano es todavía el de llegada. (Con el destino planeado a 3 162 ft el AGL de
        /// la referencia vieja vale 31 838 ft, así que una disyunción con `computedAgl > 1000` habría
        /// dejado el crucero abierto.)
        /// </summary>
        [TestMethod]
        public void EnCruceroFL350_LaPuertaEstaCerrada()
        {
            const double fl350Msl = 35000.0;
            double aglConReferenciaDeSKCL = fl350Msl - SkclElevationFt;   // 31 838 ft

            Assert.IsFalse(DiversionQueryGate.ShouldQuery(
                true, false, aglConReferenciaDeSKCL, fl350Msl, ThrottleSec),
                "a FL350 no se pregunta por aeropuertos de aproximación");
        }

        /// <summary>El throttle de 5 s: por debajo no se vuelve a preguntar, a los 5 s sí.</summary>
        [TestMethod]
        public void ElThrottleDeCincoSegundos_SeRespeta()
        {
            Assert.IsFalse(DiversionQueryGate.ShouldQuery(
                true, false, RealComputedAglFt, RealAltitudeMslFt, 0.0),
                "recién consultado (0 s) no se pregunta otra vez");

            Assert.IsFalse(DiversionQueryGate.ShouldQuery(
                true, false, RealComputedAglFt, RealAltitudeMslFt, 4.9),
                "a 4,9 s tampoco: la cadencia es de 5 s");

            Assert.IsTrue(DiversionQueryGate.ShouldQuery(
                true, false, RealComputedAglFt, RealAltitudeMslFt, ThrottleSec),
                "a los 5 s ya toca el siguiente sondeo");
        }

        // ── Las dos condiciones que nunca abren la puerta ────────────────────────

        /// <summary>Sin NavData no hay a quién preguntar: la puerta no se abre ni estando bajo.</summary>
        [TestMethod]
        public void SinNavData_NoSeConsulta()
        {
            Assert.IsFalse(DiversionQueryGate.ShouldQuery(
                false, false, RealComputedAglFt, RealAltitudeMslFt, ThrottleSec));
        }

        /// <summary>Con una consulta ya en vuelo (`_reconfirmingApproachAirport`) no se lanza otra.</summary>
        [TestMethod]
        public void ConUnaConsultaEnVuelo_NoSeLanzaOtra()
        {
            Assert.IsFalse(DiversionQueryGate.ShouldQuery(
                true, true, RealComputedAglFt, RealAltitudeMslFt, ThrottleSec));
        }

        // ── Degradar sin datos ───────────────────────────────────────────────────

        /// <summary>
        /// **Sin altitud MSL la puerta se comporta como hoy** (regla de la casa: degradar sin datos,
        /// no abrir por una suposición). Con `NaN` manda `computedAgl > 1000`: el caso real **sigue
        /// cerrado** —igual que hoy—, y un AGL alto sigue abierto.
        /// </summary>
        [TestMethod]
        public void SinAltitudMsl_LaPuertaSeComportaComoHoy()
        {
            Assert.IsFalse(DiversionQueryGate.ShouldQuery(
                true, false, RealComputedAglFt, double.NaN, ThrottleSec),
                "sin altitud MSL, los 19 ft de AGL del caso real no abren nada: hoy tampoco lo hacían");

            Assert.IsTrue(DiversionQueryGate.ShouldQuery(
                true, false, 30000.0, double.NaN, ThrottleSec),
                "sin altitud MSL se conserva la condición de hoy: AGL > 1 000 ft");
        }

        // ── La geometría del campo al que se alternó ─────────────────────────────

        /// <summary>
        /// **Las dos tomas de SKMR, con sus coordenadas reales, caen dentro de media milla del ARP**
        /// —frente a las 44 NM a SKCZ y las 319 NM a SKCL del mismo sondeo—. Es lo que fija que el
        /// aeropuerto al que el servidor apuntaba **era el campo donde el avión tocó**, y por qué el
        /// fallo de la puerta costó el `arr_airport_id` del PIREP.
        /// </summary>
        [TestMethod]
        public void LasDosTomasDeSKMR_QuedanDentroDeMediaNmDelArp()
        {
            // ARP de SKMR y los dos toques de la pista 14/32 (rumbos 316° y 136°, los del eje real).
            const double arpLat = 8.822856, arpLon = -75.825172;
            const double toque32Lat = 8.82180, toque32Lon = -75.82429;   // 04:17:44
            const double toque14Lat = 8.82754, toque14Lon = -75.82944;   // 04:23:27

            double d32 = GeoMath.DistanceNm(toque32Lat, toque32Lon, arpLat, arpLon);
            double d14 = GeoMath.DistanceNm(toque14Lat, toque14Lon, arpLat, arpLon);

            Assert.AreEqual(0.08, d32, 0.01,
                "el toque de la 32 está a 0,08 NM del ARP de SKMR (medido)");
            Assert.AreEqual(0.38, d14, 0.01,
                "el toque de la 14 está a 0,38 NM del ARP de SKMR (medido)");
            Assert.IsTrue(d32 < 0.4 && d14 < 0.4,
                "los dos toques están dentro de 0,4 NM del ARP: el avión aterrizó en SKMR, no en SKCL");
        }
    }
}
