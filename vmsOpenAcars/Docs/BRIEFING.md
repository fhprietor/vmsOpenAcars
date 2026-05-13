# vmsOpenAcars — Guía del Usuario

**Versión 0.4.15**

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
- [8. LOGBOOK y Landing Analysis](#8-logbook-y-landing-analysis)
- [9. Solución de problemas](#9-solución-de-problemas)

---

## 1. Requisitos

| Requisito | Detalle |
|---|---|
| Simulador | Ver tabla de simuladores compatibles abajo |
| FSUIPC | Instalado y activo (versión gratuita es suficiente) |
| LittleNavMap | Necesario para scoring de pista (Touchdown Zone y Centreline) |
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

### Sección NavMap Database

vmsOpenAcars usa la base de datos de **LittleNavMap** para calcular con precisión la distancia al umbral de pista, la desviación de centreline, y detectar el ILS de la pista de aterrizaje. Sin esta BD el scoring básico funciona, pero los criterios **Touchdown Zone**, **Centreline Deviation**, **Localizer Alignment** y **Minimums Compliance** no se evaluarán.

1. Abre LittleNavMap al menos una vez para que genere su base de datos.
2. En Settings → **LNM DB Path**, haz clic en `[...]` y selecciona el archivo `.sqlite` de LittleNavMap. Por defecto se encuentra en:
   ```
   C:\Users\<TuUsuario>\AppData\Roaming\ABarthel\little_navmap_db\little_navmap_navigraph.sqlite
   ```

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
│  LOGIN  SETTINGS  SIMBRIEF  METAR  LOGBOOK  DISPATCH  MENU  START   │
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

El score parte de **100 puntos** y aplica deducciones según **12 criterios**. El valor final (0–100) se envía con el PIREP a phpVMS y queda registrado en el LOGBOOK.

### Tabla de criterios

| Criterio | Máx. deducción | Regla |
|---|---|---|
| **Landing Rate** | −40 pts | ≤ 100 fpm → 0 · ≤ 200 → −5 · ≤ 300 → −15 · ≤ 400 → −25 · ≤ 600 → −35 · > 600 → −40 |
| **G-Force** | −15 pts | ≤ 1.3 g → 0 · ≤ 1.5 g → −7 · > 1.5 g → −15 |
| **Bank Angle** | −10 pts | ≤ 2° → 0 · ≤ 5° → −5 · > 5° → −10 |
| **Pitch Angle** | −10 pts | 1°–5° nose-up → 0 (ideal) · < −2° → −10 · −2° a 1° → −5 · > 8° → −5 |
| **Overspeed** | −15 pts | 0 eventos → 0 · 1 evento → −7 · ≥ 2 eventos → −15 |
| **Lights Compliance** | −10 pts | −5 pts por violación (cap −10). Ver detalle abajo. |
| **Stabilized Approach** | −15 pts | Evaluado al cruzar 1 000 ft AGL en descenso. Ver detalle abajo. |
| **QNH Compliance** | −10 pts | −5 pts si Δ QNH > 2 hPa. Verificado **dos veces**: salida (TakeoffRoll) y llegada (gate 1 000 ft AGL). |
| **IVAO Offline** | −5 pts | −5 si el piloto no está conectado a IVAO al iniciar el TaxiOut |
| **On-Time Departure** | −5 pts | −5 si el Blocks Off real difiere más de 10 min del STD programado |
| **Touchdown Zone** | −7 pts | ≤ 1 500 ft del umbral → 0 · ≤ 2 500 ft → −3 · > 2 500 ft → −7 ¹ |
| **Centreline Deviation** | −7 pts | ≤ 10 ft → 0 · ≤ 30 ft → −3 · > 30 ft → −7 ¹ |
| **Localizer Alignment** | −5 pts | ILS no sintonizado → −3 · desviación de rumbo > 5° (× 2 máx) → −2 ¹ |
| **Minimums Compliance** | −5 pts | −5 si el avión descendió bajo la DA sin aterrizar ¹ |

> ¹ Requiere la base de datos de LittleNavMap configurada en Settings. **Localizer Alignment** y **Minimums Compliance** además requieren que la pista tenga un procedimiento ILS en la BD. Sin ello estos criterios no se evalúan.

---

### Detalle: Lights Compliance (−5 pts cada violación, cap −10)

El sistema monitoriza las luces durante todo el vuelo y penaliza cada incumplimiento:

| Momento | Luz requerida |
|---|---|
| Pushback | NAV ON |
| Inicio de rodaje (TaxiOut) | NAV ON + TAXI ON |
| TakeoffRoll | STROBE ON + LANDING ON |
| En vuelo (cualquier fase) | BEACON ON continuo |
| Por debajo de 10 000 ft AGL | LANDING ON |

> **Excepción Beacon:** en aeronaves con switch único BEACON/STROBE (como el Dash 8 / Q400), encender los Strobes apaga el Beacon automáticamente. Estas aeronaves están exentas de la penalización de Beacon.

---

### Detalle: Stabilized Approach — gate de 1 000 ft AGL (hasta −15 pts)

Al cruzar **1 000 ft AGL en descenso** el sistema evalúa el estado del avión. Cada incumplimiento resta puntos al score final:

| Criterio | Penalización |
|---|---|
| Velocidad fuera del rango Vapp ± tolerancia | −5 pts |
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
| 🧈 **Butter** | ≤ 100 fpm |
| ✅ **Smooth** | 101 – 200 fpm |
| 🟢 **Normal** | 201 – 300 fpm |
| 🟡 **Hard** | 301 – 400 fpm |
| 🟠 **Very Hard** | 401 – 600 fpm |
| 🔴 **Slam** | > 600 fpm |

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
| **Tasa de descenso** | Apunta a ≤ 200 fpm para eliminar la deducción por Landing Rate |

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

> Los mensajes **Critical** parpadean en rojo durante ~1.3 s antes de mostrar el texto fijo. Los demás niveles usan fade-in/fade-out suave.

### Configuración

Ve a **Settings → OSD** para ajustar duración, pantalla y opacidad. Usa **MENU → Test OSD** para previsualizar el overlay antes de volar.

---

## 8. LOGBOOK y Landing Analysis

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

## 9. Solución de problemas

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
- La base de datos de LittleNavMap no está configurada o la ruta es incorrecta.
- Ve a Settings → NavMap Database y selecciona el archivo `.sqlite` correcto.

**El LOGBOOK no guarda los vuelos**
- Ve a Settings → Landing Log y selecciona o crea el archivo `.sqlite`.

**Los gráficos del Landing Analysis aparecen vacíos**
- La trayectoria solo se captura en fase **Approach** con AGL < 3 000 ft. Si el vuelo pasó directamente a Landing sin activar Approach, no habrá track.

**La ventana de METAR no carga datos**
- Requiere un plan activo con aeropuertos ICAO válidos y conexión a internet.

---

*vmsOpenAcars v0.4.15 — que tengas buen vuelo.*
