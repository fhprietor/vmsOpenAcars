using System;

namespace vmsOpenAcars.Models
{
    /// <summary>
    /// **Una muestra de la traza fina del flare**: el tramo desde que se arma la captura —los
    /// últimos ~1 500 ft al umbral, o 1 000 ft AGL como respaldo— hasta un margen después del toque.
    ///
    /// Es la traza de **alta frecuencia** (`flare_track`), hermana de
    /// <see cref="ApproachTrackPoint"/> pero **no la sustituye ni la reutiliza**: la de aproximación
    /// es de **2 s** (≈ 2,2 s medidos en la base local, ≈ 550 ft por muestra a 150 kt) y la usan los
    /// cuatro gráficos actuales. Meter el flare ahí obligaría a cambiar el muestreo de todo el
    /// descenso para afinar 5 segundos, que es exactamente lo que no se quiere.
    ///
    /// **Lo que no existe va en `null`, no en 0.** Un 0 en <see cref="RadarAltFt"/> sería un avión a
    /// cero pies, y un 0 en <see cref="PitchDeg"/> un avión perfectamente nivelado: son datos, no
    /// huecos. Solo se persiste lo que el simulador publica de verdad.
    /// </summary>
    public class FlareTrackPoint
    {
        public int      FlightId { get; set; }
        public int      SeqNo    { get; set; }

        /// <summary>Instante de la muestra, en UTC. Es lo que se guarda en la base.</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Segundos respecto al toque (**negativo antes** de tocar). Se **deriva** al mirar la
        /// traza, no se guarda: el toque se conoce al filear el PIREP y la captura empieza antes,
        /// así que persistirlo obligaría a un UPDATE posterior de toda la tabla.
        /// </summary>
        public double? TdRelativeSec { get; set; }

        /// <summary>Distancia al umbral en **pies**, positivo antes de él y negativo pasado.</summary>
        public double DistFt { get; set; }

        /// <summary>Altitud sobre el terreno en pies. El radioaltímetro manda cuando existe.</summary>
        public double? AglFt { get; set; }

        /// <summary>
        /// Lectura del **radioaltímetro** (FSUIPC `0x31E4`), en pies. Es el instrumento bueno para
        /// el flare, pero no todos los addons lo publican: sin dato, `null`, y el gráfico usa AGL.
        /// </summary>
        public double? RadarAltFt { get; set; }

        /// <summary>Velocidad indicada, en nudos.</summary>
        public double? IasKt { get; set; }

        /// <summary>Velocidad vertical, en pies por minuto.</summary>
        public double? VsFpm { get; set; }

        /// <summary>Actitud de morro, en grados.</summary>
        public double? PitchDeg { get; set; }

        /// <summary>Alabeo, en grados.</summary>
        public double? BankDeg { get; set; }

        /// <summary>Velocidad respecto al suelo, en nudos.</summary>
        public double? GsKt { get; set; }

        /// <summary>
        /// N1 del motor 1 en porcentaje (0–100). Se guarda **como está**: si el addon no escribe el
        /// offset viene 0 y se descarta al persistir, porque un motor a 0 % no es un hueco, es un
        /// motor parado y aquí no interesa ese dato.
        /// </summary>
        public double? Eng1Pct { get; set; }

        /// <inheritdoc cref="Eng1Pct"/>
        public double? Eng2Pct { get; set; }

        /// <summary>
        /// **N2 del motor 1, en porcentaje (0–100)** — la bobina de alta presión, la que dice si el
        /// motor está girando de verdad. Se lee del offset `0x2220` (`DWORD`, 0–16384 = 0–100 %) en
        /// `Services/FsuipcService.cs`, declarado en la línea 108 y convertido a porcentaje en la 601.
        ///
        /// **`null` cuando el addon no escribe el offset.** `FsuipcService` documenta ese caso en su
        /// propio campo (`N2_1`, línea 1837: «0 when addon does not write offset 0x2220») y aquí se
        /// respeta: un 0 guardado se leería como «motor parado» en mitad del descenso, que es
        /// exactamente el error que se evita en el radioaltímetro.
        /// </summary>
        public double? Eng1N2Pct { get; set; }

        /// <inheritdoc cref="Eng1N2Pct"/>
        public double? Eng2N2Pct { get; set; }

        /// <summary>
        /// Deflexión del **estabilizador horizontal**: **no se captura**. La posición de la columna
        /// que mueve el flare no viaja en ninguno de los offsets que este cliente ya lee, y meter un
        /// offset nuevo sin poder comprobarlo contra el simulador sería inventar la magnitud. Se
        /// deja fuera en vez de rellenarlo con ceros.
        /// </summary>

        /// <summary>Posición de los **flaps** en porcentaje del handle (0–100).</summary>
        public double? FlapsPct { get; set; }

        /// <summary>
        /// **El detente real** que el simulador publica en `0x0BFC` («Notch actual»): 0 = arriba,
        /// 1 = primera compuerta, 2 = segunda…, la escala que pinta el marcado del avión. Se lee junto
        /// a <see cref="FlapsPct"/> (`0x0BDC`, el recorrido del mando) y es un dato distinto: el
        /// porcentaje es una **posición** y el detente es una **compuerta**.
        ///
        /// **`null` cuando el avión no lo publica** (ver `Helpers/FlapSetting.DetentOrNull`, que cruza
        /// el notch con el porcentaje para no confundir «flaps arriba» con «el addon no escribe el
        /// offset»), cuando la fila es anterior a la columna o cuando la muestra no lo traía. Nunca 0
        /// por defecto: un 0 es «flaps arriba», que es un dato.
        /// </summary>
        public int? FlapsIndex { get; set; }

        /// <summary>Spoilers desplegados.</summary>
        public bool? SpoilersDeployed { get; set; }

        /// <summary>Avión en tierra en el momento de la muestra.</summary>
        public bool OnGround { get; set; }
    }
}
