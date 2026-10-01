using System.Collections.Generic;
using Newtonsoft.Json;

namespace vmsOpenAcars.Models.NavData
{
    // ── Response wrappers ─────────────────────────────────────────────────────────

    internal class NavRunwaysResponse
    {
        [JsonProperty("runways")]
        public List<NavRunway> Runways { get; set; } = new List<NavRunway>();
    }

    internal class NavTaxiwaysResponse
    {
        [JsonProperty("taxiways")]
        public List<NavTaxiway> Taxiways { get; set; } = new List<NavTaxiway>();
    }

    internal class NavParkingsResponse
    {
        [JsonProperty("parkings")]
        public List<NavParking> Parkings { get; set; } = new List<NavParking>();
    }

    internal class NavHoldShortResponse
    {
        [JsonProperty("holdshort")]
        public List<NavHoldShort> Holdshort { get; set; } = new List<NavHoldShort>();
    }

    internal class NavApproachesResponse
    {
        [JsonProperty("approaches")]
        public List<NavApproach> Approaches { get; set; } = new List<NavApproach>();
    }

    // ── Runway ────────────────────────────────────────────────────────────────────

    internal class NavRunway
    {
        /// <summary>
        /// Variacion magnetica del aeropuerto, en grados y con el este positivo (SKBO -8,58;
        /// KBOS -13,73; LEMD +0,68). NavData la publica desde el 30/09/2026. **Sin dato vale 0**, y
        /// entonces la conversion de abajo no cambia nada: degradar sin datos, nunca bloquear.
        /// </summary>
        [JsonProperty("mag_var")]
        public double MagVar { get; set; }
        [JsonProperty("name")]                  public string  Name                { get; set; }
        [JsonProperty("heading")]               public double  Heading             { get; set; }
        [JsonProperty("threshold_lat")]         public double  ThresholdLat        { get; set; }
        [JsonProperty("threshold_lon")]         public double  ThresholdLon        { get; set; }
        [JsonProperty("end_lat")]               public double  EndLat              { get; set; }
        [JsonProperty("end_lon")]               public double  EndLon              { get; set; }
        [JsonProperty("length_ft")]             public double  LengthFt            { get; set; }
        [JsonProperty("width_ft")]              public double  WidthFt             { get; set; }
        [JsonProperty("elevation_ft")]          public double  ElevationFt         { get; set; }
        [JsonProperty("offset_threshold_ft")]   public double  OffsetThresholdFt   { get; set; }
        [JsonProperty("has_ils")]               public bool    HasIls              { get; set; }
        [JsonProperty("ils_ident")]             public string  IlsIdent            { get; set; }
        [JsonProperty("ils_freq_mhz")]          public double? IlsFreqMhz          { get; set; }
        [JsonProperty("ils_course")]            public double? IlsCourse           { get; set; }
        [JsonProperty("ils_glideslope_angle")]  public double? IlsGlideslopeAngle  { get; set; }
    }

    // ── Taxiway ───────────────────────────────────────────────────────────────────

    internal class NavTaxiway
    {
        [JsonProperty("name")]      public string Name      { get; set; }
        [JsonProperty("type")]      public string Type      { get; set; }
        [JsonProperty("width_ft")]  public double WidthFt   { get; set; }
        [JsonProperty("start_lat")] public double StartLat  { get; set; }
        [JsonProperty("start_lon")] public double StartLon  { get; set; }
        [JsonProperty("end_lat")]   public double EndLat    { get; set; }
        [JsonProperty("end_lon")]   public double EndLon    { get; set; }

