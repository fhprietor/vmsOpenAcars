using System;
using System.Collections.Generic;

namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **¿Se está moviendo el avión?** Se mide **desplazamiento**, no velocidad de tierra.
    ///
    /// El caso que lo obliga es el PIREP `29Z3A8r748rGPnzq` (SKRG→KSEA, 20/10/2026): con el avión
    /// aparcado en el puesto y los motores apagados, el simulador publicaba **GS 4-7 kt**, **IAS 0** y un
    /// `vs` de hasta 500 fpm, mientras las posiciones delataban **60 m en ocho minutos**. Aquellas
    /// lecturas entraron como rodaje (243 s), abrieron la fase `TaxiOut` doce minutos antes de que el
    /// avión rodara y cobraron un −5 de luces con los motores apagados.
    ///
    /// Y el ruido miente **en los dos sentidos**: los 4-6 kt con ENG1 en marcha y el avión parado
    /// también contaron como tiempo con un solo motor, o sea que regalaban bonificación. Con
    /// desplazamiento, el avión parado no cruza el umbral por mucho que su GS parpadee, porque su
    /// posición no cambia.
    ///
    /// La ventana: se compara la posición de **hace <see cref="WindowSeconds"/>** con la de ahora. Ocho
    /// metros en cinco segundos son ~3 kt —el mismo umbral de antes— pero medidos con la prueba de que
    /// el avión se movió.
    /// </summary>
    internal sealed class GroundMotion
    {
        /// <summary>Ventana sobre la que se mide el desplazamiento (s).</summary>
        internal const double WindowSeconds = 5.0;

        private readonly List<Sample> _samples = new List<Sample>();
        private double _clock;

        private struct Sample
        {
            internal double At;
            internal double Lat;
            internal double Lon;
        }

        /// <summary>Metros recorridos dentro de la ventana: línea recta del extremo viejo al actual.</summary>
        internal double WindowMetres { get; private set; }

        /// <summary>Metros entre la observación anterior y esta: el recorrido real, sumable.</summary>
        internal double StepMetres { get; private set; }

        /// <summary>
        /// Un ciclo de telemetría. `dtSeconds` son los segundos **creíbles** desde el ciclo anterior (ya
        /// filtrados por <see cref="SampleClock.CountableSeconds"/>): un portátil que se suspende no
        /// puede fabricar un desplazamiento que no existió.
        /// </summary>
        internal void Observe(double latitude, double longitude, double dtSeconds)
        {
            StepMetres = 0.0;
            if (_samples.Count > 0)
            {
                Sample previous = _samples[_samples.Count - 1];
                StepMetres = GeoMath.DistanceKm(previous.Lat, previous.Lon, latitude, longitude) * 1000.0;
            }

            _clock += Math.Max(0.0, dtSeconds);
            _samples.Add(new Sample { At = _clock, Lat = latitude, Lon = longitude });

            while (_samples.Count > 1 && _clock - _samples[0].At > WindowSeconds)
                _samples.RemoveAt(0);

            // Con una sola muestra todavía no hay ventana: se informa de cero y no se miente.
            WindowMetres = _samples.Count > 1
                         ? GeoMath.DistanceKm(_samples[0].Lat, _samples[0].Lon, latitude, longitude) * 1000.0
                         : 0.0;
        }

        /// <summary>Olvida la historia: al cambiar de fase o de vuelo, la ventana anterior no vale.</summary>
        internal void Reset()
        {
            _samples.Clear();
            _clock           = 0.0;
            WindowMetres     = 0.0;
            StepMetres       = 0.0;
        }
    }
}
