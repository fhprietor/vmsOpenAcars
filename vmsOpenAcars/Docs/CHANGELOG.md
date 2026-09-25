# CHANGELOG — vmsOpenAcars

---

## [0.9.14] — 2026-09-24

### Added

- **RAAS: avisos de rodaje y guía giro a giro** — pedido del mantenedor («una forma más didáctica
  e inmersiva de rodar, tipo RAAS»), con voz SAPI en esta primera versión. Sale un **popup de
  rodaje** al encender la **luz de taxi** o al entrar en **TaxiOut** —lo que ocurra antes, una vez
  por vuelo— donde el piloto elige la **pista del aeropuerto** (por defecto la del OFP,
  `SimbriefPlan.OriginRunway`) y revisa la **ruta sugerida**, escrita con las calles separadas por
  espacios y **editable** (la sugerencia es la más corta por el grafo, no la que ha dado ATC).

  Avisos (log + OSD + voz), con el formato de un RAAS real:

  | Aviso | Cuándo |
  |---|---|
  | `APROXIMANDO PISTA 14R` | a ≤150 m de un hold-short **yendo hacia él**, GS ≥ 2 kt |
  | `ESPERA ANTES DE PISTA 14R` | a ≤40 m |
  | `CALLE M A LA DERECHA EN 120 METROS` | a ≤250 m del cruce con la siguiente calle de la ruta |
  | `GIRA AHORA A LA DERECHA EN CALLE M` | a ≤60 m |
  | `FUERA DE RUTA, VUELVE A CALLE B` | la calle actual no está en la ruta |
  | `RUTA DE RODAJE COMPLETA` | sin waypoints pendientes |

  Cada aviso se dice **una vez por situación**, con 20 s de enfriamiento y re-armado al terminar la
  situación: el log de rodaje actual repetía `CALLE B, Próximo a C` varias veces por minuto, y eso
  en voz es inservible.

- **`Helpers/TaxiGraph.cs`** (nuevo, puro) — el aeropuerto como grafo: extremos de segmento a ≤45 m
  son un nodo, cada segmento una arista, y un Dijkstra hasta el umbral de la pista da la secuencia
  de calles. Resuelve también el cruce entre dos calles (para la distancia al giro) y el **lado**
  del giro a partir de los dos rumbos.

- **`Helpers/RaasAdvisor.cs`** (nuevo, puro y con estado) — `TaxiRoutePlan` (parsea lo que el piloto
  escribe de verdad: espacios, comas, flechas, `via`; colapsa nombres repetidos seguidos),
  `ResolveGuidance` (en qué punto de la ruta va, próxima calle, distancia al cruce y lado) y el
  motor de avisos con su antirrebote. El instante entra por parámetro: sin reloj propio, para poder
  probarlo.

- **`Services/RaasVoice.cs`** (nuevo) — voz por **SAPI** (`System.Speech`, que va con .NET
  Framework: nada que distribuir). Cola FIFO en un hilo propio, así que **el hilo de telemetría
  nunca se bloquea esperando a que termine de hablar**; volumen propio; intenta la voz del idioma de
  la aplicación y, si no hay ninguna instalada, **degrada en silencio** diciéndolo una vez en el log.

- **`UI/Forms/TaxiRouteForm.cs`** (nuevo) — el popup: pista, ruta editable, botón `RECALCULAR`
  (recalcula por grafo al cambiar de pista), casillas de RAAS y voz, volumen con prueba hablada, y
  `EMPEZAR GUÍA` / `AHORA NO`.

### Fixed

- **NullReferenceException al arrancar** («Error inicializando servicios: Object reference not set
  to an instance of an object») — reportado por el mantenedor al lanzar la depuración. La
  suscripción al nuevo evento del RAAS se puso dentro de `SubscribeToEvents()`, que
  `MainViewModel` llama en la **línea 107**, mientras que el coordinador `_tc` se crea en la
  **110**: la suscripción dereferenciaba un `_tc` todavía nulo y la excepción caía en el `try`
  del arranque. Movida a después de `_tc.WireEvents()`, con un comentario en el propio código
  avisando del orden, que es el que ya seguían las demás suscripciones al coordinador.

- **El aviso de hold-short nunca sonaba en un rodaje real** — encontrado al reproducir el rodaje del
  PIREP `MNjR664PBAr25RbD` (SKBO→MMGL). `NavDataService.FindHoldingPoint` filtraba por
  `HeadingDelta(hs.Heading, heading) > 45°`, pero el `heading` de un hold-short es el **eje de la
  pista**, no la dirección de llegada: en ese vuelo el avión rodó a **267–270°** mientras los
  hold-shorts de la 14R están a **136°**, o sea **134° de diferencia** — descartados todos, incluso
  pasando a **5–22 m** de ellos (y con una parada de **135 s** con el freno puesto a 22 m del de la
  14R). Ahora el filtro es «voy **hacia** el punto» (rumbo al hold-short dentro de ±90° del rumbo
  del avión), que sí discrimina: si queda de través, rodando en paralelo, no se avisa.

### Verificación

- Build **Debug** y **Release** en verde; suite completa **242/242** (13 tests nuevos).
- `RaasTests` usa **106 segmentos reales de SKBO** (AIRAC 2609) y las posiciones reales del rodaje:
  la ruta sugerida del puesto G49 a la pista 14R sale por las calles que el piloto hizo de verdad
  (C → B → M → K → V/K1, >1 500 m), el parseo acepta lo que se escribe en la práctica, la guía
  apunta al cruce correcto con su lado, y los cinco puntos reales frente al hold-short de la 14R
  producen `APROXIMANDO`/`ESPERA` mientras el filtro viejo del eje los habría descartado.
- `es.json` y `en.json`: **416 claves cada uno**, simétricos (21 nuevas).
- Versión **0.9.14** en los tres atributos de `AssemblyInfo` y en el proyecto de tests.
- **No verificado en vuelo**: el popup, la voz en un equipo con voces SAPI y la guía sobre un rodaje
  nuevo. La geometría y las decisiones están probadas con datos reales; falta la sesión de
  simulador, y en particular **oir** cómo quedan los avisos encadenados.

---

## [0.9.13] — 2026-09-24

### Fixed

- **El "resume desde el historial ACARS" estaba muerto: leía una ruta que no existe** —
  encontrado al buscar la traza del PIREP `MNjR664PBAr25RbD` para comprobar una duda geométrica.
  `ApiService.GetPirepAcarsAsync` pedía `GET api/pireps/{id}/acars` y esta instalación responde
  **404** (`The route api/pireps/{id}/acars could not be found`); como el método se tragaba el
  fallo con `return new List<>()`, el `ResumeFromAcarsHistoryAsync` que lo consume recibía una
  lista vacía **siempre** y fallaba en silencio: ni restauraba las penalizaciones del checkpoint
  ni mostraba el historial. Mismo patrón que `GetNearestAirport` (404) y `MovePilotAsync` (405),
  ya eliminados en v0.9.8.

  La ruta que sí existe es **`GET api/pireps/{id}/acars/position`**, la misma que el cliente usa
  por POST para enviar cada posición: con ese PIREP (SKBO→MMGL, **ya fileado**, `state=2`)
  devuelve **967 entradas / 1,8 MB en ~2 s**, con lat/lon, rumbo, GS, MSL, VS, IAS y `status`.

- **El historial no llega en orden cronológico** — y eso habría dejado el arreglo anterior a
  medias. `/acars/position` entrega **primero los 239 `CHK` y después las posiciones**, y dentro
  de cada grupo tampoco respeta la hora: en ese PIREP el último `CHK` del array era el de las
  **22:43** cuando el más reciente era el de las **02:52**. Como `ResumeFromAcarsHistoryAsync`
  usa `LastOrDefault(status == "CHK")` para restaurar el scoring y `Skip(count - 20)` para mostrar
  los últimos 20 apuntes, sin ordenar habría restaurado el **checkpoint equivocado** del vuelo.
  Ahora `GetPirepAcarsAsync` ordena por `created_at` en la frontera de la API, que es donde el
  contrato "el más nuevo al final" tiene que ser cierto.

### Docs

- **`CLAUDE.md` se partió en dos para que quepa.** Tenía **67,3 KB** y el presupuesto de
  instrucciones del agente es **65 536 bytes**: el harness lo **truncaba** en cada turno, y lo que
  cortaba era el final del archivo —*Próximas áreas*, justo las decisiones pendientes que no se
  pueden perder (y menos al cambiar de equipo). Se movieron a `Docs/architecture.md`:
  - la narrativa de desvíos (~21 KB: SKTL, SKGY, los tres falsos de KBOS, SKCL→SKBO, el QNH
    provisional, el corredor y los seis filtros) → `architecture.md` → *Aeropuerto de llegada
    distinto al planeado*, junto al flujo que ya vivía allí;
  - el índice de archivos clave (~10 KB, 46 filas) → `architecture.md` → *Referencias de archivos
    clave*.

  En `CLAUDE.md` queda de cada uno un resumen con puntero. Resultado: **38,0 KB** y sin
  truncación. La regla queda escrita en **Reglas de trabajo → Documentación**: el techo de 64 KB,
  que el harness trunca en silencio, y que lo largo va a `architecture.md`.
- **`Docs/architecture.md`** gana además una sección **API phpVMS — endpoints verificados**: qué
  rutas existen y cuáles no en esta instalación, los códigos de `status` del ACARS (`BST`, `PBT`,
  `TXI`, …), el formato del checkpoint `SC:` y el aviso de que el historial no llega ordenado, para
  no volver a probarlo a ciegas.

### Verificación

- Build **Debug** y **Release** en verde; suite completa **229/229**.
- `CLAUDE.md`: **38 KB**, por debajo del presupuesto con ~27 KB de margen.
- `architecture.md`: conserva íntegras las dos secciones movidas (comprobado por encabezado).
- Versión **0.9.13** en los tres atributos de `AssemblyInfo` y en el proyecto de tests.
- **No verificado en vuelo**: que el resume muestre bien el historial y restaure el checkpoint
  correcto. Los datos están comprobados contra la API real (967 entradas, orden corregido), pero el
  camino completo —arrancar un vuelo, cortarlo, reanudarlo— necesita una sesión del simulador.

---

## [0.9.12] — 2026-09-24

### Fixed

- **La API Key de NavData se quedaba sin su propia línea** — reportado por el mantenedor al ver
  la pantalla. Al añadir la fila de la URL en v0.9.11, el rótulo *NavData API* de la fila de
  estado se quedó apuntando a la **fila 11**, que es la de la API Key: dos `Label` en la **misma
  celda** del `TableLayoutPanel`, el segundo pintado encima del primero, y la fila 12 con la
  columna 0 vacía. Resultado visible: la API Key aparecía rotulada como *NavData API* y compartía
  línea con el estado/`TEST`/`REFRESH`, en vez de tener la suya entre la URL y el estado.

  Es un fallo de la v0.9.11 y se explica solo: ese rótulo estaba en la fila 11 y la renumeración
  movió a su vez la Key de la 10 a la 11, así que la celda quedó ocupada dos veces. Ninguna
  comprobación automática lo detecta —los `Label` superpuestos no dan error de compilación ni de
  ejecución— así que lo que lo caza es revisar la rejilla: **una celda, un control**.

  Corregido a la fila 12, con un comentario en el propio código explicando por qué el rótulo del
  estado va en su fila y no en la de arriba.

- **`REFRESH` y `TEST` compartían celda con el resultado del test** — pedido del mantenedor.
  Estaban en el mismo `Panel` que el texto de estado y se repartían el ancho en un manejador de
  `Resize` a mano: los botones se quedaban con el borde derecho y al `Label` del resultado le
  tocaba el ancho sobrante, así que en cuanto el mensaje era largo —`AIRAC 2509 until …⚠ EXPIRED`,
  o `Cache cleared — reload flight plan to fetch updated data`— se cortaba justo donde el piloto
  necesita leerlo. Ahora cada cosa tiene su fila: el resultado en una, los dos botones en otra,
  alineados a la derecha en un `FlowLayoutPanel` con `RightToLeft` (el orden en pantalla sigue
  siendo `[REFRESH][TEST]`) y sin manejador de `Resize`, que era pura aritmética manual
  sustituible por el layout.

- **El resultado del test ya no repite la URL.** Tenía `Text = AppConfig.NavDataApiUrl` de cuando
  el rótulo de estado era la única forma de ver la URL configurada. Con el campo propio de la
  v0.9.11 era redundante, y ahora muestra el ciclo AIRAC si la sesión ya lo conoce y queda en
  blanco hasta que se pulse `TEST`.

- **La última fila de la columna izquierda se habría recortado.** La tabla pasó de 13 a 14 filas
  (490 px) y el área de contenido útil son `alto − 99` px —barra de título 35, botones 44,
  `Padding` 4 y 16—: con los 560 px de ventana eran 461 px, así que la fila nueva no cabía. La
  ventana pasa a 920×**600** y el `MinimumSize` al mismo alto, para que no se pueda encoger hasta
  dejar la rejilla cortada.

La sección queda así:

| Fila | Columna 0 | Columna 1 |
|---|---|---|
| 9 | *── NavData API ──* (span 2) | |
| 10 | NavData URL | `navdata_api_url` |
| 11 | API Key | `navdata_api_key` (enmascarada) |
| 12 | NavData API | resultado del test / estado del AIRAC |
| 13 | _(vacía: los botones se explican solos)_ | `[REFRESH] [TEST]` |

### Verificación

- Build **Debug** y **Release** en verde; suite completa **229/229**.
- Rejilla de la tabla izquierda auditada celda por celda (14 filas × 2 columnas): hay
  **un solo control por celda**, sin superposiciones, y la fila 13 sólo lleva los botones. Es la
  comprobación que faltaba en v0.9.11.
- Versión **0.9.12** en los tres atributos de `AssemblyInfo` y en el proyecto de tests.
- **No verificado**: que se vea bien en pantalla. La superposición era invisible al compilador,
  así que la confirmación definitiva es abrir Settings.

---

## [0.9.11] — 2026-09-24

### Added

- **Campo editable para la URL de NavData en Settings** — pedido del mantenedor al ver que
  `navdata_api_url` existía en la configuración y se usaba en **todas** las llamadas
  (`NavDataClient` la lee en cada petición, no la cachea), pero no había ninguna forma de
  editarla desde la aplicación: había que abrir `vmsOpenAcars.exe.config` a mano. La sección
  **NavData API** de Settings tiene ahora la fila *NavData URL* → *URL NavData* encima de la API
  Key, con la tabla izquierda pasando de 12 a 13 filas.

  Es además una promesa incumplida que queda saldada: el `BRIEFING` (documento del piloto) ya
  listaba *NavData API URL* como campo de esa pantalla desde antes, y el campo no existía.

- **El TEST de NavData valida lo que hay escrito, no lo guardado** — `NavDataClient.TestApiAsync`
  acepta un `urlOverride` opcional y Settings le pasa el contenido del campo. Sin eso, pegar una
  URL nueva y pulsar TEST habría validado contra la URL antigua, dando por bueno un valor que
  todavía no se había guardado. Mismo criterio que la key, que ya se pasaba desde el formulario.

  Guardar un cambio de URL o de key sigue reiniciando la aplicación (`HasChanges()` lo detecta),
  que es el comportamiento que ya tenían las credenciales.

### Docs

- **`Docs/BRIEFING.md`** — la tabla de la sección NavData API dice ahora que la URL **sí** es un
  campo editable, que guardar reinicia la app y que TEST usa lo escrito; y aclara que
  *Origin Domain* **no** se edita en pantalla (se deduce del dominio de la API de phpVMS, y solo
  se toca a mano en el `.config` si NavData vive en otro dominio). Pie a v0.9.11.
- **`CLAUDE.md`** — versión actual a v0.9.11; firma de `NavDataClient.TestApiAsync` con el nuevo
  parámetro; contador de claves de idioma a 395.
- **`Docs/architecture.md`** — fila de `navdata_api_url` en la tabla de claves, indicando que se
  edita en Settings; versión del documento a 0.9.11.
- **`Docs/MEMORY.md`** — índice revisado a v0.9.11.

### Verificación

- Build **Debug** y **Release** en verde (MSBuild 15.0 de VS2017), exit 0.
- Suite completa **229/229**.
- `es.json` y `en.json`: **395 claves cada uno**, simétricos. `NavData URL` → *URL NavData* en
  español y *NavData URL* en inglés.
- Versión en los binarios: `FileVersion = 0.9.11.0` y **`ProductVersion = 0.9.11`** en Debug y
  Release — los tres atributos movidos juntos, siguiendo la regla añadida en v0.9.10.
- **No verificado**: que el formulario se vea bien con la fila extra no se puede comprobar sin
  abrir la interfaz. La tabla izquierda pasa de 420 a 455 px de alto y el área de contenido ronda
  los 474 px con el tamaño por defecto de la ventana (920×560), así que entra; con la ventana en
  su tamaño mínimo (760×520) es la fila del estado de NavData la que puede quedar apretada.

---

## [0.9.10] — 2026-09-24

### Added

- **Avisos de zona restringida en el OSD, configurables** — pedido del mantenedor. Settings →
  OSD incorpora la fila *Restricted zones* → *Alert on the OSD*, nueva clave
  `osd_airspace_alerts` (def `true`, así que el comportamiento actual no cambia para quien no la
  toque).

  Apaga **solo el aviso en pantalla** de las zonas acotadas: `AIRSPACE
  {PROHIBITED|RESTRICTED|DANGER}`, `AIRSPACE AHEAD` y `ABOVE … DO NOT DESCEND` — precisamente los
  que son intrusivos (severidad Crítica y con chime). **No** suprime el log de vuelo ni el
  polígono en el mapa: el piloto que apaga el OSD sigue teniendo el registro y la traza, que era
  el motivo de pedirlo (zonas con muchas áreas activas, o vuelo local dentro de una CTR). Las
  entradas y salidas de CTR/TMA/RMZ (`OnAirspaceEntered`, informativas) quedan fuera del ajuste
  porque no son zonas restringidas.

  Se evalúa al disparar y no al iniciar sesión (`MainViewModel.LogBoundedAirspace` y el handler
  de `OnAirspaceOverflight`), así que se puede cambiar en vuelo: aplica al siguiente aviso sin
  reiniciar. La casilla se auto-guarda al marcarla, igual que el resto de OSD/Cabin.

  Cobertura de configuración: `Helpers/AppConfig.cs` (propiedad con backing field, para el
  cambio en caliente), `UI/Forms/SettingsForm.cs` (fila nueva en la tabla, que pasa de 11 a 12
  filas), `UI/Forms/MainForm.cs` (default para instalaciones que regeneran el `.config`) y
  `App.config` local de desarrollo.

### Fixed

- **El rótulo de la fila nueva salía como `[[Airspace]]`** — reportado por el mantenedor al
  probarla. Los rótulos de `SettingsForm` se traducen con `_(clave)`, usando el propio texto en
  inglés como clave; **una clave ausente no cae al inglés**: `LocalizationService.GetString`
  devuelve `[[clave]]` y eso es lo que se pintaba en pantalla. Se añaden las claves
  `Restricted zones` (*Zonas restringidas* / *Restricted zones*) y `Stg_AirspaceOsdAlerts`
  (*Avisar en el OSD* / *Alert on the OSD*) a los dos idiomas — 394 claves cada uno, simétricos.
  El texto de la casilla, que estaba fijo en inglés, pasa por `_()` como el resto de la fila.
  Queda anotado en `CLAUDE.md` para que la próxima fila del formulario no repita el fallo.

- **El cliente seguía anunciándose como 0.9.9 con el binario ya en 0.9.10** — reportado por el
  mantenedor («sigo viendo 0.9.9 en el título»). `Properties/AssemblyInfo.cs` tiene tres
  atributos de versión y al subir a 0.9.10 solo se movieron dos: `AssemblyVersion` y
  `AssemblyFileVersion` quedaron en `0.9.10.0` pero **`AssemblyInformationalVersion` se quedó en
  `0.9.9`** — y `AppInfo.Version` (`Core/Helpers/AppInfo.cs`) **prefiere el informativo** sobre el
  numérico, con el numérico solo como respaldo. Ese valor es el que se pinta en la cabecera, el
  que se escribe en el log de ACARS y el que se envía a phpVMS, así que toda la traza del cliente
  decía 0.9.9 mientras el fichero era nuevo: se veía en `ProductVersion` del `.exe`
  (`FileVersion=0.9.10.0` contra `ProductVersion=0.9.9`).

  Corregido a `0.9.10` en el atributo, con un comentario en el propio `AssemblyInfo.cs`
  advirtiendo de que los **tres** van juntos, y una regla nueva **Versionado** en `CLAUDE.md` con
  la comprobación (`(Get-Item bin\Release\vmsOpenAcars.exe).VersionInfo` → `ProductVersion`).
  El historial confirma que es un desliz de este cambio y no algo arrastrado: en 0.9.2, 0.9.8 y
  0.9.9 los tres atributos sí se movieron juntos.

### Docs

- **`CLAUDE.md`** — la clave `osd_airspace_alerts` y el alcance exacto del ajuste en la sección
  OSD; en **Idioma**, cómo se traducen los rótulos de `SettingsForm`, que una clave ausente
  devuelve `[[clave]]` y no inglés, y la obligación de añadir la clave a los dos `.json`; contador
  de claves a 394. Versión actual: v0.9.10.
- **`Docs/architecture.md`** — fila nueva en la tabla de claves de `App.config`
  (`osd_airspace_alerts`) y versión del documento a 0.9.10.
- **`Docs/BRIEFING.md`** (documento del piloto) — la tabla de la sección OSD documenta ahora
  *Chimes* (que existía sin documentar) y *Restricted zones*, con la aclaración de que el log y el
  mapa no se ven afectados; nota equivalente en el capítulo 7 (OSD Overlay); pie a v0.9.10.
- **`Docs/MEMORY.md`** — índice revisado a v0.9.10.
- **`vmsOpenAcars.Tests/Properties/AssemblyInfo.cs`** — la versión del ensamblado de tests seguía
  en 0.9.4.0 (no se toca desde v0.9.4 porque Release no compila ese proyecto); alineada a 0.9.10.0
  para que ninguna versión del repo quede descolgada de la del cliente.

### Verificación

- Build **Debug** y **Release** en verde (MSBuild 15.0 de VS2017), exit 0.
- Suite completa **229/229** contra el `.exe` Debug recién compilado.
- `es.json` y `en.json`: **394 claves cada uno**, sin diferencias en ninguna dirección.
- Auditoría de las **18 claves de rótulo** y los **6 textos con `_()`** de `SettingsForm` contra
  los dos idiomas: todas existen (era el fallo reportado, y no había ninguna otra).
- La versión que muestra el cliente sale de `AppInfo.Version`, que **prefiere
  `AssemblyInformationalVersion`** y solo cae al `AssemblyVersion` numérico si el informativo
  falta (`Core/Helpers/AppInfo.cs`); `UpdateChecker` sí usa el numérico. Comprobado sobre los
  binarios generados: `ProductVersion = 0.9.10` (antes del arreglo, `0.9.9`) y
  `FileVersion = 0.9.10.0` en Debug y Release. `ProductVersion` es el indicador fiable, porque es
  el que refleja el informativo.

---

## [0.9.9] — 2026-09-24

### Fixed

- **Falso desvío por alineación casual con un aeródromo de la derrota** — reportado en vuelo:
  PIREP `E7DK47e88XdabzoL` (SKRG→SKBQ del 20/09/2026, con un desvío simulado a SKCG). A las
  15:12:47, descendiendo a ~17 000 ft en lat 9.22349, lon -75.43016, rumbo 348, el cliente
  anunció `DIVERTING TO SKTL` — un aeródromo en el que el piloto no pensaba aterrizar. Diez
  minutos después detectó SKCG, que sí era correcto.

  Causa: `NavDataService.FindApproachAirport` (`GET /nearest/approach-airport/`) empareja por
  **rumbo + cross-track**, y mide ese cross-track contra una centerline **infinita**. SKTL
  comparte la alineación costera de la llegada (su rwy 35 va a 348.9° magnética y el avión
  volaba a 348; `heading_diff_deg` **0.9°**), así que el endpoint siguió devolviendo SKTL
  durante **siete minutos** de descenso. El filtro de 3 NM introducido en v0.9.1 lo dejó pasar
  por **0.007 NM** (`cross_track_nm` = 2.993) durante **un solo sondeo** — 5 s después ya era
  3.06 NM y habría sido rechazado.

  Lo decisivo es que ambos matches, el falso y el real, están a **~19 NM del umbral**
  (19.13 vs 19.04), así que la distancia no los distingue: lo que los separa es el **ángulo**
  (9.0° contra 2.1°).

