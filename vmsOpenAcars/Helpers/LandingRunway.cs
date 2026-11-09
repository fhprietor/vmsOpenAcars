namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Qué pista se guarda en el registro del vuelo.** La columna salía vacía aunque la pista
    /// se conocía: en el PIREP real `nROPdk4GxYY5AOy1` el log dijo `02:56:46 APPROACH CAPTURE:
    /// RWY 08`, `02:58:27 FLARE TRACK START: RWY 08` y sin embargo a las `03:04:58` se guardó
    /// `Landing log: flight #51 saved (55 points, RWY )`.
    ///
    /// El dato del registro sale de `FlightManager.TouchdownRunwayName`, que es la geometría que
    /// rellena `TelemetryCoordinator.LookupRunwayData` en un `Task.Run` al detectar el toque. Si
    /// `FindTouchdownRunway` no encuentra el rectángulo de la pista (posición/rumbo fuera del
    /// footprint, o consulta que no llega), esa geometría **nunca se escribe** y el registro queda
    /// vacío aunque la aproximación ya hubiera resuelto la pista. Esta regla no inventa: si no hay
    /// ninguna de las dos, el registro va vacío.
    /// </summary>
    internal static class LandingRunway
    {
        /// <summary>
        /// La pista del toque si se resolvió; si no, la que ya conocía la aproximación. Vacío si no
        /// hay ninguna.
        /// </summary>
        internal static string ForRecord(string touchdownRunway, string approachRunway)
        {
            if (!string.IsNullOrWhiteSpace(touchdownRunway)) return touchdownRunway.Trim();
            if (!string.IsNullOrWhiteSpace(approachRunway))  return approachRunway.Trim();
            return "";
        }
    }
}
