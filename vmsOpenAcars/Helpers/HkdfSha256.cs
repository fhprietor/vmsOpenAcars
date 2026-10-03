using System;
using System.Security.Cryptography;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// HKDF-SHA256 (RFC 5869) a mano sobre <see cref="HMACSHA256"/>, para poder derivar la clave del
    /// sobre de NavData **sin añadir ningún paquete**: phpVMS recomienda `aes-256-cbc-hmac-sha256`
    /// justo por eso —BCL pura—, y BouncyCastle o GCM quedarían fuera de lo que .NET Framework 4.8
    /// trae de serie.
    ///
    /// El contrato del sobre (fijado con phpVMS) es:
    /// <code>
    /// IKM    = vms_api_key del piloto (UTF-8, tal cual, sin trim ni prefijos)
    /// SALT   = "vmsopenacars/navdata/v1"     AAD = "vmsopenacars/navdata/v1"
    /// INFO   = "navdata-api-key-cbc"
    /// material = HKDF-SHA256(ikm, salt, info, L=64)
    /// enc_key  = material[0..32)   mac_key = material[32..64)
    /// </code>
    /// **Si baila un byte, el sobre no abre**: el salt y el info son parte del contrato, no una
    /// elección local, y por eso viven como constantes en <see cref="NavDataCipher"/>.
    ///
    /// Los dos pasos se exponen por separado (`Extract` y `Expand`) porque el RFC los fija por
    /// separado y los vectores oficiales se pueden comprobar uno a uno: si algún día falla, el test
    /// dice si el roto es el extract o el expand.
    /// </summary>
    internal static class HkdfSha256
    {
        /// <summary>Longitud de salida de SHA-256: el `HashLen` del RFC 5869.</summary>
        internal const int HashLength = 32;

        /// <summary>Tope del RFC 5869 §2.3: como mucho 255 bloques (255 × 32 = 8 160 bytes).</summary>
        internal const int MaxOutputLength = 255 * HashLength;

        /// <summary>
        /// Paso *extract* (RFC 5869 §2.2): `PRK = HMAC-SHA256(salt, IKM)`.
        /// Con salt vacío o nulo se usa un bloque de ceros de `HashLen`, que es lo que manda el RFC
        /// —y lo que comprueba el Test Case 3, que va sin salt—.
        /// </summary>
        internal static byte[] Extract(byte[] salt, byte[] ikm)
        {
            if (ikm == null) throw new ArgumentNullException(nameof(ikm));
            if (salt == null || salt.Length == 0) salt = new byte[HashLength];

            using (var hmac = new HMACSHA256(salt))
                return hmac.ComputeHash(ikm);
        }

        /// <summary>
        /// Paso *expand* (RFC 5869 §2.3): `T(n) = HMAC(PRK, T(n-1) | info | n)` con `n` de 1 a 255,
        /// concatenando hasta <paramref name="length"/> bytes.
        /// </summary>
        internal static byte[] Expand(byte[] prk, byte[] info, int length)
        {
            if (prk == null) throw new ArgumentNullException(nameof(prk));
            if (length < 0 || length > MaxOutputLength)
                throw new ArgumentOutOfRangeException(nameof(length),
                    "HKDF-Expand admite entre 0 y " + MaxOutputLength + " bytes (255 bloques).");
            if (info == null) info = new byte[0];

            byte[] okm = new byte[length];
            using (var hmac = new HMACSHA256(prk))
            {
                byte[] previous = new byte[0];
                int offset = 0;
                int counter = 1;

                while (offset < length)
                {
                    // input = T(n-1) | info | n  — el contador va al final y es de un solo byte.
                    byte[] input = new byte[previous.Length + info.Length + 1];
                    Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
                    Buffer.BlockCopy(info,     0, input, previous.Length, info.Length);
                    input[input.Length - 1] = (byte)counter;

                    previous = hmac.ComputeHash(input);

                    int take = Math.Min(HashLength, length - offset);
                    Buffer.BlockCopy(previous, 0, okm, offset, take);
                    offset += take;
                    counter++;
                }
            }
            return okm;
        }

        /// <summary>Extract + expand en una llamada, que es como lo usa el sobre de NavData.</summary>
        internal static byte[] DeriveKey(byte[] ikm, byte[] salt, byte[] info, int length)
            => Expand(Extract(salt, ikm), info, length);
    }
}