- **Tres falsos desvíos seguidos en la aproximación a KBOS** — mismo vuelo, día siguiente
  (PIREP del 22-23/09/2026, SKCG→KBOS, A320, **v0.9.2**): durante el descenso y el viraje de
  encaje a la final de Logan el cliente anunció `DIVERTING TO 28M` (03:44:33), `TO 1B9`
  (03:45:38) y `TO KOWD` (03:46:09), y revirtió a KBOS a las 03:47:49. Ese último desvío
  además dejó la captura de aproximación apuntando **a la pista 28 de Norwood mientras el
  avión estaba en Logan** (`INICIO CAPTURA APROX: PISTA 28 | Dist 3,4 NM` a las 03:47:16).

  Mecanismo distinto al de SKTL: aquí el destino real **estaba dentro del radio de 20 NM**
  del matcher (a 6.5 NM en el primer evento), pero durante el viraje no tenía ninguna pista a
  menos de 15° del rumbo en curso, así que el endpoint devolvía el aeródromo pequeño mejor
  alineado. Los tres `cross_track_nm` —1.244, 2.993 y 1.203 NM— están por debajo del corte de
  3 NM de v0.9.1: los tres pasaban el filtro viejo, y el de 1B9 por 0.007 NM otra vez.

  `ReconfirmApproachRunway` exige ahora **seis** filtros independientes para marcar un desvío:

  1. **Lateral** — `cross_track_nm ≤ 3 NM`, como en v0.9.1.
  2. **Angular** — `NavDataService.IsWithinFinalCone`: `|cross| ≤ max(0.25 NM,
     dist_umbral · tan 4°)`. Un corte lateral fijo no puede separar los casos reales: el falso
     de 28M estaba a 1.244 NM del eje (pasaba los 3 NM) pero a 14.99 NM del umbral, o sea
     4.7°; el desvío real a SKCG estaba a 0.70 NM y 2.1°. El suelo de 0.25 NM evita que el
     cono colapse a cero a menos de una milla del umbral, donde el ángulo degenera.
  3. **Vertical** — `NavDataService.IsPlausibleDiversionDescent`: el AGL sobre el aeródromo
     emparejado, dividido entre las millas que faltan, no puede pasar de 700 ft/NM (6.6°).
     Propuesta del piloto al ver el caso SKTL ("es ridículo pensar en aterrizar en un
     aeropuerto 10 000 pies por debajo, a tan corta distancia"), y los datos reales le dan la
     razón sin ambigüedad: la serie completa del falso SKTL corrió a **906–2 224 ft/NM**
     (8.5°–20.1°) y **empeoraba** conforme el avión se acercaba, porque descendía pasando de
     largo junto a un campo en el que nunca iba a aterrizar; el desvío **real** a SKCG se
     mantenía en 260 ft/NM (2.4°) y la final de Logan en 310–347 ft/NM (2.9°–3.3°) — sendas de
     planeo de manual. Se mide contra la elevación del aeródromo **emparejado**, no la del
     destino planeado, para que un destino en altiplano no enmascare un alterno a nivel del mar.
     Caza por sí solo la clase SKTL desde el primer sondeo; **no** caza los falsos de Boston,
     cuyo perfil de descenso era normal (2.8°–5.8°) y a los que paran el cono y la distancia.
  4. **El alterno tiene que estar más cerca que el destino planeado** —
     `NavDataService.IsPlausibleDiversionDistance`. No se desvía uno a un aeropuerto que está
     más lejos que aquel al que ya iba: en el primer evento de Boston el "alterno" (28M)
     estaba a 15.1 NM mientras el avión tenía Logan a 6.5 NM. Esta regla detiene ese caso con
     un margen de 2.3×, donde el cono sólo lo detenía por 0.7°.
  5. **Establecido en final** — `SelectApproachThreshold` (extraído de `GetRunwayThreshold`
     para poder probarlo) exige además estar **antes del umbral** (`along ≤ 0`).
  6. **Persistencia** — dos sondeos consecutivos (~10 s) nombrando el mismo alterno, para que
     una sola muestra no dispare el OSD ni reoriente el destino efectivo.

  Replay de la aproximación real completa (14 posiciones, 03:44–03:50) contra el endpoint en
  vivo con la puerta nueva: **cero falsos desvíos**, y la pista real (KBOS 04R) resuelta desde
  9.45 NM — 33 s antes de la reversión que se observó en el log, de modo que la captura de
  aproximación habría sido la correcta desde el principio.

  Marcar el desvío un poco más tarde (al establecerse en final, no al primer match) no degrada
  nada: desde v0.8.10 el veredicto de QNH de llegada es provisional y se decide al filear, y los
  gates de QNH/ILS disparan a TL−1000 ft / 1000 ft AGL, después de esa final.

- **`SelectApproachThreshold` proyectaba sobre el rumbo magnético de la pista, no el
  verdadero** — la variación magnética local (8.4°E aquí) rota el eje y produce un error lateral
  de `distancia × sin(variación)`: **2.8 NM a 19 NM de distancia**. En la práctica ese error
  hacía que el desvío **real** a SKCG (0.70 NM del eje, medido por el endpoint con 0.703)
  calculara 3.51 NM y superara la tolerancia de 2 NM — es decir, la primera versión de este fix
  **habría dejado pasar el desvío real mientras rechazaba el falso**, por el motivo equivocado.
  `ProjectOnRunway` ya proyectaba con `TrueRunwayBearing`; ahora el umbral también. Y
  `ThresholdHeading` devuelve el bearing verdadero en vez del magnético, porque alimenta
  `ComputeApproachMetrics`, que proyecta con él (misma clase de error, hasta ~600 ft de
  desviación lateral en el landing log con variación ≥ 13°).

- **La llegada del propio plan de vuelo como comprobación de "¿estoy donde debería?"** —
  sugerencia del piloto, y es la más directa de todas: el OFP de SimBrief trae el navlog completo
  con las coordenadas de cada fix (`SimbriefPlan.Waypoints`), así que la llegada presentada es una
  polilínea medible. Nuevo `Helpers/RouteCorridor.cs`: si el avión está dentro de su corredor
  (5 NM a cada lado de los últimos 40 NM de la traza planificada), está exactamente donde su plan
  dice, y que el matcher nombre otro aeródromo solo puede ser la llegada pasando cerca de él → no
  se marca desvío.

  Se pudo **validar con datos reales** porque el último OFP del usuario en SimBrief seguía siendo
  el del vuelo SKRG→SKBQ (ruta `BIVI1B BIVIG UW3 SERVO DCT LOLUD LOLU1A`): los puntos de su
  llegada planificada dan **0 NM** de desviación, y el avión se mantuvo a **25–33 NM** de esa
  traza durante todo el descenso del falso SKTL, porque se había ido hacia SKCG. Es decir, esta
  regla **no** habría tocado ese caso —allí deciden los seis filtros geométricos— y actúa en el
  escenario opuesto: volando la llegada presentada, que es exactamente donde ocurrieron los tres
  falsos de KBOS.

  De ese caso, en cambio, **no se pudo verificar el beneficio**: ese vuelo no tiene PIREP en la
  API (se comprobaron los 20; ninguno con KBOS), así que no hay ni ruta ni OFP que medir. Queda
  anotado como la única parte del arreglo sin validar contra vuelo real.

  El corredor se define por **distancia** (los últimos 40 NM de la traza) y no por las banderas
  `IsSidStar`/`Stage` del navlog, porque en este OFP real el fix de transición **LOLUD** llegó con
  `is_sid_star = 0` pese a pertenecer a la llegada: filtrar por bandera dejaba el corredor
  reducido a un tramo de 11 NM. Un desvío real empieza precisamente por salirse de la llegada,
  así que el coste es retrasar la detección lo que se tarde en abandonar 5 NM de corredor. Sin
  navlog (plan construido desde phpVMS, o sin OFP) la regla no opina y deciden los seis filtros.

- **Fuga en la red de seguridad de touchdown** — `LookupRunwayData` probaba el aeropuerto
  resuelto por la fase de aproximación y, como fallback, el de origen. Si `_approachDestination`
  había quedado apuntando a un alterno equivocado, el destino planeado **no se probaba nunca** y
  el PIREP se habría fileado con la llegada incorrecta. Ahora la huella de pista de touchdown
  (evidencia física) se contrasta contra una lista ordenada y sin duplicados: aeropuerto
  resuelto → origen → último alterno propuesto por el matcher → **destino planeado**. Aterrizar
  en el destino planeado tras un desvío revertido limpia también el flag y su override de
  elevación.

### Added

- **`NavDataService.SelectApproachThreshold`** (`internal static`, puro) — la definición de
  "está en final", testeable sin red.
- **`NavDataService.IsWithinFinalCone`** (`internal static`, puro) con
  `MaxFinalConeAngleDeg` (4°) y `FinalConeFloorNm` (0.25).
- **`NavDataService.IsPlausibleDiversionDistance`** (`internal static`, puro) y
  `INavDataService.GetAirportDistanceNm(icao, lat, lon)` — un desvío tiene que ser a un
  aeropuerto más cercano que el destino planeado.
- **`NavDataService.IsPlausibleDiversionDescent`** (`internal static`, puro) con
  `MaxDiversionGradientFtPerNm` (700 ft/NM ≈ 6.6°) y `MinGradientDistanceNm` (0.5) — el AGL
  sobre el aeródromo emparejado tiene que ser alcanzable con un gradiente de descenso usable.
  `ReconfirmApproachRunway` recibe ahora la altitud MSL de la telemetría.
- **`dist_to_threshold_nm`** se mapea en `NearestApproachAirportResult` y alimenta los filtros
  angular y vertical, y el diagnóstico (antes se descartaba).
- **`Helpers/RouteCorridor.cs`** (nuevo, puro) — `ArrivalFixes` / `DistanceFromArrivalNm` /
  `IsOnArrival`: el corredor de la llegada planificada, con `ArrivalCorridorNm` (5) y
  `ArrivalWindowNm` (40). Y **`GeoMath.DistanceToSegmentNm`**, que faltaba: el código solo tenía
  proyección sobre la recta infinita, y medir desviación de traza necesita la recta **recortada**
  a sus extremos (un punto pasado el destino no está "sobre la ruta").
- **Claves de log nuevas** (`es.json` + `en.json`): `Lnm_DiversionRejectedCrossTrack`,
  `Lnm_DiversionRejectedCone`, `Lnm_DiversionRejectedDescent`, `Lnm_DiversionRejectedFarther`,
  `Lnm_DiversionRejectedOnArrival`, `Lnm_DiversionRejectedNotOnFinal`. Los rechazos se registran
  **una sola vez** por aeródromo y motivo, para no repetir la misma línea en cada sondeo de 5 s
  durante todo el descenso.
- **`vmsOpenAcars.Tests/RouteCorridorTests.cs`** (nuevo) — 7 tests sobre el **navlog real de
  SimBrief** del vuelo SKRG→SKBQ, con las coordenadas que entregó el OFP: la llegada son los
  últimos tramos y no los marcados `is_sid_star` (LOLUD llegó sin la bandera), los puntos de la
  llegada dan 0 NM, el corredor aguanta 3 NM al lado y suelta a 7, un punto en crucero no cuenta
  como llegada, las seis posiciones reales del falso SKTL están a más de 20 NM del plan, y sin
  navlog la regla no suprime nada.
- **`vmsOpenAcars.Tests/ApproachThresholdTests.cs`** — 26 tests. Nueve usan las **coordenadas
  exactas de los dos vuelos reales**: el falso SKTL (SKRG→SKBQ) y los tres falsos de la
  aproximación a KBOS (28M, 1B9, KOWD) con sus `cross_track_nm`/`dist_to_threshold_nm`/altitudes
  reales, más la final correcta de Logan. Además comprueban que el eje devuelto es el
  **verdadero** (340.58° SKTL rwy 35, 2.31° SKCG rwy 01) y no el magnético, el límite y el suelo
  del cono, el gradiente con su frontera exacta y su suelo, que el AGL se mide contra la
  elevación del aeródromo emparejado, la regla de distancia, 1 NM pasado el umbral, 3 NM
  laterales, rumbo fuera de 15°, la recíproca y el desempate de paralelas.
- **Suite total: 229 tests** (los data-driven de `ScoringServiceTests` cuentan una vez por fila).
  Build Debug y Release en verde y suite completa en verde antes de esta entrada.

### Docs

- **`Docs/architecture.md`** — el flujo de re-detección de aeropuerto incluye ya el pre-filtro
  del corredor y los seis filtros, la red de seguridad de touchdown con sus cuatro candidatos y
  la sección de tests a v0.9.9 (229). Versión del documento: 0.9.9.
- **`Docs/BRIEFING.md`** — la nota de desvíos al día con v0.9.9 (senda de descenso, final,
  distancia y persistencia), pie a v0.9.9.
- **`CLAUDE.md`** — nueva sección **"Reglas de trabajo"**: el repo es la única memoria que
  sobrevive a un cambio de modelo o de sesión, así que se escriben explícitamente la
  verificación contra servicios reales, los tests con datos de vuelo reales, la obligación de
  declarar lo no verificado, el patrón "un helper puro por regla" y la simetría de idiomas. Y
  **"Decisiones del mantenedor pendientes"** en Próximas áreas (rotar claves, `vmsOpenAcars.txt`,
  el PIREP ausente del SKCG→KBOS).
- **`Docs/MEMORY.md`** deja de presentarse como fuente de verdad (la verdad es `CLAUDE.md` +
  `Docs/CHANGELOG.md`); **`Docs/README.md`** indexa el resumen 0.9.2→0.9.9, que estaba huérfano.

---

## [0.9.8] — 2026-09-21

### Removed

- **Todo el andamiaje de simulación (mock)** — sin código de datos falsos en el binario:

  - **`Services/MockSimulator.cs`** (274 l) eliminado y desregistrado del `.csproj`. Era
    un simulador IFR sintético (SKRG → MQT → AMVES → SKBO) que **nunca se instanciaba**:
    código muerto que además era geométricamente incorrecto (movía la posición mezclando
    grados con seno/coseno sin corregir la longitud).
  - **`LandingLogService.SeedMockData()`** eliminado junto con el botón **SEED DEMO DATA**
    del LOGBOOK y su `#if DEBUG`. Inyectaba 5 vuelos falsos con trayectorias sintéticas
    (SKRG RWY 01) para poblar la interfaz en desarrollo.

  > Consecuencia: el LOGBOOK arranca **vacío** en una instalación nueva y solo se llena
  > con vuelos reales. Las bases de datos existentes conservan sus registros; los vuelos
  > de demostración ya insertados, si los hubiera, no se borran — hay que eliminarlos a
  > mano desde el propio LOGBOOK.

- **Artefactos de documentación generados y desactualizados** (`Docs/`):

  El repositorio guardaba ~12,5 MB de PDF/HTML/PNG/JPEG generados desde los `.md`, y se
  habían quedado atrás en silencio: `architecture.html` era de **v0.4.12** (ni siquiera
  mencionaba la detección de desvíos, los espacios aéreos ni la carta de aproximación) y
  `BRIEFING` —la guía del piloto, la que sí se publica— era de **v0.8.7**, cinco releases
  por detrás del cliente. Ese PDF documentaba un producto que ya no existe: sin los fixes
  de scoring, sin el panel ATC y sin el comportamiento de desvíos actual.

  Eliminados los ocho artefactos. Los `.md` quedan como única fuente de verdad y
  `Docs/README.md` explica ahora cómo regenerar un PDF/HTML **fuera** del control de
  versiones, para que no vuelva a pasar.

### Removed (código muerto)

- **Tres métodos que ya no se llamaban desde ningún sitio**:

  | Método | Por qué se elimina |
  |---|---|
  | `IApiService.GetNearestAirport` / `ApiService.GetNearestAirport` | Confirmado roto en producción: `GET api/airports/nearest` devuelve 404 ("No query results for model `[App\Models\Airport] NEAREST`") — esa ruta no existe en esta instalación de phpVMS |
  | `FlightManager.DetectNearestAirport` | Su único propósito era llamar al anterior, y **no lo llamaba nadie**. Devolvía `CurrentAirport ?? "SKBO"` |
  | `IApiService.MovePilotAsync` / `ApiService.MovePilotAsync` | Confirmado roto en producción: `PUT api/user` devuelve 405. Se dejó de llamar en v0.9.0; phpVMS reubica al piloto solo vía `diversion-airport` |

  Estaban documentados como "rotos pero conservados", que es lo correcto mientras algo los
  use. Al quedar sin llamadores pasan a ser deuda: un método roto que nadie llama conserva
  en el código la tentación de volver a usarlo. Si algún día se confirma la ruta correcta
  de phpVMS, hay que reimplementarlos desde cero.

- **`FsuipcService.GetAutobrakeName(byte)`** — sobrecarga sin llamadores;
  `TelemetryCoordinator.GetAutobrakeName(int)` (que sí se usa) hacía lo mismo.

### Added

- **Panel ATC/ATIS detallado en el mapa** (`UI/Forms/AtcPanel.cs`): botón **ATC ▸** en la
  barra inferior que despliega un panel lateral con **todas** las posiciones activas en
  IVAO — callsign, frecuencia y **el texto completo del ATIS** de cada estación.

  Las formas geográficas del mapa ya indicaban *dónde* está cada posición a 20 NM; este
  panel dice *qué* hay activo y con qué frecuencia, y es el único sitio donde el ATIS se
  lee entero sin pasar el ratón por encima de cada marcador. Agrupa por aeropuerto y
  ordena las dependencias locales primero (DEL → GND → TWR → ATIS → APP → DEP → CTR),
  porque durante el rodaje lo urgente es la torre, no el centro de área. Se repuebla con
  cada poll de IVAO.

  La regla de orden vive en `Helpers/AtcStationOrder.cs`, fuera del control WinForms: es
  dominio, no dibujo, y así se prueba sin arrastrar `System.Windows.Forms` a los tests.

### Fixed

- **La carta de aproximación deformaba la geometría y encuadraba distinto en cada
  aeropuerto** (`ApproachChartForm`):

  La vista de planta proyectaba lat/lon a píxeles con
  `(lon - bounds.Left) / bounds.Width * w` mientras `ComputeBounds` usaba un factor
  **`/1.5` fijo** para el ancho. Ese factor no tenía relación con la latitud del
  aeropuerto, así que las cartas salían estiradas en horizontal y con un encuadre
  arbitrario en cada sitio.

  Ahora el encuadre se calcula **en metros** con `cos(lat)` y la conversión a pantalla
  normaliza ambos ejes con la misma escala, de modo que un grado de longitud ocupa los
  píxeles que le corresponden. Se añade una guarda para latitudes extremas.

- **El FAF se resolvía tres veces con ligeras variantes** (`ApproachChartForm`): la vista
  de planta, el perfil y el briefing strip calculaban el índice del FAF por separado, de
  modo que podían mostrar un FAF distinto para la misma aproximación. Unificado en
  `ComputeFafIndex(NavApproach)`.

- **Los tramos de procedimiento sin coordenadas se omitían, rompiendo la polilínea**
  (`ApproachChartForm`): los tipos `CI`, `VI`, `CA`, `VA`, `FA`, `FM` y `VM` no traen
  lat/lon propias (su extremo se define por un rumbo o por el punto de partida), y el
  render los descartaba — lo que partía la polilínea y **ocultaba el viraje real** que
  describen. Ahora se dibujan como vector desde el último fix conocido usando su rumbo
  publicado, prefiriendo `distance_nm` cuando existe y con un vector representativo en
  caso contrario. Las piernas `RF` (arco de radio a fijo) se dibujan con el arco real
  alrededor de su centro DME, reutilizando la misma rutina que los arcos `AF`.

- **Los METAR no llegaban al LOGBOOK** (`AcarsReporter`): `FlightRecord.MetarRaw` existía
  pero `SnapshotLandingRecord` nunca lo poblaba, así que la columna quedaba vacía siempre.
  Ahora toma el METAR de llegada del servicio METAR **ya descargado durante el vuelo** —
  el snapshot debe ser síncrono y reflejar lo que el piloto tenía delante —, y en caso de
  desvío prefiere el slot del aeropuerto de aterrizaje real sobre el del plan.

- **TA/TL sin fallback: dos criterios de scoring se perdían en silencio**
  (`MainViewModel`, `Helpers/TransitionDefaults.cs`):

  Cuando el NavData de un aeropuerto no publicaba `transition_altitude_ft` /
  `transition_level_ft`, el cliente simplemente no hacía nada: sin aviso de TA/TL, sin
  comprobación de 1013 al subir y sin gate de QNH de llegada por TL. Se añade un respaldo
  regional por `iso_country` (EE. UU./Canadá 18 000 ft, Colombia 18 000, Europa 5 000,
  etc.), marcado explícitamente en el log como respaldo. Con un país desconocido devuelve 0
  y **no inventa** un valor.

### Changed

- **Geometría flat-earth consolidada** (`Helpers/GeoMath.cs`): la proyección
  rumbo/distancia estaba implementada **tres veces** con la misma fórmula y tolerancias
  distintas al caso degenerado (`MapRouteController.Helpers.DispGeoNm`,
  `AirspaceMonitorService.ProjectPosition` y la local de `ApproachChartForm`). Un único
  punto de verdad, con la guarda de `cos(lat)` para latitudes extremas. Los envoltorios
  privados se conservan para no tocar los ~15 puntos de llamada.

- **`ApproachChartForm.InitLayout()` renombrado a `BuildLayout()`**: el nombre ocultaba
  `Control.InitLayout()` (virtual), que es un método del ciclo de vida del control —
  `CS0114`. Un nombre que sugiere ser parte de la infraestructura de WinForms cuando no lo
  es invita a que alguien lo llame en el momento equivocado.

- **Build sin warnings**: de 19 warnings iniciales a **cero**. Además de los 14 campos
  muertos, se corrigieron 4 `CS4014` (llamadas async sin await) y el `CS0114` anterior.

- **Excepciones sin observar en tareas fire-and-forget** (`Helpers/FireAndForget.cs`): los
  cuatro `CS4014` no eran solo ruido del compilador. Una tarea que nadie espera y falla
  deja la excepción sin observar, y en .NET Framework 4.x eso acaba en un
  `UnobservedTaskException` silencioso o en un cierre del proceso. Ocurría en la descarga
  del METAR, la del PDF del OFP, la recarga del approach y el refresco de datos del
  piloto: el usuario solo veía que "no pasó nada".

  Nuevo helper `FireAndForget.Run(work, onError, operationName)` que arranca el trabajo sin
  esperarlo **y observa la excepción**, reportándola al log en lugar de perderla. Se
  eligió una clase aparte y no un método en `L` porque estos archivos hacen
  `using static vmsOpenAcars.Helpers.L`, y `_` ya resuelve a ese helper de localización.

- **14 campos muertos eliminados** (`MainForm`, `FlightPlannerForm`, `MainViewModel`,
  `TelemetryCoordinator`): el build ya no produce ningún `CS0169`/`CS0414`/`CS0219`.

### Added (tests)

- `GeoMathTests` (25 métodos + 17 filas) y `AtcPanelTests` (6) — suite total: **193 tests**.
  Los tests de geometría se comprueban contra valores derivados, no contra reglas de tres
  mentales: al escribirlos descubrieron que 60 NM no son 1° de latitud con la constante
  usada (son ~0.998°, porque 111 320 m/grado equivalen a ~60.108 NM por grado) y que
  `GetTransitionLevelFt` devolvía 1 000 ft para un país desconocido en lugar de "no sé".


## [0.9.7] — 2026-09-21

### Fixed

- **Un fallo transitorio de red borraba el METAR que ya estaba en pantalla**
  (`MetarService`):

  `SafeFetchByIcaoAsync` / `SafeFetchNearestAsync` ponían el slot a `null` **antes** de
  pedir el METAR. Si la descarga fallaba (timeout, 500, DNS), el slot se quedaba vacío y
  `finally` disparaba `OnMetarUpdated` con huecos, de modo que un parpadeo de red hacía
  desaparecer un METAR válido. El valor anterior solo se reemplaza ahora cuando la
  descarga tiene éxito; la antigüedad queda visible vía `MetarData.FetchedAt`.

  Ese `null` tenía una segunda función legítima que se ha conservado de forma dirigida:
  cuando el aeropuerto de un slot **cambia** (nuevo plan, otro origen/destino), el slot se
  vacía en `SetStations` para que un METAR del vuelo anterior no quede en pantalla bajo la
  etiqueta del aeropuerto nuevo. Los slots cuyo aeropuerto no cambia se conservan, que es
  lo que permite tolerar el fallo transitorio.

- **El QNH cacheado no caducaba y podía penalizar con un valor de días atrás**
  (`WeatherService`):

  `_qnhCache` guardaba el último QNH bueno por aeropuerto **sin marca de tiempo** y se
  devolvía como último recurso cuando NavData y aviationweather fallaban. El QNH es una
  magnitud meteorológica, no estática: comparar el altímetro del avión contra un valor
  viejo podía penalizar por una diferencia de >2 hPa que ya no existe, o dar por bueno un
  QNH que sí cambió.

  La caché pasa a guardar `(Qnh, FetchedAt)` con **TTL de 1 hora** (un METAR es válido
  ~1 h y se emite cada 30 min). Un valor caducado se descarta y se elimina de la caché, y
  se devuelve `null` — que el scoring interpreta como "no se pudo comprobar" y **no
  penaliza**. Se elige ese lado del compromiso a propósito: ante un dato que ya no
  representa la realidad, omitir el criterio es más honesto que puntuar con él.

- **`LoadRoute` no descartaba los resultados obsoletos: ganaba el que terminara último**
  (`MapRouteController`):

  Cada cambio en el sidebar (pista, SID, STAR, transición) lanzaba un `Task.Run` sin
  cancelación ni serialización. Si una carga antigua terminaba *después* de una nueva,
  su commit pintaba la ruta y poblaba el sidebar de un procedimiento ya descartado. Un
  cambio de pista dispara además una recarga completa de NavData, así que dos carreras
  consecutivas son lo normal, no la excepción. El spinner tenía el mismo problema: la
  primera tarea en terminar lo apagaba mientras la segunda seguía trabajando, dando la
  impresión de que la recarga había acabado.

  Se añade un **token de generación** (`_routeGeneration`, sellado con `Interlocked`): la
  carga que ya no es la más reciente aborta en el arranque de la tarea y, lo que importa,
  no aplica su commit a la UI. El `StopSpin` vive dentro del commit, así que solo la carga
  vigente apaga el spinner.

  Se eligió el token de generación sobre un `CancellationTokenSource` por proporción: el
  cuerpo de `LoadRoute` son ~700 líneas de geometría y llamadas a NavData, y salpicarlo de
  comprobaciones de cancelación habría sido un cambio mucho mayor con el mismo resultado
  para el caso real (el commit era el único punto donde el daño se materializaba).

### Documented

- Nota de concurrencia en `CLAUDE.md` sobre el token de generación de `LoadRoute` y el TTL
  del QNH.


## [0.9.6] — 2026-09-21

### Fixed

- **"PIREP FILED" falso: el fallback de `/file` decidía con el campo equivocado**
  (`FlightManager.Lifecycle`, `ApiService`, `Models/Pirep`):

  phpVMS puede archivar el PIREP y aun así devolver un código HTTP no-2xx, así que desde
  v0.7.7 `FilePirep()` consulta el estado real con `GET api/pireps/{id}` antes de asumir
  fallo. El problema era **con qué campo decidía**:

  ```csharp
  if (pirepDetail?.Status != null &&
      pirepDetail.Status != "1" && pirepDetail.Status != "6")
      success = true;   // <-- "archivado"
  ```

  `Status` es un **código de fase ACARS** (`"BST"`, `"TXI"`, `"FIN"`… — los mismos que esta
  app envía vía `FlightPhaseHelper.GetStatusCode`), no un número de estado. Comparado
  contra `"1"`/`"6"` daba siempre distinto, de modo que **cualquier respuesta no nula se
  interpretaba como "PIREP archivado"**. Si el `/file` fallaba de verdad pero el `GET`
  respondía, el piloto veía "PIREP FILED — SCORE: XX/100", el botón SEND se desactivaba y
  el vuelo **no quedaba registrado**.

  La raíz estaba en `ApiService.GetPirepDetail()`: **no leía el campo `state`** —el
  numérico correcto, y el que `GetActivePireps()` ya usa para filtrar (`state == 0`)—
  dejando `Pirep.State` sin poblar. El fallback no tenía acceso a la información correcta y
  usaba la que sí tenía.

  **Fix:**
  - `GetPirepDetail()` puebla `State` desde `item["state"]`; si el campo no viene usa
    `Pirep.UnknownState` (-1), distinto de `InProgress = 0` para que un DTO sin poblar no
    se confunda con un estado leído del servidor.
  - Nuevo `PirepState` (enum) y `Pirep.IsActiveState(int?)`: un PIREP sigue activo solo en
    `InProgress` (0) o `Paused` (1); cualquier otro estado significa archivado.
  - El fallback usa `state`. **Ante un estado ilegible devuelve "activo"**, es decir no
    afirma que el PIREP se archivó: se prefiere que el piloto vea "no se pudo enviar" y
    pueda reintentar antes que perder el vuelo en silencio.

  > **Sobre los códigos de estado:** solo `InProgress = 0` está verificado contra
  > producción (es el filtro que usa `GetActivePireps` y funciona). El resto sigue la tabla
  > de estados de phpVMS, que no se pudo consultar en línea durante este cambio. Si se
  > observa un PIREP real, conviene confirmarlos: un valor equivocado aquí solo puede hacer
  > que un vuelo *no* se dé por enviado (el lado conservador), nunca al revés.

### Added

- `vmsOpenAcars.Tests/PirepStateTests.cs` — 13 tests de la clasificación de estado de
  PIREP, incluido el caso que motivó el fix (estado ilegible → no afirmar archivado).
  Suite total: **145 tests**.

---

## [0.9.4] — 2026-09-21

### Added

- **Proyecto de tests `vmsOpenAcars.Tests/` — 132 tests de `ScoringService`, todos en
  verde** (`vmsOpenAcars.Tests/ScoringServiceTests.cs`):

  El `CLAUDE.md` y el `CHANGELOG` de v0.8.0 documentaban desde entonces un proyecto
  `vmsOpenAcars.Tests/` con 66 tests MSTest que **nunca existió en el repositorio** —
  ni carpeta, ni `.csproj`, ni rastro en el historial de git. El motor de scoring, que
  es lo que decide la nota del piloto, llevaba 5 releases sin ninguna red de seguridad.

  La suite cubre los 17 criterios y sus umbrales con **un test por frontera, siempre en
  sus dos lados** (`≤150` penaliza 0 y `151` penaliza 5), porque es ahí donde un cambio
  de `<` por `<=` pasa desapercibido. Además:

  - Todos los tests parten de un vuelo perfecto (score 100, cero deducciones) y añaden
    **una sola** violación, de modo que la deducción observada es atribuible sin
    ambigüedad a ese criterio.
  - `AllCriteria_AreIndividuallyAttributable` exige **17 líneas de desglose únicas**: es
    el test de regresión del bug de contador compartido entre STD y QNH.
  - `Score_IsFlooredAtZero` lleva el vuelo a >100 pts de deducción bruta.
  - Regresión explícita del criterio omitido sin datos de aterrizaje.

  El proyecto referencia el `.exe` de la aplicación (el scoring vive en un WinExe) con un
  `ProjectReference` `ReferenceOutputAssembly=false` que solo fuerza el **orden** de
  compilación, más un `<Reference>` que sí copia el assembly para el runtime.

  Ejecución:
  ```
  msbuild vmsOpenAcars.sln /p:Configuration=Debug
  vstest.console.exe vmsOpenAcars.Tests\bin\Debug\vmsOpenAcars.Tests.dll
  ```

  En Release el proyecto de tests **no se compila** (la solución mapea su configuración
  Release a ActiveCfg Debug sin `Build.0`), para que no entre en el paquete distribuible.

### Fixed

- **Touch-and-go / stop-and-go dejaban la máquina de fases congelada en `TaxiIn`**
  (`FlightPhaseStateMachine`):

  Si el avión aterrizaba y deceleraba por debajo de 40 kt, la transición
  `AfterLanding → TaxiIn` disparaba antes de que el piloto aplicara potencia. A partir de
  ahí, el segundo vuelo era irrecuperable: `HandleAirPhases` **no tenía `case TaxiIn`**,
  así que la máquina se quedaba en `TaxiIn` indefinidamente y `OnBlock` nunca podía
  alcanzarse (exige GS<1 en tierra). El caso de touch-and-go solo era alcanzable desde
  `AfterLanding`, y el aterrizaje lento lo saltaba.

  Dos causas independientes:
  1. **`_wasOnGround` solo se actualizaba dentro del `case Takeoff`** (`if (... &&
     CurrentPhase == FlightPhase.Takeoff)`). Tras el aterrizaje quedaba fijado en `true`
     para siempre, de modo que `HandleAirPhases` no volvía a ejecutarse nunca más. Ahora
     se refresca de forma incondicional (salvo en el early-return del touchdown).
  2. **`TaxiIn` no manejaba el despegue.** El caso de touch-and-go se comparte ahora entre
     `AfterLanding` y `TaxiIn`, con la misma condición (GS>60 kt y ≥5 s desde el
     touchdown), de modo que ambos caminos de aterrizaje convergen.

  También se limpia `_takeoffRollStart` al detectar un nuevo aterrizaje y al pasar por
  `TaxiIn` con GS>60: los timestamps son absolutos y sobrevivían al aterrizaje, pudiendo
  disparar `TakeoffRoll` al primer ciclo del siguiente rodaje.

  **Nota:** sigue siendo un touch-and-go simplificado — el `ApproachBuffer` se limpia y la
  segunda aproximación se puntúa desde cero, pero la **primera** ya quedó registrada en el
  PIREP vía ACARS. Un touch-and-go de entrenamiento (varios ciclos) no está soportado como
  tal; lo que se corrige aquí es que el vuelo no quede bloqueado.

- **Un aterrizaje no capturado se puntuaba como "Butter" y enviaba `landing_rate = 0`**
  (`ScoringService`, `FlightScoreData`, `FlightManager`, `PirepBuilder`, `AcarsReporter`,
  `FlightRecord`, `LandingAnalysisForm`, `FlightHistoryForm`):

  `LandingRate` es un `int` no anulable y se rellenaba con `_td.Fpm ?? 0`. Es decir: si el
  touchdown nunca se detectaba, el criterio de mayor peso del sistema (**−40 pts**) se
  evaluaba como un aterrizaje perfecto de 0 fpm, la calificación cualitativa salía
  **"Butter"** y a phpVMS se le enviaba `landing_rate = 0` como si fuera un dato real.

  Se distingue "sin datos" de "0 fpm" con un centinela explícito
  (`ScoringService.NoLandingData = -1`) más el flag `FlightScoreData.LandingDataCaptured`,
  en lugar de cambiar el tipo a `int?`: ese valor fluye al esquema SQLite y al payload de
  phpVMS, donde un cambio de tipo forzaría una migración.

  - El criterio Landing Rate **se omite** cuando no hay datos (con red de seguridad: el
    flag *o* el centinela bastan, para que un constructor descuidado no devuelva un
    "Butter" gratis).
  - La calificación pasa a `"Unknown"` → `Score_Unknown` ("Sin datos de aterrizaje" /
    "No landing data").
  - `FlightRecord.DisplayLandingRate` muestra `—` en el LOGBOOK.
  - El centinela **nunca** llega a phpVMS: `PirepBuilder` lo normaliza a `0` en el payload.
  - Un 0 fpm real sigue puntuando como Butter (test de regresión).

- **`App.config` deja de estar trackeado en git** (`.gitignore` ya lo declaraba):

  La regla `App.config` del `.gitignore` no tenía efecto porque el archivo estaba
  añadido al índice: una regla de ignore no destrackea un archivo ya seguido. El archivo
  local se conserva intacto (es la configuración de desarrollo), pero `git rm --cached`
  impide que vuelva a subirse por accidente. Ver la nota de credenciales en `CLAUDE.md`.

### Documented

- `CLAUDE.md`: eliminada la referencia al proyecto de tests inexistente, que ahora sí
  existe y se describe con su comando de ejecución.
- `Docs/architecture.md`: nueva sección "Tests" en las notas de build.

---

## [0.9.5] — 2026-09-21

### Fixed

- **Carreras de datos reales entre el hilo de polling y las tareas de NavData**
  (`TelemetryCoordinator`, `TouchdownState`, `FlightManager`):

  Tres estructuras se escribían desde `Task.Run` mientras el hilo de polling (20 Hz) las
  leía y modificaba en paralelo. Ninguna estaba sincronizada.

  1. **`ApproachBuffer` era un `List<T>` desnudo.** `Add` se hacía en el hilo de polling
     (`ProcessRawData`) mientras `ReconfirmApproachRunway` le hacía `Clear()` desde un
     `Task.Run` (cambio de pista o desvío), y `SaveLandingRecord` lo enumeraba. La
     mutación concurrente de `List<T>` es la clase de fallo que corrompe el estado
     interno del array y puede lanzar `IndexOutOfRangeException` dentro de `List.Add`.
     Ahora está encapsulado tras un lock con `AddApproachPoint`/`ClearApproachBuffer`/
     `ApproachBufferCount`/`SnapshotApproachBuffer`; `AcarsReporter` trabaja sobre una
     **instantánea**, no sobre la lista viva.

  2. **`TouchdownState.SetRunwayData` hacía tres escrituras independientes**
     (`DistanceFt`, `CenterlineDeviationFt`, `RunwayName`) desde `Task.Run`, mientras
     `BuildScoreData` y `SnapshotLandingRecord` las leen. Un lector podía observar un
     conjunto a medio actualizar — nombre de pista nuevo con la distancia anterior — que
     es exactamente lo que decide la penalización de Touchdown Zone. Las tres se publican
     ahora como una única referencia inmutable (`RunwayGeometry`) vía `volatile`, de modo
     que el lector ve el conjunto anterior completo o el nuevo completo.

  3. **`_arrivalAirportElevation` era `double?`** (struct de 16 bytes: escritura y lectura
     no atómicas) publicado desde `Task.Run` y leído en cada ciclo de telemetría por
     `ReferenceAirportElevation` y `BuildPhaseInput()`. Pasa a almacenarse como su patrón
     de bits en un `long` y accederse con `Interlocked`, con `double.NaN` como "sin dato".
     La API pública (`ArrivalAirportElevationFt`) sigue siendo `double?`, así que los
     consumidores no cambian.

     > Nota: C# no permite `volatile` sobre `double` ni sobre `long` (solo tipos de hasta
     > 32 bits), que es la razón del patrón de bits en lugar de un campo `volatile`.

  `_effectiveDestination` y `_divertedAirport` pasan a `volatile` (referencias: la
  escritura ya era atómica, pero faltaba la visibilidad entre hilos).

