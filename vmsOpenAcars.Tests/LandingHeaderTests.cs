using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La cabecera de datos del gráfico del flare**: qué se enseña, en qué orden y —lo que decide
    /// el diseño— **qué se calla cuando falta**.
    ///
    /// Un campo sin dato **no se añade**: el gráfico de un vuelo sin título de aeronave, sin peso o
    /// sin viento enseña menos líneas, no líneas con ceros. Ese «bloque con sus huecos» es lo que se
    /// prueba aquí, con el **vuelo 41 real** de la base local (SKBG → SKCG, pista 01, toque a
    /// 1 736,4 ft, pista de 7 841 ft) y la identidad real del mantenedor (PMDG 777-200LR con el
    /// simulador publicando la familia `B777`).
    ///
    /// Los rótulos se comparan con `L._` y no con literales: los dos idiomas tienen que poder pasar
    /// esta prueba sin tocarla.
    /// </summary>
    [TestClass]
    public class LandingHeaderTests
    {
        /// <summary>El vuelo 41 de la base local, con la aeronave del log del mantenedor encima.</summary>
        private static FlightRecord Pmdg777()
        {
            var r = Skcg41();
            r.AircraftTitle = "PMDG 777-200LR";
            r.AircraftModel = null;              // el PMDG resuelve la variante por el título
            r.AircraftIcao  = "B777";            // el modelo ATC de familia, lo que publica el sim
            r.LandingWeightLbs = 138400.0;       // el peso bruto en la toma, leído del simulador
            r.LandingWindDirDeg     = 340.0;
            r.LandingWindSpeedKt    = 8.0;
            r.LandingHeadwindKt     = 6.0;
            r.LandingCrosswindKt    = 4.0;
            r.LandingRunwayTrueDeg  = 5.0;
            return r;
        }

        private static FlightRecord Skcg41() => new FlightRecord
        {
            Id              = 41,
            FlightNumber    = "9821",
            Origin          = "SKBG",
            Destination     = "SKCG",
            RunwayName      = "01",
            FlightDate      = new DateTime(2026, 10, 7, 3, 9, 22, DateTimeKind.Utc),
            LandingRateFpm  = -223,
            GForce          = 1.48,
            TouchdownDistFt = 1736.398,
            CenterlineDevFt = 5.27,
            Score           = 82,
            RunwayLengthFt  = 7841.0,
        };

        private static FlareChartLayout Layout(IList<FlareTrackPoint> samples, string family)
            => FlareChartLayout.Build(samples, 7841.0, true, family);

        private static LandingHeaderField Field(IList<LandingHeaderField> fields, string key)
            => fields.FirstOrDefault(f => f.Key == key);

        /// <summary>Un vuelo con la identidad que se le diga, sin tocar el resto del caso real.</summary>
        private static FlightRecord ConAddon(string icao, string title)
        {
            var r = Skcg41();
            r.AircraftIcao  = icao;
            r.AircraftTitle = title;
            r.AircraftModel = null;
            return r;
        }

        /// <summary>
        /// **La identidad que el log de inicio escribe para ese mismo vuelo**, sin el adorno `✈️ `. El
        /// test la compone con la función real —`AircraftIdentity.LogLine`, la que ejecuta
        /// `MainViewModel`— en vez de con un literal: así se comparan de verdad las dos salidas.
        /// </summary>
        private static string LoggedIdentity(FlightRecord record)
        {
            string line = AircraftIdentity.LogLine(record.AircraftModel, record.AircraftTitle,
                                                   record.AircraftIcao);
            StringAssert.StartsWith(line, AircraftIdentity.LogPrefix, "el log lleva el avión delante");
            return line.Substring(AircraftIdentity.LogPrefix.Length);
        }

        // ── El bloque completo ────────────────────────────────────────────────────

        /// <summary>
        /// **Con todos los datos, el bloque los enseña todos y en orden de lectura**: quién voló, con
        /// qué, cuánto pesaba, cuándo y dónde, cómo fue la toma y con qué viento.
        /// </summary>
        [TestMethod]
        public void ConTodosLosDatos_ElBloqueLosLlevaEnOrden()
        {
            var record  = Pmdg777();
            var samples = FlareChartLayoutTests.Skcg41Flare();
            var fields  = LandingHeader.Build(record, Layout(samples, record.AircraftIcao), samples);

            CollectionAssert.AreEqual(
                new[]
                {
                    LandingHeader.KeyAircraft,
                    LandingHeader.KeyManufacturer,
                    LandingHeader.KeyWeight,
                    LandingHeader.KeyFlight,
                    LandingHeader.KeyTouchdown,
                    LandingHeader.KeyWind,
                },
                fields.Select(f => f.Key).ToList(),
                "el orden del bloque es el de lectura");

            // La aeronave: **la identidad del log de inicio** —el designador que manda, que es la
            // variante resuelta del título (`B77L`), y el addon entre corchetes— y, aparte, la familia
            // del modelo ATC, que no coincide y por eso se dice marcada como tal.
            var aircraft = Field(fields, LandingHeader.KeyAircraft).Value;
            Assert.AreEqual(
                LoggedIdentity(record) + LandingHeader.Separator + L._(LandingHeader.KeyAtcFamily, "B777"),
                aircraft,
                "el bloque tiene que decir lo mismo que el log para la misma aeronave");

            // El fabricante **deducido del ICAO** y el addon **del título**, los dos y separados.
            var maker = Field(fields, LandingHeader.KeyManufacturer).Value;
            StringAssert.Contains(maker, "Boeing", maker);
            StringAssert.Contains(maker, "B77L", maker);
            StringAssert.Contains(maker, "PMDG", maker);

            // El peso, con su unidad explícita: es una lectura, no una conversión.
            var weight = Field(fields, LandingHeader.KeyWeight).Value;
            StringAssert.Contains(weight, "138,400 lb", weight);
            StringAssert.Contains(weight, L._(LandingHeader.KeyAtTouchdown), weight);

            var flight = Field(fields, LandingHeader.KeyFlight).Value;
            StringAssert.Contains(flight, "2026-10-07 03:09Z", flight);
            StringAssert.Contains(flight, "SKBG → SKCG", flight);
            StringAssert.Contains(flight, "RWY 01", flight);
            StringAssert.Contains(flight, "7,841 ft", flight);

            var touchdown = Field(fields, LandingHeader.KeyTouchdown).Value;
            StringAssert.Contains(touchdown, "-223 fpm", touchdown);
            StringAssert.Contains(touchdown, L._(LandingHeader.KeyFromThreshold, "1,736"), touchdown);

            Assert.IsTrue(Field(fields, LandingHeader.KeyWind).Value.StartsWith("340/08"),
                Field(fields, LandingHeader.KeyWind).Value);
            StringAssert.Contains(Field(fields, LandingHeader.KeyWind).Value, "HW 6",
                "las componentes son las que se guardaron con el vuelo");
        }

        /// <summary>
        /// **La versión del cliente va en la cabecera**: es lo que permite saber con qué se generó la
        /// imagen que se comparte.
        /// </summary>
        [TestMethod]
        public void LaCabeceraLlevaLaVersionDelCliente()
        {
            string caption = LandingHeader.ClientCaption("40 samples");
            StringAssert.Contains(caption, vmsOpenAcars.Core.Helpers.AppInfo.Version);
            StringAssert.Contains(caption, "40 samples");
        }

        // ── Los huecos: sin dato no hay línea ─────────────────────────────────────

        /// <summary>
        /// **Un vuelo sin título, sin modelo, sin peso y sin viento no inventa esas líneas.** El
        /// bloque se queda con lo que sí sabe —el vuelo y la toma— y el fabricante sale, si acaso, del
        /// designador ICAO, que es lo único que hay.
        /// </summary>
        [TestMethod]
        public void SinAeronaveNiPesoNiViento_LosCamposSeOmiten()
        {
            var record = Skcg41();
            var fields = LandingHeader.Build(record, Layout(null, null), null);

            Assert.IsNull(Field(fields, LandingHeader.KeyAircraft),
                "sin título, sin modelo y sin ICAO no hay línea de aeronave");
            Assert.IsNull(Field(fields, LandingHeader.KeyWeight), "sin lectura no hay LTOW");
            Assert.IsNull(Field(fields, LandingHeader.KeyWind), "sin viento no hay línea de viento");
            Assert.IsNull(Field(fields, LandingHeader.KeyFlaps), "sin traza no hay flaps");
            Assert.IsNull(Field(fields, LandingHeader.KeyPower), "sin traza no hay potencia");

            // Lo que sí hay: el vuelo y la toma (con el fpm real y la distancia al umbral).
            Assert.IsNotNull(Field(fields, LandingHeader.KeyFlight));
            var touchdown = Field(fields, LandingHeader.KeyTouchdown);
            Assert.IsNotNull(touchdown);
            StringAssert.Contains(touchdown.Value, "-223 fpm", touchdown.Value);
            StringAssert.Contains(touchdown.Value, L._(LandingHeader.KeyFromThreshold, "1,736"));
        }

        /// <summary>
        /// **Sin captura del toque no se publica el centinela.** `LandingRateFpm` vale
        /// `ScoringService.NoLandingData` (-1) cuando no hubo touchdown: un `-1 fpm` en la cabecera
        /// sería un aterrizaje, y no lo hubo.
        /// </summary>
        [TestMethod]
        public void SinToqueCapturado_NoSePublicaUnMenosUno()
        {
            var record = Skcg41();
            record.LandingRateFpm  = ScoringService.NoLandingData;
            record.TouchdownDistFt = 0.0;
            record.CenterlineDevFt = 0.0;

            var fields = LandingHeader.Build(record, Layout(null, null), null);
            Assert.IsNull(Field(fields, LandingHeader.KeyTouchdown),
                "sin toque capturado no queda nada que decir de la toma");
        }

        /// <summary>Un peso de 0 es «no lo sé», no un avión sin peso: la línea se omite.</summary>
        [TestMethod]
        public void UnPesoNoPositivo_NoSePublica()
        {
            var record = Skcg41();
            record.LandingWeightLbs = 0.0;
            Assert.IsNull(Field(LandingHeader.Build(record, Layout(null, null), null),
                                LandingHeader.KeyWeight));

            record.LandingWeightLbs = double.NaN;
            Assert.IsNull(Field(LandingHeader.Build(record, Layout(null, null), null),
                                LandingHeader.KeyWeight));
        }

        /// <summary>
        /// **La familia desconocida no se traduce a un fabricante**: el bloque se queda sin la línea
        /// de fabricante en vez de adivinar.
        /// </summary>
        [TestMethod]
        public void UnaFamiliaDesconocida_NoDaFabricante()
        {
            var record = Skcg41();
            record.AircraftIcao = "BE20";       // un King Air: no está en la tabla
            var fields = LandingHeader.Build(record, Layout(null, null), null);

            Assert.IsNotNull(Field(fields, LandingHeader.KeyAircraft), "el designador sí se enseña");
            Assert.IsNull(Field(fields, LandingHeader.KeyManufacturer),
                "sin fabricante deducible y sin addon en el título, la línea no existe");
        }

        // ── La identidad: el log y el gráfico dicen lo mismo ──────────────────────

        /// <summary>
        /// **Los dos casos reales del corpus.** Los PIREPs que el mantenedor subió a la web llevan
        /// estas dos líneas de log (con los **dos** espacios de la plantilla del log,
        /// `✈️ {tipo}  [{addon}]`):
        ///
        ///     2026-10-08 14:35:43   ✈️ A319  [ToLiss]
        ///     2026-10-07 01:57:50   ✈️ B38M  [iFly]
        ///
        /// El **título exacto no viaja en el log** (solo el tipo y el addon), así que el título se
        /// **reconstruye** con la forma real de esos addons —el desarrollador delante del modelo,
        /// «ToLiss A319» e «iFly 737 MAX 8»—; lo que sí es dato del PIREP es el **tipo que publicó el
        /// simulador**, `A319` y `B38M`, y es el que se usa aquí. Lo que se mide: que el bloque del
        /// gráfico enseña la variante y el addon entre corchetes y que el fabricante sale del
        /// designador (`A319` → Airbus, `B38M` → Boeing, que empieza por `B3`, el prefijo de los MAX).
        /// </summary>
        [TestMethod]
        public void LosDosCasosReales_ElBloqueDiceLoMismoQueElLog()
        {
            var toliss = ConAddon("A319", "ToLiss A319");
            var ifly   = ConAddon("B38M", "iFly 737 MAX 8");

            // El log dice, literalmente, lo que dice el PIREP.
            Assert.AreEqual("✈️ A319  [ToLiss]",
                AircraftIdentity.LogLine(toliss.AircraftModel, toliss.AircraftTitle, toliss.AircraftIcao));
            Assert.AreEqual("✈️ B38M  [iFly]",
                AircraftIdentity.LogLine(ifly.AircraftModel, ifly.AircraftTitle, ifly.AircraftIcao));

            // Y el bloque del gráfico, exactamente la misma identidad. `A319`/`B38M` son la familia
            // que publica el ATC **y** la variante, así que no hay segunda parte que repetir.
            var tolissFields = LandingHeader.Build(toliss, Layout(null, null), null);
            Assert.AreEqual("A319  [ToLiss]", Field(tolissFields, LandingHeader.KeyAircraft).Value);
            Assert.AreEqual(LoggedIdentity(toliss),
                            Field(tolissFields, LandingHeader.KeyAircraft).Value,
                            "el bloque no puede enseñar otra identidad que el log");

            var tolissMaker = Field(tolissFields, LandingHeader.KeyManufacturer).Value;
            StringAssert.Contains(tolissMaker, "Airbus", tolissMaker);
            StringAssert.Contains(tolissMaker, "A319",   tolissMaker);
            StringAssert.Contains(tolissMaker, "ToLiss", tolissMaker);

            var iflyFields = LandingHeader.Build(ifly, Layout(null, null), null);
            Assert.AreEqual("B38M  [iFly]", Field(iflyFields, LandingHeader.KeyAircraft).Value);
            Assert.AreEqual(LoggedIdentity(ifly),
                            Field(iflyFields, LandingHeader.KeyAircraft).Value,
                            "el bloque no puede enseñar otra identidad que el log");

            var iflyMaker = Field(iflyFields, LandingHeader.KeyManufacturer).Value;
            StringAssert.Contains(iflyMaker, "Boeing", iflyMaker);
            StringAssert.Contains(iflyMaker, "B38M",   iflyMaker);
            StringAssert.Contains(iflyMaker, "iFly",   iflyMaker);
        }

        /// <summary>
        /// **Coherencia, con las tres formas de identidad que se dan de verdad**: el simulador publica
        /// la familia y el título trae la variante (PMDG 777-200LR → `B77L`), el simulador publica ya
        /// la variante (ToLiss A319, iFly 737 MAX 8) y el título no nombra addon alguno (737-800). En
        /// las tres, **lo que enseña el bloque empieza por lo que escribe el log** —el bloque añade
        /// solo la familia del ATC cuando no coincide— y **sin addon no se pintan corchetes vacíos**.
        /// </summary>
        [TestMethod]
        public void LaIdentidadDelBloqueYLaDelLog_Coinciden()
        {
            var records = new[]
            {
                Pmdg777(),                                   // familia B777 + variante B77L del título
                ConAddon("A319", "ToLiss A319"),             // variante y familia, las dos A319
                ConAddon("B38M", "iFly 737 MAX 8"),          // idem con el MAX 8
                ConAddon("B738", "737-800"),                 // título sin addon
            };

            foreach (var record in records)
            {
                string block  = Field(LandingHeader.Build(record, Layout(null, null), null),
                                      LandingHeader.KeyAircraft).Value;
                string logged = LoggedIdentity(record);

                StringAssert.StartsWith(block, logged,
                    $"el bloque («{block}») tiene que decir lo mismo que el log («{logged}»)");
                Assert.IsFalse(block.Contains("[]"),
                    $"sin addon en el título no se pintan corchetes vacíos: «{block}»");
                Assert.IsFalse(logged.Contains("[]"),
                    $"ni el log: «{logged}»");
            }

            // El título que no nombra addon ni siquiera abre el corchete.
            Assert.AreEqual("✈️ B77L", AircraftIdentity.LogLine(null, "777-200LR", "B777"));
        }

        // ── Los flaps y la potencia ───────────────────────────────────────────────

        /// <summary>
        /// **Los flaps y el corte de potencia salen de la traza fina**, con los mismos helpers que los
        /// publican en el logbook: la familia del vuelo (`B737`) permite etiquetar la compuerta.
        /// </summary>
        [TestMethod]
        public void FlapsYPotencia_SalenDeLaTraza()
        {
            // La traza fina **real** del vuelo 41 —la misma que usa la prueba de flaps y potencia de la
            // ventana del flare—, que es la que trae el eje temporal completo (cruce del umbral y
            // contacto) que `PowerCut` necesita para decir «cuándo».
            var samples = ThresholdToTouchdownTests.Skcg41FineTrack();
            foreach (var s in samples)
            {
                s.FlapsPct = 82.0;
                s.Eng1Pct  = s.SeqNo <= 28 ? 58.0 : 30.0;
                s.Eng2Pct  = s.SeqNo <= 28 ? 57.0 : 29.0;
            }

            var record = Skcg41();
            record.AircraftIcao = "B737";

            var fields = LandingHeader.Build(record, Layout(samples, "B737"), samples);
            var flaps  = Field(fields, LandingHeader.KeyFlaps);
            var power  = Field(fields, LandingHeader.KeyPower);

            Assert.IsNotNull(flaps, "con flaps en la traza y familia conocida, se etiqueta la compuerta");
            StringAssert.Contains(flaps.Value, "FLAPS 30", flaps.Value);

            // El corte lo publica el **mismo helper** que la línea del logbook: aquí solo se comprueba
            // que el bloque dice lo mismo que `PowerCut`, no que se invente un número.
            var cut = PowerCut.Compute(samples);
            Assert.IsTrue(cut.HasValue, "el caso corta la potencia en la traza");
            Assert.IsNotNull(power, "el corte de potencia también va en la cabecera");
            Assert.AreEqual(
                L._(cut.EngineCount == 2 ? "Landing_PowerCut" : "Landing_PowerCutEngine1",
                    cut.SecondsBeforeTouchdown),
                power.Value);
        }

        // ── Las velocidades del toque ─────────────────────────────────────────────

        /// <summary>
        /// **La GS y la IAS del toque son las de la primera muestra en tierra**, que es el contacto —
        /// no las de la última, que ya son las del rodaje—. Sin ninguna muestra en tierra, `null` en
        /// las dos.
        /// </summary>
        [TestMethod]
        public void LasVelocidadesDelToque_SonLasDelContacto()
        {
            var samples = new List<FlareTrackPoint>
            {
                new FlareTrackPoint { SeqNo = 0, DistFt = 200.0, IasKt = 148.0, GsKt = 152.0 },
                new FlareTrackPoint { SeqNo = 1, DistFt =  50.0, IasKt = 146.0, GsKt = 150.0 },
                // El contacto: aquí se cogen.
                new FlareTrackPoint { SeqNo = 2, DistFt = -10.0, IasKt = 141.0, GsKt = 144.0, OnGround = true },
                // Ya rodando: NO se cogen.
                new FlareTrackPoint { SeqNo = 3, DistFt = -400.0, IasKt = 90.0, GsKt = 95.0, OnGround = true },
            };

            double? gs, ias;
            LandingHeader.TouchdownSpeeds(samples, out gs, out ias);

            Assert.AreEqual(144.0, gs.Value, 1e-9, "la GS es la del contacto, no la de la frenada");
            Assert.AreEqual(141.0, ias.Value, 1e-9, "y la IAS también");

            LandingHeader.TouchdownSpeeds(new List<FlareTrackPoint>
            {
                new FlareTrackPoint { SeqNo = 0, DistFt = 200.0, IasKt = 148.0, GsKt = 152.0 },
            }, out gs, out ias);
            Assert.IsNull(gs, "sin muestra en tierra no se publica una velocidad de toque");
            Assert.IsNull(ias);

            LandingHeader.TouchdownSpeeds(null, out gs, out ias);
            Assert.IsNull(gs);
            Assert.IsNull(ias);
        }

        // ── El render ─────────────────────────────────────────────────────────────

        /// <summary>
        /// **El bloque se renderiza alineado y con una línea por campo**: es lo que se lee en la
        /// franja del formulario y lo que hace que el dato no parezca una sopa.
        /// </summary>
        [TestMethod]
        public void ElBloqueRenderizado_VaAlineadoYUnalineaPorCampo()
        {
            var record  = Pmdg777();
            var samples = FlareChartLayoutTests.Skcg41Flare();
            var fields  = LandingHeader.Build(record, Layout(samples, record.AircraftIcao), samples);

            string text = LandingHeader.Render(fields);
            var lines = text.Split('\n');

            Assert.AreEqual(fields.Count, lines.Length, "una línea por campo, sin líneas vacías");
            Assert.IsTrue(lines.All(l => l.Trim().Length > 0));

            // Todas las líneas empiezan su valor en la misma columna: el rótulo más largo manda.
            int widest = fields.Max(f => L._(f.Key).Length);
            foreach (var line in lines)
                Assert.AreEqual(' ', line[widest], $"«{line}» no está alineado en la columna {widest}");

            Assert.AreEqual("", LandingHeader.Render(null));
        }

        /// <summary>
        /// **La identidad compacta del título del gráfico**: es la parte del bloque que **sí viaja en
        /// el PNG**, así que tiene que caber en un renglón y llevar avión, fecha y peso.
        /// </summary>
        [TestMethod]
        public void LaIdentidadCompacta_LlevaLoQueViajaEnElPng()
        {
            string id = LandingHeader.CompactIdentity(Pmdg777());

            StringAssert.Contains(id, "9821", id);
            StringAssert.Contains(id, "SKBG → SKCG", id);
            StringAssert.Contains(id, "RWY 01", id);
            StringAssert.Contains(id, "2026-10-07 03:09Z", id);
            StringAssert.Contains(id, "B77L", id);
            StringAssert.Contains(id, "Boeing", id);
            StringAssert.Contains(id, "LTOW 138,400 lb", id);
            Assert.IsTrue(id.IndexOf('\n') < 0, "tiene que caber en un renglón");

            Assert.AreEqual("", LandingHeader.CompactIdentity(null));
        }
    }
}
