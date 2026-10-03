using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Comparación de dos bloques de bytes **en tiempo constante** respecto al contenido.
    ///
    /// Por qué no vale `SequenceEqual` ni `==`: esas cortan en el primer byte distinto, así que el
    /// tiempo de respuesta filtra **cuántos bytes iniciales acertó** quien está probando. Un atacante
    /// que puede medir esa diferencia reconstruye el MAC byte a byte sin necesidad de romper HMAC.
    /// Aquí se recorre siempre la longitud completa y se acumula el XOR: el coste depende solo del
    /// tamaño, nunca de por dónde difieren.
    ///
    /// Es la puerta que se cierra **antes** de descifrar el sobre de NavData (<see cref="NavDataCipher"/>):
    /// si el MAC no cuadra, el criptograma ni se toca.
    /// </summary>
    internal static class FixedTimeEquals
    {
        /// <summary>
        /// `true` solo si los dos bloques tienen el mismo tamaño y el mismo contenido. Longitudes
        /// distintas devuelven `false` sin recorrer nada: la longitud del MAC ya es pública.
        /// </summary>
        internal static bool Bytes(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }
    }
}