### Changed

- **`IsApproachStabilized` duplicaba la lógica de velocidad del gate de scoring con otro
  valor** (`FlightManager`): el indicador STABLE/UNSTABLE de la UI usaba una ventana fija
  de **100–160 kt**, mientras el gate de 1 000 ft que puntúa usa
  `AircraftPerformanceTable.GetApproachSpeedRange(icao)` (65–100 en Cat A, 140–185 en
  Cat D). El piloto podía ver "✅ STABLE" en una aeronave ligera que el gate habría
  penalizado, o "⚠️ UNSTABLE" en un wide-body perfectamente estable. Ahora ambos leen la
  misma fuente; era el último sitio con un umbral de velocidad hardcodeado.

- **Debounce de luces: aclarado que las dos capas no son redundantes** (`FlightManager`):
  el informe de auditoría lo señaló como "doble debounce con latencia ~4.5 s", pero el
  análisis del flujo real muestra que son **dos cadenas paralelas sobre el mismo dato
  crudo**, no serie: `FsuipcService` (hold 2.5 s) debouncea solo la *emisión* de sus
  eventos `*LightChanged` (log y anuncios de cabina), mientras `FlightManager` debouncea
  el *estado* que consumen las penalizaciones. Los flags de `RawTelemetryData` son crudos
  —`FsuipcService` nunca los filtra— así que sin la segunda capa el scoring evaluaría el
  bitfield sin debouncear. **No se ha cambiado la lógica**; se alineó el umbral a 2.5 s
  (mismo flicker absorbido en ambas capas) y se documentó el porqué de cada una.

### Documented

- `CLAUDE.md` / `Docs/architecture.md`: notas de concurrencia en los puntos de captura de
  aproximación y en el estado de touchdown.

---

## [0.9.3] — 2026-09-21

### Security

- **Credenciales reales de producción comiteadas en el repositorio** (`App.config`,
  `App.Release.config`, `Helpers/AppConfig.cs`):

  El repositorio contenía la API key de phpVMS de producción
  (`vms_api_key`), la del servicio NavData (`navdata_api_key`, presente **dos veces**:
  en `App.config` y como valor por defecto en `AppConfig.cs`) y el usuario personal de
  SimBrief.

  La separación correcta es **dev vs. distribución**, no "vaciar todo":
  - **`App.Release.config`** (plantilla publicable) pasa a ser neutra: placeholders en
    lugar de la URL de la aerolínea, la API key de NavData, el nombre de aerolínea y la
    ruta de la BD del LOGBOOK.
  - **`App.config`** (configuración local del desarrollador, no se distribuye) conserva
    las claves del entorno propio, para no tener que reconfigurar la app en cada
    compilación. Ahora lleva un aviso explícito de ese rol.
  - **`Helpers/AppConfig.cs`** es donde estaba el problema real: aunque un despliegue
    limpiara su `.config`, el código seguía inyectando la clave de NavData como default.
    `NavDataApiUrl`/`NavDataApiKey` ya no tienen valor por defecto.
  - `navdata_api_domain` se deja vacío en `App.config` a propósito, para que
    `X-Origin-Domain` siga derivándose de `vms_api_url` (comportamiento idéntico al
    anterior a este cambio).

  > **Acción requerida:** las claves de NavData y phpVMS estuvieron comiteadas, así que
  > **deben rotarse** en sus respectivos servicios — eliminarlas de la plantilla de
  > distribución no las invalida.

- **La API key de phpVMS se enviaba a simbrief.com** (`IApiService`, `ApiService`,
  `SimbriefEnhancedService`, `MainViewModel`):

  `IApiService` exponía el `HttpClient` autenticado de phpVMS
  (`x-api-key` en `DefaultRequestHeaders`) y se reutilizaba para peticiones a terceros:
  el fetch del OFP a `simbrief.com` y la descarga del PDF del OFP. En cada vuelo, la
  credencial del piloto viajaba a un host externo.

  **Fix en tres partes:**
  1. Nuevo cliente dedicado `HttpClientProvider.Simbrief` — sin cabeceras de phpVMS.
     `SimbriefEnhancedService` y `DownloadOFPPdfAsync` lo usan.
  2. `HttpClient` eliminado de `IApiService` (permanece en `ApiService` con un comentario
     advirtiendo que es solo para el mismo host). `SimbriefEnhancedService` ya no recibe
     `IApiService` en su constructor: no puede alcanzar el cliente autenticado.
  3. Las tres llamadas que `PhpVmsFlightService` hacía con el cliente crudo
     (`api/flights`, `api/fleet`, `POST api/user/bids`) se encapsulan en
     `IApiService.GetAsync`/`PostJsonAsync`.

- **Credenciales enviadas sin escapar** — el usuario de SimBrief se interpolaba en la
  query string sin codificar; ahora usa `Uri.EscapeDataString`.

### Fixed

- **`GetStatusCode` y `FromPirepStatus` no compartían vocabulario — fase errónea al
  reanudar un PIREP** (`Helpers/FlightPhaseHelper.cs`):

  `GetStatusCode` emite `PBT/TOF/ICL/APR/LAN`, pero `FromPirepStatus` buscaba
  `PBK/TKF/CLB/DSC/LND`: ninguno de los códigos que este cliente envía era reconocido.
  Al reanudar un vuelo la fase restaurada caía al `default` (Enroute). Peor: `"APR"` — que
  esta app usa para **Descent** — se mapeaba a **Approach**, y `"FIN"` (Approach) no se
  reconocía en absoluto.

  `FromPirepStatus` ahora invierte `PhaseToStatusCode` para resolver primero el
  vocabulario propio, y solo después aplica un diccionario de alias de otras fuentes
  ACARS. Ambos sentidos ya **no pueden desincronizarse** al añadir una fase.

- **El intervalo de reporte por fase era código muerto** (`FsuipcService`,
  `MainViewModel`):

  `FsuipcService.SetUpdateIntervalForPhase()` no se llamaba desde ningún sitio, así que
  `_currentPhaseInterval` se quedaba en su valor inicial (30 s) durante todo el vuelo y
  las seis claves `update_interval_*` de `App.config` no tenían ningún efecto: el reporte
  de posición a phpVMS salía a una cadencia fija. Ahora se actualiza en
  `OnFlightPhaseChanged`, restaurando la tabla documentada (5 s en despegue/llegada a
  30 s en crucero).

  > Este cambio **sí altera el volumen de tráfico** hacia phpVMS respecto a v0.9.2
  > (más posiciones en despegue, aproximación y rodaje; menos en crucero). No afecta al
  > bucle de telemetría ni a la máquina de fases, que corren a ritmo completo vía
  > `RawDataUpdated`. Conviene validarlo en vuelo.

- **La trayectoria de aproximación se descartaba aunque el guardado fallara**
  (`ViewModels/AcarsReporter.cs`):

  `ApproachBuffer.Clear()` estaba fuera del `if (newId > 0)`, de modo que un fallo de
  `SaveFlight` (que devuelve −1) logueaba el error y aun así borraba el buffer — la única
  copia de los puntos de aproximación. Ahora solo se limpia si el registro se persistió.

- **El check de presión estándar consumía el tope del QNH** (`ApproachValidator`,
  `ScoringService`, `FlightScoreData`, `PirepBuilder`):

  `CheckStdPressure` (aplicar 1013 al cruzar la altitud de transición en subida)
  incrementaba `QnhViolations`, el mismo contador que los checks de QNH de salida y
  llegada, sujeto a un tope compartido de 10 pts. Un STD incorrecto podía agotar el tope
  y **enmascarar** la penalización del QNH de llegada — que es precisamente el check con
  más consecuencias del sistema. Pasa a ser un criterio propio
  (`StdPressureViolation`, −5 pts), independiente y visible en el desglose del score.

- **`NullReferenceException` al arrancar sin configuración** (`UI/Forms/MainForm.cs`):

  Si `vms_api_url`/`vms_api_key` estaban vacíos, `_viewModel` quedaba `null` y
  `InitializeViewModel()` lo desreferenciaba **fuera** del `try`, abortando el arranque
  con una excepción sin mensaje útil. Ahora muestra un aviso explicando qué configurar y
  retorna limpiamente.

- **`navdata_api_domain` documentada pero ignorada** (`Helpers/AppConfig.cs`):

  `CLAUDE.md` y `docs` describían la clave `navdata_api_domain`, pero `NavDataApiDomain`
  siempre derivaba el host de `vms_api_url`, ignorando la clave. Ahora respeta el valor
  explícito si está configurado y solo deriva del host como fallback.

### Documented

- Corregido el conteo de criterios de scoring: son **17** (no 14). El criterio
  **Engine Stabilization** (−5 pts) no aparecía en ninguna documentación pese a existir
  en `ScoringService` con entrada propia en `PirepBuilder`. El comentario XML de la clase
  afirmaba una suma máxima de 120 pts listando solo 8 criterios: corregido.

---

## [0.9.2] — 2026-09-20

### Fixed

- **Falso "TAKEOFF ROLL" durante un pico breve de velocidad en taxi — penalizaba strobe/landing
  lights y QNH de salida sin haber estado en pista** (`FlightPhaseStateMachine`, `FlightManager`,
  `ApproachValidator`, `NavDataService`):

  PIREP real en curso (`E7DK47e88XdabzoL`, SKRG rwy01→SKBQ) reportado por el usuario: rodando por
  la calle A (paralela a la pista 01/19), la velocidad en tierra cruzó momentáneamente 30 kt y el
  sistema interpretó eso como el inicio del despegue. La fase revirtió a `TaxiOut` dos segundos
  después, pero para entonces ya se habían aplicado, de forma irreversible, tres penalizaciones
  de -5 pts (landing lights apagadas, strobe apagada, QNH de salida incorrecto) pensadas para un
  despegue real. El log también mostró `🛫️ PISTA 19 | ALINEACIÓN: 6325 ft desde umbral | CL: 614
  ft de desviación` — geométricamente imposible para una pista real, confirmando que el sistema
  "detectó" la pista sobre la calle paralela, no sobre el asfalto.

  Causa: `FlightPhaseStateMachine.cs`'s transición `TaxiOut → TakeoffRoll` (`GS>30 kt && Pitch<1.0°`)
  era la única de toda la máquina de fases sin debounce — disparaba con una sola muestra de
  telemetría. En cuanto disparaba, `FlightManager.CheckProcedureAtPhaseEntry` evaluaba de forma
  irreversible ("una sola vez por transición de fase") las luces de despegue
  (`ApproachValidator.CheckPhaseEntryLights`) y el QNH de salida (`CheckQnhAsync`). Además,
  `NavDataService.ProjectOnRunway` (usada por `FindTakeoffRunway`/`FindTouchdownRunway`)
  seleccionaba la pista más cercana solo por rumbo (45° de tolerancia, sin validar posición) y no
  tenía fallback a `null` cuando ninguna pista pasaba el chequeo de footprint — devolvía el mejor
  candidato por rumbo igual, geométricamente inválido.

  **Fix, tres partes:** (1) `TaxiOut → TakeoffRoll` ahora exige `GS>30 kt` sostenido 5 s
  (`_takeoffRollStart`/`TakeoffRollConfirmSec`), mismo patrón "temporizador pendiente" que las
  demás transiciones de esta máquina de estados. (2) `ProjectOnRunway` retorna `null` si ninguna
  pista pasa `WithinFootprint` tras la desambiguación de paralelas — mismo criterio que ya usaba
  `GetRunwayThreshold`. (3) Defensa en profundidad: nuevos guards
  `ApproachValidator._departureQnhChecked`/`_takeoffLightsChecked` (reset en `Reset()`) evitan que
  un rebote de fase repetido, o un rechazo de despegue real seguido de un segundo intento,
  penalice dos veces — ninguno de los dos checks tenía guard de "ya evaluado este vuelo" (a
  diferencia del STD de climb o el QNH de llegada).

---

## [0.9.1] — 2026-09-19

### Fixed

- **Falsos positivos de desvío durante giros de STAR — nunca se revertían** (`TelemetryCoordinator`,
  `FlightManager`, `NavDataService`/`INavDataService`, `Db/RunwayService`):

  Vuelo real (`9QJYgErnggMdgKPO`, SKLT→SKBO) reveló que `ReconfirmApproachRunway` podía marcar
  como "desvío" un aeropuerto que el avión solo cruzaba de pasada durante un giro de STAR — el
  log mostró `⚠️ DESVÍO DETECTADO — aproximando a SKGY` (18:27:44) seguido de `⚠️ DESVÍO
  DETECTADO — aproximando a SKMA` (18:33:31), ambos falsos positivos a decenas de NM de SKBO. El
  tracking local se autocorrigió casi de inmediato (`↻ RUNWAY UPDATED — 14L (SKBO)` desde las
  18:35:10, sostenido ~4 min, sobreviviendo un touch-and-go y una segunda aproximación completa),
  pero el PIREP se fileó igual con `arr_airport_id` corregido a SKMA — el falso positivo nunca se
  revirtió pese a que el destino planeado sí se reconfirmó reiteradamente antes del touchdown.

  Confirmado en vivo contra NavData con las coordenadas exactas del log: `GET
  /nearest/approach-airport/` matcheó SKGY con `heading_diff_deg: 3.1` (coincidencia casual de
  rumbo durante el giro) pero `cross_track_nm: 11.36` — el avión estaba a más de 11 NM del eje
  extendido de esa pista, nada parecido a una aproximación real.

  **Causa 1 — sin filtro de plausibilidad geométrica:** el endpoint ya devuelve `cross_track_nm`
  en la respuesta, pero `NearestApproachAirportResult` lo descartaba. **Causa 2 — sin
  reconciliación:** `SetEffectiveDestination`/`SetDivertedAirport`/`SetArrivalAirportElevation`
  solo se reseteaban en `ResetFlightState()`/`ResumeFlight()` (inicio/fin de vuelo) — cuando un
  poll posterior volvía a confirmar el destino planeado, solo se actualizaban las variables
  locales de tracking (`_approachThreshold`/`_approachDestination`), nunca los flags de
  `FlightManager`.

  **Fix:** (1) `cross_track_nm` mapeado a `NearestApproachAirportResult.CrossTrackNm`;
  `ReconfirmApproachRunway` exige `≤ 3.0 NM` (o ausente) antes de aceptar un match como desvío
  genuino. (2) Nuevo `FlightManager.ClearDivertedAirport()`, llamado en cuanto un poll
  no-desviado reconfirma el destino planeado — revierte los tres campos a `null`
  (`EffectiveDestination ?? DestIcao` ya maneja ese fallback en `ApproachValidator`/`FilePirep()`).
  Nuevo log `Lnm_DiversionReverted` cuando hay una reversión real.

  Evaluado y descartado del alcance: filtro de tamaño de pista (`length_ft`/`width_ft` —
  propuesto con SKMA 70 ft / SKGY 66 ft de ancho como evidencia de que un A320 no cabría; el
  endpoint no expone esos campos y el filtro de `cross_track_nm` ya cubre el caso real sin ese
  costo) y conciencia de fixpoints de STAR (mismo motivo).

  Claves de log nuevas: `Lnm_DiversionReverted` (`Languages/en.json` + `es.json`).

---

## [0.9.0] — 2026-09-19

### Fixed

- **La elevación de referencia (AGL) no se actualizaba al confirmar un desvío — landing log vacío,
  gate de Stabilized Approach sin disparar** (`FlightManager`, `FlightManager.Telemetry`,
  `TelemetryCoordinator`, `NavDataService`/`INavDataService`):

  Confirmado en un vuelo real de prueba tras v0.8.10 (SKRG→SKCL, aterrizó en SKBO): el log mostró
  `⚠️ Landing log no grabado: solo 0 puntos en buffer (mínimo 3)`. Causa: `FlightManager.
  ReferenceAirportElevation` seguía devolviendo siempre `_activePlan.DestinationElevation` (SKCL,
  3162 ft) para todas las fases de llegada, **incluso después de que `SetEffectiveDestination`/
  `SetDivertedAirport` ya habían confirmado el desvío a SKBO (8361 ft)**. Con ~5200 ft de
  diferencia, el AGL calculado (`CurrentAGL`) nunca bajaba de 3000 ft ni en el touchdown real —
  el gate de captura del buffer de aproximación (`computedAgl < 3000` en `TelemetryCoordinator`)
  nunca se cumplía. Evidencia adicional en un log de una sesión anterior:
  `"Luces LANDING ENCENDIDAS (13843 ft AGL)"` con altitud real 16977 ft —
  `16977 − 3162 (SKCL) ≈ 13815`, coincide casi exacto con el AGL mal calculado.

  Había **tres** lugares independientes con el mismo patrón (ninguno consultaba el desvío
  confirmado):
  - `FlightManager.ReferenceAirportElevation` → `CurrentAGL` → gate de Stabilized Approach,
    `IsApproachStabilized`, `computedAgl` en `TelemetryCoordinator` (polling + captura de buffer),
    mensajes de log/OSD "(N ft AGL)".
  - `FlightManager.Telemetry.cs::BuildPhaseInput().DestinationElevation` → `FlightPhaseStateMachine`'s
    `altAboveDest` → la transición `Descent → Approach` (misma causa raíz documentada en v0.8.10
    como "por qué Approach puede no alcanzarse nunca" — corregir esto la resuelve de rebote).
  - `Helpers/FlightPhaseHelper.cs::GetTerrainElevation` → `TelemetryCoordinator.PrepareTelemetry`'s
    `altitude_agl` enviado a phpVMS en cada posición ACARS.

  **Fix:** nuevo campo cacheado `FlightManager._arrivalAirportElevation` (+ `SetArrivalAirportElevation`/
  `ArrivalAirportElevationFt`), seteado en los mismos dos call sites donde ya se confirma el desvío
  (`TelemetryCoordinator.ReconfirmApproachRunway`, `LookupRunwayData`) vía el nuevo
  `NavDataService.GetAirportElevationFt(icao)` (usa `NavDataClient.GetAirportInfo(icao)?.ElevationFt`,
  síncrono, carga on-demand — mismo patrón que `GetRunwayThreshold`). `ReferenceAirportElevation` y
  `BuildPhaseInput()` ahora hacen `_arrivalAirportElevation ?? <elevación planeada>`; `PrepareTelemetry`
  usa el mismo override para `altitude_agl`. Si la consulta a NavData falla, se mantiene el valor
  anterior (nunca cae a `0`, que sería peor).

  **Efecto colateral positivo:** al corregir `BuildPhaseInput().DestinationElevation`, la transición
  `Descent → Approach` de `FlightPhaseStateMachine` ahora también funciona correctamente para desvíos
  con diferencia de elevación grande — esto resuelve la limitación documentada en v0.8.10
  ("Stabilized Approach nunca se evalúa en desvíos severos"); ese criterio (hasta 15 pts) ahora puede
  evaluarse normalmente una vez el desvío se detecta a tiempo.

  **Archivos modificados:**
  - `Services/Interfaces/INavDataService.cs` / `Services/NavDataService.cs` — nuevo
    `GetAirportElevationFt(icao)`
  - `Core/Flight/FlightManager.cs` — nuevo campo `_arrivalAirportElevation`, `SetArrivalAirportElevation`,
    `ArrivalAirportElevationFt`; `ReferenceAirportElevation` actualizado
  - `Core/Flight/FlightManager.Telemetry.cs` — `BuildPhaseInput().DestinationElevation` actualizado
  - `Core/Flight/FlightManager.Lifecycle.cs` — reset del nuevo campo en `ResetFlightState()`/`ResumeFlight()`
  - `ViewModels/TelemetryCoordinator.cs` — fetch+set de elevación en los 2 call sites de desvío;
    override en `PrepareTelemetry`