        /// <summary>Identificadores de nodo (29/09/2026). Dos extremos con el mismo id son **el
        /// mismo nodo**, sin fusión por proximidad: es lo que permite borrar el umbral de 45 m de
        /// `TaxiGraph`, que afectaba a **318 de los 569 segmentos de SKBO** (56%, mediana 39,8 m).
        /// Son 52 bits a propósito, para que quepan en `long` y sean enteros seguros en JavaScript;
        /// **cambiaron de 64 a 52 bits el 29/09/2026**, así que una caché vieja trae ids distintos.
        /// Todavía no los usa el grafo: es el paso siguiente.</summary>
        [JsonProperty("start_node_id")] public long? StartNodeId { get; set; }
        [JsonProperty("end_node_id")]   public long? EndNodeId   { get; set; }
    }

    /// <summary>
    /// Empalme curado entre dos nodos que el escenario no une (29/09/2026). Es lo que permite
    /// puentear un hueco real —el `K2`→`K1` de la 14R de SKBO son **76,0 m** de plataforma sin
    /// ningún segmento— **sin volver a la heurística de proximidad** que el grafo acaba de dejar.
    /// NavData no sirve un empalme cuyo nodo haya dejado de existir: aparece en `invalid`.
    /// </summary>
    internal class NavTaxiwayJoin
    {
        [JsonProperty("node_a")]   public long   NodeA   { get; set; }
        [JsonProperty("node_b")]   public long   NodeB   { get; set; }
        /// <summary>Calle que pide el empalme (el `K2` del caso de SKBO).</summary>
        [JsonProperty("taxiway")]  public string Taxiway { get; set; }
        [JsonProperty("gap_m")]    public double GapM    { get; set; }
        [JsonProperty("lat_a")]    public double LatA    { get; set; }
        [JsonProperty("lon_a")]    public double LonA    { get; set; }
        [JsonProperty("lat_b")]    public double LatB    { get; set; }
        [JsonProperty("lon_b")]    public double LonB    { get; set; }
        [JsonProperty("note")]     public string Note    { get; set; }
        [JsonProperty("source")]   public string Source  { get; set; }
        /// <summary>
        /// **Confianza del empalme (0–1)** que publica NavData: `0,45 × distancia normalizada + 0,35 ×
        /// giro + 0,20 × nombre`. Es lo que permite decidir un umbral **por confianza y no por
        /// distancia**: en CYUL su único empalme viene con **0,14** y un giro de 68°, y no es lo mismo
        /// que el `component_bridge` de SKBO con **0,88** y 0,6°. Ausente ⇒ se trata como 1 (el empalme
        /// curado del `K2` no trae `turn_deg`; degradar sin dato, nunca bloquear por una suposición).
        /// </summary>
        [JsonProperty("confidence")] public double Confidence { get; set; } = 1.0;
        /// <summary>Cambio de rumbo entre los dos extremos, en grados. Puede no venir (curado).</summary>
        [JsonProperty("turn_deg")]   public double? TurnDeg { get; set; }
        /// <summary>`gap` (hueco entre extremos sueltos) o `component_bridge` (puente entre componentes).</summary>
        [JsonProperty("kind")]       public string Kind   { get; set; }
    }

    /// <summary>
    /// Estadísticas de la red de calles que publica NavData junto a los empalmes: es lo que deja
    /// **derivar el umbral de fusión de sus propios datos** en vez de llevar el 45 m escrito a mano, y
    /// saber de antemano si en ese aeropuerto la red está completa. `components` es el aviso serio: en
    /// KMIA son 2 y el segundo son los muñones del eje de pista.
    /// </summary>
    internal class NavTaxiNetworkStats
    {
        [JsonProperty("nodes")]            public int    Nodes          { get; set; }
        [JsonProperty("segments")]         public int    Segments       { get; set; }
        [JsonProperty("dangling_ends")]    public int    DanglingEnds   { get; set; }
        [JsonProperty("components")]       public int    Components     { get; set; }
        [JsonProperty("median_gap_m")]     public double MedianGapM     { get; set; }
        [JsonProperty("median_segment_m")] public double MedianSegmentM { get; set; }
        [JsonProperty("joins_published")]  public int    JoinsPublished { get; set; }
        [JsonProperty("joins_curated")]    public int    JoinsCurated   { get; set; }
        [JsonProperty("gaps")]             public Dictionary<string, int> Gaps { get; set; }
            = new Dictionary<string, int>();
    }

