namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Qué contestó el piloto al **aviso de ruta** del prefile. Existe como tipo propio porque hay
    /// **cuatro** desenlaces y no dos: aceptar («he revisado la ruta», y queda `fields.route_ack = 1`),
    /// corregir el plan (el vuelo no empieza), **cerrar el aviso** (sigue sin acuse: no se le puede
    /// atribuir una lectura que no confirmó) y **no poder preguntar** (la ventana no está disponible).
    ///
    /// **El aviso es previo**: ninguna de las dos últimas cancela nada. Solo la decisión explícita de
    /// corregir el plan detiene el vuelo, y la toma el piloto, no el programa.
    /// </summary>
    public enum RouteReviewAnswer
    {
        /// <summary>«He revisado la ruta»: se continúa y el PIREP lleva `route_ack = 1`.</summary>
        Acknowledge,

        /// <summary>«Corregir el plan»: el prefile se cancela y el vuelo no empieza.</summary>
        CorrectPlan,

        /// <summary>
        /// El piloto **cerró** el aviso sin contestar: se sigue, sin acuse. Distinto de
        /// <see cref="Unavailable"/> porque aquí sí lo vio, y eso se registra.
        /// </summary>
        Dismissed,

        /// <summary>No se pudo preguntar (sin ventana): se avisa en el registro y se continúa sin acuse.</summary>
        Unavailable,
    }
}