- **`MovePilotAsync` roto — confirmado 405 en producción** (`FlightManager.Lifecycle`, `ApiService`):

  Confirmado en el mismo vuelo real: `❌ Error al reubicar al piloto en el aeropuerto real de
  llegada: Move pilot failed: {"...":"The PUT method is not supported for route api/user.
  Supported methods: GET, HEAD."...}`. A pesar del error, phpVMS dejó correctamente al piloto y
  al avión en SKBO — evidencia de que phpVMS ya reubica al piloto automáticamente al procesar el
  campo `diversion-airport` del PIREP. Se retiró la llamada rota de `FilePirep()` (mismo criterio
  que `GetNearestAirport` en v0.8.10: el método permanece en `ApiService.cs`, documentado como
  roto vía comentario, sin borrarse). El bloque `UpdatePirep(arr_airport_id)` justo antes no se
  toca — sigue siendo necesario y funciona.

  **Archivos modificados:**
  - `Core/Flight/FlightManager.Lifecycle.cs` — retiro de la llamada a `MovePilotAsync` en `FilePirep()`
  - `Services/ApiService.cs` — comentario documentando el 405 confirmado

---

## [0.8.10] — 2026-09-18

### Fixed

- **Desvío severo (aeropuerto real con elevación muy distinta a la planeada) nunca detectado
  — QNH penalizado sin posibilidad de revisión, pista/ILS nunca resueltos, PIREP dejaba al
  piloto y la aeronave en el destino planeado** (`TelemetryCoordinator`, `ApproachValidator`,
  `FlightManager.Lifecycle`, `ApiService`):

  Reportado con un vuelo real SKRG→SKCL que en realidad voló y aterrizó en SKBO (nunca cerca
  de SKCL ni del alterno filed SKPE). El log ACARS real mostró el status saltando de `APR`
  (código de `Descent`) directo a `LDG`, **sin pasar nunca por `FIN`** (código de `Approach`):
  la fase interna `FlightPhase.Approach` nunca se alcanzó. Causa raíz:
  `FlightManager.Telemetry.cs` hardcodea `DistanceToDestinationNm = -1` siempre, dejando
  muerta la rama de distancia en la transición `Descent → Approach`; solo puede disparar la
  rama de altitud, calculada contra la **elevación del destino planeado** (SKCL, 3162 ft).
  Como SKBO está ~5200 ft más alto, esa condición nunca se cumplió — ni en el touchdown.

  Como todo el mecanismo de detección de desvío (`ReconfirmApproachRunway`, endpoint NavData
  `nearest/approach-airport`) vivía gateado a `FlightPhase.Approach`, nunca corrió para este
  vuelo. El fallback de último recurso en touchdown (`LookupRunwayData` → `IApiService.
  GetNearestAirport`, `GET /api/airports/nearest`) tampoco salvó el caso: probado en vivo
  contra producción, devuelve `404 "No query results for model [App\Models\Airport] NEAREST"`
  — esa ruta no existe en esta instalación de phpVMS, y el fallo se tragaba en un `catch {}`
  silencioso.

  **Fix en tres partes:**

  1. **Detección también en `FlightPhase.Descent`** (`TelemetryCoordinator.ProcessRawData`):
     el gate de `ReconfirmApproachRunway`/captura del buffer de aproximación ahora corre en
     `Descent || Approach`, no solo `Approach`. `OnPhaseChanged` trata ambas fases como un
     superestado único — el reset de `_approachThreshold`/`_approachDestination` solo ocurre
     al entrar al par desde afuera (`Climb → Descent`), no en la transición interna
     `Descent → Approach`, así lo ya resuelto durante Descent sobrevive esa transición.
  2. **Retiro del fallback roto de `GetNearestAirport`** en `LookupRunwayData` (confirmado
     404 en producción) — `LookupRunwayData` vuelve a ser síncrono (`void`). El método sigue
     existiendo en `ApiService.cs` (documentado como roto vía comentario) porque
     `FlightManager.DetectNearestAirport` todavía depende de él — problema separado, no
     corregido aquí.
  3. **QNH de llegada: provisional durante el vuelo, confirmado/revertido al filear**
     (estilo "comisarios de F1", decisión del maintainer): el check de QNH de llegada puede
     disparar antes de que el desvío se detecte, incluso con el fix de la Parte 1 (a mucha
     distancia/altitud todavía). Antes penalizaba de forma inmediata e irreversible contra
     `EffectiveDestination ?? DestIcao` en ese momento. Ahora:
     - `ApproachValidator.CheckArrivalQnhProvisionalAsync` reemplaza a `CheckQnhAsync` en los
       dos call sites de llegada (gate TL−1000 ft y fallback 1000 ft AGL) — loguea/OSD en
       tiempo real pero **no toca `QnhViolations`**, solo guarda un veredicto provisional.
     - `ApproachValidator.FinalizeArrivalQnhAsync` — único punto que suma al contador para el
       componente de llegada. Llamado una sola vez, **awaited**, desde
       `FlightManager.Lifecycle.FilePirep()` antes de `BuildScoreData()`/`ComputeScore()`,
       usando el destino **final** conocido y el QNH **actual** del avión (no el capturado en
       el check temprano). Sin METAR definitivo, nunca puntúa en ningún sentido.
     - Los checks de salida (vs METAR de origen) y de clima (vs STD 1013) no cambiaron —
       nunca son ambiguos, siguen siendo inmediatos y definitivos.

  **Archivos modificados:**
  - `ViewModels/TelemetryCoordinator.cs` — gate de `ProcessRawData` extendido a Descent;
    `OnPhaseChanged` con superestado `{Descent, Approach}`; `LookupRunwayData` vuelve a
    `void`, retirado el fallback a `GetNearestAirport`; retirada la dependencia `IApiService`
    ya sin uso (constructor + call site en `MainViewModel.cs`)
  - `Core/Flight/ApproachValidator.cs` — nuevos campos `_arrivalQnhFinalized`,
    `_provisionalArrivalQnhViolation`; nuevos métodos `CheckArrivalQnhProvisionalAsync`,
    `FinalizeArrivalQnhAsync`; call sites C/D migrados
  - `Core/Flight/FlightManager.Lifecycle.cs` — `FilePirep()` reordenado: `arrivalIcao` se
    calcula antes del scoring y se awaita `FinalizeArrivalQnhAsync` antes de
    `BuildScoreData()`
  - `Services/ApiService.cs` — comentario documentando `GetNearestAirport` como roto
    (404 confirmado en producción)
  - `Languages/en.json` / `es.json` — nuevas claves `Log_QnhPenaltyProvisional`,
    `Log_QnhFinalPenalty`, `Log_QnhPenaltyReversed`, `Log_QnhFinalIndeterminate`

---

## [0.8.9] — 2026-09-18

### Added

- **Detección de aeropuerto de aproximación vía NavData — reemplaza el fallback dest→alt→origin
  y resuelve pistas paralelas** (`NavDataClient`, `NavDataService`, `TelemetryCoordinator`,
  `PirepBuilder`, `FlightManager`):

  phpVMS fue corregido del lado del backend para dejar de inferir diversiones desde
  `alt_airport_id` (poblado siempre por SimBrief, aterrice o no en el alterno) y solo procesar
  una diversión real cuando el pirep incluye el campo `diversion-airport`. Aprovechando el
  cambio, NavData expuso un endpoint dedicado (`GET /nearest/approach-airport/`) que resuelve
  aeropuerto+pista por posición/heading en una sola llamada, con desambiguación de **pistas
  paralelas** por `score` (dominado por `cross_track_nm`, la desviación lateral al eje de cada
  pista — validado por NavData para SKBO 14L/14R, ~355 m de separación, con margen de
  crosswind >20° antes de fallar el desempate).

  **Reemplaza** la cadena dest→alt→origin agregada en v0.8.8 (`TelemetryCoordinator.ProcessRawData`)
  por una **re-confirmación continua** durante toda la fase Approach (throttled a 5 s, activa
  mientras AGL > 1000 ft) — no solo una resolución única: pistas paralelas con un fix inicial
  compartido (ej. SKBO 14L/14R vía AMVES) son geométricamente ambiguas justo en ese fix, y el
  match solo se vuelve confiable cuando el avión diverge hacia el curso final específico. Si el
  resultado cambia respecto al ya confirmado:
  - **Diversión** (ICAO ≠ destino planeado): se marca de inmediato vía
    `FlightManager.SetEffectiveDestination` + nuevo `SetDivertedAirport`, sin esperar a
    resolver la geometría completa del umbral — los gates de QNH/Localizer necesitan esto lo
    antes posible.
  - **Corrección de pista paralela sin diversión** (mismo aeropuerto, pista distinta): limpia
    `ApproachBuffer` (los puntos previos se calcularon contra el umbral equivocado) y relanza
    `LoadApproachData` para recargar el ILS/approach de la pista correcta.

  El fallback dest→alt→origin→`GetNearestAirport` en `LookupRunwayData` (touchdown, v0.8.8) se
  mantiene como red de seguridad si la capa de Approach no llegó a resolver nada.

  **`diversion-airport` en el payload del PIREP** — `PirepBuilder.BuildPayload` se reescribió
  como `Dictionary<string,object>` (antes objeto anónimo) para poder omitir la clave por
  completo cuando no hay desvío, en vez de enviarla como `null` — phpVMS solo procesa una
  diversión si el pirep **incluye** el campo. Se agrega solo si `FlightManager.DivertedAirport`
  no es null.

  **Decisión de diseño:** vmsOpenAcars sigue siendo responsable de garantizar
  `arr_airport_id`/`curr_airport_id` (vía `UpdatePirep`/`MovePilotAsync`, agregados en v0.8.8)
  independientemente de lo que phpVMS procese internamente al recibir `diversion-airport` — no
  se asume ni se depende de eso.

  **Archivos modificados:**
  - `Models/NavData.cs` — `NavApproachAirportResponse`, `NavApproachAirportRunway`
  - `Db/RunwayService.cs` — `NearestApproachAirportResult`
  - `Services/NavDataClient.cs` — `GetNearestApproachAirportAsync`
  - `Services/NavDataService.cs` / `Services/Interfaces/INavDataService.cs` — `FindApproachAirport`
  - `Core/Flight/FlightManager.cs` — `_divertedAirport`, `SetDivertedAirport`, `DivertedAirport`;
    reset en `ResetFlightState`/`ResumeFlight`
  - `Core/Flight/PirepBuilder.cs` — `BuildPayload` ahora `Dictionary<string,object>`;
    `PirepPayloadArgs.DiversionAirport`
  - `Core/Flight/FlightManager.Lifecycle.cs` — pasa `DiversionAirport` a `PirepPayloadArgs`
  - `ViewModels/TelemetryCoordinator.cs` — reemplaza la cadena dest→alt→origin por
    `ReconfirmApproachRunway` (throttled, re-entrante-safe vía `_reconfirmingApproachAirport`)
  - `Languages/en.json` / `es.json` — nueva clave `Lnm_DiversionDetected`

---

## [0.8.8] — 2026-09-17

### Fixed

- **PIREP fileado con `arr_airport_id` incorrecto cuando el aterrizaje real ocurre en un
  aeropuerto distinto al destino planeado** (`TelemetryCoordinator`, `FlightManager.Lifecycle`,
  `PirepBuilder`, `ApproachValidator`):

  Reportado con un vuelo real SKCL→SKBO con alterno SKRG donde el avión en realidad nunca
  llegó a SKBO (heading de touchdown ~020°, coincidente con SKCL rwy 02, no con ninguna pista
  de SKBO). El sistema ya detectaba el mismatch — logueaba
  `⚠️ NavMap: runway not found for SKBO (heading 8°)` — pero no actuaba sobre él: el PIREP se
  fileaba igual con `arr_airport_id = SKBO`, dejando al piloto y a la aeronave "parados" en el
  aeropuerto equivocado en phpVMS. Un segundo caso (emergencia SKBO→SKCG con alterno SKBQ y
  regreso a SKBO) reveló que además el criterio **QNH Compliance** en el gate de aproximación
  seguía validando contra el METAR del destino planeado (SKCG) en vez del aeropuerto realmente
  aproximado (SKBO), porque el `EffectiveDestination` que usa `ApproachValidator` para los
  checks de QNH/Localizer/Minimums nunca se actualizaba con el aeropuerto de salida.

  **Detección en dos capas, ambas cruzando contra el aeropuerto de salida** (su NavData
  siempre está pre-cargado desde el inicio del vuelo):

  1. **Durante Approach** (`TelemetryCoordinator.ProcessRawData`): si `GetRunwayThreshold` no
     matchea ni destino ni alterno planeados, prueba `plan.Origin`. Si matchea, fija
     `EffectiveDestination` **antes** de que se disparen los gates de QNH (TL−1000 ft y
     1000 ft AGL), corrigiendo el METAR usado en tiempo real.
     **Superseded en v0.8.9** por `NavDataService.FindApproachAirport` (endpoint dedicado de
     NavData) — ver esa entrada.
  2. **En touchdown** (`TelemetryCoordinator.LookupRunwayData`): mismo cross-reference como
     red de seguridad si la capa de Approach no llegó a resolver el threshold. Añade además
     `CheckFlownDistance` — heurística secundaria: si la distancia volada es <60% de la
     planeada, loguea aviso de revisión (`Lnm_DistanceMismatch`) independientemente de si
     hubo match de pista.

  **Fallback genérico a cualquier aeropuerto** (`LookupRunwayData`): si destino, alterno
  y origen fallan los tres, consulta `IApiService.GetNearestAirport(lat, lon)`
  (`GET /api/airports/nearest` de phpVMS, ya existente pero solo usado antes para
  validación pre-vuelo) para resolver el ICAO más cercano a la posición real, y reintenta
  `FindTouchdownRunway` contra ese ICAO. `NavDataService`/`NavDataClient` cargan cualquier
  aeropuerto on-demand (no están limitados a los pre-cargados), así que esto generaliza la
  detección a un desvío a **cualquier** aeropuerto, no solo destino/alterno/origen.

  **Corrección del PIREP** — `FlightManager.Lifecycle.FilePirep()`: si `_effectiveDestination`
  difiere del destino planeado, llama a `_apiService.UpdatePirep(id, { arr_airport_id })`
  antes de filear (mismo mecanismo `PUT` ya usado para `block_off_time`/status).
  `PirepBuilder.BuildPayload` también incluye `arr_airport_id` en el payload de `/file` como
  refuerzo. Validado contra producción: el `PUT` es aceptado mientras el PIREP está
  `state=0/in_progress` (el momento exacto en que se llama); un PIREP ya `Accepted` lo
  rechaza con `503`, lo cual no afecta el flujo normal porque la corrección siempre ocurre
  antes de filear.

  **Corrección de la posición del piloto** — mismo punto en `FilePirep()`: llama a
  `_apiService.MovePilotAsync(arrivalIcao)` (`PUT /api/user { curr_airport_id }`). Este
  método ya existía en `ApiService`/`IApiService` (implementado, documentado, nunca
  invocado desde ningún flujo) — sin él, `user.curr_airport` en phpVMS quedaba "parado" en
  el destino planeado nunca alcanzado aunque el `arr_airport_id` del PIREP ya estuviera
  corregido.

  **Archivos modificados:**
  - `ViewModels/TelemetryCoordinator.cs` — fallback a `plan.Origin` y luego a
    `GetNearestAirport` en la detección de threshold de Approach y en `LookupRunwayData`
    (ahora `async Task`); nuevo método `CheckFlownDistance`; el lookup de parking en
    `OnBlock` ahora prefiere `_approachDestination`; nueva dependencia `IApiService`
  - `Core/Flight/FlightManager.Lifecycle.cs` — `FilePirep()` corrige `arr_airport_id` vía
    `UpdatePirep` y reubica al piloto vía `MovePilotAsync` cuando hay mismatch; logs
    `Log_ArrivalAirportCorrected` / `Log_ErrorPilotPositionUpdate`
  - `Core/Flight/PirepBuilder.cs` — `PirepPayloadArgs.ArrivalAirport` + `arr_airport_id` en
    `BuildPayload`
  - `ViewModels/MainViewModel.cs` — pasa `apiService` al constructor de
    `TelemetryCoordinator`
  - `Languages/en.json` / `es.json` — nuevas claves `Lnm_ArrivalAirportMismatch`,
    `Lnm_DistanceMismatch`, `Log_ArrivalAirportCorrected`, `Log_ErrorArrivalAirportUpdate`,
    `Log_ErrorPilotPositionUpdate`

---

## [0.8.7] — 2026-07-26

### Changed

- **Bonificación Single Engine Taxi condicionada al cumplimiento del ciclo de vida de motores**
  (`ScoringService`, `PenaltyState`, `FlightScoreData`, `FlightManager`):

  La bonificación de +5 pts por taxi en un solo motor ahora tiene reglas diferenciadas por tipo
  de propulsión, gestionadas mediante el nuevo enum `ScoredEngineType` en `Models`:

  | Tipo | Regla |
  |---|---|
  | **Piston** | Nunca elegible para la bonificación |
  | **Turboprop** (Q400, ATR, etc.) | Siempre elegible — no requiere cumplimiento de lifecycle |
  | **Jet** / Unknown | Elegible solo si warmup y cool-down fueron cumplidos |

  Para jets, la bonificación se deniega si:
  - `EngineWarmupViolation` — tiempo de ralentí insuficiente al iniciar TakeoffRoll
    (< 120 s con OAT ≥ 5 °C, o < 300 s con OAT < 5 °C)
  - `EngineCooldownViolation` — motores apagados antes de completar los 180 s de cool-down
    post-reversa en TaxiIn

  Cuando se deniega la bonificación, se registra en el log al finalizar el PIREP:
  `"⚠️ Single engine taxi sin bonificación — {razón}"`.

  **Archivos modificados:**
  - `PenaltyState` — nuevos campos `EngineWarmupViolation` / `EngineCooldownViolation`
  - `FlightManager.cs` — setea `EngineWarmupViolation` en TakeoffRoll cuando `CheckPreTakeoff()` falla
  - `FlightManager.Telemetry.cs` — setea `EngineCooldownViolation` en TaxiIn cuando `CheckShutdown()` detecta apagado prematuro
  - `FlightManager.Lifecycle.cs` — propaga flags + categoría de motor a `FlightScoreData`; agrega `using vmsOpenAcars.Services`
  - `FlightScoreData` — nuevas propiedades `EngineType`, `EngineWarmupViolation`, `EngineCooldownViolation`
  - `ScoringResult` — nueva propiedad `SingleEngineTaxiDeniedReason`
  - `PirepBuilder.LogScore` — emite warning cuando `SingleEngineTaxiDeniedReason != null`

---

## [0.8.6] — 2026-07-16

### Added

- **Panel de motores — temperatura y presión de aceite** (`EngineMonitorPanel`):  
  Nueva tercera fila por motor que muestra `OIL {temp}°C  {press}PSI` con código de color:
  rojo (<40 °C, fuera de rango verde), amarillo (40–70 °C, calentando), verde (>70 °C, temperatura
  operacional). La fila de estabilización ahora incluye el objetivo de tiempo: `IDLE 45s/2m` (rojo
  mientras no se alcanza, amarillo si el tiempo se cumplió pero el aceite aún no estabiliza, verde ✓
  cuando todo está en orden). Para motores pre-arrancados sin tiempo rastreado, se sigue mostrando
  `STAB ✓` / `CALENTANDO...` pero con la temperatura real de aceite visible debajo.  
  `EngineLifecycleSnapshot` extendido con `OilTemp1/2`, `OilPress1/2`, `OatCelsius`,
  `RequiredIdleSeconds`; poblados desde `LastRawData` en `FlightManager.GetEngineLifecycleSnapshot()`.

### Fixed

- **Pushback detectado inmediatamente al iniciar vuelo** (`FlightPhaseStateMachine`):  
  Si el avión ya tenía velocidad de suelo > 0,5 kt al pulsar START (tractor de pushback enganchado
  o ruido de GS del sim), el contador de pushback iniciaba desde el primer ciclo y disparaba la
  transición a Pushback tras exactamente `PushbackMinSec = 8 s`, registrando Block Off y penalización
  de salida con 10 min de antelación de forma errónea.  
  Corregido con el flag `_boardingStationaryConfirmed`: el conteo de pushback/taxi solo comienza
  tras observar al menos un ciclo con GS ≤ 0,5 kt durante la fase Boarding. Si el avión ya viene
  en movimiento al iniciar, se ignora hasta que se detenga.

---

## [0.8.5] — 2026-07-14

### Added

- **Engine Lifecycle Monitor** — dos subsistemas nuevos de monitoreo de ciclo de vida de motores:

  **Subsistema 1 — Arranque tardío de motor (`EngineStartMonitor`)**  
  Detecta motores arrancados durante TaxiOut sin suficiente calentamiento en ralentí antes de
  aplicar TOGA. Requisito mínimo: 120 s con OAT ≥ 5 °C, 300 s con OAT < 5 °C. Criterios de
  estabilización para el panel: N2 ≥ 58 % + presión aceite ≥ 15 PSI + temperatura aceite ≥ 40 °C.  
  Motores pre-arrancados (corriendo antes de iniciar el vuelo) no generan penalización; el panel
  muestra `STAB ✓` / `CALENTANDO...` en su lugar. Si un motor se arranca durante TaxiOut y el
  tiempo de ralentí es insuficiente al entrar en TakeoffRoll, se registra advertencia en log y OSD.

  **Subsistema 2 — Cool-down post-aterrizaje (`ThrustReverserMonitor`)**  
  Monitorea el despliegue de reversas tras aterrizaje (offset FSUIPC FLOAT64 0x207C/0x217C,
  umbral 0,6 %). Requiere 180 s de ralentí antes de apagar motores si se usaron reversas.
  Al apagar motores en TaxiIn con cool-down insuficiente se genera advertencia en log.  
  Si el offset retorna siempre 0 tras 30 s post-aterrizaje, el sistema advierte que el addon
  puede no soportar este offset.

  **Panel de motores (`EngineMonitorPanel`)** — nueva fila por motor y barra de reversa:
  - `STAB ✓` (verde) — motor estabilizado (pre-arrancado o tiempo idle suficiente)
  - `CALENTANDO...` (amarillo) — motor corriendo pero aceite aún frío
  - `IDLE 1m30s ✓` / `IDLE 45s` — motor arrancado durante TaxiOut (verde/amarillo)
  - `REV OK` / `REV COOL-DOWN Xs` / `REV —` / `REV N/D` — estado de reversas post-aterrizaje

  **Entradas de log por fase:**
  - TakeoffRoll: estado de cada motor (idle medido o pre-arrancado) + estabilización + OAT
  - Touchdown: confirmación de inicio de monitoreo de reversas
  - TaxiIn: resumen de reversas (pico ENG1/ENG2 %) o aviso de offset sin datos

  **Offsets FSUIPC nuevos en `RawTelemetryData`:**  
  `N2_1`/`N2_2` (0x2220/0x2420 DWORD → %), `Rev1Pct`/`Rev2Pct` (0x207C/0x217C FLOAT64 × 100),
  `OatCelsius` (0x0E8C INT16 ÷ 256).

---

## [0.8.4] — 2026-07-09

### Fixed

- **Error de login con IVAO ID vacío** (`ApiService`): usuarios sin ID de IVAO configurado
  en phpVMS recibían `Login error: Unexpected Error: La cadena de entrada no tiene el
  formato correcto`. phpVMS devuelve `ivao_id` como cadena vacía `""` en esos casos;
  `Value<int>()` llamaba internamente a `Convert.ToInt32("")` lanzando `FormatException`.
  Cambiado a `int.TryParse(...)` que trata `null`, `""` y cualquier no-numérico como `0`.

---

## [0.8.3] — 2026-07-09

### Fixed

- **Auto-actualización bloqueada por `fsuipcClient.dll` en uso** (`Updater`, `MainForm`):
  el Updater esperaba sólo 2 s fijos antes de copiar los archivos. Los DLLs permanecen
  mapeados en el proceso hasta su terminación completa —no sólo hasta que se llama a
  `FSUIPCConnection.Close()`— por lo que 2 s no eran suficientes para el shutdown de
  WinForms. El Updater ahora usa `Process.WaitForExit(30 000)` sobre el proceso por nombre,
  más 500 ms de gracia, eliminando la race condition.  
  Segundo problema: MainForm sobreescribía el `Updater.exe` del paquete de actualización con
  el instalado localmente, impidiendo que las correcciones del propio Updater llegaran a los
  usuarios (dependencia circular). Ahora se usa el `Updater.exe` del paquete si ya existe;
  el local se copia sólo como fallback si el paquete no incluye uno.

---

## [0.8.2] — 2026-07-07

### Fixed

- **Doble detección de touchdown** (`FsuipcService`): el debounce era de 2 s; un rebote del
  avión durante la rodadura generaba un segundo evento 3 s después del aterrizaje real.
  Nuevo `TOUCHDOWN_DEBOUNCE_SECONDS = 10 s` (el debounce de despegue no se modifica).

- **"PISTA DESOCUPADA" prematuro y doble** (`TelemetryCoordinator`): la salida de pista se
  declaraba a la primera lectura negativa del footprint, mientras que la entrada requería
  2 positivas. Se añadió `_pendingRunwayOffCount` — la salida ahora requiere **3 lecturas
  consecutivas** fuera del footprint. Además, un guard de **30 s** en `_lastRunwayVacatedTime`
  impide el re-disparo cuando el avión roza el borde después de haber vacado la pista.

