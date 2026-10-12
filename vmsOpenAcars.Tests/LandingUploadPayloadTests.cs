using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El cuerpo que se sube a phpVMS** (`POST /api/pireps/{id}/landing`), probado contra los
    /// **números reales del vuelo 44** —SKCC → SKBG, pista 17, **−789 fpm, 2,5 g, score 42**, toque a
    /// 202,3 ft del umbral legal—, que es el mismo payload que ellos usaron como ejemplo al aceptar
    /// el contrato el 11/10/2026.
    ///
    /// Lo que se prueba aquí no es «que serialice»: es **dónde acaba cada hueco**. Su ingesta es un
    /// reemplazo total y su validación es todo o nada, así que un `0` donde no hay medida envenena
    /// sus medianas y un `NaN` tumba el vuelo entero. Las tres reglas que se fijan:
    ///
    /// 1. el centinela `NoLandingData` **no viaja** (sería un −1 en su agregación),
    /// 2. `0` es medida en `touchdown_dist_ft` y hueco en `centerline_dev_ft` —cada uno como lo
    ///    entiende la puntuación de este cliente—,
    /// 3. los `null` **van escritos en el JSON**, no omitidos, y nada no finito se cuela.
    /// </summary>
    [TestClass]
    public class LandingUploadPayloadTests
    {
        private const string Version = "0.9.37";

        /// <summary>
        /// El registro del **vuelo 44** tal como lo deja el snapshot: aeronave real del mantenedor
        /// (iFly 737 MAX 8), LTOW medido y meteo del aterrizaje.
        /// </summary>
        private static FlightRecord Vuelo44() => new FlightRecord
        {
            Id              = 44,
            FlightNumber     = "9060",
            Origin          = "SKCC",
            Destination     = "SKBG",
            RunwayName      = "17",
            FlightDate      = new DateTime(2026, 10, 9, 11, 26, 39, DateTimeKind.Utc),
            LandingRateFpm  = -789,
            GForce          = 2.5,
            TouchdownDistFt = 202.3,
            CenterlineDevFt = 24.4,
            Score           = 42,
            MetarRaw        = "SKBG 091100Z 01003KT 9999 SCT020 24/19 Q1013",
            LandingWeightLbs       = 150821.6,
            RunwayLengthFt         = 7362.0,
            LandingRunwayTrueDeg   = 173.2,
            AircraftIcao           = "B38M",
            AircraftTitle          = "iFly B38M VHR N665VH (178Seat)",
            AircraftModel          = "737 MAX 8",
            FlareCaptureArmed      = true,
            LandingWindDirDeg      = 10.0,
            LandingWindSpeedKt     = 3.0,
            LandingWindGustKt      = null,   // sin racha: el METAR no la traía y FSUIPC no la publica
            LandingHeadwindKt      = 1.4,
            LandingCrosswindKt     = -2.7,
        };

        /// <summary>
        /// La traza del flare, recortada a las muestras que deciden el ajuste: una en aproximación,
        /// la del cruce del umbral y la del contacto. Los valores son los del contrato
        /// (`ts_utc`, `dist_ft` negativo pasado el umbral, flaps del detente real).
        /// </summary>
        private static List<FlareTrackPoint> Traza()
        {
            var t0 = new DateTime(2026, 10, 9, 11, 26, 39, 733, DateTimeKind.Utc);
            return new List<FlareTrackPoint>
            {
                new FlareTrackPoint
                {
                    SeqNo = 0, TimestampUtc = t0,
                    DistFt = 1482.8, AglFt = 95.0, RadarAltFt = 362.8, IasKt = 149.0, VsFpm = -850,
                    PitchDeg = 2.5, BankDeg = 0.3, GsKt = 145,
                    Eng1Pct = 55.0, Eng2Pct = 55.0, Eng1N2Pct = null, Eng2N2Pct = null,
                    FlapsPct = 62.5, FlapsIndex = 5, SpoilersDeployed = false, OnGround = false,
                },
                new FlareTrackPoint
                {
                    SeqNo = 1, TimestampUtc = t0.AddMilliseconds(100),
                    DistFt = 12.0, AglFt = 18.5, RadarAltFt = 22.0, IasKt = 148.0, VsFpm = -420,
                    PitchDeg = 4.1, BankDeg = 0.1, GsKt = 143,
                    Eng1Pct = 52.0, Eng2Pct = 52.0, Eng1N2Pct = null, Eng2N2Pct = null,
                    FlapsPct = 62.5, FlapsIndex = 5, SpoilersDeployed = false, OnGround = false,
                },
                new FlareTrackPoint
                {
                    // La transición aire→tierra: la muestra del salto **ya es «en tierra»**, así que
                    // el ajuste de la toma es el de la anterior (así lo define `FlapTrackSummary`).
                    SeqNo = 2, TimestampUtc = t0.AddMilliseconds(200),
                    DistFt = -700.0, AglFt = 0.0, RadarAltFt = null, IasKt = 140.0, VsFpm = -120,
                    PitchDeg = 1.2, BankDeg = 0.0, GsKt = 138,
                    Eng1Pct = 40.0, Eng2Pct = 40.0, Eng1N2Pct = null, Eng2N2Pct = null,
                    FlapsPct = 62.5, FlapsIndex = 5, SpoilersDeployed = true, OnGround = true,
                },
            };
        }

        [TestMethod]
        public void Vuelo44_LasMetricasYLaTrazaSalenComoElContrato()
        {
            var p = LandingUploadPayload.Build(Vuelo44(), Traza(), Version);

            Assert.AreEqual(1, p.schema_version);
            Assert.AreEqual("vmsOpenACars/0.9.37", p.source_name);

            var m = p.metrics;
            Assert.AreEqual(-789, m.landing_rate_fpm);
            Assert.AreEqual(2.5,  m.g_force.Value, 1e-9);
            Assert.AreEqual(202.3, m.touchdown_dist_ft.Value, 1e-9);
            Assert.AreEqual(24.4, m.centerline_dev_ft.Value, 1e-9);
            Assert.AreEqual(42,   m.score);
            Assert.AreEqual("17", m.runway_name);
            Assert.AreEqual(7362.0, m.runway_length_ft.Value, 1e-9);
            Assert.AreEqual(173.2,  m.runway_true_deg.Value, 1e-9);
            Assert.AreEqual(150821.6, m.landing_weight_lbs.Value, 1e-9);
            Assert.AreEqual("B38M", m.aircraft_icao);
            Assert.AreEqual("iFly B38M VHR N665VH (178Seat)", m.aircraft_title);
            Assert.AreEqual("737 MAX 8", m.aircraft_model);
            Assert.AreEqual(1.4,  m.headwind_kt.Value, 1e-9);
            Assert.AreEqual(-2.7, m.crosswind_kt.Value, 1e-9);
            Assert.IsTrue(m.flare_capture_armed.Value);

            // Los flaps del **detente real** (`0x0BFC`): etiqueta sin «≈», porcentaje del mando y el
            // índice crudo. La familia sale del `B38M`, que `FlapSetting` reconoce como 737 (MAX).
            Assert.AreEqual("FLAPS 15", m.flaps_label);
            Assert.AreEqual(62.5, m.flaps_pct.Value, 1e-9);
            Assert.AreEqual(5, m.flaps_index);

            Assert.AreEqual(3, p.trace.Count);
            // El signo de `dist_ft` es el de la traza y **no se toca**: negativo pasado el umbral.
            Assert.AreEqual(1482.8, p.trace[0].dist_ft, 1e-9);
            Assert.AreEqual(-700.0, p.trace[2].dist_ft, 1e-9);
            Assert.IsTrue(p.trace[2].on_ground);
            Assert.IsFalse(p.trace[0].on_ground);
            Assert.AreEqual(5, p.trace[0].flaps_index);
            // El N2 no lo publica este addon: viaja como hueco, no como 0.
            Assert.IsNull(p.trace[0].n2_1_pct);
            Assert.IsNull(p.trace[2].radar_alt_ft);
        }

        [TestMethod]
        public void SinToqueCapturado_ElCentinelaNoViajaYLosDatosDeToqueSonHuecos()
        {
            // Un vuelo que terminó sin capturar el contacto: el registro guarda el centinela -1 y
            // los cuatro datos del toque se quedan a cero. Ninguno de los cinco puede publicarse:
            // el -1 entraría en su mediana como una tasa real y los ceros como medidas.
            var r = Vuelo44();
            r.LandingRateFpm  = ScoringService.NoLandingData;
            r.GForce          = 0.0;
            r.TouchdownDistFt = 0.0;
            r.CenterlineDevFt = 0.0;

            var m = LandingUploadPayload.Build(r, Traza(), Version).metrics;

            Assert.IsNull(m.landing_rate_fpm, "el centinela -1 no puede viajar como tasa");
            Assert.IsNull(m.g_force);
            Assert.IsNull(m.touchdown_dist_ft);
            Assert.IsNull(m.centerline_dev_ft);
            // Lo que sí existe sin toque capturado se sigue mandando: pista, avión, meteo y viento.
            Assert.AreEqual("17", m.runway_name);
            Assert.AreEqual("B38M", m.aircraft_icao);
        }

        [TestMethod]
        public void UnaTasaCapturadaEnPositivoSePublicaEnNegativo()
        {
            // Medido en el log real: **3 de los 41 vuelos con dato (7%)** tienen la tasa en positivo
            // (+156 y +34 fpm entre ellos), que en un toque es un artefacto del detector de contacto
            // —los tres con el toque a 0 ft y sin captura de flare, la firma de las capturas
            // antiguas—, no una subida. El scoring lo tolera (`CalcLandingRateDeduction` usa
            // `Math.Abs`), pero **la mediana de phpVMS se calcula sobre el valor con signo**: un
            // positivo se va al otro lado del cero y la arrastra hacia arriba.
            var r = Vuelo44();
            r.LandingRateFpm = 156;

            Assert.AreEqual(-156, LandingUploadPayload.Build(r, Traza(), Version).metrics.landing_rate_fpm,
                            "una tasa positiva tiene que publicarse en negativo");

            r.LandingRateFpm = -789;
            Assert.AreEqual(-789, LandingUploadPayload.Build(r, Traza(), Version).metrics.landing_rate_fpm,
                            "una tasa normal no se toca");
        }

        [TestMethod]
        public void ElMotivoDeCierreDeLaTraza_ViajaY_SuAusenciaEsNull()
        {
            // phpVMS lo pidió y aceptó con este nombre el 11/10/2026, y no es decorativo: con él su IA
            // y su gráfico distinguen un final normal (`stopped:post-touchdown`, `stopped:on-ground`)
            // de una traza **truncada** (`stopped:cap`, `stopped:timeout`).
            //
            // **`null` es «la captura no llegó a cerrarse»**, no «no se sabe»: ese caso tiene que
            // viajar como null para que no lo lean como un final.
            var r = Vuelo44();

            r.TraceStopReason = "stopped:post-touchdown 2.0s";
            Assert.AreEqual("stopped:post-touchdown 2.0s",
                            LandingUploadPayload.Build(r, Traza(), Version).metrics.trace_stop_reason,
                            "el motivo va tal cual, con el número en formato invariante");

            r.TraceStopReason = "stopped:cap";
            Assert.AreEqual("stopped:cap",
                            LandingUploadPayload.Build(r, Traza(), Version).metrics.trace_stop_reason,
                            "el que marca traza truncada");

            r.TraceStopReason = null;
            Assert.IsNull(LandingUploadPayload.Build(r, Traza(), Version).metrics.trace_stop_reason,
                          "sin cierre va null: es un valor, no un hueco");
        }

        [TestMethod]
        public void ElMotivoDemasiadoLargo_SeRecortaAlTopeDeSuValidacion()
        {
            // **El tope es el suyo (32), no uno nuestro más holgado, y este test existe por eso:** su
            // validación es todo o nada, así que un motivo que se pase **no se recorta en su lado:
            // tumba el envío entero**, traza y métricas incluidas. Recortando nosotros, un motivo nuevo
            // y largo cuesta una cadena cortada en vez de costar el análisis del vuelo.
            //
            // Y el recorte tiene que **conservar el prefijo**, que es lo que ellos parsean (hasta el
            // primer espacio): el prefijo más largo del vocabulario son 22 caracteres, así que en 32
            // entra entero siempre.
            var r = Vuelo44();

            r.TraceStopReason = "stopped:post-touchdown 1234.5s";   // 31: entra sin recorte
            Assert.AreEqual("stopped:post-touchdown 1234.5s",
                            LandingUploadPayload.Build(r, Traza(), Version).metrics.trace_stop_reason);

            r.TraceStopReason = "stopped:un-motivo-nuevo-y-larguisimo-que-nadie-habia-previsto";
            var recortado = LandingUploadPayload.Build(r, Traza(), Version).metrics.trace_stop_reason;
            Assert.AreEqual(LandingUploadPayload.MaxTraceStopReason, recortado.Length,
                            "se recorta a SU tope, que es lo que su validación admite");
            Assert.IsTrue(recortado.StartsWith("stopped:"),
                          "y el prefijo sobrevive, que es lo que su parser mira");
        }

        [TestMethod]
        public void ElDesfaseDelUmbralViajaTalCual_PeroSinPistaEsNull()
        {
            // phpVMS lo pidió el 11/10/2026 para alinear la traza del flare con una **resta** en vez de
            // deducirla (los 496 ft de la 17 de SKBG, que hasta ahora deducían y con guardas).
            //
            // **Aquí el 0 NO es «sin dato»**: es «el umbral legal está pegado al extremo físico», así
            // que tiene que llegar como 0. Quien dice «no lo sé» —y por tanto «dedúcelo tú»— es null.
            var r = Vuelo44();

            r.OffsetThresholdFt = 496.0;
            Assert.AreEqual(496.0,
                            LandingUploadPayload.Build(r, Traza(), Version).metrics.offset_threshold_ft.Value, 1e-9,
                            "el desfase real de la 17 de SKBG");

            r.OffsetThresholdFt = 0.0;
            Assert.AreEqual(0.0,
                            LandingUploadPayload.Build(r, Traza(), Version).metrics.offset_threshold_ft.Value, 1e-9,
                            "un 0 es una medida: la pista no tiene umbral desplazado");

            r.OffsetThresholdFt = null;
            Assert.IsNull(LandingUploadPayload.Build(r, Traza(), Version).metrics.offset_threshold_ft,
                          "sin pista resuelta va null, que es la señal para que ellos sigan deduciendo");
        }

        [TestMethod]
        public void ElVrefDelPlanViajaEntero_Y_SinPlanVaNull()
        {
            // El Vref sale del TLR de SimBrief, al **peso de planeo**. No se estima por tipo de avión:
            // la ventana de Vapp que usa el gate de aproximación no es un Vref, y mandarla con este
            // nombre sería una etiqueta donde hay un valor.
            var r = Vuelo44();

            r.VrefKt = 148;
            Assert.AreEqual(148, LandingUploadPayload.Build(r, Traza(), Version).metrics.vref_kt,
                            "el Vref del plan de ese vuelo");

            r.VrefKt = null;
            Assert.IsNull(LandingUploadPayload.Build(r, Traza(), Version).metrics.vref_kt,
                          "sin TLR no se inventa un Vref");
        }

        [TestMethod]
        public void ElCeroDeLaDistanciaDeTomaEsUnaMedida_YElDeLaDesviacionDeEjeNo()
        {
            // Tocar **justo en el umbral** es 0 ft de verdad, y ahí el 0 es el dato. En el eje, el
            // 0 es «no medido» —es el guard que usa `ScoringService` (`CenterlineDeviationFt > 0`)—,
            // así que publicarlo diría «centrado perfecto» de algo que nadie midió.
            var r = Vuelo44();
            r.TouchdownDistFt = 0.0;
            r.CenterlineDevFt = 0.0;

            var m = LandingUploadPayload.Build(r, Traza(), Version).metrics;

            Assert.AreEqual(0.0, m.touchdown_dist_ft.Value, 1e-9);
            Assert.IsNull(m.centerline_dev_ft);
        }

        [TestMethod]
        public void ElJsonLlevaLosNullEscritos_NoLosOmite()
        {
            // Es lo que phpVMS pidió expresamente: un campo sin dato tiene que **llegar** como null.
            // Si alguien añadiera `NullValueHandling.Ignore` a este camino, los campos
            // desaparecerían del cuerpo y la prueba se pone roja.
            string json = LandingUploadPayload.ToJson(
                LandingUploadPayload.Build(Vuelo44(), Traza(), Version));

            StringAssert.Contains(json, "\"wind_gust_kt\":null");
            StringAssert.Contains(json, "\"n2_1_pct\":null");
            StringAssert.Contains(json, "\"radar_alt_ft\":null");
            StringAssert.Contains(json, "\"schema_version\":1");
            StringAssert.Contains(json, "\"flaps_index\":5");
        }

        [TestMethod]
        public void LosEnterosDelContratoNoLlevanDecimales()
        {
            // Su validación rechaza decimales en estos tres campos, y el rechazo es **todo o nada**:
            // un `-789.0` no perdería el campo, perdería la traza entera del vuelo.
            string json = LandingUploadPayload.ToJson(
                LandingUploadPayload.Build(Vuelo44(), Traza(), Version));

            StringAssert.Contains(json, "\"landing_rate_fpm\":-789");
            StringAssert.Contains(json, "\"score\":42");
            Assert.IsFalse(json.Contains("\"landing_rate_fpm\":-789."), "la tasa no puede ir con decimales");
            Assert.IsFalse(json.Contains("\"score\":42."), "la puntuación no puede ir con decimales");
        }

        [TestMethod]
        public void ElInstanteDeCadaMuestraVaEnIso8601Utc()
        {
            var p = LandingUploadPayload.Build(Vuelo44(), Traza(), Version);

            Assert.AreEqual("2026-10-09T11:26:39.733Z", p.trace[0].ts_utc);
            Assert.AreEqual("2026-10-09T11:26:39.833Z", p.trace[1].ts_utc);
        }

        [TestMethod]
        public void ElHusoSeConvierteDeVerdad_NoSeReetiquetaLaHoraLocal()
        {
            // El fallo que destapó el relleno de los vuelos 43 y 44: al releer el log local, la marca
            // vuelve en hora **local**, y aquí se reetiquetaba como UTC sin convertirla, así que cada
            // muestra salía el huso entero antes (5 h en Colombia). Las tres formas de la MISMA marca
            // tienen que acabar en el mismo instante.
            var utc = new DateTime(2026, 10, 9, 11, 26, 39, 733, DateTimeKind.Utc);

            var traza = Traza();
            traza[0].TimestampUtc = utc;                                              // en vivo
            traza[1].TimestampUtc = utc.ToLocalTime();                                // releída del log
            traza[2].TimestampUtc = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified); // sin huso

            var p = LandingUploadPayload.Build(Vuelo44(), traza, Version);

            Assert.AreEqual("2026-10-09T11:26:39.733Z", p.trace[0].ts_utc, "una marca UTC sale tal cual");
            Assert.AreEqual("2026-10-09T11:26:39.733Z", p.trace[1].ts_utc,
                            "una marca local se convierte, no se reetiqueta");
            Assert.AreEqual("2026-10-09T11:26:39.733Z", p.trace[2].ts_utc,
                            "sin huso se interpreta como UTC");
        }

        [TestMethod]
        public void UnValorNoFinitoSeConvierteEnHueco_YNoRompeElJson()
        {
            // `FlareTrackPoint` puede traer NaN (el propio `FlapTrackSummary` los filtra), y Newtonsoft
            // escribiría `NaN`, que **no es JSON válido**: su validador tiraría el payload completo y
            // se perdería la traza por una muestra.
            var traza = Traza();
            traza[0].FlapsPct  = double.NaN;
            traza[0].IasKt     = double.PositiveInfinity;

            var p = LandingUploadPayload.Build(Vuelo44(), traza, Version);
            string json = LandingUploadPayload.ToJson(p);

            Assert.IsNull(p.trace[0].flaps_pct);
            Assert.IsNull(p.trace[0].ias_kt);
            Assert.IsFalse(json.Contains("NaN"), "NaN no es JSON válido");
            Assert.IsFalse(json.Contains("Infinity"), "Infinity no es JSON válido");
        }

        [TestMethod]
        public void LosTopesDeLongitudSeRespetan_PorqueSuValidacionEsTodoONada()
        {
            var r = Vuelo44();
            r.MetarRaw      = new string('M', 520);   // su tope son 500 (subido a petición nuestra el 11/10/2026)
            r.AircraftTitle = new string('T', 250);   // el suyo son 191

            var m = LandingUploadPayload.Build(r, Traza(), Version).metrics;

            Assert.AreEqual(500, m.landing_metar.Length);
            Assert.AreEqual(191, m.aircraft_title.Length);
        }

        [TestMethod]
        public void UnRellenoHistoricoSeMarcaComoTalEnElSourceName()
        {
            // phpVMS nos pidió poder distinguir un aterrizaje **volado** de uno **rellenado**, y el
            // `source_name` es la única marca de procedencia que guardan. La cadena está concretada
            // con ellos y no puede cambiar sin avisarles.
            var vivo    = LandingUploadPayload.Build(Vuelo44(), Traza(), Version);
            var relleno = LandingUploadPayload.Build(Vuelo44(), Traza(), Version, isBackfill: true);

            Assert.AreEqual("vmsOpenACars/0.9.37", vivo.source_name);
            Assert.AreEqual("vmsOpenACars/0.9.37-relleno", relleno.source_name);
            Assert.IsTrue(relleno.source_name.Length <= LandingUploadPayload.MaxSourceName,
                          "el source_name tiene que caber en su tope de 64");
        }

        [TestMethod]
        public void SinTraza_LaListaVaVaciaYLasMetricasSeMandanIgual()
        {
            // Un vuelo sin captura de flare sigue aportando a su agregación: las métricas van en
            // columnas y son lo agregable. Lo que no se hace es inventar muestras.
            var p = LandingUploadPayload.Build(Vuelo44(), new List<FlareTrackPoint>(), Version);

            Assert.AreEqual(0, p.trace.Count);
            Assert.AreEqual(-789, p.metrics.landing_rate_fpm);
            Assert.IsNull(p.metrics.flaps_index, "sin traza no hay detente que publicar");
            Assert.IsNull(p.metrics.power_cut_sec, "sin traza no hay corte que publicar");
        }

        [TestMethod]
        public void LosCamposVaciosDelRegistroSonHuecos_NoCadenasVacias()
        {
            var r = Vuelo44();
            r.RunwayName    = "   ";
            r.MetarRaw      = null;
            r.AircraftIcao  = "";
            r.AircraftModel = null;

            var m = LandingUploadPayload.Build(r, Traza(), Version).metrics;

            Assert.IsNull(m.runway_name);
            Assert.IsNull(m.landing_metar);
            Assert.IsNull(m.aircraft_icao);
            Assert.IsNull(m.aircraft_model);
        }
    }
}
