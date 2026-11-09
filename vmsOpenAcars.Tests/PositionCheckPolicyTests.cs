using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// Contra qué aeropuerto se valida la posición del simulador. El caso real es el PIREP
    /// `nROPdk4GxYY5AOy1` (AT76, **SKBO→SKPE**): `03:04:58 PIREP filed` → `03:05:00 ⚠️ YOU
    /// SHOULD BE AT SKBO (95.4 NM)` → `03:05:03 VALID POSITION SKPE (0.2 NM)` → `03:05:03 Home
    /// base updated: SKPE`. La comprobación miraba la **base** del piloto (SKBO) con el avión ya
    /// aparcado en el **destino** (SKPE) y el vuelo ya fileado.
    /// </summary>
    [TestClass]
    public class PositionCheckPolicyTests
    {
        // Coordenadas reales de los dos campos del vuelo.
        private const double SkboLat = 4.70159, SkboLon = -74.14695;
        private const double SkpeLat = 4.81267, SkpeLon = -75.73952;

        [TestMethod]
        public void DespuesDeFilear_NoHayNadaQueComprobar()
        {
            // 03:04:58 fileado, 03:05:00 el aviso falso. Con el vuelo cerrado la política no
            // devuelve referencia: ni base ni llegada, ninguna.
            var reference = PositionCheckPolicy.Resolve(
                flightClosed: true,
                arrivalAirport: "SKPE", arrivalLat: SkpeLat, arrivalLon: SkpeLon,
                baseAirport:    "SKBO", baseLat:    SkboLat, baseLon:    SkboLon);

            Assert.IsNull(reference, "fileado el vuelo, no se avisa de nada");
        }

        [TestMethod]
        public void YaAterrizado_LaReferenciaEsLaLlegada_YNoLaBase()
        {
            // El avión está en SKPE (0,2 NM de su punto de toque) y la base sigue siendo SKBO a
            // 95,4 NM. La comparación tiene que ser con SKPE, o vuelve el aviso falso.
            var reference = PositionCheckPolicy.Resolve(
                flightClosed: false,
                arrivalAirport: "SKPE", arrivalLat: SkpeLat, arrivalLon: SkpeLon,
                baseAirport:    "SKBO", baseLat:    SkboLat, baseLon:    SkboLon);

            Assert.IsNotNull(reference);
            Assert.AreEqual("SKPE", reference.Airport, "la llegada manda sobre la base");

            double desdeLlegada = UnitConverter.CalculateDistanceNm(
                SkpeLat, SkpeLon, reference.Lat, reference.Lon);
            double desdeBase = UnitConverter.CalculateDistanceNm(
                SkpeLat, SkpeLon, SkboLat, SkboLon);

            Assert.IsTrue(desdeLlegada <= Constants.MaxValidationDistanceNM,
                $"con la llegada la posición es válida ({desdeLlegada:F1} NM)");
            Assert.IsTrue(desdeBase > Constants.MaxValidationDistanceNM,
                $"con la base seguiría avisando ({desdeBase:F1} NM; el log midió 95,4)");
        }

        [TestMethod]
        public void AntesDeAterrizar_LaReferenciaSigueSiendoLaBase()
        {
            // Sin ancla de llegada (todavía no ha tocado), la base es el campo asignado y lo que
            // habilita el START: la comprobación previa no cambia.
            var reference = PositionCheckPolicy.Resolve(
                flightClosed: false,
                arrivalAirport: null, arrivalLat: null, arrivalLon: null,
                baseAirport:    "SKBO", baseLat: SkboLat, baseLon: SkboLon);

            Assert.IsNotNull(reference);
            Assert.AreEqual("SKBO", reference.Airport);
        }

        [TestMethod]
        public void SinCoordenadas_NoSeInventaUnAviso()
        {
            // Degradar sin datos: si no hay ni ancla de llegada ni coordenadas de la base, no hay
            // contra qué comparar y no se avisa.
            Assert.IsNull(PositionCheckPolicy.Resolve(
                flightClosed: false,
                arrivalAirport: "SKPE", arrivalLat: null, arrivalLon: null,
                baseAirport:    "SKBO", baseLat: null, baseLon: null));
        }
    }
}