- **Penalización incorrecta por "Bajo Mínimos" tras aterrizaje normal** (`ApproachValidator`,
  `FlightManager.Telemetry`): `BelowMinimums` se activaba al cruzar la DA en cualquier
  approach ILS, incluso en aterrizajes correctos. Se añadió `ClearBelowMinimumsIfLanded()`:
  el flag se limpia en el momento del touchdown, de forma que la penalización sólo persiste
  si el avión cruzó la DA y luego ejecutó una aproximación frustrada.

---

## [0.8.1] — 2026-07-06

### Fixed

- **MapForm — crash al abrir el mapa** (`NullReferenceException` en `MapRouteController.LoadRoute`):
  `_spinner` se inicializaba después de `InitMap()`, pero `InitMap()` ya pasaba `_spinner` al
  constructor de `MapRouteController`. Al llamar a `LoadRoute`, `_spinner.StartSpin()` intentaba
  acceder a un campo nulo. Solución: mover la creación de `SpinnerOverlay` antes de `InitMap()`.

---

## [0.8.0] — 2026-07-03

### Refactored

Gran refactoring arquitectural. No hay cambios funcionales — el comportamiento en vuelo
y el scoring son idénticos a v0.7.8.

**Core — FlightManager descompuesto** (`~2 000 l` → `532 l` + archivos satélite):
- `FlightPhaseStateMachine.cs` — máquina de estados de fase, umbrales y debounce
- `ApproachValidator.cs` — approach estabilizado 1 000 ft, localizer, DA/MDA
- `TouchdownState.cs` — touchdown data: TDZ, centreline, g-force, bank, pitch
- `PenaltyState.cs` — acumulación de penalizaciones y violaciones
- `FlightManager.Telemetry.cs` — procesamiento de telemetría raw
- `FlightManager.Lifecycle.cs` — ciclo de vida del PIREP (prefile, file, resume)

**ViewModels — MainViewModel descompuesto** (`~2 450 l` → `711 l` + archivos satélite):
- `TelemetryCoordinator.cs` — puente datos raw → FlightManager (throttling, debounce UI)
- `AcarsReporter.cs` — envío de PIREP, resume desde historial, checkpoint 60 s

**UI/Map — MapForm descompuesto** (`~4 100 l` → `1 473 l` + controladores):
- `MapRouteController.cs` — `LoadRoute`, SID/STAR virtual, suavizado Bézier
- `MapRouteController.Approach.cs` — overlay de aproximación y missed approach
- `MapRouteController.Helpers.cs` — 22 helpers estáticos (arcos DME, departure arc,
  hold racetrack, matching de procedimientos, GeodesicBearing, DistanceKm, etc.)
- `MapOverlayManager.cs` — overlays de airspaces y estaciones ATC IVAO
- `SidebarController.cs` — panel SID/STAR/APP, link carta de aproximación

**Interfaces de servicio** (`Services/Interfaces/`):
- `IApiService`, `IMetarService` y demás — desacoplamiento para testabilidad

### Added

- **Proyecto MSTest** `vmsOpenAcars.Tests/` con **66 tests de `ScoringService`** cubriendo
  todos los criterios y umbrales documentados en CLAUDE.md.

---

## [0.7.8] — 2026-07-01

### Fixed

- **PIREP archivado por phpVMS al recibir status="ARR", bloqueando el endpoint /file** — al
  entrar en fase OnBlock, `FlightManager` lanzaba `Task.Run(() => UpdatePirepStatus("ARR"))`.
  phpVMS, al recibir ese update, archivaba internamente el PIREP (lo movía a estado "pending"),
  pero lo hacía **sin pasar por el flujo de auto-aprobación** del endpoint `/file`. Resultado:
  cuando el piloto pulsaba SEND, `POST /api/pireps/{id}/file` llegaba a un PIREP ya en estado
  "pending" y phpVMS devolvía HTTP no-2xx. El PIREP requería aprobación manual del administrador
  porque nunca pasó por el mecanismo de auto-aprobación de phpVMS.

  **Tres correcciones en `FlightManager`:**
  1. **Supresión de `UpdatePirepStatus` en fases terminales** — el guard de envío de estado
     ahora excluye `FlightPhase.OnBlock` y `FlightPhase.Completed`. El endpoint `/file` es el
     que gestiona la transición de estado en phpVMS (incluido el trigger de auto-aprobación).
  2. **`block_on_time` consolidado en el payload de `FilePirep()`** — en lugar de enviarse como
     `Task.Run` fire-and-forget separado (que competía con `/file` y no tenía garantía de orden),
     `block_on_time` se incluye directamente en el `finalData` del envío final. `_serverBlockOnTime`
     se establece sincrónicamente al detectar OnBlock.
  3. **`UpdateBlockOffTime()` refactorizado** — eliminado acceso directo a `_apiService.HttpClient`
     (propiedad pública); ahora usa el método encapsulado `_apiService.UpdatePirep()`, igual que
     el resto de llamadas de actualización.

---

## [0.7.7] — 2026-06-30

### Fixed

- **PIREP archivado pero FilePirep() devuelve false** — phpVMS puede procesar y archivar un
  PIREP correctamente pero devolver un código HTTP no-2xx (comportamiento observado en producción).
  En ese caso `FilePirep()` retornaba `false` aunque el PIREP estuviera en el servidor como
  "pendiente de aprobar". Resultado: el mensaje "No se pudo enviar el PIREP" aparecía, SEND
  permanecía activo, y si el piloto pulsaba CANCEL el PIREP ya archivado se eliminaba del servidor.
  
  Corregido en `FlightManager.FilePirep()`: cuando la llamada HTTP devuelve error, se realiza
  un `GET /api/pireps/{id}` para consultar el estado real del PIREP en el servidor. Si el estado
  es distinto de `1` (in_progress) o `6` (paused), el PIREP ya fue archivado y se trata como
  éxito — `ActivePirepId` se limpia, la UI se actualiza correctamente y el vuelo queda completo.
  Si el GET también falla (sin conexión), el comportamiento previo se mantiene: SEND queda
  habilitado para reintento.

- **Variable `data` inaccesible en `UpdatePhase()`** — la corrección de Hotel Mode Block On
  de v0.7.6 usaba `data.HotelModeActive` en el método `UpdatePhase()`, que no recibe
  `RawTelemetryData`. Corregido usando el campo `_hotelModeActive`, que `UpdateTelemetry()`
  actualiza en cada ciclo de telemetría.

---

## [0.7.6] — 2026-06-24

### Fixed

- **Hotel Mode — Block Off falso al arrancar Motor 2 en Boarding** — el ATR72-600 y similares
  arrancan el Motor 2 como generador de tierra (Hotel Mode: turbina en marcha, hélice bloqueada)
  antes del vuelo. Esto hacía que `EnginesRunning` pasara a `true`, disparando la lógica de
  Block Off por encendido de motores en la fase Boarding (prevista para aviones sin pushback).
  El sistema registraba el Block Off antes de que el avión se moviera. Corregido añadiendo
  `&& !data.HotelModeActive` al guard de la condición en `FlightManager` (~línea 1766).

- **Hotel Mode — Block On bloqueado en TaxiIn** — con el Motor 2 en Hotel Mode (N1 > 10%),
  `_areEnginesOn` se mantenía `true` indefinidamente, impidiendo que la condición de Block On
  (`!_areEnginesOn` + 90 s detenido) se satisficiera. El Block On no se registraba hasta que
  el Motor 2 se apagaba completamente, lo que podía ser mucho tiempo después del parqueo.
  Corregido cambiando el guard a `(!_areEnginesOn || data.HotelModeActive)` en el case TaxiIn
  de `FlightManager` (~línea 1478).

- **SEND no se deshabilita / CANCEL borra PIREP ya enviado** — bajo ciertas condiciones de red
  (timeout en respuesta HTTP, error transitorio), `FilePirep()` podía devolver `false` aunque
  el PIREP hubiera llegado correctamente a phpVMS. Resultado: `SendPirep()` no tenía rama
  `else`, la UI no se actualizaba (SEND permanecía verde, CANCEL no cambiaba a EXIT), y al
  pulsar CANCEL el sistema borraba el PIREP ya archivado porque `ActivePirepId` seguía activo.
  Tres correcciones:
  1. **`FlightManager.FilePirep()`**: `ActivePirepId` se limpia inmediatamente cuando la API
     confirma éxito, antes de `ResetFlightState()` — así CANCEL nunca puede borrar un PIREP
     ya enviado aunque algo falle después localmente.
  2. **`MainViewModel.SendPirep()`**: añadida rama `else` con mensaje de error visible al
     piloto ("No se pudo enviar el PIREP, verifique la conexión"), y try/catch para excepciones
     inesperadas. En ambos casos SEND queda habilitado para reintento.
  3. **`MainViewModel.OnFlightPhaseChanged()`**: el guard de habilitación de SEND ahora
     verifica `!string.IsNullOrEmpty(_flightManager.ActivePirepId)` — previene que una
     continuación asíncrona tardía re-habilite el botón tras el éxito del envío.

---

## [0.7.5] — 2026-06-15

### Fixed

- **Turboprop — offset TRQ incorrecto** — `FsuipcService` leía el offset `0x2068` (FLOAT64) como "Torque %" cuando en FSUIPC7 ese offset corresponde a **Fuel Flow (lb/hr)**. En aeronaves como el ATR72-600 (PW127M) el flujo en crucero es 500–700 lb/hr, produciendo valores de "torque" de varios cientos de porcentaje. El offset correcto para Torque % es `0x2020` (motor 1) / `0x2120` (motor 2). Corregido en `FsuipcService._eng1TorquePctF64` y `_eng2TorquePctF64`.

- **Hotel Mode — falsa penalización de BEACON** — el ATR72-600 y otros turbohélices soportan **Hotel Mode**: el motor 2 arranca como generador de tierra (turbina en marcha, NH ~65-70%) con la hélice bloqueada, sin que el BEACON sea requerido (las hélices no están girando). vmsOpenAcars penalizaba −5 pts en dos puntos distintos del código:
  1. **Transición ON→OFF→ON** (en `OnRawDataUpdated`, línea de detección `EnginesRunning && !_areEnginesOn`): se evaluaba `!_isBeaconOn` antes de que `_hotelModeActive` se actualizara → siempre penalizaba. Corregido usando `data.HotelModeActive` directamente del paquete de telemetría.
  2. **Loop continuo** (`CheckViolations`): `beaconExempt` ahora incluye `|| _hotelModeActive`.

  **Detección de Hotel Mode** (`FsuipcService`): para categoría Turboprop, `HotelModeActive = true` cuando algún motor tiene N1 > 10% (turbina en marcha) pero `PropRpm < 50` RPM (hélice bloqueada). Se propaga en `RawTelemetryData.HotelModeActive`. En cuanto la hélice comienza a girar (`PropRpm ≥ 50`), hotel mode se desactiva y el beacon vuelve a ser obligatorio.

---

## [0.7.4] — 2026-06-14

### Added

- **Scoring en aeropuerto alterno** — al entrar en fase Approach, el sistema intenta adquirir el umbral de pista primero en el aeropuerto de destino del OFP (`SimbriefPlan.Destination`); si no lo encuentra (el avión está alineado con una pista de otro aeropuerto), reintenta con el alterno del OFP (`SimbriefPlan.Alternate`). Al confirmar el alterno se loguea `"⚠️ Approaching ALTERNATE — XXXX"` y se activa el modo alterno para el resto del vuelo:
  - **TDZ y Centreline**: `LookupRunwayData` usa `_approachDestination` (alterno) en lugar de `_activePlan.Destination` → `FindTouchdownRunway` localiza correctamente la pista en el aeropuerto alterno.
  - **ILS / Localizer / Minimums**: `LoadApproachData` carga los datos de ILS, approach type y fixes del alterno → el gate de 1 000 ft evalúa estos criterios contra la frecuencia y procedimiento reales del alterno.
  - **QNH de llegada**: `FlightManager._effectiveDestination` (nuevo campo) se establece al ICAO del alterno → ambas rutas de check QNH (basada en Transition Level y basada en gate 1 000 ft) llaman `GetWeatherAsync(alterno)` en lugar del destino original → sin falsos positivos por diferencia de QNH entre aeropuertos.
  - Si el aterrizaje ocurre en un aeropuerto que no es ni el destino ni el alterno del OFP, el comportamiento es el existente: TDZ/Centreline/ILS omitidos sin penalización.
- **`MainViewModel._approachDestination`** — nuevo campo `string` que almacena el ICAO resuelto (destino o alterno) desde la adquisición del umbral hasta el touchdown. Se resetea a null al iniciar cada nueva fase Approach; persiste intencionalmente al salir de Approach para que `LookupRunwayData` pueda usarlo tras el touchdown.
- **`FlightManager.SetEffectiveDestination(string)`** — nuevo método público que `MainViewModel` llama cuando detecta el alterno; redirige los dos puntos de check QNH de destino.

---

## [0.7.3] — 2026-06-14

### Changed

- **Overspeed — exención por instrucción ATC (IVAO)** — cuando el piloto tiene COM1 sintonizada en la frecuencia de una estación ATC activa en IVAO (TWR, APP, DEP, CTR…), los eventos de overspeed ya no generan deducción de puntos; la advertencia en el log y el OSD `OVERSPEED  XXX KTS` se mantienen. Si no hay ATC activo o COM1 está en UNICOM (122.8) u otra frecuencia no-ATC, aplican las penalizaciones habituales (0→0 / 1→−7 / ≥2→−15 pts). El desglose del PIREP diferencia eventos penalizados de exentos: `"3 event(s), 1 penalized (ATC exempt: 2)"`.

- **Vapp gate (Stabilized Approach, 1 000 ft AGL) — exención por instrucción ATC** — igual que overspeed: si COM1 está sintonizada en ATC activo IVAO, la deducción de −5 pts por velocidad fuera del rango Vapp queda suprimida, pero el log Warning persiste. Los otros seis sub-criterios del gate (VS, bank, pitch, gear, flaps, ILS) no cambian.

- Implementado mediante delegate `FlightManager.IsOnAtcFrequency` (`Func<bool>`) inyectado por `MainViewModel`. El helper `IsAtcOnCom1()` consulta `FsuipcService.Com1FrequencyMhz` (offset FSUIPC `0x034E` BCD) contra `AirspaceMonitorService.GetAtcStations()` con tolerancia ±0.005 MHz (5 kHz). `FlightManager` mantiene contadores separados `_overspeedCount` (total) y `_overspeedPenaltyCount` (solo penalizados), propagados en `FlightScoreData` al calcular el score.

---

## [0.7.2] — 2026-06-01

### Fixed

- **Touchdown Zone — umbral desplazado (displaced threshold)** — `FindTouchdownRunway` (NavDataService) restaba `NavRunway.OffsetThresholdFt` de la distancia proyectada antes de evaluar la penalización TDZ. Antes, la distancia se medía desde el extremo físico de la superficie pavimentada (`threshold_lat`/`threshold_lon`), ignorando que el umbral legal de aterrizaje puede estar hasta 1 800+ ft más adelante. Ejemplo: KSAM RWY 27 con `offset_threshold_ft = 1810 ft` — un aterrizaje a ~3 140 ft del extremo físico es en realidad a ~1 330 ft del umbral desplazado (dentro de la TDZ, sin penalización). Ahora: `distFt = along × 3.28084 − OffsetThresholdFt`; si `distFt < 0` (toca antes del umbral, en la zona TORA), se fija a 0 sin penalización. Pistas sin umbral desplazado (`OffsetThresholdFt = 0`) no cambian.

---

## [0.7.1] — 2026-06-01

### Added

- **AirspaceMonitorService: alertas predictivas** — `CheckPosition` acepta ahora `headingDeg` y `groundSpeedKts` para proyectar la posición ~3 min hacia adelante (lookahead = min(GS×3/60, 20 NM)):
  - `OnAirspaceApproaching` (nuevo evento): se dispara cuando la posición proyectada cae dentro del polígono y los límites verticales de un espacio Prohibited/Restricted/Danger. Se suprime mientras el ID esté en `_approachingIds`; se cancela automáticamente al entrar al espacio o al girar fuera de la trayectoria. Log: `⚠️ AIRSPACE AHEAD  {TYPE}  {ICAO}  [{lower} – {upper}]`. OSD: `AIRSPACE AHEAD  {TYPE}  {ICAO}` (Warning). No activo si GS < 30 kt.
  - `OnAirspaceOverflight` (nuevo evento): se dispara cuando el avión está lateralmente dentro del polígono pero por encima del límite superior (`altFt > upperFt`). Avisa que la restricción existe bajo él y que no debe descender. Log: `⚠️ OVERFLIGHT  {TYPE}  {ICAO}  [ABOVE {upper}]  DO NOT DESCEND`. OSD: `ABOVE  {ICAO}  DO NOT DESCEND` (Warning).
  - `_approachingIds` y `_overflightIds` — nuevos `HashSet<string>` de supresión; se limpian en `Reset()`.

- **AirspaceMonitorService: límites verticales robustos** — nuevo helper `ParseAltDisplay(string)` parsea el campo textual `NavAirspaceLimit.Display` cuando `ValueFt == null`: `"FL095"` → 9500 ft, `"GND"`/`"SFC"` → 0 ft, cadenas numéricas → ft, `"UNL"` → null (ilimitado). `IsWithinVerticalLimits` usa `ParseAltDisplay` como fallback para ambos límites, eliminando falsas alertas cuando `UpperLimit.ValueFt` es null pero `Display` contiene `"FLxxx"`. Nuevo helper `GetUpperLimitFt(NavAirspace)` para las comprobaciones de sobrevuelo. `OnAirspaceAlert` incluye `[{lower} – {upper}]` en el mensaje de log.

- **Validación de tipo de aeronave contra OFP SimBrief**:
  - `SetActivePlan`: si FSUIPC está conectado y el ICAO del sim difiere del OFP, emite log advisory `⚠️ Aircraft mismatch — Simulator: {simType} / OFP: {planType}` (Theme.Warning).
  - `StartFlight`: diálogo de confirmación bloqueante (YesNo) si los tipos difieren. El piloto puede continuar o cancelar.

### Changed

- **ScoringService: umbrales de Landing Rate** — ≤150 fpm → 0 / ≤250 → −5 / ≤350 → −15 / ≤450 → −25 / ≤650 → −35 / >650 → −40 (antes: ≤100/200/300/400/600/>600). `GetLandingRating` actualizado en consecuencia (etiqueta "Butter" hasta 150 fpm).
- **ScoringService: umbrales de G-Force** — ≤1.5g → 0 / ≤1.7g → −7 / >1.7g → −15 (antes: ≤1.3g/≤1.5g/>1.5g).

---

## [0.7.0] — 2026-05-28

### Added

- **MapForm: transiciones de aproximación** — nuevo combo `Trans.` en el sidebar de destino, inmediatamente debajo del selector de aproximación. Muestra los IAF de entrada (transiciones) del procedimiento activo:
  - `NavApproachTransition` (nuevo DTO en `Models/NavData.cs`): campos `Fix` (nombre del IAF), `FixType`, `FixRegion`, `Type`, `Legs` (`List<NavApproachLeg>`).
  - Propiedad `Transitions` (`List<NavApproachTransition>`) añadida a `NavApproach`.
  - `FillApproachTransCombo(cmb, approach, ref selection)` — puebla el combo con `(none)` + fixes ordenados por nombre; preserva la selección activa si el fix sigue disponible tras cambiar approach.
  - `OnApproachTransChanged` — al cambiar la selección llama `DrawApproachOverlay(app, trans, rwy, ils)`.
  - `DrawApproachOverlay` (firma actualizada) — acepta `NavApproachTransition trans`; si no es null, prepende los legs de la transición a los legs del procedimiento antes de dibujar la polilínea magenta.
  - El combo se limpia y re-puebla al cambiar el approach activo; se resetea a `(none)` al cambiar la pista de destino.

- **Invalidación manual de caché NavData** — botón **REFRESH NAVDATA** en Settings → sección NavData API (junto al botón TEST):
  - `NavDataCache.PurgeAirportData()` — elimina todas las filas de `airport_entries` y `navaid_entries` en la BD SQLite sin tocar `airspace_entries`.
  - `NavDataClient.ClearMemoryCache()` — limpia todos los `ConcurrentDictionary` de aeropuertos/procedimientos en memoria de sesión; sin necesidad de reiniciar la app.
  - Actualiza `lblNavDataStatus` con mensaje de confirmación. La siguiente llamada a `PrefetchAirport(icao)` descarga los datos frescos del API automáticamente.

### Fixed

- **MapForm: filtrado bidireccional SID/STAR ↔ pista** — al cambiar SID o STAR en el sidebar, `GetCompatibleRunways()` filtra el combo de pista de salida/llegada mostrando solo las pistas compatibles con el procedimiento seleccionado. Simetría completa: al cambiar pista, el SID/STAR incompatible sigue mostrando el diálogo de confirmación existente.

- **MapForm: `MatchProcedure` — preselección con nombres de sufijo NavData** — los nombres de procedimiento con designador de transición (ej. `"BIVI3C.01"`) no coincidían con el nombre base SimBrief (ej. `"BIVI3C"`). Añadidos dos pasos de lookup adicional que comparan el nombre truncado al primer punto con el nombre del plan, y viceversa, garantizando la preselección correcta del SID/STAR al cargar el OFP.

- **MapForm: barra de estado — recorte de controles de zoom y capas** — `_lblStatus` tenía `DockStyle.Left` con ancho fijo de 380 px; al reducir el ancho del formulario el label empujaba fuera de vista los controles de zoom (`−`/`+`, dropdown de proveedor) y las checkboxes de capa. Cambiado a `DockStyle.Fill` y añadido al final de la secuencia `Controls.Add` para que tome el espacio residual y los controles `DockStyle.Right` siempre queden visibles.

---

## [0.6.9] — 2026-05-27

### Changed

- **IVAO ATC — filtrado de estaciones para reducir ruido en el log** — el reporte de posiciones IVAO se filtra ahora con tres criterios encadenados antes de mostrarse en el log y en el mapa:
  1. **Suppressión de duplicados consecutivos** — si una estación (mismo callsign, frecuencia, posición y ATIS) no cambió desde el poll anterior, no se muestra. Elimina la repetición de las mismas 3-4 estaciones cada 3 minutos durante crucero.
  2. **Filtro de distancia** — se omiten estaciones cuyo aeropuerto está a más de 150 NM del avión (80 NM en fase Approach/Landing). Las coordenadas de aeropuerto se cachean perezosamente desde `NavDataClient.GetAirportInfo()`. Los matches de prefijo FIR (ej. todos los `SK*`) sin coordenadas en caché pasan sin filtrar — ya están limitados por `_relevantIcaos`.
  3. **Priorización por fase** — en fases Approach y Landing, solo se muestran estaciones del aeropuerto de destino + posiciones APP/DEP de aeropuertos cercanos. Elimina las estaciones del origen y otros aeropuertos de ruta que no son relevantes en la aproximación.

- **`AirspaceMonitorService`** — nuevos campos de estado (`_lastAtcPoll`, `_airportCoordsCache`, `_lastAcLat/Lon`, `_isApproachPhase`, `_destIcao`), métodos `UpdateAircraftState()` (inyectado desde MainViewModel en cada ciclo de telemetría) y `FilterAtcStations()` (aplicado en `PollIvaoAsync` antes de `OnAtcUpdated`). `InitRouteAsync` acepta parámetros opcionales `initLat/initLon` para inicializar la posición en el primer poll.

- **`MainViewModel`** — llamada a `UpdateAircraftState()` cada 30 segundos junto con `CheckPosition()`. Las dos llamadas a `InitRouteAsync` (en `SetActivePlan` y `StartFlight`) ahora pasan la posición actual del avión para que el primer poll IVAO ya tenga coordenadas válidas.

### Resultado esperado

En el vuelo SKRG→MMMX, el log pasó de ~200 líneas de 📻 a un número significativamente menor. En crucero solo aparecen estaciones dentro de 150 NM. En Approach, solo MMMX TWR/GND/APP y APP/DEP cercanos.

---

## [0.6.8] — 2026-05-27

### Added

- **Cartas de aproximación dinámicas** (`ApproachChartForm`) — ventana no-modal 960×720 px, paleta oscura, redimensionable. Se abre desde el botón **"📋 APPROACH CHART"** en el sidebar de destino del mapa.
  - **Plan view (GDI+ north-up):** bounding-box automático de todos los legs, escala flat-earth, extended centerline punteada (5 NM), rectángulo de pista, legs approach (línea sólida blanco/cyan para missed), arcos DME (`AF` leg con `DrawDmeArc`). Símbolos en fixes: triángulo = IAF, círculo verde = FAF, cuadrado cyan = MAP, punto = intermedio. Labels con ident, altitud con descriptor (`+`/`-`/`B`) y velocidad en kt.
  - **Profile view (GDI+ cross-section):** eje X = NM desde threshold (recorrido inverso de `legs[]`); eje Y = ft MSL. Glideslope naranja (`ils_gs`): línea sólida desde FAF a threshold+50 ft usando `NavIls.Glideslope.PitchDeg`. Glidepath verde (`vnav_path`): ángulo de `legs[FafIndex].VerticalAngle`. Advisory gris punteado. Escalera de escalones (`null`/non-precision). DA/MDA línea roja punteada horizontal (último leg con `AltDescriptor="A"`). Ticks de fix con ident y altitud.
  - **Selector de approach:** ComboBox con todos los approaches del aeropuerto, ordenado por pista y tipo; preselecciona el approach activo en el sidebar del mapa.
  - **Header informativo:** nombre de aproximación, ICAO + nombre del aeropuerto, frecuencia ILS o VOR/NDB, elevación pista, TA, TL, ciclo AIRAC.
  - **Carga de datos:** `NavDataClient.PrefetchAirport` + GetApproaches/GetIls/GetRunways/GetAirportInfo; muestra panel "Loading…" mientras carga; reutiliza caché si el aeropuerto ya fue prefetchado en `StartFlight`.
- **`NavApproach`:** campos `ApproachName`, `VerticalGuidance`, `FafIndex`, `Navaid` + propiedad calculada `DisplayName`.
- **`NavApproachLeg`:** campos `FixType`, `FixRegion`, `TurnDirection`, `Rnp`, `SpeedLimitType`, `DmeRadiusNm`, `DmeRadial`, `CenterFix`, `CenterFixRegion`, `CenterLat`, `CenterLon`.
- **`NavNavaid`:** campos `Region`, `VorType`, `NdbType`, `MagVar`.

---

## [0.6.7] — 2026-05-26

### Added

- **Mapa: ATC geográfico estilo WebEye** — las posiciones IVAO se representan como `GMapPolygon` escalados geográficamente (20 nm de radio), reemplazando el dot de 10 px en espacio de pantalla:
  - **TWR** → círculo de radio 20 nm, borde rojo (α 170) + relleno rojo muy transparente (α 30).
  - **GND** → estrella de 4 puntas alineada N/S/E/W, radio exterior 20 nm, radio interior 7.6 nm (0.38×), amarillo.
  - **DEL** → estrella de 4 puntas rotada 45° (puntas NE/SE/SW/NW), mismos radios, naranja.
  - Las puntas de GND y DEL rozan el borde del círculo TWR cuando coexisten (mismo radio 20 nm).
  - `AtcLabelMarker` centrado en ARP: texto ICAO 7 pt Consolas Bold con sombra de 4 px para visibilidad sobre formas de color + punto de 4 px en el centro.
  - Helpers privados en `MapForm`: `MakeCirclePolygon(lat, lon, radiusNm, fill, stroke, n=72)` y `MakeStarPolygon(lat, lon, outerNm, innerRatio, startDeg, fill, stroke)`.
  - APP / CTR / DEP / FSS siguen usando `AtcStationMarker` (text-box) sin cambios.
