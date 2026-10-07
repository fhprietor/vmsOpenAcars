using System;
using System.Globalization;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El closeup del aterrizaje ya decidido**: qué tramo se ve y dónde caen las marcas.
    ///
    /// Lo consume `LandingAnalysisForm` para pintar el perfil vertical ampliado —umbral, pista,
    /// bandas de la zona de toma y punto de toque—, y **no decide nada por su cuenta**: el
    /// formulario solo traduce estos números a píxeles. Es `internal`, sin WinForms y sin red,
    /// como `GeoMath` o `TouchdownZonePolicy`, para poder fijarlo con tests.
    ///
    /// **Convenio de signos, el mismo del perfil vertical**: la X del gráfico son pies respecto al
    /// umbral, **positivo antes del umbral** (como <see cref="Models.ApproachTrackPoint.DistNm"/>,
    /// que es positivo acercándose) y **negativo pasado el umbral**. Por eso la pista y el toque se
    /// dibujan en X negativa: están *después* del umbral y salen del encuadre por la derecha.
    /// </summary>
    internal sealed class TouchdownCloseup
    {
        private TouchdownCloseup() { }

        // ── El encuadre ───────────────────────────────────────────────────────────

        /// <summary>Pies vistos **antes** del umbral (borde izquierdo: el eje X está invertido).</summary>
        internal double BeforeFt { get; private set; }

        /// <summary>Pies vistos **pasado** el umbral (borde derecho).</summary>
        internal double AfterFt { get; private set; }

        // ── El punto de toque ─────────────────────────────────────────────────────

        /// <summary>Hay distancia de toma utilizable (`touchdown_dist_ft &gt; 0`).</summary>
        internal bool HasTouchdown { get; private set; }

        /// <summary>Pies pasados el umbral; `NaN` sin dato.</summary>
        internal double TouchdownFt { get; private set; }

        /// <summary>
        /// El toque cae **dentro** del encuadre. Si es falso no se marca nada: colocar la marca en
        /// el borde sería inventar una posición que la base no tiene.
        /// </summary>
        internal bool TouchdownInView { get; private set; }

        /// <summary>X del toque en el gráfico (= −<see cref="TouchdownFt"/>); `NaN` si no se pinta.</summary>
        internal double TouchdownX { get; private set; }

        /// <summary>Puntos del criterio «Touchdown Zone» para esa toma (0 si no hay toque).</summary>
        internal int TouchdownPoints { get; private set; }

        /// <summary>Rótulo de la marca: `TD 1,736 ft from threshold`. Vacío sin toque.</summary>
        internal string TouchdownLabel { get; private set; }

        // ── La pista ──────────────────────────────────────────────────────────────

        /// <summary>Hay longitud de pista publicada por NavData.</summary>
        internal bool HasRunwayLength { get; private set; }

        /// <summary>Longitud real de la pista en pies (0 sin dato).</summary>
        internal double RunwayLengthFt { get; private set; }

        /// <summary>
        /// Pies de pista que entran en el encuadre: `min(longitud, AfterFt)`. **Cero sin dato de
        /// longitud**: no se dibuja una pista de largo inventado.
        /// </summary>
        internal double RunwayVisibleFt { get; private set; }

        /// <summary>Se puede pintar la línea de pista.</summary>
        internal bool RunwayVisible { get; private set; }

        /// <summary>La pista no cabe en el encuadre y sale por la derecha (lo correcto).</summary>
        internal bool RunwaySpillsRight { get; private set; }

        /// <summary>`RWY 7,841 ft`, o vacío sin dato.</summary>
        internal string RunwayLabel { get; private set; }

        // ── Las bandas de la zona de toma ─────────────────────────────────────────

        /// <summary>Final de la banda de 0 puntos, en pies pasados el umbral.</summary>
        internal double ZeroBandFt { get; private set; }

        /// <summary>Final de la banda de 3 puntos, en pies pasados el umbral.</summary>
        internal double ThreeBandFt { get; private set; }

        /// <summary>Línea compacta para el panel del formulario, con las cifras que se pintan.</summary>
        internal string Summary { get; private set; }

        internal static TouchdownCloseup Build(double beforeFt, double afterFt,
                                               bool hasTouchdown, double touchdownFt,
                                               bool hasRunwayLength, double runwayLengthFt,
                                               double zeroBandFt, double threeBandFt)
        {
            bool inView = hasTouchdown && touchdownFt <= afterFt;

            var c = new TouchdownCloseup
            {
                BeforeFt        = beforeFt,
                AfterFt         = afterFt,
                HasTouchdown    = hasTouchdown,
                TouchdownFt     = touchdownFt,
                TouchdownInView = inView,
                TouchdownX      = inView ? -touchdownFt : double.NaN,
                HasRunwayLength = hasRunwayLength,
                RunwayLengthFt  = runwayLengthFt,
                // Sin encuadre pasado el umbral no entra ni un pie de pista: no hay línea que pintar,
                // aunque la longitud se conozca (por eso no basta con `hasRunwayLength`).
                RunwayVisible   = hasRunwayLength && afterFt > 0.0,
                RunwayVisibleFt = hasRunwayLength ? Math.Min(runwayLengthFt, afterFt) : 0.0,
                ZeroBandFt      = zeroBandFt,
                ThreeBandFt     = threeBandFt,
            };

            c.TouchdownPoints = hasTouchdown ? TouchdownZonePolicy.PointsFor(touchdownFt, runwayLengthFt) : 0;
            c.RunwayVisible   = c.RunwayVisible && c.RunwayVisibleFt > 0.0;
            c.RunwaySpillsRight = c.RunwayVisible && runwayLengthFt > afterFt;
            c.TouchdownLabel  = hasTouchdown
                ? string.Format(CultureInfo.InvariantCulture, "TD {0:N0} ft from threshold", touchdownFt)
                : "";
            c.RunwayLabel     = hasRunwayLength
                ? string.Format(CultureInfo.InvariantCulture, "RWY {0:N0} ft", runwayLengthFt)
                : "";
            c.Summary         = BuildSummary(c);
            return c;
        }

        private static string BuildSummary(TouchdownCloseup c)
        {
            string runway = c.HasRunwayLength ? c.RunwayLabel : "RWY —";
            string zone = string.Format(CultureInfo.InvariantCulture,
                "TDZ 0 pts ≤ {0:N0} ft · 3 pts ≤ {1:N0} ft", c.ZeroBandFt, c.ThreeBandFt);

            string td;
            if (!c.HasTouchdown)
                td = "TD —";
            else if (!c.TouchdownInView)
                td = string.Format(CultureInfo.InvariantCulture,
                    "{0} → beyond this view", c.TouchdownLabel);
            else
                td = string.Format(CultureInfo.InvariantCulture,
                    "{0} → {1} pts", c.TouchdownLabel, c.TouchdownPoints);

            string spill = c.RunwaySpillsRight ? "  (runway continues past the frame)" : "";
            return string.Format(CultureInfo.InvariantCulture, "{0}  ·  {1}  ·  {2}{3}",
                                 runway, zone, td, spill);
        }
    }

    /// <summary>
    /// **El encuadre del closeup**, con las dos escalas que justifican los datos.
    ///
    /// El porqué de las dos escalas está en las cifras de la base local (41 vuelos con traza,
    /// muestreo de **2 s** ≈ **500 ft por muestra** a 150 kt de GS):
    ///
    /// | Rango | Muestras del tramo (min–max, media) | Toques que caben |
    /// |---|---|---|
    /// | Últimos 5 000 ft antes del umbral | 4 – 13 (9,0) | — |
    /// | Primeros 4 500 ft pasados | 1 – 9 (3,9) | **31 de 32** (`touchdown_dist_ft ≤ 4 500`) |
    /// | Últimos 2 500 ft antes | 2 – 7 (4,7) | — |
    /// | Primeros 2 500 ft pasados | 1 – 6 (3,6) | **24 de 32** |
    ///
    /// La escala «de cerca» es de **2 500 ft y no de 1 500**: con 1 500 ft el tramo se queda en
    /// **2–4 muestras** y el punto de toque solo entra en **9 de 32** aterrizajes (la toma media del
    /// corpus está en **2 077 ft**, con 503 ft la más corta y 5 898 ft la más larga). Una escala que
    /// esconde justo lo que se quiere mirar no sirve.
    ///
    /// Y **el rango no se estira solo**: si el toque queda fuera, el formulario lo dice en vez de
    /// mover la marca. El caso real es el vuelo **11** (SKCL→KJFK 22L, toque a **5 898 ft**): en la
    /// escala de 4 500 ft queda fuera y no se dibuja marca, pero el rótulo sigue dando la distancia.
    /// </summary>
    internal static class TouchdownCloseupGeometry
    {
        /// <summary>Pies por milla náutica. Es la misma conversión que usa la telemetría
        /// (`1852 m × 3.28084 = 6 076,12 ft`), para que la X en pies de este gráfico cuadre con las
        /// distancias en NM de la traza y con `touchdown_dist_ft`.</summary>
        internal const double FeetPerNm = 6076.12;

        /// <summary>Escala ancha: últimos 5 000 ft antes del umbral.</summary>
        internal const double WideBeforeFt = 5000.0;

        /// <summary>Escala ancha: primeros 4 500 ft pasados el umbral (cubre el 97 % de las tomas).</summary>
        internal const double WideAfterFt = 4500.0;

        /// <summary>Escala de cerca: últimos 2 500 ft antes del umbral.</summary>
        internal const double CloseBeforeFt = 2500.0;

        /// <summary>Escala de cerca: primeros 2 500 ft pasados el umbral (cubre el 75 % de las tomas).</summary>
        internal const double CloseAfterFt = 2500.0;

        /// <summary>
        /// Escala fina: últimos **1 000 ft** antes del umbral. **No se ofrece siempre**, y el porqué
        /// es el muestreo: con la traza de 2 s (± 550 ft por muestra) este encuadre se queda en
        /// **2–3 puntos** y no hay curva que mirar — sería una escala que promete detalle y no lo
        /// tiene—. Solo entra cuando el vuelo tiene la traza del flare (**10 Hz**, ~1 muestra cada
        /// 25 ft a 150 kt), que es quien la sostiene; con la de 2 s el formulario no la ofrece.
        /// </summary>
        internal const double FineBeforeFt = 1000.0;

        /// <inheritdoc cref="FineBeforeFt"/>
        internal const double FineAfterFt = 1000.0;

        /// <summary>
        /// **La decisión**, a partir de lo que hay en la base. Función pura.
        ///
        /// Degrada sin datos en los tres casos que importan: **sin distancia de toma** no hay marca
        /// (pero sí encuadre, pista y bandas); **sin longitud de pista** no hay línea de pista ni se
        /// inventa su largo —las bandas caen a la regla de la casa, 1 500/2 500, que es la que se
        /// puntúa—; y **toque más allá del encuadre** deja `TouchdownInView` en falso y
        /// `TouchdownX` en `NaN`, que es como se prohíbe pintarlo donde no está.
        /// </summary>
        internal static TouchdownCloseup Compute(double touchdownDistFt, double runwayLengthFt,
                                                 double beforeFt, double afterFt)
        {
            double before = beforeFt > 0.0 ? beforeFt : 0.0;
            double after  = afterFt  > 0.0 ? afterFt  : 0.0;

            bool hasTouchdown = !double.IsNaN(touchdownDistFt)
                                && !double.IsInfinity(touchdownDistFt)
                                && touchdownDistFt > 0.0;

            // `TouchdownZonePolicy` es la única definición de las bandas: si allí cambia el
            // criterio, el sombreado del closeup cambia con él y no puede desincronizarse.
            double runwayForPolicy = TouchdownZonePolicy.HasRunwayLength(runwayLengthFt) ? runwayLengthFt : 0.0;

            return TouchdownCloseup.Build(
                before, after,
                hasTouchdown, hasTouchdown ? touchdownDistFt : double.NaN,
                TouchdownZonePolicy.HasRunwayLength(runwayLengthFt), runwayForPolicy,
                TouchdownZonePolicy.ZeroBandFt(runwayForPolicy),
                TouchdownZonePolicy.ThreeBandFt(runwayForPolicy));
        }
    }
}
