# vmsOpenAcars — Guía del Usuario

**Versión 0.3.16**

vmsOpenAcars es un cliente ACARS de escritorio para Simuladores de vuelo en PC bajo windows que conecta tu simulador con aerolíneas virtuales basadas en phpVMS 7. Lee los datos del simulador en tiempo real via FSUIPC/XUIPC, detecta automáticamente las fases de vuelo, califica tu aterrizaje y envía el PIREP al servidor de tu aerolínea.

---

## Índice

- [vmsOpenAcars — Guía del Usuario](#vmsopenacars--guía-del-usuario)
  - [Índice](#índice)
  - [1. Requisitos](#1-requisitos)
    - [Simuladores compatibles](#simuladores-compatibles)
  - [2. Configuración inicial (Settings)](#2-configuración-inicial-settings)
    - [Sección phpVMS](#sección-phpvms)
    - [Sección SimBrief](#sección-simbrief)
    - [Sección NavMap Database](#sección-navmap-database)
    - [Sección Landing Log](#sección-landing-log)
  - [3. Interfaz principal](#3-interfaz-principal)
    - [Panel FMA](#panel-fma)
  - [4. Flujo de un vuelo típico](#4-flujo-de-un-vuelo-típico)
    - [4.1 Login](#41-login)
    - [4.2 Selección de vuelo y aeronave](#42-selección-de-vuelo-y-aeronave)
    - [4.3 Plan de vuelo con SimBrief](#43-plan-de-vuelo-con-simbrief)
    - [4.4 Inicio del vuelo](#44-inicio-del-vuelo)
    - [4.5 Durante el vuelo](#45-durante-el-vuelo)
      - [Fases detectadas automáticamente](#fases-detectadas-automáticamente)
      - [Ground operations](#ground-operations)
      - [Trayectoria de aproximación](#trayectoria-de-aproximación)
      - [Frecuencia de envío de posición](#frecuencia-de-envío-de-posición)
    - [4.6 Finalización y envío del PIREP](#46-finalización-y-envío-del-pirep)
  - [5. Scoring de aterrizaje](#5-scoring-de-aterrizaje)
    - [Consejos para un score perfecto](#consejos-para-un-score-perfecto)
  - [6. METAR](#6-metar)
  - [7. LOGBOOK y Landing Analysis](#7-logbook-y-landing-analysis)
    - [Abrir el LOGBOOK](#abrir-el-logbook)
    - [Columnas del historial](#columnas-del-historial)
    - [Ver análisis de un vuelo](#ver-análisis-de-un-vuelo)
    - [Comparar aproximaciones](#comparar-aproximaciones)
    - [Eliminar registros](#eliminar-registros)
  - [8. Solución de problemas](#8-solución-de-problemas)

---
S
## 1. Requisitos

| Requisito | Detalle |
|---|---|
| Simulador | Ver tabla de simuladores compatibles abajo |
| FSUIPC | Instalado y activo (versión gratuita es suficiente) |
| LittleNavMap | Necesario para scoring de pista (touchdown zone y centreline) |
| Cuenta phpVMS 7 | API Key generada en tu perfil de la aerolínea virtual |
| Cuenta SimBrief | Usuario de SimBrief (gratuito) |
| Conexión a internet | Para comunicarse con phpVMS y SimBrief |

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

> Instala la versión de FSUIPC que corresponda a tu simulador. La versión gratuita es suficiente para que vmsOpenAcars funcione con todas sus funcionalidades. Para X-Plane, instala el plugin **XUIPC** en lugar de FSUIPC.

---

## 2. Configuración inicial (Settings)

Haz clic en el botón **SETTINGS** (esquina superior derecha) para abrir el diálogo de configuración. Todos los cambios se guardan en `vmsOpenAcars.exe.config`.

### Sección phpVMS

| Campo | Descripción |
|---|---|
| API URL | URL base de tu aerolínea virtual, con `/` al final. Ej: `https://miaerolinea.com/` |
| API Key | Tu clave personal de phpVMS (generada en tu perfil de piloto) |

### Sección SimBrief

| Campo | Descripción |
|---|---|
| SimBrief User | Tu nombre de usuario de SimBrief (no el correo, sino el pilot ID o alias) |

### Sección NavMap Database

vmsOpenAcars usa la base de datos de **LittleNavMap** para calcular con precisión la distancia al umbral de pista y la desviación de centreline en el aterrizaje. Sin esta BD el scoring básico sigue funcionando, pero los criterios de Touchdown Zone y Centreline no se evaluarán.

1. Abre LittleNavMap al menos una vez para que genere su base de datos.
2. En Settings, campo **LNM DB Path**, haz clic en `[...]` y selecciona el archivo `airports.sqlite`. Por defecto se encuentra en:
   ```
   C:\Users\<TuUsuario>\AppData\Roaming\ABarthel\little_navmap_db\little_navmap_navigraph.sqlite
   ```
   o bien en la ruta que hayas configurado en LittleNavMap.

### Sección Landing Log

El LOGBOOK guarda el historial de tus aterrizajes con trayectoria de aproximación en una base de datos SQLite local.

1. En Settings, sección **Landing Log**, haz clic en `[...]`.
2. Selecciona un archivo `.sqlite` existente (si ya tienes historial) o escribe un nombre nuevo para crearlo, por ejemplo `landing_log.sqlite`.
3. La base de datos se crea y migra automáticamente al primer uso.

> La base de datos es un archivo SQLite estándar. Puedes hacer copias de seguridad simplemente copiando el archivo.

---

## 3. Interfaz principal

```
┌────────────────────────────────────────────────────────────────────┐
│  [FMA]  Línea 1: Vuelo / ruta / CI / fecha / matrícula / tipo      │
│         Línea 2: PAX / FUEL / TRIP FUEL / CARGO / FL / WIND / ISA  │
│                                          PHASE BOARDING │ GROUND   │
├────────────────────────────────────────────────────────────────────┤
│  [FLIGHT INFORMATION]  Datos del plan activo                       │
├────────────────────────────────────────────────────────────────────┤
│  [GAUGES / ENGINE]  Indicadores de vuelo en tiempo real            │
├────────────────────────────────────────────────────────────────────┤
│  [INCOMING MSG]  Log de eventos del vuelo                          │
├────────────────────────────────────────────────────────────────────┤
│  [STATUS]  GPS • Conexión sim • ACARS • Aeropuerto actual          │
├────────────────────────────────────────────────────────────────────┤
│  LOGIN  SETTINGS  SIMBRIEF  METAR  LOGBOOK  DISPATCH  START        │
└────────────────────────────────────────────────────────────────────┘
```

### Panel FMA

El panel FMA (Flight Mode Annunciator) en la parte superior muestra en tiempo real:

- **Línea 1:** identificador del vuelo, par de aeropuertos ICAO/IATA, Cost Index, fecha, matrícula y tipo de aeronave.
- **Línea 2:** pasajeros, combustible en rampa (FUEL), combustible de vuelo (TRIP), carga, nivel de crucero, viento promedio e ISA.
- **Columna derecha:** fase actual (`PHASE BOARDING`, `PHASE TAXIOUT`, etc.) y estado `GROUND` / `AIRBORNE`, más una cuenta regresiva hasta la hora de salida planificada cuando el avión está en boarding.

---

## 4. Flujo de un vuelo típico

### 4.1 Login

1. Inicia el simulador y carga tu aeronave en el aeropuerto de salida.
2. Abre vmsOpenAcars y haz clic en **LOGIN**.
3. La aplicación se conecta a phpVMS con tu API Key. Tu nombre de piloto y aeropuerto base aparecerán en el panel STATUS.

### 4.2 Selección de vuelo y aeronave

1. Haz clic en **SIMBRIEF** para abrir el Flight Planner.
2. El planner tiene dos pestañas:
   - **My Bids** — tus vuelos reservados en phpVMS. Solo muestra los que salen de tu aeropuerto actual.
   - **Available Flights** — todos los vuelos disponibles desde tu aeropuerto actual, filtrados por aeronaves presentes en dicho aeropuerto.
3. Selecciona un vuelo. La lista de aeronaves disponibles se cargará automáticamente.
4. Elige la aeronave con la que quieres operar el vuelo.

### 4.3 Plan de vuelo con SimBrief

1. Haz clic en **PLAN IN SIMBRIEF**. Se abrirá tu navegador con el dispatch de SimBrief pre-cargado (vuelo, ruta, aeronave, matrícula, piloto, hora UTC+30 min).
2. Ajusta en SimBrief lo que necesites y genera el OFP.
3. Vuelve a vmsOpenAcars y haz clic en **FETCH OFP**.
4. El sistema descarga y valida el plan (origen, destino, tipo de aeronave, matrícula, antigüedad máx. 2 h).
5. Si la validación pasa, el botón **ACCEPT** se habilitará. Haz clic para cargar el plan.

El FMA se actualizará con todos los datos del plan (PAX, combustible, nivel de crucero, viento, ISA, etc.).

### 4.4 Inicio del vuelo

El botón **START** se habilita cuando se cumplen todas las condiciones:

- ✅ Simulador conectado via FSUIPC
- ✅ Plan de vuelo cargado y aceptado
- ✅ Posición GPS válida (estás en el aeropuerto correcto, dentro de ~5 km)

Haz clic en **START**. La aplicación enviará un prefile a phpVMS (PIREP en estado BOARDING) y comenzará el seguimiento automático del vuelo.

> Si necesitas cancelar, haz clic en **ABORT** y confirma en el diálogo. El PIREP se eliminará del servidor.

### 4.5 Durante el vuelo

#### Fases detectadas automáticamente

| Fase | Condición de entrada |
|---|---|
| Boarding | Plan activo, avión en tierra con freno de estacionamiento |
| Pushback | Movimiento lento hacia atrás |
| TaxiOut | Movimiento hacia delante antes del despegue |
| TakeoffRoll | Velocidad de suelo > 30 kt con freno liberado |
| Takeoff | Liftoff detectado |
| Climb | Velocidad vertical positiva sostenida |
| Enroute | Crucero estabilizado |
| Descent | Inicio de descenso hacia destino |
| Approach | Descenso final hacia pista |
| Landing | Touchdown detectado |
| AfterLanding | Deceleración tras aterrizaje |
| TaxiIn | Rodaje hacia puerta tras aterrizar |
| OnBlock | Freno de estacionamiento puesto con motores apagados |
| Completed | Vuelo listo para enviar PIREP |

#### Ground operations

Durante el rodaje, el sistema informa en el log:

- **Pista en uso** al alinearse para el despegue (distancia al umbral y desviación de centreline)
- **Taxiways** por los que circulas
- **Holding points** detectados
- **Puerta/estacionamiento** de llegada

#### Trayectoria de aproximación

A partir de **3 000 ft AGL** con la fase en Approach, el sistema captura un punto de trayectoria cada 2 segundos (altitud, velocidad, VS, desviación lateral) que se usará para generar los gráficos del LOGBOOK.

#### Frecuencia de envío de posición

| Fase | Intervalo |
|---|---|
| En tierra (rodaje) | 30 s |
| Takeoff / Landing | 2 s |
| Climb / Descent / Approach | 5 s |
| Crucero | 15 s |

### 4.6 Finalización y envío del PIREP

1. Cuando el avión llegue a puerta y los motores estén apagados, la fase cambiará a **Completed**.
2. El botón cambiará a **SEND PIREP** (verde).
3. Haz clic. El PIREP se enviará a phpVMS con combustible usado, tasa de aterrizaje, tiempo de vuelo, distancia y score.
4. Si la base de datos del LOGBOOK está configurada, el aterrizaje se guardará automáticamente con su trayectoria de aproximación.

---

## 5. Scoring de aterrizaje

El score parte de **100 puntos** y aplica deducciones según 11 criterios. Se envía junto con el PIREP a phpVMS.

| Criterio | Deducción máx | Referencia |
|---|---|---|
| **Landing Rate** | 40 pts | ≤ 100 fpm = perfecto · ≤ 200 = −5 · ≤ 300 = −15 · ≤ 400 = −25 · ≤ 600 = −35 · > 600 = −40 |
| **G-Force** | 15 pts | ≤ 1.3 g = perfecto · ≤ 1.5 g = −7 · > 1.5 g = −15 |
| **Bank Angle** | 10 pts | ≤ 2° = perfecto · ≤ 5° = −5 · > 5° = −10 |
| **Pitch Angle** | 10 pts | 1°–5° = perfecto · fuera de rango = −5 a −10 |
| **Overspeed** | 15 pts | 0 eventos = perfecto · 1 = −7 · ≥ 2 = −15 |
| **Lights Compliance** | 10 pts | −5 pts por cada violación de luces (cap 10) |
| **Stabilized Approach (1000 ft)** | 15 pts | Evalúa velocidad, VS, bank, pitch, gear y flaps a 1000 ft AGL |
| **QNH Compliance** | 5 pts | −5 si el QNH difiere > 2 hPa del METAR del destino |
| **IVAO Offline** | 5 pts | −5 si el vuelo se realizó sin conexión a la red IVAO |
| **Touchdown Zone** | 7 pts | ≤ 1500 ft del umbral = perfecto · ≤ 2500 ft = −3 · > 2500 ft = −7 |
| **Centreline Deviation** | 7 pts | ≤ 10 ft = perfecto · ≤ 30 ft = −3 · > 30 ft = −7 |

> Los criterios **Touchdown Zone** y **Centreline Deviation** requieren la base de datos de LittleNavMap configurada en Settings. Si no está disponible, estos dos criterios no se evalúan.

### Consejos para un score perfecto

- Mantén la aproximación estabilizada antes de los 1 000 ft: velocidad ≤ Vref+10, VS < 1 000 fpm, bank < 5°, pitch entre 1° y 5°, tren extendido, flaps en configuración de aterrizaje.
- Aterriza en la zona de touchdown (primeros 1 500 ft de pista).
- Mantén la alineación con el eje de pista (centreline < 10 ft).
- Vuela siempre conectado a IVAO.
- Ajusta el QNH al del aeropuerto de destino antes de aterrizar.

---

## 6. METAR

El botón **METAR** abre el panel de información meteorológica de los aeropuertos del plan activo.

- Se muestran hasta 4 estaciones: **ORIG** (salida), **DEST** (destino), **ALT** (alternado) y **ENRT** (en ruta).
- Los METAR se recuperan automáticamente al cargar el plan y se actualizan periódicamente durante el vuelo.
- Haz clic en cualquier estación para abrir el **METAR Decode** — una ventana que desglosa cada elemento del METAR en lenguaje comprensible (viento, visibilidad, nubes, temperatura, QNH, etc.).

---

## 7. LOGBOOK y Landing Analysis

El LOGBOOK guarda el historial completo de tus aterrizajes con análisis gráfico de la aproximación.

### Abrir el LOGBOOK

Haz clic en el botón **LOGBOOK** en la pantalla principal. Se abrirá la ventana de historial con la lista de todos tus vuelos registrados.

### Columnas del historial

| Columna | Descripción |
|---|---|
| Date | Fecha y hora local del aterrizaje |
| Flight | Número de vuelo (callsign) |
| Route | Par origen → destino |
| RWY | Pista de aterrizaje |
| VS (fpm) | Velocidad vertical en el touchdown |
| G | Factor de carga en el touchdown |
| Score | Puntuación del aterrizaje (sobre 100) |

El color del score indica la calidad: verde ≥ 90 · amarillo ≥ 75 · rojo < 75.

### Ver análisis de un vuelo

Selecciona un vuelo en la lista y haz clic en **VIEW ANALYSIS** (o doble clic en la fila).

Se abrirá la ventana de análisis con **4 gráficos** de la aproximación. El eje X representa la distancia al umbral de pista (de 5 NM a la izquierda hasta el umbral a la derecha):

| Gráfico | Qué muestra | Referencia |
|---|---|---|
| **Vertical Profile** | Altitud AGL (ft) vs distancia | Línea verde punteada = planeo 3° ideal |
| **Lateral Deviation** | Desviación del eje de pista (ft, ± signed) vs distancia | Línea cero = eje de pista |
| **IAS** | Velocidad indicada (kt) vs distancia | Línea naranja = Vref promedio estimado |
| **VS** | Velocidad vertical (fpm) vs distancia | Línea cero |

### Comparar aproximaciones

1. Selecciona entre 2 y 5 vuelos en la lista (usando **Ctrl+clic** o **Shift+clic**).
2. Haz clic en **COMPARE**.
3. Los 4 gráficos mostrarán todas las trayectorias superpuestas, cada una con un color distinto.
4. El encabezado de la ventana muestra una leyenda por vuelo con callsign, fecha/hora, pista y estadísticas completas del touchdown.

Esto te permite comparar, por ejemplo, dos aproximaciones a la misma pista en días distintos e identificar qué cambió.

### Eliminar registros

Selecciona uno o varios vuelos y haz clic en **DELETE**. Se pedirá confirmación antes de borrar.

---

## 8. Solución de problemas

**El botón START no se habilita**
- Verifica que el simulador esté activo y FSUIPC esté conectado (el panel STATUS debe mostrar el nombre del simulador).
- Asegúrate de que el avión esté posicionado en el aeropuerto correcto. La validación GPS requiere estar a menos de ~5 km del aeropuerto del plan.
- El ICAO del plan debe coincidir con el aeropuerto asignado en phpVMS.

**FETCH OFP no encuentra el plan**
- El plan de SimBrief no puede tener más de 2 horas de antigüedad.
- Verifica que el nombre de usuario de SimBrief en Settings sea correcto.
- Asegúrate de haber generado el plan en SimBrief antes de hacer FETCH.

**Touchdown Zone y Centreline siempre muestran 0 / no se evalúan**
- La base de datos de LittleNavMap no está configurada o la ruta es incorrecta.
- Ve a Settings → NavMap Database y selecciona el archivo `airports.sqlite` correcto.

**El LOGBOOK no guarda los vuelos**
- La ruta de la base de datos de Landing Log no está configurada.
- Ve a Settings → Landing Log y selecciona o crea el archivo `.sqlite`.

**Los gráficos del Landing Analysis aparecen vacíos**
- La trayectoria de aproximación solo se captura cuando la fase es **Approach** con AGL < 3 000 ft. Si el vuelo pasó directamente de Descent a Landing sin activar la fase Approach, no habrá track.

**La ventana de METAR no carga datos**
- Requiere un plan de vuelo activo con aeropuertos ICAO válidos.
- Verifica la conexión a internet; el sistema consulta fuentes METAR públicas.

---

*vmsOpenAcars v0.3.16 — que tengas buen vuelo.*
