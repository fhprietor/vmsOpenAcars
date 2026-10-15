using System.Collections.Generic;
using System.Drawing;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Un botón de la barra principal del ACARS: su nombre de código (con el que `MainForm` decide
    /// qué hace), la clave de idioma de su rótulo y su color.
    ///
    /// Los tres van **juntos** a propósito. Hasta v0.9.42 la barra se armaba con dos arrays paralelos
    /// —`buttonNames` y `buttonColors`— emparejados por índice: insertar un botón en medio desplazaba
    /// todos los colores siguientes sin que nada avisara, y el rótulo era un literal en inglés
    /// mientras las claves `Btn*` del archivo de idioma quedaban sin usar.
    /// </summary>
    internal sealed class MainButtonSpec
    {
        internal MainButtonSpec(string name, string labelKey, Color backColor)
        {
            Name      = name;
            LabelKey  = labelKey;
            BackColor = backColor;
        }

        /// <summary>Nombre de código del botón; `MainForm` lo usa para enganchar su acción.</summary>
        internal string Name { get; }

        /// <summary>Clave del rótulo en `Languages/{es,en}.json`.</summary>
        internal string LabelKey { get; }

        internal Color BackColor { get; }
    }

    /// <summary>
    /// **La barra de botones y el ancho que necesita**, que es una restricción real de la ventana y
    /// no un detalle estético: el panel no tiene `AutoScroll` y los botones se colocan con
    /// `Location` absoluta, así que **lo que no quepa en el ancho de la ventana no se puede pulsar**.
    ///
    /// Con los diez botones de hasta v0.9.42 eran `6 + 10×90 + 9×4 = 942` px contra un
    /// `MinimumSize` de **800**: a poco que el piloto encogiera la ventana, `START` y `CANCEL`
    /// quedaban fuera del panel. Se retiraron los dos que no hacían nada —
    /// `MENU` (su desplegable solo tenía «Settings» y «Test OSD», y los dos ya viven en el engranaje
    /// de la cabecera y en `SettingsForm`) y `ATIS` (no tenía `case` y caía en el manejador genérico,
    /// que solo escribía la pulsación en el log; el ATIS se pinta en `AtcPanel`)— y con ocho la barra
    /// pide **754** px: cabe en la ventana mínima con holgura.
    ///
    /// **Al añadir un botón hay que volver a hacer esa resta**: el test `MainButtonBarTests` la
    /// mantiene y falla si la barra deja de caber, que es lo que nadie comprobaba antes.
    /// </summary>
    internal static class MainButtonBar
    {
        internal const int ButtonWidth  = 90;
        internal const int ButtonHeight = 34;
        internal const int Spacing      = 4;
        internal const int StartX       = 6;

        /// <summary>
        /// Ancho mínimo de la ventana al que la barra tiene que caber: el `MinimumSize` de `MainForm`
        /// (800). Si algún día sube o baja, este número y el de allí tienen que moverse juntos — por
        /// eso el test lo comprueba contra el formulario real y no contra esta constante.
        /// </summary>
        internal const int MinWindowWidth = 800;

        private static readonly MainButtonSpec[] _specs =
        {
            // El orden es el de la pantalla, de izquierda a derecha.
            new MainButtonSpec("LOGIN",    "BtnLogin",     Color.FromArgb(  0, 120, 200)),
            new MainButtonSpec("OFP",      "BtnOfp",       Color.FromArgb(  0, 100, 200)),
            new MainButtonSpec("MAP",      "BtnMap",       Color.FromArgb( 60,  70,  80)),
            new MainButtonSpec("WEATHER",  "BtnWeather",   Color.FromArgb( 60,  70,  80)),
            new MainButtonSpec("DISPATCH", "BtnSimbrief",  Color.FromArgb(  0, 150,   0)),
            new MainButtonSpec("LOGBOOK",  "BtnLogbook",   Color.FromArgb( 60,  90, 110)),
            new MainButtonSpec("START",    "BtnStartStop", Color.FromArgb(200, 100,   0)),
            new MainButtonSpec("CANCEL",   "BtnCancel",    Color.FromArgb(150,   0,   0)),
        };

        internal static IReadOnlyList<MainButtonSpec> Specs => _specs;

        /// <summary>Ancho que ocupa la barra entera, con el margen inicial y los huecos.</summary>
        internal static int RequiredWidth => RequiredWidthFor(_specs.Length);

        /// <summary>
        /// Ancho necesario para `buttonCount` botones. Está aparte de <see cref="RequiredWidth"/>
        /// para poder comprobar en el test cuánto costaría añadir uno más sin tener que añadirlo.
        /// </summary>
        internal static int RequiredWidthFor(int buttonCount)
            => buttonCount <= 0 ? 0 : StartX + buttonCount * ButtonWidth + (buttonCount - 1) * Spacing;
    }
}
