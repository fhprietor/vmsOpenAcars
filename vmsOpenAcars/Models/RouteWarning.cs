namespace vmsOpenAcars.Models
{
    /// <summary>
    /// **El aviso de ruta del prefile**: la ruta planificada entra en un espacio aéreo restringido o
    /// prohibido, y el piloto tiene que enterarse **antes de despegar**, que es el único momento en el
    /// que todavía puede corregir el plan.
    ///
    /// Lo que lo motiva (mensaje de phpVMS del 16/11/2026): el vuelo **863 (SKBO–KATL)** entró en
    /// **SKR11 / BARRANQUILLA** —restringida, GND–FL999, permanente— porque el plan de SimBrief
    /// pasaba por dentro. El piloto aceptó el OFP sin mirarlo y el ACARS solo lo detectó **en vuelo**
    /// (17:30:55), con el «AHEAD» dos minutos antes; al presentar el PIREP ya no había nada que
    /// corregir. El aviso del prefile llega a tiempo y **no bloquea** nada: es información.
    ///
    /// Viaja en el recurso del PIREP (`route_warning`) y es **`null`** cuando no hay nada que avisar:
    /// ruta limpia, sin OFP, o área de tipo **Danger** —que es advertencia, no prohibición—.
    ///
    /// La misma forma la usa la comprobación **local** del cliente (`Helpers/RouteAirspaceCheck`), que
    /// se calcula con el `navlog` del OFP y los polígonos que NavData ya nos dio para la ruta; en ese
    /// caso <see cref="FromServer"/> es `false` y el aviso se marca como propio, para que el piloto
    /// sepa de dónde sale.
    /// </summary>
    public sealed class RouteWarning
    {
        /// <summary>El área tal como la publica NavData: `SKR11 / BARRANQUILLA`.</summary>
        public string Area { get; set; }

        /// <summary>Los límites verticales: `GND–FL999`, `GND–3000ft MSL`…</summary>
        public string Limits { get; set; }

        /// <summary>El tramo de la ruta que cruza: `GIKPU–PADUD`.</summary>
        public string Leg { get; set; }

        /// <summary>Qué pide el servidor: `revisar_ruta`. No se interpreta: se enseña.</summary>
        public string Action { get; set; }

        /// <summary>Tipo del área (`Restricted`, `Prohibited`). Informativo; la decisión ya está tomada.</summary>
        public string Type { get; set; }

        /// <summary>
        /// `true` si el aviso vino en la respuesta del prefile; `false` si lo encontró la comprobación
        /// local del cliente. Sirve para decírselo al piloto y para no atribuirle al servidor algo que
        /// decidimos aquí.
        /// </summary>
        public bool FromServer { get; set; }

        /// <summary>
        /// El **motivo** del aviso local cuando no es un cruce limpio: p. ej. que el tramo no traiga
        /// altitud y la comprobación vertical no se haya podido hacer. `null` en el aviso del servidor.
        /// </summary>
        public string Note { get; set; }

        public override string ToString() => Describe();

        /// <summary>Una línea para el registro: `SKR11 / BARRANQUILLA (GND–FL999) GIKPU–PADUD`.</summary>
        public string Describe()
        {
            string area   = string.IsNullOrWhiteSpace(Area)   ? "?" : Area.Trim();
            string limits = string.IsNullOrWhiteSpace(Limits) ? "" : " (" + Limits.Trim() + ")";
            string leg    = string.IsNullOrWhiteSpace(Leg)    ? "" : " " + Leg.Trim();
            return area + limits + leg;
        }
    }
}
