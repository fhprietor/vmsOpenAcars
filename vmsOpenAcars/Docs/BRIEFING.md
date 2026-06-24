# vmsOpenAcars — Guía del Usuario

**Versión 0.7.6**

vmsOpenAcars es un cliente ACARS de escritorio para simuladores de vuelo en PC bajo Windows que conecta tu simulador con aerolíneas virtuales basadas en phpVMS 7. Lee los datos del simulador en tiempo real via FSUIPC/XUIPC, detecta automáticamente las fases de vuelo, califica tu actuación con 14 criterios de scoring y envía el PIREP al servidor de tu aerolínea.

---

## Índice

- [1. Requisitos](#1-requisitos)
- [2. Configuración inicial (Settings)](#2-configuración-inicial-settings)
- [3. Interfaz principal](#3-interfaz-principal)
- [4. Flujo de un vuelo típico](#4-flujo-de-un-vuelo-típico)
- [5. Scoring de vuelo](#5-scoring-de-vuelo)
- [6. METAR](#6-metar)
- [7. OSD Overlay](#7-osd-overlay)
- [8. Mapa en movimiento (MAP)](#8-mapa-en-movimiento-map)
- [9. LOGBOOK y Landing Analysis](#9-logbook-y-landing-analysis)
- [10. Solución de problemas](#10-solución-de-problemas)

---

## 1. Requisitos

| Requisito | Detalle |
|---|---|
| Simulador | Ver tabla de simuladores compatibles abajo |
| FSUIPC | Instalado y activo (versión gratuita es suficiente) |
| NavData API | API Key proporcionada por tu aerolínea virtual (para scoring de pista) |
| Cuenta phpVMS 7 | API Key generada en tu perfil de la aerolínea virtual |
| Cuenta SimBrief | Usuario de SimBrief (gratuito) |
| Conexión a internet | Para comunicarse con phpVMS, SimBrief y METARs |

### Simuladores compatibles

vmsOpenAcars se comunica con el simulador a través de **FSUIPC**, por lo que es compatible con todos los simuladores que soporta dicha librería:

| Simulador | Versión de FSUIPC requerida |
|---|---|
| Microsoft Flight Simulator 2020 | FSUIPC 7 |
| Microsoft Flight Simulator 2024 | FSUIPC 7 |
| Prepar3D v4 | FSUIPC 5 o 6 |
| Prepar3D v5 / v6 | FSUIPC 6 |
| Prepar3D v1 / v2 / v3 | FSUIPC 4 |
| FSX / FSX: Steam Edition | FSUIPC 4 |
| X-Plane 11 | XUIPC (plugin para X-Plane) |
| X-Plane 12 | XUIPC (plugin para X-Plane) |

> Instala la versión de FSUIPC que corresponda a tu simulador. La versión gratuita es suficiente. Para X-Plane, instala el plugin **XUIPC** en lugar de FSUIPC.

---

## 2. Configuración inicial (Settings)

Haz clic en el botón **SETTINGS** para abrir el diálogo de configuración. Todos los cambios se guardan en `vmsOpenAcars.exe.config`.

### Sección phpVMS

| Campo | Descripción |
|---|---|
| API URL | URL base de tu aerolínea virtual, con `/` al final. Ej: `https://miaerolinea.com/` |
| API Key | Tu clave personal de phpVMS (generada en tu perfil de piloto) |

### Sección SimBrief

| Campo | Descripción |
|---|---|
| SimBrief User | Tu nombre de usuario de SimBrief (pilot ID o alias, no el correo) |

### Sección NavData API

vmsOpenAcars consulta el servicio **NavData API** para calcular con precisión la distancia al umbral de pista, la desviación de centreline, y detectar el ILS de la pista de aterrizaje. Sin una API key válida el scoring básico funciona, pero los criterios **Touchdown Zone**, **Centreline Deviation**, **Localizer Alignment** y **Minimums Compliance** no se evaluarán.

| Campo | Descripción |
|---|---|
| NavData API URL | URL base del servicio NavData de tu aerolínea virtual |
| NavData API Key | Clave de acceso proporcionada por tu aerolínea virtual |
| Origin Domain | Dominio de la aerolínea (para validación de origen HTTP) |

Pulsa **TEST** para verificar la conectividad y la validez de la API key. El botón muestra en verde el ciclo AIRAC vigente si todo es correcto, en naranja si el servicio está activo pero la key es inválida, y en rojo si el servicio no es alcanzable.

Pulsa **REFRESH NAVDATA** para invalidar manualmente la caché de procedimientos (SIDs, STARs, aproximaciones). Úsalo cuando la aerolínea haya corregido datos de NavData dentro del mismo ciclo AIRAC y necesites que la app descargue los datos actualizados sin tener que esperar al próximo ciclo. No afecta a los espacios aéreos ni requiere reiniciar la app.

### Sección Landing Log

El LOGBOOK guarda el historial de tus aterrizajes con trayectoria de aproximación en una base de datos SQLite local.

1. En Settings → **Landing Log**, haz clic en `[...]`.
2. Selecciona un archivo `.sqlite` existente o escribe un nombre nuevo para crearlo (p. ej. `landing_log.sqlite`).
3. La base de datos se crea y migra automáticamente al primer uso.

> Puedes hacer copias de seguridad simplemente copiando el archivo `.sqlite`.

### Sección OSD

Controla el overlay de notificaciones que aparece sobre el simulador.

| Campo | Descripción |
|---|---|
| Enable OSD | Activa o desactiva el overlay globalmente |
| Duration (s) | Tiempo que permanece visible cada notificación (1–30 s) |
| Screen index | Índice de la pantalla donde se muestra (0 = pantalla principal, 1 = segunda pantalla, etc.) |
| Opacity (%) | Opacidad del overlay (10–100 %) |

### Sección Cabin Announcements

vmsOpenAcars puede reproducir anuncios de cabina pregrabados durante las distintas fases del vuelo.

| Campo | Descripción |
|---|---|
| Enable Cabin Announcements | Activa o desactiva los anuncios. Se aplica y guarda **al instante**, sin pulsar Save. |
| Volume | Slider 0–100 % (default 80 %). Se aplica **en tiempo real** sobre el audio en curso y se guarda automáticamente sin pulsar Save. |

El botón **TEST** abre un menú con las 7 fases disponibles (`boarding`, `taxi_out`, `on_runway`, `cruise`, `top_of_descent`, `approach`, `taxi_in`). Al seleccionar una, el sistema detiene el anuncio en curso, descarga el nuevo si hace falta y lo reproduce mostrando el formato del audio y el tamaño del archivo.

> Los anuncios solo están disponibles si la NavData API de tu aerolínea los proporciona. Sin API key válida, o si la aerolínea no dispone de audios, la función queda silenciosa (el vuelo continúa con normalidad).  
> En aeronaves con capacidad inferior a 40 pasajeros los anuncios se desactivan automáticamente.

---

## 3. Interfaz principal

```
┌────────────────────────────────────────────────────────────────────┐
│  [FMA]  Línea 1: Vuelo / ruta / CI / fecha / matrícula / tipo      │
│         Línea 2: PAX / FUEL / TRIP FUEL / CARGO / FL / WIND / ISA  │
│                                          PHASE BOARDING │ GROUND   │
│                                          cuenta atrás salida        │
├────────────────────────────────────────────────────────────────────┤
│  [FLIGHT INFORMATION]  Datos del plan activo                        │
├────────────────────────────────────────────────────────────────────┤
│  [GAUGES / ENGINE]  Indicadores de vuelo en tiempo real             │
├────────────────────────────────────────────────────────────────────┤
│  [INCOMING MSG]  Log de eventos del vuelo                           │
├────────────────────────────────────────────────────────────────────┤
│  [STATUS]  GPS • Conexión sim • ACARS • Aeropuerto • COM • NAV       │
├────────────────────────────────────────────────────────────────────┤
│  LOGIN  SETTINGS  SIMBRIEF  METAR  LOGBOOK  DISPATCH  MAP  MENU  START │
└────────────────────────────────────────────────────────────────────┘
```

### Panel FMA

- **Línea 1:** identificador del vuelo, par ICAO/IATA, Cost Index, fecha, matrícula y tipo.
- **Línea 2:** PAX, combustible en rampa (FUEL), combustible de vuelo (TRIP), carga, nivel de crucero, viento promedio e ISA.
- **Columna derecha:** fase actual y estado `GROUND` / `AIRBORNE`. Mientras el avión está en boarding muestra una **cuenta regresiva hasta la hora de salida** (Blocks Off programado).

### Botón MENU

Abre un menú con opciones adicionales:

- **Test OSD → Info / Success / Warning / Critical** — muestra un mensaje de prueba del overlay de notificaciones en cada nivel de severidad. Útil para verificar la posición y apariencia del OSD antes de volar.

---

## 4. Flujo de un vuelo típico

### 4.1 Login

1. Inicia el simulador y carga tu aeronave en el aeropuerto de salida.
2. Abre vmsOpenAcars y haz clic en **LOGIN**.
3. Tu nombre de piloto y aeropuerto base aparecerán en el panel STATUS.

### 4.2 Selección de vuelo con el Flight Planner

1. Haz clic en **SIMBRIEF** para abrir el Flight Planner.
2. El planner tiene dos pestañas:
   - **My Bids** — tus vuelos reservados en phpVMS que salen de tu aeropuerto actual.
   - **Available Flights** — todos los vuelos disponibles desde tu aeropuerto, ordenados por número de vuelo. Haz clic en cualquier encabezado de columna para reordenar.
3. Selecciona un vuelo. La lista de aeronaves disponibles se cargará automáticamente.
4. Elige la aeronave con la que quieres operar.

### 4.3 Plan de vuelo con SimBrief

1. Haz clic en **PLAN IN SIMBRIEF**. Se abrirá tu navegador con el dispatch pre-cargado.
2. Ajusta lo que necesites y genera el OFP en SimBrief.
3. Vuelve a vmsOpenAcars y haz clic en **FETCH OFP**.
4. El sistema descarga y valida el plan (origen, destino, tipo, matrícula, antigüedad máx. 2 h).
5. Si la validación pasa, haz clic en **ACCEPT** para cargar el plan.

> **Validación de tipo de aeronave:** si el tipo ICAO del OFP (ej. `B737`) no coincide con el que reporta el simulador (ej. `B738`), aparecerá un log advisory en amarillo al cargar el plan. Al pulsar **START**, si el desacuerdo persiste, se mostrará un diálogo de confirmación: puedes continuar el vuelo de todas formas o cancelar para corregir el plan en SimBrief.

### 4.4 Inicio del vuelo

El botón **START** se habilita cuando:

- ✅ Simulador conectado via FSUIPC
- ✅ Plan de vuelo cargado y aceptado
- ✅ Posición GPS válida (estás en el aeropuerto correcto, dentro de ~5 km)

Haz clic en **START**. La aplicación enviará un prefile a phpVMS y comenzará el seguimiento.

> Para cancelar, haz clic en **ABORT** y confirma. El PIREP se eliminará del servidor.

### 4.5 Durante el vuelo

#### Fases detectadas automáticamente

| Fase | Condición de entrada |
|---|---|
| Boarding | Plan activo, avión en tierra con freno de estacionamiento |
| Pushback | Movimiento lento hacia atrás |
| TaxiOut | Movimiento hacia delante antes del despegue |
| TakeoffRoll | Velocidad de suelo > 30 kt con freno liberado |
| Takeoff | Liftoff detectado |
| Climb | VS positivo sostenido |
| Enroute | Crucero estabilizado |
| Descent | Descenso sostenido (VS < −500 fpm durante 20 s) |
| Approach | Descenso final hacia pista |
| Landing | Touchdown detectado |
| AfterLanding | Deceleración tras aterrizaje |
| TaxiIn | Rodaje hacia puerta |
| OnBlock | Freno de estacionamiento puesto con motores apagados |
| Completed | Vuelo listo para enviar PIREP |

#### Ground operations

Durante el rodaje el sistema informa en el log: pista en uso al alinearse, taxiways recorridos, holding points y puerta/estacionamiento de llegada.

#### Trayectoria de aproximación

A partir de **3 000 ft AGL** en fase Approach, el sistema captura un punto de trayectoria cada 2 segundos (altitud, velocidad, VS, desviación lateral) para los gráficos del LOGBOOK.

#### Anuncios de cabina

Si la aerolínea dispone de audios y los anuncios están activados en Settings, vmsOpenAcars reproduce automáticamente un chime seguido del anuncio de cabina en cada fase:

| Anuncio | Trigger |
|---|---|
| Boarding | Al iniciar el vuelo (START) |
| Taxi Out | Al entrar en fase TaxiOut |
| On Runway | Al encender luces de aterrizaje o strobes con GS ≤ 40 kt |
| Cruise | Fase Enroute + AGL > 10 000 ft durante 30 s continuos |
| Top of Descent | Al entrar en fase Descent |
| Approach | Al entrar en fase Approach |
| Taxi In | Al entrar en fase TaxiIn |

En vuelos **internacionales** con aerolínea hispanohablante los anuncios se reproducen en inglés seguidos de español. En vuelos **domésticos** solo suena el idioma nativo de la aerolínea.

#### Frecuencia de envío de posición

| Fase | Intervalo |
|---|---|
| En tierra (rodaje) | 30 s |
| Takeoff / Landing | 2 s |
| Climb / Descent / Approach | 5 s |
| Crucero | 15 s |

### 4.6 Finalización y envío del PIREP

1. Cuando llegues a puerta con motores apagados, la fase cambiará a **Completed**.
2. El botón cambiará a **SEND PIREP**.
3. Haz clic. Se envía el PIREP a phpVMS con combustible usado, tasa de aterrizaje, tiempo de vuelo, distancia y **score**.
4. Si el LOGBOOK está configurado, el aterrizaje se guarda automáticamente con su trayectoria.

---

## 5. Scoring de vuelo

El score parte de **100 puntos** y aplica deducciones según **14 criterios**. Adicionalmente existe una **bonificación** por taxi en single-engine. El valor final (0–100) se envía con el PIREP a phpVMS y queda registrado en el LOGBOOK.

### Tabla de criterios

| Criterio | Máx. deducción | Regla |
|---|---|---|
| **Landing Rate** | −40 pts | ≤ 150 fpm → 0 · ≤ 250 → −5 · ≤ 350 → −15 · ≤ 450 → −25 · ≤ 650 → −35 · > 650 → −40 |
| **G-Force** | −15 pts | ≤ 1.5 g → 0 · ≤ 1.7 g → −7 · > 1.7 g → −15 |
| **Bank Angle** | −10 pts | ≤ 2° → 0 · ≤ 5° → −5 · > 5° → −10 |
| **Pitch Angle** | −10 pts | 1°–7° nose-up → 0 (ideal) · < −2° → −10 · −2° a 1° → −5 · > 8° → −5 |
| **Overspeed** | −15 pts | 0 eventos → 0 · 1 evento → −7 · ≥ 2 eventos → −15 · Exento si COM1 en ATC IVAO ⁴ |
| **Lights Compliance** | −10 pts | −5 pts por violación (cap −10). Ver detalle abajo. |
| **Stabilized Approach** | −15 pts | Evaluado al cruzar 1 000 ft AGL en descenso. Ver detalle abajo. |
| **QNH Compliance** | −10 pts | −5 pts si Δ QNH > 2 hPa. Verificado **dos veces**: salida (TakeoffRoll) y llegada (gate 1 000 ft AGL). |
| **IVAO Offline** | −5 pts | −5 si el piloto no está conectado a IVAO al iniciar el TaxiOut |
| **On-Time Departure** | −5 pts | −5 si el Blocks Off real difiere más de 10 min del STD programado |
| **Touchdown Zone** | −7 pts | ≤ 1 500 ft del umbral → 0 · ≤ 2 500 ft → −3 · > 2 500 ft → −7 ¹ ³ |
| **Centreline Deviation** | −7 pts | ≤ 10 ft → 0 · ≤ 30 ft → −3 · > 30 ft → −7 ¹ |
| **Localizer Alignment** | −5 pts | ILS no sintonizado → −3 · desviación de rumbo > 5° (× 2 máx) → −2 ¹ ² |
| **Minimums Compliance** | −5 pts | −5 si el avión descendió bajo la DA sin aterrizar ¹ ² |
| **Single Engine Taxi** | **+5 pts** (bonus) | Se otorgan si ruedas ≥ 50 % del tiempo de movimiento con un solo motor en TaxiOut o TaxiIn. Solo aplica en aeronaves multi-motor. El score no puede superar 100. |

> ¹ Requiere la **NavData API** configurada en Settings (URL + API Key válida). Sin ella, estos criterios no se evalúan.  
> ² **Localizer Alignment** y **Minimums Compliance** también se omiten si el piloto sintoniza una frecuencia distinta a la del ILS al cruzar 1 000 ft AGL (aproximación RNP, visual u otro procedimiento). En ese caso no hay penalización.  
> ³ En pistas con **umbral desplazado** (*displaced threshold*), la distancia se mide desde el umbral legal de aterrizaje, no desde el extremo físico del pavimento. Un aterrizaje en la zona de aceleración (antes del umbral desplazado) no genera penalización.  
> ⁴ **Exención por ATC IVAO:** si COM1 está sintonizada en la frecuencia de una estación ATC activa en IVAO (TWR, APP, DEP, CTR…), el exceso de velocidad y la penalización por Vapp a 1 000 ft se suprimen — el ATC puede haber ordenado esa velocidad. La advertencia en el log y el OSD siguen activos. Sin ATC activo o con UNICOM (122.800), aplican las penalizaciones normales.  
> ⁵ **Scoring en aeropuerto alterno:** si el vuelo se desvía al aeropuerto alterno del OFP (SimBrief), todos los criterios que dependen del aeropuerto de llegada (TDZ, Centreline, ILS/Localizer, Minimums y QNH) se evalúan contra el alterno, no contra el destino original. El sistema lo detecta automáticamente al adquirir el umbral de pista durante la aproximación y lo indica en el log (`⚠️ Approaching ALTERNATE — XXXX`). Si el aterrizaje es en un aeropuerto distinto tanto del destino como del alterno del OFP, esos criterios se omiten sin penalización.

---

### Detalle: Lights Compliance (−5 pts cada violación, cap −10)

El sistema monitoriza las luces durante todo el vuelo y penaliza cada incumplimiento:

| Momento | Luz requerida |
|---|---|
| Pushback | NAV ON |
| Inicio de rodaje (TaxiOut) | NAV ON + TAXI ON |
| TakeoffRoll | STROBE ON + LANDING ON |
| En vuelo (cualquier fase) | BEACON ON continuo |
| Por debajo de 9 500 ft AGL | LANDING ON |

> **Excepción Beacon — switch compartido:** en aeronaves con switch único BEACON/STROBE (como el Dash 8 / Q400), encender los Strobes apaga el Beacon automáticamente. Estas aeronaves están exentas de la penalización de Beacon.
>
> **Excepción Beacon — Hotel Mode:** en turbohélices (ATR72 y similares), el **Hotel Mode** arranca la turbina del motor 2 como generador de tierra con la hélice bloqueada. En este modo el Beacon permanece apagado correctamente (las hélices no giran). vmsOpenAcars detecta el Hotel Mode automáticamente (`N1 > 10 %` y `Prop RPM < 50`) y suspende la verificación de Beacon hasta que la hélice comience a girar.
>
> **Hotel Mode — Blocks Off y Blocks On:** en Hotel Mode el tiempo de **Blocks Off** no se registra al arrancar el motor 2 (la hélice no gira, la aeronave no va a moverse). El registro se produce cuando el primer motor propulsor con hélice real empieza a girar y el avión inicia el rodaje. De igual forma, al llegar a puerta, si el motor 2 sigue en Hotel Mode (funcionando como generador APU), el sistema acepta la condición de **Blocks On** aunque ese motor esté técnicamente encendido — no es necesario apagar el generador de tierra antes de que el sistema detecte el fin del vuelo.

---

### Detalle: Stabilized Approach — gate de 1 000 ft AGL (hasta −15 pts)

Al cruzar **1 000 ft AGL en descenso** el sistema evalúa el estado del avión. Cada incumplimiento resta puntos al score final:

| Criterio | Penalización |
|---|---|
| Velocidad fuera del rango Vapp ± tolerancia | −5 pts · Exento si COM1 en ATC IVAO ⁴ |
| VS excesivo (< −1 000 fpm) | −5 pts |
| No descendiendo (VS > −100 fpm) | −5 pts |
| Bank > 7° | −3 pts |
| Pitch fuera de [−2.5°, +10°] | −3 pts |
| Tren de aterrizaje no extendido | −5 pts |
| Flaps < 50 % de extensión | −4 pts |

> El gate también comprueba el **QNH de destino** en ese mismo momento (ver criterio QNH Compliance). Si el Δ QNH supera 2 hPa, se aplican −5 pts adicionales en el apartado QNH, no en Stabilized Approach.

La suma máxima de penalizaciones del gate es **−15 pts** para Stabilized Approach + **−5 pts** para QNH llegada = **−20 pts** posibles en ese único instante.

---

### Detalle: QNH Compliance (hasta −10 pts)

El QNH del altímetro se comprueba en **dos momentos distintos**:

| Momento | Aeropuerto verificado | Penalización |
|---|---|---|
| TakeoffRoll (inicio de carrera) | Origen | −5 pts si Δ > 2 hPa |
| Gate 1 000 ft AGL (en aproximación) | Destino | −5 pts si Δ > 2 hPa |

Ambas verificaciones son independientes: si fallas las dos, el score baja −10 pts. El sistema registra el resultado de cada check en el log del vuelo.

---

### Detalle: On-Time Departure (−5 pts)

El sistema compara la hora real de **Blocks Off** (primer movimiento registrado por el servidor) con la hora de salida programada (`sched_out`) del plan de SimBrief.

- Si la diferencia es **≤ 10 minutos** (temprano o tarde) → sin penalización.
- Si la diferencia es **> 10 minutos** → −5 pts.

El resultado se notifica en el log en el momento en que se registra el Blocks Off, mostrando el delta y la hora STD.

---

### Calificaciones de aterrizaje

La tasa de descenso (fpm) en el touchdown determina también una calificación cualitativa que aparece en el log y en el LOGBOOK:

| Calificación | Tasa de descenso |
|---|---|
| 🧈 **Butter** | ≤ 150 fpm |
| ✅ **Smooth** | 151 – 250 fpm |
| 🟢 **Normal** | 251 – 350 fpm |
| 🟡 **Hard** | 351 – 450 fpm |
| 🟠 **Very Hard** | 451 – 650 fpm |
| 🔴 **Slam** | > 650 fpm |

---

### Consejos para un score perfecto

| Aspecto | Qué hacer |
|---|---|
| **Luces** | NAV ON antes del pushback · TAXI ON al rodar · STROBE y LANDING ON en TakeoffRoll · BEACON ON siempre en vuelo |
| **QNH salida** | Sintoniza el QNH del aeropuerto de origen **antes** de comenzar la carrera de despegue |
| **Hora de salida** | Respeta el STD del plan. La tolerancia es ±10 min |
| **IVAO** | Conéctate a la red IVAO antes de iniciar el rodaje (TaxiOut) |
| **Aproximación estabilizada** | A 1 000 ft AGL: velocidad en rango Vapp, VS entre −100 y −1 000 fpm, bank < 7°, tren extendido, flaps ≥ 50 % |
| **QNH llegada** | Sintoniza el QNH del aeropuerto de destino (recibirás el ATIS durante el descenso) antes de llegar a 1 000 ft AGL |
| **Touchdown** | Aterriza en la zona de touchdown (primeros 1 500 ft de pista), alineado con el eje (< 10 ft de desviación) |
| **Tasa de descenso** | Apunta a ≤ 150 fpm para eliminar la deducción por Landing Rate |

---

## 6. METAR

El botón **METAR** abre el panel meteorológico de los aeropuertos del plan activo.

- Se muestran hasta 4 estaciones: **ORIG**, **DEST**, **ALT** y **ENRT**.
- Los METARs se recuperan automáticamente al cargar el plan y se actualizan durante el vuelo.
- Haz clic en cualquier estación para abrir el **METAR Decode** — desglose completo de cada elemento (viento, visibilidad, nubes, temperatura, QNH, tendencia, etc.).

---

## 7. OSD Overlay

El overlay OSD (**On-Screen Display**) muestra notificaciones superpuestas al simulador, sin robar el foco ni interrumpir la experiencia de vuelo.

### Notificaciones automáticas

| Momento | Mensaje | Severidad |
|---|---|---|
| START pulsado | `ACARS ACTIVE` | Success (verde) |
| Inicio de rodaje | `TAXI OUT` | Info (azul) |
| Carrera de despegue | `TAKEOFF ROLL` | Info |
| Crucero estabilizado | `CRUISE` | Info |
| Inicio de descenso | `DESCENDING` | Info |
| Fase Approach | `APPROACH` | Info |
| Touchdown (≤ 100 fpm) | `BUTTER  −XX fpm  X.Xg` | Success |
| Touchdown (101–300 fpm) | `SMOOTH / NORMAL LANDING  −XX fpm  X.Xg` | Success / Info |
| Touchdown (301–600 fpm) | `FIRM LANDING  −XX fpm  X.Xg` | Warning (dorado) |
| Touchdown (> 600 fpm) | `HARD LANDING  −XX fpm  X.Xg` | Critical (rojo parpadeante) |
| Touch-and-go | `TOUCH AND GO` | Warning |
| PIREP enviado | `PIREP FILED — SCORE: XX/100` | Success |
| OnBlock | `ON BLOCK` | Info |
| Single-engine taxi detectado | `SINGLE ENGINE TAXI  +5 PTS` | Success |
| Trayectoria hacia espacio aéreo restringido | `AIRSPACE AHEAD  {TIPO}  {ICAO}` | Warning (dorado) |
| Sobre un espacio restringido (no descender) | `ABOVE  {ICAO}  DO NOT DESCEND` | Warning (dorado) |
| Dentro de espacio restringido | `AIRSPACE  {TIPO}  {ICAO}` | Critical (rojo parpadeante) |

> Los mensajes **Critical** parpadean en rojo durante ~1.3 s antes de mostrar el texto fijo. Los demás niveles usan fade-in/fade-out suave.

### Configuración

Ve a **Settings → OSD** para ajustar duración, pantalla y opacidad. Usa **MENU → Test OSD** para previsualizar el overlay antes de volar.

---

## 8. Mapa en movimiento (MAP)

El botón **MAP** abre una ventana no modal con un mapa en tiempo real sincronizado con la posición del simulador. Se actualiza cada ~250 ms independientemente de la fase de vuelo.

> **Novedad v0.6.7:** el mapa muestra los espacios aéreos y las posiciones ATC de IVAO en cuanto se carga un OFP válido, sin necesidad de iniciar el vuelo. Basta con hacer FETCH OFP + ACCEPT y luego abrir MAP.

### Controles

| Control | Función |
|---|---|
| **FOLLOW** (checkbox) | Activa el auto-centrado: el mapa sigue al avión automáticamente. Desactívalo para explorar el mapa libremente con el ratón. |
| **Dropdown de proveedor** | Cambia la capa de mapa entre las opciones disponibles. |
| **+ / −** | Aumenta o reduce el nivel de zoom. También puedes hacer scroll con la rueda del ratón. |
| **Arrastrar (botón izq.)** | Desplaza el mapa manualmente (solo activo si FOLLOW está desactivado). |

### Capas (v0.6.7)

Cuatro checkboxes en la barra inferior permiten activar o desactivar capas en tiempo real:

| Capa | Contenido |
|---|---|
| **TILES** | Tiles del proveedor de mapa (calles / satélite). Al desactivarla el mapa queda en negro, útil para ver solo la ruta. |
| **ROUTE** | Ruta, waypoints, SID/STAR, overlay de aproximación y línea al alterno. |
| **SPACES** | Polígonos de espacios aéreos: Prohibited (rojo), Restricted (naranja), Danger (amarillo), CTR (cyan), TMA (azul), ATZ (azul claro), RMZ (violeta). |
| **IVAO** | Posiciones ATC de IVAO: formas geográficas TWR/GND/DEL + text-box APP/CTR/DEP. |

### Posiciones ATC de IVAO (v0.6.7)

Las posiciones activas en IVAO se representan como formas geográficas de **20 nm de radio** que escalan con el zoom, al estilo de la WebEye de IVAO:

| Posición | Forma | Color |
|---|---|---|
| **TWR** | Círculo de 20 nm | Rojo (borde semitransparente, relleno muy transparente) |
| **GND** | Estrella 4 puntas N/S/E/W | Amarillo |
| **DEL** | Estrella 4 puntas NE/SE/SW/NW (45°) | Naranja |
| **APP / CTR / DEP** | Text-box de texto | Color según tipo |

- Las puntas de la estrella GND/DEL tienen exactamente la misma distancia al ARP que el borde del círculo TWR (20 nm), de modo que cuando TWR y GND coexisten los extremos de la estrella rozan el borde del círculo.
- El nombre ICAO del aeropuerto aparece centrado en el ARP.
- Pasa el ratón por el ICAO para ver un tooltip con las posiciones activas, frecuencias y ATIS.

### Marcador de aeronave (v0.6.7)

El avión se dibuja con una **silueta diferente según la categoría**:

| Categoría | Silueta |
|---|---|
| Jet (turbofan) | Fuselaje estrecho con alas en flecha |
| Turboprop | Alas más anchas con motores de hélice |
| Pistón | Aeronave ligera |
| Helicóptero / desconocido | Silueta genérica |

### Proveedores de mapa

| Opción | Descripción |
|---|---|
| **Street (Carto)** | Mapa de calles limpio y legible, ideal para aeropuertos y navegación en tierra. Sin API key. |
| **Satellite (ESRI)** | Imágenes satelitales de ESRI World Imagery. Útil para identificar pistas y terminales visualmente. Sin API key. |

### Sidebar de procedimientos (v0.6.5)

El panel lateral izquierdo (expandible con el botón `◀`/`▶`) permite cambiar en tiempo real la pista, SID/transición, STAR/transición y aproximación de salida y llegada.

**Sección ORIGIN:**

| Campo | Descripción |
|---|---|
| Pista | Pista de despegue |
| SID | Procedimiento de salida estándar |
| Trans. | Transición del SID |
| Chip de viento | Componente HW/TW y crosswind calculado con el METAR en vigor |

**Sección DESTINATION:**

| Campo | Descripción |
|---|---|
| Pista | Pista de llegada |
| STAR | Procedimiento de llegada estándar |
| Trans. | Transición del STAR |
| Approach | Procedimiento de aproximación (ILS, RNAV, VOR…) |
| Trans. | **Transición de aproximación (IAF)** — muestra los fixes de entrada al procedimiento (v0.7.0). Al seleccionar una transición, el overlay de aproximación se actualiza incluyendo los legs desde el IAF. |
| Carta | Botón **📋 APPROACH CHART** — abre la carta de aproximación dinámica (v0.6.8) |

> Al cambiar pista, el SID/STAR activo se valida. Si el procedimiento no aplica a la nueva pista, aparece un diálogo de confirmación. El approach se descarta automáticamente en cualquier cambio de pista destino. Al cambiar SID o STAR, el selector de pista filtra automáticamente para mostrar solo pistas compatibles con el procedimiento elegido (v0.7.0).

### Alertas de espacios aéreos (v0.7.1)

Durante el vuelo, vmsOpenAcars monitoriza los espacios aéreos de la ruta y emite tres tipos de alerta según la situación:

| Situación | Log | OSD |
|---|---|---|
| **Predictiva** — la trayectoria proyectada (~3 min a GS actual) entra en un espacio Prohibited/Restricted/Danger dentro de sus límites verticales | `⚠️ AIRSPACE AHEAD  {TIPO}  {ICAO}  [SFC – FLxxx]` | `AIRSPACE AHEAD  {TIPO}  {ICAO}` (dorado) |
| **Sobrevuelo** — el avión está lateralmente sobre el espacio pero por encima del límite superior | `⚠️ OVERFLIGHT  {TIPO}  {ICAO}  [ABOVE FLxxx]  DO NOT DESCEND` | `ABOVE  {ICAO}  DO NOT DESCEND` (dorado) |
| **Entrada** — el avión penetra el espacio dentro de sus límites verticales | `⚠️ AIRSPACE  {TIPO}  {ICAO}  [SFC – FLxxx]` | `AIRSPACE  {TIPO}  {ICAO}` (rojo parpadeante) |

> Las alertas tienen en cuenta los **límites verticales** del espacio: si vuelas a FL290 sobre una zona restringida hasta FL150, no se dispara ninguna alerta de entrada. Solo si tu trayectoria proyectada o tu altitud real cae dentro del rango vertical se activa el aviso.

### Overlay de aproximación

Al seleccionar un approach en el sidebar, el mapa dibuja en magenta la trayectoria de los legs del procedimiento + extended centerline (±5 NM, punteado). El missed approach se dibuja en cian. Si se selecciona una transición (IAF), los legs de la transición se dibujan a continuación del procedimiento principal.

### Barra de estado

La barra inferior muestra la posición actual: `latitud°  longitud°   HDG XXX°  Z:NN`. Los controles de zoom (`−`/`+`), el selector de proveedor y las checkboxes de capa siempre quedan visibles independientemente del ancho de la ventana.

> La ventana MAP puede permanecer abierta durante todo el vuelo. Al cerrarla y volver a abrirla retoma la posición actual del avión.

---

## 9. LOGBOOK y Landing Analysis

El LOGBOOK guarda el historial completo de tus aterrizajes con análisis gráfico de cada aproximación.

### Abrir el LOGBOOK

Haz clic en **LOGBOOK** en la pantalla principal.

### Columnas del historial

| Columna | Descripción |
|---|---|
| Date | Fecha y hora local del aterrizaje |
| Flight | Número de vuelo |
| Route | Par origen → destino |
| RWY | Pista de aterrizaje |
| VS (fpm) | Velocidad vertical en el touchdown |
| G | Factor de carga en el touchdown |
| Score | Puntuación del vuelo (sobre 100) |

El color del score indica la calidad: verde ≥ 90 · amarillo ≥ 75 · rojo < 75.

### Ver análisis de un vuelo

Selecciona un vuelo y haz clic en **VIEW ANALYSIS**. Se abrirán **4 gráficos** de la aproximación (eje X = distancia al umbral, de 5 NM a la izquierda hasta el umbral a la derecha):

| Gráfico | Qué muestra | Referencia visual |
|---|---|---|
| **Vertical Profile** | Altitud AGL (ft) | Línea verde punteada = planeo 3° ideal |
| **Lateral Deviation** | Desviación del eje de pista (ft ± signed) | Línea cero = eje de pista |
| **IAS** | Velocidad indicada (kt) | Línea = Vref promedio estimado |
| **VS** | Velocidad vertical (fpm) | Línea cero |

### Comparar aproximaciones

1. Selecciona 2–5 vuelos (Ctrl+clic o Shift+clic) y haz clic en **COMPARE**.
2. Los 4 gráficos mostrarán todas las trayectorias superpuestas, cada una con un color distinto.
3. El encabezado muestra una leyenda por vuelo con callsign, fecha, pista y estadísticas de touchdown.

### Eliminar registros

Selecciona uno o varios vuelos y haz clic en **DELETE**. Se pedirá confirmación.

---

## 10. Solución de problemas

**El botón START no se habilita**
- Verifica que el simulador esté activo y FSUIPC conectado (el STATUS debe mostrarlo).
- El avión debe estar posicionado en el aeropuerto correcto (< ~5 km del ICAO del plan).
- El ICAO del plan debe coincidir con el aeropuerto asignado en phpVMS.

**FETCH OFP no encuentra el plan**
- El plan de SimBrief no puede tener más de 2 horas de antigüedad.
- Verifica que el usuario de SimBrief en Settings sea correcto.
- Asegúrate de haber generado el OFP en SimBrief antes de hacer FETCH.

**El score aparece más bajo de lo esperado**
- Revisa el log del vuelo: cada penalización se registra en el momento en que ocurre.
- Comprueba especialmente las penalizaciones de QNH (salida y llegada), luces y la evaluación del gate de 1 000 ft AGL.

**Touchdown Zone y Centreline no se evalúan**
- La API key de NavData no está configurada o es inválida.
- Ve a Settings → NavData API, introduce la key y pulsa TEST para verificarla.

**El LOGBOOK no guarda los vuelos**
- Ve a Settings → Landing Log y selecciona o crea el archivo `.sqlite`.

**Los gráficos del Landing Analysis aparecen vacíos**
- La trayectoria solo se captura en fase **Approach** con AGL < 3 000 ft. Si el vuelo pasó directamente a Landing sin activar Approach, no habrá track.

**La ventana de METAR no carga datos**
- Requiere un plan activo con aeropuertos ICAO válidos y conexión a internet.

**Los anuncios de cabina no suenan**
- Verifica que **Cabin Announcements** esté activo en Settings.
- Los anuncios requieren que la NavData API esté configurada y que la aerolínea publique audios. Usa el botón **TEST** en Settings para comprobar si hay archivos disponibles para una fase concreta.
- En aeronaves con capacidad inferior a 40 pasajeros los anuncios se desactivan automáticamente.

---

*vmsOpenAcars v0.7.6 — que tengas buen vuelo.*