    internal class NavTaxiwayJoinsResponse
    {
        [JsonProperty("joins")]   public List<NavTaxiwayJoin> Joins   { get; set; } = new List<NavTaxiwayJoin>();
        [JsonProperty("count")]   public int                  Count   { get; set; }
        [JsonProperty("invalid")] public List<object>         Invalid { get; set; } = new List<object>();
        /// <summary>
        /// Hash de 12 caracteres del conjunto de nodos del aeropuerto. **Si cambia, la red cambió**: es
        /// lo que permite invalidar caché en vez de arrastrar empalmes de una versión anterior. Hoy los
        /// empalmes viven en la caché de sesión y cada `PrefetchAirport` los refresca, así que sirve
        /// además para **saber de qué versión salió una ruta** cuando alguien pregunta por ella.
        /// </summary>
        [JsonProperty("version")] public string                Version { get; set; }
        [JsonProperty("stats")]   public NavTaxiNetworkStats   Stats   { get; set; }
    }

    // ── Parking ───────────────────────────────────────────────────────────────────

    internal class NavParking
    {
        [JsonProperty("name")]       public string  Name      { get; set; }
        [JsonProperty("number")]     public int?    Number    { get; set; }
        [JsonProperty("suffix")]     public string  Suffix    { get; set; }
        [JsonProperty("type")]       public string  Type      { get; set; }
        [JsonProperty("radius_ft")]  public double? RadiusFt  { get; set; }
        [JsonProperty("heading")]    public double? Heading   { get; set; }
        [JsonProperty("has_jetway")] public bool    HasJetway { get; set; }
        [JsonProperty("lat")]        public double  Lat       { get; set; }
        [JsonProperty("lon")]        public double  Lon       { get; set; }
    }

    // ── Hold short ────────────────────────────────────────────────────────────────

    internal class NavHoldShort
    {
        [JsonProperty("runway_name")] public string RunwayName { get; set; }
        [JsonProperty("lat")]         public double Lat        { get; set; }
        [JsonProperty("lon")]         public double Lon        { get; set; }
        [JsonProperty("heading")]     public double Heading    { get; set; }

        /// <summary>La **pareja física** de la pista (`["14L","32R"]`). Un punto de espera a mitad
        /// de pista no es «de 14L» ni «de 32R»: es de la 14L/32R. NavData pide expresamente
        /// (29/09/2026) que el filtro por pista de destino use **esta lista** y no `runway_name`,
        /// que se mantiene como etiqueta del extremo más cercano.</summary>
        [JsonProperty("runway_names")] public List<string> RunwayNames { get; set; } = new List<string>();

        /// <summary>`hold_short` o `ils_hold_short` (el del área crítica de ILS: el más alejado del
        /// eje, el que protege la señal). Desde el 29/09/2026 la respuesta sale de los **tipos de
        /// nodo del escenario** (`HSND`/`IHSND`), no de una heurística geométrica.</summary>
        [JsonProperty("type")]        public string Type       { get; set; }

        /// <summary>Calle sugerida por NavData: la que entra más perpendicular a la pista. En un
        /// cruce de cuatro calles puede no ser la que el piloto espera —en el nodo de A3 de SKBO
        /// 14L sugieren `A1` y el piloto dice `A3`—, así que para los avisos se usa esa sugerencia
        /// **solo como último recurso**: ver <see cref="Taxiways"/>.</summary>
        [JsonProperty("taxiway")]     public string Taxiway    { get; set; }

        /// <summary>Todas las calles que se tocan en ese nodo. Puede venir vacía. Es la lista que
        /// permite decir el nombre correcto: la calle por la que llega el avión, si está aquí.</summary>
        [JsonProperty("taxiways")]    public List<string> Taxiways { get; set; } = new List<string>();
    }

