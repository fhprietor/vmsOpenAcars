using System.Collections.Generic;
using vmsOpenAcars.Models;

namespace vmsOpenAcars.Helpers
{
    /// <summary>De dónde sale la traza que pinta el closeup.</summary>
    internal enum CloseupTraceSource
    {
        /// <summary>Traza de aproximación, muestreo de **2 s** (≈ 550 ft por muestra a 150 kt).</summary>
        Approach,

        /// <summary>Traza fina del flare, **10 Hz** (`flare_track`). Es la que sostiene la escala de 1 000 ft.</summary>
        Flare,
    }

    /// <summary>
    /// **Qué traza pinta el closeup y con qué rótulo se declara.**
    ///
    /// El closeup del perfil vertical nació pintando `approach_track`, que es de **2 s**: en los
    /// últimos 1 500 ft al umbral eso son **6 muestras** de mediana en el corpus local (el vuelo 41
    /// tiene 5 entre −1 500 y +1 500 ft), y la escala de ±1 000 ft no tiene con qué dibujarse. Desde
    /// v0.9.32 hay una captura dedicada a **10 Hz** (`flare_track`, `Helpers/FlareCapturePolicy`), y
    /// cuando el vuelo la tiene, el closeup pinta **esa**: la resolución fina está donde se viene a
    /// mirar.
    ///
    /// **No son lo mismo y el gráfico tiene que decirlo.** El rótulo (`LandingCloseup_TrackFlare` /
    /// `LandingCloseup_TrackApproach`) distingue las dos resoluciones; confundir 0,1 s con 2 s es
    /// creer que el avión tocó donde no hay ni una muestra. El helper devuelve la **clave**, no el
    /// texto: el idioma lo pone `LocalizationService`, como en el resto de la aplicación.
    ///
    /// **El convenio de signos es el mismo en las dos trazas** y es el del closeup: la X son pies al
    /// umbral, **positivo antes** del umbral y **negativo después**, que es exactamente lo que ya
    /// guarda `flare_track.dist_ft` (y `approach_track.dist_nm`, positivo acercándose). Aquí no se
    /// da la vuelta a nada: la conversión de signo existe **solo** para la distancia de toma —que
    /// `TouchdownCloseupGeometry` y `flights.touchdown_dist_ft` llevan en positivo— y vive en
    /// `FlareChartLayout`, que es su único punto de cruce. Volver a invertirla aquí pondría el punto
    /// de toque en el lado equivocado del umbral.
    /// </summary>
    internal static class CloseupTrackSource
    {
        /// <summary>Clave del rótulo cuando se pinta la traza fina (`flare_track`, 10 Hz).</summary>
        internal const string FlareLabelKey = "LandingCloseup_TrackFlare";

        /// <summary>Clave del rótulo cuando se pinta la traza de 2 s (`approach_track`).</summary>
        internal const string ApproachLabelKey = "LandingCloseup_TrackApproach";

        /// <summary>
        /// **La decisión**: la traza fina manda cuando existe. Sin muestras —todos los vuelos
        /// anteriores a `flare_track`, o la captura que no llegó a armarse— se queda la de 2 s,
        /// exactamente como estaba.
        /// </summary>
        internal static CloseupTraceSource Pick(int flareSampleCount) =>
            flareSampleCount > 0 ? CloseupTraceSource.Flare : CloseupTraceSource.Approach;

        /// <summary>La clave de idioma del rótulo de esa fuente.</summary>
        internal static string LabelKey(CloseupTraceSource source) =>
            source == CloseupTraceSource.Flare ? FlareLabelKey : ApproachLabelKey;

        /// <summary>
        /// **Los puntos de la traza fina en el sistema del closeup**: `(X en pies al umbral, AGL)`.
        ///
        /// Dos cosas que se dejan fuera a propósito:
        ///
        /// - **La altitud se corta en el toque** (`OnGround`). En tierra el AGL vale 0 por
        ///   definición, así que seguir pintándolo dibujaba una caída vertical de la última altura
        ///   al suelo y una raya pegada a la banda de pista durante toda la frenada. No es la
        ///   maniobra, es el convenio del dato — es el mismo corte que ya hace la ventana del flare.
        ///   El punto de toque no se pierde: la marca `TD` sale de `touchdown_dist_ft`.
        /// - **El radioaltímetro.** El eje del closeup es **AGL** y la ventana del flare es la que
        ///   pinta el radioaltímetro (es su instrumento). Mezclar los dos en una sola línea dibujaría
        ///   saltos allí donde una muestra tiene uno y la otra no; aquí manda el AGL, que es lo que
        ///   dice el eje.
        /// </summary>
        internal static List<(double XFt, double AltFt)> FlarePoints(IList<FlareTrackPoint> samples)
        {
            var points = new List<(double XFt, double AltFt)>();
            if (samples == null) return points;

            foreach (var s in samples)
            {
                if (s == null || s.OnGround || !s.AglFt.HasValue) continue;

                double alt = s.AglFt.Value;
                if (double.IsNaN(alt) || double.IsInfinity(alt)) continue;
                if (double.IsNaN(s.DistFt) || double.IsInfinity(s.DistFt)) continue;

                points.Add((s.DistFt, alt));
            }
            return points;
        }

        /// <summary>
        /// **Los puntos de la traza de 2 s en el sistema del closeup.** La tabla guarda la distancia
        /// en millas náuticas y el eje del closeup va en pies: la conversión es la misma
        /// (`TouchdownCloseupGeometry.FeetPerNm`) que usan el encuadre y el eje X, para que las dos
        /// trazas caigan en la misma columna del gráfico.
        /// </summary>
        internal static List<(double XFt, double AltFt)> ApproachPoints(IList<ApproachTrackPoint> track)
        {
            var points = new List<(double XFt, double AltFt)>();
            if (track == null) return points;

            foreach (var p in track)
            {
                if (p == null) continue;
                double x = p.DistNm * TouchdownCloseupGeometry.FeetPerNm;
                double alt = p.AglFt;
                if (double.IsNaN(x) || double.IsInfinity(x)) continue;
                if (double.IsNaN(alt) || double.IsInfinity(alt)) continue;

                points.Add((x, alt));
            }
            return points;
        }
    }
}
