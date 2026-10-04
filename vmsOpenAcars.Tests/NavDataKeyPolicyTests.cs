using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **El ciclo de vida de la credencial de NavData** (`NavDataKeyPolicy`), §6 del documento de
    /// phpVMS.
    ///
    /// Las reglas y el porqué: la `navdata_api_key` **estaba publicada** en el `.config`; ahora se pide
    /// a `GET /api/navdata` **una vez por sesión** —el límite del servidor es **30/min por piloto**— y
    /// se guarda **en memoria** hasta `expires_at`. Un `401` (clave inválida o piloto no `ACTIVE`) o un
    /// `400` (`unsupported-cipher`) son deterministas y **no se reintentan en bucle**; un `503`
    /// `navdata-not-configured` deja el vuelo **sin scoring NavData**, como sin clave.
    ///
    /// **No hay respaldo** (decisión del mantenedor): la clave del `.config` **era la filtrada** y su
    /// respaldo **enmascaraba** que el sobre nuevo se rompiera. Este test fija los dos lados: lo que se
    /// pide, lo que **no** se vuelve a pedir, y que una clave escrita en el `.config` **se ignora**.
    /// </summary>
    [TestClass]
    public class NavDataKeyPolicyTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void ClaveEnElConfig_SeIgnora_YSigueMandandoElSobre()
        {
            // El respaldo se eliminó a propósito: la clave del `.config` era la misma que se filtró y,
            // mientras existiera, un sobre roto no se notaba —el cliente seguía volando con la clave
            // vieja—. Con una clave puesta en el `.config` y sin sobre, la decisión es la misma que sin
            // ella: pedir el sobre. Y con el sobre vigente, usarlo, aunque el `.config` traiga otra.
            const string filtrada = "vhr-clave-que-se-filtro";

            Assert.AreEqual(NavDataKeyAction.RequestEnvelope,
                NavDataKeyPolicy.Decide(filtrada, false, null, false, Now));
            Assert.AreEqual(NavDataKeyAction.UseCachedEnvelope,
                NavDataKeyPolicy.Decide(filtrada, true, Now.AddDays(7), false, Now));
        }

        [TestMethod]
        public void ClaveEnElConfig_NoEsLaClaveEfectiva()
        {
            // La resolución, no solo la decisión: `NavDataApiKeyEffective` tiene que RESOLVER el sobre
            // en memoria y nada más. Sin sobre: **cadena vacía**, no la del `.config`.
            //
            // Se mira el cuerpo compilado del `get` a propósito, y no se escribe una clave en el
            // `.config` para leerla después: el host de pruebas es `vstest.console.exe`, que vive en
            // `Program Files (x86)` y su `.config` no se puede guardar (acceso denegado). Un test que
            // no puede escribir lo que dice comprobar no comprueba nada; este sí: si alguien vuelve a
            // colar la lectura de `navdata_api_key` aquí dentro, el `get` deja de ser una sola carga
            // estática y la prueba cae.
            Assert.AreEqual("", AppConfig.NavDataApiKeyEffective,
                "sin sobre no hay clave: una escrita en el `.config` se ignora");

            MethodInfo getter = typeof(AppConfig)
                .GetProperty(nameof(AppConfig.NavDataApiKeyEffective))
                ?.GetGetMethod(nonPublic: true);
            Assert.IsNotNull(getter, "la propiedad tiene que seguir siendo una propiedad");
            Assert.AreEqual(0, getter.GetMethodBody().LocalVariables.Count,
                "el `get` de la clave efectiva no puede tener variables locales: solo puede resolver " +
                "el sobre en memoria, no comparar ni leer el `.config`");

            // Y el único sitio que sí lee la clave cruda es el lector de diagnóstico, que no se usa
            // para volar (nadie llama a `NavDataApiKey` fuera del propio `AppConfig` y los tests).
            Assert.IsNotNull(typeof(AppConfig).GetProperty(nameof(AppConfig.NavDataApiKey)),
                "el lector de diagnóstico se conserva, pero no alimenta la clave efectiva");
        }

        // ── Motivo del aviso «sin clave» ─────────────────────────────────────────

        [TestMethod]
        public void MotivoDelAviso_CadaFalloDicePorQue()
        {
            // «No hay clave» sin motivo no se puede diagnosticar: el aviso del log lleva el porqué.
            Assert.AreEqual("NavDataReason_MissingConfig",
                NavDataKeyPolicy.ReasonKey(NavDataKeyOutcome.MissingConfiguration));
            Assert.AreEqual("NavDataReason_Unauthorized",
                NavDataKeyPolicy.ReasonKey(NavDataKeyOutcome.Unauthorized));
            Assert.AreEqual("NavDataReason_NotConfigured",
                NavDataKeyPolicy.ReasonKey(NavDataKeyOutcome.NotConfigured));
            Assert.AreEqual("NavDataReason_UnsupportedCipher",
                NavDataKeyPolicy.ReasonKey(NavDataKeyOutcome.UnsupportedCipher));
            Assert.AreEqual("NavDataReason_Failed",
                NavDataKeyPolicy.ReasonKey(NavDataKeyOutcome.Failed));
        }

        [TestMethod]
        public void ConSobreEntregado_NoHayMotivoQueDar()
        {
            Assert.AreEqual("", NavDataKeyPolicy.ReasonKey(NavDataKeyOutcome.Delivered));
            Assert.AreEqual("", NavDataKeyPolicy.ReasonKey(NavDataKeyOutcome.Idle));
        }

        [TestMethod]
        public void ConSobreVigenteEnMemoria_SeUsaSinVolverAPedir()
        {
            // Una petición por sesión: el sobre vive en memoria hasta expirar.
            Assert.AreEqual(NavDataKeyAction.UseCachedEnvelope,
                NavDataKeyPolicy.Decide("", true, Now.AddDays(7), false, Now));
        }

        [TestMethod]
        public void SinSaberCuandoVence_SeSigueUsandoElSobre()
        {
            // `expires_at` ilegible no puede dejar al piloto sin NavData: null es «no sé», no «venció».
            Assert.AreEqual(NavDataKeyAction.UseCachedEnvelope,
                NavDataKeyPolicy.Decide("", true, null, false, Now));
        }

        [TestMethod]
        public void SinClaveNiSobre_YaIntentado_NoSeReintenta()
        {
            // 401/400/503 no se reintentan en bucle: la sesión se queda sin NavData y el vuelo no se
            // resiente. Este es el lado que evita el bucle de peticiones contra phpVMS.
            Assert.AreEqual(NavDataKeyAction.GiveUp,
                NavDataKeyPolicy.Decide("", false, null, true, Now));
        }

        [TestMethod]
        public void SobreVencido_SinHaberIntentado_SePide()
        {
            Assert.AreEqual(NavDataKeyAction.RequestEnvelope,
                NavDataKeyPolicy.Decide("", true, Now.AddMinutes(-1), false, Now));
        }

        [TestMethod]
        public void SobreVencido_YaIntentado_SeRinde()
        {
            // Vencido y con el intento gastado: mejor un vuelo sin NavData que insistir contra el
            // límite de 30/min del servidor.
            Assert.AreEqual(NavDataKeyAction.GiveUp,
                NavDataKeyPolicy.Decide("", true, Now.AddMinutes(-1), true, Now));
        }

        [TestMethod]
        public void PrimeraVezDeLaSesion_SePideElSobre()
        {
            Assert.AreEqual(NavDataKeyAction.RequestEnvelope,
                NavDataKeyPolicy.Decide("", false, null, false, Now));
        }

        // ── Invalidación de la caché por cambio de `key_id` ───────────────────────

        [TestMethod]
        public void KeyIdDistinto_ObligaAPurgarLaCache()
        {
            Assert.IsTrue(NavDataKeyPolicy.CacheMustBePurged("kd-1", "kd-2"));
        }

        [TestMethod]
        public void MismoKeyId_NoTocaLaCache()
        {
            Assert.IsFalse(NavDataKeyPolicy.CacheMustBePurged("kd-1", "kd-1"));
        }

        [TestMethod]
        public void SinKeyIdAnterior_NoSePurgaPorSospecha()
        {
            // Degradar sin datos: la primera sesión no tiene con qué comparar y no borra nada.
            Assert.IsFalse(NavDataKeyPolicy.CacheMustBePurged(null, "kd-1"));
            Assert.IsFalse(NavDataKeyPolicy.CacheMustBePurged("", "kd-1"));
        }

        // ── Comparación y elección de la URL entregada ────────────────────────────

        [TestMethod]
        public void UrlEntregada_SeComparaSinBarraFinalNiMayusculas()
        {
            Assert.IsTrue(NavDataKeyPolicy.UrlMatches(
                "https://navdata.vholar.co/api/v1/", "https://navdata.vholar.co/api/v1"));
            Assert.IsTrue(NavDataKeyPolicy.UrlMatches(
                "https://NAVDATA.vholar.co/api/v1", "https://navdata.vholar.co/api/v1/"));

            // Y la que se usa es la del sobre TAL CUAL, sin completarle ni quitarle rutas: el contrato
            // dice que phpVMS entrega la base completa.
            Assert.AreEqual("https://navdata.vholar.co/api/v1",
                NavDataKeyPolicy.ResolveUrl(" https://navdata.vholar.co/api/v1 ", "https://otra/api/v1"));
        }

        [TestMethod]
        public void UrlDistintaONula_NoCoincide()
        {
            // Si phpVMS mueve el servicio, la diferencia tiene que verse en el reporte, no esconderse.
            Assert.IsFalse(NavDataKeyPolicy.UrlMatches(
                "https://navdata.otra.com/api/v1", "https://navdata.vholar.co/api/v1"));
            Assert.IsFalse(NavDataKeyPolicy.UrlMatches(null, "https://navdata.vholar.co/api/v1"));
            Assert.IsFalse(NavDataKeyPolicy.UrlMatches("https://x/", null));

            // Sin `url` en el sobre (o vacía) se cae a la del `.config`: es el único caso en que esa
            // manda, y así el cliente no se queda sin base con un sobre incompleto.
            Assert.AreEqual("https://navdata.vholar.co/api/v1",
                NavDataKeyPolicy.ResolveUrl("", "https://navdata.vholar.co/api/v1"));
            Assert.AreEqual("https://navdata.vholar.co/api/v1",
                NavDataKeyPolicy.ResolveUrl(null, "https://navdata.vholar.co/api/v1"));
            Assert.AreEqual("", NavDataKeyPolicy.ResolveUrl(null, null));
        }
    }
}