    // ── Approaches ────────────────────────────────────────────────────────────────

    internal class NavApproach
    {
        [JsonProperty("type")]               public string              Type             { get; set; }
        [JsonProperty("suffix")]             public string              Suffix           { get; set; }
        [JsonProperty("runway")]             public string              Runway           { get; set; }
        [JsonProperty("fix_ident")]          public string              FixIdent         { get; set; }
        [JsonProperty("has_gps_overlay")]    public bool                HasGpsOverlay    { get; set; }
        [JsonProperty("has_vertical_angle")] public bool                HasVerticalAngle { get; set; }
        [JsonProperty("approach_name")]      public string              ApproachName     { get; set; }
        [JsonProperty("vertical_guidance")]  public string              VerticalGuidance { get; set; }
        [JsonProperty("faf_index")]          public int?                FafIndex         { get; set; }
        [JsonProperty("navaid")]             public NavNavaid           Navaid           { get; set; }
        [JsonProperty("legs")]               public List<NavApproachLeg>        Legs        { get; set; } = new List<NavApproachLeg>();
        [JsonProperty("missed_legs")]        public List<NavApproachLeg>        MissedLegs  { get; set; } = new List<NavApproachLeg>();
        [JsonProperty("transitions")]        public List<NavApproachTransition> Transitions { get; set; } = new List<NavApproachTransition>();

        public string DisplayName => !string.IsNullOrEmpty(ApproachName)
            ? ApproachName
            : string.IsNullOrEmpty(Suffix) ? $"{Type} RWY {Runway}" : $"{Type} {Suffix} RWY {Runway}";
    }

    internal class NavApproachTransition
    {
        [JsonProperty("fix")]        public string              Fix       { get; set; }
        [JsonProperty("fix_type")]   public string              FixType   { get; set; }
        [JsonProperty("fix_region")] public string              FixRegion { get; set; }
        [JsonProperty("type")]       public string              Type      { get; set; }
        [JsonProperty("legs")]       public List<NavApproachLeg> Legs     { get; set; } = new List<NavApproachLeg>();
    }

    internal class NavApproachLeg
    {
        [JsonProperty("type")]                   public string  Type           { get; set; }
        [JsonProperty("fix")]                    public string  Fix            { get; set; }
        [JsonProperty("fix_type")]               public string  FixType        { get; set; }
        [JsonProperty("fix_region")]             public string  FixRegion      { get; set; }
        [JsonProperty("lat")]                    public double? Lat            { get; set; }
        [JsonProperty("lon")]                    public double? Lon            { get; set; }
        [JsonProperty("course")]                 public double  Course         { get; set; }
        [JsonProperty("distance_nm")]            public double  DistanceNm     { get; set; }
        [JsonProperty("altitude_ft")]            public double  AltitudeFt     { get; set; }
        [JsonProperty("altitude2_ft")]           public double  Altitude2Ft    { get; set; }
        [JsonProperty("alt_descriptor")]         public string  AltDescriptor  { get; set; }
        [JsonProperty("speed_kts")]              public int?    SpeedKts       { get; set; }
        [JsonProperty("speed_limit_type")]       public string  SpeedLimitType { get; set; }
        [JsonProperty("vertical_angle")]         public double? VerticalAngle  { get; set; }
        [JsonProperty("turn_direction")]         public string  TurnDirection  { get; set; }
        [JsonProperty("is_flyover")]             public bool    IsFlyover      { get; set; }
        [JsonProperty("rnp")]                    public double? Rnp            { get; set; }
        [JsonProperty("dme_radius_nm")]          public double? DmeRadiusNm    { get; set; }
        [JsonProperty("dme_radial")]             public double? DmeRadial      { get; set; }
        [JsonProperty("recommended_fix")]        public string  CenterFix      { get; set; }
        [JsonProperty("recommended_fix_region")] public string  CenterFixRegion{ get; set; }
        [JsonProperty("recommended_fix_lat")]    public double? CenterLat      { get; set; }
        [JsonProperty("recommended_fix_lon")]    public double? CenterLon      { get; set; }
    }

