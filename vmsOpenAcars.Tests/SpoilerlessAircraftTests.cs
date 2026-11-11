using Microsoft.VisualStudio.TestTools.UnitTesting;
using vmsOpenAcars.Helpers;

namespace vmsOpenAcars.Tests
{
    /// <summary>
    /// **El ATR no tiene spoilers**, y su addon no deja el indicador en cero: lo publica oscilando
    /// alrededor del 10 % que `SpoilersPosition` exige para decir «desplegado». En el PIREP real
    /// `D0Wy9Pa7GNB5pYEo` (AT76, MSFS 2024, SKPE→SKMD) eso escribió **seis pares
    /// `DEPLOYED`/`RETRACTED`** en el descenso, entre 14 500 y 8 300 ft y con el avión limpio:
    ///
    /// ```
    /// 01:27:23 → 01:27:39   01:27:53 → 01:28:13   01:29:11 → 01:30:23
    /// 01:31:18 → 01:31:28   01:31:37 → 01:32:09   01:32:28 → 01:32:36
    /// ```
    ///
    /// y en el propio toque el bloque de datos decía `Spoilers: 0%`.
    ///
    /// La regla es una **lista de tipos**, como la del DH8D en `BeaconStrobeSharedAircraft`, y
    /// **degrada**: un tipo que no esté en la lista se comporta exactamente como hasta ahora.
    /// </summary>
    [TestClass]
    public class SpoilerlessAircraftTests
    {
        [TestMethod]
        public void ElAtrNoTieneSpoilers()
        {
            // El del PIREP, y la familia entera: ATR 42 (AT43/AT45/AT46) y ATR 72 (AT72/AT75/AT76).
            foreach (string type in new[] { "AT76", "at76", " AT76 ", "AT72", "AT75", "AT43", "AT45", "AT46" })
                Assert.IsTrue(SpoilerlessAircraft.HasNoSpoilers(type), $"{type} es un ATR");
        }

        [TestMethod]
        public void CualquierOtroAvionSeComportaComoHoy()
        {
            foreach (string type in new[] { "B738", "B77L", "A320", "DH8D", "C172", "E195" })
                Assert.IsFalse(SpoilerlessAircraft.HasNoSpoilers(type),
                    $"{type} no está en la lista: no se le toca nada");
        }

        [TestMethod]
        public void SinTipoNoSeDecideNada()
        {
            // Degradar, no adivinar: sin designador se sigue leyendo el offset como siempre.
            Assert.IsFalse(SpoilerlessAircraft.HasNoSpoilers(null));
            Assert.IsFalse(SpoilerlessAircraft.HasNoSpoilers(""));
            Assert.IsFalse(SpoilerlessAircraft.HasNoSpoilers("????"));
            Assert.IsFalse(SpoilerlessAircraft.HasNoSpoilers("ATR"),
                "«ATR» es el fabricante, no un designador de tipo");
        }

        [TestMethod]
        public void LaPosicionDelAtrSeFuerzaACero()
        {
            // El 42 % de recorrido es el tipo de lectura que producía los pares DEPLOYED/RETRACTED.
            Assert.AreEqual(0.0, SpoilerlessAircraft.EffectiveFraction(0.42, "AT76"), 1e-9);
            Assert.IsFalse(SpoilersPosition.IsDeployed(
                SpoilerlessAircraft.EffectiveFraction(0.42, "AT76")));

            // …y en un avión que sí los tiene, la lectura se respeta tal cual.
            Assert.AreEqual(0.42, SpoilerlessAircraft.EffectiveFraction(0.42, "B738"), 1e-9);
            Assert.AreEqual(1.0, SpoilerlessAircraft.EffectiveFraction(1.0, "B738"), 1e-9);
        }

        [TestMethod]
        public void LaOscilacionDelAtrYaNoCruzaElUmbral()
        {
            // Los valores crudos que el indicador fantasma dejaba alrededor del 10 % (1639 de 16383):
            // 1 200 es el 7,3 % y 1 900 el 11,6 %, así que sin la lista el estado bailaba.
            foreach (int raw in new[] { 0, 900, 1200, 1638, 1639, 1900, 2500 })
            {
                double asAtr = SpoilerlessAircraft.EffectiveFraction(
                    SpoilersPosition.FromRaw(raw), "AT76");
                Assert.IsFalse(SpoilersPosition.IsDeployed(asAtr),
                    $"{raw}/16383 en un ATR no puede ser un despliegue");
            }

            // El mismo valor en un 737 sí cuenta: la degradación no silencia a los demás.
            Assert.IsTrue(SpoilersPosition.IsDeployed(
                SpoilerlessAircraft.EffectiveFraction(SpoilersPosition.FromRaw(1900), "B738")));
        }
    }
}
