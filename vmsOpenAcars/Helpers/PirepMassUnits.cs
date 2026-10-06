using System;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// Unidad de masa con la que viaja el payload del PIREP: **phpVMS la guarda en LIBRAS**.
    ///
    /// La fuente de esa unidad es el propio phpVMS, no nuestra etiqueta:
    ///  - `config/phpvms.php` → `internal_units` = `fuel: 'lbs'`, `mass: 'lbs'`, con el aviso
    ///    «DO NOT CHANGE THESE! It will result in messed up data».
    ///  - Su autor, en el foro: «Units in ACARS are just in lbs» y «Everything internally is
    ///    stored in imperial units» (https://forum.phpvms.net/t/acars-units/13683).
    ///  - La respuesta de su API lo dice por escrito en cada PIREP:
    ///    `"block_fuel": {"localUnit":"kg","internalUnit":"lbs","responseUnits":["kg","lbs"]}`.
    ///
    /// Y **no convierte lo que recibe**: `App\Casts\FuelCast::set()` devuelve el valor tal cual
    /// cuando no es un objeto `Fuel`, así que un número suelto se escribe en la columna y al leerlo
    /// se interpreta en la unidad interna. Su test lo fija literalmente: «no conversion with plain
    /// numbers» (`tests/Feature/PIREPTest.php`).
    ///
    /// Nosotros sí calculamos en kg (el simulador se lee en libras por el offset 0x126C y el
    /// cliente lo pasa a kg), así que el número que sale hacia la API tiene que ir en libras o
    /// phpVMS lo registra **2,20462 veces por debajo**. Medido en el PIREP real `9E20We81wlBgp3Nj`
    /// (SKRG→SKBG, B38M, 05/10/2026): enviamos `block_fuel = 4444` (los 4444 kg del simulador) y la
    /// API lo devolvió como `{kg: 2015,76, lbs: 4444}`, mientras las posiciones —que ya iban en
    /// libras— marcaban 9797 lbs al inicio y 6519 al final (4444 kg → 2957 kg reales).
    ///
    /// El log **sigue en kg** (`Log_FuelSummary`, `Log_FuelUsed`, `Log_FuelSim`): esos valores son
    /// kg de verdad y es la unidad que entiende el piloto. Lo que se corrige es solo el payload.
    /// </summary>
    internal static class PirepMassUnits
    {
        /// <summary>Libras por kilogramo (2,20462). La constante del proyecto es <see cref="UnitConverter.KgToLbs"/>.</summary>
        internal const double LbsPerKg = UnitConverter.KgToLbs;

        /// <summary>Kilogramos → libras, sin redondear (para ida y vuelta y comprobaciones).</summary>
        internal static double KgToLbs(double kg) => kg * LbsPerKg;

        /// <summary>Libras → kilogramos, sin redondear (inversa de <see cref="KgToLbs"/>).</summary>
        internal static double LbsToKg(double lbs) => lbs / LbsPerKg;

        /// <summary>
        /// Masa en kg → el entero en libras que viaja en `block_fuel` / `fuel_used`.
        /// Se redondea a entero porque el payload siempre ha ido en números enteros
        /// (`Math.Round(…, 0)`); el redondeo va **después** de convertir, nunca antes.
        /// </summary>
        internal static double PayloadLbs(double kg) => Math.Round(KgToLbs(kg), 0);
    }
}