    // ── Airport info ──────────────────────────────────────────────────────────────

    internal class NavAirportInfo
    {
        [JsonProperty("icao")]                    public string  Icao                 { get; set; }
        [JsonProperty("iata")]                    public string  IataCode             { get; set; }
        [JsonProperty("name")]                    public string  Name                 { get; set; }
        [JsonProperty("city")]                    public string  City                 { get; set; }
        [JsonProperty("country")]                 public string  Country              { get; set; }
        [JsonProperty("iso_country")]             public string  IsoCountry           { get; set; }
        [JsonProperty("type")]                    public string  AirportType          { get; set; }
        [JsonProperty("region")]                  public string  Region               { get; set; }
        [JsonProperty("freqs")]                   public List<NavAirportFreq> Freqs   { get; set; }
        [JsonProperty("elevation_ft")]            public double  ElevationFt          { get; set; }
        [JsonProperty("lat")]                     public double  Lat                  { get; set; }
        [JsonProperty("lon")]                     public double  Lon                  { get; set; }
        [JsonProperty("transition_altitude_ft")]  public double? TransitionAltitudeFt { get; set; }
        [JsonProperty("transition_level_ft")]     public double? TransitionLevelFt    { get; set; }
    }

    internal class NavAirportFreq
    {
        [JsonProperty("type")]          public string Type         { get; set; }
        [JsonProperty("description")]   public string Description  { get; set; }
        [JsonProperty("frequency_mhz")] public string FrequencyMhz { get; set; }
    }

    // ── Status ────────────────────────────────────────────────────────────────────

    internal class NavStatusResponse
    {
        [JsonProperty("status")]            public string Status         { get; set; }
        [JsonProperty("version")]           public string Version        { get; set; }
        [JsonProperty("airac_cycle")]       public string AiracCycle     { get; set; }
        [JsonProperty("airac_valid_until")] public string AiracValidUntil { get; set; }
    }

    // ── Navaid (VOR / NDB / DME) ─────────────────────────────────────────────────

    internal class NavNavaid
    {
        [JsonProperty("ident")]         public string  Ident        { get; set; }
        [JsonProperty("type")]          public string  Type         { get; set; }
        [JsonProperty("name")]          public string  Name         { get; set; }
        [JsonProperty("region")]        public string  Region       { get; set; }
        [JsonProperty("vor_type")]      public string  VorType      { get; set; }
        [JsonProperty("ndb_type")]      public string  NdbType      { get; set; }
        [JsonProperty("frequency_mhz")] public double? FrequencyMhz { get; set; }
        [JsonProperty("frequency_khz")] public double? FrequencyKhz { get; set; }
        [JsonProperty("mag_var")]       public double? MagVar       { get; set; }
        [JsonProperty("lat")]           public double  Lat          { get; set; }
        [JsonProperty("lon")]           public double  Lon          { get; set; }
    }

    // ── Procedures (SID / STAR) ──────────────────────────────────────────────────

