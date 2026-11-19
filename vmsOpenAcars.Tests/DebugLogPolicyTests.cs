using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El log visible se limpia de volcados, pero el PIREP sigue recibiendo todo.**
    ///
    /// Las líneas de estos tests son **literales de PIREP reales** (el `ez9OqzRnYxbnGLR8`,
    /// SKBO→SKBG 14L, B738 PMDG): el `🛠` del panel, el chatter del `↳ TWY …, Next …` que salía
    /// decenas de veces por vuelo, los diagnósticos de la reversa y el volcado de ralentí de los
    /// motores. Se ocultan **solo de la pantalla** con `debug_log` apagado (que es como nace).
    ///
    /// Lo que no puede ocultarse nunca: cualquier línea que cuente un evento o una penalización.
    /// La regla de oro —si el piloto no puede actuar sobre ella y no describe un evento, es
    /// depuración— está escrita en `Helpers/DebugLogPolicy.cs`; aquí se comprueba con el log
    /// completo del vuelo, no solo con las tres líneas de ejemplo.
    ///
    /// El filtro no toca `AcarsLogMirror`: eso es lo que permite diagnosticar un addon después
    /// (el volcado del panel del PMDG sirvió para encontrar los fallos de la 0.9.74), y por eso
    /// `UIService.AddLog` mantiene el espejo y las notificaciones aunque no pinte la línea.
    /// </summary>
    [TestClass]
    public class DebugLogPolicyTests
    {
        /// <summary>El volcado del panel, tal cual salió en `ez9OqzRnYxbnGLR8`.</summary>
        private const string PanelPmdg =
            "🛠 Enroute: PMDG: powered=1 cmd=10 cws=00 ap=00 fd=10 lnav=1 vnav=1 flch=0 hdg=0 alt=0 " +
            "vs=0 loc=0 app=0 at=1 -> LNAV+VNAV hdgBug=0° · CAB cint=OFF nofum=AUTO publicado=si (SDK) " +
            "· ABRK OFF act=0 publicado=si";

        [TestMethod]
        public void LosTresVolcadosDelVueloReal_SeOcultanConElAjusteApagado()
        {
            string[] dumps =
            {
                PanelPmdg,
                "↳ TWY A, Next B4",
                "Reversa: monitoreando despliegue post-aterrizaje (offset 0x207C/0x217C)",
            };

            foreach (string line in dumps)
            {
                Assert.IsTrue(DebugLogPolicy.IsDebug(line),
                    $"«{line}» es un volcado de depuración y debería clasificarse como tal");
                Assert.IsFalse(DebugLogPolicy.ShouldDisplay(false, line),
                    $"«{line}» no debe pintarse con `debug_log` apagado");
            }
        }

        [TestMethod]
        public void ConElAjusteEncendido_SeVenLosMismosVolcados()
        {
            string[] dumps =
            {
                PanelPmdg,
                "↳ TWY A, Next B4",
                "Reversa: monitoreando despliegue post-aterrizaje (offset 0x207C/0x217C)",
            };

            foreach (string line in dumps)
                Assert.IsTrue(DebugLogPolicy.ShouldDisplay(true, line),
                    $"«{line}» debe poder verse cuando el piloto marca la casilla");
        }

        [TestMethod]
        public void LaListaCerradaDeVolcados_SeReconoceEnLosDosIdiomasYEnSusVariantes()
        {
            // Las variantes que el código puede escribir de verdad: la calle en español, la reversa
            // que no se detectó, el máximo del rodaje (el 100 %/100 % falso de ese mismo vuelo) y
            // el volcado de ralentí de los motores.
            string[] dumps =
            {
                "↳ CALLE A2, Próximo a A1",
                "Reversa: no detectada (offset 0x207C sin datos — addon puede no soportarlo)",
                "Reversa: máximo del rodaje  ENG1 100.0%  ENG2 100.0%  — cool-down 180s requeridos",
                "Motor: ENG1 idle 291s [STAB ✓]  ENG2 idle 942s [STAB ✓]  OAT 16°C",
                "📊 PIREP Status: TXI",
            };

            foreach (string line in dumps)
            {
                Assert.IsTrue(DebugLogPolicy.IsDebug(line),
                    $"«{line}» es un volcado de estado sin acción para el piloto");
                Assert.IsFalse(DebugLogPolicy.ShouldDisplay(false, line));
            }
        }

        [TestMethod]
        public void LoQueLlevaEventoOPenalizacion_NoSeOcultaNunca()
        {
            // Las ocho líneas que el mantenedor marcó como intocables, literales del log real.
            string[] eventos =
            {
                "⚠️ PENALTY: LANDING lights OFF below 10,000 ft",
                "── ON BLOCK ──",
                "🛬 Landing recorded: -236 fpm, 1.72 G, Heading: 340°, Pitch: 4.5°, Bank: -3.6°  " +
                "·  THR-TD 7 s  ·  FLAPS 25 (75%)  ·  PWR CUT 5.9 s before touchdown",
                "⛽ Taxi Out fuel: 271 kg",
                "🎙️ RAAS: HOLD SHORT RUNWAY 14L",
                "✅ Vuelo reportado, listo para siguiente vuelo",
                "🎯 FLARE TRACK START: RWY 35 | Dist 1479 ft · 10 Hz",
                "🅿️ Parking Brake: SET",
            };

            foreach (string line in eventos)
            {
                Assert.IsFalse(DebugLogPolicy.IsDebug(line),
                    $"«{line}» cuenta un evento o una penalización: no puede clasificarse como depuración");
                Assert.IsTrue(DebugLogPolicy.ShouldDisplay(false, line),
                    $"«{line}» tiene que seguir viéndose con `debug_log` apagado");
                Assert.IsTrue(DebugLogPolicy.ShouldDisplay(true, line));
            }
        }

        [TestMethod]
        public void ElLogCompletoDelVueloReal_ConservaLosEventosYSeLlevaLosVolcados()
        {
            // El log del PIREP `ez9OqzRnYxbnGLR8`, en orden, sin omitir líneas.
            string[] log =
            {
                PanelPmdg,
                "↳ TWY A, Next B4",
                "↳ TWY A2, Next A1",
                "Reversa: monitoreando despliegue post-aterrizaje (offset 0x207C/0x217C)",
                "Reversa: no detectada (offset 0x207C sin datos — addon puede no soportarlo)",
                "Reversa: desplegada  ENG1 11% ENG2 2%",
                "Motor: ENG1 idle 291s [STAB ✓]  ENG2 idle 942s [STAB ✓]  OAT 16°C",
                "📊 PIREP Status: TXI",
                "⛽ Taxi Out fuel: 271 kg",
                "⚠️ LANDING lights OFF below 10,000 ft (8409 ft)",
                "── ON BLOCK ──",
                "🛬 Landing recorded: -236 fpm, 1.72 G, Heading: 340°, Pitch: 4.5°, Bank: -3.6°  " +
                "·  THR-TD 7 s  ·  FLAPS 25 (75%)  ·  PWR CUT 5.9 s before touchdown",
                "🎙️ RAAS: HOLD SHORT RUNWAY 14L",
                "✅ Vuelo reportado, listo para siguiente vuelo",
            };

            int ocultas = 0;
            foreach (string line in log)
            {
                if (DebugLogPolicy.IsDebug(line)) ocultas++;
                else Assert.IsTrue(DebugLogPolicy.ShouldDisplay(false, line),
                    $"«{line}» lleva evento y tiene que seguir apareciendo");
            }

            // Los siete volcados del vuelo: el panel (1), el chatter del Next (2 y 3), el aviso de
            // que se vigila la reversa y el de «no detectada» (4 y 5), el ralentí (6) y el estado (7).
            Assert.AreEqual(7, ocultas,
                "el filtro tiene que llevarse exactamente los volcados del vuelo, ni uno más");

            // Y lo que el piloto sí tiene que leer, línea por línea.
            string[] obligatorias =
            {
                "Reversa: desplegada  ENG1 11% ENG2 2%",
                "⛽ Taxi Out fuel: 271 kg",
                "⚠️ LANDING lights OFF below 10,000 ft (8409 ft)",
                "── ON BLOCK ──",
                "🛬 Landing recorded: -236 fpm, 1.72 G, Heading: 340°, Pitch: 4.5°, Bank: -3.6°  " +
                "·  THR-TD 7 s  ·  FLAPS 25 (75%)  ·  PWR CUT 5.9 s before touchdown",
                "🎙️ RAAS: HOLD SHORT RUNWAY 14L",
                "✅ Vuelo reportado, listo para siguiente vuelo",
            };
            foreach (string line in obligatorias)
                Assert.IsTrue(DebugLogPolicy.ShouldDisplay(false, line),
                    $"«{line}» es un evento del vuelo y no puede desaparecer del log visible");
        }

        [TestMethod]
        public void SinTextoNoSeOcultaNadaPorAccidente()
        {
            Assert.IsTrue(DebugLogPolicy.ShouldDisplay(false, null));
            Assert.IsTrue(DebugLogPolicy.ShouldDisplay(false, ""));
            Assert.IsTrue(DebugLogPolicy.ShouldDisplay(false, "   "));
            Assert.IsFalse(DebugLogPolicy.IsDebug(null));
            Assert.IsFalse(DebugLogPolicy.IsDebug("   "));

            // Una línea de puntuación (un separador) tampoco es depuración.
            Assert.IsTrue(DebugLogPolicy.ShouldDisplay(false, "────────────────"));
            Assert.IsFalse(DebugLogPolicy.IsDebug("────────────────"));
        }

        [TestMethod]
        public void LaCopiaDelPirepLlevaLaBandera_YLaDePantallaNo()
        {
            string line = "↳ TWY A, Next B4";

            // La copia que viaja a phpVMS (PIREP y tabla `acars`) lleva la bandera estable.
            string pirep = AcarsLogMirror.MessageForPirep(line);
            StringAssert.StartsWith(pirep, DebugLogPolicy.DebugPrefix,
                "el volcado tiene que poder filtrarse luego en el backend");
            Assert.AreEqual(DebugLogPolicy.DebugPrefix + line, pirep,
                "la bandera va delante y no se toca el texto de la línea");

            // La copia de pantalla es la línea original, sin bandera: con el ajuste encendido el
            // piloto la lee limpia.
            Assert.IsFalse(line.StartsWith(DebugLogPolicy.DebugPrefix, System.StringComparison.Ordinal));
            Assert.IsTrue(DebugLogPolicy.ShouldDisplay(true, line));
        }

        [TestMethod]
        public void UnaLineaNormalNoSePrefijaNunca()
        {
            string[] normales =
            {
                "── ON BLOCK ──",
                "⛽ Taxi Out fuel: 271 kg",
                "⚠️ LANDING lights OFF below 10,000 ft (8409 ft)",
            };

            bool before = AppConfig.DebugLogEnabled;
            try
            {
                foreach (bool ajuste in new[] { false, true })
                {
                    AppConfig.DebugLogEnabled = ajuste;
                    foreach (string line in normales)
                        Assert.AreEqual(line, AcarsLogMirror.MessageForPirep(line),
                            $"«{line}» no es depuración: no puede llevar la bandera (ajuste {ajuste})");
                }
            }
            finally
            {
                AppConfig.DebugLogEnabled = before;
            }
        }

        [TestMethod]
        public void LaBanderaNoSeDuplica()
        {
            string line = PanelPmdg;

            string conBandera = AcarsLogMirror.MessageForPirep(line);
            Assert.AreEqual(conBandera, AcarsLogMirror.MessageForPirep(conBandera),
                "pasar dos veces por el espejo no puede acumular `[DBG] [DBG] `");
            Assert.AreEqual(conBandera, DebugLogPolicy.ForPirep(conBandera));

            // Y la clasificación sigue reconociéndola detrás de la bandera.
            Assert.IsTrue(DebugLogPolicy.IsDebug(conBandera),
                "una línea ya prefijada se sigue clasificando como depuración");
            Assert.IsFalse(DebugLogPolicy.ShouldDisplay(false, conBandera));
        }

        [TestMethod]
        public void ConElAjusteApagado_SeEspejaConBanderaYNoSePintaEnPantalla()
        {
            string line = "Motor: ENG1 idle 291s [STAB ✓]  ENG2 idle 942s [STAB ✓]  OAT 16°C";

            // El comportamiento que pidió el mantenedor: phpVMS recibe todo, la pantalla se limpia.
            Assert.IsFalse(DebugLogPolicy.ShouldDisplay(false, line),
                "con `debug_log` apagado no se pinta");
            Assert.IsTrue(AcarsLogMirror.ShouldMirror(line),
                "el espejo al PIREP no se filtra: ninguna línea se pierde");
            StringAssert.StartsWith(AcarsLogMirror.MessageForPirep(line), DebugLogPolicy.DebugPrefix);
        }

        [TestMethod]
        public void ElAjusteSeLeeEnCaliente_SinReiniciar()
        {
            bool before = AppConfig.DebugLogEnabled;
            try
            {
                AppConfig.DebugLogEnabled = true;
                Assert.IsTrue(AppConfig.DebugLogEnabled,
                    "la casilla tiene que aplicar al momento, sin reiniciar la aplicación");

                AppConfig.DebugLogEnabled = false;
                Assert.IsFalse(AppConfig.DebugLogEnabled);
            }
            finally
            {
                AppConfig.DebugLogEnabled = before;
            }
        }

        [TestMethod]
        public void SinLaClaveEnElConfig_ElAjusteNaceApagado()
        {
            // `debug_log` **no** se escribe en `App.config` ni en `App.Release.config` a propósito:
            // el `.config` de un piloto ya instalado no la trae y el log tiene que nacer limpio.
            if (ConfigurationManager.AppSettings["debug_log"] != null)
                Assert.Inconclusive("este equipo tiene `debug_log` en App.config: la ausencia no se puede comprobar");

            Assert.IsFalse(AppConfig.DebugLogEnabled,
                "sin `debug_log` en el `.config`, el ajuste tiene que valer `false` (ausente = apagado)");
        }
    }
}
