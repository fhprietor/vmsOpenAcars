namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Crédito de las teselas que se están pintando. **Es una obligación de las fuentes, no un
    /// adorno**: las condiciones de las basemaps de CARTO exigen acreditar «CARTO and OpenStreetMap»
    /// en cada mapa, y las de ESRI su imagen. Hasta v0.9.16 el cliente no mostraba ningún crédito.
    ///
    /// GMap.NET ya trae el mecanismo —cada proveedor publica su campo `Copyright`, y los suyos lo
    /// llenan—, así que lo que decide esta clase es sólo **qué enseñar cuando un proveedor no lo
    /// trae**: se acreditan las tres fuentes. Acreditar de más no incumple nada; de menos, sí. La
    /// tabla de tests impide que ese caso llegue a darse con los proveedores del combo.
    /// </summary>
    internal static class MapAttribution
    {
        /// <summary>Crédito de las basemaps de CARTO (los dos estilos: son las mismas fuentes).</summary>
        internal const string CartoCredit = "© OpenStreetMap contributors · © CARTO";

        /// <summary>Crédito de la imagen satelital de ESRI, que es distinto del de CARTO.</summary>
        internal const string EsriCredit = "© Esri, Maxar, Earthstar Geographics";

        internal const string Fallback =
            "© OpenStreetMap contributors · © CARTO · © Esri, Maxar, Earthstar Geographics";

        /// <summary>El crédito del proveedor, o el de todas las fuentes si no publica ninguno.</summary>
        internal static string For(string providerCopyright)
            => string.IsNullOrWhiteSpace(providerCopyright) ? Fallback : providerCopyright.Trim();
    }
}