    internal class NavProcedureLeg
    {
        [JsonProperty("type")]             public string  Type           { get; set; }
        [JsonProperty("fix")]              public string  Fix            { get; set; }
        [JsonProperty("fix_type")]         public string  FixType        { get; set; }
        [JsonProperty("fix_region")]       public string  FixRegion      { get; set; }
        [JsonProperty("lat")]              public double? Lat            { get; set; }
        [JsonProperty("lon")]              public double? Lon            { get; set; }
        [JsonProperty("course")]           public double? Course         { get; set; }
        [JsonProperty("distance_nm")]      public double? DistanceNm     { get; set; }
        [JsonProperty("altitude_ft")]      public double? AltitudeFt     { get; set; }
        [JsonProperty("altitude2_ft")]     public double? Altitude2Ft    { get; set; }
        [JsonProperty("alt_descriptor")]   public string  AltDescriptor  { get; set; }
        [JsonProperty("speed_kts")]        public int?    SpeedKts       { get; set; }
        [JsonProperty("speed_limit_type")] public string  SpeedLimitType { get; set; }
        [JsonProperty("turn_direction")]   public string  TurnDirection  { get; set; }
        [JsonProperty("is_flyover")]       public bool    IsFlyover      { get; set; }
        [JsonProperty("rnp")]              public double? Rnp            { get; set; }
        // Arco DME (AF) / Radio (RF): presentes solo cuando Type == "AF" o "RF"
        [JsonProperty("dme_radius_nm")]          public double? DmeRadiusNm     { get; set; }
        [JsonProperty("dme_radial")]             public double? DmeRadial       { get; set; }
        [JsonProperty("recommended_fix")]        public string  CenterFix       { get; set; }
        [JsonProperty("recommended_fix_region")] public string  CenterFixRegion { get; set; }
        [JsonProperty("recommended_fix_lat")]    public double? CenterLat       { get; set; }
        [JsonProperty("recommended_fix_lon")]    public double? CenterLon       { get; set; }
    }

    internal class NavProcedure
    {
        [JsonProperty("name")]   public string                Name   { get; set; }
        [JsonProperty("runway")] public string                Runway { get; set; }
        [JsonProperty("legs")]   public List<NavProcedureLeg> Legs   { get; set; } = new List<NavProcedureLeg>();
    }

    internal class NavSidsResponse  { [JsonProperty("sids")]  public List<NavProcedure> Sids  { get; set; } }
    internal class NavStarsResponse { [JsonProperty("stars")] public List<NavProcedure> Stars { get; set; } }

    // ── ILS ───────────────────────────────────────────────────────────────────────

    internal class NavIlsGlideslope
    {
        [JsonProperty("pitch_deg")]   public double  PitchDeg   { get; set; }
        [JsonProperty("altitude_ft")] public double? AltitudeFt { get; set; }
        [JsonProperty("lat")]         public double? Lat        { get; set; }
        [JsonProperty("lon")]         public double? Lon        { get; set; }
        [JsonProperty("range_nm")]    public double? RangeNm    { get; set; }
    }

    internal class NavIls
    {
        [JsonProperty("ident")]            public string           Ident          { get; set; }
        [JsonProperty("type")]             public string           Type           { get; set; }
        [JsonProperty("frequency_mhz")]    public double           FrequencyMhz   { get; set; }
        [JsonProperty("runway")]           public string           Runway         { get; set; }
        [JsonProperty("loc_true_heading")] public double?          LocTrueHeading { get; set; }
        [JsonProperty("loc_width")]        public double?          LocWidth       { get; set; }
        [JsonProperty("mag_var")]          public double?          MagVar         { get; set; }
        [JsonProperty("glideslope")]       public NavIlsGlideslope Glideslope     { get; set; }
    }

    internal class NavIlsResponse
    {
        [JsonProperty("ils")]
        public List<NavIls> Ils { get; set; } = new List<NavIls>();
    }

    // ── Weather ───────────────────────────────────────────────────────────────────

    internal class NavWeather
    {
        [JsonProperty("raw_metar")]     public string  RawMetar     { get; set; }
        [JsonProperty("icao")]          public string  Icao         { get; set; }
        [JsonProperty("wind_dir")]      public int?    WindDir      { get; set; }
        [JsonProperty("wind_speed_kt")] public int?    WindSpeedKt  { get; set; }
        [JsonProperty("wind_gust_kt")]  public int?    WindGustKt   { get; set; }
        [JsonProperty("visibility_m")]  public int?    VisibilityM  { get; set; }
        [JsonProperty("ceiling_ft")]    public int?    CeilingFt    { get; set; }
        [JsonProperty("temperature_c")] public int?    TemperatureC { get; set; }
        [JsonProperty("dewpoint_c")]    public int?    DewpointC    { get; set; }
        [JsonProperty("qnh_hpa")]       public double? QnhHpa       { get; set; }
        [JsonProperty("qnh_inhg")]      public double? QnhInhg      { get; set; }
        [JsonProperty("condition")]     public string  Condition    { get; set; }
    }

