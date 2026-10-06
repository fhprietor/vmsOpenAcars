using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models;
using vmsOpenAcars.Services;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Despacho de SimBrief servido por phpVMS. El cliente **no construye** la URL: llama a
    /// `GET {vms_api_url}/api/flights/{id}/dispatch?aircraft_id={id}` y abre `simbrief.url` tal
    /// cual. Estas pruebas fijan dos cosas:
    ///
    ///  1. La decisión «¿servidor o constructor local?» — <see cref="SimbriefDispatchPolicy"/> es
    ///     puro y se prueba sin red. La clave está en el **404**: el mismo código HTTP significa
    ///     «endpoint no desplegado» (→ respaldo) o «el vuelo ya no existe» (→ mensaje al piloto),
    ///     y lo decide el `error` del cuerpo.
    ///  2. El **parseo del cuerpo real** del endpoint desplegado, con una respuesta capturada en
    ///     vivo (VHR055, SKBG→SKCG, vuelo `yrVjGlOxg7lZ3BzN`, avión 36, 06/10/2026 04:09 UTC), que
    ///     es la que demuestra los valores auditados: `fl=33000` en **pies**, `maps=detail` y el
    ///     Item 18 en `extrarmk`.
    ///
    /// Los cuerpos de error también son los reales, leídos del servidor el mismo día: el 401 de
    /// Laravel (`{"error":{"code":"401",…}}`), el `404 flight_not_found`, el `422
    /// aircraft_not_found` (que responde igual si falta `aircraft_id`) y el `403 aircraft_not_allowed`.
    /// </summary>
    [TestClass]
    public class SimbriefDispatchPolicyTests
    {
        // ── Respuesta REAL del endpoint desplegado (recortada a lo que el cliente lee) ──────
        private const string RealDispatchBody =
            "{\"ok\":true,\"flight_id\":\"yrVjGlOxg7lZ3BzN\",\"aircraft_id\":36," +
            "\"simbrief\":{\"url\":\"https:\\/\\/dispatch.simbrief.com\\/options\\/custom?airline=VHR&fltnum=9821&orig=SKBG&dest=SKCG&type=B38M&reg=N665VH&cpt=Franklin+Prieto&civalue=30&units=kgs&maps=detail&deph=04&depm=49&flighttype=s&fl=33000&pax=166&cargo=7991&extrarmk=CS%2FVHOLAR+IVAOVA%2FVHR+OPR%2FVHR\"," +
            "\"params\":{\"airline\":\"VHR\",\"fltnum\":\"9821\",\"orig\":\"SKBG\",\"dest\":\"SKCG\",\"type\":\"B38M\",\"reg\":\"N665VH\",\"cpt\":\"Franklin Prieto\",\"civalue\":\"30\",\"units\":\"kgs\",\"maps\":\"detail\",\"deph\":\"04\",\"depm\":\"49\",\"flighttype\":\"s\",\"fl\":\"33000\",\"pax\":\"166\",\"cargo\":\"7991\",\"extrarmk\":\"CS\\/VHOLAR IVAOVA\\/VHR OPR\\/VHR\"}}," +
            "\"applicable\":true,\"reason\":\"ok\",\"flight_number\":\"9821\",\"route_code\":null," +
            "\"aircraft_type\":\"B38M\",\"registration\":\"N665VH\"," +
            "\"route\":{\"dpt\":\"SKBG\",\"arr\":\"SKCG\"}," +
            "\"block\":{\"minutes\":105,\"hours\":1.75,\"cost_hour\":9142}," +
            "\"suggestion\":{\"pax\":166,\"cargo\":7991,\"pax_revenue\":6064,\"cargo_revenue\":22374.8,\"revenue\":28438.8,\"target\":28437.18,\"margin_pct\":20,\"target_reached\":true}," +
            "\"notes\":[{\"level\":\"warn\",\"text\":\"Rendimiento: plazas recortadas de 178 a 166 por elevacion 3.897 ft (-3,9 %).\"}," +
            "{\"level\":\"info\",\"text\":\"El pasaje va al maximo (166 plazas); el resto del objetivo se cubre con 7.991 kg de carga.\"}," +
            "{\"level\":\"ok\",\"text\":\"Margen estimado: +20,0 %.\"}]}";

        private const string RealFlightNotFoundBody  = "{ \"ok\": false, \"error\": \"flight_not_found\" }";
        private const string RealAircraftNotFoundBody = "{ \"ok\": false, \"error\": \"aircraft_not_found\" }";
        private const string RealAircraftNotAllowedBody = "{ \"ok\": false, \"error\": \"aircraft_not_allowed\" }";
        // 401 real de Laravel: aquí `error` es un OBJETO, no una cadena.
        private const string RealUnauthorizedBody =
            "{\"error\":{\"code\":\"401\",\"http_code\":\"Unauthorized\",\"message\":\"Invalid or missing API key (User not found with key \\\"badkey\\\")\"}}";
        // 404 real de una ruta que no existe (servidor sin el endpoint desplegado), capturado en
        // vivo el 06/10/2026. Ojo: aquí `error` es un OBJETO con `status`/`message`, no la cadena
        // `flight_not_found` — es justo lo que distingue los dos 404.
        private const string OldServerRouteNotFoundBody =
            "{\"type\":\"https:\\/\\/phpvms.net\\/errors\\/not-found\",\"title\":\"The route api\\/flights\\/yrVjGlOxg7lZ3BzN\\/dispatch could not be found.\"," +
            "\"details\":\"The route api\\/flights\\/yrVjGlOxg7lZ3BzN\\/dispatch could not be found.\",\"status\":404," +
            "\"error\":{\"status\":404,\"message\":\"The route api\\/flights\\/yrVjGlOxg7lZ3BzN\\/dispatch could not be found.\"}}";

        private static SimbriefDispatch Parse(int status, string body)
        {
            var d = new SimbriefDispatch { StatusCode = status };
            SimbriefDispatchService.ParseInto(d, body);
            return d;
        }

        private static SimbriefDispatch RealDispatch() => Parse(200, RealDispatchBody);

        // ── Parseo del cuerpo real ────────────────────────────────────────────────────────

        [TestMethod]
        public void TheRealResponse_CarriesTheServerUrl_AndTheClientUsesItUntouched()
        {
            var d = RealDispatch();

            Assert.IsFalse(string.IsNullOrWhiteSpace(d.Url), "el endpoint desplegado trae simbrief.url");

            var decision = SimbriefDispatchPolicy.Decide(d);
            Assert.AreEqual(SimbriefDispatchSource.ServerUrl, decision.Source);
            Assert.AreEqual("servidor", decision.Reason);

            // La URL se abre TAL CUAL: ni se recorta ni se re-codifica. Es lo que elimina la
            // segunda versión del plan.
            Assert.IsTrue(d.Url.StartsWith("https://dispatch.simbrief.com/options/custom?", StringComparison.Ordinal));
            StringAssert.Contains(d.Url, "maps=detail");
            StringAssert.Contains(d.Url, "extrarmk=CS%2FVHOLAR+IVAOVA%2FVHR+OPR%2FVHR");
        }

        [TestMethod]
        public void TheRealResponse_SendsFlInFeet_NotInHundredsOfFeet()
        {
            // El `fl` auditado: 33.000 ft van como 33000 (o FL330), nunca como 330.
            var d = RealDispatch();

            StringAssert.Contains(d.Url, "fl=33000");
            Assert.IsFalse(d.Url.Contains("fl=330&") || d.Url.EndsWith("fl=330"),
                "fl=330 daría 330 ft: 100 veces por debajo de los 33.000 pedidos");
        }

        [TestMethod]
        public void TheRealResponse_SendsMapsDetail_AndNoStaticUrl()
        {
            var d = RealDispatch();

            StringAssert.Contains(d.Url, "maps=detail");
            Assert.IsFalse(d.Url.Contains("maps=detailed"), "`detailed` no es un valor de SimBrief");
            Assert.IsFalse(d.Url.Contains("static_url"), "`static_url` no existe en la API de SimBrief");
        }

        [TestMethod]
        public void TheRealResponse_Item18IsAlreadyInTheUrl()
        {
            // El Item 18 lo pone el servidor (ajuste `simbrief.extrarmk` de phpVMS): por eso el
            // camino principal NO añade nada — si sumáramos el nuestro, el plan saldría con dos.
            var d = RealDispatch();

            StringAssert.Contains(d.Url, "extrarmk=");
            Assert.AreEqual("CS/VHOLAR IVAOVA/VHR OPR/VHR", d.Url.Substring(d.Url.IndexOf("extrarmk=", StringComparison.Ordinal) + 9)
                .Replace("%2F", "/").Replace("+", " "));
        }

        [TestMethod]
        public void TheRealResponse_Suggestion_IsTheServers_NotOurs()
        {
            var d = RealDispatch();

            Assert.IsNotNull(d.Suggestion);
            Assert.AreEqual(166, d.Suggestion.Pax);
            Assert.AreEqual(7991.0, d.Suggestion.Cargo.Value, 0.001);
            Assert.AreEqual(20.0, d.Suggestion.MarginPct.Value, 0.001);
            Assert.IsTrue(d.Suggestion.TargetReached.Value);
            Assert.AreEqual(28438.8, d.Suggestion.Revenue.Value, 0.001);
            Assert.AreEqual(28437.18, d.Suggestion.Target.Value, 0.001);
        }

        [TestMethod]
        public void TheRealResponse_Notes_KeepTheServerTextAndLevel()
        {
            var d = RealDispatch();

            Assert.AreEqual(3, d.Notes.Count);
            CollectionAssert.AreEqual(new[] { "warn", "info", "ok" }, d.Notes.Select(n => n.Level).ToArray());
            // El texto se muestra tal cual: no se reescribe ni se recalcula.
            StringAssert.Contains(d.Notes[0].Text, "plazas recortadas de 178 a 166");
            StringAssert.Contains(d.Notes[1].Text, "7.991 kg de carga");
            StringAssert.Contains(d.Notes[2].Text, "+20,0 %");
        }

        [TestMethod]
        public void TheRealResponse_IsApplicable()
        {
            var d = RealDispatch();
            Assert.IsTrue(d.Applicable);
            Assert.AreEqual("ok", d.Reason);
        }

        [TestMethod]
        public void CharterOrFerry_NotApplicable_HasUrlWithoutSuggestion_AndIsNotAnError()
        {
            // `applicable=false` (charter/ferry): hay URL pero sin pax/cargo. No puede parecer un
            // error: el contrato dice que ahí el sugerido no existe.
            var d = Parse(200, "{\"ok\":true,\"applicable\":false,\"reason\":\"charter\"," +
                               "\"simbrief\":{\"url\":\"https://dispatch.simbrief.com/options/custom?airline=VHR&fltnum=55CH\"}}");

            Assert.IsFalse(d.Applicable);
            Assert.IsNull(d.Suggestion);
            Assert.AreEqual(SimbriefDispatchSource.ServerUrl, SimbriefDispatchPolicy.Decide(d).Source);
        }

        // ── Decisión: servidor vs. constructor local ──────────────────────────────────────

        [TestMethod]
        public void ServerAnswers_NoMatterWhatTheLocalBuilderCouldDo_TheServerUrlWins()
        {
            // El punto del encargo: si el endpoint contesta, se usa SU url aunque el constructor
            // local funcione perfectamente. Solo el camino del servidor elimina la segunda versión.
            var d = RealDispatch();
            Assert.AreEqual(SimbriefDispatchSource.ServerUrl, SimbriefDispatchPolicy.Decide(d).Source);
        }

        [TestMethod]
        public void NoResponse_NetworkOrTimeout_FallsBackWithAReason()
        {
            var decision = SimbriefDispatchPolicy.Decide(new SimbriefDispatch { StatusCode = 0 });

            Assert.AreEqual(SimbriefDispatchSource.LocalFallback, decision.Source);
            Assert.AreEqual("sin_respuesta", decision.Reason);
            Assert.IsNull(decision.ErrorKey);
        }

        [TestMethod]
        public void NullDispatch_DoesNotThrow_AndFallsBack()
        {
            // Degradar sin datos: sin objeto no hay nada que decidir y nunca se lanza.
            var decision = SimbriefDispatchPolicy.Decide(null);
            Assert.AreEqual(SimbriefDispatchSource.LocalFallback, decision.Source);
        }

        [TestMethod]
        public void OldServer_404RouteNotFound_FallsBack()
        {
            // Mismo 404 que el caso siguiente, pero SIN `flight_not_found`: la ruta no existe
            // porque phpVMS todavía no tiene el endpoint → respaldo.
            var d = Parse(404, OldServerRouteNotFoundBody);

            var decision = SimbriefDispatchPolicy.Decide(d);
            Assert.AreEqual(SimbriefDispatchSource.LocalFallback, decision.Source);
            Assert.AreEqual("404_endpoint_no_desplegado", decision.Reason);
        }

        [TestMethod]
        public void ServerError_500_FallsBack()
        {
            var decision = SimbriefDispatchPolicy.Decide(new SimbriefDispatch { StatusCode = 500 });
            Assert.AreEqual(SimbriefDispatchSource.LocalFallback, decision.Source);
            Assert.AreEqual("http_500", decision.Reason);
        }

        [TestMethod]
        public void ServerAnswers200_WithoutUrl_FallsBack()
        {
            var decision = SimbriefDispatchPolicy.Decide(new SimbriefDispatch { StatusCode = 200 });
            Assert.AreEqual(SimbriefDispatchSource.LocalFallback, decision.Source);
        }

        // ── Los cuatro errores, con su clave de idioma ────────────────────────────────────

        [TestMethod]
        public void FlightNotFound_404_IsRejectedWithTheFlightMessage()
        {
            var decision = SimbriefDispatchPolicy.Decide(Parse(404, RealFlightNotFoundBody));

            Assert.AreEqual(SimbriefDispatchSource.Rejected, decision.Source);
            Assert.AreEqual(SimbriefDispatchPolicy.KeyFlightNotFound, decision.ErrorKey);
            Assert.AreEqual("404_flight_not_found", decision.Reason);
        }

        [TestMethod]
        public void AircraftNotFound_422_IsRejectedWithTheAircraftMessage()
        {
            var decision = SimbriefDispatchPolicy.Decide(Parse(422, RealAircraftNotFoundBody));

            Assert.AreEqual(SimbriefDispatchSource.Rejected, decision.Source);
            Assert.AreEqual(SimbriefDispatchPolicy.KeyAircraftNotFound, decision.ErrorKey);
        }

        [TestMethod]
        public void AircraftNotAllowed_403_IsRejectedWithTheSubfleetMessage()
        {
            var decision = SimbriefDispatchPolicy.Decide(Parse(403, RealAircraftNotAllowedBody));

            Assert.AreEqual(SimbriefDispatchSource.Rejected, decision.Source);
            Assert.AreEqual(SimbriefDispatchPolicy.KeyAircraftNotAllowed, decision.ErrorKey);
        }

        [TestMethod]
        public void InvalidKey_401_IsRejected_EvenThoughTheErrorBodyIsAnObject()
        {
            // El 401 de Laravel trae `error` como objeto: hay que leerlo igual, o el piloto vería
            // «servidor no disponible» en vez de «clave inválida».
            var d = Parse(401, RealUnauthorizedBody);

            Assert.AreEqual("401", d.ErrorCode);
            var decision = SimbriefDispatchPolicy.Decide(d);
            Assert.AreEqual(SimbriefDispatchSource.Rejected, decision.Source);
            Assert.AreEqual(SimbriefDispatchPolicy.KeyInvalidApiKey, decision.ErrorKey);
        }

        [TestMethod]
        public void RejectedErrors_DoNotFallBack_SoThePilotIsNotSentToPlanTheWrongAircraft()
        {
            // Un respaldo con el avión equivocado haría planificar un vuelo que la aerolínea va a
            // rechazar: cuando el servidor dice «no», se para y se explica.
            foreach (var d in new[]
            {
                Parse(404, RealFlightNotFoundBody),
                Parse(422, RealAircraftNotFoundBody),
                Parse(403, RealAircraftNotAllowedBody),
                Parse(401, RealUnauthorizedBody),
            })
            {
                var decision = SimbriefDispatchPolicy.Decide(d);
                Assert.AreEqual(SimbriefDispatchSource.Rejected, decision.Source,
                    $"un {d.StatusCode} con error {d.ErrorCode} no puede caer al constructor local");
                Assert.IsFalse(string.IsNullOrEmpty(decision.ErrorKey));
            }
        }

        [TestMethod]
        public void Unknown422_WithoutAKnownErrorCode_FallsBack()
        {
            var decision = SimbriefDispatchPolicy.Decide(Parse(422, "{\"ok\":false,\"error\":\"validation_error\"}"));
            Assert.AreEqual(SimbriefDispatchSource.LocalFallback, decision.Source);
        }

        // ── Auditoría del constructor local (respaldo) ────────────────────────────────────

        [TestMethod]
        public void LocalBuilder_SendsMapsDetail_KeepsExtraRmk_AndDropsStaticUrlAndFl()
        {
            // El respaldo se audita aparte: `maps` era `detailed`, `static_url` no existe en
            // SimBrief y `fl` no debe aparecer (y si aparece, en pies). `extrarmk` SE CONSERVA:
            // en un phpVMS viejo nadie más pone el Item 18, y sin él el plan saldría sin él.
            var service = new SimbriefEnhancedService();
            string url = service.GenerateDispatchUrl(
                new Flight
                {
                    Airline = "VHR", FlightNumber = "9821",
                    Departure = "SKBG", Arrival = "SKCG",
                    Route = "DCT", FlightType = "J",
                },
                new Pilot { Name = "Franklin Prieto" },
                new Aircraft { Type = "B38M", Registration = "N665VH" },
                // El valor real del `App.config` del piloto (dev): es el Item 18 de la aerolínea.
                "CS/VHOLAR IVAOVA/VHR OPR/VHR");

            var query = ParseQuery(url);

            Assert.AreEqual("detail", query["maps"]);
            Assert.IsFalse(query.ContainsKey("static_url"), "static_url no existe en la API de SimBrief");
            Assert.IsFalse(query.ContainsKey("fl"), "el constructor local no manda altitud; si la mandara, iría en pies");
            Assert.AreEqual("CS/VHOLAR IVAOVA/VHR OPR/VHR", query["extrarmk"],
                "el respaldo conserva el Item 18: en un phpVMS viejo nadie más lo pone");
        }

        [TestMethod]
        public void LocalBuilder_WithoutExtraRmk_DoesNotInventOne()
        {
            // Sin ajuste no se manda `extrarmk` vacío: `BuildQueryString` omite los valores vacíos.
            var service = new SimbriefEnhancedService();
            string url = service.GenerateDispatchUrl(
                new Flight { Airline = "VHR", FlightNumber = "9821", Departure = "SKBG", Arrival = "SKCG", FlightType = "J" },
                new Pilot { Name = "Franklin Prieto" },
                new Aircraft { Type = "B38M", Registration = "N665VH" },
                "");

            Assert.IsFalse(ParseQuery(url).ContainsKey("extrarmk"));
        }

        private static Dictionary<string, string> ParseQuery(string url)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int q = url.IndexOf('?');
            if (q < 0) return result;

            foreach (string pair in url.Substring(q + 1).Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                int eq = pair.IndexOf('=');
                string key = eq < 0 ? pair : pair.Substring(0, eq);
                string val = eq < 0 ? "" : Uri.UnescapeDataString(pair.Substring(eq + 1).Replace("+", " "));
                result[key] = val;
            }
            return result;
        }
    }
}
