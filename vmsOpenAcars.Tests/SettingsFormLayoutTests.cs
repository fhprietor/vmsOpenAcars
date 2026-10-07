using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.UI.Forms;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La rejilla de `SettingsForm`, celda por celda.**
    ///
    /// El formulario se construye con índices de fila explícitos y `TableLayoutPanel` **no avisa**
    /// de dos controles en la misma celda: el último se pinta encima y el síntoma es un rótulo
    /// tapado, no un fallo de compilación. Pasó al añadir la URL de NavData en v0.9.11 (el rótulo
    /// *NavData API* se quedó en la fila de la Key, se corrigió en v0.9.12) y la guía le dedica dos
    /// párrafos porque el error se repite. Estos tres tests convierten esa comprobación manual en
    /// una regresión automática: se instancia el formulario **real** (sin enseñarlo, como
    /// `EcamDialogTests`) y se mira su rejilla de verdad.
    ///
    /// Además fijan el **presupuesto de alto**: cada fila de la columna cuesta 35 px y el alto útil
    /// de contenido es (alto de ventana − 99) px —barra de título 35, panel de botones 44 y los
    /// `Padding` de 4 y 16—. Una fila nueva sin subir la ventana recorta la rejilla, y eso tampoco
    /// da error: los controles de abajo desaparecen.
    /// </summary>
    [TestClass]
    public class SettingsFormLayoutTests
    {
        /// <summary>Barra de título 35 + panel de botones 44 + `Padding` del formulario 4 + el del
        /// panel de contenido 16. Es la cifra que documenta el propio formulario.</summary>
        private const int ChromePx = 99;

        [TestMethod]
        public void NingunaCeldaLaCompartenDosControles()
        {
            using (var form = new SettingsForm())
            {
                form.PerformLayout();
                foreach (var table in Tables(form))
                    AssertNoSharedCell(table);
            }
        }

        [TestMethod]
        public void LasRejillasDeColumnaCabenEnElAltoUtil()
        {
            using (var form = new SettingsForm())
            {
                form.PerformLayout();
                int available = form.ClientSize.Height - ChromePx;

                foreach (var table in Tables(form).Where(HasOnlyAbsoluteRows))
                {
                    int needed = AskedHeight(table);
                    Assert.IsTrue(needed <= available,
                        $"la rejilla pide {needed} px y solo hay {available} " +
                        $"(ventana {form.ClientSize.Height} px − {ChromePx}); una fila nueva cuesta " +
                        "35 px y hay que subir el alto de la ventana y su MinimumSize");
                }
            }
        }

        [TestMethod]
        public void ElInterruptorDeLaGuiaDeRodajeExisteYVieneActivado()
        {
            using (var form = new SettingsForm())
            {
                form.PerformLayout();
                var chk = FindCheckBox(form, "chkTaxiGuidance");

                Assert.IsNotNull(chk, "falta el checkbox «Taxi guidance» de la guía de rodaje");
                Assert.IsTrue(chk.Checked,
                    "la guía de rodaje nace ACTIVADA: quien ya la usaba no puede notar el cambio");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void AssertNoSharedCell(TableLayoutPanel table)
        {
            var occupied = new Dictionary<(int Col, int Row), Control>();

            foreach (Control c in table.Controls)
            {
                int col = table.GetColumn(c);
                int row = table.GetRow(c);
                int colSpan = table.GetColumnSpan(c);
                int rowSpan = table.GetRowSpan(c);

                for (int dc = 0; dc < colSpan; dc++)
                    for (int dr = 0; dr < rowSpan; dr++)
                    {
                        var cell = (col + dc, row + dr);
                        if (occupied.TryGetValue(cell, out Control other))
                            Assert.Fail(
                                $"«{other.Text}» y «{c.Text}» comparten la celda " +
                                $"(columna {cell.Item1}, fila {cell.Item2}) de {table.Name}: " +
                                "el último se pinta encima y el síntoma es un rótulo tapado");
                        occupied[cell] = c;
                    }
            }

            // Y ninguna fila fuera de la rejilla: `RowCount` corto deja controles sin celda.
            foreach (Control c in table.Controls)
            {
                string label = c.Text ?? "";
                Assert.IsTrue(table.GetRow(c) < table.RowCount,
                    $"«{label}» va a la fila {table.GetRow(c)} y la rejilla solo declara {table.RowCount}");
                Assert.IsTrue(table.GetColumn(c) < table.ColumnCount,
                    $"«{label}» va a la columna {table.GetColumn(c)} y solo hay {table.ColumnCount}");
            }
        }

        private static IEnumerable<TableLayoutPanel> Tables(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is TableLayoutPanel t)
                {
                    yield return t;
                    foreach (var inner in Tables(t)) yield return inner;
                }
            }
        }

        private static bool HasOnlyAbsoluteRows(TableLayoutPanel table)
            => table.RowStyles.Cast<RowStyle>().All(r => r.SizeType == SizeType.Absolute);

        private static int AskedHeight(TableLayoutPanel table)
            => (int)table.RowStyles.Cast<RowStyle>().Sum(r => r.Height);

        private static CheckBox FindCheckBox(Control root, string name)
        {
            foreach (Control c in root.Controls)
            {
                if (c is CheckBox chk && chk.Name == name) return chk;
                var inner = FindCheckBox(c, name);
                if (inner != null) return inner;
            }
            return null;
        }
    }
}
