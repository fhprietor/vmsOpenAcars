using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenACars.Tests
{
    /// <summary>
    /// **El encuadre del closeup del aterrizaje**: qué tramo se ve y dónde caen las marcas.
    ///
    /// Los casos son **aterrizajes reales de la base local** (`F:\FS\vmsOpenAcars\db\landing_log.sqlite`,
    /// 41 vuelos con traza), no geometría inventada — es la regla de la casa. Las longitudes de pista
    /// son las que publica NavData (`length_ft`): **SKCG 01 = 7 841 ft**, **SKBO 14R = 12 467 ft**,
    /// las mismas que fija `TouchdownZonePolicyTests`.
    ///
    /// Lo que se comprueba aquí: que el encuadre sea el pedido, que el toque caiga **donde la base
    /// dice** (no donde quedaría bonito), que las bandas de la zona sean **exactamente** las de
    /// `TouchdownZonePolicy`, y los tres casos de degradación —sin toque, sin longitud de pista y
    /// toque fuera del encuadre—, donde lo que se prohíbe es inventar.
    /// </summary>
    [TestClass]
    public class TouchdownCloseupGeometryTests
    {
        // ── Longitudes reales publicadas por NavData (length_ft) ─────────────────
        private const double Skcg01  = 7841.0;
        private const double Skbo14R = 12467.0;

        // ── El caso real de verificación ─────────────────────────────────────────

        /// <summary>
        /// Vuelo 41 de la base: **SKBG → SKCG, pista 01**, toque a **1 736 ft** del umbral
        /// (`touchdown_dist_ft = 1736,398`), pista de **7 841 ft**.
        ///
        /// Con el muestreo de 2 s, la traza guarda **13 muestras** en el encuadre ancho
        /// (−4 500 … +5 000 ft) y **8** en el de cerca (−2 500 … +2 500): suficiente para que la
        /// curva tenga forma, que es lo que justifica estas dos escalas y no otras.
        /// </summary>
        [TestMethod]
        public void Skcg01_CasoReal_ElToqueCaeDentroYLaPistaSaleDelEncuadre()
        {
            var cu = TouchdownCloseupGeometry.Compute(
                1736.3980938681052, Skcg01,
                TouchdownCloseupGeometry.WideBeforeFt, TouchdownCloseupGeometry.WideAfterFt);

            Assert.AreEqual(5000.0, cu.BeforeFt, 1e-9);
            Assert.AreEqual(4500.0, cu.AfterFt, 1e-9);

            Assert.IsTrue(cu.HasTouchdown);
            Assert.IsTrue(cu.TouchdownInView, "1 736 ft caben en los 4 500 ft vistos tras el umbral");
            Assert.AreEqual(-1736.3980938681052, cu.TouchdownX, 1e-9,
                "la marca va en X negativa: pasada la pista del umbral");
            Assert.AreEqual("TD 1,736 ft from threshold", cu.TouchdownLabel);

            // La pista son 7 841 ft y solo entran los 4 500 del encuadre: la línea sale por la
            // derecha, que es lo correcto (no se recorta ni se dibuja más corta de lo que es).
            Assert.IsTrue(cu.HasRunwayLength);
            Assert.AreEqual(Skcg01, cu.RunwayLengthFt, 1e-9);
            Assert.AreEqual(4500.0, cu.RunwayVisibleFt, 1e-9);
            Assert.IsTrue(cu.RunwaySpillsRight);
            Assert.AreEqual("RWY 7,841 ft", cu.RunwayLabel);

            // Las bandas son las que se puntúan: 15 % de 7 841 = 1 176 < suelo de 1 500, así que
            // manda el suelo; la mitad de pista son 3 920 y manda el techo de 3 000.
            Assert.AreEqual(1500.0, cu.ZeroBandFt, 1e-9);
            Assert.AreEqual(3000.0, cu.ThreeBandFt, 1e-9);
            // Y el toque de 1 736 ft está en la banda de 3 puntos: por encima del suelo de 1 500 y
            // por debajo del techo de 3 000.
            Assert.AreEqual(3, cu.TouchdownPoints);
            Assert.IsTrue(cu.Summary.Contains("3 pts"));
            Assert.IsTrue(cu.Summary.Contains("runway continues past the frame"));
        }

        // ── La pista larga: el 15 % manda sobre el suelo ─────────────────────────

        /// <summary>SKBO 14R (12 467 ft) con la toma real de 2 924 ft (PIREP 4866): el 15 % de una
        /// pista de 3 800 m llega a 1 870 ft, así que son 3 puntos y no 7.</summary>
        [TestMethod]
        public void Skbo14R_CasoReal_LasBandasSonLasDeLaPistaLarga()
        {
            var cu = TouchdownCloseupGeometry.Compute(2924.0, Skbo14R,
                TouchdownCloseupGeometry.WideBeforeFt, TouchdownCloseupGeometry.WideAfterFt);

            Assert.AreEqual(1870.05, cu.ZeroBandFt, 0.01);
            Assert.AreEqual(3000.0, cu.ThreeBandFt, 1e-9);
            Assert.AreEqual(3, cu.TouchdownPoints);
            Assert.IsTrue(cu.TouchdownInView);
            Assert.AreEqual(-2924.0, cu.TouchdownX, 1e-9);
            Assert.AreEqual("TD 2,924 ft from threshold", cu.TouchdownLabel);
        }

        // ── El toque más allá del encuadre ───────────────────────────────────────

        /// <summary>
        /// Vuelo 11 de la base: **SKCL → KJFK, pista 22L**, toque a **5 898 ft** del umbral — la toma
        /// más larga del corpus (la media está en 2 077 ft).
        ///
        /// No cabe en la escala ancha (4 500 ft vistos tras el umbral). La marca **no se pinta**:
        /// llevarla al borde derecho sería colocar el toque donde no está. Lo que sí se conserva es
        /// el dato, en el rótulo, para que el piloto sepa que existe y que use la traza completa.
        /// </summary>
        [TestMethod]
        public void ToqueMasAllaDelEncuadre_NoSeInventaLaMarcaPeroSeConservaElDato()
        {
            var cu = TouchdownCloseupGeometry.Compute(5898.0, 10006.0,
                TouchdownCloseupGeometry.WideBeforeFt, TouchdownCloseupGeometry.WideAfterFt);

            Assert.IsTrue(cu.HasTouchdown, "el dato existe");
            Assert.IsFalse(cu.TouchdownInView, "5 898 ft no caben en 4 500");
            Assert.IsTrue(double.IsNaN(cu.TouchdownX), "sin posición dentro del encuadre");
            Assert.AreEqual("TD 5,898 ft from threshold", cu.TouchdownLabel);
            Assert.IsTrue(cu.Summary.Contains("beyond this view"));
        }

        /// <summary>La escala de cerca esconde más toques — es su naturaleza: el toque medio del
        /// corpus (2 077 ft) sí entra, y uno de 3 095 ft (vuelo 39, SKSP → SKRG 01) no.</summary>
        [TestMethod]
        public void EscalaDeCerca_ElToqueMedioEntraPeroUnoDe3095FtNo()
        {
            var medio = TouchdownCloseupGeometry.Compute(2077.0, Skcg01,
                TouchdownCloseupGeometry.CloseBeforeFt, TouchdownCloseupGeometry.CloseAfterFt);
            Assert.IsTrue(medio.TouchdownInView);
            Assert.AreEqual(3, medio.TouchdownPoints, "1 500 < 2 077 ≤ 3 000");

            var largo = TouchdownCloseupGeometry.Compute(3095.0, Skcg01,
                TouchdownCloseupGeometry.CloseBeforeFt, TouchdownCloseupGeometry.CloseAfterFt);
            Assert.IsFalse(largo.TouchdownInView);
        }

        // ── Sin distancia de toma ────────────────────────────────────────────────

        /// <summary>
        /// Vuelos 38 y 40 de la base: `touchdown_dist_ft = 0` (no se capturó la toma). El closeup
        /// sigue sirviendo —encuadre, pista y bandas— pero **no hay marca ni puntos**: 0 no es un
        /// toque en el umbral, es un hueco.
        /// </summary>
        [TestMethod]
        public void SinDistanciaDeToma_NoHayMarcaPeroSiEncuadre()
        {
            var cu = TouchdownCloseupGeometry.Compute(0.0, Skcg01,
                TouchdownCloseupGeometry.WideBeforeFt, TouchdownCloseupGeometry.WideAfterFt);

            Assert.IsFalse(cu.HasTouchdown);
            Assert.IsFalse(cu.TouchdownInView);
            Assert.IsTrue(double.IsNaN(cu.TouchdownX));
            Assert.AreEqual("", cu.TouchdownLabel);
            Assert.AreEqual(0, cu.TouchdownPoints);
            Assert.IsTrue(cu.Summary.Contains("TD —"), cu.Summary);

            // Lo que no depende del toque sigue en pie.
            Assert.AreEqual(1500.0, cu.ZeroBandFt, 1e-9);
            Assert.AreEqual(3000.0, cu.ThreeBandFt, 1e-9);
            Assert.IsTrue(cu.RunwayVisible);

            // Y tampoco un valor negativo —una traza corrupta— se cuela como toque.
            var corrupta = TouchdownCloseupGeometry.Compute(-37451.0, Skcg01, 5000.0, 4500.0);
            Assert.IsFalse(corrupta.HasTouchdown);
        }

        // ── Sin longitud de pista ────────────────────────────────────────────────

        /// <summary>
        /// Los vuelos guardados antes de que `flights` tuviera la columna (y cualquier vuelo en el
        /// que NavData no resolviera la pista) llegan con longitud **0**. Entonces **no se dibuja
        /// línea de pista** —no se inventa un largo— pero el umbral, el toque y las bandas sí
        /// funcionan, con los umbrales de la casa (1 500 / 2 500) que son los que se puntúan.
        /// </summary>
        [TestMethod]
        public void SinLongitudDePista_NoHayLineaDePistaPeroSiUmbralYToque()
        {
            var cu = TouchdownCloseupGeometry.Compute(1736.0, 0.0,
                TouchdownCloseupGeometry.WideBeforeFt, TouchdownCloseupGeometry.WideAfterFt);

            Assert.IsFalse(cu.HasRunwayLength);
            Assert.IsFalse(cu.RunwayVisible, "sin dato no hay línea de pista");
            Assert.AreEqual(0.0, cu.RunwayVisibleFt, 1e-9);
            Assert.IsFalse(cu.RunwaySpillsRight);
            Assert.AreEqual("", cu.RunwayLabel);
            Assert.IsTrue(cu.Summary.StartsWith("RWY —"), cu.Summary);

            // El toque sigue marcándose, y las bandas caen a la regla de siempre.
            Assert.IsTrue(cu.TouchdownInView);
            Assert.AreEqual(-1736.0, cu.TouchdownX, 1e-9);
            Assert.AreEqual(1500.0, cu.ZeroBandFt, 1e-9);
            Assert.AreEqual(2500.0, cu.ThreeBandFt, 1e-9);
            Assert.AreEqual(3, cu.TouchdownPoints, "1 500 < 1 736 ≤ 2 500");

            // Una longitud no utilizable (negativa) es lo mismo que no tenerla.
            var negativa = TouchdownCloseupGeometry.Compute(1736.0, -1.0, 5000.0, 4500.0);
            Assert.IsFalse(negativa.RunwayVisible);
        }

        // ── Las bandas no pueden desincronizarse del criterio ────────────────────

        [TestMethod]
        public void LasBandasSonExactamenteLasQueSePuntuan()
        {
            foreach (double length in new[] { 0.0, 2557.0, 5577.0, Skcg01, 11004.0, Skbo14R, 13711.0 })
            {
                var cu = TouchdownCloseupGeometry.Compute(1200.0, length, 5000.0, 4500.0);
                Assert.AreEqual(TouchdownZonePolicy.ZeroBandFt(length),  cu.ZeroBandFt,  1e-9,
                                $"banda de 0 puntos desincronizada con la pista de {length} ft");
                Assert.AreEqual(TouchdownZonePolicy.ThreeBandFt(length), cu.ThreeBandFt, 1e-9,
                                $"banda de 3 puntos desincronizada con la pista de {length} ft");
                Assert.AreEqual(TouchdownZonePolicy.PointsFor(1200.0, length), cu.TouchdownPoints);
            }
        }

        // ── El encuadre vacío o absurdo no revienta ──────────────────────────────

        [TestMethod]
        public void UnEncuadreNoUtilizable_SeQuedaEnCeroYNoLanzaNada()
        {
            var cu = TouchdownCloseupGeometry.Compute(1736.0, Skcg01, 0.0, -5.0);

            Assert.AreEqual(0.0, cu.BeforeFt, 1e-9);
            Assert.AreEqual(0.0, cu.AfterFt, 1e-9);
            Assert.IsFalse(cu.TouchdownInView, "sin encuadre pasado el umbral no cabe ningún toque");
            Assert.IsTrue(double.IsNaN(cu.TouchdownX));
            Assert.IsFalse(cu.RunwayVisible, "una pista sin encuadre donde entrar no se pinta");
        }
    }
}
