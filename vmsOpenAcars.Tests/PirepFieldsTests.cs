using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Los campos personalizados con los que el PIREP lleva la pista y la ruta de rodaje, acordados
    /// con phpVMS el 29/09/2026 (`pirep_fields`: `Departure Runway`, `Arrival Runway`, `Taxi Route`).
    ///
    /// Lo que se fija aquí, y por qué:
    ///  - **La clave es el NOMBRE, no el slug.** La API guarda la clave como nombre del campo y
    ///    deriva el slug con `str_slug()`; mandar `departure-runway` casa igual pero el PIREP acaba
    ///    pintando el slug. El test lo comprueba explícitamente para que nadie «arregle» el nombre.
    ///  - **Un campo sin valor se omite.** Así un envío posterior (la ruta de rodaje, que llega
    ///    minutos más tarde) no borra con `""` la pista que ya se mandó en el prefile.
    ///  - **La ruta conserva el orden y las calles**, solo se colapsan los espacios de más.
    ///
    /// Los datos son del caso real que estamos validando: SKBO, salida por la 14L, ruta tecleada por
    /// el mantenedor `F E M A A3`.
    /// </summary>
    [TestClass]
    public class PirepFieldsTests
    {
        [TestMethod]
        public void TheKeysAreTheFieldNames_NotTheSlugs()
        {
            var fields = PirepFields.Build("14L", "14R", "F E M A A3");

            CollectionAssert.AreEquivalent(
                new[] { "Departure Runway", "Arrival Runway", "Taxi Route" },
                new List<string>(fields.Keys));

            // La trampa que avisó phpVMS: con el slug casa igual, pero el PIREP pinta el slug.
            Assert.IsFalse(fields.ContainsKey("departure-runway"));
            Assert.IsFalse(fields.ContainsKey("arrival-runway"));
            Assert.IsFalse(fields.ContainsKey("taxi-route"));
        }

        [TestMethod]
        public void TheRealSkboCase_GoesThroughUntouched()
        {
            var fields = PirepFields.Build("14L", "14R", "F E M A A3");

            Assert.AreEqual("14L",      fields["Departure Runway"]);
            Assert.AreEqual("14R",      fields["Arrival Runway"]);
            Assert.AreEqual("F E M A A3", fields["Taxi Route"]);
        }

        [TestMethod]
        public void TheRunwayIsTrimmedAndUppercased()
        {
            // El OFP y el dataset no la escriben siempre igual; la pista es «14R», no « 14r ».
            var fields = PirepFields.Build(" 14r ", "\t32l\n", null);

            Assert.AreEqual("14R", fields["Departure Runway"]);
            Assert.AreEqual("32L", fields["Arrival Runway"]);
        }

        [TestMethod]
        public void TheRouteKeepsTheOrderAndOnlyCollapsesSpaces()
        {
            // El popup admite pegar texto con saltos de línea; la ruta sigue siendo la del piloto,
            // en su orden, y las calles no se tocan (ni a mayúsculas ni a la grafía del dataset).
            var fields = PirepFields.Build(null, null, "F  E\nM   A A3");

            Assert.AreEqual("F E M A A3", fields["Taxi Route"]);
        }

        [TestMethod]
        public void AtPrefileOnlyTheRunwaysAreSent_AndTheRouteLaterDoesNotWipeThem()
        {
            // Lo que se manda al prefilear: la ruta de rodaje todavía no existe (el piloto no ha
            // respondido al popup), así que su clave ni aparece.
            var prefile = PirepFields.Build("14L", "14R", null);
            Assert.AreEqual(2, prefile.Count);
            Assert.IsFalse(prefile.ContainsKey("Taxi Route"));

            // Y lo que se manda después, cuando confirma la ruta: solo la ruta, con la pista de
            // salida. La de llegada no se repite para no arriesgar un valor viejo si hubo desvío.
            var later = PirepFields.Build("14L", null, "F E M A A3");
            Assert.AreEqual(2, later.Count);
            Assert.IsFalse(later.ContainsKey("Arrival Runway"));
        }

        [TestMethod]
        public void WithoutData_NothingIsSent()
        {
            // Un avión sin pista en el OFP y sin ruta tecleada no manda un diccionario con claves
            // vacías: no manda nada. Nunca suprimir por suposición, pero tampoco escribir "".
            var fields = PirepFields.Build(null, "   ", "");

            Assert.AreEqual(0, fields.Count);
        }
    }
}