- **Mapa: capas toggleables en tiempo real** — cuatro `CheckBox` en la barra inferior permiten activar/desactivar capas independientemente sin recargar el mapa:
  - **TILES** — oculta los tiles del proveedor (usa `EmptyProvider`), mantiene la vista y el zoom.
  - **ROUTE** — oculta/muestra la ruta, waypoints, SID/STAR, overlay de aproximación y línea al alterno.
  - **SPACES** — oculta/muestra los polígonos de espacio aéreo (Prohibited/Restricted/Danger/CTR/TMA…).
  - **IVAO** — oculta/muestra las formas geográficas ATC y los `AtcStationMarker` de área.
- **Mapa: icono de aeronave por categoría** — `AircraftMarker` dibuja una silueta diferente según `FsuipcService.AircraftCategory`:
  - Jet (categoría A/B) — silueta de avión de fuselaje estrecho.
  - Turboprop (categoría C) — silueta con motores de hélice más anchos.
  - Piston (categoría D) — silueta de aeronave ligera.
  - Helicopter / Unknown — silueta genérica / flecha.
  - `SetAircraftCategory(FsuipcService.AircraftCategory cat)` en `MapForm`; llamado desde `MainForm` al abrir el mapa y al cambiar el OFP.
- **Mapa: ATC e airspaces visibles sin iniciar vuelo** — `SetActivePlan()` en `MainViewModel` lanza `InitRouteAsync` en segundo plano en cuanto se acepta el OFP, sin necesidad de pulsar START. Al abrir el mapa (`BtnMap_Click`), `MainForm` pre-carga airspaces y estaciones ATC ya disponibles.

### Fixed

- **IVAO ATC filtrado por `_relevantIcaos` — SKRG_TWR y posiciones locales no visibles** — `_relevantIcaos` se construía únicamente a partir de los objetos de espacio aéreo devueltos por NavData; si ningún airspace alrededor del aeropuerto tenía un `ExtractIcao()` que coincidiera exactamente, las posiciones TWR/GND/DEL quedaban filtradas. Corregido añadiendo `originIcao` y `destIcao` explícitamente al set en `InitRouteAsync`, garantizando que el ATC local de salida y llegada siempre pasa el filtro.
- **Debounce de luces — falsos positivos por parpadeo del simulador (~1.6 s)** — el modelo anterior (cooldown 1.5 s: dispara inmediatamente, bloquea repetidos) era sensible a glitches breves del sim que cambiaban el estado de una luz por 1–2 ciclos. Reemplazado por un **hold debounce de 2.5 s**: el nuevo estado debe mantenerse estable durante 2.5 s continuos antes de disparar el evento; cualquier revertido del estado cancela el contador.

### Changed

- **Opacidad de airspaces reducida al 50 %** respecto a v0.6.6 para evitar confusión visual con las formas ATC de IVAO. Valores de ejemplo: Prohibited fill (20,220,0,0) / stroke (95,200,0,0), CTR fill (12,0,180,255) / stroke (70,0,160,230).

---

## [0.6.5] — 2026-05-24

### Added

- **Sidebar de procedimientos en MapForm (estilo Navigraph Maps)** — panel lateral izquierdo que permite cambiar en tiempo real la pista, SID/transición, STAR/transición y aproximación de salida y llegada:
  - Panel colapsable (230 px expandido / 18 px colapsado, botón `◀`/`▶`).
  - Sección **ORIGIN**: label de aeropuerto (ICAO + nombre), selector de pista, SID, transición SID. Chip de viento HW/TW + XW calculado con el METAR en vigor.
  - Sección **DESTINATION**: igual + STAR, transición STAR, aproximación, contador de aproximaciones disponibles para la pista seleccionada.
  - **Validación de compatibilidad** al cambiar pista: si el SID/STAR activo no aplica a la nueva pista se muestra un `EcamDialog` de confirmación; si el usuario rechaza, el combo se revierte. El approach se borra silenciosamente en cualquier cambio de pista destino (siempre runway-specific).
  - **Overlay de aproximación** independiente (`_approachOverlay`, sobre la ruta enroute pero bajo el marcador de avión): trayectoria de legs con coordenadas, extended centerline ±5 NM (semitransparente punteado), missed approach (cian punteado). Color approach = magenta `#FF00C8`, missed = cian `#00C8FF`. No toca `LoadRoute` al cambiar approach.
  - **Chips de viento** actualizados en tiempo real desde `MainForm.UpdateMetarPanel` → `MapForm.SetMetarData()` (índice 0 = ORIG, índice 1 = DEST).
  - **Callback `OnProcedureChanged`** (`Action<string, string, string, string>`) — disparado en cada `RedrawRoute`; `MainForm` lo suscribe y llama `MainViewModel.UpdateProcedureOverrides` para mantener el plan activo sincronizado.
  - `RedrawRoute()` — redibujar la ruta completa con el estado actual del sidebar sin cambiar el par de aeropuertos.
  - `MainViewModel.UpdateProcedureOverrides(originRwy, sidName, destRwy, starName)` — actualiza los campos `OriginRunway`, `SidName`, `DestinationRunway`, `StarName` del plan activo.

### Changed

- **`LoadRoute`** guarda `_currentWaypoints/Icao/AltIcao` para `RedrawRoute`. Las selecciones del sidebar se resetean a los valores del plan de SimBrief solo cuando cambia el par de aeropuertos (no en cada llamada sucesiva de redibujado).
- **`InitMap`** — `_approachOverlay` insertado entre `_waypointOverlay` y `_aircraftOverlay`.

---

## [0.6.4] — 2026-05-24

### Added

- **Recuperación de vuelo activo con historial ACARS, penalizaciones y OFP** — al retomar un PIREP `IN_PROGRESS` tras reiniciar la app, el sistema ahora:
  - **Lee el historial ACARS** de phpVMS (`GET /api/pireps/{id}/acars`) y muestra los últimos 20 registros no-CHK en el log para reconstruir el contexto del vuelo.
  - **Parsea el último checkpoint de penalizaciones** (registro con `status = "CHK"`) y restaura todos los contadores de scoring: overspeed, luces, aproximación inestable, QNH, vuelo offline, salida tardía, velocidades en procedimientos, localizer violations y below minimums. Si no hay checkpoint previo, muestra un OSD de aviso.
  - **Recarga el OFP de SimBrief** — si el usuario tiene `simbrief_user` configurado, descarga automáticamente el último plan activo y lo carga si el origen/destino coincide con el PIREP. El mapa, la ruta y el FMA se populan exactamente igual que al iniciar un vuelo nuevo.
- **Checkpoints de penalizaciones cada 60 s** — mientras hay un vuelo activo, cada 60 segundos se envía automáticamente un registro `AcarsPosition { status = "CHK" }` al servidor phpVMS con el estado actual de todas las penalizaciones en el formato compacto `SC:ov=N,lt=N,sa=N,qnh=N,it=N,od=N,spd=N,lz=N,bm=N,ts=<unix>`. Estos registros son los que la recuperación anterior parsea al retomar el vuelo.
- **Sin límite de tiempo para retomar vuelos** — eliminado el filtro de 20 minutos en `CheckAndResumeFlight`; cualquier PIREP `IN_PROGRESS` (independientemente de cuándo fue la última actualización) se ofrece para retomar.

### Changed

- **`FlightManager`**: añadidas propiedades públicas de solo lectura para todos los contadores de penalizaciones (`OverspeedCount`, `LightsViolationCount`, `StabilizedApproachDeductions`, `QnhViolationCount`, `IsOfflineFlight`, `DepartedLate`, `ProcedureSpdViolations`, `LocalizerViolations`, `BelowMinimums`) y nuevo método `SetResumedPenalties()` para restaurarlos al retomar.
- **`ApiService`**: nuevo método `GetPirepAcarsAsync(pirepId)` — `GET /api/pireps/{id}/acars`, retorna `List<AcarsPosition>`.

---

## [0.6.3] — 2026-05-24

### Fixed

- **Conflicto de versión de System.Data.SQLite en equipos con GAC** — en Windows con software corporativo o de desarrollo instalado (Visual Studio, SQL Server Tools, etc.) el GAC puede contener una versión anterior de `System.Data.SQLite.dll` (p. ej. 1.0.115.5) que entraba en conflicto con la 1.0.119.0 incluida en vmsOpenAcars, produciendo el error `0x80131040` ("La definición del manifiesto del ensamblado no coincide con la referencia"). El `App.config` ya tenía el binding redirect correcto (`0.0.0.0-1.0.119.0 → 1.0.119.0`), pero `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>` en el `.csproj` podía hacer que MSBuild sobreescribiera ese redirect en el `exe.config` de salida si el equipo del desarrollador no tenía SQLite en su propio GAC. Corregido añadiendo `<GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>` al `.csproj`, que preserva los redirects manuales del `App.config` en el output sin que la generación automática los sobreescriba.

  > **Usuarios afectados:** editar manualmente `vmsOpenAcars.exe.config` en la carpeta de instalación y verificar que existe el bloque siguiente dentro de `<runtime><assemblyBinding>`:
  > ```xml
  > <dependentAssembly>
  >   <assemblyIdentity name="System.Data.SQLite" publicKeyToken="db937bc2d44ff139" culture="neutral" />
  >   <bindingRedirect oldVersion="0.0.0.0-1.0.119.0" newVersion="1.0.119.0" />
  > </dependentAssembly>
  > ```
  > Los builds posteriores a v0.6.3 incluyen este redirect de forma fiable.

---

## [0.6.2] — 2026-05-24

### Added

- **Log de sistema en la tabla ACARS de phpVMS al iniciar el vuelo** — al pulsar START y confirmar el inicio del vuelo, se envía automáticamente un `AcarsPositionUpdate` con hasta 4 entradas `log` a la tabla ACARS del servidor phpVMS: sistema operativo + RAM, GPU + VRAM, simulador + versión, y tipo de aeronave / fabricante del add-on. Cada entrada aparece como fila independiente en el historial ACARS del PIREP con `status = "ground"`. El envío es asíncrono (`Task.Run`), no bloquea la interfaz y usa la posición actual del avión como coordenadas de la entrada.
- **Líneas de información de sistema en el log local al arrancar** — al iniciar la aplicación el log muestra: (1) versión de vmsOpenAcars, (2) sistema operativo y RAM, (3) GPU y VRAM. Al conectar FSUIPC se añade una cuarta línea con el simulador y su versión. Todas estas líneas también se incluyen en el campo `notes` del prefile phpVMS.
- **`SystemInfoHelper`** — nueva clase `Helpers/SystemInfoHelper.cs` que recopila información de hardware sin WMI:
  - **OS + RAM**: nombre del SO desde el registro (`ProductName`; detecta Windows 11 por `CurrentBuildNumber >= 22000`) + RAM total vía P/Invoke `GlobalMemoryStatusEx` (kernel32, sin WMI).
  - **GPU**: lectura directa de `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968...}`. Selección por **rango discreto primario** (NVIDIA/GeForce/RTX/GTX/Quadro = 3 · AMD Radeon RX/Pro/Intel Arc = 2 · otros = 1 · Intel integrado = 0); la VRAM es desempate secundario dentro del mismo rango. Garantiza que en portátiles NVIDIA Optimus (GPU discreta como `Render-Only Device` con VRAM = 0 en registro) se muestre la GPU dedicada y no la iGPU Intel. Filtra adaptadores virtuales (Hyper-V, VMware, VirtualBox, Parsec, VDDM, Remote Desktop). VRAM leída como QWORD (8 bytes), soporta tarjetas >4 GB.
  - **Simulador**: `FileVersionInfo` del proceso activo (`FlightSimulator2024`, `FlightSimulator`, `X-Plane`, `Prepar3D`), versión recortada a 3 partes.

---

## [0.6.1] — 2026-05-23

### Added

- **Restricciones de altitud/velocidad en fixes SID/STAR** — los fixes de salida (CLB) y llegada (DSC) que tienen restricciones publicadas en la base de datos NavData muestran ahora debajo de su etiqueta en el mapa:
  - Texto de altitud con las líneas aeronáuticas estándar: línea inferior ("a o superior" `+`), línea superior ("a o inferior" `-`), ambas ("exactamente" `A`/`@`), rango entre dos valores (`B`). Color amarillo cálido `#FFDC78`, fuente 9 pt Consolas. Visible con zoom ≥ 9.
  - Texto de velocidad en kt debajo de la restricción de altitud.
  - Nuevos campos en `NavProcedureLeg`: `Altitude2Ft`, `AltDescriptor`, `SpeedKts`, `SpeedLimitType`.
  - Nueva clase `Models/FixRestriction.cs` con helpers `AltText()`, `SpdText()`, `OsdLine()`.
  - Campo `Restriction` en `SimbriefWaypoint`.
- **OSD de fix próximo** — durante la fase Climb y Descent, cuando el avión se aproxima a ≤ 3 NM de un fix con restricción, aparece un OSD `"PRÓXIMO SIGOX  7000A  250 kts"` (una vez por fix). El log también registra el evento.
- **Scoring: velocidad en procedimientos** — al pasar el fix (≤ 0.5 NM), si la IAS supera el límite publicado en más de 5 kt, se registra una violación. Al enviar el PIREP: −3 pts por violación, cap −10 pts (`Score_CritProcSpeed`).
- **ILS con heading verdadero** — `GetIlsForRunway` usa ahora `loc_true_heading` del endpoint `/airport/{icao}/ils/` (heading TRUE) en lugar de `ils_course` del endpoint de pistas (magnético). Misma clase de error corregida en v0.5.7 para `TrueRunwayBearing`. La DA ahora usa `glideslope.altitude_ft` de la API cuando está disponible, en lugar de `threshold_elevation + 200 ft` constante.
- **Weather desde NavData** — `WeatherService` usa como fuente primaria el endpoint `/weather/{icao}/` de la NavData API (QNH pre-parseado, caché de 5 min) con fallback a aviationweather.gov.
- **is_flyover generalizado** — cualquier fix de SID/STAR con `is_flyover = true` en los legs de NavData recibe tratamiento de fly-over en el mapa (arco Bézier cúbico). Antes solo el primer fix del SID estaba hardcodeado.
- **Ciclo AIRAC en UI** — tras el test de conexión exitoso, si el ciclo AIRAC está expirado se muestra un OSD Warning y se registra en el log con fecha de expiración.
- **Mapa: proveedor Carto Dark** — nuevo proveedor de tiles "Dark (Carto)" (`dark_all`) añadido al combo. Pasa a ser el proveedor por defecto. La preferencia se persiste en `App.config` clave `map_provider_index` y se restaura al abrir el mapa.
- **Mapa: proyección de salida sin SID** — cuando el plan de SimBrief no incluye SID real (ningún fix con `is_sid_star = 1` en la fase CLB), el mapa dibuja la pista física y traza un arco de salida desde el final de pista hasta el primer fix del navlog:
  - Se proyecta un punto de pivote a 3 NM del final de pista en el eje de despegue. Si existe un waypoint publicado entre 2 y 5 NM en esa dirección (alineado con ≤ 25° del eje), se usa ese waypoint como pivote y recibe marcador `apfx` propio.
  - Desde el pivote se traza un arco circular de radio 2.5 NM que gira hasta que la tangente apunta al primer fix del navlog. El arco incluye una recta de tangencia hasta el fix.
  - Función `ComputeDepartureArc`; fallback a `ComputeTransitionCurve` (Bézier cúbico) cuando el fix está muy cerca del arco.
- **Mapa: proyección de llegada sin STAR** — cuando el plan no incluye STAR real, el mapa calcula la pista de llegada desde NavData y construye la llegada visual:
  - Se proyecta el punto `thr-5nm` a 5 NM delante del umbral en el eje de aproximación contrario. El último fix del navlog actúa como fly-over; `BuildSmoothedRoutes` genera la curva de interceptación hacia `thr-5nm`.
  - Tramo físico coloreado `thr-5nm → umbral` con marcadores en ambos extremos.
  - Nueva función `FindArrivalRunway` — selecciona la pista cuyo eje de aproximación es más próximo al bearing del último fix hacia el umbral; respeta `destRunway` cuando SimBrief lo provee.
- **Mapa: waypoint alineado como guía de final** — en dos escenarios, el mapa busca en la caché de waypoints ambient el fix más próximo a 10 NM del umbral alineado con el eje de pista (tolerancia ±20°):
  - *Sin STAR*: el fix encontrado se inserta como punto interior entre el último fix del navlog y `thr-5nm`; `BuildSmoothedRoutes` genera la curva fly-by en ese punto, llegando a `thr-5nm` ya en el eje.
  - *Con STAR desalineada* (último fix de la STAR con diferencia de rumbo > 25° respecto al eje de final): igual — el fix se inserta y hace de interceptor del eje final, añadiéndose también el umbral como endpoint.
  - El fix alineado recibe siempre un marcador `apfx` propio y se excluye del layer ambient.
- **Mapa: waypoints ambient del aeropuerto de origen** — además del destino, `LoadRoute` carga ahora los waypoints ambient del aeropuerto de **origen** y los muestra en el overlay ambient (atenuados), limitados a ≤ 20 NM del aeropuerto. Los fixes que ya tienen marcador explícito en la ruta (fix alineado, waypoint de salida) se excluyen automáticamente para evitar duplicados.
- **Mapa: visibilidad de waypoints ambient según zoom** — el overlay `_ambientOverlay` (navaids y fixes alrededor de origen/destino) solo es visible con zoom ≥ 10. Se aplica en el cambio de zoom (`UpdateZoomInStatus`) y al completar la carga de la ruta.
- **Mapa: anillos de distancia al umbral** — se dibujan dos círculos punteados a 5 NM y 10 NM alrededor del umbral de llegada cuando se detecta la pista de destino (tanto con STAR como sin STAR). Ayudan a estimar distancia al umbral durante la aproximación.
- **Mapa: línea al alterno** — si SimBrief provee aeropuerto alterno, se traza una línea punteada violeta desde el aeropuerto de destino hasta el alterno con marcador `apt`.
- **Mapa: icono de barra de tareas** — la ventana `MapForm` usa el mismo icono `logo.png` que `MainForm` (antes mostraba el icono genérico de Windows).
- **Mapa: redimensionado de ventana** — la ventana del mapa (`FormBorderStyle.None`) puede ahora redimensionarse arrastrando los bordes y esquinas como cualquier ventana normal de Windows. La solución añade `Padding = new Padding(6)` para que la franja de 6 px del borde quede expuesta al `WndProc` de `WM_NCHITTEST` sin ser interceptada por `GMapControl`.
- **NavData caché SQLite persistente** (`NavData_cache.sqlite`) — nueva clase `Services/NavDataCache.cs` que persiste localmente todos los datos estáticos de la API NavData entre sesiones:
  - Tablas: `meta` (ciclo AIRAC y fecha de validez), `airport_entries` (runways, taxiways, approaches, SIDs, STARs, waypoints por ICAO), `navaid_entries` (VOR, NDB, DME). Archivo junto al ejecutable.
  - `NavDataClient` integrado: comprueba la caché antes de cada petición HTTP y almacena tras fetch exitoso. Los datos estáticos (renovados solo con el AIRAC cada 28 días) no se refrescan hasta que el ciclo cambia.
  - Invalidación automática: `SyncAirac(airac, validUntil)` borra las entradas del ciclo anterior en una transacción atómica al detectar cambio de AIRAC. `Initialize()` lee `airac_valid_until`; si la fecha está expirada al arrancar la app, purga toda la caché antes del primer acceso.
  - Ganancia típica: 50–500× más rápido para aeropuertos ya cacheados; ~96 % menos peticiones a NavData API por sesión.
- **DISPATCH: carga condicional** — al abrir `FlightPlannerForm`, solo se cargan los bids del aeropuerto actual. Si no hay bids, se cargan los vuelos disponibles y se activa automáticamente la pestaña "Available Flights". Antes se cargaban ambas fuentes en paralelo siempre.
- **DISPATCH: eliminar bid** — nuevo botón `🗑 DELETE BID` en la pestaña "My Bids" (rojo, deshabilitado hasta seleccionar un bid). Requiere confirmación con diálogo ECAM `"CONFIRM DELETE BID"`. Tras borrar, refresca la lista y si queda vacía activa automáticamente "Available Flights".
- **Botón OFP deshabilitado sin plan** — `btnOfp` arranca `Enabled = false` y se habilita únicamente cuando hay un plan activo (`OnPlanChanged`). Elimina el modal de advertencia que aparecía antes al pulsarlo sin plan.

### Changed

- **`LoadRoute` ampliado** — firma extendida a `LoadRoute(waypoints, originIcao, originRunway, destIcao, destRunway, altIcao, sidName, starName)`. `MainForm` pasa `plan.Origin`, `plan.OriginRunway`, `plan.Destination`, `plan.DestinationRunway`, `plan.Alternate`, `plan.SidName` y `plan.StarName`.
- **Detección de SID/STAR real** — `hasSid` y `hasStar` ya no se basan en el conteo de fixes con `Stage == "CLB"/"DSC"`, sino en que al menos un fix tenga `IsSidStar = true` (campo `is_sid_star` del navlog de SimBrief). Elimina falsos positivos en rutas donde SimBrief usa `Stage = CLB` para todos los waypoints de subida aunque no haya SID publicada.
- **`SimbriefPlan`** — nuevos campos `OriginRunway`, `DestinationRunway`, `SidName`, `StarName` y `Alternate` leídos desde el JSON de SimBrief.
- **`SimbriefWaypoint`** — nuevo campo `IsSidStar` (`is_sid_star == "1"` en el navlog de SimBrief).

### Fixed

- **Cross-thread exception al cerrar la app** — `OsdOverlayForm.ShowMessage()` y `HideOsd()` llamaban `Invoke` (bloqueante) sin verificar `IsDisposed || !IsHandleCreated`. Al cerrar la app con FSUIPC activo, el hilo de telemetría podía disparar un OSD sobre un handle destruido. Corregido: guard `IsDisposed || !IsHandleCreated` + cambio a `BeginInvoke` (no bloqueante).

---

## [0.5.10] — 2026-05-20

### Added

- **Control de volumen en tiempo real** — nuevo `TrackBar` en Settings → Cabin Announcements (rango 0–100 %, default 80 %, pasos de 5). El cambio se aplica **en tiempo real** sobre el audio en reproducción vía `AudioFileReader.Volume` (NAudio). Cadena de propagación: `trkCabinVolume.ValueChanged` → `CabinVolumeChangedCallback` → `MainViewModel.SetCabinVolume()` → `CabinAnnouncementService.SetVolume()` → `_currentReader.Volume`. La clave `cabin_announcements_volume` se persiste **de forma inmediata** en `App.config` en el momento del cambio (auto-save), sin necesidad de pulsar Save.
- **Auto-save en controles de cabina** — el slider de volumen y el toggle "Enabled" se persisten automáticamente al cambiar, mediante `SaveConfigKey()`. No forman parte del ciclo `HasChanges` / `BtnSave`, por lo que mover el slider o activar/desactivar los anuncios **no activa el botón Save ni cierra el diálogo**.

### Changed

- **`TestAnnouncementAsync`** — al seleccionar una nueva fase en el botón `TEST ▾`, ahora detiene el audio en reproducción (`StopCurrent()`) y limpia la cola antes de encolar el nuevo anuncio. Evitaba que fases anteriores continuasen sonando mientras se reproducía la nueva selección.
- **`Reset()`** — también llama a `StopCurrent()` para detener inmediatamente cualquier audio al finalizar o cancelar un vuelo.

---

## [0.5.9] — 2026-05-20

### Added

