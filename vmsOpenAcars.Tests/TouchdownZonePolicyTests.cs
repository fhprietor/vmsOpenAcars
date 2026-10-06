using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **Criterio «Touchdown Zone»**: los mismos tres tramos (0/3/7) con los **umbrales que pone la
    /// pista**. El porqué y las cifras están en la cabecera de <see cref="TouchdownZonePolicy"/>.
    ///
    /// Las longitudes de pista de estos tests **son las que publica NavData en vivo** (consultadas
    /// el 03/10/2026 en `/airport/{icao}/runways/`, campo `length_ft`), no geometría inventada:
    /// SKCG 01 = 7 841 ft · SKBO 14L/14R = 12 467 ft (3 800 m) · MDSD 17 = 11 004 ft ·
    /// MDSD 18 = 9 813 ft · LEMD 18R = 13 711 ft · KBOS 04R = 10 006 ft · KBOS 15L = 2 557 ft.
    /// (NavData publica 13 711 ft para la 18R de LEMD, no los 4 350 m = 14 272 ft del enunciado:
    /// se prueba con **el dato que sirve el servicio**, que es el que va a llegar en vuelo.)
    ///
    /// Cada frontera se comprueba **en sus dos lados**, que es donde un `&lt;` por `&lt;=` se
    /// colaría, y el **caso sin dato de longitud** se fija aparte contra la regla de siempre.
    /// </summary>
    [TestClass]
    public class TouchdownZonePolicyTests
    {
        // Longitudes reales publicadas por NavData (length_ft), 03/10/2026.
        private const double Skcg01    = 7841.0;
        private const double Skbo14L   = 12467.0;   // 3 800 m
        private const double Mdsd17    = 11004.0;
        private const double Mdsd18    = 9813.0;
        private const double Lemd18R   = 13711.0;
        private const double Kbos04R   = 10006.0;
        private const double Kbos15L   = 2557.0;    // la pista corta
        private const double Sksm01    = 5577.0;    // la más corta del corpus local

        // ── Sin dato de longitud: la regla de la casa, exactamente ───────────────

        [DataTestMethod]
        [DataRow(1500.0, 0, "1 500 ft sigue siendo el final de la banda de 0 puntos")]
        [DataRow(1500.1, 3, "un pie más allá de 1 500 ya son 3 puntos")]
        [DataRow(2500.0, 3, "2 500 ft sigue siendo el final de la banda de 3 puntos")]
        [DataRow(2500.1, 7, "un pie más allá de 2 500 son 7 puntos")]
        [DataRow(6000.0, 7, "tope")]
        public void SinDatoDeLongitud_EsExactamenteLaReglaDeHoy(double distFt, int expected, string because)
        {
            // El caso que manda la regla de la casa: **degradar sin datos**. Y no es una regla
            // aproximada, es la de hoy: si esto cambia, el vuelo que vuela sin NavData cambia de
            // puntuación sin que nadie haya pedido cambiarla.
            Assert.AreEqual(expected, TouchdownZonePolicy.PointsFor(distFt, 0.0), because);
            Assert.AreEqual(expected, TouchdownZonePolicy.PointsFor(distFt, -1.0),
                            because + " (longitud no utilizable = sin dato)");
        }

        [TestMethod]
        public void SinDatoDeLongitud_LasBandasSonLasDeSiempre()
        {
            Assert.AreEqual(1500.0, TouchdownZonePolicy.ZeroBandFt(0.0), 1e-9);
            Assert.AreEqual(2500.0, TouchdownZonePolicy.ThreeBandFt(0.0), 1e-9);
        }

        // ── SKBO 14L (3 800 m = 12 467 ft): 15 % = 1 870 ft manda sobre el suelo ──

        [DataTestMethod]
        [DataRow(1870.0, 0, "15 % de 12 467 ft = 1 870,05: 1 870 ft sigue siendo 0 puntos")]
        [DataRow(1871.0, 3, "por encima de la banda de 15 % ya son 3 puntos")]
        [DataRow(3000.0, 3, "3 000 ft es el final de la banda de 3 puntos (mitad de pista = 6 233)")]
        [DataRow(3001.0, 7, "por encima de 3 000 ft, siempre 7")]
        [DataRow(3225.0, 7, "la toma real del PIREP 8422 (SKBO 14R, 20/05/2026)")]
        public void Skbo14L_LaBandaLaFijaEl15PorCiento(double distFt, int expected, string because)
        {
            Assert.AreEqual(1870.05, TouchdownZonePolicy.ZeroBandFt(Skbo14L), 0.01);
            Assert.AreEqual(expected, TouchdownZonePolicy.PointsFor(distFt, Skbo14L), because);
        }

        [TestMethod]
        public void Skbo14L_ElCasoRealQueDejaDeEstarCastigado()
        {
            // 2 924 ft de toma (PIREP 4866, SKBO 14R, 25/09/2026): con la regla de hoy son 7 puntos
            // —pasaba de 2 500— y con la nueva son 3, porque la banda de 15 % de una pista de
            // 3 800 m llega a 1 870 ft. Es el cambio que pidió la aerolínea.
            Assert.AreEqual(7, TouchdownZonePolicy.PointsFor(2924.0, 0.0), "regla de hoy sin dato");
            Assert.AreEqual(3, TouchdownZonePolicy.PointsFor(2924.0, Skbo14L), "regla nueva con la pista");
        }

        // ── Pistas donde manda el suelo de 1 500 ft (15 % < 1 500) ───────────────

        [DataTestMethod]
        [DataRow(1500.0, 0, "el suelo de 1 500 ft es el que manda: 15 % de 7 841 son 1 176")]
        [DataRow(1501.0, 3, "justo por encima del suelo")]
        [DataRow(3000.0, 3, "mitad de pista = 3 920, así que la banda de 3 llega al techo de 3 000")]
        [DataRow(3001.0, 7, "por encima del techo, 7")]
        public void Skcg01_ElSueloDe1500FtManda(double distFt, int expected, string because)
            => Assert.AreEqual(expected, TouchdownZonePolicy.PointsFor(distFt, Skcg01), because);

        [DataTestMethod]
        [DataRow(Mdsd18, 1500.0, "15 % de 9 813 son 1 471,95: manda el suelo")]
        [DataRow(Kbos04R, 1500.9, "15 % de 10 006 son 1 500,9: manda el porcentaje por poco")]
        [DataRow(Mdsd17, 1650.6, "15 % de 11 004")]
        [DataRow(Sksm01, 1500.0, "15 % de 5 577 son 836,55: manda el suelo")]
        public void ElUmbralDeCeroPuntos_EsMaximoDeSueloYPorcentaje(double lengthFt, double expected, string because)
            => Assert.AreEqual(expected, TouchdownZonePolicy.ZeroBandFt(lengthFt), 0.11, because);

        // ── LEMD 18R: la pista más larga del corpus ──────────────────────────────

        [DataTestMethod]
        [DataRow(2056.0, 0, "15 % de 13 711 son 2 056,65")]
        [DataRow(2057.0, 3, "por encima")]
        [DataRow(3000.0, 3, "el techo de la TDZ")]
        [DataRow(3200.0, 7, "una pista larga no convierte en gratis una toma larga: 3 200 ft son 7")]
        public void Lemd18R_LaPistaLargaNoRegalaElTramoLargo(double distFt, int expected, string because)
            => Assert.AreEqual(expected, TouchdownZonePolicy.PointsFor(distFt, Lemd18R), because);

        // ── El techo de 3 000 ft no se negocia ───────────────────────────────────

        [TestMethod]
        public void PistaLarguisima_ElTechoDe3000FtCortaElPorcentaje()
        {
            // 15 % de 24 000 ft serían 3 600 ft "gratis": el techo los deja en 3 000. **Ninguna pista
            // del corpus llega ahí** —hacen falta más de 20 000 ft para que el techo del 15 % muerda,
            // y la más larga que se ha medido es la 18R de LEMD con 13 711—, así que esta longitud es
            // sintética y se dice: lo que se fija es la regla, no un caso real.
            Assert.AreEqual(3000.0, TouchdownZonePolicy.ZeroBandFt(24000.0), 1e-9,
                            "el techo de 3 000 ft recorta el 15 % de una pista de 24 000 ft");
            Assert.AreEqual(3000.0, TouchdownZonePolicy.ThreeBandFt(24000.0), 1e-9);
        }

        [DataTestMethod]
        [DataRow(3000.0, 0, "3 000 ft sigue dentro de la banda, que el techo deja justo ahí")]
        [DataRow(3001.0, 7, "el techo tapa también la banda de 3: por encima de 3 000 ft, 7")]
        [DataRow(3200.0, 7, "**el caso del techo**: en una pista larguísima, 3 200 ft siguen siendo 7")]
        [DataRow(12000.0, 7, "tope")]
        public void PistaLarguisima_TocarA3200FtSigueSiendo7Puntos(double distFt, int expected, string because)
            => Assert.AreEqual(expected, TouchdownZonePolicy.PointsFor(distFt, 24000.0), because);

        // ── Pista corta: el tramo de 3 puntos puede quedar vacío ─────────────────

        [TestMethod]
        public void PistaCorta_ElSueloDeCeroPuntosGanaYElTramoDe3QuedaVacio()
        {
            // KBOS 15L son 2 557 ft: la mitad son 1 278,5 ft, **por debajo** del suelo de 1 500 de la
            // banda de 0 puntos. Como el tramo de 0 se evalúa primero, ≤1 500 sigue siendo 0 y de ahí
            // para arriba es 7 —no hay tramo de 3—. Es el caso límite declarado en la cabecera del
            // helper; ninguna pista del corpus baja de 5 577 ft, así que se fija aquí a propósito.
            Assert.AreEqual(1278.5, TouchdownZonePolicy.ThreeBandFt(Kbos15L), 1e-9);
            Assert.AreEqual(1500.0, TouchdownZonePolicy.ZeroBandFt(Kbos15L), 1e-9);
            Assert.AreEqual(0, TouchdownZonePolicy.PointsFor(1500.0, Kbos15L));
            Assert.AreEqual(7, TouchdownZonePolicy.PointsFor(1501.0, Kbos15L));
        }

        // ── Propiedades que deben valer para cualquier pista ─────────────────────

        [TestMethod]
        public void CualquierPista_ElTramoDeCeroPuntosNuncaEsMasEstrechoQueHoy()
        {
            // El suelo de 1 500 ft es una protección: la banda de 0 puntos no puede encogerse por
            // haber mirado la pista. Y el techo tapa el otro lado.
            for (double length = 1000.0; length <= 30000.0; length += 250.0)
            {
                double band = TouchdownZonePolicy.ZeroBandFt(length);
                Assert.IsTrue(band >= 1500.0, $"la banda de 0 pts no puede bajar de 1 500 ft ({length} ft)");
                Assert.IsTrue(band <= 3000.0, $"la banda de 0 pts no puede pasar de 3 000 ft ({length} ft)");
            }
        }

        [TestMethod]
        public void CualquierPista_MasLargoNuncaPuntuaPeor()
        {
            // Dos propiedades que deben valer para toda pista y toda distancia:
            //  (a) para una misma pista, tocar más lejos **nunca** puntúa mejor;
            //  (b) para una misma distancia, una pista más larga **nunca** puntúa peor (la regla es
            //      más indulgente con la pista larga, nunca al revés).
            double[] lengths = { 2557.0, 5577.0, 7841.0, 9813.0, 11004.0, 12467.0, 13711.0, 20000.0 };

            foreach (double length in lengths)
            {
                for (double dist = 100.0; dist <= 4000.0; dist += 100.0)
                {
                    int here = TouchdownZonePolicy.PointsFor(dist, length);
                    Assert.IsTrue(TouchdownZonePolicy.PointsFor(dist + 100.0, length) >= here,
                        $"más distancia no puede puntuar mejor ({dist} vs {dist + 100} ft en {length} ft)");
                }
            }

            for (int i = 1; i < lengths.Length; i++)
            {
                for (double dist = 100.0; dist <= 4000.0; dist += 100.0)
                {
                    int shorter = TouchdownZonePolicy.PointsFor(dist, lengths[i - 1]);
                    int longer  = TouchdownZonePolicy.PointsFor(dist, lengths[i]);
                    Assert.IsTrue(longer <= shorter,
                        $"una pista más larga no puede puntuar peor ({lengths[i - 1]} vs {lengths[i]} ft a {dist} ft)");
                }
            }
        }
    }
}
