namespace vmsOpenAcars.Helpers
{
    /// <summary>El aeropuerto contra el que se comprueba la posición del simulador.</summary>
    internal sealed class PositionReference
    {
        internal string Airport;
        internal double Lat;
        internal double Lon;
    }

    /// <summary>
    /// **Contra qué aeropuerto se valida la posición del simulador.** La comparación existía para
    /// gatear el START —«el avión está en el campo que tengo asignado»— y se hacía siempre contra
    /// la **base del piloto**, que después de aterrizar ya no es la referencia correcta.
    ///
    /// En el PIREP real `nROPdk4GxYY5AOy1` (AT76, **SKBO→SKPE**) la secuencia fue
    /// `03:04:58 PIREP filed` → `03:05:00 ⚠️ YOU SHOULD BE AT SKBO (95.4 NM)` →
    /// `03:05:03 VALID POSITION SKPE (0.2 NM)` → `03:05:03 Home base updated: SKPE`. Estaba
    /// comparando el avión, ya aparcado en **SKPE**, con la base **SKBO** que phpVMS todavía tenía
    /// sin actualizar, y lo hacía **después** de terminar el vuelo. Aquí se decide:
    ///
    /// 1. **Vuelo cerrado** (fileado o en bloque): no hay nada que comprobar, no se avisa.
    /// 2. **Ya aterrizó**: el aeropuerto de llegada —la posición del toque es la única coordenada
    ///    de llegada que tenemos—, nunca la base.
    /// 3. **Antes de aterrizar**: la base, que es el campo asignado y el que habilita el START.
    ///
    /// Sin coordenadas no se inventa nada: devuelve `null` y el aviso no sale (degradar sin datos,
    /// nunca bloquear por una suposición).
    /// </summary>
    internal static class PositionCheckPolicy
    {
        internal static PositionReference Resolve(
            bool    flightClosed,
            string  arrivalAirport, double? arrivalLat, double? arrivalLon,
            string  baseAirport,    double? baseLat,    double? baseLon)
        {
            // 1) El vuelo ya terminó: comparar la posición con cualquier campo solo puede producir
            //    un aviso falso, y en el log lo produjo (SKBO a 95,4 NM con el avión en SKPE).
            if (flightClosed) return null;

            // 2) Aeronave en el campo de llegada: la referencia es ese campo, no la base.
            if (!string.IsNullOrEmpty(arrivalAirport)
                && arrivalLat.HasValue && arrivalLon.HasValue)
            {
                return new PositionReference
                {
                    Airport = arrivalAirport, Lat = arrivalLat.Value, Lon = arrivalLon.Value
                };
            }

            // 3) Todavía no ha aterrizado (o no hay ancla de llegada): la base es el campo
            //    asignado y lo que el piloto tiene delante al preparar el vuelo.
            if (baseLat.HasValue && baseLon.HasValue)
                return new PositionReference
                {
                    Airport = baseAirport, Lat = baseLat.Value, Lon = baseLon.Value
                };

            return null;
        }
    }
}
