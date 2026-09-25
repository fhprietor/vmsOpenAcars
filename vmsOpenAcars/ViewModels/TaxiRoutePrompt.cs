using System;
using System.Collections.Generic;

namespace vmsOpenAcars.ViewModels
{
    /// <summary>
    /// Lo que necesita el popup de rodaje para poder pintarse: el aeropuerto, las pistas que
    /// ofrece, la que trae el OFP por defecto y la ruta sugerida por el grafo. `SuggestRoute`
    /// permite recalcularla si el piloto cambia de pista sin volver a pasar por el coordinador.
    /// </summary>
    internal sealed class TaxiRoutePrompt
    {
        public string       Icao          { get; set; }
        public string       DefaultRunway { get; set; }
        public List<string> Runways       { get; } = new List<string>();
        public string       SuggestedRoute { get; set; } = "";
        public Func<string, string> SuggestRoute { get; set; }

        /// <summary>De dónde salió el aviso ("taxi light" / "taxi out"), para el log.</summary>
        public string Reason { get; set; }
    }
}
