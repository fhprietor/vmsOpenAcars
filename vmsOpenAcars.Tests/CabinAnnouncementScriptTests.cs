using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Librería de frases de los anuncios de cabina generados en el cliente.
    ///
    /// Lo que se fija aquí no es la redacción exacta —eso se cambia con el tiempo— sino las cuatro
    /// propiedades que rompen un anuncio hablado: que **exista** para las once fases en los dos
    /// idiomas, que **no queden huecos** cuando falta un dato (los datos son todos opcionales, y el
    /// aeropuerto puede no estar en la caché de NavData o el METAR no responder), que el destino se
    /// diga **por su nombre** y que las cifras se lean como las lee una voz («1 grado», «menos 3
    /// grados», «1 2 3 4», «las catorce treinta y cinco»).
    /// </summary>
    [TestClass]
    public class CabinAnnouncementScriptTests
    {
        private static readonly string[] Languages = { "es", "en" };

        /// <summary>Datos reales de un vuelo a SKBO, con los valores tal como los publica NavData.</summary>
        private static CabinAnnouncementContext Full() => new CabinAnnouncementContext
        {
            AirlineName          = "Vholar Virtual",
            FlightNumber         = "55CH",
            AirportName          = "El Dorado Intl",
            City                 = "Bogota",
            DestinationCode      = "BOG",
            DestinationLongitude = -74.1469,
            LocalTime            = new TimeSpan(14, 35, 0),
            TemperatureC         = 23,
            Condition            = "Cloudy",
        };

        private static CabinAnnouncementContext WeatherOnly() => new CabinAnnouncementContext
        {
            AirportName = "El Dorado Intl",
            City        = "Bogota",
            TemperatureC = 23,
        };

        private static CabinAnnouncementContext Vacio() => new CabinAnnouncementContext();

        private static IEnumerable<string[]> AllTexts()
        {
            var contexts = new[] { Full(), WeatherOnly(), Vacio() };
            foreach (string phase in CabinAnnouncementScript.Phases)
                foreach (string lang in Languages)
                    foreach (var ctx in contexts)
                        yield return new[] { phase, lang, CabinAnnouncementScript.Build(phase, lang, ctx) };
        }

        [TestMethod]
        public void ElCatalogoTieneLasFasesDelCalendarioRealMasLosEventos()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "boarding", "pushback", "taxi_out", "after_takeoff", "cruise",
                    "top_of_descent", "approach", "on_runway", "slides_disarm", "taxi_in", "on_block",
                    "turbulence_on", "turbulence_off"
                },
                CabinAnnouncementScript.Phases,
                "El catálogo es el calendario de una tripulación (siete con clip en NavData más " +
                "puerta cerrada, después del despegue, desarmar toboganes y llegada a puesto) y dos " +
                "eventos que no son momentos del vuelo: la turbulencia y su final.");
        }

        [TestMethod]
        public void LosAnunciosDeTurbulenciaNoAfirmanLoQueNoSeSabe()
        {
            string on = CabinAnnouncementScript.Build("turbulence_on", "es", Vacio());
            StringAssert.Contains(on, "estamos atravesando una zona de turbulencia");
            StringAssert.Contains(on, "abróchense los cinturones");
            Assert.IsFalse(on.Contains("comandante"),
                "Se mide la turbulencia, no se lee el letrero: no se puede decir que el comandante lo encendió");

            string off = CabinAnnouncementScript.Build("turbulence_off", "es", Vacio());
            StringAssert.Contains(off, "hemos salido de la zona de turbulencia");
            StringAssert.Contains(off, "cinturón abrochado",
                "El final no es barra libre: se sigue pidiendo el cinturón");

            StringAssert.Contains(CabinAnnouncementScript.Build("turbulence_on", "en", Vacio()),
                "we are experiencing turbulence");
            StringAssert.Contains(CabinAnnouncementScript.Build("turbulence_off", "en", Vacio()),
                "we have left the area of turbulence");
        }

        [TestMethod]
        public void CadaFaseTieneTextoEnLosDosIdiomas()
        {
            foreach (string phase in CabinAnnouncementScript.Phases)
                foreach (string lang in Languages)
                {
                    string text = CabinAnnouncementScript.Build(phase, lang, Full());
                    Assert.IsFalse(string.IsNullOrWhiteSpace(text),
                        $"La fase '{phase}' no tiene texto en '{lang}'.");
                    Assert.IsTrue(text.EndsWith("."),
                        $"El texto de '{phase}' ({lang}) no cierra la última frase: {text}");
                }
        }

        [TestMethod]
        public void NingunTextoQuedaConHuecosNiPuntuacionHuerfana()
        {
            // Los datos son opcionales por diseño; el defecto clásico es «con destino .» o un
            // doble espacio en mitad de la locución.
            foreach (string[] row in AllTexts())
            {
                string phase = row[0], lang = row[1], text = row[2];
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), $"{phase}/{lang} vacío");

                Assert.IsFalse(text.Contains("  "), $"Doble espacio en {phase}/{lang}: {text}");
                Assert.IsFalse(text.Contains(" ."), $"Espacio antes de punto en {phase}/{lang}: {text}");
                Assert.IsFalse(text.Contains(".."), $"Doble punto en {phase}/{lang}: {text}");
                Assert.IsFalse(text.Contains(",,"), $"Doble coma en {phase}/{lang}: {text}");
                Assert.IsFalse(text.Contains(" ,"), $"Espacio antes de coma en {phase}/{lang}: {text}");
                Assert.IsFalse(text.Contains("{") || text.Contains("}"),
                    $"Marcador sin sustituir en {phase}/{lang}: {text}");
                Assert.IsFalse(text.Contains("null"), $"«null» en el texto de {phase}/{lang}: {text}");
                Assert.IsFalse(text.Contains("  "), $"Doble espacio en {phase}/{lang}: {text}");
            }
        }

        [TestMethod]
        public void SinDatosNoSeHablaDeLoQueNoSeSabe()
        {
            string taxiIn = CabinAnnouncementScript.Build("taxi_in", "es", Vacio());
            StringAssert.DoesNotMatch(taxiIn, new System.Text.RegularExpressions.Regex("(?i)temperatura"),
                "Sin METAR no se puede decir la temperatura: " + taxiIn);
            StringAssert.DoesNotMatch(taxiIn, new System.Text.RegularExpressions.Regex("(?i)hora local"),
                "Sin longitud no se puede decir la hora local: " + taxiIn);

            string boarding = CabinAnnouncementScript.Build("boarding", "es", Vacio());
            StringAssert.DoesNotMatch(boarding, new System.Text.RegularExpressions.Regex("(?i)en nombre de"),
                "Sin aerolínea no se puede hablar en su nombre: " + boarding);
            StringAssert.DoesNotMatch(boarding, new System.Text.RegularExpressions.Regex("(?i)con destino"),
                "Sin destino no se puede decir «con destino»: " + boarding);
            // Pero el anuncio sigue teniendo sentido: la fórmula de bienvenida no depende de los datos.
            StringAssert.Contains(boarding, "bienvenida a bordo");
        }

        // ── El aeropuerto por su nombre (valores reales de la caché de NavData) ───

        [TestMethod]
        public void ElDestinoSeDiceConElNombreDelAeropuertoYLaCiudad()
        {
            // Valores textuales de bin\Release\NavData_cache.sqlite: name abreviado y sin tildes.
            var skbo = new CabinAnnouncementContext { AirportName = "El Dorado Intl", City = "Bogota" };
            Assert.AreEqual("Aeropuerto Internacional El Dorado de Bogota",
                CabinAnnouncementScript.AirportPhrase(skbo, "es"));
            Assert.AreEqual("El Dorado International Airport in Bogota",
                CabinAnnouncementScript.AirportPhrase(skbo, "en"));

            var skcl = new CabinAnnouncementContext { AirportName = "Alfonso Bonilla Aragon Intl", City = "Cali" };
            Assert.AreEqual("Aeropuerto Internacional Alfonso Bonilla Aragon de Cali",
                CabinAnnouncementScript.AirportPhrase(skcl, "es"));

            // Un aeropuerto sin abreviatura no se inventa categoría: «Aeropuerto Palonegro».
            var skbg = new CabinAnnouncementContext { AirportName = "Palonegro", City = "Bucaramanga" };
            Assert.AreEqual("Aeropuerto Palonegro de Bucaramanga",
                CabinAnnouncementScript.AirportPhrase(skbg, "es"));
            Assert.AreEqual("Palonegro Airport in Bucaramanga",
                CabinAnnouncementScript.AirportPhrase(skbg, "en"));

            var skrg = new CabinAnnouncementContext { AirportName = "Jose Maria Cordova", City = "Medellín" };
            Assert.AreEqual("Aeropuerto Jose Maria Cordova de Medellín",
                CabinAnnouncementScript.AirportPhrase(skrg, "es"));
        }

        [TestMethod]
        public void ElNombreDelAeropuertoDegradaSinInventar()
        {
            // Solo nombre: se dice sin ciudad.
            Assert.AreEqual("Aeropuerto Internacional El Dorado",
                CabinAnnouncementScript.AirportPhrase(new CabinAnnouncementContext { AirportName = "El Dorado Intl" }, "es"));

            // Solo ciudad.
            Assert.AreEqual("Aeropuerto de Bogota",
                CabinAnnouncementScript.AirportPhrase(new CabinAnnouncementContext { City = "Bogota" }, "es"));
            Assert.AreEqual("the airport in Bogota",
                CabinAnnouncementScript.AirportPhrase(new CabinAnnouncementContext { City = "Bogota" }, "en"));

            // Solo código.
            Assert.AreEqual("Aeropuerto B O G",
                CabinAnnouncementScript.AirportPhrase(new CabinAnnouncementContext { DestinationCode = "BOG" }, "es"));

            // Nada: la cláusula entera desaparece.
            Assert.IsNull(CabinAnnouncementScript.AirportPhrase(new CabinAnnouncementContext(), "es"));

            // La ciudad no se repite si el nombre ya la lleva.
            Assert.AreEqual("Aeropuerto Internacional Bogota",
                CabinAnnouncementScript.AirportPhrase(
                    new CabinAnnouncementContext { AirportName = "Bogota Intl", City = "Bogota" }, "es"));

            // Sufijo desconocido: el nombre se respeta tal cual.
            Assert.AreEqual("Aeropuerto El Dorado Air Base",
                CabinAnnouncementScript.AirportPhrase(
                    new CabinAnnouncementContext { AirportName = "El Dorado Air Base" }, "es"));

            // Abreviatura con punto.
            Assert.AreEqual("Aeropuerto Internacional El Dorado",
                CabinAnnouncementScript.AirportPhrase(
                    new CabinAnnouncementContext { AirportName = "El Dorado Intl." }, "es"));
        }

        [TestMethod]
        public void LasPlantillasUsanElNombreDelAeropuertoYLlevanElArticuloCorrecto()
        {
            string boarding = CabinAnnouncementScript.Build("boarding", "es", Full());
            StringAssert.Contains(boarding, "con destino al Aeropuerto Internacional El Dorado de Bogota");

            string tod = CabinAnnouncementScript.Build("top_of_descent", "es", Full());
            StringAssert.Contains(tod, "hacia el Aeropuerto Internacional El Dorado de Bogota");

            string taxiIn = CabinAnnouncementScript.Build("taxi_in", "es", Full());
            StringAssert.Contains(taxiIn, "hemos aterrizado en el Aeropuerto Internacional El Dorado de Bogota");

            string cruise = CabinAnnouncementScript.Build("cruise", "es", Full());
            StringAssert.Contains(cruise, "Nuestro destino es el Aeropuerto Internacional El Dorado de Bogota");

            string en = CabinAnnouncementScript.Build("taxi_in", "en", Full());
            StringAssert.Contains(en, "we have landed at El Dorado International Airport in Bogota");
        }

        [TestMethod]
        public void LaHoraLocalSeDiceEnElRodajeDeEntrada()
        {
            // 14:35 con reloj de 12 h y franja del día, como se dice por megafonía.
            string es = CabinAnnouncementScript.Build("taxi_in", "es", Full());
            StringAssert.Contains(es, "Son las dos de la tarde con treinta y cinco minutos, hora local.");

            string en = CabinAnnouncementScript.Build("taxi_in", "en", Full());
            StringAssert.Contains(en, "The local time is two thirty-five in the afternoon.");

            // La hora vive solo donde tiene sentido: la llegada.
            string boarding = CabinAnnouncementScript.Build("boarding", "es", Full());
            Assert.IsFalse(boarding.Contains("hora local"));
        }

        [TestMethod]
        public void LaOrdenDeDesarmarToboganesEsUnaLlamadaALaTripulacion()
        {
            string es = CabinAnnouncementScript.Build("slides_disarm", "es", Full());
            StringAssert.Contains(es, "Tripulación de cabina, desarmen los toboganes y realicen el chequeo cruzado.");

            string en = CabinAnnouncementScript.Build("slides_disarm", "en", Full());
            StringAssert.Contains(en, "Cabin crew, disarm slides and cross-check.");

            // No va dirigida a los pasajeros ni depende de datos del vuelo.
            Assert.IsFalse(es.Contains("Señores pasajeros"));
            Assert.AreEqual(es, CabinAnnouncementScript.Build("slides_disarm", "es", Vacio()),
                "La llamada es fija: no depende de que haya datos");
        }

        [TestMethod]
        public void LosDatosRealesSeDicenEnLosDosIdiomas()
        {
            string es = CabinAnnouncementScript.Build("boarding", "es", Full());
            StringAssert.Contains(es, "Vholar Virtual");
            StringAssert.Contains(es, "El Dorado");
            StringAssert.Contains(es, "bienvenida a bordo");

            string en = CabinAnnouncementScript.Build("boarding", "en", Full());
            StringAssert.Contains(en, "Vholar Virtual");
            StringAssert.Contains(en, "El Dorado");
            StringAssert.Contains(en, "welcome on board");

            // La meteo de llegada va en el descenso y en el rodaje de entrada, con la condición
            // traducida: nunca el texto crudo del METAR.
            string tod = CabinAnnouncementScript.Build("top_of_descent", "es", Full());
            StringAssert.Contains(tod, "23 grados centígrados");
            StringAssert.Contains(tod, "cielo nublado");
            Assert.IsFalse(tod.Contains("Cloudy"), "La condición del METAR no se traduce: " + tod);

            string todEn = CabinAnnouncementScript.Build("top_of_descent", "en", Full());
            StringAssert.Contains(todEn, "23 degrees Celsius");
            StringAssert.Contains(todEn, "cloudy skies");
        }

        [TestMethod]
        public void SinCiudadSeUsaElCodigoDeletreado()
        {
            var ctx = new CabinAnnouncementContext { DestinationCode = "BOG" };
            string text = CabinAnnouncementScript.Build("taxi_in", "es", ctx);
            StringAssert.Contains(text, "B O G",
                "Con el aeropuerto aún sin cachear, el código se deletrea en vez de omitir el destino");
        }

        [TestMethod]
        public void ElNumeroDeVueloSeDeletreaSoloSiEsNumerico()
        {
            Assert.AreEqual("1 2 3 4", CabinAnnouncementScript.SpellFlightNumber("1234"),
                "«mil doscientos treinta y cuatro» no es un número de vuelo");
            Assert.AreEqual("55CH", CabinAnnouncementScript.SpellFlightNumber("55CH"),
                "Una secuencia corta con letras se deja tal cual: las voces la leen bien");
            Assert.AreEqual("", CabinAnnouncementScript.SpellFlightNumber(null));
            Assert.AreEqual("", CabinAnnouncementScript.SpellFlightNumber("   "));
        }

        [TestMethod]
        public void ElCodigoDeAeropuertoSeDeletreaLetraALetra()
        {
            Assert.AreEqual("S K B O", CabinAnnouncementScript.SpellCode("SKBO"));
            Assert.AreEqual("B O G", CabinAnnouncementScript.SpellCode("bog"),
                "Se normaliza a mayúsculas para que la voz lea letras");
            Assert.AreEqual("", CabinAnnouncementScript.SpellCode(null));
        }

        [TestMethod]
        public void LaTemperaturaSeLeeComoSeHabla()
        {
            Assert.AreEqual("cero grados centígrados", CabinAnnouncementScript.SpellTemperature(0, "es"));
            Assert.AreEqual("un grado centígrado", CabinAnnouncementScript.SpellTemperature(1, "es"),
                "Singular, no «1 grados»");
            Assert.AreEqual("menos un grado centígrado", CabinAnnouncementScript.SpellTemperature(-1, "es"));
            Assert.AreEqual("menos 3 grados centígrados", CabinAnnouncementScript.SpellTemperature(-3, "es"),
                "El signo se dice «menos», no se lee el guion");
            Assert.AreEqual("23 grados centígrados", CabinAnnouncementScript.SpellTemperature(23, "es"));

            Assert.AreEqual("zero degrees Celsius", CabinAnnouncementScript.SpellTemperature(0, "en"));
            Assert.AreEqual("one degree Celsius", CabinAnnouncementScript.SpellTemperature(1, "en"));
            Assert.AreEqual("minus 3 degrees Celsius", CabinAnnouncementScript.SpellTemperature(-3, "en"));
        }

        [TestMethod]
        public void LaCondicionDelMetarSeTraduceYLoDesconocidoSeOmite()
        {
            Assert.AreEqual("cielo despejado", CabinAnnouncementScript.DescribeCondition("Clear", "es"));
            Assert.AreEqual("clear skies",     CabinAnnouncementScript.DescribeCondition("Clear", "en"));
            Assert.AreEqual("cielo nublado",   CabinAnnouncementScript.DescribeCondition("Overcast", "es"));
            Assert.AreEqual("lluvia",          CabinAnnouncementScript.DescribeCondition("Light rain", "es"));
            Assert.AreEqual("tormenta",        CabinAnnouncementScript.DescribeCondition("Thunderstorm", "es"));
            Assert.AreEqual("nieve",           CabinAnnouncementScript.DescribeCondition("Snow", "es"));
            Assert.AreEqual("niebla",          CabinAnnouncementScript.DescribeCondition("Mist", "es"));
            Assert.AreEqual("lluvia",          CabinAnnouncementScript.DescribeCondition("Lluvia débil", "es"),
                "El campo es texto libre: también se reconocen los términos en español");

            Assert.IsNull(CabinAnnouncementScript.DescribeCondition("Volcanic ash", "es"),
                "Lo que no se reconoce se omite: mejor no decir el tiempo que decir «con volcanic ash»");
            Assert.IsNull(CabinAnnouncementScript.DescribeCondition("", "es"));
            Assert.IsNull(CabinAnnouncementScript.DescribeCondition(null, "es"));
        }

        [TestMethod]
        public void UnaFaseDesconocidaNoInventaTexto()
        {
            Assert.IsNull(CabinAnnouncementScript.Build("seatbelt", "es", Full()),
                "Una fase sin plantilla devuelve null para que el servicio calle en vez de improvisar");
            Assert.IsNull(CabinAnnouncementScript.Build("", "es", Full()));
            Assert.IsNull(CabinAnnouncementScript.Build(null, "es", Full()));
        }

        [TestMethod]
        public void SoloDosFasesDependenDelTiempo()
        {
            Assert.IsTrue(CabinAnnouncementScript.UsesWeather("top_of_descent"));
            Assert.IsTrue(CabinAnnouncementScript.UsesWeather("taxi_in"));
            foreach (string phase in CabinAnnouncementScript.Phases)
                if (phase != "top_of_descent" && phase != "taxi_in")
                    Assert.IsFalse(CabinAnnouncementScript.UsesWeather(phase),
                        $"'{phase}' no habla del tiempo y no debe esperar al METAR");
        }

        [TestMethod]
        public void ElTextoEsDeterministaYNoSeContaminaEntreLlamadas()
        {
            // La misma entrada da el mismo texto: el anuncio no cambia entre la prueba de Settings y
            // el vuelo, y un contexto vacío no arrastra datos del anterior.
            string first  = CabinAnnouncementScript.Build("cruise", "es", Full());
            string second = CabinAnnouncementScript.Build("cruise", "es", Full());
            Assert.AreEqual(first, second);

            string empty = CabinAnnouncementScript.Build("cruise", "es", Vacio());
            Assert.IsFalse(empty.Contains("El Dorado"));
            Assert.IsFalse(empty.Contains("23"));
            Assert.IsFalse(empty.Contains("hora local"));

            // Un contexto nulo se trata como vacío: el servicio puede llamar antes de que
            // StartFlight haya fijado los datos y el anuncio debe salir igual.
            StringAssert.Contains(CabinAnnouncementScript.Build("cruise", "es", null), "Señores pasajeros");
        }
    }
}
