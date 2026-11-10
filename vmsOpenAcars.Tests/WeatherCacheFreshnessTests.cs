using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using vmsOpenAcars.Helpers;
using vmsOpenAcars.Models.NavData;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **La caché del METAR se caduca por observación, no solo por reloj** (`Helpers/WeatherCacheFreshness`).
    ///
    /// Los dos METAR son los **reales de este caso**, medidos el 10/11/2026 contra
    /// `GET https://navdata.vholar.co/api/v1/weather/SKRG/` (03:36 UTC): el servicio ya publicaba el
    /// de las `100300Z` mientras el cliente seguía devolviendo el de las `100200Z`, y el botón
    /// REFRESH no cambiaba nada porque la caché de `NavDataClient` solo caducaba por el TTL de
    /// 5 minutos y **nadie la invalidaba**. La forma es la que devolvió el servicio:
    /// `VRB01KT 9999 NSC 14/12 Q1028`.
    ///
    /// Qué decide cada prueba:
    ///  - **(a) caducada por observación**: `100200Z` deja de servir en cuanto dan las 03:00, aunque
    ///    la entrada tenga 40 segundos. El TTL no la vería.
    ///  - **(b) válida**: `100300Z` a las 03:36 con la entrada descargada hace 3 minutos.
    ///  - **(c) sin grupo de hora**: no se caduca por esta vía; decide el TTL. Degradar sin dato,
    ///    nunca descartar por suposición.
    ///  - **(d) `forceRefresh`**: el refresco explícito se salta una entrada que por lo demás es
    ///    válida. Es lo que hace que el piloto vea el METAR nuevo al pulsar REFRESH.
    ///  - **cambio de día**: `302350Z` capturado a las 00:20 del día 1 es del 30 a las 23:50 y queda
    ///    por debajo de las 00:00 del día 1.
    /// </summary>
    [TestClass]
    public class WeatherCacheFreshnessTests
    {
        // Los dos METAR reales del 10/11/2026 en SKRG (misma forma, distinta hora de observación).
        private const string Skrg0200Z = "METAR SKRG 100200Z VRB01KT 9999 NSC 14/12 Q1028";
        private const string Skrg0300Z = "METAR SKRG 100300Z VRB01KT 9999 NSC 14/12 Q1028";

        private static readonly TimeSpan Ttl5Min = TimeSpan.FromMinutes(5);

        [TestMethod]
        public void CacheDeLas0200Z_PasadasLas0300_EstaCaducada()
        {
            var fetched = new DateTime(2026, 11, 10, 2, 5, 0, DateTimeKind.Utc);
            var now     = new DateTime(2026, 11, 10, 3, 36, 0, DateTimeKind.Utc);

            Assert.IsTrue(WeatherCacheFreshness.IsObservationStale(Skrg0200Z, fetched, now));
            Assert.IsFalse(WeatherCacheFreshness.CanUseCached(
                forceRefresh: false, Skrg0200Z, fetched, now, Ttl5Min));
        }

        [TestMethod]
        public void CacheDeLas0200Z_40SegundosDespuesDeLas0300_SigueCaducada()
        {
            // El caso que el TTL no puede ver: descargada a las 02:59:40 y consultada a las
            // 03:00:20. Han pasado 40 s, pero el METAR de las 02:00 ya fue reemplazado.
            var fetched = new DateTime(2026, 11, 10, 2, 59, 40, DateTimeKind.Utc);
            var now     = new DateTime(2026, 11, 10, 3, 0, 20, DateTimeKind.Utc);

            Assert.IsTrue(now - fetched < Ttl5Min);
            Assert.IsFalse(WeatherCacheFreshness.CanUseCached(
                forceRefresh: false, Skrg0200Z, fetched, now, Ttl5Min));
        }

        [TestMethod]
        public void CacheDeLas0300Z_A_Las0336_EsValida()
        {
            // Descargada a las 03:33 (dentro del TTL) y consultada a las 03:36: la observación de
            // las 03:00 es la vigente, así que se sirve sin salir a la red.
            var fetched = new DateTime(2026, 11, 10, 3, 33, 0, DateTimeKind.Utc);
            var now     = new DateTime(2026, 11, 10, 3, 36, 0, DateTimeKind.Utc);

            Assert.IsFalse(WeatherCacheFreshness.IsObservationStale(Skrg0300Z, fetched, now));
            Assert.IsTrue(WeatherCacheFreshness.CanUseCached(
                forceRefresh: false, Skrg0300Z, fetched, now, Ttl5Min));
        }

        [TestMethod]
        public void JustoAlas0300_ElMetarDeLas0300SigueValido()
        {
            // El umbral es «anterior a la última hora en punto»: a las 03:00:00 exactas, las 03:00
            // no son anteriores a las 03:00. Solo el de las 02:00 ha quedado atrás.
            var now = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);

            Assert.IsFalse(WeatherCacheFreshness.IsObservationStale(Skrg0300Z, now, now));
            Assert.IsTrue(WeatherCacheFreshness.IsObservationStale(Skrg0200Z, now.AddMinutes(-1), now));
        }

        [TestMethod]
        public void MetarSinGrupoDeHora_NoCaducaPorObservacion()
        {
            // Sin `ddHHMMZ` no hay dato con el que caducar: el TTL es el que decide, y aquí está
            // dentro de los 5 minutos.
            const string sinHora = "METAR SKRG 10003KT 9999 NSC 14/12 Q1028";
            var fetched = new DateTime(2026, 11, 10, 3, 35, 0, DateTimeKind.Utc);
            var now     = new DateTime(2026, 11, 10, 3, 36, 0, DateTimeKind.Utc);

            Assert.IsFalse(WeatherCacheFreshness.IsObservationStale(sinHora, fetched, now));
            Assert.IsTrue(WeatherCacheFreshness.CanUseCached(
                forceRefresh: false, sinHora, fetched, now, Ttl5Min));
        }

        [TestMethod]
        public void ForceRefresh_IgnoraUnaEntradaValida()
        {
            // Misma entrada que (b), que sin forzar se sirve; el refresco explícito la descarta
            // para pedir red. Es el botón REFRESH del piloto y el temporizador de `MetarService`.
            var fetched = new DateTime(2026, 11, 10, 3, 33, 0, DateTimeKind.Utc);
            var now     = new DateTime(2026, 11, 10, 3, 36, 0, DateTimeKind.Utc);

            Assert.IsTrue(WeatherCacheFreshness.CanUseCached(
                forceRefresh: false, Skrg0300Z, fetched, now, Ttl5Min));
            Assert.IsFalse(WeatherCacheFreshness.CanUseCached(
                forceRefresh: true, Skrg0300Z, fetched, now, Ttl5Min));
        }

        [TestMethod]
        public void CambioDeDia_UnaObservacionDeAyerEsCaducada()
        {
            // `302350Z` capturado a las 00:20 del 1 de noviembre: el grupo solo trae el día, así que
            // `MetarObservationTime` lo ancla al mes anterior (30/10 23:50). Queda por debajo de la
            // última hora en punto (01/11 00:00) y la entrada está caducada.
            const string skcg = "METAR SKCG 302350Z 32012KT 9999 SCT020 28/24 Q1009";
            var fetched = new DateTime(2026, 11, 1, 0, 20, 0, DateTimeKind.Utc);

            Assert.IsTrue(WeatherCacheFreshness.IsObservationStale(skcg, fetched, fetched));
        }

        [TestMethod]
        public void Ttl_SigueSiendoLaRedDeSeguridad()
        {
            // La observación es la vigente (03:00) pero la entrada tiene 6 minutos: el TTL de 5 min
            // la descarta igual. Se conserva para poder endurecerlo sin tocar la regla nueva.
            var fetched = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);
            var now     = new DateTime(2026, 11, 10, 3, 6, 0, DateTimeKind.Utc);

            Assert.IsFalse(WeatherCacheFreshness.CanUseCached(
                forceRefresh: false, Skrg0300Z, fetched, now, Ttl5Min));
        }
    }

    /// <summary>
    /// **`wind_dir` de NavData puede ser `"VRB"`** (`Helpers/MetarWindDirection`). Medido el
    /// 10/11/2026 en la misma consulta que el resto de este archivo: el servicio devolvió
    /// `"wind_dir":"VRB"` y `NavWeather.WindDir` era `int?`, así que Json.NET lanzaba
    /// `Could not convert string to integer: VRB` y `GetWeatherAsync` devolvía la caché vieja
    /// —el METAR de las 0200Z— sin llegar a guardar el de las 0300Z. El último test deserializa el
    /// JSON real para que un cambio de tipo no vuelva a romperlo en silencio.
    /// </summary>
    [TestClass]
    public class MetarWindDirectionTests
    {
        [TestMethod]
        public void Vrb_NoEsUnaDireccion()
        {
            // Viento variable: sin rumbo fijo. No es 0 —0 es norte y el METAR ya distingue la calma
            // con `00000KT`—.
            Assert.IsNull(MetarWindDirection.Parse("VRB"));
            Assert.IsNull(MetarWindDirection.Parse("vrb"));
        }

        [TestMethod]
        public void GradosComoTexto_SeLeeComoEntero()
        {
            // Otros METAR traen el campo como entero en el JSON; el DTO lo lee como texto y aquí se
            // vuelve a número, con los ceros a la izquierda del formato METAR.
            Assert.AreEqual(180, MetarWindDirection.Parse("180"));
            Assert.AreEqual(10,  MetarWindDirection.Parse("010"));
            Assert.AreEqual(360, MetarWindDirection.Parse("360"));
        }

        [TestMethod]
        public void ValorNoNumerico_NoInventaRumbo()
        {
            Assert.IsNull(MetarWindDirection.Parse(null));
            Assert.IsNull(MetarWindDirection.Parse(""));
            Assert.IsNull(MetarWindDirection.Parse("CALM"));
            Assert.IsNull(MetarWindDirection.Parse("999"));
        }

        [TestMethod]
        public void PayloadRealDeNavData_SeDeserializaEntero()
        {
            // Respuesta literal del servicio (03:36 UTC del 10/11/2026), recortada a los campos que
            // usa el cliente. Antes de v0.9.68 esto lanzaba y el METAR nuevo no se guardaba nunca.
            const string json = @"{
                ""raw_metar"": ""METAR SKRG 100300Z VRB01KT 9999 NSC 14/12 Q1028"",
                ""icao"": ""SKRG"",
                ""observed_at"": ""2026-11-10T03:00:00.000Z"",
                ""wind_dir"": ""VRB"",
                ""wind_speed_kt"": 1,
                ""wind_gust_kt"": null,
                ""visibility_m"": 9656,
                ""ceiling_ft"": null,
                ""temperature_c"": 14,
                ""dewpoint_c"": 12,
                ""qnh_hpa"": 1028,
                ""condition"": ""VMC""
            }";

            var weather = JsonConvert.DeserializeObject<NavWeather>(json);

            Assert.IsNotNull(weather);
            Assert.AreEqual("METAR SKRG 100300Z VRB01KT 9999 NSC 14/12 Q1028", weather.RawMetar);
            Assert.AreEqual("SKRG", weather.Icao);
            Assert.AreEqual("VRB", weather.WindDir);
            Assert.IsNull(MetarWindDirection.Parse(weather.WindDir));
            Assert.AreEqual(1, weather.WindSpeedKt);
            Assert.AreEqual(14, weather.TemperatureC);
            Assert.AreEqual(1028d, weather.QnhHpa);
            Assert.AreEqual("VMC", weather.Condition);

            // Y la hora de observación que lee la regla de caducidad, del propio texto.
            var fetched = new DateTime(2026, 11, 10, 3, 30, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc),
                            MetarObservationTime.Parse(weather.RawMetar, fetched));
        }
    }
}
