namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **Densidad de la traza de rodaje** que se manda a phpVMS: **5 s** con la guía de rodaje activa,
    /// el comportamiento de siempre sin ella.
    ///
    /// El porqué (medido con NavData, 01/10/2026): su **métrica de cobertura de la traza de rodaje**
    /// sale con **mediana 65,0 %** y **mínima 22,2 %** porque hoy el envío va a
    /// <see cref="AppConfig.UpdateIntervalTaxi"/> (**30 s**) y un rodaje real deja **9–34 puntos**.
    /// Con esa densidad la métrica no es medible; a 5 s la traza ya es medible.
    ///
    /// **Y son 5 s, no el 1 Hz que pedía NavData**: a 1 Hz serían **30× el tráfico** en phpVMS durante
    /// los vuelos guiados (30 s → 1 s son 30 veces más peticiones por piloto y por rodaje), y a 5 s ya
    /// se pasa de los **9–34 puntos** por rodaje a una traza medible.
    ///
    /// Se pide **solo con la guía activa**, no subiendo el valor por defecto de la configuración:
    /// quien no usa la guía **no tiene por qué ver su tráfico multiplicado**. El cambio queda acotado
    /// al piloto que ya pidió la guía giro a giro y, con ella, la traza densa.
    ///
    /// **Una sola regla, tres puertas.** La cadencia real de la traza no la fija un solo sitio: la
    /// capan tres filtros en serie, y aflojar solo uno no da 5 s. Aquí está la decisión de los tres,
    /// para que no haya tres reglas distintas:
    ///
    /// | Puerta | Efecto sin guía | Efecto de aflojarla |
    /// |---|---|---|
    /// | <see cref="TaxiSeconds"/> — `FsuipcService.SetUpdateIntervalForPhase` | 30 s (configurado) | produce la muestra |
    /// | <see cref="SendFloorSeconds"/> — `TelemetryCoordinator.PositionUpdateInterval` | 5 s | deja pasar la muestra |
    /// | <see cref="TaxiTracePosThresholdDeg"/> — `TelemetryCoordinator.HasSignificantChange` | 0,0003° ≈ 33 m | acepta el paso corto |
    ///
    /// Sin las otras dos, el **deduplicador** de ~33 m deja la muestra siguiente a **4–6 s** a
    /// velocidad de rodaje y el suelo de 5 s la recorta otra vez: el intervalo adaptativo bajaría a
    /// 5 s y por el cable no saldría ni una muestra cada 5 s.
    ///
    /// Degrada sin datos: si la guía **no está activa o no se sabe** —el llamante pasa <c>false</c>
    /// cuando no puede afirmarlo— se devuelve **el valor de siempre**. Nunca se acelera el envío por
    /// una suposición.
    /// </summary>
    internal static class UpdateIntervalPolicy
    {
        /// <summary>Cadencia de la traza de rodaje con la guía activa: **5 s**. No son los 1 Hz que
        /// pedía NavData porque a 1 Hz sería **30×** el tráfico en phpVMS durante los vuelos guiados y
        /// a 5 s la traza ya es medible.</summary>
        internal const int GuidedTaxiSeconds = 5;

        /// <summary>
        /// Cadencia de reserva si el valor configurado no es utilizable (≤ 0, o sea un `App.config`
        /// roto): la misma por defecto de <see cref="AppConfig.UpdateIntervalTaxi"/>. Un cero no puede
        /// significar «manda en cada vuelta del sondeo».
        /// </summary>
        internal const int FallbackSeconds = 30;

        /// <summary>Umbral de desplazamiento entre muestras sin guía: 0,0003° ≈ **33 m**.</summary>
        internal const double DefaultPosThresholdDeg = 0.0003;

        /// <summary>
        /// Umbral con la guía activa: 0,00002° ≈ **2,2 m**, muy por debajo del paso de **5 s** de
        /// rodaje (5 kt ≈ 13 m), así que en marcha no deduplica: solo evita **repetir la muestra
        /// parado**, que no añade cobertura y engorda la traza.
        /// </summary>
        internal const double GuidedTaxiPosThresholdDeg = 0.00002;

        /// <summary>
        /// Segundos entre posiciones en rodaje: **5** con la guía activa, **el valor configurado**
        /// sin ella. Pura, sin red ni WinForms, con test.
        /// </summary>
        internal static int TaxiSeconds(bool taxiGuidanceActive, int configuredSeconds)
        {
            if (taxiGuidanceActive) return GuidedTaxiSeconds;
            return configuredSeconds > 0 ? configuredSeconds : FallbackSeconds;
        }

        /// <summary>
        /// Suelo del envío de posiciones: con la guía activa baja a **5 s** —hoy el suelo de
        /// `TelemetryCoordinator.PositionUpdateInterval` ya vale 5 s, así que esta puerta ya no
        /// recorta; se deja explícita para que un suelo mayor no vuelva a comerse la cadencia—. Sin
        /// guía se queda en su valor de siempre. Pura, con test.
        /// </summary>
        internal static int SendFloorSeconds(bool taxiGuidanceActive, int baseSeconds)
            => taxiGuidanceActive ? GuidedTaxiSeconds : baseSeconds;

        /// <summary>
        /// Desplazamiento mínimo entre dos posiciones de rodaje (grados). Con la guía activa se baja
        /// al paso corto (~2,2 m, muy por debajo del paso de 5 s); sin ella manda el umbral de
        /// siempre (~33 m). Pura, con test.
        /// </summary>
        internal static double TaxiTracePosThresholdDeg(bool taxiGuidanceActive)
            => taxiGuidanceActive ? GuidedTaxiPosThresholdDeg : DefaultPosThresholdDeg;
    }
}
