using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Muestreo de la ruta para pedir espacios aéreos. Nace del aviso de NavData del 29/09/2026: la
    /// cobertura de `/airspaces/` pasó de 200 nm a **54 nm**, y el cliente pedía sólo origen,
    /// destino y —a veces— el punto medio. En el SKBO→KBOS real de estas pruebas eso dejaba 600 nm
    /// cubiertos de 2.200 y el resto se pintaba como cielo vacío.
    ///
    /// Las coordenadas son aeropuertos reales del piloto, no geometría inventada.
    /// </summary>
    [TestClass]
    public class AirspaceRouteSamplerTests
    {
        // Aeropuertos reales
        private const double SkboLat =   4.7016, SkboLon = -74.1469;   // Bogotá El Dorado
        private const double SkrgLat =   6.1645, SkrgLon = -75.4231;   // Rionegro
        private const double KbosLat =  42.3630, KbosLon = -71.0064;   // Boston Logan

        [TestMethod]
        public void Sample_CoversTheRouteWithoutHoles_WhenTheServerGivesEnoughRadius()
        {
            // Con los 200 nm de antes, el SKBO→KBOS (~2.200 nm) cabe en el tope de puntos y cada
            // salto queda por debajo del radio: sin huecos. Es la propiedad que se perdió al bajar
            // a 54 nm, y la que el muestreo recupera en cuanto NavData devuelva más cobertura.
            var pts = AirspaceRouteSampler.Sample(SkboLat, SkboLon, KbosLat, KbosLon, 200.0);

            Assert.IsTrue(pts.Count <= AirspaceRouteSampler.MaxPoints,
                $"no debe pasar del tope de peticiones (fueron {pts.Count})");

            double maxHop = 0;
            for (int i = 1; i < pts.Count; i++)
                maxHop = Math.Max(maxHop, GeoMath.DistanceNm(
                    pts[i - 1].Lat, pts[i - 1].Lon, pts[i].Lat, pts[i].Lon));

            Assert.IsTrue(maxHop <= 200.0,
                $"con 200 nm de radio ningún salto puede superarlo (el mayor fue {maxHop:F1} nm)");
        }

        [TestMethod]
        public void Sample_WithTheRealRadius_StillSpreadsOverTheWholeRoute()
        {
            // Con el radio **reducido a 54 nm** —el que llegó a declarar el servidor antes de que
            // montara su índice propio— el tope de puntos no alcanza para cubrir 2.200 nm, pero los
            // puntos siguen repartidos de punta a punta: antes eran 3 (extremos y medio), ahora 24.
            var pts = AirspaceRouteSampler.Sample(SkboLat, SkboLon, KbosLat, KbosLon, 54.0);

            Assert.AreEqual(AirspaceRouteSampler.MaxPoints, pts.Count);

            // Reparto uniforme: cada salto mide lo mismo (propiedad del slerp).
            double first = GeoMath.DistanceNm(pts[0].Lat, pts[0].Lon, pts[1].Lat, pts[1].Lon);
            for (int i = 2; i < pts.Count; i++)
            {
                double hop = GeoMath.DistanceNm(pts[i - 1].Lat, pts[i - 1].Lon, pts[i].Lat, pts[i].Lon);
                Assert.AreEqual(first, hop, 0.5,
                    "los puntos van equiespaciados sobre el arco; un salto distinto delata interpolación lineal");
            }

            // Y de extremo a extremo: el primero es el origen y el último el destino.
            Assert.AreEqual(0.0, GeoMath.DistanceNm(SkboLat, SkboLon, pts[0].Lat, pts[0].Lon), 0.05);
            Assert.AreEqual(0.0, GeoMath.DistanceNm(KbosLat, KbosLon, pts[pts.Count - 1].Lat,
                                                    pts[pts.Count - 1].Lon), 0.05);
        }

        [TestMethod]
        public void Sample_ShortHopInsideOneRadius_AsksOnce()
        {
            // Origen y destino más cerca que la cobertura: una sola consulta en el medio cubre los
            // dos extremos. Sin esto se pedía dos veces lo mismo.
            var pts = AirspaceRouteSampler.Sample(SkboLat, SkboLon, SkboLat + 0.2, SkboLon + 0.2, 54.0);
            Assert.AreEqual(1, pts.Count);
        }

        [TestMethod]
        public void Sample_SkboToSkrg_NoHolesAt54Nm()
        {
            // Salto corto real (117 nm): 4 muestras y ningún hueco por debajo de 54 nm.
            var pts = AirspaceRouteSampler.Sample(SkboLat, SkboLon, SkrgLat, SkrgLon, 54.0);

            Assert.AreEqual(4, pts.Count);
            for (int i = 1; i < pts.Count; i++)
            {
                double hop = GeoMath.DistanceNm(pts[i - 1].Lat, pts[i - 1].Lon, pts[i].Lat, pts[i].Lon);
                Assert.IsTrue(hop <= 54.0, $"hueco de {hop:F1} nm entre muestras");
            }
        }

        [TestMethod]
        public void Sample_CappedZone_KeepsCoverageOnALongRoute()
        {
            // El caso que NavData avisó: en zonas densas recorta a 500 espacios y el radio real baja
            // a ~91 nm (medido en Londres). El tope de 24 puntos está puesto por esto — con 16
            // habría huecos de 129 nm entre muestras.
            var pts = AirspaceRouteSampler.Sample(10.4424, -75.5130, 42.3630, -71.0064, 91.0);

            Assert.AreEqual(AirspaceRouteSampler.MaxPoints, pts.Count);

            for (int i = 1; i < pts.Count; i++)
            {
                double hop = GeoMath.DistanceNm(pts[i - 1].Lat, pts[i - 1].Lon, pts[i].Lat, pts[i].Lon);
                Assert.IsTrue(hop <= 91.0,
                    $"con el radio reducido por 'capped' no puede haber huecos (salto de {hop:F1} nm)");
            }
        }

        [TestMethod]
        public void Sample_SkcgToKbos_RealFlightSpacingAndDumpForLiveCheck()
        {
            // El vuelo real del piloto (SKCG→KBOS, 1.931 nm). Con el radio real de 54 nm el tope de
            // 16 puntos deja saltos de ~129 nm: cubre más de lo que cubría (3 puntos mal puestos),
            // pero NO cubre toda la ruta. Por eso se pide más cobertura en el punto 5.3(a) de la
            // respuesta a NavData; con 200 nm el mismo tope no deja ningún hueco.
            var pts = AirspaceRouteSampler.Sample(10.4424, -75.5130, 42.3630, -71.0064,
                                                  AirspaceRouteSampler.DefaultRadiusNm);

            Assert.AreEqual(AirspaceRouteSampler.MaxPoints, pts.Count);

            double hop = GeoMath.DistanceNm(pts[0].Lat, pts[0].Lon, pts[1].Lat, pts[1].Lon);
            Assert.IsTrue(hop > 54.0,
                "con 54 nm de radio y 16 puntos el salto supera la cobertura: la ruta queda a trozos");

            // Volcado para la comprobación manual contra el servicio en vivo (mismo patrón que el
            // volcado del rodaje de RaasReplayTests): se consulta cada punto y se compara la unión
            // de espacios aéreos con la de la estrategia anterior de 3 puntos.
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var p in pts)
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                                "{0:F6},{1:F6}", p.Lat, p.Lon));
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "airspace_samples_skcg_kbos.txt"),
                    sb.ToString());
            }
            catch { /* el volcado es un extra: que no tumbe el test */ }
        }

        [TestMethod]
        public void Sample_IsGreatCircle_NotStraightLineOnTheChart()
        {
            // SKBO→KBOS: el punto medio del arco no es el promedio de lat/lon. Si se interpolara en
            // línea recta, se muestrearían zonas por las que el avión no pasa (y el reparto no
            // sería uniforme, que es lo que comprueba el otro test). Aquí se mide cuánto se separan.
            var mid = AirspaceRouteSampler.Interpolate(SkboLat, SkboLon, KbosLat, KbosLon, 0.5);
            double naiveLat = (SkboLat + KbosLat) / 2.0;
            double naiveLon = (SkboLon + KbosLon) / 2.0;

            double separation = GeoMath.DistanceNm(mid.Lat, mid.Lon, naiveLat, naiveLon);

            // Medido en este par real: 12,9 nm de separación en el punto medio. Parece poco, pero
            // con la cobertura de 54 nm es quasi un cuarto del radio: en el borde, muestrear el
            // promedio en vez del arco cambia qué espacios aéreos entran.
            Assert.IsTrue(separation > 10.0,
                $"el arco se separa del promedio en línea recta (medido: {separation:F1} nm); " +
                "si esto baja, la interpolación dejó de ser esférica");

            // Equidistante de los dos extremos: eso sólo lo cumple el punto sobre el arco.
            double toOrigin = GeoMath.DistanceNm(SkboLat, SkboLon, mid.Lat, mid.Lon);
            double toDest   = GeoMath.DistanceNm(KbosLat, KbosLon, mid.Lat, mid.Lon);
            Assert.AreEqual(toOrigin, toDest, 1.0);
        }

        [TestMethod]
        public void Sample_DegenerateInputs_DoNotBreak()
        {
            // Mismo punto: una muestra, sin división por cero.
            var same = AirspaceRouteSampler.Sample(SkboLat, SkboLon, SkboLat, SkboLon, 54.0);
            Assert.AreEqual(1, same.Count);
            Assert.AreEqual(SkboLat, same[0].Lat, 1e-9);

            // Radio ausente o inválido: se cae al valor por defecto, no a cero (que daría infinito).
            var noRadius = AirspaceRouteSampler.Sample(SkboLat, SkboLon, KbosLat, KbosLon, 0);
            Assert.AreEqual(AirspaceRouteSampler.MaxPoints, noRadius.Count);
            Assert.AreEqual(AirspaceRouteSampler.DefaultRadiusNm, 54.0, 1e-9,
                "el valor por defecto es el MENOR radio conocido: asumir de más deja huecos");

            // Interpolación en los extremos: exacta, sin redondeos de por medio.
            var (lat0, lon0) = AirspaceRouteSampler.Interpolate(SkboLat, SkboLon, KbosLat, KbosLon, 0);
            var (lat1, lon1) = AirspaceRouteSampler.Interpolate(SkboLat, SkboLon, KbosLat, KbosLon, 1);
            Assert.AreEqual(SkboLat, lat0, 1e-9); Assert.AreEqual(SkboLon, lon0, 1e-9);
            Assert.AreEqual(KbosLat, lat1, 1e-9); Assert.AreEqual(KbosLon, lon1, 1e-9);
        }
    }
}
