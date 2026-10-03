using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Apertura del **sobre cifrado de la clave de NavData** que sirve phpVMS en `GET /api/navdata`.
    ///
    /// El porqué: la `navdata_api_key` **estaba publicada** en el `.config` que se descarga del gestor
    /// de ficheros, así que cualquiera podía leerla. Ahora el `.config` la deja vacía y phpVMS la
    /// entrega cifrada con una clave derivada de la `vms_api_key` del piloto —el único secreto ya
    /// compartido—. No hay proxy: phpVMS entrega `{url, key}` **una vez** y a partir de ahí se habla
    /// directo con NavData.
    ///
    /// Contrato exacto (si baila un byte, no abre):
    /// <code>
    /// blob = base64(payload)
    /// iv   = blob[0..16)      mac = blob[len-32..len)      ct = blob[16..len-32)
    /// mac_esperado = HMAC-SHA256(mac_key, AAD || iv || ct)   → se compara ANTES de descifrar
    /// plaintext    = AES-256-CBC(enc_key, iv, PKCS7, ct)     → JSON {url, key, key_id, issued_at, expires_at}
    /// </code>
    ///
    /// **El orden no es negociable**: el MAC se comprueba antes de descifrar, y con comparación en
    /// tiempo constante (<see cref="FixedTimeEquals"/>). Un criptograma que no autentica **no llega
    /// nunca a AES**: así un oráculo de relleno PKCS7 no puede decir nada.
    ///
    /// Helper puro: sin red, sin WinForms y sin estado. El que pregunta a phpVMS y guarda el resultado
    /// en memoria es `NavDataKeyProvider`.
    /// </summary>
    internal static class NavDataCipher
    {
        /// <summary>Nombre del cifrado que anuncia la cabecera `X-NavData-Cipher`.</summary>
        internal const string CipherName = "aes-256-cbc-hmac-sha256";

        /// <summary>
        /// ¿El `cipher` que anuncia el sobre es el que sabemos abrir?
        ///
        /// El porqué: el **defecto del servidor sigue siendo `aes-256-gcm`** —confirmado por phpVMS el
        /// 03/10/2026—, así que lo que decide qué sobre nos llega es la cabecera
        /// `X-NavData-Cipher: aes-256-cbc-hmac-sha256`. Si esa cabecera se cayera, el sobre llegaría en
        /// GCM y este cliente **no lo puede abrir**: GCM exigiría BouncyCastle en un binario que se
        /// distribuye a los pilotos. Se dice que no **antes** de intentarlo, para no quedarnos con la
        /// sesión creyendo que hay credencial cuando lo que hay es un criptograma ilegible.
        ///
        /// Sin campo `cipher` se acepta: el contrato puede omitirlo y entonces la autoridad es el MAC
        /// (degradar sin datos, nunca rechazar por un campo que no vino). Cualquier otro nombre
        /// —`aes-256-gcm` incluido— se rechaza. Helper puro, con test.
        /// </summary>
        internal static bool IsSupportedCipher(string cipher)
        {
            return string.IsNullOrEmpty(cipher)
                || string.Equals(cipher, CipherName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Salt de la HKDF. Es parte del contrato con phpVMS, no una elección local.</summary>
        internal const string SaltText = "vmsopenacars/navdata/v1";

        /// <summary>AAD del HMAC (y no se cifra: solo autentica).</summary>
        internal const string AadText = "vmsopenacars/navdata/v1";

        /// <summary>Info de la HKDF: separa esta derivación de cualquier otra del mismo salt.</summary>
        internal const string InfoText = "navdata-api-key-cbc";

        /// <summary>Longitud del IV de AES-CBC.</summary>
        internal const int IvLength = 16;

        /// <summary>Longitud del HMAC-SHA256.</summary>
        internal const int MacLength = 32;

        /// <summary>Longitud de la clave de cifrado AES-256 y de la de MAC.</summary>
        internal const int KeyLength = 32;

        /// <summary>Material HKDF completo: 32 de cifrado + 32 de MAC.</summary>
        internal const int KeyMaterialLength = 2 * KeyLength;

        /// <summary>
        /// Abre el sobre. Devuelve `false` con el motivo en <paramref name="error"/> en vez de lanzar:
        /// el llamante decide, y lo normal es **degradar sin NavData** en lugar de tumbar el vuelo.
        ///
        /// <paramref name="vmsApiKeyIkm"/> es la `vms_api_key` del piloto **tal cual** (UTF-8, sin trim
        /// ni prefijos): es el IKM de la HKDF.
        /// </summary>
        internal static bool TryOpen(string payloadBase64, string vmsApiKeyIkm,
                                     out NavDataEnvelope envelope, out string error)
        {
            envelope = null;
            error    = null;

            if (string.IsNullOrEmpty(payloadBase64)) { error = "payload vacío"; return false; }
            if (string.IsNullOrEmpty(vmsApiKeyIkm))  { error = "vms_api_key vacía"; return false; }

            byte[] blob;
            try { blob = Convert.FromBase64String(payloadBase64); }
            catch (FormatException) { error = "payload no es base64"; return false; }

            if (blob.Length < IvLength + MacLength) { error = "blob demasiado corto"; return false; }

            byte[] iv = new byte[IvLength];
            byte[] ct = new byte[blob.Length - IvLength - MacLength];
            byte[] mac = new byte[MacLength];
            Buffer.BlockCopy(blob, 0, iv, 0, IvLength);
            Buffer.BlockCopy(blob, IvLength, ct, 0, ct.Length);
            Buffer.BlockCopy(blob, blob.Length - MacLength, mac, 0, MacLength);

            // HKDF-SHA256(ikm, salt, info, L=64) → enc_key | mac_key
            byte[] material = HkdfSha256.DeriveKey(
                Encoding.UTF8.GetBytes(vmsApiKeyIkm),
                Encoding.UTF8.GetBytes(SaltText),
                Encoding.UTF8.GetBytes(InfoText),
                KeyMaterialLength);

            byte[] encKey = new byte[KeyLength];
            byte[] macKey = new byte[KeyLength];
            Buffer.BlockCopy(material, 0, encKey, 0, KeyLength);
            Buffer.BlockCopy(material, KeyLength, macKey, 0, KeyLength);

            byte[] expectedMac;
            byte[] aad = Encoding.UTF8.GetBytes(AadText);
            using (var hmac = new HMACSHA256(macKey))
            {
                byte[] signed = new byte[aad.Length + iv.Length + ct.Length];
                Buffer.BlockCopy(aad, 0, signed, 0, aad.Length);
                Buffer.BlockCopy(iv,  0, signed, aad.Length, iv.Length);
                Buffer.BlockCopy(ct,  0, signed, aad.Length + iv.Length, ct.Length);
                expectedMac = hmac.ComputeHash(signed);
            }

            // El MAC primero, y en tiempo constante. Ver el porqué en FixedTimeEquals.
            if (!FixedTimeEquals.Bytes(expectedMac, mac))
            {
                error = "MAC no válido";
                return false;
            }

            string json;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key     = encKey;
                aes.IV      = iv;
                try
                {
                    using (var decryptor = aes.CreateDecryptor())
                        json = Encoding.UTF8.GetString(decryptor.TransformFinalBlock(ct, 0, ct.Length));
                }
                catch (CryptographicException) { error = "descifrado fallido"; return false; }
            }

            JObject obj;
            try
            {
                // `DateParseHandling.None`: sin esto Newtonsoft convierte `issued_at`/`expires_at`
                // (ISO-8601) en `DateTime` y al volver a texto los deja con el formato de la cultura
                // del equipo —`2026/10/01 08:00:00`—, que no es lo que entregó phpVMS. Los campos se
                // conservan **tal cual vinieron**; la fecha se interpreta aparte, en ParseUtc.
                using (var textReader = new StringReader(json))
                using (var jsonReader = new JsonTextReader(textReader) { DateParseHandling = DateParseHandling.None })
                    obj = JObject.Load(jsonReader);
            }
            catch { error = "el descifrado no es JSON"; return false; }

            string url = obj["url"]?.ToString();
            string key = obj["key"]?.ToString();
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
            {
                error = "faltan url o key";
                return false;
            }

            string expiresAt = obj["expires_at"]?.ToString();
            envelope = new NavDataEnvelope
            {
                Url        = url.Trim(),
                Key        = key,
                KeyId      = obj["key_id"]?.ToString(),
                IssuedAt   = obj["issued_at"]?.ToString(),
                ExpiresAt  = expiresAt,
                ExpiresAtUtc = ParseUtc(expiresAt),
            };
            return true;
        }

        /// <summary>
        /// `expires_at` como instante UTC. Se acepta la forma ISO-8601 (`...Z` o con desfase) porque es
        /// lo que publica la API; sin desfase se asume UTC (`AssumeUniversal`), que es lo que phpVMS
        /// escribe. Si no se entiende, se devuelve `null` y el llamante decide (no se inventa una fecha).
        /// </summary>
        private static DateTime? ParseUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                return parsed;
            return null;
        }
    }
}
