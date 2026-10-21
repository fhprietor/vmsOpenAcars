using System;
using System.Collections.Generic;
using System.IO;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **El tipo ICAO del avión de X-Plane, leído de su `.acf`.**
    ///
    /// X-Plane sí publica el designador —es el campo `ICAO` del avión, el dataref
    /// `sim/aircraft/view/acf_ICAO`—, pero **XPUIPC no lo expone en ningún offset**: medido el
    /// 20/10/2026 con el ToLiss A320neo cargado (XPUIPC 2.0.3.8, X-Plane 12.4.3) los offsets
    /// `0x0600`, `0x0618`, `0x0630`, `0x09D2`, `0x0B26` y `0x3138` devuelven **cadena vacía**, y el
    /// propio binario de XPUIPC no referencia `acf_ICAO` en ninguna parte (sí `acf_descrip` y
    /// `acf_livery_path`, de donde saca el `0x3D00`). Tampoco hay atajo por el `.cfg` de XPUIPC: sus
    /// expresiones son numéricas y no pueden volcar una cadena.
    ///
    /// Así que la única fuente honesta es el fichero del avión cargado:
    ///
    ///     P acf/_ICAO A20N
    ///
    /// Y no es un detalle: **el `0x3D00` de X-Plane es el nombre de la CARPETA**
    /// (`ToLissA320_V1p2p1`), no el del avión. De esa carpeta, `ExtractIcaoFromTitle` deduce `A320`
    /// por contener esa subcadena, y el avión es un **A20N**: el piloto veía «ICAO: A320» y el ACARS
    /// le avisaba de discrepancia contra el OFP de su propio avión. El `.acf` de ese ToLiss lo dice
    /// en las cuatro variantes del paquete (`a320.acf`, `a320_StdDef.acf`, `a320_XP11.acf`,
    /// `a320_XP11_StdDef.acf` → `A20N`).
    ///
    /// Aquí vive **solo la decisión**, sin disco ni WinForms: qué texto es un designador, cuál manda
    /// cuando la carpeta trae varios `.acf` y qué carpetas se prueban. El que lee el disco es
    /// `FsuipcService`.
    /// </summary>
    internal static class XPlaneAcfIdentity
    {
        /// <summary>La clave del `.acf` que guarda el designador ICAO de tipo (`P acf/_ICAO A20N`).</summary>
        internal const string IcaoKey = "P acf/_ICAO";

        /// <summary>
        /// El designador de un `.acf`, en mayúsculas, o `""` si no lo trae o no es plausible.
        /// Recorre el texto línea a línea y se queda con la **primera** aparición de la clave: en un
        /// `.acf` real está una sola vez (`a320.acf`, línea 63.505 de 2,3 MB).
        /// </summary>
        internal static string IcaoFromAcfText(string acfText)
        {
            if (string.IsNullOrEmpty(acfText)) return "";

            int start = 0;
            while (start < acfText.Length)
            {
                int end = acfText.IndexOf('\n', start);
                if (end < 0) end = acfText.Length;

                string line = acfText.Substring(start, end - start).Trim();
                start = end + 1;

                if (!line.StartsWith(IcaoKey, StringComparison.Ordinal)) continue;

                // La clave tiene que terminar ahí: `P acf/_ICAOx` no es `_ICAO`. Después solo puede
                // venir el espacio separador y el valor.
                int after = IcaoKey.Length;
                if (line.Length > after && !char.IsWhiteSpace(line[after])) continue;

                string value = line.Substring(after).Trim();
                return IsPlausibleDesignator(value) ? value.ToUpperInvariant() : "";
            }

            return "";
        }

        /// <summary>
        /// **El designador de una carpeta de avión con varios `.acf`, y solo si todos coinciden.**
        ///
        /// El ToLiss A320neo trae cuatro (`a320`, `a320_StdDef`, `a320_XP11`, `a320_XP11_StdDef`) y
        /// X-Plane carga uno según la versión; los cuatro dicen `A20N`, así que la respuesta es
        /// inequívoca. Si discreparan —un paquete que mezcle CEO y NEO— o a alguno le faltara el
        /// dato, **no se afirma nada**: se devuelve `""` y el cliente degrada a la comparación por
        /// familia. Es la misma regla que el resto de filtros del cliente: sin dato no se bloquea,
        /// pero tampoco se inventa.
        /// </summary>
        internal static string IcaoFromAcfTexts(IEnumerable<string> acfTexts)
        {
            if (acfTexts == null) return "";

            string agreed = null;
            int seen = 0;
            foreach (string text in acfTexts)
            {
                seen++;
                string icao = IcaoFromAcfText(text);
                if (icao.Length == 0) return "";
                if (agreed == null) { agreed = icao; continue; }
                if (!string.Equals(agreed, icao, StringComparison.Ordinal)) return "";
            }

            return seen == 0 ? "" : agreed;
        }

        /// <summary>
        /// ¿La cadena puede ser un designador ICAO de tipo? De 2 a 5 caracteres, solo letras y
        /// dígitos, y **al menos una letra**: los designadores reales son `A20N`, `B738`, `AT76`,
        /// `C172`, `DH8D`, `B58`… Un `737` suelto o un `????` (el centinela de «no lo publico») no
        /// lo son, y aceptarlos sería meter basura donde luego se compara.
        /// </summary>
        internal static bool IsPlausibleDesignator(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            string v = value.Trim();
            if (v.Length < 2 || v.Length > 5) return false;

            bool hasLetter = false;
            foreach (char c in v)
            {
                bool letter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                bool digit  = c >= '0' && c <= '9';
                if (!letter && !digit) return false;
                if (letter) hasLetter = true;
            }

            return hasLetter;
        }

        /// <summary>
        /// **Las carpetas donde puede estar el `.acf` del avión cargado**, en orden de preferencia.
        ///
        /// El `0x3D00` de XPUIPC es el nombre de la carpeta (`ToLissA320_V1p2p1`), así que el primer
        /// candidato es `<X-Plane>\Aircraft\&lt;ese nombre&gt;`. Si el addon está anidado —X-Plane lo
        /// permite, y los de Laminar viven en `Aircraft\Laminar Research\…`— el nombre puede llegar
        /// con la ruta relativa (`Laminar Research\Cessna 172 SP`) o solo con el último tramo, y se
        /// prueban las dos formas. **No hay barrido de la carpeta de aviones**: son dos rutas
        /// construidas a partir de lo que dice el simulador.
        /// </summary>
        internal static IList<string> CandidateDirectories(string aircraftRoot, string title)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(aircraftRoot)) return list;

            string name = FolderName(title);
            if (name.Length == 0) return list;

            string root = aircraftRoot.TrimEnd('\\', '/');
            string withPath = Path.Combine(root, name);
            list.Add(withPath);

            // Si el nombre llegó con la ruta relativa (`Laminar Research\Cessna 172 SP`), la carpeta
            // puede estar en el primer nivel y la ruta completa ser la buena; probar el último tramo
            // cubre el caso contrario, y no cuesta nada porque solo se hace un Directory.Exists.
            string leaf = LeafName(name);
            if (!string.Equals(leaf, name, StringComparison.Ordinal))
                list.Add(Path.Combine(root, leaf));

            return list;
        }

        /// <summary>El nombre de carpeta limpio: recortado, sin comillas y sin separadores finales.</summary>
        internal static string FolderName(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";

            string name = title.Trim().Trim('"', '\'');
            return name.TrimEnd('\\', '/').Trim();
        }

        /// <summary>El último tramo de una ruta relativa (`Laminar Research\Cessna 172 SP` → `Cessna 172 SP`).</summary>
        internal static string LeafName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string clean = name.Trim().TrimEnd('\\', '/');
            int sep = clean.LastIndexOfAny(new[] { '\\', '/' });
            return sep >= 0 ? clean.Substring(sep + 1).Trim() : clean;
        }
    }
}
