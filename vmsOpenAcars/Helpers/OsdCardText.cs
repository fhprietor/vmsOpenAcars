using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using vmsOpenAcars.UI.Forms;
using static vmsOpenAcars.Helpers.L;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Todo el texto de la tarjeta del OSD se compone aquí**, con datos reales y sin inventar.
    ///
    /// La regla que manda es la de siempre en el cliente: **si falta el dato, la línea desaparece**.
    /// No hay `0 kt` de relleno ni `--`: un cero es una medida y un guion es una promesa de dato.
    /// Los números van en cultura invariante (punto decimal, coma de millares fuera) para que la
    /// tarjeta se lea igual con el Windows en español o en inglés, y el menos es el tipográfico
    /// **U+2212** (`−145`), no el guion del teclado.
    ///
    /// Los textos que ve el piloto salen por `_()` con la clave inglesa en los dos `.json`. Las
    /// cadenas completas que se publican en la tarjeta se pueden reconstruir desde sus tramos
    /// (`OsdTextLine.Text`) para que un test fije exactamente lo que se pinta.
    /// </summary>
    internal static class OsdCardText
    {
        private const char TypographicMinus = '\u2212';
        private const char MiddleDot        = '\u00B7';
        private const char Degree           = '\u00B0';
        private const string UtcSuffix      = "z";

        /// <summary>Los datos reales del toque que alimentan la tarjeta. Null = no hubo medida.</summary>
        internal sealed class OsdCardPayload
        {
            public OsdSeverity Severity       { get; set; }
            public string      Icao           { get; set; }
            public string      AirportName    { get; set; }
            public DateTime?   TouchdownUtc   { get; set; }
            public int?        LandingRateFpm { get; set; }
            public double?     GForce         { get; set; }
            public int?        IasKt          { get; set; }
            public int?        GroundSpeedKt  { get; set; }
            public double?     PitchDeg       { get; set; }
            public double?     BankDeg        { get; set; }
        }

        /// <summary>Un tramo de una línea: lo que se pinta y si va destacado (número) o en gris (unidad).</summary>
        internal sealed class OsdTextRun
        {
            internal OsdTextRun(string text, bool emphasized)
            {
                Text       = text ?? string.Empty;
                Emphasized = emphasized;
            }

            internal string Text       { get; }
            internal bool   Emphasized { get; }
        }

        /// <summary>Una línea de la tarjeta, ya troceada para pintar números en blanco y texto en gris.</summary>
        internal sealed class OsdTextLine
        {
            private readonly List<OsdTextRun> _runs = new List<OsdTextRun>();

            internal IList<OsdTextRun> Runs => _runs;

            internal OsdTextLine Add(string text, bool emphasized = false)
            {
                if (!string.IsNullOrEmpty(text))
                    _runs.Add(new OsdTextRun(text, emphasized));
                return this;
            }

            internal OsdTextLine Add(double value, string unit, bool emphasizedValue = true)
            {
                if (!string.IsNullOrEmpty(unit)) unit = " " + unit;
                return Add(FormatNumber(value), emphasizedValue).Add(unit, false);
            }

            internal bool IsEmpty => _runs.Count == 0;

            /// <summary>La cadena completa de la línea, que es lo que fijan los tests.</summary>
            internal string Text
            {
                get
                {
                    var sb = new StringBuilder();
                    foreach (var run in _runs) sb.Append(run.Text);
                    return sb.ToString();
                }
            }
        }

        // ── Cabecera ─────────────────────────────────────────────────────────────

        internal static string StatusLabel => _("Osd_Card_Landed");

        /// <summary>`Amsterdam Schiphol · 15:15z`; si falta un lado se pinta solo el otro.</summary>
        internal static string AirportLine(string airportName, DateTime? utc)
        {
            bool hasName = !string.IsNullOrWhiteSpace(airportName);
            bool hasTime = utc.HasValue;
            if (!hasName && !hasTime) return null;

            string time = hasTime ? TouchdownTime(utc.Value) : null;
            if (hasName && hasTime) return airportName.Trim() + " " + MiddleDot + " " + time;
            return hasName ? airportName.Trim() : time;
        }

        /// <summary>`Rolling out · 24 kt`. Sin velocidad de tierra queda solo el estado.</summary>
        internal static OsdTextLine LiveState(int? groundSpeedKt)
        {
            var line = new OsdTextLine().Add(_("Osd_Card_RollingOut"), false);
            if (groundSpeedKt.HasValue && groundSpeedKt.Value > 0)
            {
                line.Add(" " + MiddleDot + " ", false);
                line.Add(groundSpeedKt.Value.ToString(CultureInfo.InvariantCulture), true);
                line.Add(" " + _("Osd_Card_Kt"), false);
            }
            return line;
        }

        // ── Rótulos de las tres columnas ─────────────────────────────────────────

        internal static string LandingRateCaption => _("Osd_Card_LandingRate");
        internal static string TouchdownGCaption  => _("Osd_Card_TouchdownG");

        // ── Números grandes ──────────────────────────────────────────────────────

        /// <summary>`−145`: menos tipográfico, cero decimales, signo solo si es negativo.</summary>
        internal static string LandingRateValue(int? fpm)
        {
            if (!fpm.HasValue) return null;
            int value = fpm.Value;
            if (value < 0) return TypographicMinus + Math.Abs(value).ToString(CultureInfo.InvariantCulture);
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>`−145 fpm`; null si no hay dato.</summary>
        internal static string LandingRate(int? fpm)
        {
            string value = LandingRateValue(fpm);
            return value == null ? null : value + " " + _("Osd_Card_UnitFpm");
        }

        /// <summary>
        /// `1.18`: **dos decimales**. El G del toque se mide con el acelerómetro (`0x11BA`) y se
        /// publica con dos decimales en todo el cliente; un valor `≤ 0` es ausencia de lectura.
        /// </summary>
        internal static string GForceValue(double? gForce)
        {
            if (!gForce.HasValue || gForce.Value <= 0.0) return null;
            return gForce.Value.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>`1.18 G`; null si no hay medida.</summary>
        internal static string GForce(double? gForce)
        {
            string value = GForceValue(gForce);
            return value == null ? null : value + " " + _("Osd_Card_UnitG");
        }

        /// <summary>
        /// La píldora sale de **la tabla única del G** (`Helpers/GForceBands`), no de la severidad.
        ///
        /// La severidad del toque la decide el **fpm** (≤300 / ≤600 / más), mientras el número que
        /// la tarjeta pinta justo encima es el **G medido**. Elegir la etiqueta por severidad
        /// reintrodujo el defecto que v0.9.74 cerró: con **1,21 g** la píldora decía `Normal` y el
        /// registro y el scoring decían `Perfect` para el mismo valor. Ahora las dos salen de
        /// `GForceBands.LabelKey`, y sin G medido **no hay píldora**: no se inventa un veredicto.
        /// </summary>
        internal static string LandingBadge(double? gForce)
        {
            if (!gForce.HasValue || gForce.Value <= 0.0) return null;
            return _(GForceBands.LabelKey(gForce.Value));
        }

        internal static string ScoringNote => _("Osd_Card_ScoringNote");

        // ── Columna derecha ──────────────────────────────────────────────────────

        /// <summary>`134 kt IAS · 141 kt ground`; si solo hay una velocidad se pinta esa.</summary>
        internal static OsdTextLine SpeedLine(int? iasKt, int? groundSpeedKt)
        {
            bool hasIas = iasKt.HasValue && iasKt.Value > 0;
            bool hasGs  = groundSpeedKt.HasValue && groundSpeedKt.Value > 0;
            if (!hasIas && !hasGs) return null;

            var line = new OsdTextLine();
            if (hasIas)
            {
                line.Add(iasKt.Value.ToString(CultureInfo.InvariantCulture), true);
                line.Add(" " + _("Osd_Card_KtIas"), false);
            }
            if (hasIas && hasGs) line.Add(" " + MiddleDot + " ", false);
            if (hasGs)
            {
                line.Add(groundSpeedKt.Value.ToString(CultureInfo.InvariantCulture), true);
                line.Add(" " + _("Osd_Card_KtGround"), false);
            }
            return line;
        }

        /// <summary>`Pitch 4.2° · Bank 0.8°`, con un decimal y U+00B0; degrada por mitades.</summary>
        internal static OsdTextLine AttitudeLine(double? pitchDeg, double? bankDeg)
        {
            bool hasPitch = pitchDeg.HasValue;
            bool hasBank  = bankDeg.HasValue;
            if (!hasPitch && !hasBank) return null;

            var line = new OsdTextLine();
            if (hasPitch)
            {
                line.Add(_("Osd_Card_Pitch") + " ", false);
                line.Add(FormatNumber(pitchDeg.Value), true);
                line.Add(Degree.ToString(), false);
            }
            if (hasPitch && hasBank) line.Add(" " + MiddleDot + " ", false);
            if (hasBank)
            {
                line.Add(_("Osd_Card_Bank") + " ", false);
                line.Add(FormatNumber(bankDeg.Value), true);
                line.Add(Degree.ToString(), false);
            }
            return line;
        }

        /// <summary>
        /// `on the ground since 15:15z`: la hora **UTC** del contacto, sin contador de toques.
        /// El cliente solo conserva el último touchdown, así que un `1` fijo no informaría de nada.
        /// </summary>
        internal static OsdTextLine TouchdownLine(DateTime? touchdownUtc)
        {
            if (!touchdownUtc.HasValue) return null;
            return new OsdTextLine()
                .Add(_("Osd_Card_OnGroundSince") + " ", false)
                .Add(TouchdownTime(touchdownUtc.Value), true);
        }

        /// <summary>`15:15z`: hora UTC en 24 h con la `z` del sufijo, en cultura invariante.</summary>
        internal static string TouchdownTime(DateTime utc)
            => utc.ToUniversalTime().ToString("HH:mm", CultureInfo.InvariantCulture) + UtcSuffix;

        // ── Plantilla simple ─────────────────────────────────────────────────────

        internal static string SeverityLabel(OsdSeverity severity)
        {
            switch (severity)
            {
                case OsdSeverity.Success:  return _("Osd_Sev_Success");
                case OsdSeverity.Warning:  return _("Osd_Sev_Warning");
                case OsdSeverity.Critical: return _("Osd_Sev_Critical");
                default:                   return _("Osd_Sev_Info");
            }
        }

        /// <summary>
        /// La plantilla simple reestiliza los mensajes que ya existen **sin reinterpretarlos**: si
        /// traen el separador doble (`"PENALTY  NAV LIGHTS  −5 PTS"`), la primera parte es la
        /// etiqueta y el resto el detalle; si no, la etiqueta es la severidad y el mensaje entero
        /// va al detalle. No se recorta ni se reescribe ninguna palabra.
        /// </summary>
        internal static void SplitSimple(string text, OsdSeverity severity,
                                         out string label, out string detail)
        {
            string trimmed = (text ?? string.Empty).Trim();
            int split = trimmed.IndexOf("  ", StringComparison.Ordinal);
            if (split > 0)
            {
                label  = trimmed.Substring(0, split).Trim();
                detail = trimmed.Substring(split).Trim();
            }
            else
            {
                label  = SeverityLabel(severity);
                detail = trimmed;
            }
        }

        // ── Formato numérico común ───────────────────────────────────────────────

        /// <summary>Un decimal invariante; `-0.0` se normaliza a `0.0` (no es una medida negativa).</summary>
        private static string FormatNumber(double value)
        {
            double normalized = Math.Abs(value) < 0.05 ? 0.0 : value;
            return normalized.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
