namespace vmsOpenAcars.Helpers
{
    /// <summary>
    /// **La ruta propuesta (`planned`) no se publica mientras NavData no tenga su almacén.**
    ///
    /// El porqué (NavData, 01/10/2026): su validador de ingesta **rechaza hoy** el cuerpo de
    /// `POST /taxi-routes/observations` que manda <c>TelemetryCoordinator.PublishPlannedRoute</c>,
    /// por dos motivos exactos: **`missing_stand`** —el `planned` no lleva `stand`— y
    /// **`route_too_short`** —`route` es obligatorio con **≥2 calles** y la propuesta **no** puede
    /// ir en `route`, va en `planned`—. Hasta que monten su almacén propio (`TaxiRoutePlanned`, con
    /// su migración de MariaDB) y nos digan la forma exacta, cada vuelo guiado manda una petición
    /// condenada: no rompe nada —va en segundo plano y se registra **una** vez—, pero es ruido en
    /// su log y en el nuestro.
    ///
    /// Por eso el interruptor (`AppConfig.TaxiPlannedObservationEnabled`, clave
    /// `taxi_planned_observation_enabled`) nace **apagado** y se enciende **sin recompilar** el día
    /// que exista el almacén. Aquí está la decisión entera, para poder probarla sin red: si está
    /// apagado **no se construye el cuerpo ni se llama al cliente HTTP**.
    ///
    /// **Degrada sin datos**: sin ruta encontrada o con menos de dos puntos de polilínea no hay
    /// nada que publicar —el cuerpo no describiría un camino—, y ese guardia es el de siempre, no
    /// se afloja. Pura, sin red ni WinForms, con test.
    /// </summary>
    internal static class TaxiPlannedObservationPolicy
    {
        /// <summary>
        /// Mínimo de puntos de polilínea para que la propuesta describa un camino. Es el guardia
        /// que ya tenía <c>PublishPlannedRoute</c>; se conserva tal cual.
        /// </summary>
        internal const int MinPolylinePoints = 2;

        /// <summary>
        /// ¿Se publica la observación `planned`? **Solo** con el interruptor encendido **y** una
        /// propuesta utilizable. Apagado devuelve <c>false</c> aunque la ruta sea perfecta: el
        /// llamante sale antes de construir el cuerpo, así que no se toca la red.
        /// </summary>
        internal static bool ShouldPublish(bool enabled, bool routeFound, int polylinePoints)
        {
            if (!enabled) return false;
            return routeFound && polylinePoints >= MinPolylinePoints;
        }
    }
}
