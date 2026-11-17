using System;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// Lo que devuelve `POST /api/pireps/prefile`: el id del PIREP, su hora de creación en el servidor
    /// y —desde el aviso de ruta del 16/11/2026— el <see cref="RouteWarning"/> si la ruta planificada
    /// cruza un espacio aéreo restringido o prohibido.
    ///
    /// Existe como tipo propio y no como tupla de tres porque el aviso **puede ser `null`** y una
    /// tupla con un nulo posicional invita a confundirlo con «no vino». Aquí `RouteWarning == null`
    /// significa exactamente «no hay nada que avisar» (ruta limpia, sin OFP en el servidor, o área
    /// `Danger`), que es lo que dice el contrato.
    /// </summary>
    public sealed class PrefileResult
    {
        public string    PirepId         { get; set; }
        public DateTime  ServerCreatedAt { get; set; }
        public RouteWarning RouteWarning { get; set; }

        /// <summary>Atajo de lectura: el prefile funcionó si hay id.</summary>
        public bool Succeeded => !string.IsNullOrEmpty(PirepId);
    }
}
