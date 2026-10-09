using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **Quién fabricó el avión y quién hizo el addon**, que son dos preguntas con dos fuentes
    /// distintas y por eso se prueban por separado:
    ///
    /// 1. El **fabricante** se deduce del **designador ICAO** que publica el simulador, y solo si el
    ///    designador está en la tabla: una letra suelta no identifica a nadie (`BE20` es un
    ///    Beechcraft y empieza por la misma letra que un Boeing, `B738`). Lo que no está, **no se
    ///    traduce**: se devuelve `null` en vez de adivinar.
    /// 2. El **addon** solo se puede leer del **título**. El ACARS no escanea la carpeta de community
    ///    (decisión del mantenedor, v0.9.16) y no hay offset de FSUIPC que lo publique, así que un
    ///    título que solo dice el modelo devuelve `null` y la cabecera del gráfico no enseña la línea.
    ///
    /// Los designadores son los reales del corpus y del log del mantenedor (`B77L` del PMDG 777-200LR,
    /// `B738` del 737-800, `A320`/`A20N` de Airbus, `AT76`, `DH8D` del Q400…).
    /// </summary>
    [TestClass]
    public class AircraftIdentityTests
    {
        // ── El fabricante, desde el designador ICAO ───────────────────────────────

        /// <summary>
        /// **Variantes reales** (las que resuelve `AircraftTypeMatch`): cada familia cae en su
        /// fabricante. Son los designadores que el cliente se encuentra de verdad, no inventados.
        /// </summary>
        [TestMethod]
        public void ElFabricanteSaleDelDesignadorDeVariante()
        {
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B77L"),
                "el PMDG 777-200LR del mantenedor reporta B77L");
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B77W"));
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B738"));
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B739"));
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B744"));
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B789"));
            // Los MAX: B37M/B38M/B39M/B3XM empiezan por B3, no por B7.
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B38M"));

            Assert.AreEqual("Airbus", AircraftIdentity.ManufacturerFromIcao("A320"));
            Assert.AreEqual("Airbus", AircraftIdentity.ManufacturerFromIcao("A20N"));
            Assert.AreEqual("Airbus", AircraftIdentity.ManufacturerFromIcao("A359"));
            Assert.AreEqual("Airbus", AircraftIdentity.ManufacturerFromIcao("A388"));

            Assert.AreEqual("ATR", AircraftIdentity.ManufacturerFromIcao("AT76"));
            Assert.AreEqual("De Havilland", AircraftIdentity.ManufacturerFromIcao("DH8D"),
                "el Q400, que el cliente ya reconoce como DH8D");
            Assert.AreEqual("Embraer", AircraftIdentity.ManufacturerFromIcao("E190"));
            Assert.AreEqual("Bombardier", AircraftIdentity.ManufacturerFromIcao("CRJ9"));
            Assert.AreEqual("McDonnell Douglas", AircraftIdentity.ManufacturerFromIcao("MD11"));
            Assert.AreEqual("Airbus", AircraftIdentity.ManufacturerFromIcao("BCS3"),
                "el A220 es el BCS de la CSeries y hoy se vende como Airbus");
        }

        /// <summary>
        /// **El modelo ATC de familia también sirve**: el simulador solo publica `B777`/`B737` cuando
        /// no se puede resolver la variante, y la familia empieza por el mismo prefijo.
        /// </summary>
        [TestMethod]
        public void LaFamiliaDelModeloAtc_TambienDaElFabricante()
        {
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B777"));
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B737"));
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao("B747"));
            Assert.AreEqual("Airbus", AircraftIdentity.ManufacturerFromIcao("A320"));
        }

        /// <summary>
        /// **Lo que no está en la tabla no se traduce.** `BE20` es un Beechcraft King Air y empieza
        /// por la misma letra que un Boeing: afirmar «Boeing» ahí sería inventar. Y `????` es el
        /// centinela de «no lo publico» de `FsuipcService`, no un avión.
        /// </summary>
        [TestMethod]
        public void UnDesignadorQueNoEstaEnLaTabla_NoDevuelveFabricante()
        {
            Assert.IsNull(AircraftIdentity.ManufacturerFromIcao("BE20"),
                "un King Air no es un Boeing aunque empiece por B");
            Assert.IsNull(AircraftIdentity.ManufacturerFromIcao("C172"),
                "un Cessna 172 no está en la tabla: mejor no decir nada");
            Assert.IsNull(AircraftIdentity.ManufacturerFromIcao("????"));
            Assert.IsNull(AircraftIdentity.ManufacturerFromIcao(""));
            Assert.IsNull(AircraftIdentity.ManufacturerFromIcao("   "));
            Assert.IsNull(AircraftIdentity.ManufacturerFromIcao(null));
        }

        /// <summary>Sin distinguir mayúsculas de minúsculas y recortando: el offset puede llegar así.</summary>
        [TestMethod]
        public void ElDesignadorSeNormaliza()
        {
            Assert.AreEqual("Boeing", AircraftIdentity.ManufacturerFromIcao(" b77l "));
            Assert.AreEqual("Airbus", AircraftIdentity.ManufacturerFromIcao("a320"));
        }

        // ── El addon, SOLO desde el título ────────────────────────────────────────

        /// <summary>
        /// Los títulos reales de la escena: el nombre del desarrollador va delante del modelo, y el
        /// detector lo devuelve. El título del mantenedor (`777-200LR`) **no** lo lleva.
        /// </summary>
        [TestMethod]
        public void ElAddonSaleDelTituloCuandoEsteLoNombra()
        {
            Assert.AreEqual("PMDG",     AircraftIdentity.AddonFromTitle("PMDG 777-200LR"));
            Assert.AreEqual("PMDG",     AircraftIdentity.AddonFromTitle("PMDG777-200LR"),
                "hay addons que pegan el nombre al modelo sin espacio");
            Assert.AreEqual("Fenix",    AircraftIdentity.AddonFromTitle("Fenix A320"));
            Assert.AreEqual("ToLiss",   AircraftIdentity.AddonFromTitle("ToLiss A321"));
            Assert.AreEqual("Asobo",    AircraftIdentity.AddonFromTitle("Asobo A320neo"));
            Assert.AreEqual("FlyByWire", AircraftIdentity.AddonFromTitle("FlyByWire A32NX"));
            Assert.AreEqual("iniBuilds", AircraftIdentity.AddonFromTitle("iniBuilds A310-300"));
            Assert.AreEqual("PMDG",     AircraftIdentity.AddonFromTitle("pmdg 737-800"));
        }

        /// <summary>
        /// **Un título que solo dice el modelo no nombra ningún addon.** Es el caso del mantenedor
        /// (`✈️ Aeronave: 777-200LR`): devolver algo ahí sería vender como dato lo que es una
        /// suposición, y el bloque del gráfico se queda sin esa línea.
        /// </summary>
        [TestMethod]
        public void UnTituloQueSoloDiceElModelo_NoDevuelveAddon()
        {
            Assert.IsNull(AircraftIdentity.AddonFromTitle("777-200LR"));
            Assert.IsNull(AircraftIdentity.AddonFromTitle("737-800"));
            Assert.IsNull(AircraftIdentity.AddonFromTitle("Airbus A320neo"));
            Assert.IsNull(AircraftIdentity.AddonFromTitle(""));
            Assert.IsNull(AircraftIdentity.AddonFromTitle(null));
        }
        // ── La identidad ya compuesta: una sola cadena para el log y el gráfico ───

        /// <summary>
        /// **La identidad es una sola y se compone en un solo sitio.** El log de inicio y la cabecera
        /// del gráfico llaman a la misma función, así que la cadena de los dos PIREPs reales del
        /// mantenedor —`✈️ A319  [ToLiss]` y `✈️ B38M  [iFly]`— es la que se prueba aquí, literal. El
        /// **título exacto no viaja en el log**, solo el tipo que publicó el simulador y el addon, así
        /// que el título se reconstruye con la forma real de esos addons (desarrollador delante del
        /// modelo); el designador sí es el del PIREP.
        /// </summary>
        [TestMethod]
        public void LaIdentidadCompuesta_EsLaDelLogDeInicio()
        {
            Assert.AreEqual("A319  [ToLiss]", AircraftIdentity.Compose("A319", "ToLiss A319"));
            Assert.AreEqual("B38M  [iFly]",   AircraftIdentity.Compose("B38M", "iFly 737 MAX 8"));

            Assert.AreEqual("✈️ A319  [ToLiss]", AircraftIdentity.LogLine(null, "ToLiss A319", "A319"));
            Assert.AreEqual("✈️ B38M  [iFly]",   AircraftIdentity.LogLine(null, "iFly 737 MAX 8", "B38M"));
        }

        /// <summary>
        /// **La variante le gana a la familia, también en el log.** En un PMDG 777-200LR el simulador
        /// publica la familia (`B777`) y el título trae la variante: el log publicaba el crudo
        /// `✈️ B777` mientras el bloque del gráfico enseñaba `B77L`. Ahora los dos resuelven con la
        /// misma función y dicen `B77L`.
        /// </summary>
        [TestMethod]
        public void LaVarianteLeGanaALaFamilia_TambienEnElLog()
        {
            Assert.AreEqual("✈️ B77L  [PMDG]", AircraftIdentity.LogLine(null, "PMDG 777-200LR", "B777"));
        }

        /// <summary>**Sin addon en el título no se abren corchetes**: un `[]` vacío no es un dato.</summary>
        [TestMethod]
        public void SinAddonEnElTitulo_NoHayCorchetes()
        {
            Assert.AreEqual("B77L", AircraftIdentity.Compose("B77L", "777-200LR"));
            Assert.AreEqual("✈️ B77L", AircraftIdentity.LogLine(null, "777-200LR", "B777"));
            Assert.AreEqual("B777", AircraftIdentity.Compose("B777", null));
            Assert.IsFalse(AircraftIdentity.LogLine(null, "777-200LR", "B777").Contains("["));
        }

        /// <summary>
        /// **Degradar sin datos.** Sin designador se cae al título, que es lo único que hay, y si no
        /// hay ni eso el log sigue escribiendo algo (`Unknown`, como antes): la identidad nunca queda
        /// en blanco.
        /// </summary>
        [TestMethod]
        public void SinDatoDeTipo_QuedaElTituloYSiNoUnknown()
        {
            Assert.AreEqual("Falcon 50", AircraftIdentity.Compose(null, "Falcon 50"));
            Assert.AreEqual("✈️ Falcon 50", AircraftIdentity.LogLine(null, "Falcon 50", "????"));
            Assert.AreEqual("✈️ Unknown", AircraftIdentity.LogLine(null, null, "????"));
            Assert.AreEqual("Unknown", AircraftIdentity.Compose(null, null));
        }
    }
}
