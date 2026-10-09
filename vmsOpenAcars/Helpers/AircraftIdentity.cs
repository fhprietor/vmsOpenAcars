using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Quién fabricó el avión y quién hizo el addon**, que son dos preguntas distintas y con dos
    /// fuentes distintas:
    ///
    /// 1. **El fabricante del avión se deduce del designador ICAO de tipo.** Es el dato honesto: el
    ///    designador lo publica el simulador (modelo ATC, `0x0618`, o la variante resuelta del
    ///    modelo/título) y el fabricante es una propiedad del tipo, no del addon. `B77L` → Boeing,
    ///    `A320` → Airbus, `AT76` → ATR. **Si el designador no está en la tabla, no se devuelve
    ///    nada**: una letra suelta no identifica un fabricante —`BE20` es un Beechcraft y empieza
    ///    por la misma letra que un Boeing—, y afirmar «Boeing» de un King Air sería inventar.
    ///
    /// 2. **El desarrollador del addon (PMDG, Fenix, ToLiss, Asobo…) solo se puede leer del texto
    ///    del título**, porque no hay ningún offset de FSUIPC que lo publique y el ACARS **no
    ///    escanea la carpeta de community** (decisión del mantenedor, v0.9.16: «no quiero que
    ///    escanee mi carpeta de community»). Así que solo se afirma cuando el título lo nombra, y
    ///    quien lo pinte tiene que decir que viene del título: un título que solo dice el modelo
    ///    devuelve `null` y el bloque de datos simplemente no enseña esa línea.
    ///
    /// Es función pura, sin FSUIPC ni WinForms: la decisión vive aquí y no en el formulario.
    /// </summary>
    internal static class AircraftIdentity
    {
        /// <summary>
        /// El fabricante del avión a partir del designador ICAO de tipo (`B77L`, `A320`, `AT76`), o
        /// `null` si el designador no está en la tabla. Acepta tanto el designador de **variante**
        /// (`B77L`) como el de **familia** que publica el modelo ATC (`B777`): los dos empiezan por
        /// el mismo prefijo, que es lo que decide la tabla.
        /// </summary>
        internal static string ManufacturerFromIcao(string designator)
        {
            if (string.IsNullOrWhiteSpace(designator)) return null;

            string code = designator.Trim().ToUpperInvariant();
            if (code == AircraftTypeMatch.Unknown) return null;

            // **El prefijo más largo gana**: `AT` (ATR) antes que `A` (Airbus), `BCS` (A220) antes
            // que `B` (Boeing) y `MD` antes que nada. La lista va ordenada por longitud descendente
            // al construirla, así que la primera coincidencia ya es la más específica.
            foreach (var entry in Prefixes)
                if (code.StartsWith(entry.Key, StringComparison.Ordinal)) return entry.Value;

            return null;
        }

        /// <summary>
        /// **El desarrollador del addon, solo si el título lo dice.** Devuelve el nombre canónico
        /// (`PMDG`, `Fenix`, `ToLiss`, `Asobo`…) o `null`.
        ///
        /// **Esta es la única tabla de desarrolladores del cliente.** Hasta ahora vivía dentro de
        /// `FsuipcService.GetAircraftDeveloper` (el «✈️ B738 [PMDG]» del log de inicio) y aquí vive
        /// la de la cabecera del gráfico; dos listas separadas se habrían separado en cuanto alguien
        /// añadiera un addon en una sola. `FsuipcService` delega en este helper.
        ///
        /// La búsqueda es por **trozo de texto** y no por palabra completa, que es como se comportaba
        /// el detector del log: hay títulos que pegan el nombre al modelo sin espacio (`PMDG777-200`).
        /// Ningún alias de la tabla es tan corto o tan común como para colarse en otro nombre.
        /// </summary>
        internal static string AddonFromTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            string upper = title.ToUpperInvariant();
            foreach (var entry in Developers)
                if (upper.IndexOf(entry.Key, StringComparison.Ordinal) >= 0) return entry.Value;

            return null;
        }

        /// <summary>Lo que se publica cuando no hay **ningún** dato de tipo: no se inventa un avión.</summary>
        internal const string UnknownType = "Unknown";

        /// <summary>El adorno del log de inicio delante de la identidad (no va en el gráfico).</summary>
        internal const string LogPrefix = "✈️ ";

        /// <summary>
        /// **La identidad de la aeronave del simulador, resuelta en un solo sitio** —el designador que
        /// manda y la familia del modelo ATC, por separado—. El designador es la **variante** cuando se
        /// puede resolver del modelo o del título (`A319`, `B38M`, `B77L`) y, si no, la **familia** que
        /// publica el ATC (`B777`, `B737`): es el dato más preciso que se sabe, y el mismo con el que
        /// `AircraftTypeMatch` valida el vuelo contra el OFP.
        ///
        /// `designator` y `family` valen `null` cuando el dato no existe (`????`, vacío, nulo): quien
        /// lo pinte decide entonces qué calla, en vez de rellenar el hueco.
        /// </summary>
        internal static void Resolve(string simModel, string simTitle, string simAtcModel,
                                     out string designator, out string family)
        {
            family = AircraftTypeMatch.IsKnown(simAtcModel)
                         ? simAtcModel.Trim().ToUpperInvariant() : null;

            string variant = AircraftTypeMatch.ResolveVariant(simModel, simTitle, simAtcModel);
            designator = variant.Length > 0 ? variant : family;
        }

        /// <summary>
        /// **La identidad ya compuesta, y la única cadena válida para pintarla**: el designador y, si el
        /// título nombra al addon, entre corchetes —`A319  [ToLiss]`—. Sin addon **no se pintan
        /// corchetes vacíos**; sin designador se cae al título, que es lo único que queda, y si tampoco
        /// lo hay, a `Unknown`.
        ///
        /// **Vive aquí porque el log de inicio y la cabecera del gráfico tienen que decir lo mismo.**
        /// Hasta ahora el log publicaba el crudo del modelo ATC —`B777` en un PMDG 777-200LR, que el
        /// bloque del gráfico llamaba `B77L`— y esa era justo la divergencia que se ve en los PIREPs
        /// (`✈️ A319  [ToLiss]`, `✈️ B38M  [iFly]`): una sola función, y no dos listas ni dos formatos,
        /// es lo que impide repetirla. El propio `FsuipcService.GetAircraftDeveloper` ya avisaba del
        /// riesgo al unificar la tabla de desarrolladores.
        /// </summary>
        internal static string Compose(string designator, string title)
        {
            string type = !string.IsNullOrWhiteSpace(designator) ? designator.Trim()
                        : !string.IsNullOrWhiteSpace(title)      ? title.Trim()
                        : UnknownType;

            string addon = AddonFromTitle(title);
            return addon == null ? type : type + "  [" + addon + "]";
        }

        /// <summary>
        /// **La línea que el log de inicio escribe para la aeronave** —`✈️ A319  [ToLiss]`—, compuesta
        /// con la **misma** `Compose` que el bloque del gráfico: es lo que garantiza que el log y la
        /// imagen que se comparte no puedan decir cosas distintas de la misma aeronave.
        /// </summary>
        internal static string LogLine(string simModel, string simTitle, string simAtcModel)
        {
            string designator, family;
            Resolve(simModel, simTitle, simAtcModel, out designator, out family);
            return LogPrefix + Compose(designator, simTitle);
        }

        // ── Tabla designador ICAO → fabricante ────────────────────────────────────
        // Solo los prefijos que identifican un fabricante **sin ambigüedad**. No es una tabla de
        // tipos ICAO completa —no hace falta—: lo que no está aquí no se traduce, que es la
        // respuesta honesta. Los prefijos son los que usa la tabla de `AircraftTypeMatch` (las
        // variantes que el cliente sabe resolver) más las familias del modelo ATC.
        private static readonly List<KeyValuePair<string, string>> Prefixes = BuildPrefixes();

        private static List<KeyValuePair<string, string>> BuildPrefixes()
        {
            var list = new List<KeyValuePair<string, string>>
            {
                // Airbus — incluida la familia A220, que es el `BCS` de la antigua CSeries de
                // Bombardier y hoy se vende como Airbus. El designador no cambió con la marca.
                P("BCS", "Airbus"),
                P("A19", "Airbus"), P("A20", "Airbus"), P("A21", "Airbus"), P("A22", "Airbus"),
                P("A31", "Airbus"), P("A32", "Airbus"), P("A33", "Airbus"), P("A34", "Airbus"),
                P("A35", "Airbus"), P("A38", "Airbus"),

                // Boeing — familias y variantes. `B3` cubre los MAX (B37M/B38M/B39M/B3XM).
                P("B3",  "Boeing"), P("B7",  "Boeing"),

                // Regionales y demás.
                P("AT4", "ATR"), P("AT7", "ATR"), P("ATR", "ATR"),
                P("DH8", "De Havilland"), P("DHC", "De Havilland"),
                P("CRJ", "Bombardier"),
                P("E17", "Embraer"), P("E19", "Embraer"), P("E50", "Embraer"), P("E55", "Embraer"),
                P("MD1", "McDonnell Douglas"), P("MD8", "McDonnell Douglas"), P("MD9", "McDonnell Douglas"),
            };
            list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return list;
        }

        private static KeyValuePair<string, string> P(string prefix, string manufacturer)
            => new KeyValuePair<string, string>(prefix, manufacturer);

        // ── Tabla desarrollador → nombre canónico ─────────────────────────────────
        // Los alias van en mayúsculas y el valor es lo que se pinta. Es la **unión** de la tabla que
        // tenía `FsuipcService.GetAircraftDeveloper` (la del log de inicio, con su treintena de
        // addons) y la que hacía falta para la cabecera del gráfico (`Asobo`, `ToLiss`, `Level-D`…):
        // una sola lista, un solo sitio donde añadir un addon.
        private static readonly List<KeyValuePair<string, string>> Developers = BuildDevelopers();

        private static List<KeyValuePair<string, string>> BuildDevelopers()
        {
            return new List<KeyValuePair<string, string>>
            {
                P2("PMDG",             "PMDG"),
                P2("FENIX",            "Fenix"),
                P2("TOLISS",           "ToLiss"),
                P2("ASOBO",            "Asobo"),
                P2("INIBUILDS",        "iniBuilds"),
                P2("FLY BY WIRE",      "FlyByWire"),
                P2("FLYBYWIRE",        "FlyByWire"),
                P2("FLYBYWISE",        "FlyByWire"),
                P2("A32NX",            "FlyByWire"),
                P2("FLIGHTFACTOR",     "FlightFactor"),
                P2("FLIGHT FACTOR",    "FlightFactor"),
                P2("FLIGHTSIMLABS",    "FlightSimLabs"),
                P2("FSLABS",           "FSLabs"),
                P2("JUSTFLIGHT",       "JustFlight"),
                P2("JUST FLIGHT",      "JustFlight"),
                P2("AEROSOFT",         "Aerosoft"),
                P2("LEONARDO",         "Leonardo"),
                P2("QUALITYWINGS",     "QualityWings"),
                P2("CAPTAINSIM",       "Captain Sim"),
                P2("CAPTAIN SIM",      "Captain Sim"),
                P2("LEVEL-D",          "Level-D"),
                P2("LEVEL D",          "Level-D"),
                P2("MAJESTIC",         "Majestic"),
                P2("X-CRAFTS",         "X-Crafts"),
                P2("HEADWIND",         "Headwind"),
                P2("WORKING TITLE",    "Working Title"),
                P2("FELIS",            "Felis"),
                P2("ZIBO",             "Zibo"),
                P2("IFLY",             "iFly"),
                P2("TFDI",             "TFDi"),
                P2("AIRFOILLABS",      "Airfoillabs"),
                P2("LAMINAR",          "Laminar"),
                P2("BLACKBOX",         "BlackBox"),
                P2("BLACK SQUARE",     "Black Square"),
                P2("BLACKSQUARE",      "Black Square"),
                P2("JARDESIGN",        "JARDesign"),
                P2("CARENADO",         "Carenado"),
                P2("ALABEO",           "Alabeo"),
                P2("ROTATE",           "Rotate"),
                P2("MILVIZ",           "MilViz"),
                P2("INDIAFOXTECHO",    "IndiaFoxtEcho"),
                P2("AEROPLANE HEAVEN", "Aeroplane Heaven"),
                P2("FLYSIMWARE",       "Flysimware"),
                P2("SWS",              "SWS"),
            };
        }

        private static KeyValuePair<string, string> P2(string alias, string developer)
            => new KeyValuePair<string, string>(alias, developer);
    }
}
