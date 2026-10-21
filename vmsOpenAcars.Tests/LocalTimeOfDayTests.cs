using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Hora local del destino a partir de la longitud.
    ///
    /// No hay zona horaria en ningún dato que tengamos —NavData publica `lat`/`lon`, no una base de
    /// tiempos, y el METAR tampoco la lleva—, así que el huso se deduce de la longitud: 15° por
    /// hora. El mantenedor aceptó el error («no importa si no es fiable»), pero el error se mide
    /// aquí con **coordenadas reales de la caché** para que se sepa de qué tamaño es: acierta en
    /// los países sin horario de verano (Colombia, Brasil) y se queda una hora corta en los que lo
    /// tienen (España en verano).
    /// </summary>
    [TestClass]
    public class LocalTimeOfDayTests
    {
        // Longitudes reales de bin\Release\NavData_cache.sqlite (JSON `Info` de cada aeropuerto).
        private const double SkboLon = -74.14694213867188;   // Bogotá
        private const double SkrgLon = -75.4229965209961;    // Medellín/Rionegro
        private const double SkclLon = -76.3816909790039;    // Cali

        [TestMethod]
        public void ElHusoSeDeduceDeLaLongitud()
        {
            Assert.AreEqual(-5, LocalTimeOfDay.OffsetHours(SkboLon), "Bogotá es UTC−5 todo el año");
            Assert.AreEqual(-5, LocalTimeOfDay.OffsetHours(SkrgLon));
            Assert.AreEqual(-5, LocalTimeOfDay.OffsetHours(SkclLon));

            Assert.AreEqual(-3, LocalTimeOfDay.OffsetHours(-46.47));   // SBGR Guarulhos
            Assert.AreEqual(-8, LocalTimeOfDay.OffsetHours(-118.41));  // KLAX
            Assert.AreEqual(-5, LocalTimeOfDay.OffsetHours(-73.78));   // KJFK
            Assert.AreEqual(0,  LocalTimeOfDay.OffsetHours(-3.59));    // LEMD: en invierno acierta
            Assert.AreEqual(0,  LocalTimeOfDay.OffsetHours(-0.46));    // EGLL
            Assert.AreEqual(10, LocalTimeOfDay.OffsetHours(151.18));   // YSSY
            Assert.AreEqual(12, LocalTimeOfDay.OffsetHours(174.79));   // NZAA
        }

        [TestMethod]
        public void LosExtremosYLosDatosImposiblesNoRompenNada()
        {
            Assert.AreEqual(12,  LocalTimeOfDay.OffsetHours(180));
            Assert.AreEqual(-12, LocalTimeOfDay.OffsetHours(-180));
            Assert.AreEqual(0,   LocalTimeOfDay.OffsetHours(double.NaN));
            Assert.AreEqual(0,   LocalTimeOfDay.OffsetHours(double.PositiveInfinity));
            // Una longitud imposible se acota a ±180° antes de dividir, así que sale ±12 h: es lo
            // más lejos que se puede estar del meridiano, y nunca se devuelve un huso inventado.
            Assert.AreEqual(12,  LocalTimeOfDay.OffsetHours(200));
            Assert.AreEqual(-12, LocalTimeOfDay.OffsetHours(-200));
        }

        [TestMethod]
        public void LaHoraLocalSeCalculaDesdeElRelojUtc()
        {
            // 07:00Z en Bogotá son las 02:00 locales.
            var utc = new DateTime(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(new TimeSpan(2, 0, 0), LocalTimeOfDay.At(utc, SkboLon));

            // Y al revés: 02:00Z son las 21:00 del día anterior, que como hora del día es 21:00.
            var early = new DateTime(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(new TimeSpan(21, 0, 0), LocalTimeOfDay.At(early, SkboLon));
        }

        [TestMethod]
        public void LaHoraSeDiceComoLaDiriaLaTripulacion()
        {
            // Los dos ejemplos que pidió el mantenedor, literales.
            Assert.AreEqual("Son las diez de la mañana con cinco minutos",
                LocalTimeOfDay.Spell(new TimeSpan(10, 5, 0), "es"));
            Assert.AreEqual("Son las dos de la tarde con veinticinco minutos",
                LocalTimeOfDay.Spell(new TimeSpan(14, 25, 0), "es"));

            // Sin minutos se dice «en punto», no «con cero minutos».
            Assert.AreEqual("Son las dos de la tarde en punto",
                LocalTimeOfDay.Spell(new TimeSpan(14, 0, 0), "es"));

            // Concordancia: «Es la una», no «Son la una». Y la una de la tarde es 13:xx.
            Assert.AreEqual("Es la una de la tarde con cinco minutos",
                LocalTimeOfDay.Spell(new TimeSpan(13, 5, 0), "es"));
            Assert.AreEqual("Es la una de la madrugada en punto",
                LocalTimeOfDay.Spell(new TimeSpan(1, 0, 0), "es"));

            // Un minuto va en singular.
            Assert.AreEqual("Son las doce de la madrugada con un minuto",
                LocalTimeOfDay.Spell(new TimeSpan(0, 1, 0), "es"));
        }

        [TestMethod]
        public void LasFranjasDelDiaSonLasQuePidioElMantenedor()
        {
            // Antes de las 05:00 es madrugada; de 05:00 a 11:59, mañana.
            Assert.AreEqual("Son las cuatro de la madrugada con cincuenta y nueve minutos",
                LocalTimeOfDay.Spell(new TimeSpan(4, 59, 0), "es"));
            Assert.AreEqual("Son las cinco de la mañana en punto",
                LocalTimeOfDay.Spell(new TimeSpan(5, 0, 0), "es"));
            Assert.AreEqual("Son las once de la mañana con cincuenta y nueve minutos",
                LocalTimeOfDay.Spell(new TimeSpan(11, 59, 0), "es"));

            // Las 12 son mediodía; de 13:00 a 19:59, tarde.
            Assert.AreEqual("Son las doce del mediodía en punto",
                LocalTimeOfDay.Spell(new TimeSpan(12, 0, 0), "es"));
            Assert.AreEqual("Son las siete de la tarde con cincuenta y nueve minutos",
                LocalTimeOfDay.Spell(new TimeSpan(19, 59, 0), "es"));

            // De 20:00 en adelante es noche, y las 12 del reloj de 12 h son las 00:xx.
            Assert.AreEqual("Son las ocho de la noche en punto",
                LocalTimeOfDay.Spell(new TimeSpan(20, 0, 0), "es"));
            Assert.AreEqual("Son las once de la noche con cincuenta y nueve minutos",
                LocalTimeOfDay.Spell(new TimeSpan(23, 59, 0), "es"));
            Assert.AreEqual("Son las doce de la madrugada en punto",
                LocalTimeOfDay.Spell(new TimeSpan(0, 0, 0), "es"));
            Assert.AreEqual("Son las doce del mediodía con treinta minutos",
                LocalTimeOfDay.Spell(new TimeSpan(12, 30, 0), "es"));
        }

        [TestMethod]
        public void LaHoraEnInglesSigueSuPropiaConvencion()
        {
            Assert.AreEqual("The local time is ten oh-five in the morning",
                LocalTimeOfDay.Spell(new TimeSpan(10, 5, 0), "en"));
            Assert.AreEqual("The local time is two twenty-five in the afternoon",
                LocalTimeOfDay.Spell(new TimeSpan(14, 25, 0), "en"));
            Assert.AreEqual("The local time is two in the afternoon",
                LocalTimeOfDay.Spell(new TimeSpan(14, 0, 0), "en"));
            Assert.AreEqual("The local time is twelve at noon",
                LocalTimeOfDay.Spell(new TimeSpan(12, 0, 0), "en"));
            Assert.AreEqual("The local time is twelve fifteen at night",
                LocalTimeOfDay.Spell(new TimeSpan(0, 15, 0), "en"));
            Assert.AreEqual("The local time is eight thirty in the evening",
                LocalTimeOfDay.Spell(new TimeSpan(20, 30, 0), "en"));
        }

        [TestMethod]
        public void LosNumerosSeDicenHastaCincuentaYNueve()
        {
            Assert.AreEqual("veinte", LocalTimeOfDay.Number(20, true));
            Assert.AreEqual("veintiuno", LocalTimeOfDay.Number(21, true));
            Assert.AreEqual("veintidós", LocalTimeOfDay.Number(22, true));
            Assert.AreEqual("veintiséis", LocalTimeOfDay.Number(26, true));
            Assert.AreEqual("treinta y uno", LocalTimeOfDay.Number(31, true));
            Assert.AreEqual("cuarenta y cuatro", LocalTimeOfDay.Number(44, true));
            Assert.AreEqual("cincuenta y nueve", LocalTimeOfDay.Number(59, true));

            Assert.AreEqual("twenty", LocalTimeOfDay.Number(20, false));
            Assert.AreEqual("twenty-one", LocalTimeOfDay.Number(21, false));
            Assert.AreEqual("forty-four", LocalTimeOfDay.Number(44, false));
            Assert.AreEqual("fifty-nine", LocalTimeOfDay.Number(59, false));

            // Fuera de rango no se inventa una palabra: el número se lee tal cual.
            Assert.AreEqual("60", LocalTimeOfDay.Number(60, true));
            Assert.AreEqual("-1", LocalTimeOfDay.Number(-1, false));
        }

        [TestMethod]
        public void ElAnuncioDeLlegadaLlevaLaHoraQueDaLaLongitudReal()
        {
            // Cadena completa con los datos del vuelo: 07:00Z en SKBO son las 02:00 locales.
            var ctx = new CabinAnnouncementContext
            {
                AirportName          = "El Dorado Intl",
                City                 = "Bogota",
                DestinationLongitude = SkboLon,
                LocalTime            = LocalTimeOfDay.At(new DateTime(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc), SkboLon),
            };

            string text = CabinAnnouncementScript.Build("taxi_in", "es", ctx);
            StringAssert.Contains(text, "Son las dos de la madrugada en punto, hora local.");
        }
    }
}
