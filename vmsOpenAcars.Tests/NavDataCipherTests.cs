using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **El sobre cifrado de la clave de NavData** (`NavDataCipher`).
    ///
    /// Lo que se prueba y lo que no, dicho sin adornos: el contrato (`aes-256-cbc-hmac-sha256`, salt,
    /// AAD, info y orden MAC-antes-de-descifrar) se fija **de dos maneras**. Con el **round-trip**:
    /// el `Seal` de abajo **vive solo en el test** —en producción no hay cifrador, el cliente solo
    /// abre— y demuestra que implementamos el mismo contrato que firmamos con phpVMS. Y con el
    /// **rechazo**: manipular un byte del `ct`, del `mac` o del `iv`, o usar otra `vms_api_key`, tiene
    /// que fallar **en el MAC**, antes de tocar el criptograma.
    ///
    /// **La prueba definitiva es la entrega real**: si el sobre que manda phpVMS no abre, el contrato
    /// que se rompió no está aquí. Por eso el resultado de la llamada en vivo va aparte, en el resumen.
    /// </summary>
    [TestClass]
    public class NavDataCipherTests
    {
        private const string Ikm = "vms-api-key-de-prueba-0123456789";

        private const string EnvelopeJson =
            "{\"url\":\"https://navdata.vholar.co/api/v1/\"," +
            "\"key\":\"vhr-clave-de-prueba-0001\"," +
            "\"key_id\":\"kd-2026-10-01\"," +
            "\"issued_at\":\"2026-10-01T08:00:00Z\"," +
            "\"expires_at\":\"2026-10-08T08:00:00Z\"}";

        // ── El camino feliz ───────────────────────────────────────────────────────

        [TestMethod]
        public void RoundTrip_ElSobreFabricadoConElContrato_AbreYDevuelveLosCampos()
        {
            string payload = Seal(EnvelopeJson, Ikm);

            bool ok = NavDataCipher.TryOpen(payload, Ikm, out NavDataEnvelope env, out string error);

            Assert.IsTrue(ok, "el sobre tiene que abrir: " + error);
            Assert.AreEqual("https://navdata.vholar.co/api/v1/", env.Url);
            Assert.AreEqual("vhr-clave-de-prueba-0001", env.Key);
            Assert.AreEqual("kd-2026-10-01", env.KeyId);
            Assert.AreEqual("2026-10-01T08:00:00Z", env.IssuedAt);
            Assert.AreEqual("2026-10-08T08:00:00Z", env.ExpiresAt);
            Assert.AreEqual(new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc), env.ExpiresAtUtc);
        }

        [TestMethod]
        public void SinFechaDeVencimientoLegible_NoInventaVencimiento()
        {
            // `expires_at` en un formato que no entendemos no puede inventar una fecha: null significa
            // «no sé cuándo vence» y el sobre se sigue usando (una petición por sesión).
            string payload = Seal("{\"url\":\"https://n/\",\"key\":\"k\",\"expires_at\":\"en un rato\"}",
                                  Ikm);

            Assert.IsTrue(NavDataCipher.TryOpen(payload, Ikm, out NavDataEnvelope env, out _));
            Assert.IsNull(env.ExpiresAtUtc);
            Assert.AreEqual("en un rato", env.ExpiresAt);
        }

        // ── El rechazo: manipular el sobre ────────────────────────────────────────

        [TestMethod]
        public void UnByteDelCriptogramaCambiado_SeRechazaPorElMac()
        {
            byte[] blob  = Convert.FromBase64String(Seal(EnvelopeJson, Ikm));
            blob[24]    ^= 0x01;   // dentro del ct: el MAC ya no cuadra

            AssertRejectedPorMac(Convert.ToBase64String(blob));
        }

        [TestMethod]
        public void UnByteDelMacCambiado_SeRechazaPorElMac()
        {
            byte[] blob     = Convert.FromBase64String(Seal(EnvelopeJson, Ikm));
            blob[blob.Length - 1] ^= 0x01;

            AssertRejectedPorMac(Convert.ToBase64String(blob));
        }

        [TestMethod]
        public void UnByteDelIvCambiado_SeRechazaPorElMac()
        {
            // El IV también está autenticado (AAD || iv || ct): cambiarlo cambia el descifrado Y el MAC.
            byte[] blob = Convert.FromBase64String(Seal(EnvelopeJson, Ikm));
            blob[3]    ^= 0x01;

            AssertRejectedPorMac(Convert.ToBase64String(blob));
        }

        [TestMethod]
        public void OtraVmsApiKey_NoAbreElSobre()
        {
            // El IKM es el único secreto compartido: con otra clave no sale ni el MAC ni el material.
            string payload = Seal(EnvelopeJson, Ikm);

            Assert.IsFalse(NavDataCipher.TryOpen(payload, "otra-clave-distinta", out _, out string error));
            StringAssert.Contains(error, "MAC");
        }

        [TestMethod]
        public void MacValidoPeroCriptogramaRoto_NoPasaDelDescifrado()
        {
            // Aquí el MAC SÍ cuadra (se recompone con la mac_key del test) y el fallo es del AES/JSON:
            // demuestra que la puerta del MAC no tapa los otros caminos y que no se cuela nada.
            byte[] blob = Convert.FromBase64String(Seal(EnvelopeJson, Ikm));
            blob[30]   ^= 0xFF;                         // byte del ct, sin tocar el relleno final
            RecomputeMac(blob, Ikm);

            Assert.IsFalse(NavDataCipher.TryOpen(Convert.ToBase64String(blob), Ikm, out _, out string error));
            Assert.IsNotNull(error);
        }

        // ── Entradas que ni se intentan ───────────────────────────────────────────

        [TestMethod]
        public void PayloadVacioONoBase64_SeRechazaSinLanzar()
        {
            Assert.IsFalse(NavDataCipher.TryOpen("", Ikm, out _, out string e1));
            Assert.IsNotNull(e1);
            Assert.IsFalse(NavDataCipher.TryOpen("esto no es base64 !!!", Ikm, out _, out string e2));
            StringAssert.Contains(e2, "base64");
        }

        [TestMethod]
        public void BlobDemasiadoCorto_SeRechaza()
        {
            // Menos de 16 de IV + 32 de MAC no puede ser un sobre.
            Assert.IsFalse(NavDataCipher.TryOpen(
                Convert.ToBase64String(new byte[NavDataCipher.IvLength + NavDataCipher.MacLength - 1]),
                Ikm, out _, out string error));
            StringAssert.Contains(error, "corto");
        }

        [TestMethod]
        public void SinIkm_NoSeIntentaAbrir()
        {
            Assert.IsFalse(NavDataCipher.TryOpen(Seal(EnvelopeJson, Ikm), "", out _, out string error));
            StringAssert.Contains(error, "vms_api_key");
        }

        // ── El cifrado que anuncia el sobre: solo sabemos abrir CBC+HMAC ──────────
        //
        // El porqué de esta puerta: el **defecto de phpVMS sigue siendo `aes-256-gcm`**
        // (confirmado el 03/10/2026), así que lo que decide qué sobre nos llega es la cabecera
        // `X-NavData-Cipher: aes-256-cbc-hmac-sha256` que manda `NavDataKeyProvider`. Si esa
        // cabecera se cayera, el sobre llegaría en GCM y **no lo podemos abrir** —GCM exigiría
        // BouncyCastle en un binario que se distribuye a pilotos—. Rechazarlo ANTES de intentarlo
        // es lo que deja la sesión sin NavData **sin fingir que hay credencial**, y el vuelo sigue.

        [TestMethod]
        public void CifradoCbcHmac_EsElQueSabemosAbrir()
        {
            Assert.IsTrue(NavDataCipher.IsSupportedCipher(NavDataCipher.CipherName));
            // El nombre lo publica el servidor: la caja no puede decidir si es el mismo cifrado.
            Assert.IsTrue(NavDataCipher.IsSupportedCipher("AES-256-CBC-HMAC-SHA256"));
        }

        [TestMethod]
        public void SobreGcm_SeRechazaYLaSesionSigueSinNavData()
        {
            Assert.IsFalse(NavDataCipher.IsSupportedCipher("aes-256-gcm"));
            Assert.IsFalse(NavDataCipher.IsSupportedCipher("aes-128-gcm"));
            Assert.IsFalse(NavDataCipher.IsSupportedCipher("chacha20-poly1305"));
        }

        [TestMethod]
        public void SinCampoDeCifrado_NoSeRechazaPorSospecha()
        {
            // El contrato puede omitir `cipher`: entonces la única autoridad es el MAC. Rechazar por
            // un campo que no vino dejaría al piloto sin NavData sin motivo (degradar sin datos).
            Assert.IsTrue(NavDataCipher.IsSupportedCipher(null));
            Assert.IsTrue(NavDataCipher.IsSupportedCipher(""));
        }

        // ── Utilidades del test: el cifrador — SOLO del test ──────────────────────

        private static void AssertRejectedPorMac(string payload)
        {
            Assert.IsFalse(NavDataCipher.TryOpen(payload, Ikm, out NavDataEnvelope env, out string error),
                           "un sobre manipulado no puede abrir");
            Assert.IsNull(env);
            StringAssert.Contains(error, "MAC", "el fallo tiene que ser del MAC, antes de descifrar");
        }

        /// <summary>
        /// **Cifrador del test, nunca de producción**: el cliente solo abre sobres, no los fabrica.
        /// Reproduce el contrato al pie de la letra para poder probar el `Open` sin depender de la red.
        /// </summary>
        private static string Seal(string json, string ikm)
        {
            byte[] encKey, macKey;
            SplitKeys(ikm, out encKey, out macKey);

            byte[] iv = new byte[NavDataCipher.IvLength];
            for (int i = 0; i < iv.Length; i++) iv[i] = (byte)(0x10 + i);

            byte[] ct;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode    = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key     = encKey;
                aes.IV      = iv;
                using (var enc = aes.CreateEncryptor())
                    ct = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(json), 0, Encoding.UTF8.GetByteCount(json));
            }

            byte[] blob = new byte[iv.Length + ct.Length + NavDataCipher.MacLength];
            Buffer.BlockCopy(iv, 0, blob, 0, iv.Length);
            Buffer.BlockCopy(ct, 0, blob, iv.Length, ct.Length);
            Buffer.BlockCopy(ComputeMac(ikm, iv, ct), 0, blob, iv.Length + ct.Length, NavDataCipher.MacLength);
            return Convert.ToBase64String(blob);
        }

        /// <summary>Rehace el MAC de un blob ya manipulado (para el caso «MAC válido, ct roto»).</summary>
        private static void RecomputeMac(byte[] blob, string ikm)
        {
            byte[] iv = new byte[NavDataCipher.IvLength];
            byte[] ct = new byte[blob.Length - NavDataCipher.IvLength - NavDataCipher.MacLength];
            Buffer.BlockCopy(blob, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(blob, iv.Length, ct, 0, ct.Length);
            Buffer.BlockCopy(ComputeMac(ikm, iv, ct), 0, blob, iv.Length + ct.Length, NavDataCipher.MacLength);
        }

        private static byte[] ComputeMac(string ikm, byte[] iv, byte[] ct)
        {
            byte[] encKey, macKey;
            SplitKeys(ikm, out encKey, out macKey);

            byte[] aad = Encoding.UTF8.GetBytes(NavDataCipher.AadText);
            byte[] signed = new byte[aad.Length + iv.Length + ct.Length];
            Buffer.BlockCopy(aad, 0, signed, 0, aad.Length);
            Buffer.BlockCopy(iv,  0, signed, aad.Length, iv.Length);
            Buffer.BlockCopy(ct,  0, signed, aad.Length + iv.Length, ct.Length);

            using (var hmac = new HMACSHA256(macKey))
                return hmac.ComputeHash(signed);
        }

        private static void SplitKeys(string ikm, out byte[] encKey, out byte[] macKey)
        {
            byte[] material = HkdfSha256.DeriveKey(
                Encoding.UTF8.GetBytes(ikm),
                Encoding.UTF8.GetBytes(NavDataCipher.SaltText),
                Encoding.UTF8.GetBytes(NavDataCipher.InfoText),
                NavDataCipher.KeyMaterialLength);

            encKey = new byte[NavDataCipher.KeyLength];
            macKey = new byte[NavDataCipher.KeyLength];
            Buffer.BlockCopy(material, 0, encKey, 0, NavDataCipher.KeyLength);
            Buffer.BlockCopy(material, NavDataCipher.KeyLength, macKey, 0, NavDataCipher.KeyLength);
        }
    }

    /// <summary>La comparación en tiempo constante, que es la puerta del MAC.</summary>
    [TestClass]
    public class FixedTimeEqualsTests
    {
        [TestMethod]
        public void MismoContenido_EsIgual()
        {
            Assert.IsTrue(FixedTimeEquals.Bytes(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
            Assert.IsTrue(FixedTimeEquals.Bytes(new byte[0], new byte[0]));
        }

        [TestMethod]
        public void UnSoloByteDistinto_NoEsIgual()
        {
            // El caso que importa: un MAC con 31 bytes bien y 1 mal tiene que decir que no.
            byte[] a = new byte[32];
            byte[] b = new byte[32];
            b[31] = 1;
            Assert.IsFalse(FixedTimeEquals.Bytes(a, b));
        }

        [TestMethod]
        public void DistintaLongitudONulo_NoEsIgual()
        {
            Assert.IsFalse(FixedTimeEquals.Bytes(new byte[] { 1, 2 }, new byte[] { 1, 2, 3 }));
            Assert.IsFalse(FixedTimeEquals.Bytes(null, new byte[1]));
            Assert.IsFalse(FixedTimeEquals.Bytes(new byte[1], null));
        }
    }
}
