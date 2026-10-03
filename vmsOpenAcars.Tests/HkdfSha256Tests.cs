using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **HKDF-SHA256 (RFC 5869)** contra los vectores oficiales del RFC, que son públicos y no
    /// inventados: Test Case 1 (con salt e info) y Test Case 3 (**sin salt ni info**, el caso que se
    /// rompe cuando alguien «simplifica» poniendo `salt = new byte[0]` en vez del bloque de ceros).
    ///
    /// El porqué de esta implementación: phpVMS recomienda `aes-256-cbc-hmac-sha256` para .NET
    /// Framework, o sea **BCL pura**: nada de BouncyCastle y nada de GCM. La derivación es el IKM del
    /// contrato —el único secreto compartido es la `vms_api_key` del piloto— y por eso se prueba
    /// separada del sobre: si algún día falla, el test dice si el roto es el *extract* o el *expand*.
    /// </summary>
    [TestClass]
    public class HkdfSha256Tests
    {
        // ── Test Case 1 del RFC 5869 ──────────────────────────────────────────────
        private const string Tc1Ikm = "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b";
        private const string Tc1Salt = "000102030405060708090a0b0c";
        private const string Tc1Info = "f0f1f2f3f4f5f6f7f8f9";
        private const string Tc1Prk =
            "077709362c2e32df0ddc3f0dc47bba6390b6c73bb50f9c3122ec844ad7c2b3e5";
        private const string Tc1Okm =
            "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865";

        // ── Test Case 3 del RFC 5869: sin salt y sin info ─────────────────────────
        private const string Tc3Prk =
            "19ef24a32c717b167f33a91d6f648bdf96596776afdb6377ac434c1c293ccb04";
        private const string Tc3Okm =
            "8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8";

        private const int TcLength = 42;

        [TestMethod]
        public void Rfc5869TestCase1_Extract_DevuelveElPrkOficial()
        {
            byte[] prk = HkdfSha256.Extract(HexTest.From(Tc1Salt), HexTest.From(Tc1Ikm));
            Assert.AreEqual(Tc1Prk, HexTest.To(prk), "PRK del Test Case 1 del RFC 5869");
        }

        [TestMethod]
        public void Rfc5869TestCase1_Expand_DevuelveElOkmOficial()
        {
            byte[] okm = HkdfSha256.Expand(HexTest.From(Tc1Prk), HexTest.From(Tc1Info), TcLength);
            Assert.AreEqual(Tc1Okm, HexTest.To(okm), "OKM del Test Case 1 del RFC 5869");
        }

        [TestMethod]
        public void Rfc5869TestCase1_DeriveKey_DevuelveElOkmOficial()
        {
            // El camino que usa el sobre de NavData: extract + expand de una vez.
            byte[] okm = HkdfSha256.DeriveKey(
                HexTest.From(Tc1Ikm), HexTest.From(Tc1Salt), HexTest.From(Tc1Info), TcLength);
            Assert.AreEqual(Tc1Okm, HexTest.To(okm), "HKDF completo del Test Case 1 del RFC 5869");
        }

        [TestMethod]
        public void Rfc5869TestCase3_SinSaltNiInfo_DevuelveElPrkYElOkmOficiales()
        {
            // Salt vacío es el caso que el RFC manda tratar como 32 ceros. Con `new byte[0]` como
            // clave de HMAC (que es lo que hace la implementación ingenua) esto no sale.
            byte[] salt = new byte[0];
            byte[] info = new byte[0];

            Assert.AreEqual(Tc3Prk, HexTest.To(HkdfSha256.Extract(salt, HexTest.From(Tc1Ikm))),
                            "PRK del Test Case 3 (sin salt) del RFC 5869");
            Assert.AreEqual(Tc3Okm, HexTest.To(
                                HkdfSha256.DeriveKey(HexTest.From(Tc1Ikm), salt, info, TcLength)),
                            "OKM del Test Case 3 (sin salt ni info) del RFC 5869");
        }

        [TestMethod]
        public void Extract_SaltNulo_EsEquivalenteAlBloqueDeCeros()
        {
            // RFC 5869 §2.2: sin salt se usa HashLen ceros. Nulo y vacío tienen que dar lo mismo.
            byte[] ikm = HexTest.From(Tc1Ikm);
            Assert.AreEqual(
                HexTest.To(HkdfSha256.Extract(new byte[HkdfSha256.HashLength], ikm)),
                HexTest.To(HkdfSha256.Extract(null, ikm)));
        }

        [TestMethod]
        public void Expand_LongitudCero_DevuelveVacio()
        {
            Assert.AreEqual(0, HkdfSha256.Expand(HexTest.From(Tc1Prk), null, 0).Length);
        }

        [TestMethod]
        public void Expand_PorEncimaDelTopeDelRfc_Lanza()
        {
            // 255 bloques es el máximo del RFC: aceptar más devolvería material repitiendo contador.
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                HkdfSha256.Expand(HexTest.From(Tc1Prk), null, HkdfSha256.MaxOutputLength + 1));
        }
    }

    /// <summary>Conversión hexadecimal para los vectores del RFC. Solo existe en los tests.</summary>
    internal static class HexTest
    {
        internal static byte[] From(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return new byte[0];
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        internal static string To(byte[] bytes)
        {
            if (bytes == null) return null;
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
