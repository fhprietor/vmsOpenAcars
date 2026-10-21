namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **De dónde salió el designador ICAO de la aeronave del simulador.** No es un detalle de
    /// fontanería: decide cuánto se puede afirmar de él.
    ///
    /// - <see cref="AtcModel"/> — lo publicó el simulador en el **modelo ATC** (`0x0618`). Es un dato
    ///   del avión. En MSFS trae la familia (`B777`, `B737`) y el título la afina a la variante.
    /// - <see cref="AcfFile"/> — X-Plane: sale del **`_ICAO` del `.acf` del avión cargado**. También es
    ///   un dato del avión, y **más preciso que el título**, que en X-Plane es el nombre de la carpeta.
    /// - <see cref="TitleGuess"/> — el simulador **no publica** el tipo y lo deducimos del título. Es
    ///   una suposición: sirve para enseñarlo en el log, nunca para comparar variantes.
    ///
    /// El caso que obligó a separarlas (medido en vivo el 20/10/2026 con el ToLiss A320neo cargado en
    /// X-Plane 12.4.3 + XPUIPC 2.0.3.8): los offsets `0x0600`, `0x0618`, `0x0630`, `0x09D2`, `0x0B26` y
    /// `0x3138` devuelven **cadena vacía**, y `0x3D00` devuelve la **carpeta** (`ToLissA320_V1p2p1`).
    /// De ahí salía «A320» por contener esa subcadena, y el avión —que es un **A20N** según su `.acf`—
    /// se marcaba como discrepancia contra un OFP de A20N.
    /// </summary>
    public enum AircraftTypeSource
    {
        /// <summary>Sin dato, o fila anterior a que se guardara el origen (se trata como <see cref="AtcModel"/>).</summary>
        Unknown = 0,

        /// <summary>Modelo ATC del simulador (`0x0618`): un dato del avión. El título puede afinarlo.</summary>
        AtcModel = 1,

        /// <summary>`_ICAO` del `.acf` cargado (X-Plane): la ficha del avión. El título NO lo afina.</summary>
        AcfFile = 2,

        /// <summary>Deducido del título porque el simulador no publica el tipo: una suposición.</summary>
        TitleGuess = 3
    }
}