- **Cabin Announcements** — pregrabados descargados desde la NavData API y reproducidos en cabina durante las fases de vuelo. Al iniciar el vuelo, `CabinAnnouncementService` descarga en paralelo los MP3 de 7 fases (`boarding`, `taxi_out`, `on_runway`, `cruise`, `top_of_descent`, `approach`, `taxi_in`) vía `/briefing/check/` + `/briefing/download/`. Los archivos se cachean en `%TEMP%\vmsacars\briefing\`. Reproducción: chime WAV (NAudio/SoundPlayer) + MP3 en cola FIFO secuencial vía **NAudio** (`AudioFileReader` + `WaveOutEvent`). Vuelos internacionales (ICAO prefix distinto): inglés primero, luego idioma nativo. Vuelos domésticos: solo idioma nativo. Trigger `on_runway`: primer evento `LandingLightChanged(on=true)` o `StrobeLightChanged(on=true)` con GS ≤ 40 kts. Trigger `cruise`: Enroute + AGL > 10 000 ft sostenido 30 s. Idioma nativo: detectado por `airline.country` (phpVMS API) — países hispanohablantes → `es`, resto → `en`. Supresión automática en aeronaves con capacidad < 40 pasajeros (`curr_aircraft.subfleet.total_seats`).
- **Settings — Cabin Announcements** — sección en el panel derecho de Settings con checkbox de activación (efecto inmediato, sin reinicio) y botón `TEST ▾` con dropdown por fase. El test descarga el audio bajo demanda si no está en caché, reproduce chime + MP3, y muestra el resultado debajo del botón: formato real del archivo (`MP3/ID3`, `OGG`, `AAC`…), tamaño en KB y nombre de archivo.
- **Settings — layout apaisado** — rediseño completo de `SettingsForm` de una sola columna (21 filas, ~760 px de alto) a dos columnas lado a lado (920 × 560 px, mínimo 760 × 520 px). Columna izquierda: Conexión + SimBrief + NavData API. Columna derecha: Landing Log + OSD + Cabin Announcements. Los botones Guardar/Cancelar quedan en un panel fijo inferior, siempre visibles.

### Fixed

- **Reproducción MP3** — reemplazado Win32 MCI (`mciSendString` + `type mpegvideo`) por **NAudio 2.3.0** (`AudioFileReader` + `WaveOutEvent` + `ManualResetEventSlim`). MCI fallaba silenciosamente en Windows 11 para streams de audio puro, reproduciendo solo el chime WAV pero no los MP3 de cabina.

---

## [0.5.8] — 2026-05-18

### Added

- **Mapa en movimiento (MAP)** — nuevo botón MAP (reemplaza MSG no utilizado) que abre una ventana no modal `MapForm` con un mapa GMap.NET en tiempo real. El avión se representa como un marcador amarillo con forma de flecha girado por heading. Modos de seguimiento: FOLLOW activa auto-centrado; el piloto puede desactivarlo para explorar el mapa manualmente. Proveedores disponibles: OpenStreetMap (defecto) y ESRI World Imagery (satélite, sin API key). Botones de zoom ±. Barra de estado inferior con coordenadas lat/lon, heading y zoom actual. El mapa se actualiza cada 5 ciclos de telemetría (~250 ms). Paquete NuGet: **GMap.NET.WinForms 17.2.0** y **GMap.NET.Core 17.2.0**.
- **Criterio de transición de calle de rodaje angular** — el cambio de calle ya no se basa únicamente en proximidad temporal. Una vez confirmada una calle (`_lastLoggedTaxiway`), el contador de histéresis sólo avanza si el heading del avión diverge más de **25°** respecto al bearing bidireccional del segmento actual de la calle confirmada (`FindTaxiwaySegmentBearing()`). Esto elimina los falsos cambios por calles paralelas o calles de cruce que momentáneamente están más cerca. Mientras el avión mantenga el rumbo de la calle actual, los segmentos de otras calles más cercanas se ignoran. Se usa la función `HeadingDeltaBidirectional` para tratar la calle como una línea sin dirección (ambos sentidos de rodaje son válidos).

### Changed

- `NavDataService` — nuevo método público `FindTaxiwaySegmentBearing(airport, taxiwayName, lat, lon)` que devuelve el bearing geográfico del segmento más próximo de una calle de rodaje dada (usado por el criterio angular).
- `MainViewModel` — `HandleTaxiPositionUpdate` modificado para usar el criterio angular; nuevo campo `_mapUpdateCounter` y evento `OnMapPositionUpdate` (lat, lon, heading) que dispara cada 5 ciclos de `RawDataUpdated` (~250 ms, independiente de la tasa de telemetría adaptativa). **Fix:** el contador estaba inicialmente en `OnTelemetryUpdated` (tasa adaptativa: 30 s en taxi → mapa se actualizaba cada 150 s); movido a `OnRawDataUpdated`.

---

## [0.5.7] — 2026-05-18

### Fixed

- **`WithinFootprint` y `ProjectOnRunway` usaban heading magnético como eje de proyección geográfica** — el bug idéntico al de v0.5.6 (heading magnético ≠ bearing geográfico verdadero) afectaba a dos rutas de código adicionales: (1) `WithinFootprint`, que proyecta la posición del avión sobre el eje de pista para detectar entradas y backtracks; (2) la proyección final de touchdown en `ProjectOnRunway`, que tras el fix de 0.5.6 usaba el heading verdadero del *avión* en lugar del magnético de NavData, pero ese heading incluye el ángulo de crab de viento en cruce. Caso real: **TJSJ pista 08** (var. −14°W) con el avión rodando por la Calle S a 513 ft del centreline — el código calculaba −90 ft de desviación lateral (footprint 96 ft halfW) y disparaba un falso backtrack. **SKBO pista 14L** (var. −8.5°W), aterrizaje prácticamente en el eje del ILS, reportaba 226 ft de desviación de centreline.
- Solución unificada: nuevo método privado `TrueRunwayBearing(NavRunway)` que calcula el bearing geográfico verdadero a partir de las coordenadas `EndLat/EndLon` → `ThresholdLat/ThresholdLon` de NavData (WGS-84, libres de variación magnética). Se usa en `WithinFootprint` (elimina falsos positivos en footprint check) y en `ProjectOnRunway` (touchdown metrics precisos independientemente del crab angle). El heading magnético de NavData (`rwy.Heading`) se conserva **únicamente** en `HeadingDelta` para la selección de pista, donde el error de ~13° no impacta la clasificación (umbrales 45°/135°).

---

## [0.5.6] — 2026-05-18

### Fixed

- **Desviación de centreline y distancia al umbral incorrectas por heading magnético vs. verdadero** — `ProjectOnRunway` usaba `rwy.Heading` (rumbo magnético publicado en el AIRAC, ej. 220° para la pista 22R de KEWR) como eje de proyección geométrica, pero FSUIPC offset `0x0580` devuelve el heading **verdadero (true)** del avión. La diferencia entre ambos es la variación magnética local; en KEWR (−13°W) eso produce un error de cross-track de `along × sin(13°)` ≈ **183 m (600 ft)** a 800 m del umbral, aunque el avión aterrice en el centro exacto de la pista. Corregido usando el heading verdadero del avión (`heading` ya disponible en el parámetro) en la llamada final a `Project()`. El heading de NavData se conserva para la selección de pista y el footprint check (donde el error angular de ~13° no afecta la desambiguación de pistas paralelas). El fix es universal: aplica a todos los aeropuertos con variación magnética significativa (Europa, Escandinavia, Alaska, América del Norte).

---

## [0.5.5] — 2026-05-18

### Added

- **Log de transiciones de fase** — cada cambio de `FlightPhase` registra una entrada en el log con el nombre de la fase nueva (formato `── FASE ──`), excepto la transición inicial a `Idle`. Implementado en `FlightManager.TransitionTo` con `OnLog?.Invoke`. Las 16 fases no-Idle tienen claves de localización en `es.json` / `en.json` (`Log_PhaseBoarding`, `Log_PhaseTaxiOut`, `Log_PhaseClimb`…).
- **OSD callout `10 000 FT` en climb** — al superar 10 000 ft AGL durante la fase Climb, el OSD muestra `10 000 FT` (Info). Dispara una sola vez por vuelo (`_passing10kFtSent`); se resetea con el vuelo.
- **Botón minimizar** — nueva acción `─` en el header junto al botón de ajustes ⚙️. Llama a `WindowState = FormWindowState.Minimized`. Compatible con la ventana borderless (`FormBorderStyle.None`); respeta el drag-to-reposition.

### Fixed

- **Falso backtrack / entrada a pista en calle de rodaje paralela** — `FindRunwayEntry` podía devolver un positivo en una calle paralela si las coordenadas de umbral o el ancho de pista en NavData eran ligeramente imprecisos (caso real: SKRG CALLE A detectada como backtrack en pista 14L durante un único ciclo de telemetría). Añadido debounce de **2 ciclos consecutivos** (`_pendingRunwayOnCount >= 2`) antes de confirmar presencia en pista, análogo al `_pendingTaxiwayCount >= 3` de los cambios de calle. Un falso positivo de 1 ciclo queda filtrado sin retardo perceptible en la detección legítima (~6 s con actualizaciones SCH).
- **Severidades OSD incorrectas** — tres mensajes de fase tenían severidad inconsistente con la especificación: `TAKEOFF ROLL` (`Warning` → `Info`), `APPROACH` (`Warning` → `Info`), `ON BLOCK` (`Success` → `Info`).

---

## [0.5.4] — 2026-05-17

### Added

- **Alertas sonoras de cabina para mensajes OSD** — cada mensaje OSD dispara ahora un chime de cabina acorde a su severidad: Info → single chime, Success → double chime, Warning → cavalry charge (3 tonos ascendentes Airbus), Critical → master warning burst. Los cuatro sonidos se compilan como `EmbeddedResource` dentro del ejecutable (no requieren archivos sueltos en la instalación). Los players se pre-cargan en memoria al arrancar, por lo que cada reproducción es instantánea y no bloquea el hilo UI.
- **Toggle de chimes en Settings** — nueva fila "Chimes" en la sección OSD de SettingsForm con checkbox "Play cockpit chimes" (activado por defecto). La preferencia se guarda en `App.config` como `osd_sound_enabled` y se lee vía `AppConfig.OsdSoundEnabled`.
- **Botón TEST ▾ en Settings** — botón desplegable junto al checkbox de chimes que permite probar los cuatro tipos de mensaje (Info / Success / Warning / Critical) directamente desde la ventana de ajustes, sin necesidad de simular un vuelo. El chime respeta el estado actual del checkbox aunque el cambio no haya sido guardado todavía. El OSD visual se muestra sobre la pantalla configurada.

### Changed

- **`OsdAudio.Play`** acepta parámetro opcional `forcePlay = false` para que el TEST ignore `AppConfig.OsdSoundEnabled` y use en su lugar el estado del checkbox en tiempo real.
- **Test OSD en menú MENU** — los cuatro items del submenú "Test OSD" disparan ahora también el chime correspondiente (antes solo mostraban el OSD visual).

---

## [0.5.3] — 2026-05-17

### Fixed

- **Distancia en actualizaciones de posición enviada en km en lugar de NM** — `PrepareTelemetry` en `MainViewModel` construía el campo `distance` de `AcarsPosition` con `totalDistanceKm` sin convertir. phpVMS espera millas náuticas; el valor llegaba aproximadamente 1,85× mayor de lo real. Corregido aplicando la conversión `× 0.539957`, consistente con el PIREP final y `UpdateTimerPirep`, que ya usaban NM.
- **QNH de aeropuerto de origen — fallo silencioso en despegue si la red no responde** — `WeatherService.GetQnhMbAsync` hacía siempre un fetch en vivo y devolvía `null` en cualquier error, sin caché. Si la petición HTTP fallaba en el momento del TakeoffRoll (la ventana de QNH de salida), se registraba `⚠️ QNH {ICAO}: no se pudo obtener el METAR` aunque el METAR hubiera sido obtenido exitosamente durante el embarque. Añadida caché estática por ICAO (`ConcurrentDictionary`): cada fetch exitoso persiste el valor; en caso de error (red, timeout, array vacío, altim nulo) se devuelve el último valor cacheado para ese ICAO. Si la caché también está vacía, el comportamiento es idéntico al anterior: devuelve `null` y la penalización no se aplica.
- **A/P ENGAGED falso al despegar** — el contador de debounce `_apEngagedCounter` se acumulaba durante la fase TakeoffRoll (fase de tierra no incluida en el guard de fases). Al producirse el liftoff y transicionar a Takeoff (`isAirbornePhase = true`), el contador ya tenía 6 ciclos y disparaba inmediatamente `A/P ENGAGED` con 0 ft AGL. Corregido reseteando `_apEngagedCounter = 0` al entrar en TakeoffRoll (`CheckProcedureAtPhaseEntry`), de modo que cualquier señal acumulada en tierra queda descartada y el counter debe acumular 6 ciclos frescos una vez en el aire.

---

## [0.5.2] — 2026-05-16

### Added

- **Detección de taxi en single-engine y bonificación +5 pts** — en aeronaves multi-motor, si el piloto rueda con un solo motor durante **≥ 50 % del tiempo de movimiento** en TaxiOut y/o TaxiIn, el sistema otorga automáticamente **+5 puntos** al score final (sin superar 100). La detección usa cuatro contadores de ciclos (`_taxiOut/InMovingCycles`, `_taxiOut/InSingleEngineCycles`) acumulados durante el rodaje. La evaluación ocurre al entrar en TakeoffRoll (TaxiOut) y al detectar OnBlock (TaxiIn). La bonificación solo aplica en aeronaves donde ambos motores corrieron simultáneamente en algún momento del vuelo (`_bothEnginesRunning`), lo que excluye aviones monomotor. El OSD muestra `SINGLE ENGINE TAXI  +5 PTS` (Success) en el momento de la detección.
- **Log de consumo de combustible en fases clave** — tres nuevas entradas en el log de vuelo registran automáticamente el combustible consumido en cada etapa en tierra:
  - Al entrar en **TakeoffRoll**: combustible consumido en taxi-out (bloque off → carrera).
  - Al abandonar pista (**AfterLanding → TaxiIn**): combustible de viaje (carrera de despegue → salida de pista).
  - Al detectar **OnBlock**: combustible consumido en taxi-in (entrada a pista de rodaje → puerta).

### Changed

- **AGL en mensajes de piloto automático** — los mensajes de log `A/P ENGAGED` y `A/P DISENGAGED` incluyen ahora el AGL en el momento del evento. Ejemplo: `🤖 A/P ENGAGED — HDG/ALT  2 340 ft AGL`.

### Fixed

- **Aproximación RNP/visual penalizada por no sintonizar ILS** — cuando una pista tiene ILS en NavData pero el piloto ejecuta una aproximación RNP, visual o de otro tipo, el sistema comprobaba si NAV1 estaba en la frecuencia del ILS y penalizaba −3 pts por no estarlo. Ahora, al cruzar el gate de 1 000 ft AGL, si NAV1 difiere de la frecuencia ILS esperada en más de 0.05 MHz, el criterio completo se omite en silencio: se anulan `_expectedIls` y `_daAltitudeFt`, lo que cancela tanto la verificación de ILS en el gate como los controles de Localizer Alignment y Minimums Compliance por debajo de 500 ft AGL.
- **Piloto automático — falsos positivos con iFly B38M (MSFS 2024)** — al mover cualquier dial del MCP (p. ej. el selector de HDG), el offset `0x07CC` recibía un valor no nulo por 1-2 ciclos de telemetría, lo que disparaba inmediatamente "A/P ENGAGED". Añadido debounce de confirmación: la señal raw debe mantenerse `true` durante **6 ciclos consecutivos (≈ 300 ms)** antes de confirmar el engagement y registrarlo. La desconexión (`DISENGAGED`) sigue siendo inmediata.
- **`ObjectDisposedException` en `FlightPlannerForm`** — al cerrar el planner de vuelo mientras `LoadAllDataAsync()` aún awaita la respuesta de la API, la continuación asíncrona intentaba acceder a un `RichTextBox` ya destruido. Añadida guarda `if (IsDisposed || !IsHandleCreated) return;` al inicio de `AppendLog()`.

### Removed

- **Penalización de 14 pts por NavData no disponible** — se elimina el criterio "LNM Database" (−14 pts aplicados cuando `NavDataClient.IsKeyValid` era `false`). La NavData API es una función premium; aerolíneas virtuales sin acceso pueden seguir obteniendo puntuaciones perfectas. Los criterios Touchdown Zone, Centreline Deviation, Localizer Alignment y Minimums Compliance simplemente no se evalúan cuando los datos no están disponibles, sin penalización adicional.

---

## [0.5.1] — 2026-05-16

### Added

- **Migración NavData API** — la dependencia de la base de datos local de LittleNavMap se reemplaza por un servicio REST alojado (`https://navdata.vholar.co/api/v1/`). El nuevo `NavDataService` (`Services/`) implementa la misma interfaz pública que el anterior `RunwayService`. La geometría flat-earth y la lógica de scoring se conservan intactas.
- **NavDataClient** — cliente HTTP estático con caché por ICAO (`ConcurrentDictionary`). Prefetch paralelo de 6 endpoints por aeropuerto (runways, taxiways, parkings, holdshort, approaches, info). Acceso síncrono thread-safe para uso desde `Task.Run`. Expone `IsReachable` e `IsKeyValid`.
- **NavAirportInfo** — endpoint `/airport/{icao}/` cargado durante el prefetch. Proporciona `transition_altitude_ft` y `transition_level_ft` (ambos `double?`, null cuando el aeropuerto no tiene el dato).
- **Transition Altitude OSD** — al ascender a través de la TA del aeropuerto de origen, el OSD muestra `TRANS ALT  SET STD 1013` (Warning). Dispara una sola vez por vuelo; se resetea en touch-and-go.
- **Transition Level OSD** — al descender a través del TL del aeropuerto de destino, el OSD muestra `TRANS LEVEL  SET QNH` (Warning). Dispara una sola vez por vuelo.
- **Penalización QNH en climb (STD)** — 1 000 ft por encima de la TA, se comprueba si el altímetro está en estándar (1 013 ±2 hPa). Si no, aplica penalización de QNH (−5 pts) con OSD `PENALTY  QNH  −5 PTS`. Comparación directa contra 1 013,25 hPa; no requiere METAR.
- **Diagnóstico de prefetch NavData** — al iniciar el vuelo, `LogNavDataPrefetch` registra en el log el conteo de pistas, calles, gates y aproximaciones por aeropuerto. Si todos los conteos son cero, avisa con `⚠️ NavData {ICAO}: sin datos`.
- **API key por defecto** — si `navdata_api_key` está vacía o ausente, `AppConfig` usa una key embebida como fallback; `App.Release.config` la incluye preconfigurada para nuevas instalaciones. *(Comportamiento eliminado en v0.9.8 — ver esa entrada. La key literal se retiró de este documento: exponerla aquí la comitea al repositorio.)*
- **Localización completa de mensajes de log** — todos los mensajes hardcodeados en `FlightManager.cs` y `MainViewModel.cs` migrados a claves de localización en `es.json` / `en.json` (70+ claves nuevas). Cubre: luces, fases, scoring, combustible, ILS, IVAO, NavData API, landing log, login, equipamiento del avión y gestión de PIREPs activos.

### Changed

- **QNH de llegada — gate basado en Transition Level** — si NavData provee el TL del destino, la comprobación de QNH de llegada se traslada a TL−1 000 ft MSL. Sin datos de TL, el fallback sigue siendo 1 000 ft AGL en `CheckStabilizedApproachGate`.
- **Validación de API key NavData (dos pasos)** — TEST en SettingsForm y comprobación al iniciar ACARS: (1) `/status/` verifica alcanzabilidad; (2) `/airport/LEMD/runways/` con key verifica validez (401/403 = rechazada). `LnmDbAvailable` se inicializa con `NavDataClient.IsKeyValid`.
- **`GetApproachFixes`** — firma cambiada de `(int approachId)` a `(string airport, string runway)` acorde a la API REST.
- **`RunwayService.cs`** — reducido a solo los tipos resultado (`RunwayTouchdownResult`, `RunwayEntry`, `HoldingPoint`, `ParkingSpot`, `IlsData`, `ApproachInfo`, `ApproachFix`). El código SQLite ha sido eliminado.
- **SettingsForm** — sección "NavMap Database" (SQLite) reemplazada por "NavData API" con label de estado y botón TEST con resultado de conectividad y AIRAC vigente.
- **AppConfig** — claves nuevas: `navdata_api_url`, `navdata_api_key`. Fallback hardcodeado en `??` para garantizar operación sin entrada de configuración.
- **Idioma desbloqueado** — `LocalizationService` respeta la preferencia de idioma configurada en `App.config`. La versión anterior forzaba `es` incondicionalmente.

### Fixed

- **Decodificación BCD de frecuencias de radio (NAV1 / COM1)** — `DecodeNav1Bcd()` usaba la fórmula incorrecta `d3×100 + d2×10 + d1 + d0×0.1`, que leía 110.70 MHz como 107.00 MHz. Fórmula corregida: `100 + d3×10 + d2 + d1×0.1 + d0×0.01`. El formato real de FSUIPC es `(freq − 100) × 100` como número BCD de 4 dígitos. El error provocaba que la comprobación de ILS penalizara con −3 pts incluso con el ILS correctamente sintonizado.
- **Penalización "Below Minimums" falsa positiva** — `_belowMinimums` se activaba al cruzar la DA/DH pero nunca se limpiaba cuando el avión finalmente aterrizaba. Todo aterrizaje normal (que cruza la DA y aterriza) era penalizado. Corregido: `RegisterTouchdown()` establece `_belowMinimums = false`. La penalización solo se aplica si el avión cruzó la DA sin hacer un touchdown posterior.
- **`[[Score_CritMinimums]]` y `[[Score_CritLocalizer]]` en el desglose del score** — ambas claves de localización faltaban en `es.json` y `en.json`. Añadidas: `Score_CritMinimums` y `Score_CritLocalizer`.
- **Criterio "On-Time Departure" sin traducir** — faltaba la entrada en el mapa de criterios → claves de FlightManager. Añadido `"On-Time Departure" → "Score_CritDeparture"`.
- **Endpoints NavData en plural** — todos los fetch usaban la forma `/airports/` (que devolvía cuerpo vacío). Corregidos a la forma singular `/airport/` en los 6 endpoints.

---

## [0.4.17] — 2026-05-15

### Added

- **Detección de taxiway con filtro de rumbo** — `NearestTaxiway()` en `RunwayService` ahora recibe el rumbo del avión. Los segmentos cuyo bearing difiere más de 50° del rumbo del avión reciben una penalización ×2,5 en la distancia efectiva, haciendo que los taxiways transversales (ej. R al cruzar A∩R) no ganen sobre el taxiway alineado. Todos los puntos de llamada públicos (`FindNearestTaxiway`, `FindNextIntersection`) propagan el heading.

- **Histéresis de cambio de taxiway (3 ciclos)** — `HandleTaxiPositionUpdate` en `MainViewModel` requiere 3 detecciones consecutivas del mismo taxiway nuevo antes de registrar el cambio en el log. Los cambios de "next intersection" dentro del mismo taxiway siguen siendo instantáneos. Elimina la oscilación A↔D al pasar por intersecciones y los blips puntuales de taxiways transversales.

### Changed

- **Idioma forzado a español** — `LocalizationService` carga `es.json` incondicionalmente al iniciar, ignorando la preferencia guardada en Settings. El fallback por defecto también cambia de `en.json` a `es.json`.

---

## [0.4.16] — 2026-05-14

### Added

- **Log de inicio — simulador y aeronave** — las dos primeras líneas del log al confirmar el inicio del vuelo ahora muestran el simulador en uso (`🖥️ MSFS 2020`) y el tipo de aeronave con el desarrollador del add-on si se detecta (`✈️ B738  [PMDG]`). La detección de desarrollador (`GetAircraftDeveloper()` en `FsuipcService`) cubre 33 add-ons conocidos (PMDG, ToLiss, Fenix, FlyByWire/A32NX, iniBuilds, Majestic, Leonardo, Zibo, iFly, Carenado, Aerosoft, FlightFactor, Rotate, JustFlight, Headwind, Black Square, etc.).

### Changed

- **Landing lights — tolerancia de 500 ft** — la penalización por landing lights apagadas se activa a **9 500 ft AGL** (antes 10 000 ft). El OSD reminder de aviso previo sigue en 10 500 ft, dejando una ventana de 1 000 ft desde el aviso hasta la penalización.
- **Pitch Angle ideal — ampliado a 7°** — el rango sin deducción pasa de 1°–5° a **1°–7°** nose-up en touchdown. El umbral de flare excesivo (> 8° → −5 pts) no cambia.

---

## [0.4.15] — 2026-05-13

### Added

- **OSD reminder de landing lights a 10 500 ft AGL** — durante la fase **Descent**, al cruzar los 10 500 ft AGL con las landing lights apagadas, el OSD muestra `LANDING LT OFF` (Warning). Dispara una sola vez por descenso (flag `_landingLightReminderSent`); se resetea si el AGL sube por encima de 10 500 ft (go-around) o al iniciar un nuevo vuelo. No aplica penalización — es un aviso previo al check de −5 pts que sigue activo al cruzar los 10 000 ft.

---

## [0.4.14] — 2026-05-12

### Added

- **OSD — penalizaciones en tiempo real** — cada penalización de luces, QNH, overspeed y aproximación no estabilizada genera inmediatamente un mensaje OSD (`Warning` o `Critical`), sin esperar al envío del PIREP. Mensajes: `PENALTY  NAV/TAXI/STROBE/LANDING LT  −5 PTS`, `PENALTY  QNH  −5 PTS`, `OVERSPEED  XXX KTS`, `UNSTABILIZED  −N PTS`.
- **OSD — GO AROUND** — la detección de go-around emite `GO AROUND` (Warning) en el overlay.
- **OSD — TOUCH AND GO** — ya existía en v0.4.6; confirmado que se dispara correctamente desde `FlightManager.OnOsdMessage`.
- **Evento `OnOsdMessage` en FlightManager** — `public event Action<string, OsdSeverity> OnOsdMessage` propagado por `MainViewModel` hacia el overlay OSD existente, sin duplicar lógica.

### Removed

- **Seatbelts — eliminación completa** — eliminado todo el código, offsets FSUIPC (`0x0EC6`), propiedades (`IsSeatBeltSignOn`, `SeatBeltSign`), labels UI (`_lblSeatBelt`), claves de localización (`Status_SeatbeltOn`, `Status_SeatbeltOff`) y referencias en todos los archivos.
- **`Debug.WriteLine` — eliminación completa** — eliminadas todas las llamadas `System.Diagnostics.Debug.WriteLine` (activas y comentadas) en `ApiService`, `FsuipcService`, `FlightManager`, `MainViewModel`, `LandingLogService`, `FlightPlannerForm`, `PhpVmsFlightService`, `SimbriefEnhancedService` y `UIService`. El panel de salida de VS ya no recibe tráfico de depuración en runtime.
- **Entradas `[[...]]` en el log de vuelo** — eliminadas todas las llamadas `OnLog?.Invoke` que usaban claves de localización inexistentes (`Log_BidRemoved`, `Log_InitialFuel`, `Log_TimerStarted`, `Log_AircraftTypeInfo`, `Log_PirepCreated`, `Log_PhaseChanged`, `Log_BlockOffPushback`, `Log_TaxiOutAfterPush`, `Log_ApproachInfo`, y otras). El log solo muestra mensajes con clave válida o hardcoded.

---

## [0.4.13] — 2026-05-11

### Added

- **Localización de logs** — Migración de mensajes hardcodeados en el log (`MainViewModel.cs`) a archivos de idioma dinámicos (`es.json` y `en.json`) usando el servicio de localización existente.

### Changed

- **Idioma por defecto** — La aplicación ahora ignora temporalmente la preferencia de idioma de usuario (`App.config`) y fuerza la carga del archivo de idioma `en.json` (Inglés) al iniciar.

### Fixed

- **Error de compilación CS1061** — Se solucionó un problema en `FlightManager.cs` al acceder a `GearIsDown` en vez de la propiedad correcta `GearDown` definida en `RawTelemetryData`.
- **Lógica duplicada y CS0103** — Se limpió un bloque redundante no localizado en la evaluación `CheckStabilizedApproachGate` de `FlightManager.cs`, el cual incluía la variable no declarada `_isApproachUnstable`.

---

## [0.4.12] — 2026-05-11

### Added

- **Penalización por ausencia de base LNM** — si al iniciar el vuelo no está disponible la base de datos LittleNavMap, se notifica en el log inmediatamente (`⚠️ −14 pts: Base de datos LNM no disponible`) y posteriormente se aplican **14 puntos** fijos de penalización al score durante `FilePirep()` (cubre los criterios TDZ + Centreline que dependen de LNM). La penalización se calcula en `ScoringService` vía el nuevo campo `LnmDbAvailable` en `FlightScoreData`, inicializado desde `RunwayService.IsAvailable` en `StartFlight()`.

---

## [0.4.10] — 2026-05-11

### Fixed

- **Panel METAR congelado en "fetching..."** — dos bugs en cascada: (1) `MetarService.DoFetchAsync` tragaba excepciones y jamás llamaba `OnMetarUpdated` si ocurría cualquier error, dejando la UI indefinidamente en estado Fetching; (2) al corregir (1), fallaba `BeginInvoke` con `TargetParameterCountException` porque `MetarData[]`, al ser asignable a `object[]` por covarianza de arrays, se desempaquetaba como argumentos individuales en `BeginInvoke(Delegate, params object[])` en lugar de como un solo argumento. **Fix:** wrappers `SafeFetch*` independientes + `OnMetarUpdated` en `finally` + `ParseMetarToken` en try/catch + `BeginInvoke(..., new object[] { metars })` para evitar el desempaquetado de array. Añadido evento `OnLog` al servicio.

---

## [0.4.9] — 2026-05-11

### Fixed

