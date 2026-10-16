using System;
using System.Collections.Generic;
using System.Drawing;
using vmsOpenAcars.UI;

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
    ///
    /// Desde v0.9.45 el color es <see cref="LegendColor"/> y no un fondo: los botones son teclas de MCDU
    /// con el **cuerpo negro**, y este color es el que **enciende la leyenda** (el cuerpo no lleva color
    /// — ver `McduKeyButton` y `McduKeyRenderer`). Los valores salen de `McduLegend`, que es el convenio
    /// compartido con el resto de formularios, y no de una lista propia: así el verde significa lo mismo
    /// aquí que en un popup.
    /// </summary>
    internal sealed class MainButtonSpec
    {
        internal MainButtonSpec(string name, string labelKey, Color legendColor)
        {
            Name        = name;
            LabelKey    = labelKey;
            LegendColor = legendColor;
        }

        /// <summary>Nombre de código del botón; `MainForm` lo usa para enganchar su acción.</summary>
        internal string Name { get; }

        /// <summary>Clave del rótulo en `Languages/{es,en}.json`.</summary>
        internal string LabelKey { get; }

        /// <summary>Color con el que se ilumina la leyenda de la tecla (el antiguo color de fondo).</summary>
        internal Color LegendColor { get; }
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
    /// pide **866** px.
    ///
    /// **Y desde v0.9.45 el ancho se REPARTE (v0.9.45).** Antes cada tecla medía 90 px fijos y la barra
    /// dejaba **270 px muertos** a la derecha en la ventana de 1024: espacio que el piloto veía sin usar
    /// mientras `DESPACHO` y `BITÁCORA` se quedaban sin sitio. Ahora `ButtonWidthFor` reparte todo el ancho
    /// del panel entre las teclas, con <see cref="MinimumButtonWidth"/> como suelo, y `MainForm` recoloca
    /// en cada `Resize`. El suelo **no es estético**: al **150 %** de escalado de Windows —muy común en
    /// portátiles— `DESPACHO` y `BITÁCORA` ocupan **92,9 px** y con el aire de la leyenda piden **101**, así
    /// que una tecla de 90 los obligaba a encogerse… y solo a ellos, que es lo que el mantenedor vio
    /// («unos más pequeños que el resto»). Con 104 todas las leyendas caben **al mismo cuerpo de letra**.
    ///
    /// **Al añadir un botón hay que volver a hacer esa resta**: el test `MainButtonBarTests` la
    /// mantiene y falla si la barra deja de caber, que es lo que nadie comprobaba antes. Y hay una
    /// segunda comprobación —`ElAnchoDeCadaTeclaCubreElRotuloMasLargo`— que mide los rótulos reales de los
    /// dos idiomas al 150 % y falla si el ancho por tecla se queda corto.
    /// </summary>
    internal static class MainButtonBar
    {
        /// <summary>
        /// Ancho **mínimo** de una tecla: lo que necesita el rótulo más largo de la barra (`DESPACHO`,
        /// `BITÁCORA`, `CANCELAR`: 92,9 px al 150 %) más el aire de la leyenda a cada lado. El ancho real
        /// suele ser mayor, porque se reparte el panel (ver <see cref="ButtonWidthFor"/>).
        /// </summary>
        internal const int MinimumButtonWidth = 108;

        /// <summary>
        /// Cuerpo de letra de las leyendas de la barra, en puntos. Está aquí y no en `MainForm` porque la
        /// prueba del ancho tiene que medir **el mismo** que se pinta: con otro número, la comprobación
        /// del 150 % mediría algo que nadie ve.
        /// </summary>
        internal const float ButtonFontSize = 10f;

        internal const int ButtonHeight = 34;
        internal const int Spacing      = 4;
        internal const int StartX       = 6;

        /// <summary>
        /// Ancho mínimo de la ventana al que la barra tiene que caber: el `MinimumSize` de `MainForm`.
        /// Si algún día sube o baja, este número y el de allí tienen que moverse juntos — por eso el test
        /// lo comprueba contra el formulario real y no contra esta constante. Son 14 px más de lo que pide
        /// la barra (866), para no dejarla clavada.
        /// </summary>
        internal const int MinWindowWidth = 910;

        private static readonly MainButtonSpec[] _specs =
        {
            // El orden es el de la pantalla, de izquierda a derecha. El color es la leyenda de la tecla
            // (el cuerpo es negro) y sale de `McduLegend`: el convenio es el mismo en toda la aplicación.
            new MainButtonSpec("LOGIN",    "BtnLogin",     McduLegend.Info),
            new MainButtonSpec("OFP",      "BtnOfp",       McduLegend.Info),
            new MainButtonSpec("MAP",      "BtnMap",       McduLegend.Neutral),
            new MainButtonSpec("WEATHER",  "BtnWeather",   McduLegend.Neutral),
            new MainButtonSpec("DISPATCH", "BtnSimbrief",  McduLegend.Confirm),
            new MainButtonSpec("LOGBOOK",  "BtnLogbook",   McduLegend.Steel),
            new MainButtonSpec("START",    "BtnStartStop", McduLegend.Warning),
            new MainButtonSpec("CANCEL",   "BtnCancel",    McduLegend.Danger),
        };

        internal static IReadOnlyList<MainButtonSpec> Specs => _specs;

        /// <summary>Ancho que ocupa la barra entera, con el margen inicial y los huecos.</summary>
        internal static int RequiredWidth => RequiredWidthFor(_specs.Length);

        /// <summary>
        /// Ancho necesario para `buttonCount` botones. Está aparte de <see cref="RequiredWidth"/>
        /// para poder comprobar en el test cuánto costaría añadir uno más sin tener que añadirlo.
        /// </summary>
        internal static int RequiredWidthFor(int buttonCount)
            => buttonCount <= 0 ? 0 : StartX + buttonCount * MinimumButtonWidth + (buttonCount - 1) * Spacing;

        /// <summary>
        /// **El ancho de cada tecla, repartiendo el panel.** La barra ya no deja hueco muerto a la derecha:
        /// se lleva todo el ancho disponible menos el margen inicial y los huecos, y lo divide entre las
        /// teclas. <see cref="MinimumButtonWidth"/> es el suelo —por debajo, el rótulo más largo del barrido
        /// de idiomas no cabe al mismo cuerpo de letra que los demás—, así que en una ventana más estrecha
        /// que <see cref="MinWindowWidth"/> las teclas se quedan en el suelo en vez de desaparecer.
        /// </summary>
        internal static int ButtonWidthFor(int availableWidth, int buttonCount)
        {
            if (buttonCount <= 0) return 0;

            int usable = availableWidth - StartX - (buttonCount - 1) * Spacing;
            return Math.Max(MinimumButtonWidth, usable / buttonCount);
        }
    }
}
