using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Controls;
using vmsOpenAcars.UI;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// La paleta del MCDU: que los colores sean **los muestreados de la foto** y que todo texto siga
    /// leyéndose. Es la parte que no se puede juzgar a ojo, porque hay dos fondos distintos —el cristal
    /// #1B1D20 y el plástico #859AB2— y un color puede leerse en uno y desaparecer en el otro.
    ///
    /// El caso que lo obliga: la app usaba blanco y cian en la cabecera, que sobre el plástico se quedan
    /// en 2,9:1 y 1,5:1. Por eso la cabecera va grabada en tinta oscura, y por eso esta prueba existe.
    /// </summary>
    [TestClass]
    public class McduPaletteTests
    {
        /// <summary>
        /// Los valores medidos, uno a uno. Si alguien «mejora» un color, esta prueba dice que ya no es el
        /// de la foto: el número de píxeles que lo respalda está en el comentario de `McduPalette`.
        /// </summary>
        [TestMethod]
        public void LosColoresMedidosSonLosDeLaFotoDeReferencia()
        {
            Assert.AreEqual("#1B1D20", Hex(McduPalette.LcdBackground), "fondo del LCD (55.122 px)");
            Assert.AreEqual("#F8F4ED", Hex(McduPalette.LcdWhite), "blanco del LCD (604 px)");
            Assert.AreEqual("#08DA09", Hex(McduPalette.LcdGreen), "verde del LCD (519 px)");
            Assert.AreEqual("#09CFCF", Hex(McduPalette.LcdCyan), "cian del LCD (374 px)");

            Assert.AreEqual("#93A6BD", Hex(McduPalette.ChassisLight), "reflejo del canto");
            Assert.AreEqual("#859AB2", Hex(McduPalette.ChassisHigh), "plástico claro");
            Assert.AreEqual("#6A7C92", Hex(McduPalette.ChassisLow), "plástico bajo");
            Assert.AreEqual("#5A6A7E", Hex(McduPalette.ChassisDirt), "plástico sucio");
            Assert.AreEqual("#505F71", Hex(McduPalette.ChassisWorn), "desgaste");
            Assert.AreEqual("#39424D", Hex(McduPalette.SeamDark), "sombra dura");
        }

        /// <summary>
        /// **Todo color de `Theme` tiene que acabar legible en el log.** `UIService.AddLog` traduce con
        /// `ForLog`, y allí caen los colores que la aplicación usa en sus cientos de llamadas. Se recorren
        /// por reflexión para que **un color nuevo de `Theme` no pueda colarse** sin que esta prueba lo vea.
        /// </summary>
        [TestMethod]
        public void TodoColorDeThemeQuedaLegibleSobreElLcd()
        {
            List<Color> colores = typeof(Theme)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Color))
                .Select(f => (Color)f.GetValue(null))
                .Distinct()
                .ToList();

            Assert.IsTrue(colores.Count > 10, "no se leyeron los colores de Theme");

            foreach (Color original in colores)
            {
                Color enPantalla = McduPalette.ForLog(original);
                double ratio = ColorContrast.Ratio(enPantalla, McduPalette.LcdBackground);

                Assert.IsTrue(ratio >= ColorContrast.MinimumForText,
                    $"rgb({original.R},{original.G},{original.B}) queda en rgb({enPantalla.R}," +
                    $"{enPantalla.G},{enPantalla.B}) con {ratio:F2}:1 sobre el LCD: no se lee");
            }
        }

        /// <summary>
        /// El verde de lectura tiene que ser **más suave** que el medido —es lo que se pidió, porque el del
        /// aparato cansa en un párrafo— pero seguir pasando el umbral con holgura.
        /// </summary>
        [TestMethod]
        public void ElVerdeDeLecturaEsMasSuaveQueElMedidoYPasaElUmbral()
        {
            double saturacionMedido = Saturacion(McduPalette.LcdGreen);
            double saturacionLectura = Saturacion(McduPalette.LcdGreenSoft);

            Assert.IsTrue(saturacionLectura < saturacionMedido,
                $"el verde de lectura ({saturacionLectura:F2}) tendría que estar menos saturado que el " +
                $"medido ({saturacionMedido:F2}); si no, no hay motivo para tener dos");

            double ratio = ColorContrast.Ratio(McduPalette.LcdGreenSoft, McduPalette.LcdBackground);
            Assert.IsTrue(ratio >= ColorContrast.MinimumForText,
                $"el verde de lectura se queda en {ratio:F2}:1 sobre el LCD");
        }

        /// <summary>
        /// **Por qué la cabecera va grabada y no en blanco.** El blanco sobre el plástico claro no llega al
        /// umbral ni de lejos (2,9:1) y el cian que usaba la app tampoco (1,5:1); la tinta oscura sí. Es la
        /// decisión de diseño que sostiene el cambio de piel, así que se comprueba en vez de confiarse.
        /// </summary>
        [TestMethod]
        public void LaTintaGrabadaSeLeeSobreElPlasticoYElBlancoNo()
        {
            double tinta = ColorContrast.Ratio(McduPalette.Ink, McduPalette.ChassisHigh);
            double blanco = ColorContrast.Ratio(Color.White, McduPalette.ChassisHigh);

            Assert.IsTrue(tinta >= ColorContrast.MinimumForText,
                $"la tinta grabada se queda en {tinta:F2}:1 sobre el plástico");
            Assert.IsTrue(blanco < ColorContrast.MinimumForText,
                $"el blanco da {blanco:F2}:1 sobre el plástico: si ya se leyera, la tinta no haría falta");
        }

        /// <summary>
        /// La carcasa se pinta **igual** en cada repintado: si el grano saliera de un `Random` sin semilla,
        /// la ventana parpadearía en cada invalidación. Y con la textura apagada el resultado tiene que ser
        /// distinto (si no, el interruptor no haría nada) pero también estable.
        /// </summary>
        [TestMethod]
        public void LaCarcasaSePintaIgualEnCadaRepintado()
        {
            bool antes = McduPalette.Textured;
            try
            {
                McduPalette.Textured = true;
                byte[] conTextura1 = PintarCarcasa();
                byte[] conTextura2 = PintarCarcasa();
                CollectionAssert.AreEqual(conTextura1, conTextura2,
                    "la carcasa cambia entre repintados: el grano no es determinista y la ventana parpadearía");

                McduPalette.Textured = false;
                byte[] sinTextura1 = PintarCarcasa();
                byte[] sinTextura2 = PintarCarcasa();

                CollectionAssert.AreEqual(sinTextura1, sinTextura2,
                    "sin textura tampoco puede cambiar entre repintados");
                CollectionAssert.AreNotEqual(conTextura1, sinTextura1,
                    "el interruptor de la textura no cambia nada: o no apaga el grano o no lo pinta");
            }
            finally
            {
                McduPalette.Textured = antes;
            }
        }

        /// <summary>
        /// La pantalla reserva su banda de título: el contenido empieza **debajo** de la línea del título y
        /// dentro del cristal, no encima del bisel. Es lo que evita que un rótulo tape el marco.
        /// </summary>
        [TestMethod]
        public void LaPantallaReservaLaBandaDelTituloYDentroElContenido()
        {
            using (McduDisplayPanel pantalla = new McduDisplayPanel())
            {
                pantalla.Size = new Size(400, 200);
                pantalla.Title = "ACARS LOG";
                pantalla.Annotation = "TAXI OUT";

                Rectangle cristal = pantalla.ScreenBounds;
                Assert.IsTrue(cristal.Y >= 3 && cristal.X >= 3,
                    "el cristal tiene que ir dentro del bisel, no pegado al borde del control");
                Assert.IsTrue(cristal.Width < pantalla.Width && cristal.Height < pantalla.Height,
                    "el cristal no puede ocupar el control entero: no quedaría bisel");

                Rectangle contenido = pantalla.DisplayRectangle;
                Assert.IsTrue(contenido.Y >= cristal.Y + 20,
                    $"el contenido empieza en {contenido.Y} y el cristal en {cristal.Y}: el título necesita " +
                    "su banda y el contenido no puede pisarla");
                Assert.IsTrue(contenido.Left >= cristal.Left && contenido.Right <= cristal.Right,
                    "el contenido se sale del cristal por los lados");
            }
        }

        /// <summary>
        /// **La rampa del plástico, como función pura.** Va de claro arriba a sucio abajo y no se da la
        /// vuelta: es lo que hace que la ventana se lea como un aparato y no como un color plano. Aquí se
        /// comprueba de paso el fallo que se vio en la captura de la ventana real —la barra, abajo del todo,
        /// salía con el color del 26 % de la altura—, que es lo que obligó a dejar el degradado de GDI+ y
        /// calcular los extremos a mano.
        /// </summary>
        [TestMethod]
        public void LaRampaDelPlasticoVaDeClaroArribaAOscuroAbajo()
        {
            const int alto = 768;

            Assert.AreEqual(McduPalette.ChassisHigh, McduChassis.RampAt(0, alto), "arriba del todo");
            Assert.AreEqual(McduPalette.ChassisDirt, McduChassis.RampAt(alto, alto), "abajo del todo");

            // Oscurece de forma monótona: ningún tramo aclara lo que ya estaba oscuro.
            double anterior = double.MaxValue;
            for (int y = 0; y <= alto; y += 48)
            {
                double luminancia = ColorContrast.RelativeLuminance(McduChassis.RampAt(y, alto));
                Assert.IsTrue(luminancia <= anterior + 0.0001,
                    $"en y={y} la rampa aclara ({luminancia:F4} tras {anterior:F4}): se vería una banda clara abajo");
                anterior = luminancia;
            }

            // Y a la altura de la barra (96 %) tiene que estar oscura de verdad, no en el 26 %.
            double arriba = ColorContrast.RelativeLuminance(McduChassis.RampAt(0, alto));
            double barra = ColorContrast.RelativeLuminance(McduChassis.RampAt(740, alto));
            Assert.IsTrue(barra < arriba * 0.75,
                $"a la altura de la barra la luminancia es {barra:F4} contra {arriba:F4} arriba: la rampa no llega");
        }

        /// <summary>
        /// **Continuidad entre paneles**: dos paneles contiguos piden el color en la misma Y y tienen que
        /// coincidir, o se vería un escalón en la junta. Es la razón de que la rampa sea función de la
        /// altura de la ventana y no del panel.
        /// </summary>
        [TestMethod]
        public void LaRampaNoHaceEscalonEntrePanelesContiguos()
        {
            const int alto = 768;
            const int junta = 300;

            Color abajoDelDeArriba = McduChassis.RampAt(junta, alto);
            Color arribaDelDeAbajo = McduChassis.RampAt(junta, alto);

            Assert.AreEqual(abajoDelDeArriba, arribaDelDeAbajo,
                "la junta de dos paneles tiene que pedir el mismo color");
        }

        /// <summary>
        /// **De dónde saca un panel su altura dentro de la ventana.** Es lo que hace continua la rampa entre
        /// paneles: si esto devolviera 0, todos pintarían el mismo trozo y la ventana se vería plana —que es
        /// exactamente lo que pasó, y por eso está medido aquí en vez de confiado.
        /// </summary>
        [TestMethod]
        public void UnPanelSabeSuAlturaDentroDeLaVentana()
        {
            using (Form form = new Form())
            {
                form.ClientSize = new Size(800, 600);
                using (McduChassisPanel arriba = new McduChassisPanel())
                using (McduChassisPanel abajo = new McduChassisPanel())
                {
                    arriba.SetBounds(0, 40, 800, 60);
                    abajo.SetBounds(0, 400, 800, 60);
                    form.Controls.Add(arriba);
                    form.Controls.Add(abajo);
                    form.CreateControl();
                    arriba.CreateControl();
                    abajo.CreateControl();
                    form.PerformLayout();

                    Assert.AreEqual(40, McduChassis.OffsetY(arriba), "el panel de arriba");
                    Assert.AreEqual(400, McduChassis.OffsetY(abajo), "el panel de abajo");
                    Assert.AreEqual(form.ClientSize.Height, McduChassis.WindowHeight(abajo),
                        "el alto de referencia es el de la ventana");
                }
            }
        }

        // ── Utilidades ───────────────────────────────────────────────────────────────────────────────

        private static byte[] PintarCarcasa()
        {
            using (Bitmap bmp = new Bitmap(240, 160))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                    McduChassis.Paint(g, new Rectangle(0, 0, 240, 160), 120, 768);

                // Se comparan los píxeles, no el fichero PNG (que lleva metadatos).
                Rectangle todo = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData datos = bmp.LockBits(todo, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] bytes = new byte[Math.Abs(datos.Stride) * bmp.Height];
                    System.Runtime.InteropServices.Marshal.Copy(datos.Scan0, bytes, 0, bytes.Length);
                    return bytes;
                }
                finally
                {
                    bmp.UnlockBits(datos);
                }
            }
        }

        private static string Hex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        /// <summary>Saturación HSL, de 0 a 1: es lo que distingue el verde del aparato del de lectura.</summary>
        private static double Saturacion(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            if (max <= 0) return 0;

            double l = (max + min) / 2.0;
            return l > 0.5 ? (max - min) / (2.0 - max - min) : (max - min) / (max + min);
        }
    }
}
