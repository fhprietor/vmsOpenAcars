namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Traduce el selector de autobrake, con **las tablas del documento de FSUIPC7** y en un solo sitio.
    ///
    /// **Lo que estaba mal (v0.9.49 y anteriores).** El cliente leía `0x0C46`, que la tabla documenta
    /// como **`AUTOBRAKES ACTIVE`** (una bandera de «está actuando») y que además **no** está marcada
    /// como `Responded`, o sea que en MSFS devuelve 0 siempre. Encima la decodificaba con la tabla del
    /// **selector**, que es otro offset, y luego `GetAutobrakeName` volvía a mapear el enum con **0 y 1
    /// cambiados** (`AutobrakeMode.Off = 0` → imprimía «RTO»). Resultado: `Autobrake: RTO` salía en
    /// **todos** los vuelos, se hubiera puesto RTO, MAX o nada — el mismo patrón que el spoiler, pero
    /// aquí las dos equivocaciones se compensaban y el resultado parecía razonable.
    ///
    /// **Las tablas buenas:**
    /// - `0x2F80` · `AUTO BRAKE SWITCH CB` · 1 byte · **`Responded`**: *«Panel autobrake switch. Read to
    ///   check setting»* → **0=RTO, 1=Off, 2=brake1, 3=brake2, 4=brake3, 5=max**.
    /// - `0x0260` · 2 bytes · (solo el A320neo de MSFS, **no** `Responded`): **0=Off, 1=Lo, 2=Med,
    ///   3=Max**; el valor vuelve a 0 después de aplicarse.
    /// - `0x0C46` · `AUTOBRAKES ACTIVE` · 1 byte · **no** `Responded`: dice si está **actuando**, nunca
    ///   cuál está seleccionado.
    ///
    /// **Sin dato no se inventa.** `0` es un valor válido del selector (RTO), así que un offset muerto
    /// no se distingue de «RTO puesto». La política es la de `SignalAvailability`: si el valor **no
    /// cambia en todo el vuelo**, se publica <see cref="NoData"/> en vez de «RTO».
    /// </summary>
    internal static class AutobrakeSelector
    {
        /// <summary>Lo que se publica cuando el addon no ha demostrado que publique el offset.</summary>
        internal const string NoData = "---";

        /// <summary>Selector del panel (`0x2F80`): 0=RTO, 1=Off, 2=brake1, 3=brake2, 4=brake3, 5=max.</summary>
        internal static string PanelLabel(int raw)
        {
            switch (raw)
            {
                case 0:  return "RTO";
                case 1:  return "OFF";
                case 2:  return "1";
                case 3:  return "2";
                case 4:  return "3";
                case 5:  return "MAX";
                default: return $"RAW={raw}";
            }
        }

        /// <summary>Selector del A320neo de MSFS (`0x0260`): 0=Off, 1=Lo, 2=Med, 3=Max.</summary>
        internal static string AirbusLabel(int raw)
        {
            switch (raw)
            {
                case 0:  return "OFF";
                case 1:  return "LO";
                case 2:  return "MED";
                case 3:  return "MAX";
                default: return $"RAW={raw}";
            }
        }

        /// <summary>¿La etiqueta representa un ajuste real (no `NoData` ni un `RAW=`)?</summary>
        internal static bool IsKnown(string label) =>
            !string.IsNullOrEmpty(label)
            && label != NoData
            && !label.StartsWith("RAW=");
    }
}