- **Detección errónea de pista en aproximación con giros base→final** — `RunwayService.GetRunwayThreshold` rediseñado: ya no acepta cualquier pista con heading delta < 45°. Ahora exige **simultáneamente** heading delta ≤ 15°, desviación lateral al eje extendido ≤ 2 NM, y posición *antes* del umbral. Si ninguna pista cumple, devuelve null y la captura se difiere. En SKCC (RWY 16, llegada con giro 355°→159°) ya no se selecciona la pista 03 durante el giro de base.
- **Captura de aproximación no se reevaluaba** — la adquisición del threshold se hacía una sola vez al entrar en fase Approach, fijando una pista posiblemente incorrecta. Ahora `OnRawDataUpdated` llama a `GetRunwayThreshold` cada ciclo mientras `_approachThreshold == null`, garantizando que se adquiere solo cuando el avión está alineado y lateralmente cercano. Eliminado el bloque de "refinamiento a 5 NM" y el flag `_approachThresholdLocked`, ahora innecesarios.
- **Logs de diagnóstico en `SaveLandingRecord`** — antes fallaba silenciosamente (`Debug.WriteLine` solamente). Ahora emite eventos `OnLog` con detalle: servicio no disponible, buffer insuficiente (<3 puntos), excepción en `SaveFlight`, o éxito con número de vuelo y conteo de puntos.

---

## [0.4.8] — 2026-05-10

### Fixed

- **"Collection was modified" al salir** — race condition en `FsuipcService.Stop()`: el timer de polling se destruía y se llamaba a `Disconnect()` → `FSUIPCConnection.Close()` inmediatamente, sin esperar a que un callback `OnPollingTick` ya en vuelo (ThreadPool) terminara su `FSUIPCConnection.Process()`. La librería FSUIPC itera internamente su lista de offsets con `foreach` en `Process()`; `Close()` la modificaba concurrentemente → `InvalidOperationException: Collection was modified`. **Fix:** spin-wait con `Volatile.Read(ref _isPolling)` antes de `Disconnect()`, usando el flag de reentrancia ya existente.

---

## [0.4.7] — 2026-05-09

### Added

- **Versión en log de inicio de PIREP** — la línea `⏱️ PIREP created at: HH:mm:ss UTC` ahora incluye la versión del cliente: `(v0.4.7)`. La versión se lee desde `AssemblyInformationalVersionAttribute` via `AppInfo.Version`.

### Fixed

- **Pista paralela incorrecta en trayectoria de aproximación** — el threshold de re-evaluación para pistas paralelas bajó de 6 NM a **5 NM**. A 5 NM el avión ya está establecido en final y su posición lateral discrimina correctamente entre 14L/14R (o cualquier par paralelo), eliminando el falso offset de ~4 500 ft en la desviación de centreline del logbook.
- **G-Force log mostraba valor post-impacto ("1.02g Perfect")** — el sensor FSUIPC `0x11BA` ya ha decaído a ~1.0g cuando el evento `TouchdownDetected` se procesa. El log de touchdown ahora usa `GForcePeak` (`Math.Max(_peakGforceApproach, CurrentGForce)`) en lugar de `GForceAtTouch`, mostrando el pico real capturado durante el approach/impacto. La calificación cualitativa (Perfect/Normal/Hard/Crash) usa el mismo valor.

---

## [0.4.6] — 2026-05-08

### Added

- **OSD Overlay** — nueva ventana `OsdOverlayForm` (TopMost, sin borde, sin barra de tareas) que muestra notificaciones en pantalla superpuestas al simulador. Soporta 4 niveles de severidad: Info (azul), Success (verde), Warning (dorado), Critical (rojo parpadeante). Animación de fade-in/fade-out configurable.
- **OSD en fases de vuelo** — mensajes automáticos en cada transición de fase: `TAXI OUT`, `TAKEOFF ROLL`, `CRUISE`, `DESCENDING`, `APPROACH`, `ON BLOCK`, `TOUCH AND GO`.
- **OSD en touchdown** — muestra calificación cualitativa (`BUTTER / SMOOTH / NORMAL LANDING / FIRM LANDING / HARD LANDING / SLAM LANDING`), tasa en fpm y G-force en el momento del aterrizaje.
- **OSD al iniciar vuelo** — `ACARS ACTIVE` (Success) al pulsar START y confirmar inicio.
- **OSD al enviar PIREP** — muestra la puntuación final (`PIREP FILED — SCORE: XX/100`).
- **Botón MENU** — nuevo botón en la barra principal. Abre un menú desplegable con submenú **Test OSD** para probar los 4 niveles de severidad (Info / Success / Warning / Critical) sin necesidad de volar.
- **Configuración OSD en Settings** — nueva sección **OSD** en SettingsForm con:
  - Checkbox `Enable OSD` para activar/desactivar globalmente.
  - `Duration (s)` — tiempo de visualización (1–30 s).
  - `Screen index` — índice de pantalla donde mostrar el OSD (0 = primaria).
  - `Opacity (%)` — opacidad del overlay (10–100 %).
- **App.config — claves OSD:** `osd_enabled` (true), `osd_duration_seconds` (4), `osd_screen_index` (0), `osd_opacity` (90).

### Fixed

- **Botón START no se reactivaba tras enviar PIREP** — `SetActivePlan()` ahora emite `OnButtonStateChanged("START", enabled=true)` al cargar un nuevo plan, reactivando el botón inmediatamente sin esperar al siguiente ciclo de validación.
- **UI bloqueada ~120 s tras aceptar OFP** — `TriggerMetarFetchAsync()` y `DownloadOFPPdfAsync()` se llamaban con `var __ = ...` desde el hilo UI, capturando el `WindowsFormsSynchronizationContext`. Las continuaciones de los 4+ requests HTTP (incluido el doble bucle de `FetchNearestAsync`) se acumulaban en el message pump. Corregido usando `Task.Run(() => ...)` en ambas llamadas para ejecutar toda la cadena async en el ThreadPool sin capturar el contexto UI.
- **OSD mal posicionado en modo ventana del simulador** — la posición solo se calculaba en el constructor. Ahora se recalcula en cada llamada a `ShowMessage()` usando `Screen.Bounds` (área completa de la pantalla) en lugar de `WorkingArea` (que excluye la barra de tareas), garantizando posicionamiento correcto tanto en modo ventana como fullscreen.

---

## [0.4.5] — 2026-05-07

### Added
- **COM1/NAV1 en barra de estado** — dos nuevas pills muestran en tiempo real la frecuencia activa de COM1 (`COM 118.50`) y NAV1 (`NAV 111.30/135`). El curso solo aparece cuando el OBS está configurado (≠ 0°). Offset nuevo: `0x034E` BCD para COM1.
- **Autopilot fallback MSFS** — `apMaster` ahora es `true` si `0x07BC != 0` **ó** `0x07CC != 0`. Los add-ons complejos de MSFS (iFly, PMDG, FBW) no escriben el offset maestro `0x07BC`; el offset de modo de navegación (`0x07CC`) actúa como fallback.

### Fixed
- **Pistas paralelas — identificación errónea** — el threshold de aproximación se capturaba una sola vez al entrar en fase Approach, a veces con el avión a > 10 NM y sin estar alineado. Ahora se re-evalúa automáticamente la primera vez que el buffer detecta distancia < 6 NM; si el resultado es una pista distinta se limpia el buffer y se recarga el procedimiento de ILS/aproximación.
- **Touch-and-go falso por rebote** — un rebote (< 1 s en tierra) disparaba la detección de T&G y transitaba a fase Climb sin retorno. Se añadió un guard mínimo de **5 segundos en tierra** (`_touchdownTimestamp`) antes de aceptar un T&G; los rebotes se ignoran.
- **Cuenta atrás de salida — lógica de colores** — nueva escala: rojo (> 10 min antes) → amarillo (5–10 min) → verde (ventana ±5 min) → amarillo (5–10 min de retraso) → rojo (> 10 min de retraso).
- **Penalización IVAO al iniciar TaxiOut** — el check de IVAO se trasladó al inicio de la fase TaxiOut (independientemente de la fase previa). El diálogo bloqueante de StartFlight se reemplazó por un simple aviso informativo en el log. La penalización de −5 pts se aplica únicamente si el piloto no está en IVAO al comenzar el rodaje.

---

## [0.4.4]

### Added

- **ILS detection** — `RunwayService.GetIlsForRunway(airport, runway)` queries the LittleNavMap DB for the ILS serving the landing runway, filtering LOC-only procedures via `gs_pitch > 0.1`. Returns frequency (MHz), localizer course, glideslope pitch, and threshold position.
- **Approach type identification** — `RunwayService.GetApproachType(airport, runway)` returns the best available approach procedure (ILS > RNAV > other) with an `HasVerticalGuidance` flag. Falls back to `runway_end_id` join when `approach.runway_name` is empty.
- **Approach fix waypoint sequencing** — `RunwayService.GetApproachFixes(approachId)` loads IF, FAF, and MAP fixes from `approach_leg`. During approach, FlightManager logs each fix as the aircraft passes within 0.5 NM.
- **NAV1 frequency reading** — FSUIPC offset `0x0350` (BCD-encoded, `Offset<short>`) decoded to MHz. Offset `0x0C4E` reads the OBS / ILS course. Both fields added to `RawTelemetryData`.
- **ILS tuning check** (at 1000 ft AGL gate) — if an ILS approach was detected, FlightManager verifies that NAV1 is tuned to the correct frequency (±0.05 MHz tolerance). Logs confirmation or warning; non-compliance increments `LocalizerViolations`.
- **Localizer alignment scoring** — monitors aircraft heading vs ILS course below 500 ft AGL. Deviations > 5° are counted (max 2). `ScoringService` deducts up to **−5 pts** combining ILS-not-tuned (−3) and heading deviations (−1 each, max −2).
- **Decision altitude (DA) and minimums check** — DA computed as threshold elevation + 200 ft. If the aircraft descends below DA without landing, `BelowMinimums` is flagged → **−5 pts** in scoring.
- **Approach data loaded in MainViewModel** — `LoadApproachData(airport, runwayName)` runs on `Task.Run` when the Approach phase starts, calling `_flightManager.SetApproachData(ils, approach, fixes)` to wire up all ILS/approach scoring.

### Changed

- `FlightScoreData` gains three new fields: `IlsTunedCorrectly` (bool, default `true`), `LocalizerViolations` (int), `BelowMinimums` (bool). Included in `FilePirep()` score assembly and `Reset()`.
- `ScoringService` gains two new criteria: **Localizer Alignment** (max −5 pts) and **Minimums Compliance** (−5 pts). Max raw deduction sum now 130 pts; final score still clamped to 0.
- `FlightManager.CheckStabilizedApproachGate` extended with ILS tuning verification (criterion 7); QNH check renumbered to criterion 9.
- Touch-and-go reset block extended to clear ILS/approach state for the second circuit.

## [0.4.3]

### Fixed

- **Logbook columnas vacías (Flight, Route, RWY, VS, G)** — `FilePirep()` llama internamente a `ResetFlightState()` que pone `_activePlan = null` y resetea todos los campos de touchdown antes de retornar. `SaveLandingRecord()` se ejecutaba después y encontraba todo vacío. Solucionado con `SnapshotLandingRecord()`, que captura el plan y los datos de touchdown **antes** de awaitar `FilePirep()`. El `Score` se añade después porque `LastFlightScore` es la única propiedad que `ResetFlightState()` no borra.

## [0.4.2]

### Added

- **Penalización por salida fuera de horario** — se descuentan 5 puntos si el vuelo despega con más de ±10 minutos respecto al STD (`sched_out`). El aviso se emite en el log en el momento del Blocks Off con el delta y la dirección (`early`/`late`). Se añade el criterio `On-Time Departure` al desglose del score.
- **AGL real en crucero (Enroute)** — el AGL durante la fase de crucero ahora se calcula restando la elevación real del terreno bajo el avión (offset FSUIPC `0x0020`) en lugar de la elevación del aeropuerto de origen. Corrección visible cuando se sobrevuela zonas montañosas.
- **QNH verificado en aterrizaje** — el QNH de destino ahora se comprueba en el gate de 1000 ft AGL (igual que la salida se comprueba en TakeoffRoll). Antes se comprobaba al entrar en la fase Approach (~20 NM del aeropuerto), demasiado pronto para que el piloto hubiera sintonizado el QNH local. La penalización máxima por QNH sube de 5 a **10 pts** (5 salida + 5 llegada independientes).
- **Recuperación Descent → Climb** — si en fase Descent el VS supera +500 fpm durante 20 s y el avión no está en zona de aproximación al destino, la fase vuelve automáticamente a Climb. Cubre el caso de una falsa transición por cambio de QNH en salidas con altitud restringida.

### Changed

- **Umbrales de detección Climb/Enroute → Descent más tolerantes** — el umbral de VS para iniciar el descenso sube de −100/−300 fpm a **−500 fpm**, y el debounce de 10 s a **20 s**, en ambas fases (Climb y Enroute). Evita que un cambio de QNH (~100 fpm de fluctuación aparente) dispare erróneamente la transición a Descent.

### Fixed

- **GEAR UP sin AGL** — la transición "Gear UP" no mostraba altitud AGL en el log. Se debía a una condición de carrera entre el evento `GearChanged` y la actualización de telemetría (`UpdateTelemetry`). Corregido leyendo `_fsuipc.CurrentAltitudeFeet` directamente en el handler, sin depender de `FlightManager.CurrentAGL`.
- **Pista paralela incorrecta en captura de aproximación** — `GetRunwayThreshold()` solo usaba el rumbo para identificar la pista, seleccionando 14L en lugar de 14R (SKBO) porque ambas tienen rumbos similares. Ahora se añaden `lat`/`lon` del avión como parámetros y se usa la distancia lateral al eje de cada pista como desempate, eligiendo la pista cuyo eje esté más cerca del avión.
- **Flight Planner mostraba solo la primera página de vuelos** — `GetAvailableFlightsFromAirport` ahora itera todas las páginas de `api/flights` (paginación Laravel igual que la flota), mostrando la lista completa de vuelos disponibles.
- **Columnas del Flight Planner no ordenaban** — el grid de vuelos disponibles ahora ordena por cualquier columna al hacer clic en el encabezado (segundo clic invierte el orden). El orden inicial al cargar es por número de vuelo ascendente.
- **App.config sobreescrito al compilar Release** — en configuración Release, MSBuild ya no copia `App.config` → `vmsOpenAcars.exe.config` (`<AppConfig></AppConfig>` en el PropertyGroup de Release), y se eliminó el PostBuildEvent que copiaba `App.Release.config`. El archivo de configuración de producción en `bin\Release\` queda intacto.

## [0.4.1]

### Fixed

- **Cuenta atrás ETD usa blocks-off real** — el countdown del panel FMA ahora cuenta hacia el tiempo de `sched_out` (blocks off / inicio de pushback) en lugar de `sched_off` (wheels off). La diferencia era exactamente el taxi_out de SimBrief (ej: 20 min), mostrando la hora de despegue en lugar de la hora de salida. `SimbriefPlan.ScheduledOutTime` mapea `times.sched_out`; `ScheduledOffTime` mantiene `times.sched_off` para la fecha en el FMA.

## [0.3.19]

### Added

- **Debounce en Spoilers** — se implementó un sistema de filtrado (debounce) de 1.5 segundos para el estado de los spoilers, evitando falsos positivos o parpadeos en el log y en el estado del vuelo por ruido en la señal del simulador.
- **Excepción Beacon para switch compartido** — la penalización de Beacon apagado en vuelo ya no aplica a aeronaves con switch único beacon/strobe (ej: Q400/Dash 8), donde encender strobes apaga automáticamente el beacon. La lista de excepción está en `BeaconStrobeSharedAircraft`.
- **Taxi position: calle actual + próxima intersección** — el log de rodaje ahora muestra la calle por la que se rueda y la siguiente intersección por delante. Ej: `↳ CALLE C, Próximo a B7`. Cuando no hay intersección a la vista, muestra solo la calle actual como antes. Implementado en `RunwayService.FindNextIntersection()`. Además, durante `AfterLanding` se detecta la calle por la que se abandona la pista (`🛬 PISTA DESOCUPADA por CALLE K7`) y se activan los updates de posición en rodaje de llegada.
- **Log de inicio de captura de aproximación** — al comenzar la captura de telemetría de aproximación (AGL < 3000 ft), se registra en el log la pista detectada, el AGL y la distancia al umbral. Ej: `📡 INICIO CAPTURA APROX: PISTA 24R | AGL 2850 ft | Dist 8.3 NM`.

### Changed

- **Refactor Debounce** — `DebounceLight` renombrado a `DebounceState` genérico, aceptando el tiempo de debounce como parámetro. Todas las luces y spoilers comparten ahora la misma lógica.

### Fixed

- **Falso touchdown en despegue** — la detección de tomacontacto ahora solo se activa en fases de Descent, Approach o Landing. Los flickers del flag `SimOnGround` de FSUIPC durante Takeoff/Climb (rebotes en rotación, glitches del simulador) ya no disparan una transición incorrecta a `AfterLanding`.
- **Detección de pista en aeropuertos con pistas paralelas** — `FindTouchdownRunway()` ahora verifica que la posición del avión esté dentro de la huella (`WithinFootprint`) de la pista. Si la pista más cercana por rumbo no contiene el punto de toma de contacto, busca otra pista con rumbo similar que sí lo contenga. Corrige falsos positivos en KLAX (24L/24R), LEBL (25L/25R), etc.

## [0.3.18]

### Added

- **FMA Line 3** — ahora muestra la ruta completa del vuelo extraída de SimBrief (`RTE ...`).
- **Tercer escenario de Blocks Off** — se añadió el registro automático del "Blocks Off" al encender motores durante la fase de `Boarding`. Esto permite capturar correctamente el inicio del vuelo en aviones pequeños o posiciones de parking que no requieren pushback.
- **Penalización luz Beacon** — si la luz Beacon se apaga en cualquier momento mientras el avión está en vuelo (AIRBORNE), se registrará una infracción en la telemetría y penalizará el puntaje final.
- **Scoring en el Landing Log** — la puntuación final de cada vuelo ahora se expone en `LastFlightScore` y se almacena correctamente en la base de datos `landing_log.sqlite` al registrar el aterrizaje.

### Fixed

- **Fallo en captura de telemetría de aproximación** — resuelto un problema crítico donde el filtro de captura dependía del radar altímetro nativo (el cual enviaba valores inválidos desde un offset erróneo). Ahora se utiliza el `CurrentAGL` interno (MSL − elevación del aeropuerto de destino), asegurando que los datos de aproximación (por debajo de 3000 ft) se registren siempre de forma fiable.
- **Detección fallida de pista en aproximación (Approach Threshold)** — el sistema antes comprobaba el rumbo del avión *únicamente* en el momento exacto en el que pasaba a fase de Aproximación. Ahora verifica continuamente hasta encontrar alineación, evitando que los datos de aterrizaje se pierdan si el avión entró a la aproximación volando en viento en cola o desviado.
- **Cálculo AGL en ascenso** — resuelto el bug en la fase de CLIMB donde el AGL calculado retornaba el MSL debido a que la elevación de origen no era provista adecuadamente por SimBrief.
- **Offset del Radar Altímetro** — se ha cambiado de `0x0234` (ADF2) al offset correcto `0x31E4` de FSUIPC, permitiendo leer la distancia radial real hacia el suelo en fases críticas.
- **Regla de 10,000 ft usando MSL** — la penalización de luces por debajo de los 10,000 pies ha sido actualizada para utilizar la altitud AGL real del terreno en lugar del MSL. Ya no penaliza falsamente al despegar de aeropuertos de gran altitud como Bogotá (SKBO).
- **AGL en modo Crucero** — el AGL en fase de vuelo crucero (`Enroute`) ahora reporta el MSL de forma predeterminada como lo solicitan los pilotos, en lugar de intentar leer el radar altímetro a grandes altitudes.
## [0.3.17]

### Added

- **Resumen del plan al inicio del log** — al pulsar START, el primer registro del log muestra los datos del plan de SimBrief: vuelo, ruta, aeronave, matrícula, fecha, PAX, combustible, nivel de crucero y carga. Mismo contenido que el panel FMA.
- **AGL en eventos de luces** — los cambios de NAV, STROBE, LANDING y BEACON ahora incluyen la altitud AGL cuando el avión está por encima de 50 ft. Ejemplo: `💡 LANDING lights ON (10 240 ft AGL)`.
- **AGL en cambios de tren de aterrizaje** — `🛬 Gear UP` y `🛬 Gear DOWN` incluyen la altitud AGL en las mismas condiciones.

### Fixed

- **Detección de taxiways y holding short no funcionaba** — `RunwayWidthScale` reducido de `1.5` a `1.0`. Con el multiplicador anterior, el footprint de detección de pista se extendía hasta los taxiways paralelos cercanos (p. ej. TWY A a 36 m del eje en SKBO), lo que hacía que el avión fuera detectado como "en pista" durante todo el rodaje y se saltara la detección de taxiways, holding short y entrada a pista.

---

## [0.3.16]

### Added

- **Landing Analysis** — nuevo sistema de historial de aterrizajes almacenado en una base de datos SQLite local (`landing_log.sqlite`).
  - Botón **LOGBOOK** en la pantalla principal abre el historial de vuelos.
  - Cada aterrizaje registra: vertical speed, G-force, distancia al umbral, desviación de centreline, score y METAR.
  - Durante la fase de aproximación (AGL < 3 000 ft) se captura automáticamente la trayectoria cada 2 segundos.
- **LandingAnalysisForm** — ventana de análisis con 4 gráficos interactivos:
  - *Vertical Profile*: AGL vs distancia al umbral, con línea de referencia de planeo 3°.
  - *Lateral Deviation*: desviación de centreline (±ft) con línea cero.
  - *IAS*: velocidad indicada con línea de Vref promedio.
  - *Vertical Speed*: VS en fpm con línea cero.
  - Suavizado Gaussiano aplicado a Lateral, IAS y VS para mejorar la visualización.
  - Eje X invertido: 5 NM a la izquierda → umbral a la derecha.
- **Modo comparación** — selecciona entre 2 y 5 vuelos en el LOGBOOK y pulsa **COMPARE** para superponer sus trayectorias en los 4 gráficos, cada vuelo con un color distinto.
- **Borrado de registros** — botón **DELETE** en el LOGBOOK con confirmación antes de eliminar (soporta selección múltiple).

### Setup — Landing Log database

> Esta configuración es necesaria la primera vez que se usa el LOGBOOK.

1. Abre **Settings** y ve a la sección **Landing Log**.
2. Haz clic en el botón **[...]** y selecciona un archivo `.sqlite` existente, o escribe un nombre nuevo (p. ej. `landing_log.sqlite`) para crearlo.
3. Guarda la configuración. La base de datos se crea automáticamente al registrar el primer aterrizaje.

La base de datos es un archivo SQLite estándar; puedes hacer copias de seguridad simplemente copiando el archivo.

### Fixed

- El selector de archivo de la base de datos de Landing Log usaba `SaveFileDialog` (pedía confirmación de reemplazo); reemplazado por `OpenFileDialog` con `CheckFileExists = false`.
- Error en runtime al comparar dos vuelos con el mismo callsign: nombre de serie duplicado en `SeriesCollection` — corregido añadiendo índice al nombre de cada serie.
- Botón **SEED DEMO DATA** ahora solo visible en builds Debug (`#if DEBUG`).
- Proyecto `Updater` no compilaba por `App.config` faltante.

---

## [0.3.15]

### Added

- Ground operations tracking feature added to vmsOpenAcars using the LittleNavMap DB.

## [0.3.14]

### Added

- LittleNavMap SQLite runway scoring
  (touchdown zone and centerline deviation), touch-and-go detection, and LNM DB availability checks

### Fixed

- METAR retrieval process indicator while awaiting server response
- Touch-and-go detection. So a second landing after liftoff from AfterLanding captures fresh touchdown data for scoringTouch-and-go detection and storage of the PIREP with the second landing

Resumen de todo lo implementado:

Db/RunwayService.cs — consulta LNM SQLite: encuentra el aeropuerto por ICAO, itera los runway ends, elige el que esté
dentro de ±45° del heading del avión, y proyecta el punto de touchdown sobre el eje de pista con geometría flat-earth
para calcular distancia al umbral y desviación de centreline.

FlightManager — captura CurrentHeading cada ciclo de telemetría; en RegisterTouchdown guarda lat/lon/heading del
momento exacto; SetRunwayTouchdownData() permite al ViewModel inyectar los resultados; todo se resetea en
ResetFlightState y al detectar touch-and-go.

MainViewModel — al recibir TouchdownDetected (que ya incluye lat/lon/heading precisos de FSUIPC), lanza Task.Run →
LookupRunwayData → log de resultados → SetRunwayTouchdownData en FlightManager.

ScoringService — dos criterios nuevos:

- Touchdown Zone: 0 pts ≤1500 ft · −3 pts 1500-2500 ft · −7 pts >2500 ft
- Centreline: 0 pts ≤10 ft · −3 pts 10-30 ft · −7 pts >30 ft

SettingsForm — nueva sección "NavMap Database" con campo de texto + botón "..." para seleccionar el archivo, guardado
en la clave lnm_db_path.

## [0.3.12]

### Added

- METAR feature fully implemented. Added MetarService state machine,4 panels (ORIG/DEST/ALT/ENRT), MetarDecodeForm decode popup, and 4-station
- IVAO online verification with score penalty

## [0.3.11]

### Added

- OFP basic data on FMA panel

### Fixed

- duplicate log events
- false takeoff detection after landing
- Phase self-transitions
- and fuel unit inconsistencies
- MSFS autopilot display fix. AP state changes now log to the visible flight log
- AGL cruise
- MACH under FL250
- Countdown:
  - Only visible when a plan is loaded and the phase is Idle or Boarding
  - Disappears when the block off is registered (phase changes to Pushback or later)
  - Green → More than 5 minutes remaining (H:MM if > 1 hour, MM:SS if < 1 hour)
  - Yellow → 5 minutes or less remaining
  - Red → Delayed, displays the +MM:SS of the delay

## [0.3.10]

### Fixed

- stabilized approach fixes
- Q400 vmo -> 285

## [0.3.2]

### Added

- METAR check on departure and arrival

### Fixed

- Fuel units (from simbrief plan)
- fUEL INI: 2115 KGS (REAL 4843, PARECE CONVIERTE A KG NUEVAMENTE, CREYENDO QUE ESTAN EN LBS)
- USADO: 0, ff 83KG (REAL 89.7)
- N2 EN CERO
- EGT 3493 (723 REAL)
- FF 41 KG/H (REAL 1.04)

## [0.3.0]

### Added

- Aircraft category detection
- Adaptive information layout by category

### Fixed

- DIST: 0/0. fixed
- Fuel units and fuel flow value 0. fixed
- AGL varies chaotically. Fixed
- Strobe appears ON when NAV is in the center/OFF position. Fixed

---

## [0.2.9] - 2026-04-20

### Added

- Real-time Flight Information Panel
- Flap indicator by aircraft type (Airbus/Boeing)
- Engine panel with N1/N2/EGT/FF
- Light detection (NAV, BEACON, LANDING, TAXI, STROBE)
- Autobrake detection by family

### Changed

- `CurrentFlapsPosition` from double to string
- `UpdateFlightInfoPanel()` now uses usa `FlapsLabel`

### Fixed

- Flaps always displaying "UP"
- Fuel_used set to zero in PIREP
- OnBlock without parking brake and engines off
