using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>Un campo del bloque de datos del gráfico: la **clave** del rótulo (idioma) y el
    /// **valor** ya compuesto. El valor nunca es vacío: un campo sin dato **no se añade**.</summary>
    internal sealed class LandingHeaderField
    {
        internal string Key   { get; }
        internal string Value { get; }

        internal LandingHeaderField(string key, string value)
        {
            Key   = key;
            Value = value;
        }
    }

    /// <summary>
    /// **La cabecera de datos del gráfico del flare**: lo que otro piloto necesita para entender la
    /// imagen de un vistazo —de qué avión es, cuánto pesaba, cuándo y dónde fue, y cómo fue la
    /// toma—.
    ///
    /// Es una función pura porque lo que decide no es el dibujo sino **qué se sabe y qué no**: cada
    /// campo se añade solo si su dato existe, así que un vuelo sin título de aeronave, sin peso o
    /// sin viento enseña menos líneas en vez de líneas con ceros. Ese «bloque con sus huecos» es lo
    /// que se prueba.
    ///
    /// **De dónde sale cada cosa:**
    /// - **Aeronave**: la **misma línea que el log de inicio** —el designador que manda, que es la
    ///   variante resuelta (`A319`, `B38M`, `B77L`), y el addon entre corchetes—, con la familia del
    ///   modelo ATC (`ATC B777`) aparte y marcada como tal cuando no coincide con él. La identidad la
    ///   compone `AircraftIdentity`, que es la **única** que la resuelve y la misma que usa el log: dos
    ///   formatos distintos para la misma aeronave no pueden volver a separarse.
    /// - **Fabricante**: deducido del **designador ICAO**; el addon, **solo si el título lo dice**
    ///   (ver `AircraftIdentity`). Son dos datos distintos y van en la misma línea separados.
    /// - **LTOW**: el peso **en el momento de la toma**, leído del simulador y persistido con el
    ///   vuelo. La unidad se escribe siempre (`lb`) porque el dato es una lectura, no una
    ///   conversión.
    /// - **GS/IAS del toque**: de la **primera muestra en tierra** de la traza fina, que es el
    ///   contacto. Sin traza fina no se publican: la de 2 s no está en esta ventana.
    ///
    /// El render alinea por la columna de rótulos (fuente monoespaciada en el formulario) y usa
    /// `\n` como separador de línea.
    /// </summary>
    internal static class LandingHeader
    {
        // ── Claves de rótulo (están en los dos `.json`) ───────────────────────────
        internal const string KeyAircraft     = "LandingHdr_Aircraft";
        internal const string KeyManufacturer = "LandingHdr_Manufacturer";
        internal const string KeyWeight       = "LandingHdr_Weight";
        internal const string KeyFlight       = "LandingHdr_Flight";
        internal const string KeyTouchdown    = "LandingHdr_Touchdown";
        internal const string KeyWind         = "LandingHdr_Wind";

        // Los dos rótulos de flaps y potencia **no estrenan clave**: son los mismos de la línea de
        // resumen del logbook (`Landing_FlapsHeader` / `Landing_PowerCutHeader`), y usar los mismos
        // evita dos vocabularios para lo mismo.
        internal const string KeyFlaps = "Landing_FlapsHeader";
        internal const string KeyPower = "Landing_PowerCutHeader";

        // ── Claves de fragmento ───────────────────────────────────────────────────
        internal const string KeyAtcFamily      = "LandingHdr_AtcFamily";
        internal const string KeyAtTouchdown    = "LandingHdr_AtTouchdown";
        internal const string KeyFromThreshold  = "LandingHdr_FromThreshold";
        internal const string KeyScore          = "LandingHdr_Score";
        internal const string KeyDeducedFrom    = "LandingHdr_DeducedFrom";
        internal const string KeyAddonFromTitle = "LandingHdr_AddonFromTitle";
        internal const string KeyCenterline     = "LandingHdr_Centerline";

        /// <summary>Separador entre los datos de una misma línea y entre aeronave y fabricante.</summary>
        internal const string Separator = " · ";

        /// <summary>
        /// **El bloque, en orden.** Los campos vacíos no aparecen: el orden es el de lectura —quién
        /// voló, con qué, cuándo y dónde, cómo fue la toma y con qué viento—.
        /// </summary>
        internal static IList<LandingHeaderField> Build(FlightRecord record,
                                                        FlareChartLayout layout,
                                                        IList<FlareTrackPoint> samples)
        {
            var fields = new List<LandingHeaderField>();
            if (record == null) return fields;

            // El designador que manda: la variante si se puede resolver (`B77L`) y, si no, la familia
            // del modelo ATC (`B777`). Es el mismo dato con el que se compara la aeronave del vuelo.
            string title, designator, family;
            Identity(record, out title, out designator, out family);

            AddAircraft(fields, title, designator, family);
            AddManufacturer(fields, designator, title);
            AddWeight(fields, record.LandingWeightLbs);
            AddFlight(fields, record);
            AddTouchdown(fields, record, samples);
            AddFlaps(fields, layout);
            AddPower(fields, layout);
            AddWind(fields, record);

            return fields;
        }

        // ── Los campos ────────────────────────────────────────────────────────────

        private static void AddAircraft(List<LandingHeaderField> fields, string title,
                                        string designator, string family)
        {
            // Sin designador **ni** título no hay nada que decir de la aeronave: la línea se omite, en
            // vez de publicar el «Unknown» del log (que sí tiene que escribir algo en su renglón).
            if (title == null && designator == null) return;

            // **La identidad es la del log, no una versión propia**: `Compose` decide cómo se escribe
            // —designador y addon entre corchetes—, así que el bloque no puede enseñar `B737` donde el
            // log dice `B38M`.
            var parts = new List<string> { AircraftIdentity.Compose(designator, title) };

            // La familia solo se repite si dice algo distinto del designador ya publicado.
            if (family != null && !string.Equals(family, designator, StringComparison.Ordinal))
                parts.Add(L._(KeyAtcFamily, family));

            fields.Add(new LandingHeaderField(KeyAircraft, string.Join(Separator, parts)));
        }

        private static void AddManufacturer(List<LandingHeaderField> fields, string designator, string title)
        {
            var parts = new List<string>();

            string maker = AircraftIdentity.ManufacturerFromIcao(designator);
            if (maker != null) parts.Add(L._(KeyDeducedFrom, maker, designator));

            string addon = AircraftIdentity.AddonFromTitle(title);
            if (addon != null) parts.Add(L._(KeyAddonFromTitle, addon));

            if (parts.Count == 0) return;
            fields.Add(new LandingHeaderField(KeyManufacturer, string.Join(Separator, parts)));
        }

        private static void AddWeight(List<LandingHeaderField> fields, double? weightLbs)
        {
            if (!weightLbs.HasValue) return;
            if (double.IsNaN(weightLbs.Value) || double.IsInfinity(weightLbs.Value)) return;
            if (weightLbs.Value <= 0.0) return;

            fields.Add(new LandingHeaderField(KeyWeight,
                Number(weightLbs.Value) + " lb (" + L._(KeyAtTouchdown) + ")"));
        }

        private static void AddFlight(List<LandingHeaderField> fields, FlightRecord record)
        {
            var parts = new List<string>();

            // En **UTC** y con la `Z` puesta: un bloque para compartir no puede depender de la zona
            // horaria de quien lo mira.
            parts.Add(Stamp(record.FlightDate));

            if (!string.IsNullOrWhiteSpace(record.Origin) || !string.IsNullOrWhiteSpace(record.Destination))
                parts.Add((record.Origin ?? "").Trim() + " → " + (record.Destination ?? "").Trim());

            if (!string.IsNullOrWhiteSpace(record.RunwayName))
                parts.Add("RWY " + record.RunwayName.Trim());

            if (record.RunwayLengthFt.HasValue && record.RunwayLengthFt.Value > 0.0)
                parts.Add(Number(record.RunwayLengthFt.Value) + " ft");

            parts.Add(L._(KeyScore, record.Score));

            fields.Add(new LandingHeaderField(KeyFlight, string.Join(Separator, parts)));
        }

        private static void AddTouchdown(List<LandingHeaderField> fields, FlightRecord record,
                                         IList<FlareTrackPoint> samples)
        {
            var parts = new List<string>();

            // El centinela `NoLandingData` (-1) es «no hubo captura del toque», no un aterrizaje de
            // −1 fpm: se omite en vez de publicarlo.
            if (record.LandingRateFpm != Services.ScoringService.NoLandingData)
                parts.Add(record.LandingRateFpm.ToString("0", CultureInfo.InvariantCulture) + " fpm");

            double? gsKt, iasKt;
            TouchdownSpeeds(samples, out gsKt, out iasKt);
            if (gsKt.HasValue)  parts.Add("GS "  + Number(gsKt.Value)  + " kt");
            if (iasKt.HasValue) parts.Add("IAS " + Number(iasKt.Value) + " kt");

            if (record.TouchdownDistFt > 0.0)
                parts.Add(L._(KeyFromThreshold, Number(record.TouchdownDistFt)));

            if (record.CenterlineDevFt > 0.0)
                parts.Add(L._(KeyCenterline, Number(record.CenterlineDevFt)));

            if (parts.Count == 0) return;
            fields.Add(new LandingHeaderField(KeyTouchdown, string.Join(Separator, parts)));
        }

        private static void AddFlaps(List<LandingHeaderField> fields, FlareChartLayout layout)
        {
            var flaps = layout != null ? layout.Flaps : null;
            if (flaps == null || !flaps.HasTrack || flaps.AtThreshold == null) return;

            string text = L._("Landing_FlapsAtThreshold", flaps.AtThreshold.Text);
            if (flaps.AtTouchdown != null && flaps.Changed)
                text += "  " + L._("Landing_FlapsChanged", flaps.AtThreshold.ShortText,
                                                   flaps.AtTouchdown.ShortText);

            fields.Add(new LandingHeaderField(KeyFlaps, text));
        }

        private static void AddPower(List<LandingHeaderField> fields, FlareChartLayout layout)
        {
            var power = layout != null ? layout.Power : null;
            if (power == null || !power.HasValue) return;

            fields.Add(new LandingHeaderField(KeyPower,
                L._(power.EngineCount == 2 ? "Landing_PowerCut" : "Landing_PowerCutEngine1",
                    power.SecondsBeforeTouchdown)));
        }

        private static void AddWind(List<LandingHeaderField> fields, FlightRecord record)
        {
            var wind = record.WindAtLanding;
            string raw = WindComponents.FormatRaw(wind);
            if (raw == "—") return;                      // sin dirección o sin intensidad: no hay línea

            string value = raw + " kt";
            string components = WindComponents.Format(wind);
            if (components != "—") value += Separator + components;

            fields.Add(new LandingHeaderField(KeyWind, value));
        }

        // ── Reglas ────────────────────────────────────────────────────────────────

        /// <summary>
        /// **La identidad de la aeronave, resuelta en un solo sitio** —el designador que manda (la
        /// variante si se puede resolver y, si no, la familia del modelo ATC) y la familia aparte, para
        /// poder decir las dos cuando no coinciden—. La resolución vive en `AircraftIdentity`, que es
        /// también la que usa el log de inicio: el bloque no resuelve la variante por su cuenta.
        /// </summary>
        private static void Identity(FlightRecord record, out string title,
                                     out string designator, out string family)
        {
            AircraftIdentity.Resolve(record.AircraftModel, record.AircraftTitle, record.AircraftIcao,
                                     out designator, out family);
            title = Clean(record.AircraftTitle);
        }

        /// <summary>
        /// **La identidad en una sola línea corta, para el título del propio gráfico.** El bloque de
        /// datos vive en la franja del formulario y **no sale en el PNG**, así que lo que viaja en la
        /// imagen tiene que caber en un renglón: vuelo, ruta, pista, fecha, tipo con su fabricante y
        /// el peso. Los rótulos «AVIÓN»/«FABRICANTE» sobran aquí; el dato, no.
        /// </summary>
        internal static string CompactIdentity(FlightRecord record)
        {
            if (record == null) return "";

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(record.FlightNumber)) parts.Add(record.FlightNumber.Trim());
            if (!string.IsNullOrWhiteSpace(record.Origin) || !string.IsNullOrWhiteSpace(record.Destination))
                parts.Add((record.Origin ?? "").Trim() + " " + (char)0x2192 + " " + (record.Destination ?? "").Trim());
            if (!string.IsNullOrWhiteSpace(record.RunwayName))
                parts.Add("RWY " + record.RunwayName.Trim());

            parts.Add(Stamp(record.FlightDate));

            string title, designator, family;
            Identity(record, out title, out designator, out family);

            if (designator != null)
            {
                string maker = AircraftIdentity.ManufacturerFromIcao(designator);
                parts.Add(maker != null ? designator + " " + maker : designator);
            }

            if (record.LandingWeightLbs.HasValue && record.LandingWeightLbs.Value > 0.0)
                parts.Add("LTOW " + Number(record.LandingWeightLbs.Value) + " lb");

            return string.Join(Separator, parts);
        }

        /// <summary>La fecha del vuelo en UTC con la `Z`: un bloque para compartir no puede depender
        /// de la zona horaria de quien lo mira.</summary>
        private static string Stamp(DateTime utc)
            => utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "Z";

        /// <summary>
        /// **La GS y la IAS del toque**, de la **primera muestra en tierra** de la traza fina: ese
        /// es el contacto. No se coge la última muestra en tierra —sería la velocidad al final de la
        /// frenada, decenas de nudos por debajo— ni se interpola: lo que se publica es una lectura
        /// del instante. Sin ninguna muestra en tierra se devuelve `null` en las dos, en vez de la
        /// velocidad de otro momento.
        /// </summary>
        internal static void TouchdownSpeeds(IList<FlareTrackPoint> samples,
                                             out double? gsKt, out double? iasKt)
        {
            gsKt = null; iasKt = null;
            if (samples == null) return;

            foreach (var s in samples)
            {
                if (s == null || !s.OnGround) continue;
                gsKt  = Finite(s.GsKt)  ? s.GsKt  : null;
                iasKt = Finite(s.IasKt) ? s.IasKt : null;
                return;
            }
        }

        /// <summary>El bloque en texto, alineado por la columna de rótulos.</summary>
        internal static string Render(IList<LandingHeaderField> fields)
        {
            if (fields == null || fields.Count == 0) return "";

            int width = 0;
            foreach (var field in fields)
            {
                int len = L._(field.Key).Length;
                if (len > width) width = len;
            }

            var sb = new StringBuilder();
            foreach (var field in fields)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(L._(field.Key).PadRight(width)).Append("  ").Append(field.Value);
            }
            return sb.ToString();
        }

        /// <summary>
        /// **La línea de crédito del bloque**: la versión del cliente delante de la línea técnica del
        /// gráfico. Es lo que permite saber con qué se generó la imagen que se comparte, y por eso va
        /// en la misma franja que el resto del bloque y no en la barra de título de la ventana (que
        /// no sale en la captura).
        /// </summary>
        internal static string ClientCaption(string technical)
        {
            string client = L._("LandingHdr_Client", Core.Helpers.AppInfo.Version);
            return string.IsNullOrEmpty(technical) ? client : client + "  ·  " + technical;
        }

        // ── Auxiliares ────────────────────────────────────────────────────────────

        private static bool Finite(double? value)
            => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);

        /// <summary>«138,400»: separador de millar invariante, para que el bloque se lea igual en
        /// cualquier idioma y el test no dependa de la cultura de la máquina.</summary>
        private static string Number(double value)
            => value.ToString("N0", CultureInfo.InvariantCulture);

        private static string Clean(string text)
            => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