    // ── Airport Waypoints (ambient) ───────────────────────────────────────────────

    internal class NavAirportWaypoint
    {
        [JsonProperty("ident")]         public string  Ident        { get; set; }
        [JsonProperty("type")]          public string  Type         { get; set; }  // "named", "VOR-H", "NDB", etc.
        [JsonProperty("lat")]           public double  Lat          { get; set; }
        [JsonProperty("lon")]           public double  Lon          { get; set; }
        [JsonProperty("distance_nm")]   public double  DistanceNm   { get; set; }
        [JsonProperty("frequency_mhz")] public double? FrequencyMhz { get; set; }
        [JsonProperty("frequency_khz")] public double? FrequencyKhz { get; set; }
    }

    internal class NavAirportWaypointsResponse
    {
        [JsonProperty("waypoints")]
        public List<NavAirportWaypoint> Waypoints { get; set; } = new List<NavAirportWaypoint>();
    }

    // ── Airspaces ─────────────────────────────────────────────────────────────────

    internal class NavAirspacesResponse
    {
        [JsonProperty("airspaces")]
        public List<NavAirspace> Airspaces { get; set; } = new List<NavAirspace>();

        /// <summary>Radio REAL de la consulta. Era 200 nm hasta el aviso de NavData del
        /// 29/09/2026; desde entonces son 54 nm. Lo usamos para saber cuántos puntos hay que
        /// muestrear a lo largo de la ruta: asumir 200 nm dejaba la mitad sin mirar.</summary>
        [JsonProperty("radius_nm")]
        public double RadiusNm { get; set; }

        /// <summary>La respuesta es válida pero está **truncada** (tope de páginas/deadline):
        /// hay espacios aéreos que no llegaron. No es un error, pero no se puede presentar como
        /// el mapa completo de la zona. Sólo aparece con el respaldo `openaip_api`.</summary>
        [JsonProperty("partial")]
        public bool Partial { get; set; }

        /// <summary>`"local"` (índice propio por país, sin rate limit) u `"openaip_api"` (respaldo).
        /// Que un corredor salga en `openaip_api` es la señal de que falta el export de un país.</summary>
        [JsonProperty("source")]
        public string Source { get; set; }

        /// <summary>Se recortó por número máximo de espacios (zonas densas): `radius_nm` baja al
        /// valor realmente garantizado — medido en Londres, 91 nm en vez de 200—, así que hace
        /// falta muestrear más fino.</summary>
        [JsonProperty("capped")]
        public bool Capped { get; set; }

        /// <summary>Países que aportan los espacios incluidos. Sólo aparecen los que OpenAIP
        /// publica como export; un hueco aquí es un corredor que podemos reportarles.</summary>
        [JsonProperty("countries")]
        public List<string> Countries { get; set; }
    }

    internal class NavAirspace
    {
        [JsonProperty("id")]          public string                Id          { get; set; }
        [JsonProperty("name")]        public string                Name        { get; set; }
        [JsonProperty("type")]        public string                Type        { get; set; }
        [JsonProperty("icao_class")]  public string                IcaoClass   { get; set; }
        [JsonProperty("country")]     public string                Country     { get; set; }
        [JsonProperty("upper_limit")] public NavAirspaceLimit      UpperLimit  { get; set; }
        [JsonProperty("lower_limit")] public NavAirspaceLimit      LowerLimit  { get; set; }
        [JsonProperty("geometry")]    public NavAirspaceGeometry   Geometry    { get; set; }
        [JsonProperty("frequencies")] public List<NavAirspaceFreq> Frequencies { get; set; } = new List<NavAirspaceFreq>();
        [JsonProperty("on_demand")]   public bool                  OnDemand    { get; set; }
        [JsonProperty("by_notam")]    public bool                  ByNotam     { get; set; }
        [JsonProperty("remarks")]     public string                Remarks     { get; set; }

