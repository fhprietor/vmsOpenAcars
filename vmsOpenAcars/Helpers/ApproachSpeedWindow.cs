using System;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La ventana de velocidad de la compuerta de 1.000 ft AGL**: cuándo la velocidad del avión
    /// está donde debe estar en final.
    ///
    /// Hasta v0.9.70 la ventana salía **solo del tipo**
    /// (<see cref="AircraftPerformanceTable.GetApproachSpeedRange"/>), una tabla de categorías
    /// PANS-OPS: **[140–185] kt para cualquier wide-body**, sea un A339 a 400.000 lb o vacío. Eso
    /// contradice el dato que el propio cliente ya publica: el **Vref del plan** (SimBrief TLR,
    /// `tlr.landing.distance_dry.speeds_vref`) es **dependiente del peso** y viaja en
    /// `metrics.vref_kt` desde v0.9.38.
    ///
    /// Medido en el PIREP que abrió esto (ESSA→LIRF, A339, `vref_kt = 136`,
    /// `landing_weight_lbs = 400.100`): el piloto iba a **137 kt —Vref+1—** y la ventana de tipo le
    /// cobró −5 por «fuera de [140–185]». La ventana estaba **3 kt por encima de su propio Vref**: el
    /// avión no podía cumplirla sin volar por encima de su velocidad de referencia.
    ///
    /// Con Vref, la ventana es la que pidió phpVMS el 11/10/2026: **[Vref − 5, Vref + 15]**. El
    /// margen de arriba es ancho a propósito —absorbe la corrección de viento de la Vapp (1/3 del
    /// viento en cara: en un viento fuerte son 10–15 kt) y el pequeño exceso de una final con
    /// racha—. **No se le suma el viento aparte**: con `[−5, +15]` el margen ya lo cubre, y mover la
    /// ventana con la meteo haría que el mismo pilotaje puntuara distinto según el día, que es justo
    /// lo que no se puede auditar.
    ///
    /// **Sin Vref no se bloquea**: los planes sin TLR (o sin OFP) no traen Vref y ahí manda la tabla
    /// por tipo, como antes. Un dato que falta no puede inventarse ni suprimir la comprobación.
    /// </summary>
    internal static class ApproachSpeedWindow
    {
        /// <summary>
        /// Margen por debajo del Vref. Nadie vuela una final estabilizada más de 5 kt por debajo sin
        /// ir corto: ahí el avión ya está pidiendo potencia y el toque se queda antes del umbral.
        /// </summary>
        internal const int BelowKt = 5;

        /// <summary>
        /// Margen por encima del Vref. Cubre la corrección de viento de la Vapp (1/3 del viento en
        /// cara) sin meter el viento en la cuenta.
        /// </summary>
        internal const int AboveKt = 15;

        /// <summary>
        /// Rango plausible de un Vref, en nudos. Fuera de aquí el número no es un Vref —un plan
        /// corrupto, un campo en otra unidad— así que se degrada a la tabla por tipo en vez de
        /// construir una ventana sin sentido a su alrededor (un «Vref 5» daría [0–20] kt).
        /// </summary>
        internal const int PlausibleMinKt = 60;
        internal const int PlausibleMaxKt = 200;

        /// <summary>¿Este Vref sirve para construir la ventana? `null` o implausible ⇒ no.</summary>
        internal static bool IsUsable(int? vrefKt)
            => vrefKt.HasValue &&
               vrefKt.Value >= PlausibleMinKt &&
               vrefKt.Value <= PlausibleMaxKt;

        /// <summary>
        /// La ventana en nudos. **Con un Vref usable manda el Vref** (depende del peso, que es lo que
        /// se pedía); sin él, la tabla por tipo de siempre.
        /// </summary>
        internal static (int Min, int Max) For(int? vrefKt, string icaoType)
        {
            if (IsUsable(vrefKt))
                return (vrefKt.Value - BelowKt, vrefKt.Value + AboveKt);

            return AircraftPerformanceTable.GetApproachSpeedRange(icaoType);
        }
    }
}
