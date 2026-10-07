using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Componentes del viento respecto al eje de la pista de aterrizaje. Es un **dato calculado**,
    /// no una lectura: por eso vive en un helper puro, con su test, y no dentro del formulario ni
    /// del servicio.
    ///
    /// **Referencias (es donde se falla).** El viento se da **de donde viene** (METAR `32012KT` =
    /// viento del 320) y el rumbo de pista que usamos es **VERDADERO**, no magnético: es el que
    /// calcula `NavDataService.TrueRunwayBearing` desde las coordenadas WGS-84 del umbral y el
    /// extremo. Mezclar un viento con el rumbo **magnético** de la pista mete el error de la
    /// variación local — **−8,59° en SKBO**, **−13,73° en KBOS** — y en SKBO 14R la componente
    /// cruzada de un viento de 12 kt saldría 2,8 kt desviada, que es más que el ruido de la medida.
    ///
    /// **Convención de signos** (una sola, la del PIREP y la del logbook):
    /// - `HeadwindKt > 0` → **viento en cara**; `< 0` → **en cola** (el valor absoluto es la cola).
    /// - `CrosswindKt > 0` → viento **desde la DERECHA** de la pista; `< 0` desde la izquierda.
    ///   Con rumbo de pista 360 y viento 090 (del este) sale **+V**: el viento llega por la derecha.
    ///
    /// **La racha no entra en las componentes.** Se arrastra en el resultado porque hay que
    /// publicarla (`WIND 320/12G20`), pero el cálculo de cara/cruzada usa el **viento medio**, que
    /// es el que compara el piloto con las tablas de límite de su avión: aplicar la racha a la misma
    /// dirección contaría dos veces un máximo que el METAR ya declara aparte.
    /// </summary>
    internal struct WindComponentResult
    {
        /// <summary>Dirección del viento **según llegó**, en grados (de donde viene). Null si no había dato.</summary>
        internal double? WindDirDeg;

        /// <summary>Intensidad media, en nudos. Null si no había dato.</summary>
        internal double? WindSpeedKt;

        /// <summary>Racha, en nudos. Null si el METAR no la publica o no hay METAR.</summary>
        internal double? GustKt;

        /// <summary>Rumbo **verdadero** del eje de la pista de aterrizaje, en grados. Null si no se conoce.</summary>
        internal double? RunwayHeadingTrueDeg;

        /// <summary>Hay viento y hay rumbo de pista: las dos componentes de abajo tienen valor.</summary>
        internal bool Available;

        /// <summary>Viento en calma (0 kt): las componentes son 0 y el lado no significa nada.</summary>
        internal bool Calm;

        /// <summary>Componente en cara (positiva) o en cola (negativa), en nudos.</summary>
        internal double HeadwindKt;

        /// <summary>Componente cruzada: positiva **desde la derecha**, negativa **desde la izquierda**.</summary>
        internal double CrosswindKt;
    }

    /// <summary>
    /// Regla pura de las componentes del viento del aterrizaje. Ver <see cref="WindComponentResult"/>
    /// para la convención de signos y las referencias (viento «de donde viene», rumbo de pista
    /// verdadero).
    ///
    /// **Degrada sin datos**: sin dirección de viento, sin intensidad o sin rumbo de pista devuelve
    /// `Available = false` con las componentes a 0 **sin inventar nada**; los valores que sí
    /// llegaron se conservan en el resultado para poder publicarlos en crudo (`WIND 320/12`) aunque
    /// no se puedan descomponer.
    /// </summary>
    internal static class WindComponents
    {
        /// <summary>
        /// Descompone el viento respecto al eje de pista.
        /// </summary>
        /// <param name="windDirDeg">Dirección **de donde viene** el viento, en grados (METAR `32012KT` → 320).</param>
        /// <param name="windSpeedKt">Intensidad media en nudos.</param>
        /// <param name="gustKt">Racha en nudos, si el METAR la publica. Solo se arrastra: no entra en el cálculo.</param>
        /// <param name="runwayHeadingTrueDeg">Rumbo **verdadero** del eje de la pista de aterrizaje.</param>
        internal static WindComponentResult Compute(
            double? windDirDeg, double? windSpeedKt, double? gustKt, double? runwayHeadingTrueDeg)
        {
            var result = new WindComponentResult
            {
                // Lo que llegó se conserva aunque no se pueda descomponer: el dato crudo se publica.
                WindDirDeg            = Normalize(windDirDeg),
                WindSpeedKt           = Normalize(windSpeedKt),
                GustKt                = Normalize(gustKt),
                RunwayHeadingTrueDeg  = Normalize(runwayHeadingTrueDeg),
            };

            // Sin rumbo de pista no hay eje contra el que descomponer. No se supone 0 (que sería
            // «norte» y daría una cruzada falsa): se degrada.
            if (!result.RunwayHeadingTrueDeg.HasValue) return result;

            // Sin viento no hay componentes. Aquí «sin viento» es **sin dato**, no calma: la calma
            // llega como 0 kt explícito y se resuelve más abajo.
            if (!result.WindDirDeg.HasValue || !result.WindSpeedKt.HasValue) return result;

            // Un valor no finito es un dato roto, no un viento: degradar sin inventar.
            if (!IsFinite(result.WindDirDeg.Value) || !IsFinite(result.WindSpeedKt.Value)) return result;

            if (result.WindSpeedKt.Value <= 0)
            {
                // Calma: es una observación real, no un hueco. Componentes 0, y `Calm` para que
                // quien pinte no escriba «desde la derecha» de un viento que no sopla.
                result.Calm      = true;
                result.Available = true;
                return result;
            }

            // Ángulo entre «de donde viene» y el morro de la pista, en (−180, 180].
            double thetaDeg = DeltaDeg(result.WindDirDeg.Value, result.RunwayHeadingTrueDeg.Value);
            double rad      = thetaDeg * Math.PI / 180.0;

            // θ = 0 → viento de cara (cos 1, sin 0). θ = +90 → viento del lado derecho (sin 1).
            result.HeadwindKt  = result.WindSpeedKt.Value * Math.Cos(rad);
            result.CrosswindKt = result.WindSpeedKt.Value * Math.Sin(rad);
            result.Available   = true;
            return result;
        }

        /// <summary>
        /// Texto corto y neutro para el PIREP y el logbook: `HW 11 XW +5`. `HW` negativo es viento
        /// **en cola**; `XW` positivo es viento **desde la derecha**. Devuelve `CALM` en calma y
        /// `—` cuando no se pudo calcular (sin viento o sin pista): nunca un número inventado.
        /// </summary>
        internal static string Format(WindComponentResult c)
        {
            if (c.Calm) return "CALM";
            if (!c.Available) return "—";

            double hw = Math.Round(c.HeadwindKt, MidpointRounding.AwayFromZero);
            double xw = Math.Round(c.CrosswindKt, MidpointRounding.AwayFromZero);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "HW {0:0;-0;0} XW {1:+0;-0;0}", hw, xw);
        }

        /// <summary>
        /// Viento en crudo tal como se publica: `320/12G20` (dirección a tres dígitos, intensidad y,
        /// si el METAR la trae, la racha). `—` si no hay dirección o intensidad.
        /// </summary>
        internal static string FormatRaw(WindComponentResult c)
        {
            if (!c.WindDirDeg.HasValue || !c.WindSpeedKt.HasValue) return "—";

            string gust = c.GustKt.HasValue && c.GustKt.Value > 0
                ? "G" + ((int)Math.Round(c.GustKt.Value)).ToString("00",
                          System.Globalization.CultureInfo.InvariantCulture)
                : "";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "{0:000}/{1:00}{2}",
                                 (int)Math.Round(c.WindDirDeg.Value) % 360,
                                 (int)Math.Round(c.WindSpeedKt.Value), gust);
        }

        /// <summary>Diferencia angular mínima, en grados (−180, 180]. Es la misma cuenta que
        /// `GeoMath.BearingDiffDeg` salvo por el signo, que aquí hace falta: el lado del viento
        /// **es** el signo de la componente cruzada.</summary>
        internal static double DeltaDeg(double a, double b)
        {
            double d = (a - b) % 360.0;
            if (d > 180.0) d -= 360.0;
            if (d <= -180.0) d += 360.0;
            return d;
        }

        private static double? Normalize(double? value)
            => value.HasValue && IsFinite(value.Value) ? value : null;

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
