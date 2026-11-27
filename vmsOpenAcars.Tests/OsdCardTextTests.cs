using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Services;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Las cadenas de la tarjeta, con los dos juegos de datos reales.**
    ///
    /// El primer caso es el toque del PIREP **`YxzZxO92Qnbxlqla`** (B738 SSW, MPTO→SKCG):
    /// **−219 fpm**, **1.21 G**, **138 kt IAS**, **141 kt GS**, pitch **3.5°**, bank **0.0°**.
    /// El segundo son los números de la tarjeta de referencia que pasó el mantenedor:
    /// **−145 fpm**, **1.18 G**, **134/141 kt**, **4.2° / 0.8°**. Con los dos se fija el formato:
    /// menos tipográfico U+2212, dos decimales para el G, uno para las actitudes, punto medio
    /// como separador y hora UTC `HH:mmz`.
    ///
    /// Y la otra mitad de la regla: **sin dato la línea desaparece**, no se rellena con `0 kt`
    /// ni con `--`. El contador de toques **no se pinta**: el cliente solo conserva el último
    /// touchdown, así que un `1` fijo no informaría de nada (decisión del mantenedor).
    /// </summary>
    [TestClass]
    public class OsdCardTextTests
    {
        private string _originalLanguage;

        [TestInitialize]
        public void SetUp()
        {
            _originalLanguage = LocalizationService.Instance.CurrentLanguage;
            LocalizationService.Instance.LoadLanguage("en");
        }

        [TestCleanup]
        public void TearDown()
        {
            LocalizationService.Instance.LoadLanguage(_originalLanguage);
        }

        // ── El toque real del PIREP ──────────────────────────────────────────────

        [TestMethod]
        public void ElToqueRealDelPirep_SeFormateaComoLaTarjeta()
        {
            Assert.AreEqual("\u2212219 fpm", OsdCardText.LandingRate(-219));
            Assert.AreEqual("1.21 G",          OsdCardText.GForce(1.21));
            Assert.AreEqual("138 kt IAS · 141 kt ground",
                            OsdCardText.SpeedLine(138, 141).Text);
            Assert.AreEqual("Pitch 3.5° · Bank 0.0°",
                            OsdCardText.AttitudeLine(3.5, 0.0).Text);
            Assert.AreEqual("on the ground since 03:39z",
                            OsdCardText.TouchdownLine(
                                new DateTime(2026, 11, 24, 3, 39, 0, DateTimeKind.Utc)).Text);
        }

        /// <summary>
        /// La píldora y el registro tienen que decir **lo mismo del mismo G**. 1,21 g y 1,18 g son
        /// `Smooth`; 1,6 g es `Firm`; 1,72 g es `Hard` y 2,8 g es `Severe`. Se compara contra
        /// `GForceBands.LabelKey`, no contra literales, para que ningún cambio de umbral deje la
        /// tarjeta desincronizada.
        /// </summary>
        [DataTestMethod]
        [DataRow(1.21, "1,21 g: el toque real de YxzZxO92Qnbxlqla")]
        [DataRow(1.18, "1,18 g: el valor de la tarjeta de referencia")]
        [DataRow(1.60, "1,6 g: banda firme")]
        [DataRow(1.72, "1,72 g: el defecto de la v0.9.74")]
        [DataRow(2.80, "2,8 g: la banda superior, sin segundo umbral")]
        public void LaPildoraEsLaDeLaTablaUnicaDelG(double gForce, string because)
        {
            Assert.AreEqual(L._(GForceBands.LabelKey(gForce)),
                            OsdCardText.LandingBadge(gForce), because);
        }

        /// <summary>
        /// **La píldora no puede volver a mirar la severidad.** La firma solo admite el G: si
        /// alguien reintroduce un `OsdSeverity` en `LandingBadge`, este test lo delata.
        /// </summary>
        [TestMethod]
        public void LaPildoraNoDependeDeLaSeveridad()
        {
            MethodInfo method = typeof(OsdCardText).GetMethod(
                "LandingBadge", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "LandingBadge tiene que existir");
            ParameterInfo[] parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual(typeof(double?), parameters[0].ParameterType,
                "la píldora solo recibe el G medido, nunca la severidad");

            Assert.IsNull(OsdCardText.LandingBadge(null), "sin G no hay píldora");
            Assert.IsNull(OsdCardText.LandingBadge(0.0), "0 es ausencia de lectura");
        }

        [TestMethod]
        public void ElToqueDeLaReferencia_SeFormateaIgual()
        {
            Assert.AreEqual("\u2212145 fpm", OsdCardText.LandingRate(-145));
            Assert.AreEqual("1.18 G",       OsdCardText.GForce(1.18));
            Assert.AreEqual("134 kt IAS · 141 kt ground",
                            OsdCardText.SpeedLine(134, 141).Text);
            Assert.AreEqual("Pitch 4.2° · Bank 0.8°",
                            OsdCardText.AttitudeLine(4.2, 0.8).Text);
        }

        [TestMethod]
        public void LaLineaDeTierraNoLlevaNingunNumeroDelante()
        {
            var line = OsdCardText.TouchdownLine(
                new DateTime(2026, 11, 24, 3, 39, 0, DateTimeKind.Utc));
            Assert.AreEqual("on the ground since 03:39z", line.Text);
            Assert.IsFalse(line.Text.StartsWith("1"), "el contador de toques no se pinta");
            Assert.IsFalse(line.Text.Contains("touchdown"));
        }

        // ── Reglas de formato ───────────────────────────────────────────────────

        [TestMethod]
        public void ElMenosEsTipograficoYSoloVaSiEsNegativo()
        {
            Assert.AreEqual("\u2212219", OsdCardText.LandingRateValue(-219));
            Assert.IsFalse(OsdCardText.LandingRateValue(-219).Contains("-"),
                "el menos del teclado no es el de la tarjeta");
            Assert.AreEqual("145", OsdCardText.LandingRateValue(145));
            Assert.IsFalse(OsdCardText.LandingRateValue(145).Contains("\u2212"));
        }

        [TestMethod]
        public void ElGSePublicaConDosDecimales()
        {
            Assert.AreEqual("1.18", OsdCardText.GForceValue(1.18));
            Assert.AreEqual("1.21", OsdCardText.GForceValue(1.21));
            Assert.AreEqual("1.50", OsdCardText.GForceValue(1.5));
        }

        [TestMethod]
        public void LaHoraEsUtcYConSufijoSiempre()
        {
            Assert.AreEqual("03:39z",
                OsdCardText.TouchdownTime(new DateTime(2026, 11, 24, 3, 39, 0, DateTimeKind.Utc)));
            Assert.AreEqual("15:15z",
                OsdCardText.TouchdownTime(new DateTime(2026, 11, 24, 15, 15, 0, DateTimeKind.Utc)));
        }

        [TestMethod]
        public void ElAeropuertoYElEstadoVivoSeComponenConPuntoMedio()
        {
            Assert.AreEqual("Amsterdam Schiphol · 15:15z",
                OsdCardText.AirportLine("Amsterdam Schiphol",
                    new DateTime(2026, 11, 24, 15, 15, 0, DateTimeKind.Utc)));
            Assert.AreEqual("Rolling out · 24 kt",
                OsdCardText.LiveState(24).Text);
        }

        // ── Degradación: sin dato, la línea desaparece ───────────────────────────

        [TestMethod]
        public void SinDato_LaLineaDesaparece()
        {
            Assert.IsNull(OsdCardText.LandingRate(null));
            Assert.IsNull(OsdCardText.GForce(null));
            Assert.IsNull(OsdCardText.GForce(0.0), "0 es ausencia de lectura del acelerómetro");
            Assert.IsNull(OsdCardText.SpeedLine(null, null));
            Assert.IsNull(OsdCardText.AttitudeLine(null, null));
            Assert.IsNull(OsdCardText.TouchdownLine(null));
            Assert.AreEqual("Rolling out", OsdCardText.LiveState(null).Text);
        }

        [TestMethod]
        public void LasLineasParcialesMuestranSoloLoQueHay()
        {
            Assert.AreEqual("138 kt IAS", OsdCardText.SpeedLine(138, null).Text);
            Assert.AreEqual("141 kt ground", OsdCardText.SpeedLine(null, 141).Text);
            Assert.AreEqual("Pitch 3.5°", OsdCardText.AttitudeLine(3.5, null).Text);
            Assert.AreEqual("Bank 0.0°", OsdCardText.AttitudeLine(null, 0.0).Text);
        }

        // ── Plantilla simple: se reestiliza, no se reinterpreta ──────────────────

        [TestMethod]
        public void ElMensajeConSeparadorDobleSeparaEtiquetaYDetalle()
        {
            string label, detail;
            OsdCardText.SplitSimple("PENALTY  NAV LIGHTS  −5 PTS",
                                    OsdSeverity.Warning, out label, out detail);
            Assert.AreEqual("PENALTY", label);
            Assert.AreEqual("NAV LIGHTS  −5 PTS", detail);
        }

        [TestMethod]
        public void SinSeparadorDoble_LaEtiquetaEsLaSeveridadYElMensajeNoCambia()
        {
            string label, detail;
            OsdCardText.SplitSimple("TAXI OUT", OsdSeverity.Info, out label, out detail);
            Assert.AreEqual("INFO", label);
            Assert.AreEqual("TAXI OUT", detail);

            OsdCardText.SplitSimple("OVERSPEED  250 KTS", OsdSeverity.Critical, out label, out detail);
            Assert.AreEqual("OVERSPEED", label);
            Assert.AreEqual("250 KTS", detail);
        }

        // ── Los dos idiomas siguen simétricos ───────────────────────────────────

        [TestMethod]
        public void LosDosJsonTienenLasMismasClavesYElNuevoBloqueCompleto()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            var en = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                         File.ReadAllText(Path.Combine(folder, "en.json")));
            var es = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                         File.ReadAllText(Path.Combine(folder, "es.json")));

            Assert.AreEqual(583, en.Count, "recuento de claves de en.json");
            Assert.AreEqual(en.Count, es.Count, "es.json tiene que tener las mismas claves");
            foreach (var key in en.Keys)
                Assert.IsTrue(es.ContainsKey(key), $"falta la clave {key} en es.json");

            foreach (var key in new[]
                     {
                         "Osd_Card_Landed", "Osd_Card_LandingRate", "Osd_Card_TouchdownG",
                         "Osd_Card_UnitFpm", "Osd_Card_UnitG", "Osd_Card_KtIas",
                         "Osd_Card_KtGround", "Osd_Card_Kt", "Osd_Card_Pitch",
                         "Osd_Card_Bank", "Osd_Card_OnGroundSince", "Osd_Card_RollingOut",
                         "Osd_Card_ScoringNote", "Osd_Sev_Info",
                         "Osd_Sev_Success", "Osd_Sev_Warning", "Osd_Sev_Critical",
                     })
                Assert.IsTrue(en.ContainsKey(key) && es.ContainsKey(key),
                              $"la clave {key} tiene que estar en los dos idiomas");
        }
    }
}

