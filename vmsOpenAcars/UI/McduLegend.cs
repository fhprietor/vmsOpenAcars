using System.Drawing;

namespace vmsOpenAcars.UI
{
    /// <summary>
    /// Los colores de **leyenda** de las teclas MCDU: el color ya no rellena el botón, lo enciende.
    ///
    /// Están aquí, en un solo sitio, porque son un **convenio** y no una decisión de cada formulario: el
    /// verde significa lo mismo en la barra principal que en un popup. Y son estos valores, no otros,
    /// porque cada uno está medido contra el fondo real de la tecla —el capuchón negro **con su lavado de
    /// color encendido**, que es más claro que el negro puro—: todos superan el 4,5:1 de la WCAG, y el que
    /// menos margen deja es <see cref="Danger"/> con 5,25:1. `MainButtonBarTests` lo comprueba para los
    /// seis, así que subir el lavado de la tecla o aclarar un color a ojo se nota en la suite antes de que
    /// se note en la pantalla de un piloto.
    /// </summary>
    internal static class McduLegend
    {
        /// <summary>Utilidad: cerrar, volver, filtros, casillas de la cabecera. El «blanco de tecla».</summary>
        internal static readonly Color Neutral = Color.FromArgb(166, 180, 196);

        /// <summary>Consulta o sesión: lo que trae datos de fuera (login, meteorología, API).</summary>
        internal static readonly Color Info = Color.FromArgb(74, 178, 255);

        /// <summary>Panel o registro: lo que abre una vista propia (bitácora, análisis, mapa).</summary>
        internal static readonly Color Steel = Color.FromArgb(138, 186, 226);

        /// <summary>Confirmación: aceptar, guardar, descargar, enviar. El verde del MCDU.</summary>
        internal static readonly Color Confirm = Color.FromArgb(0, 214, 110);

        /// <summary>La acción principal pendiente de armar (arrancar el vuelo). Ámbar.</summary>
        internal static readonly Color Warning = Color.FromArgb(255, 168, 32);

        /// <summary>Lo que cancela, borra o sale —incluido el aspa de cerrar—. Rojo.</summary>
        internal static readonly Color Danger = Color.FromArgb(255, 118, 110);
    }
}
