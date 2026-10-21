using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El tipo ICAO del avión de X-Plane, sacado de su `.acf`.**
    ///
    /// Los datos de estos tests son los reales del ToLiss A320neo del mantenedor, medidos en su
    /// máquina el 20/10/2026 (`X-Plane 12.4.3` + `XPUIPC 2.0.3.8`):
    ///
    /// - El offset `0x0618` (modelo ATC) devuelve **cadena vacía**; lo mismo `0x0600`, `0x0630`,
    ///   `0x09D2`, `0x0B26` y `0x3138`.
    /// - `0x3D00` devuelve el nombre de la **carpeta**, con su espacio final incluido:
    ///   `ToLissA320_V1p2p1 `.
    /// - La carpeta trae **cuatro** `.acf` (`a320`, `a320_StdDef`, `a320_XP11`, `a320_XP11_StdDef`) y
    ///   los cuatro dicen `A20N`; el cargado es `a320.acf` y la clave está en su línea 63.505.
    ///
    /// De ahí salía el «ICAO: A320» del log: `ExtractIcaoFromTitle("ToLissA320_V1p2p1")` encuentra la
    /// subcadena `A320`, y el avión es un **A20N**. El piloto veía que el ACARS «detectaba un CEO» y
    /// recibía un aviso de discrepancia contra el OFP de su propio avión.
    /// </summary>
    [TestClass]
    public class XPlaneAcfIdentityTests
    {
        /// <summary>Un trozo real de `a320.acf` alrededor de la clave: las claves van en orden alfabético.</summary>
        private static string ToLissA320Acf =>
            "P acf/_Gpos 2.700000048\r\n" +
            "P acf/_HUD_del_x 710.000000000\r\n" +
            "P acf/_HUD_del_y 110.000000000\r\n" +
            "P acf/_ICAO A20N\r\n" +
            "P acf/_ITT_is_C 1\r\n" +
            "P acf/_Jxx_unitmass 0.000000000\r\n" +
            "P acf/_name ToLiSs A320 Hi Def\r\n" +
            "P acf/_descrip A320 with high fidelity system modelling\r\n";

        [TestMethod]
        public void ElDesignadorSeLeeDeLaLineaDelAcf()
        {
            Assert.AreEqual("A20N", XPlaneAcfIdentity.IcaoFromAcfText(ToLissA320Acf));

            // El Cessna de Laminar y el 737 de Laminar: los dos formatos que aparecen de verdad.
            Assert.AreEqual("C172", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO C172\nP acf/_name Cessna 172 SP\n"));
            Assert.AreEqual("B738", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO B738\n"));
        }

        [TestMethod]
        public void SeNormalizaAMayusculasYSinEspacios()
        {
            // El `.acf` no siempre está limpio: espacios de más y saltos de línea de Windows.
            Assert.AreEqual("A20N", XPlaneAcfIdentity.IcaoFromAcfText("  P acf/_ICAO   a20n  \r\n"));
            Assert.AreEqual("A20N", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO A20N"));
        }

        [TestMethod]
        public void SinDatoONoPlausible_DevuelveVacio()
        {
            // La clave puede estar vacía (aviones a medio hacer) y `????` es el centinela del cliente.
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO\n"));
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO \r\n"));
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO ????\r\n"));
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO AIRBUS\r\n"));
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO 737\r\n"),
                "un número suelto no es un designador");
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAO A\r\n"));
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_name ToLiSs A320 Hi Def\r\n"),
                "el `_name` no es el `_ICAO`: aquí pone A320 y el avión es un A20N");
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText("P acf/_ICAOx A20N\r\n"),
                "la clave se llama `_ICAO`, no `_ICAOx`");
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText(""));
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfText(null));
        }

        [TestMethod]
        public void VariosAcfEnLaMismaCarpeta_SoloValenSiCoinciden()
        {
            // Los cuatro del ToLiss dicen A20N: la respuesta es inequívoca.
            Assert.AreEqual("A20N", XPlaneAcfIdentity.IcaoFromAcfTexts(new[]
            {
                ToLissA320Acf, ToLissA320Acf, ToLissA320Acf, ToLissA320Acf
            }));

            // Un paquete que mezcle CEO y NEO no permite afirmar nada: se devuelve vacío y decide la
            // degradación (familia), nunca una moneda al aire.
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfTexts(new[]
            {
                "P acf/_ICAO A20N\r\n", "P acf/_ICAO A320\r\n"
            }));

            // Y si a uno le falta el dato, tampoco: no se ignora al que no contesta.
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfTexts(new[]
            {
                "P acf/_ICAO A20N\r\n", "P acf/_name sin tipo\r\n"
            }));

            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfTexts(new string[0]));
            Assert.AreEqual("", XPlaneAcfIdentity.IcaoFromAcfTexts(null));
        }

        [TestMethod]
        public void LasCarpetasCandidatasSalenDelNombreQuePublicaXplane()
        {
            // `0x3D00` trae el nombre de la carpeta con su espacio final, que se recorta.
            var dirs = XPlaneAcfIdentity.CandidateDirectories(
                @"F:\Steam\steamapps\common\X-Plane 12\Aircraft", "ToLissA320_V1p2p1 ");
            Assert.AreEqual(1, dirs.Count);
            Assert.AreEqual(Path.Combine(@"F:\Steam\steamapps\common\X-Plane 12\Aircraft", "ToLissA320_V1p2p1"),
                            dirs[0]);

            // Los de Laminar viven anidados: si el nombre llega con la ruta relativa se prueban las dos.
            var nested = XPlaneAcfIdentity.CandidateDirectories(
                @"F:\Steam\steamapps\common\X-Plane 12\Aircraft", @"Laminar Research\Cessna 172 SP");
            Assert.AreEqual(2, nested.Count);
            Assert.AreEqual(Path.Combine(@"F:\Steam\steamapps\common\X-Plane 12\Aircraft",
                                         @"Laminar Research\Cessna 172 SP"), nested[0]);
            Assert.AreEqual(Path.Combine(@"F:\Steam\steamapps\common\X-Plane 12\Aircraft",
                                         "Cessna 172 SP"), nested[1]);

            // Sin raíz o sin nombre no hay candidatas: el que llama degrada, no inventa una ruta.
            Assert.AreEqual(0, XPlaneAcfIdentity.CandidateDirectories("", "ToLissA320_V1p2p1").Count);
            Assert.AreEqual(0, XPlaneAcfIdentity.CandidateDirectories(@"F:\X-Plane 12", "  ").Count);
        }

        [TestMethod]
        public void ElNombreDeCarpetaSeRecortaYSeSepara()
        {
            Assert.AreEqual("ToLissA320_V1p2p1", XPlaneAcfIdentity.FolderName(" ToLissA320_V1p2p1 "));
            Assert.AreEqual("ToLissA320_V1p2p1", XPlaneAcfIdentity.FolderName("ToLissA320_V1p2p1\\"));
            Assert.AreEqual("ToLissA320_V1p2p1", XPlaneAcfIdentity.FolderName("\"ToLissA320_V1p2p1\""));
            Assert.AreEqual("", XPlaneAcfIdentity.FolderName(null));

            Assert.AreEqual("Cessna 172 SP", XPlaneAcfIdentity.LeafName(@"Laminar Research\Cessna 172 SP"));
            Assert.AreEqual("ToLissA320_V1p2p1", XPlaneAcfIdentity.LeafName("ToLissA320_V1p2p1"),
                "sin separadores, el tramo es el todo");
        }

        [TestMethod]
        public void LosDesignadoresRealesSonPlausibles()
        {
            Assert.IsTrue(XPlaneAcfIdentity.IsPlausibleDesignator("A20N"));
            Assert.IsTrue(XPlaneAcfIdentity.IsPlausibleDesignator("B738"));
            Assert.IsTrue(XPlaneAcfIdentity.IsPlausibleDesignator("AT76"));
            Assert.IsTrue(XPlaneAcfIdentity.IsPlausibleDesignator("DH8D"));
            Assert.IsTrue(XPlaneAcfIdentity.IsPlausibleDesignator("B58"));
            Assert.IsTrue(XPlaneAcfIdentity.IsPlausibleDesignator("a20n"));

            Assert.IsFalse(XPlaneAcfIdentity.IsPlausibleDesignator("????"));
            Assert.IsFalse(XPlaneAcfIdentity.IsPlausibleDesignator(""));
            Assert.IsFalse(XPlaneAcfIdentity.IsPlausibleDesignator("A"));
            Assert.IsFalse(XPlaneAcfIdentity.IsPlausibleDesignator("A20NEO"));
            Assert.IsFalse(XPlaneAcfIdentity.IsPlausibleDesignator("A-20"));
        }
    }
}
