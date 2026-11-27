using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **El contrato de `POST /api/auth/login`** (v1.0.0), fijado con la respuesta que documenta
    /// phpVMS y con los códigos que devuelve de verdad.
    ///
    /// Lo que se prueba aquí no es «que parsee»: son las tres cosas que pueden costar caro.
    ///
    /// 1. **Una `200` sin `api_key` legible no es un login bueno** (un proxy, una página de error con
    ///    código 200). Si se aceptara, el cliente guardaría basura como credencial.
    /// 2. **Solo HTTPS**: la respuesta entrega una credencial de larga vida. Un `http://` se rechaza
    ///    con un motivo que se lee en la pantalla.
    /// 3. **Los códigos se distinguen**: `401` (contraseña mal) y `403` (cuenta no activa) no pueden
    ///    acabar en el mismo mensaje, y un `429` no puede convertirse en «credenciales incorrectas»
    ///    porque entonces el piloto vuelve a intentarlo y alarga el bloqueo.
    /// </summary>
    [TestClass]
    public class AuthLoginContractTests
    {
        /// <summary>La respuesta real que documenta phpVMS, con sus campos tal cual.</summary>
        private const string OkBody =
            "{\"api_key\":\"9f2c1e40aa\",\"created\":false,\"user\":{\"id\":\"7\",\"pilot_id\":\"17\"," +
            "\"ident\":\"VHR017\",\"name\":\"Frank\",\"email\":\"p@ejemplo.com\",\"airline_id\":\"1\"," +
            "\"rank\":\"Captain\",\"state\":1}}";

        [TestMethod]
        public void Un200ConApiKeyEsUnLoginBueno()
        {
            LoginResult r = AuthLoginContract.Parse(200, OkBody);

            Assert.AreEqual(LoginOutcome.Ok, r.Outcome);
            Assert.AreEqual("9f2c1e40aa", r.ApiKey);
            Assert.IsFalse(r.Created);
            Assert.AreEqual("VHR017", r.PilotIdent);
            Assert.AreEqual("Frank", r.PilotName);
        }

        [TestMethod]
        public void CreatedVieneDeLaRespuesta_NoSeDeduceDel200()
        {
            LoginResult r = AuthLoginContract.Parse(
                200, "{\"api_key\":\"abc\",\"created\":true,\"user\":{\"ident\":\"VHR017\"}}");

            Assert.AreEqual(LoginOutcome.Ok, r.Outcome);
            Assert.IsTrue(r.Created, "el primer login lo marca el servidor con created:true");
        }

        [TestMethod]
        public void Un200SinApiKeyLegibleNoEsUnLoginBueno()
        {
            // Página de error de un proxy / captive portal con código 200: si se aceptara, se guardaría
            // basura como credencial y el piloto se quedaría sin poder conectar sin saber por qué.
            foreach (string body in new[] { "", "{}", "<html>mantenimiento</html>", "{\"api_key\":\"\"}", null })
            {
                LoginResult r = AuthLoginContract.Parse(200, body);
                Assert.AreEqual(LoginOutcome.Unavailable, r.Outcome, "cuerpo: " + (body ?? "<null>"));
                Assert.IsTrue(string.IsNullOrEmpty(r.ApiKey), "no se guarda ninguna clave");
            }
        }

        [TestMethod]
        public void LosCodigosDeErrorSeDistinguen()
        {
            Assert.AreEqual(LoginOutcome.InvalidCredentials, AuthLoginContract.Parse(401, "").Outcome);
            Assert.AreEqual(LoginOutcome.AccountNotActive, AuthLoginContract.Parse(403, "").Outcome);
            Assert.AreEqual(LoginOutcome.TooManyAttempts, AuthLoginContract.Parse(429, "").Outcome);
            Assert.AreEqual(LoginOutcome.BadRequest, AuthLoginContract.Parse(400, "").Outcome);
            Assert.AreEqual(LoginOutcome.BadRequest, AuthLoginContract.Parse(422, "").Outcome);
            Assert.AreEqual(LoginOutcome.Unavailable, AuthLoginContract.Parse(500, "").Outcome);
            Assert.AreEqual(LoginOutcome.Unavailable, AuthLoginContract.Parse(0, "").Outcome);
        }

        [TestMethod]
        public void ElMensajeDelServidorSeAprovecha_PeroAcotado()
        {
            LoginResult r = AuthLoginContract.Parse(401, "{\"message\":\"These credentials do not match.\"}");
            Assert.AreEqual("These credentials do not match.", r.Detail);

            // Un cuerpo enorme no se vuelca entero en la pantalla ni en el log.
            LoginResult big = AuthLoginContract.Parse(429, "{\"message\":\"" + new string('x', 1000) + "\"}");
            Assert.IsTrue(big.Detail.Length <= 200);
        }

        [TestMethod]
        public void UnCuerpoIlegibleNoLanza()
        {
            Assert.AreEqual(LoginOutcome.Unavailable, AuthLoginContract.Parse(200, "{{{").Outcome);
            Assert.AreEqual(LoginOutcome.InvalidCredentials, AuthLoginContract.Parse(401, "{{{").Outcome);
        }

        [TestMethod]
        public void SoloHttps_LaRespuestaEntregaUnaCredencialDeLargaVida()
        {
            Assert.IsTrue(AuthLoginContract.IsSecureUrl("https://vholar.co"));
            Assert.IsTrue(AuthLoginContract.IsSecureUrl("  https://vholar.co/  "));

            Assert.IsFalse(AuthLoginContract.IsSecureUrl("http://vholar.co"), "http no");
            Assert.IsFalse(AuthLoginContract.IsSecureUrl("vholar.co"), "sin esquema no");
            Assert.IsFalse(AuthLoginContract.IsSecureUrl(""), "vacía no");
            Assert.IsFalse(AuthLoginContract.IsSecureUrl(null), "null no");
            Assert.IsFalse(AuthLoginContract.IsSecureUrl("ftp://vholar.co"), "otro esquema no");
        }

        [TestMethod]
        public void ElEndpointSeArmaSinDuplicarBarra()
        {
            Assert.AreEqual("https://vholar.co/api/auth/login",
                AuthLoginContract.EndpointFor("https://vholar.co"));
            Assert.AreEqual("https://vholar.co/api/auth/login",
                AuthLoginContract.EndpointFor("https://vholar.co/"));
            Assert.IsNull(AuthLoginContract.EndpointFor("http://vholar.co"),
                "sin HTTPS no hay endpoint: se rechaza antes de mandar la contraseña");
        }

        [TestMethod]
        public void ElCuerpoLlevaLoginYPasswordYNadaMas()
        {
            JObject body = JObject.Parse(AuthLoginContract.BuildRequest("piloto@ejemplo.com", "secreto"));

            Assert.AreEqual(2, body.Count, "solo login y password");
            Assert.AreEqual("piloto@ejemplo.com", (string)body["login"]);
            Assert.AreEqual("secreto", (string)body["password"]);
        }
    }
}