        // Extracts the ICAO prefix from names like "SKBO / BOGOTA" → "SKBO"
        public string ExtractIcao()
        {
            if (string.IsNullOrEmpty(Name)) return null;
            int slash = Name.IndexOf('/');
            string prefix = (slash > 0 ? Name.Substring(0, slash) : Name).Trim();
            return prefix.Length >= 2 && prefix.Length <= 6 ? prefix : null;
        }
    }

    internal class NavAirspaceLimit
    {
        [JsonProperty("value_ft")]  public double? ValueFt   { get; set; }
        [JsonProperty("reference")] public string  Reference { get; set; }  // "GND","MSL","STD"
        [JsonProperty("display")]   public string  Display   { get; set; }  // "GND","5000ft MSL","FL095","UNL"
    }

    internal class NavAirspaceGeometry
    {
        [JsonProperty("type")]        public string             Type        { get; set; }
        // GeoJSON: each point is [longitude, latitude]
        [JsonProperty("coordinates")] public List<List<double[]>> Coordinates { get; set; }
    }

    internal class NavAirspaceFreq
    {
        [JsonProperty("mhz")]     public string Mhz     { get; set; }
        [JsonProperty("type")]    public string Type    { get; set; }  // "Tower","Approach","Centre"…
        [JsonProperty("name")]    public string Name    { get; set; }
        [JsonProperty("primary")] public bool   Primary { get; set; }
    }

    // ── Nearest approach airport (parallel-runway-aware position match) ────────────

    internal class NavApproachAirportResponse
    {
        [JsonProperty("icao")]                 public string                    Icao               { get; set; }
        [JsonProperty("name")]                 public string                    Name               { get; set; }
        [JsonProperty("airport_distance_nm")]  public double                    AirportDistanceNm  { get; set; }
        [JsonProperty("runway")]               public NavApproachAirportRunway  Runway             { get; set; }
        [JsonProperty("heading_diff_deg")]     public double                    HeadingDiffDeg     { get; set; }
        [JsonProperty("score")]                public double                    Score              { get; set; }
        [JsonProperty("cross_track_nm")]       public double?                   CrossTrackNm       { get; set; }
        [JsonProperty("dist_to_threshold_nm")] public double?                   DistToThresholdNm  { get; set; }

        /// <summary>Variación magnética del aeropuerto elegido (grados, con signo). Vino con la
        /// respuesta del 29/09/2026: es lo que permite convertir en el cliente un rumbo verdadero
        /// —lo que da el simulador— al magnético con el que NavData publica sus rumbos.</summary>
        [JsonProperty("mag_var")]              public double?                   MagVar             { get; set; }
    }

    internal class NavApproachAirportRunway
    {
        [JsonProperty("name")]         public string  Name       { get; set; }
        [JsonProperty("heading")]      public double  Heading    { get; set; }
        [JsonProperty("has_ils")]      public bool    HasIls     { get; set; }
        [JsonProperty("ils_ident")]    public string  IlsIdent   { get; set; }
        [JsonProperty("ils_freq_mhz")] public double? IlsFreqMhz { get; set; }
        [JsonProperty("ils_course")]   public double? IlsCourse  { get; set; }
    }

    // ── Cabin Announcements ───────────────────────────────────────────────────────

    internal class BriefingCheckResult
    {
        public bool   Available { get; set; }
        public string Version   { get; set; }
    }
}
