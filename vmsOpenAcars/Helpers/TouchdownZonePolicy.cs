using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Criterio «Touchdown Zone»**: cuántos puntos cuesta tocar lejos del umbral. Tres tramos
    /// (0 / 3 / 7 pts) —los mismos de siempre— pero **los dos umbrales los fija la pista**, no una
    /// constante.
    ///
    /// El porqué (decisión del mantenedor, 03/10/2026): el **punto de mira** está a
    /// <see cref="AimingPointFt"/> ft del umbral y la **zona de toma (TDZ)** se marca hasta los
    /// <see cref="TdzMarkingFt"/> ft —o la mitad de la pista si es más corta—, así que juzgar la
    /// toma con 1 500 / 2 500 ft para toda pista era **ciego a la longitud**: los mismos 2 900 ft
    /// de toma eran 3 puntos en los 12 467 ft de la 14R de SKBO y 7 puntos en los 5 577 ft de la 01
    /// de SKSM, cuando en la segunda eso es más de la mitad de la pista.
    ///
    /// Regla, con las constantes todas aquí para poder revertirla en un solo cambio:
    ///
    /// | Tramo | Con dato de longitud | Sin dato |
    /// |---|---|---|
    /// | **0 pts** | `min(3000, max(1500, 15 % L))` | 1 500 ft |
    /// | **3 pts** | hasta `min(3000, L/2)` | 2 500 ft |
    /// | **7 pts** | por encima de eso, siempre | por encima de 2 500 ft |
    ///
    /// **El techo de <see cref="TdzMarkingFt"/> no se negocia**: una pista larga no puede convertir
    /// en gratis una toma larga. Con 15 % a secas, una pista de 20 000 ft daría 3 000 ft de banda
    /// gratis, y una de 14 000 ft (los 4 350 m de la 18R de LEMD) daría 2 100 ft; el techo deja la
    /// banda de 0 puntos en 3 000 ft como mucho, así que a partir de ahí lo que manda es la TDZ
    /// marcada, no la longitud.
    ///
    /// **Sin dato de longitud se degrada a la regla de siempre** (1 500 / 2 500), que es la regla
    /// de la casa: no bloquear, decidir con lo que hay. Y como el suelo de la banda de 0 puntos es
    /// 1 500 ft, **el tramo de 0 puntos nunca es más estrecho que hoy**.
    ///
    /// Caso límite documentado: en una pista de menos de 3 000 ft, `L/2` cae por debajo del suelo
    /// de 1 500 ft y **el tramo de 3 puntos queda vacío** —el suelo de la banda de 0 puntos se
    /// evalúa primero, así que ≤1 500 ft sigue siendo 0 y por encima de 1 500 ft es 7—. Ninguna
    /// pista del corpus baja de 5 577 ft (SKSM), pero la regla se fija aquí para que no se
    /// descubra por sorpresa.
    /// </summary>
    internal static class TouchdownZonePolicy
    {
        // ─── Los tres tramos (no cambian: es el baremo aceptado) ──────────────────

        /// <summary>Puntos del tramo corto: dentro de la zona de toma.</summary>
        internal const int PointsZero = 0;

        /// <summary>Puntos del tramo medio.</summary>
        internal const int PointsThree = 3;

        /// <summary>Puntos del tramo largo.</summary>
        internal const int PointsSeven = 7;

        // ─── Las constantes de la regla ───────────────────────────────────────────

        /// <summary>
        /// Punto de mira: **1 000 ft** del umbral. Es el ancla de la regla — el avión debe tocar
        /// ahí, y de ahí para abajo está la zona de toma.
        /// </summary>
        internal const double AimingPointFt = 1000.0;

        /// <summary>
        /// Hasta dónde se marca la **zona de toma (TDZ)**: **3 000 ft**. Es también el techo de los
        /// dos umbrales, y no se negocia.
        /// </summary>
        internal const double TdzMarkingFt = 3000.0;

        /// <summary>Fracción de la pista que abre la banda de 0 puntos: **15 %**.</summary>
        internal const double ZeroBandFraction = 0.15;

        /// <summary>
        /// Suelo de la banda de 0 puntos, **1 500 ft**: la regla de la casa. Es lo que hace que el
        /// tramo de 0 puntos nunca sea más estrecho que el de hoy.
        /// </summary>
        internal const double ZeroBandFloorFt = 1500.0;

        /// <summary>
        /// Umbrales de la regla de siempre, que es la que decide **sin dato de longitud**
        /// (degradar sin datos, nunca bloquear).
        /// </summary>
        internal const double NoDataZeroBandFt = 1500.0;

        /// <inheritdoc cref="NoDataZeroBandFt"/>
        internal const double NoDataThreeBandFt = 2500.0;

        // ─── La regla ─────────────────────────────────────────────────────────────

        /// <summary>¿Hay dato de longitud de pista con el que aplicar la regla nueva?</summary>
        internal static bool HasRunwayLength(double runwayLengthFt) => runwayLengthFt > 0.0;

        /// <summary>
        /// Final de la banda de **0 puntos**: `min(3000, max(1500, 15 % L))`. Sin dato de longitud
        /// —o con un valor no utilizable— devuelve <see cref="NoDataZeroBandFt"/>.
        /// </summary>
        internal static double ZeroBandFt(double runwayLengthFt)
        {
            if (!HasRunwayLength(runwayLengthFt)) return NoDataZeroBandFt;
            double band = Math.Max(ZeroBandFloorFt, ZeroBandFraction * runwayLengthFt);
            return Math.Min(TdzMarkingFt, band);
        }

        /// <summary>
        /// Final de la banda de **3 puntos**: `min(3000, L/2)`. Sin dato de longitud devuelve
        /// <see cref="NoDataThreeBandFt"/>. Puede quedar **por debajo** de <see cref="ZeroBandFt"/>
        /// en pistas de menos de 3 000 ft; ver el caso límite en la cabecera.
        /// </summary>
        internal static double ThreeBandFt(double runwayLengthFt)
        {
            if (!HasRunwayLength(runwayLengthFt)) return NoDataThreeBandFt;
            return Math.Min(TdzMarkingFt, runwayLengthFt / 2.0);
        }

        /// <summary>
        /// **La decisión**: 0, 3 o 7 puntos para una toma a <paramref name="touchdownDistanceFt"/>
        /// del umbral en una pista de <paramref name="runwayLengthFt"/>.
        ///
        /// Función pura, sin red ni WinForms, con test. Los tramos se evalúan **en orden** (0, 3, 7):
        /// ver el caso límite de pistas cortas en la cabecera de la clase.
        /// </summary>
        internal static int PointsFor(double touchdownDistanceFt, double runwayLengthFt)
        {
            if (touchdownDistanceFt <= 0.0) return PointsZero;   // sin distancia: el criterio se omite
            if (touchdownDistanceFt <= ZeroBandFt(runwayLengthFt)) return PointsZero;
            if (touchdownDistanceFt <= ThreeBandFt(runwayLengthFt)) return PointsThree;
            return PointsSeven;
        }
    }
}
