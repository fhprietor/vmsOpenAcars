using System;
using System.Globalization;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Qué se pudo hacer con el número de flaps**: si no hay dato, si solo se puede enseñar el
    /// porcentaje o si además se pudo traducir a la compuerta del avión.
    /// </summary>
    internal enum FlapLabelSource
    {
        /// <summary>No hay porcentaje: no hay nada que enseñar.</summary>
        None,

        /// <summary>Hay porcentaje, pero no se etiqueta (familia desconocida, valor fuera de rango o
        /// el valor cae en la franja de duda entre dos compuertas).</summary>
        PercentageOnly,

        /// <summary>Etiqueta de la escala CONF de Airbus.</summary>
        AirbusConf,

        /// <summary>Etiqueta del marcado de detentes de una familia Boeing.</summary>
        BoeingDetent,
    }

    /// <summary>
    /// **Qué significan los flaps que el cliente tiene guardados**, que es la pregunta que no se
    /// puede contestar sin saber de qué avión se trata.
    ///
    /// **Lo que se guarda es un porcentaje del recorrido del mando, no una compuerta.**
    /// `flare_track.flaps_pct` sale de `FsuipcService.FlapsPercent`, que es
    /// `0x0BDC / 16383 × 100`: el offset está declarado en `Services/FsuipcService.cs` **línea 157**
    /// como «Posición del handle 0-16383», se lee en la **línea 823** (`_flapsHandlePercent`) y se
    /// convierte a porcentaje en la **línea 830**. El offset dice «posición del handle»: **no**
    /// publica ni un detente ni unos grados, así que el número que guardamos **no** es una compuerta.
    ///
    /// **La tabla de compuertas no se inventa aquí: es la que el cliente ya usa en vuelo.**
    /// `FsuipcService.DecodeFlapsByFamily` (líneas 878-959) traduce ese mismo raw a
    /// `UP/1/2/5/10/15/25/30/40` en el 737, a `UP/1/5/10/20/25/30` en el 747 y el 777, y a
    /// `0/1+F/2/3/FULL` en Airbus, y **es lo que el piloto ve en el panel de vuelo**. Las bandas de
    /// esta clase son **esos mismos umbrales**, copiados en la escala del offset (raw 0-16383) para
    /// que no haya dos tablas que puedan separarse: si el panel dice `15`, aquí sale `FLAPS 15`.
    ///
    /// **La única suposición que queda, dicha sin adornos**: que el reparto del recorrido del mando
    /// entre compuertas es el de esa tabla. Es la misma suposición que el panel de vuelo ya hace en
    /// cada vuelo, no una nueva; y aun así la etiqueta se publica como **aproximada** (`≈FLAPS 15`),
    /// porque es una traducción de un porcentaje y no la lectura de un detente.
    ///
    /// **El detente de verdad se lee y se guarda: `0x0BFC`** («Notch actual», declarado en la línea
    /// 156 de `Services/FsuipcService.cs` y leído como `FlapsIndex` en la 822), persistido en
    /// `flare_track.flaps_index` y pasado aquí como tercer argumento de <see cref="Interpret"/>. Ese
    /// camino **no** es una traducción —el simulador publica la compuerta que tiene puesta—, así que
    /// su etiqueta va **sin el «≈»**; el «≈» queda solo para la etiqueta que sí sale del porcentaje.
    /// El índice se mapea **posicionalmente** a la tabla de bandas de su familia (la compuerta 0 es
    /// la primera banda, la 1 la segunda…), que es el orden que publica el marcado del avión.
    ///
    /// **No se etiqueta** cuando el valor cae en la **franja de duda** junto a una frontera de banda
    /// (`BandMargin`): ahí el mando está en tránsito entre dos compuertas —lo normal mientras se
    /// mueve— y decir «≈FLAPS 15» cuando va camino del 25 es peor que decir el porcentaje. Los bordes
    /// **exteriores** (0 % y 100 %) son extremos físicos del recorrido, no fronteras difusas, así que
    /// no llevan franja de duda: `UP` y `FULL`/`40` se etiquetan desde su extremo.
    ///
    /// **Familia desconocida** (787, 757, 767, Embraer, Cessna, ATR, vacío o `????`): **porcentaje y
    /// nada más**. No se le supone a un avión un marcado que no está escrito en ninguna parte del
    /// proyecto — el propio `DecodeFlapsByFamily` mete el 787 y el 767 en el cajón del 777 para el
    /// rótulo de vuelo, y esa licencia no se hereda aquí.
    /// </summary>
    internal sealed class FlapSetting
    {
        private FlapSetting() { }

        /// <summary>Hay un porcentaje utilizable (0–100).</summary>
        internal bool HasValue { get; private set; }

        /// <summary>El porcentaje del recorrido del mando, tal cual se guardó.</summary>
        internal double Percent { get; private set; }

        /// <summary>
        /// La compuerta traducida —`FLAPS 30`, `CONF FULL`— o `null` si no se etiqueta. Las palabras
        /// son las del **marcado del avión**, no texto de interfaz: no se traducen.
        /// </summary>
        internal string Label { get; private set; }

        /// <summary>
        /// **La etiqueta es una traducción, no una medida.** Es `true` cuando la compuerta sale de
        /// traducir el porcentaje del mando y **`false`** cuando sale de la lectura del detente real
        /// (`0x0BFC`, `flare_track.flaps_index`): en ese segundo caso el «≈» no tiene sentido porque
        /// el simulador publicó la compuerta, no una posición del recorrido.
        /// </summary>
        internal bool IsApproximate { get; private set; }

        internal FlapLabelSource Source { get; private set; }

        /// <summary>El porcentaje, formateado: `91%`. `—` cuando no hay dato.</summary>
        internal string PercentText { get; private set; }

        /// <summary>Lo que se enseña cuando no cabe más: `≈FLAPS 30`, `91%` o `—`.</summary>
        internal string ShortText { get; private set; }

        /// <summary>La forma larga, con el porcentaje detrás de la etiqueta: `≈FLAPS 30 (91%)`.</summary>
        internal string Text { get; private set; }

        /// <summary>
        /// La forma que viaja al `notes` del PIREP. En **ASCII**: el `notes` es texto para la
        /// aerolínea y el «≈» no tiene por qué sobrevivir a su base de datos, así que el matiz de
        /// aproximado va con `~`. **Siempre autodescriptiva** —`~FLAPS 30`, `FLAPS 81%`—, porque en
        /// una línea de texto compartida con la meteo un `81%` suelto no dice de qué es el porcentaje.
        /// `null` sin dato, para que el llamante no concatene nada.
        /// </summary>
        internal string PirepText { get; private set; }

        /// <summary>Sin dato: ni porcentaje ni etiqueta.</summary>
        private static FlapSetting NoData() => new FlapSetting
        {
            HasValue     = false,
            Percent      = double.NaN,
            Label        = null,
            Source       = FlapLabelSource.None,
            PercentText  = "—",
            ShortText    = "—",
            Text         = "—",
            PirepText    = null,
        };

        /// <summary>
        /// **La decisión.** Traduce el porcentaje del mando a la compuerta del avión, o se queda en el
        /// porcentaje cuando no puede hacerlo con fundamento.
        /// </summary>
        /// <param name="rawPct">
        /// El porcentaje del recorrido del mando (0–100), tal como se guardó en `flare_track.flaps_pct`.
        /// `null` —o no finito— es «no hay dato».
        /// </param>
        /// <param name="aircraftFamily">
        /// El código de familia de la aeronave: el **modelo ATC** del simulador (`B737`, `B777`,
        /// `B747`, `A320`…) o el designador de variante (`B738`, `A20N`, `B77L`…). Vacío o `????` es
        /// «no se sabe», y entonces solo se enseña el porcentaje.
        /// </param>
        /// <param name="detentIndex">
        /// El detente que el simulador publica en `0x0BFC` (`flare_track.flaps_index`), o `null`
        /// cuando el avión no lo publica o la fila es anterior a la columna. **Cuando viene, manda**:
        /// es una lectura y su etiqueta no es aproximada. Solo si la tabla de esa familia no lo cubre
        /// —índice fuera de rango, o familia sin tabla— se degrada al porcentaje **sin etiqueta**:
        /// traducir el porcentaje encima de un detente que no sabemos leer sería inventar.
        /// </param>
        internal static FlapSetting Interpret(double? rawPct, string aircraftFamily, int? detentIndex = null)
        {
            if (!rawPct.HasValue || double.IsNaN(rawPct.Value) || double.IsInfinity(rawPct.Value))
                return NoData();

            double pct = rawPct.Value;

            // ── 1) El detente real, si lo hay ─────────────────────────────────────
            // `0x0BFC` es **la compuerta** que tiene puesta el avión, no una traducción del recorrido
            // del mando. Por eso su etiqueta va sin «≈» y por eso no pasa por la franja de duda: la
            // duda existía para no decidir una compuerta a partir de una posición intermedia, y aquí
            // la compuerta ya viene decidida por el simulador.
            if (detentIndex.HasValue)
            {
                var detentBands = BandsFor(aircraftFamily);
                if (detentBands != null && detentIndex.Value >= 0 && detentIndex.Value < detentBands.Length)
                    return Build(pct, detentBands[detentIndex.Value].Label,
                                 SourceFor(aircraftFamily), isApproximate: false);

                // El detente existe pero la tabla no lo cubre (o no hay familia que lo tenga): no se
                // etiqueta **ni por banda**, porque el detente real ya afirmó algo que no sabemos leer
                // y traducir el porcentaje podría dar una compuerta distinta a la que está puesta.
                return PercentageOnly(pct);
            }

            // Fuera del rango que documenta el offset (0–16383 → 0–100 %) no hay posición que
            // traducir: se enseña el número —que es lo que hay— y **ninguna** etiqueta.
            if (pct < 0.0 || pct > 100.0)
                return PercentageOnly(pct);

            var bands = BandsFor(aircraftFamily);
            if (bands == null)
                return PercentageOnly(pct);

            // Se vuelve a la escala del offset para comparar contra los umbrales de
            // `DecodeFlapsByFamily` tal cual, sin reescribirlos en porcentaje (donde sí habría
            // redondeo y, con el tiempo, deriva).
            double raw = pct / 100.0 * RawFull;

            double lower = 0.0;
            foreach (var band in bands)
            {
                if (raw >= band.UpperRaw)
                {
                    lower = band.UpperRaw;
                    continue;
                }

                double width  = band.UpperRaw - lower;
                double margin = width * BandMargin;

                // La franja de duda no se aplica al borde exterior del recorrido: 0 y 16383 son
                // extremos físicos, no fronteras entre dos compuertas.
                double usableLow  = lower <= 0.0 ? lower : lower + margin;
                double usableHigh = band.UpperRaw >= RawFull ? band.UpperRaw : band.UpperRaw - margin;

                if (raw < usableLow || raw > usableHigh)
                    return PercentageOnly(pct);

                // Aquí la etiqueta **sí** es una traducción del porcentaje: lleva «≈».
                return Build(pct, band.Label, SourceFor(aircraftFamily), isApproximate: true);
            }

            // Inalcanzable con la última banda abierta hasta `RawFull`; se deja el porcentaje en vez
            // de lanzar, que es lo que manda la regla de degradar sin datos.
            return PercentageOnly(pct);
        }

        /// <summary>
        /// **El detente que se puede persistir, o `null` si el avión no lo publica.**
        ///
        /// El offset `0x0BFC` es un byte y **cero es un detente legítimo** (flaps arriba), así que no
        /// se puede usar el 0 como «sin dato». Los addons que no escriben el offset lo dejan valiendo
        /// 0, y entonces el notch dice «arriba» aunque el mando esté desplegado: ese es el único caso
        /// en que el offset miente, y se detecta **contrastándolo con el porcentaje del mando**
        /// (`0x0BDC`, el mismo dato que ya se guarda en `flaps_pct`), que sí publica el recorrido.
        ///
        /// Se descarta también lo que no puede ser un notch: un byte por encima de
        /// <see cref="MaxDetentIndex"/> —los marcados que trata este proyecto no pasan de 9— y un
        /// porcentaje fuera de 0–100, que es un dato corrupto. En todos esos casos va `null`, nunca 0.
        /// </summary>
        internal static int? DetentOrNull(byte rawIndex, double? flapsPercent)
        {
            if (!flapsPercent.HasValue || double.IsNaN(flapsPercent.Value) ||
                double.IsInfinity(flapsPercent.Value))
                return null;

            double pct = flapsPercent.Value;
            if (pct < 0.0 || pct > 100.0) return null;
            if (rawIndex > MaxDetentIndex) return null;

            // 0x0BFC a 0 con el mando claramente desplegado = el addon no publica el notch. Un 0 con
            // el mando arriba (0 %) sí es una lectura: es «flaps arriba».
            if (rawIndex == 0 && pct > FlapsUpPercent) return null;

            return rawIndex;
        }

        /// <summary>
        /// **El byte más alto que todavía puede ser un notch**: los marcados que este proyecto trata
        /// —737 (9 compuertas), 747 y 777 (7) y Airbus (5)— no pasan de ahí, así que por encima es un
        /// offset que nadie escribió, no una compuerta.
        /// </summary>
        internal const int MaxDetentIndex = 15;

        /// <summary>
        /// **A partir de qué porcentaje del mando se considera que los flaps ya no están arriba**:
        /// 1 %. El recorrido de `0x0BDC` da pasos de 0,006 %, así que un mando arriba vale 0 exacto y
        /// cualquier desplazamiento real lo supera; el punto porcentual es margen para el ruido del
        /// addon, no una frontera fina.
        /// </summary>
        internal const double FlapsUpPercent = 1.0;

        /// <summary>El fondo de escala del offset: `0x0BDC` va de 0 a 16383.</summary>
        internal const double RawFull = 16383.0;

        /// <summary>
        /// **Cuánto se recorta cada banda por dentro antes de fiarse de ella: un 10 % de su anchura
        /// por cada lado.** Deja el 80 % central como etiquetable y convierte en «porcentaje a secas»
        /// la franja pegada a la frontera con la compuerta vecina. Es un 10 % de la **anchura de la
        /// banda** y no un valor fijo, para que una banda estrecha (el `UP` del 737 ocupa 200 de
        /// 16383) no quede entera dentro de la duda.
        /// </summary>
        internal const double BandMargin = 0.10;

        /// <summary>Una compuerta: por debajo de <see cref="UpperRaw"/> (exclusivo) es esta.</summary>
        private sealed class FlapBand
        {
            internal FlapBand(double upperRaw, string label) { UpperRaw = upperRaw; Label = label; }
            internal double UpperRaw { get; }
            internal string Label    { get; }
        }

        /// <summary>
        /// Las compuertas de esa familia, o `null` si no hay una tabla que se pueda defender. Los
        /// umbrales son **literalmente** los de `FsuipcService.DecodeFlapsByFamily` (líneas 878-959),
        /// donde cada comparación es `raw &lt; umbral`, así que el umbral es el **final** de la banda:
        /// por debajo de 2248 el 737 está en `1`, no en `2`.
        ///
        /// El último tramo termina en `RawFull + 1`, un valor que el offset nunca alcanza (va de 0 a
        /// 16383). No es un redondeo: con `RawFull` exacto, un 100 % del mando cumpliría `raw &gt;=
        /// UpperRaw` y se saldría del bucle sin etiqueta —`CONF FULL` y `FLAPS 40` no se etiquetarían
        /// nunca, que es justo el extremo donde el piloto sabe lo que tiene puesto—.
        ///
        /// El 787, el 757 y el 767 se quedan fuera a propósito: el cliente los mete en el mismo cajón
        /// que el 777 para el rótulo de vuelo, pero aquí no se les supone un marcado que no está
        /// escrito en ninguna parte del proyecto.
        /// </summary>
        private static FlapBand[] BandsFor(string aircraftFamily)
        {
            string f = (aircraftFamily ?? "").Trim().ToUpperInvariant();
            if (f.Length < 3) return null;

            if (IsAirbusFamily(f))
                return new[]
                {
                    new FlapBand(  400.0, "CONF 0"),
                    new FlapBand( 4500.0, "CONF 1+F"),
                    new FlapBand( 8600.0, "CONF 2"),
                    new FlapBand(12700.0, "CONF 3"),
                    new FlapBand(RawFull + 1.0, "CONF FULL"),
                };

            // 737: familia (`B737`) y variantes (`B738`, `B739`, `B736`, `B73G`), más los MAX, cuyos
            // designadores no empiezan por `B73` (`B37M`, `B38M`, `B39M`, `B3XM`).
            if (f.StartsWith("B73", StringComparison.Ordinal) ||
                f.StartsWith("B37", StringComparison.Ordinal) ||
                f.StartsWith("B38", StringComparison.Ordinal) ||
                f.StartsWith("B39", StringComparison.Ordinal) ||
                f.StartsWith("B3X", StringComparison.Ordinal))
                return new[]
                {
                    new FlapBand(   200.0, "FLAPS UP"),
                    new FlapBand(  2248.0, "FLAPS 1"),
                    new FlapBand(  4296.0, "FLAPS 2"),
                    new FlapBand(  6344.0, "FLAPS 5"),
                    new FlapBand(  8392.0, "FLAPS 10"),
                    new FlapBand( 10240.0, "FLAPS 15"),
                    new FlapBand( 12288.0, "FLAPS 25"),
                    new FlapBand( 14336.0, "FLAPS 30"),
                    new FlapBand(RawFull + 1.0, "FLAPS 40"),
                };

            if (f.StartsWith("B74", StringComparison.Ordinal))   // B747, B744, B748
                return new[]
                {
                    new FlapBand(   300.0, "FLAPS UP"),
                    new FlapBand(  2730.0, "FLAPS 1"),
                    new FlapBand(  5460.0, "FLAPS 5"),
                    new FlapBand(  8190.0, "FLAPS 10"),
                    new FlapBand( 10920.0, "FLAPS 20"),
                    new FlapBand( 13650.0, "FLAPS 25"),
                    new FlapBand(RawFull + 1.0, "FLAPS 30"),
                };

            if (f.StartsWith("B77", StringComparison.Ordinal))   // B777, B77L, B77W…
                return new[]
                {
                    new FlapBand(   300.0, "FLAPS UP"),
                    new FlapBand(  2730.0, "FLAPS 1"),
                    new FlapBand(  5460.0, "FLAPS 5"),
                    new FlapBand(  8190.0, "FLAPS 15"),
                    new FlapBand( 10920.0, "FLAPS 20"),
                    new FlapBand( 13650.0, "FLAPS 25"),
                    new FlapBand(RawFull + 1.0, "FLAPS 30"),
                };

            return null;
        }

        /// <summary>
        /// ¿Es una familia Airbus de las que usan la escala CONF? El `A3xx` cubre A318–A321, A330,
        /// A340, A350 y A380; los tres neo van por su nombre. **El A220 no entra**: su designador es
        /// `BCS1`/`BCS3` y no usa la escala CONF.
        /// </summary>
        private static bool IsAirbusFamily(string family)
        {
            string f = (family ?? "").Trim().ToUpperInvariant();
            return f.StartsWith("A3", StringComparison.Ordinal)
                || f == "A19N" || f == "A20N" || f == "A21N";
        }

        /// <summary>La etiqueta de familia que corresponde a una compuerta de esa tabla.</summary>
        private static FlapLabelSource SourceFor(string aircraftFamily)
            => IsAirbusFamily(aircraftFamily) ? FlapLabelSource.AirbusConf
                                              : FlapLabelSource.BoeingDetent;

        private static FlapSetting PercentageOnly(double pct)
            => Build(pct, null, FlapLabelSource.PercentageOnly, isApproximate: false);

        private static FlapSetting Build(double pct, string label, FlapLabelSource source, bool isApproximate)
        {
            // Sin decimales a propósito: el porcentaje del mando se mueve en pasos de 0,006 % (1 de
            // 16383) y publicar décimas fingiría una resolución que la lectura no tiene.
            string percentText = string.Format(CultureInfo.InvariantCulture, "{0:0}%", pct);

            // Solo se marca como aproximada una etiqueta que salga de traducir el porcentaje: la que
            // sale del detente real (`0x0BFC`) es una lectura y va limpia. Sin etiqueta no hay nada
            // que marcar.
            bool approximate = label != null && isApproximate;
            string shortText = label != null ? (approximate ? "≈" + label : label) : percentText;

            return new FlapSetting
            {
                HasValue      = true,
                Percent       = pct,
                Label         = label,
                IsApproximate = approximate,
                Source        = source,
                PercentText   = percentText,
                ShortText     = shortText,
                Text          = label != null ? shortText + " (" + percentText + ")" : percentText,
                PirepText     = label != null
                                    ? (approximate ? "~" + label : label)
                                    : "FLAPS " + percentText,
            };
        }

        public override string ToString() => Text;
    }
}
