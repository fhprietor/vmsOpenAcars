namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **¿Toca preguntar a NavData por el aeropuerto de aproximación más cercano?** — la puerta que
    /// abre la re-confirmación de aeropuerto/pista durante el descenso y la aproximación.
    ///
    /// El porqué (medido, vuelo real `wk0896ajpxKalPrp`, SKBQ→SKCL, `vmsOpenACars/0.9.40`): el
    /// piloto **alternó a SKMR** (Montería, elevación **41 ft**), aterrizó en su pista 14/32 con los
    /// dos toques a **0,08 NM** y **0,38 NM** del ARP, y el cliente **no lo detectó**: el PIREP
    /// quedó con `arr_airport_id = SKCL` y el QNH se comparó contra SKCL. La puerta de entonces era
    /// `computedAgl > 1000`, y ese AGL sale de `FlightManager.ReferenceAirportElevation`, que
    /// mientras no haya desvío confirmado devuelve **la elevación del destino planeado** (SKCL,
    /// **3 162 ft**). La prueba literal está en el log del propio vuelo: a las **04:11:12**, con el
    /// avión a **3 181 ft MSL**, el cliente escribió *«COMPUERTA DE APROXIMACIÓN (18 ft AGL)»* —
    /// 3 181 − 3 162 = **19 ft**. Con esa referencia el AGL salió **negativo en el 100 % de los
    /// sondeos** de las dos finales (**−43 a −3 111 ft**), `computedAgl > 1000` no se cumplió **ni
    /// una vez** y SKMR **jamás llegó a ser candidato** (los descartes que sí se loguean —SKCB y
    /// SKCZ— ocurrieron en descenso, con la referencia del origen SKBQ de 95 ft).
    ///
    /// Y no era un problema de los filtros: **con la posición real el servidor devuelve SKMR en
    /// todos los sondeos** y **los seis filtros pasan con holgura** (lateral 0,001–0,081 NM; cono
    /// 4°; gradiente 318 ft/NM; alterno 8,2 NM frente a 313 NM del destino; `along ≤ 0`;
    /// persistencia; y **285 NM fuera** del corredor de la llegada ULQ4E de SKCL). El único eslabón
    /// roto era esta puerta.
    ///
    /// **La regla**: la referencia pasa a ser **el avión**, no el aeropuerto planeado — por debajo
    /// de <see cref="MaxMslFt"/> (10 000 ft MSL) cualquier aeropuerto de aproximación cercano tiene
    /// sentido, y a FL350 no. Así el **desvío a un campo mucho más bajo** (SKMR: 51–1 067 ft MSL)
    /// vuelve a sondearse, que es justo el caso que más importa, y el techo sigue cerrando el
    /// crucero.
    ///
    /// **Por qué no se conserva `computedAgl > 1000` como puerta principal** —que es la propuesta
    /// literal `computedAgl > 1000 || altitud MSL &lt; 10 000—: con el destino planeado a 3 162 ft,
    /// a FL350 el AGL vale **31 838 ft &gt; 1 000**, así que esa disyunción **deja abierto el
    /// crucero** y contradice el propio throttle que se quiere conservar. El AGL del destino
    /// planeado es precisamente el término que se está desacoplando, así que se queda **solo como
    /// respaldo** para cuando falte la altitud MSL (ver abajo).
    ///
    /// **Sin dato de altitud MSL** (`NaN`/infinito) la puerta **se comporta como hoy**
    /// (`computedAgl > 1000`): no se abre por el camino nuevo, que es la regla de la casa —degradar
    /// sin datos, nunca abrir por una suposición—.
    ///
    /// **No se toca la referencia general del AGL, y es deliberado**: que `CurrentAGL` siga usando
    /// la elevación del destino hasta que haya un desvío confirmado **no se puede arreglar aquí**;
    /// mientras no sepas sobre qué campo estás, no hay otra referencia, y ese es justamente el
    /// círculo que esta puerta rompe. Por eso la decisión vive en esta función pura, y no en
    /// `FlightManager.ReferenceAirportElevation`.
    ///
    /// Límite conocido y anotado, no medido: un destino **por encima de 10 000 ft MSL** (SPZO Cusco,
    /// 10 860 ft) quedaría por encima del techo durante toda la aproximación y no se sondearía; no
    /// hay ningún caso así en el corpus del piloto (SKBO 8 360, SKRG 7 028, SKCL 3 162, SKCG 12,
    /// SKMR 41 ft) y el sondeo se reanuda en cuanto el avión baja de 10 000 ft MSL.
    /// </summary>
    internal static class DiversionQueryGate
    {
        /// <summary>
        /// Techo de altitud **MSL** por debajo del cual tiene sentido preguntar por un aeropuerto de
        /// aproximación cercano: **10 000 ft**. Es la `DiversionQueryMaxMslFt` de la propuesta, y
        /// sustituye al `computedAgl > 1000` como puerta principal. A FL350 (35 000 ft) cierra.
        /// </summary>
        internal const double MaxMslFt = 10000.0;

        /// <summary>
        /// Cadencia mínima entre sondeos: **5 s**, la de siempre (el servidor limita 30 consultas/min
        /// por piloto, así que no se puede sondear a la frecuencia de la telemetría, 20 Hz).
        /// </summary>
        internal const double ReconfirmIntervalSec = 5.0;

        /// <summary>
        /// AGL de la condición **de hoy**, que se conserva **solo** como respaldo sin dato de altitud
        /// MSL: por encima de **1 000 ft AGL** del destino planeado se re-confirmaba, y por debajo el
        /// gate de aproximación estabilizada ya había decidido.
        /// </summary>
        internal const double MinAglFt = 1000.0;

        /// <summary>¿Hay altitud MSL utilizable? `NaN`/infinito = sin dato (degradar, no inventar).</summary>
        internal static bool HasAltitudeMsl(double altitudeMslFt)
            => !double.IsNaN(altitudeMslFt) && !double.IsInfinity(altitudeMslFt);

        /// <summary>
        /// **La decisión**, pura, sin red ni WinForms, con test. Devuelve `true` cuando toca llamar a
        /// `NavDataService.FindApproachAirport`.
        ///
        /// `computedAglFt` es el AGL que ya gobierna el vuelo (`FlightManager.CurrentAGL`, medido
        /// contra la elevación del destino planeado hasta que haya desvío confirmado) y **solo se usa
        /// como respaldo** cuando falta la altitud MSL.
        /// </summary>
        internal static bool ShouldQuery(bool navAvailable, bool reconfirming,
                                         double computedAglFt, double altitudeMslFt,
                                         double secondsSinceLastQuery)
        {
            if (!navAvailable) return false;        // sin NavData no hay a quién preguntar
            if (reconfirming) return false;         // ya hay una consulta en vuelo
            if (secondsSinceLastQuery < ReconfirmIntervalSec) return false;   // throttle de 5 s

            // Sin dato de altitud MSL se degrada a la condición de siempre: la puerta nueva no se
            // abre por una suposición.
            if (!HasAltitudeMsl(altitudeMslFt)) return computedAglFt > MinAglFt;

            // La referencia es el avión: por debajo del techo MSL, cualquier aeropuerto cercano
            // puede ser el de llegada real, y la elevación del destino planeado deja de importar.
            return altitudeMslFt < MaxMslFt;
        }
    }
}
