# Respuesta (14) al equipo de phpVMS — el AGL, los dos casos raros y el radioaltímetro, medidos

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 18/11/2026
> **Respecto a:** vuestra propuesta *«la altura sobre la pista (AGL funcional)»* del 18/11/2026
> **Estado:** el síntoma está **confirmado y reproducido** en la traza del vuelo. El diagnóstico
> necesita **tres correcciones**, y los dos casos raros quedan **resueltos con su log y su traza**.
> De las cuatro preguntas del §9, **las cuatro tienen respuesta**. El TCH, que era el hueco, lo
> publica NavData desde hoy **derivado** y con el motivo cuando no puede; el §5.1 lo verifica en
> vivo contra la API.

---

## 0. Resumen: qué compartimos y qué corregimos

| Vuestra lectura | Lo que mide el dato |
|---|---|
| `agl_ft` = altitud − elevación del aeródromo | Es altitud − la elevación **del plan** (SimBrief, o 0 si el plan se reconstruyó al retomar o se cargó de una licitación), y **cambia con la fase** |
| El fallo frecuente es la pendiente de la pista (−23 ft en SKBG) | La pista explica una parte; el desfase de referencia en SKBG es de **47 ft**, no 23. La «puerta de 10 ft» mide a **57 ft** |
| `−min(agl aéreo)` estima el desfase | **No**: es un mínimo, no un datum. En SKMD da −66 con un desfase de 26; en SKBG da −23 con 47 |
| `radar_alt_ft` está mal escalado: 360 ft serían 13,7° | El radioaltímetro **está bien**: a 1.480 ft del umbral hay **240 ft de terreno** bajo el avión. La senda de 3° se cumple |
| Los 9 ft en tierra son un offset mal leído | Son la **altura del punto de referencia del addon** sobre el suelo (0,1 ft en el ToLiss A320; 13 ft en el iFly B38M) |
| `+6.964` = la consulta de elevación falló y se restó 0 | Cierto el «se restó 0», pero no hubo consulta: es un **vuelo retomado** cuyo plan se reconstruye sin elevaciones |
| `−1.076` = elevación equivocada | Es un **go-around**: la fase pasa a `Climb` y la referencia salta al **origen** (2.238 ft) estando sobre el destino (756 ft) |
| `KLAX` con 0 en vuestra base lo demuestra | Es cierto que un datum puede ser 0, pero **nosotros no leemos esa tabla**: leemos el plan. Y el umbral de NavData, no el aeropuerto, es el datum correcto por pista |
| El TCH no está en NavData | **Ya está**: `tch_ft` **derivado**, con `tch_source` y `tch_unavailable`. SKBG 35 sigue sin él (`displaced_threshold`), que es justo el caso del hilo |

Las tres correcciones de fondo, en una línea cada una:

1. **El datum no es el aeródromo; es el plan.** `agl_ft` sale de `CurrentAGL`, que resta
   `_activePlan.DestinationElevation` (o `OriginElevation` según la fase). Ese valor es un `int`
   que vale 0 cuando el plan se reconstruye (`ResumeFlight`, `LoadFlightFromBid`) o cuando el OFP
   no trae `destination.elevation` (`?? 0`). No es una consulta que falle: es un **cero por defecto**.
2. **La referencia cambia con la fase, y un go-around la devuelve al origen.** No es un datum
   equivocado: es el datum correcto aplicado en el momento equivocado.
3. **El radioaltímetro es la medida buena.** Sus dos «anomalías» —9 ft en tierra y 360 ft en la
   primera muestra— son física, no escala. Lo que hay que añadir no es otro altímetro: es el **cero
   del addon** para que las puertas de 50/30/10 ft signifiquen lo mismo en un 737 que en un A320.

---

## 1. El síntoma, reproducido en `ez9OqzRnYxbnGLR8`

La traza local del vuelo 55 (SKBO→SKBG, pista 35, umbral desplazado 421 ft) tiene **115 muestras:
99 en el aire y 16 en tierra**. De las 99 en el aire, **64 tienen `agl_ft < 0`**; el mínimo es
**−23,0 ft**. Las 16 en tierra valen **0 exacto** (un solo valor distinto). Coincide con vuestros
números, así que no hay nada que discutir sobre el síntoma.

Las **149 trazas y los 33 aeródromos** son vuestra población: no la hemos recontado. Lo que está
reproducido es el caso, el mecanismo y la forma de clasificar el resto (§3.3).

Lo que añade la traza es **cuánto** se desvía, y que el desfase es **constante**: no depende de la
posición, solo del datum. La referencia del plan en este vuelo es **3.901 ft** (la de SimBrief;
phpVMS tiene 3.897 y NavData 3.901), y el umbral de la 35 de NavData es **3.854 ft**:

| Distancia al umbral | `agl_ft` | `radar_alt_ft` | Altura sobre el plano del umbral | Terreno bajo el avión |
|---:|---:|---:|---:|---:|
| +1.480 ft | **74,0** | 360,1 | **121 ft** | 3.615 ft |
| +296 ft | **1,0** | 48,1 | **48 ft** | 3.854 ft |
| −20 ft | **−12,0** | 34,9 | **35 ft** | 3.854 ft |
| −376 ft | **−23,0** | 23,4 | **24 ft** | 3.857 ft |
| −1.851 ft | **−15,0** | 10,1 | **32 ft** | 3.876 ft |
| en tierra | **0** (forzado) | 9,3–9,5 | — | — |

La columna «altura sobre el plano del umbral» es `agl_ft + 47`: la referencia del plan menos el
umbral (`3.901 − 3.854 = 47`). La altura sobre el **terreno local** es la del radar; la diferencia
entre las dos columnas es lo que baja el terreno (y lo que sube la pista). El desfase **no es
23 ft**: es **47 ft**, y el mínimo de −23 es solo el punto donde la trayectoria se acercó más al
plano del umbral, no el tamaño del error.

La consecuencia práctica, que es la que importa para el análisis:

| Puerta | Altura sobre el umbral | `agl_ft` que ve hoy |
|---|---:|---:|
| 50 ft | 50 ft | **+3 ft** |
| 30 ft | 30 ft | **−17 ft** |
| 10 ft | 10 ft | **−37 ft** |

Es decir: **la puerta de 10 ft está midiendo a 57 ft**, no a 33. Y el cruce por cero del gráfico
—el que se lee como «tocó»— ocurre a **47 ft de altura real**, alrededor de 300 ft antes del umbral.
La curva no es que «baje de cero al final»: está desplazada 47 ft hacia abajo **desde el principio**.

> **El dato que valida el umbral:** la columna «terreno bajo el avión» es
> `agl_ft − radar_alt_ft + 3.901`. En el umbral da **3.854,1 ft**, exactamente el `elevation_ft`
> de la 35 de NavData. El radar y la elevación de NavData se confirman el uno al otro con 1 ft.

---

## 2. Qué resta el cliente de verdad (y por qué el datum es el plan)

El campo que viaja como `agl_ft` es `CurrentAGL`, y su definición está en
`Core/Flight/FlightManager.cs`:

```csharp
public double CurrentAGL
{
    get
    {
        if (IsOnGround) return 0;                       // ← (c) el 0 forzado del toque
        switch (CurrentPhase)
        {
            case FlightPhase.Enroute:
                return Math.Max(0, CurrentAltitude - _groundAltitudeFeet);
            default:
                return CurrentAltitude - ReferenceAirportElevation;
        }
    }
}
```

`ReferenceAirportElevation` (`Core/Flight/FlightManager.cs`) no es «la elevación del aeródromo»:

```csharp
if (_activePlan == null) return 0;
if (HasTouchedDownThisFlight) return ArrivalElevationOrPlanned;
switch (CurrentPhase)
{
    case Descent: case Approach: case Landing: case AfterLanding:
    case TaxiIn: case OnBlock: case Arrived: case Completed:
        return ArrivalElevationOrPlanned;    // elevación del plan, no de NavData
    default:
        return _activePlan.OriginElevation;  // ← en Climb/Enroute, la del ORIGEN
}
```

Y `ArrivalElevationOrPlanned` es `_activePlan.DestinationElevation` (o la del desvío confirmado).
Ese campo se llena en `Services/SimbriefEnhancedService.cs` con:

```csharp
DestinationElevation = json["destination"]?["elevation"]?.Value<int>() ?? 0,
```

**El `?? 0` es el origen del `+6.964`.** Y hay dos caminos que ni siquiera pasan por ahí:

- `FlightManager.Lifecycle.cs → ResumeFlight` reconstruye el plan «mínimo» y **no asigna**
  `OriginElevation` ni `DestinationElevation`: ambos quedan en **0**.
- `MainViewModel.LoadFlightFromBid` construye el plan de la licitación y **tampoco** los asigna.

No es una elevación «que no se conoce y se resta igual»: es una elevación **que se pone a 0 por
defecto**. La distinción importa porque el arreglo no es «consultar mejor», sino **no usar 0 como
datum** y distinguir «0 medido» de «no lo tengo».

### 2.1 Las tres formas de que el AGL salga mal

| Camino | Qué resta | Cuándo se ve |
|---|---|---|
| **a) Referencia fija** | La elevación del **plan** para el aeródromo | Siempre que el plan la traiga: es el −2 de EDDF; los negativos de SKBG/SKMD son el mismo fallo con otra pendiente |
| **b) Referencia 0** | 0, porque el plan se retomó, se cargó de una licitación o el OFP no traía el campo | El `+6.964`; cualquier vuelo retomado en aproximación |
| **c) Referencia de origen** | La elevación del **origen**, porque la fase volvió a `Climb` | El `−1.076` del go-around; antes, el PIREP `RBmrBApLOjWlYmO2` con 264 ft parado |

Los tres están en el mismo `switch`. No son tres bugs independientes: son **tres estados del mismo
datum**, y por eso el análisis necesita que el dato viaje con su procedencia.

### 2.2 El desfase real no es `−min(agl)`

Medido en las trazas locales de la 0.9.72/0.9.74 (la referencia se infiere del propio radar en el
umbral, `±4 ft`):

| Vuelo | Pista | Referencia del plan | Umbral NavData | Desfase en el umbral | Mínimo `agl` |
|---|---|---:|---:|---:|---:|
| 55 · SKBO→SKBG | 35 | 3.901 | 3.854 | **47** | 23 |
| 45 · SKCG→SKBG | 35 | 3.901 | 3.854 | **47** | 13 |
| 46 · SKBG→SKRG | 01 | 7.024 | 6.965 | **59** | 42 |
| 54 · SKIB→SKMD | 02 | 4.949 | 4.921 | **28** | 66 |
| 52 · SKPE→SKMD | 02 | 4.949 | 4.921 | **28** | 51 |
| 43 · SKCL→SKCC | 16 | 1.089 | 1.089 | **0** | 6 |
| 50 · SKUC→SKBO | 14L | 8.351 | 8.354 | **−3** | 9 |
| 49 · SKCC→SKUC | 11 | 429 | 421 | **8** | 4 |

Dos cosas saltan a la vista:

- **El signo cambia.** En SKBO la referencia del plan está 3 ft **por debajo** del umbral; en SKRG
  está 59 ft **por encima**. El AGL puede mentir en las dos direcciones.
- **El mínimo no es el desfase.** En SKBG el mínimo es −23 y el desfase en el umbral 47: el mínimo
  está 376 ft pasado el umbral, donde el avión aún está a 24 ft sobre el plano del umbral (21 ft
  sobre la superficie, según el radar), así que es la altura real menos el desfase. En SKMD el
  mínimo es −66 y el desfase 28: el mínimo está 2.528 ft pasado el umbral, donde la superficie ya
  no es la del umbral (la pista baja hacia el otro extremo), así que mezcla datum y perfil. En
  ninguno de los dos el mínimo sirve como medida del error de referencia. Publicar `−min(agl)` como
  «desfase» da un número que no es ni el datum ni la altura.

**Conclusión para las trazas viejas:** el desfase estimado no se puede sacar del mínimo. Se saca de
`elevación_del_plan − elevación_del_umbral`, que sí es un dato (el plan está en el PIREP, el umbral
en NavData) y se puede etiquetar como estimación. Y si no se conoce la elevación del plan, el
desfase es **desconocido**, no 0.

---

## 3. Los dos casos raros, resueltos

### 3.1 `9o45JZ5eAVZ11qAe` (+6.964): un vuelo retomado, no una consulta fallida

El log ACARS del PIREP lo dice sin ambigüedad:

```
05:37:36  ── APPROACH ──
05:56:03  ✅ Vuelo retomado — polling y actualizaciones reiniciados.
05:56:05  [OFP]  SimBrief plan mismatch (SKMD→SKIB), skipped
05:57:27  (primera muestra de la traza)
```

El vuelo se retomó **poco más de minuto y medio antes del toque** (05:56:03 → contacto 05:57:41).
`ResumeFlight` reconstruyó el plan sin `DestinationElevation` —0— y el OFP de SimBrief que había en
la sesión era de otro vuelo (`SKMD→SKIB`), así que se descartó. A partir de ahí,
`agl_ft = altitud − 0 = altitud MSL`.

La traza lo confirma por partida doble:

- **112 muestras en el aire**, `agl_ft` de **7.070** a **6.964**, `radar_alt_ft` de **197,1** a
  **1,0**. No hay ni un negativo.
- `agl_ft − radar_alt_ft` se queda entre **6.871 y 6.963** en toda la traza. Eso es exactamente la
  **elevación del terreno bajo el avión**: el umbral de la 01 de SKRG está a **6.965 ft**. El último
  punto en el aire, 2.526 ft más allá del umbral, da `6.964 − 1,2 = 6.962,8`, a 2 ft del umbral.
- En tierra el radar baja a **0,09–0,67 ft** (ToLiss A320 en X-Plane), no a 9. El radioaltímetro de
  ese addon está bien; el datum era el que faltaba.

**El número 6.964 no es la elevación del aeródromo: es la MSL del último punto en el aire**, que
coincide con la elevación porque el avión ya está sobre la pista. Vuestra lectura «elevación restada
= 0» era la correcta; lo que falla es el **porqué**: no es una consulta de elevación, es un plan
reconstruido al retomar.

> **El mismo patrón, sin retomar:** cualquier vuelo cargado con `LoadFlightFromBid` tiene
> `OriginElevation = 0` y `DestinationElevation = 0`. Si se aproxima, el AGL será MSL. No hace falta
> un caso raro para reproducirlo: basta con empezar desde una licitación.

### 3.2 `0aNnVLMo8pxpbNPq` (−1.076): un go-around, no una elevación equivocada

El log del PIREP tiene la transición **al segundo exacto** del salto:

```
02:10:53  ── CLIMB ──
```

Y la traza, en el mismo instante:

| Instante | `dist_ft` | `agl_ft` | `radar_alt_ft` | `vs_fpm` |
|---|---:|---:|---:|---:|
| 02:10:53.290 | −4.103 | **395** | 398,6 | +3.412 |
| **02:10:53.452** | −4.131 | **−1.076** | **405,9** | **+3.471** |
| 02:10:53.597 | −4.182 | −1.063 | 419,1 | +3.576 |
| 02:10:53.722 | −4.213 | −1.055 | 427,5 | +3.640 |

El salto es de **−1.471 ft en una muestra**, mientras el radar sube suavemente de 398,6 a 405,9 y
la tasa vertical es **+3.400 fpm**: el avión está **subiendo**, no descendiendo. La diferencia entre
las dos referencias es exactamente la de los dos aeródromos:

```
origen UAAA 2.238 ft − destino VIAR 756 ft = 1.482 ft
salto observado: 395 − (−1.076) = 1.471 ft
(la diferencia de 11 ft es el ascenso del avión entre las dos muestras)
```

Es el **go-around** que ya conocíamos: `Approach → Climb` cuando la tasa supera 600 fpm y el avión
lleva 30 s en aproximación. Al pasar a `Climb`, `ReferenceAirportElevation` devuelve
`_activePlan.OriginElevation` —2.238 ft— y el AGL se calcula contra el origen **estando sobre el
destino**. El mínimo −1.076 no es una elevación mal restada: es la altitud 1.162 ft menos 2.238.
El avión estaba a 406 ft sobre el terreno (el radar), no bajo tierra.

La traza tiene **359 muestras en el aire y ninguna en tierra**: no es un aterrizaje, es el tramo
del go-around. El vuelo volvió a aproximar a las 02:24:30 y aterrizó a las 02:41:56; la captura del
flare se cerró durante el ascenso y no vio la segunda toma.

### 3.3 Cómo clasificar las 69 trazas sin volver a volar

La prueba es el propio dato, y no necesita ni NavData ni el plan:

1. Calculad `d = agl_ft − radar_alt_ft` muestra a muestra.
2. **Si `d` es constante** (con el ruido del radar, ±5 ft), el datum es fijo. Si `d` ≈ la elevación
   del terreno, el datum es **0** (caso `9o45`); si `d` ≈ `elevación_del_terreno − elevación_del_plan`,
   el datum es el plan (caso SKBG).
3. **Si `d` da un escalón de una muestra** y el escalón coincide con `elev_origen − elev_llegada`,
   es el **go-around** (caso `0aNn`). El escalón va acompañado de un cambio de signo de la tasa
   vertical; el radar no salta.
4. **Si `d` cambia de forma suave**, es el perfil del terreno, no un datum: la traza está bien y lo
   que falla es la altura sobre la pista, que hay que interpolar con NavData.

El caso `SKCL` de −915 ft debería caer en (3) si es un go-around, o en (2) si el plan venía sin
elevación. Con la traza de 2 s o la fina, la clasificación es inmediata; no hace falta rehacer el
vuelo. Y el `−min(agl)` no se usa en ningún paso.

---

## 4. El radioaltímetro: 0x31E4, los 9 ft y los 360 ft

**Lo que leemos.** `Services/FsuipcService.cs`:

```csharp
/// 0x31E4 · INT32 · Radio/radar altitude in metres × 65536.
private readonly Offset<int> _radarAltitudeOffset = new Offset<int>(0x31E4);

int radarRaw = _radarAltitudeOffset.Value;
CurrentRadarAltitudeFeet = radarRaw > 0 ? (radarRaw / 65536.0) * 3.28084 : 0.0;
```

La conversión es la documentada por FSUIPC (`metros × 65536 → metros → pies`), y los datos la
confirman: en el tramo estable del flare la derivada del radar coincide con la tasa vertical (la
diferencia es la pendiente del terreno, ±10–20 %), y en el ToLiss A320 de X-Plane el radar en tierra
da **0,09–0,67 ft**. No hay factor 3,28.

**Los 9 ft en tierra son el cero del addon.** El radar de FSUIPC mide la altura del **punto de
referencia del modelo**, no la de las ruedas. Ese punto está a distinta altura en cada addon:

| Aeronave (título real) | Radar mediano en tierra |
|---|---:|
| iFly B38M (`iFly B38M VH…`) | **13,1 ft** |
| B738 (`737-800 PAX SSW HD`) | **9,5 ft** |
| ATR 72 (`AT76`) | **6,6–7,2 ft** |
| ToLiss A320 (`ToLissA320_V1p3p3`) | **0,1–0,7 ft** |

No es un offset que se pueda compensar con una constante: es **el datum de cada modelo**. Y no es
un error del addon: el radar mide lo que mide. Lo que sí es un problema es usarlo para una puerta
de «10 ft» sin decir **de qué** son esos 10 ft: en el B738, un radar de 10 ft son **0,5 ft sobre las
ruedas**; en el A320, son 9,8 ft. La misma puerta, dos significados.

**Los 360 ft de la primera muestra son terreno, no escala.** A 1.480 ft del umbral de la 35 de SKBG
el terreno bajo el avión está a **3.615 ft**, 240 ft por debajo del umbral (3.854). El avión está a
**121 ft sobre el plano del umbral** (74 de AGL + 47 de desfase) y a **360 ft sobre el terreno**. La
senda de 3° está ahí: 1.480 × tan(3°) + 50 ≈ 128 ft esperados, 121 medidos. La cuenta de la tasa
vertical lo cierra:

```
ascenso del terreno en 1.480 ft:  3.854 − 3.615 = 239 ft
tiempo a 153 kt:                  1.480 / 258 ft/s ≈ 5,74 s
tasa del terreno:                 239 / 5,74 ≈ 41,6 ft/s ≈ 2.500 fpm
tasa del radar = VS − tasa del terreno = −970 − 2.500 ≈ −3.470 fpm
observado:                        325 ft / 5,38 s ≈ −3.625 fpm  (4 % de diferencia)
```

**No son 13,7°: son 3° sobre la pista y 13,7° sobre el valle**, porque el valle no es la pista. Es
exactamente lo que se le pide a un radioaltímetro —medir el suelo que hay debajo— y la razón por la
que es la única medida sin datum. Lo que hay que corregir no es el radar: es la tentación de
compararlo con la altura sobre el umbral.

**El ruido de las primeras muestras sí existe.** En la traza 55, entre muestras de 0,12 s hay
saltos de 20–58 ft (268 → 288 → 249 → 257) que ninguna aeronave hace. El radar es fiable en el
tramo estable (por debajo de ~100 ft en este addon) y ruidoso por encima. Para el análisis del flare
no importa —las puertas están por debajo de 100 ft—, pero **no se debe usar la primera muestra como
ancla de sanidad**, y conviene filtrar por calidad (p. ej., descartar las muestras con salto
> 15 ft/s respecto a la anterior) si se va a pintar.

---

## 5. NavData: lo que hay, lo que no, y cómo se mapea

Esto responde a vuestra pregunta 1. La caché local de `NavData_cache.sqlite` (AIRAC 2611,
17/11/2026) trae el bloque del aeropuerto con este objeto de pista:

```json
// SKBG · pista 35 (bloque /airport/SKBG/)
{
  "mag_var": -9.27, "name": "35", "heading": 348.3,
  "threshold_lat": 7.116407871246338, "threshold_lon": -73.18096160888672,
  "end_lat": 7.136396682653605, "end_lon": -73.1886945243691,
  "length_ft": 7811.0, "width_ft": 148.0,
  "elevation_ft": 3854.0,
  "offset_threshold_ft": 421.0,
  "has_ils": true, "ils_ident": "IBGA", "ils_freq_mhz": 110.7,
  "ils_course": 348.0, "ils_glideslope_angle": 3.0
}
```

Y el `Info` del aeropuerto:

```json
// SKBG · Info
{ "icao": "SKBG", "elevation_ft": 3901.0, ... }
```

**La clave:** `elevation_ft` de la pista **es la elevación de su umbral**, no la del aeropuerto.
La prueba está en la traza 55: el terreno calculado en el umbral es 3.854,1 y el `elevation_ft` de
la 35 es 3.854. El `Info.elevation_ft` (3.901) es la elevación del aeródromo y coincide con el umbral
más alto —el de la 17—; en SKMD (4.949) queda 28 ft por encima de los dos umbrales. En cualquier
caso **no es la elevación del umbral de la pista que se usa**, y por eso restarlo sesga el AGL.

| Campo que propone vuestra Capa 2 | ¿NavData lo trae? | De dónde sale |
|---|---|---|
| `threshold_elevation_ft` | **Sí** | `Runways[].elevation_ft` de la pista de aterrizaje; el ILS publica además `ils.threshold_elevation_ft` (18/11/2026) |
| `tdz_elevation_ft` | **No** | No hay campo; la TDZ no se publica |
| `end_elevation_ft` | **Sí, indirecto** | `elevation_ft` de la pista **recíproca** (el otro extremo) |
| `tch_ft` | **Sí, derivado** | `tch_ft` + `tch_source`/`tch_unavailable`; ver §5.1 |
| `glide_slope_deg` | **Sí, si hay ILS** | `runway.ils_glideslope_angle` (3.0 en las ILS); `null` si no hay ILS |
| `offset_threshold_ft` | **Sí** | Ya lo mandamos |

Los valores reales de los cinco aeródromos del hilo:

| Aeródromo | `Info.elevation_ft` (aeropuerto) | Pista 1 | Pista 2 |
|---|---:|---|---|
| SKBG | 3.901 | 17 → **3.901** | 35 → **3.854** |
| SKRG | 7.024 | 01 → **6.965** | 19 → **7.024** |
| SKMD | 4.949 | 02 → **4.921** | 20 → **4.888** |
| SKPE | 4.413 | 08 → **4.334** | 26 → **4.413** |
| SKCL | 3.162 | 02 → **3.152** | 20 → **3.162** |

### 5.1 El TCH ya está: derivado, y con el motivo cuando no

**Actualización del 18/11/2026.** NavData lo ha publicado en `/runways/` y `/ils/` después de esta
respuesta. No es el TCH de la carta: es **derivado** de la geometría del glide
(`elevación_antena + tan(ángulo) × distancia(antena → umbral) − elevación_umbral`), y viaja con
`tch_source` (`derived`) y `tch_unavailable` (el motivo cuando no hay valor). Verificado en vivo
contra la API esa misma tarde:

| Aeródromo / pista | `tch_ft` | `tch_source` | `tch_unavailable` |
|---|---:|---|---|
| SKRG 01 | **53,7** | `derived` | — |
| SKCG 01 | **55,9** | `derived` | — |
| SKBO 14R | **54,4** | `derived` | — |
| SKBO 14L | — | — | `incoherent_geometry` |
| **SKBG 35** | — | — | **`displaced_threshold`** |
| LEMD 18L · 18R · 32L · 32R | — | — | `displaced_threshold` |
| SKBG 17 · SKRG 19 · SKCG 19 · SKBO 32L/32R | — | — | `no_ils` |

Los tres límites que acompañan al dato, y que hay que respetar:

- **Umbral desplazado: no hay TCH derivado.** El TCH se mide sobre el umbral real y la base no trae
  la elevación del umbral desplazado; son **179 de los 4.104** extremos con ILS, y **SKBG 35 es uno
  de ellos**. Es justo la pista del hilo: para ella, el TCH de la carta, no el derivado. En la
  muestra que consultamos, **LEMD sale `displaced_threshold` en las cuatro ILS** y **KJFK en 5 de
  7**: no es un caso raro de aeródromo de montaña.
- **Sin ILS no hay TCH, y no es una carencia.** Solo **4.104 de 39.924** extremos (10 %) tienen ILS
  con senda. Las LPV tienen senda publicada pero la base no trae su TCH; las aproximaciones RNAV
  tienen legs con altitud, de los que se podría derivar una *altura de cruce*, pero no un TCH
  publicado.
- **Geometría incoherente: 1.260 extremos descartados.** `incoherent_geometry` es un «no» honesto,
  no un hueco que haya que rellenar.

**Cómo lo usamos.** `tch_ft` cuando viene, con `tch_source = derived`; la desviación contra la senda
se ancla en ese valor y se publica como **derivada**, no como «TCH de carta». Cuando
`tch_unavailable` no es nulo, el TCH va `null` y la frase cambia: «respecto a la senda de 3° que
pasa por el umbral», sin altura de cruce publicada. Para **SKBG 35** —el caso que abrió el hilo— el
TCH correcto es el de la carta, y el desplazamiento de 421 ft es el dato que falta para derivarlo.

**El `elevation_ft` de `/ils/` ya no engaña.** NavData lo corrigió el mismo 18/11/2026: ahora es
**`null` cuando el registro venía a 0** (el 40 % de las filas) y publica **`threshold_elevation_ft`**
en el mismo objeto ILS. Verificado en vivo: SKRG 01 → **6.965**, SKBG 35 → **3.854**, SKBO 14R →
**8.348**, KJFK 04L → **12**, LEMD 18L → **1.922**. El `glideslope.altitude_ft` **no** es el umbral:
en SKBO 14L son **8.425** frente a 8.354 del umbral (es la antena de senda). Para el TCH y el
`offset_threshold_ft` seguimos leyendo `/runways/`; el ILS ya sirve para el umbral sin JOIN.

**El extremo opuesto tiene un matiz:** el `elevation_ft` de la pista recíproca es la elevación de
**su** umbral, que puede estar desplazado (la 17 de SKBG tiene 496 ft de offset). Para interpolar
los últimos 500 ft de pista el error es de pocos pies, y el toque no está ahí; para el perfil del
flare es suficiente. No es un datum exacto, pero es mucho mejor que el aeropuerto.

---

## 6. `null` sí: el contrato ya lo aguanta

Confirmado por el contrato y la base; el servidor ya acepta `null` en la misma traza:

- **El DTO:** `Models/LandingUpload.cs` declara `public double? agl_ft { get; set; }` y
  `public double? radar_alt_ft { get; set; }`.
- **El serializador:** `Helpers/LandingUploadPayload.cs` usa `FiniteOrNull(s.AglFt)` y **no** pone
  `NullValueHandling.Ignore`: un `null` viaja como `null` explícito, no se omite.
- **La base y el servidor:** `Services/LandingLogService.cs` escribe `Param(s.AglFt)` (DBNull si es
  null), y el endpoint ya acepta `radar_alt_ft: null` en la misma traza (hay nulls en las muestras
  que os mandamos: radar, N2 y viento). No hace falta subir `schema_version`.

Lo que **hoy no hace** el cliente es emitirlo: `CurrentAGL` devuelve `double`, y cuando no hay plan
devuelve 0. El cambio es nuestro, no del contrato: `ReferenceAirportElevation` tiene que poder
devolver `null`, y `agl_ft` tiene que ser `null` cuando el datum no se conoce. Las dos reglas que
pedís son las correctas:

1. **`null` = desconocido, nunca 0.** El `+6.964` nace de restar 0; si la elevación no se conoce,
   el AGL tiene que ser `null`.
2. **`0` es una elevación legítima** (un aeródromo al nivel del mar), así que la distinción
   0/null **no** es la de `offset_threshold_ft`. Por eso `elevation_source` hace falta: para poder
   decir «mide 0» sin confundirlo con «no lo tengo».

Y una precisión sobre `offset_threshold_ft`: allí `0` **sí** es un valor (la pista no tiene umbral
desplazado) y `null` es «no se resolvió la pista». Son dos contratos distintos y está bien que lo
sean; solo hay que no copiar la regla de uno al otro.

---

## 7. Lo que haremos nosotros

No está en esta respuesta todavía; es el cambio que toca, y lo decimos con el alcance para que no
haya sorpresas:

1. **`alt_msl_ft` por muestra del flare** (y en la traza de 2 s si hace falta). Es aditivo; la
   columna `flare_track` y el DTO crecen, y el `POST /landing` no cambia de forma.
2. **`agl_ft` anulable de verdad.** `ResumeFlight` y `LoadFlightFromBid` dejan de poner 0: o
   rellenan la elevación con NavData, o la dejan `null`. El AGL de la traza será `null` cuando no
   haya datum; el `CurrentAGL` interno seguirá devolviendo un número para las transiciones de fase
   (con una referencia degradada explícita), pero **no se publicará como medición**.
3. **Referencia de llegada pegajosa.** Una vez que el vuelo ha entrado en `Descent`/`Approach` (o
   está a menos de X NM del destino), un `Approach → Climb` **no** devuelve la referencia al origen.
   El go-around sigue siendo el destino. Esto cierra el `−1.076` sin tocar el resto de la máquina
   de fases.
4. **Los cinco campos de pista**, desde el `NavRunway` que ya tenemos: `threshold_elevation_ft` =
   `ElevationFt`; `end_elevation_ft` = el de la recíproca; `glide_slope_deg` =
   `IlsGlideslopeAngle`; `tch_ft` = `TchFt` con `tch_source`/`tch_unavailable`;
   `tdz_elevation_ft` = `null`; `elevation_source = "navdata"`.
5. **`radio_alt_zero_ft`** (el radar mediano en tierra del vuelo) para que vuestra Capa 1 pueda
   pasar de «indicación del radar» a «altura de las ruedas» sin adivinar el addon. Si preferís que
   `radio_alt_ft` sea ya la altura de las ruedas, decidlo y lo restamos en origen; lo que no puede
   quedar es un «10 ft» que en un 737 son 0,5 ft.
6. **Marcar las trazas viejas** con el desfase estimado `elevación_del_plan − elevación_del_umbral`
   (no `−min(agl)`), y con `null` cuando el plan no la traiga.

---

## 8. Lo que necesitamos de vosotros

1. **El TCH ya está**, derivado, con `tch_source` y `tch_unavailable`; lo usamos cuando venga. Pero
   **SKBG 35 no lo tiene** (`displaced_threshold`), que es justo el caso del hilo: para esa pista,
   el de la carta. Confirmad que aceptáis `tch_ft` + `tch_source`/`tch_unavailable` en `metrics`
   (o decidnos si preferís que no mandemos el TCH cuando es derivado).
2. **La definición de `radio_alt_ft`**: ¿indicación del radar (con su cero de addon) o altura de
   las ruedas? Si es lo primero, la puerta de 10 ft no es la de 10 ft de ruedas; si es lo segundo,
   necesitáis `radio_alt_zero_ft` o lo restamos nosotros. Elegid y lo escribimos en el contrato.
3. **Usad `alt_msl_ft` + las elevaciones de pista** para la altura funcional. `agl_ft` se queda por
   compatibilidad, documentado como «altitud − elevación del plan», y **no** debe alimentar las
   puertas cuando haya `alt_msl_ft`.
4. **Clasificad las 69 trazas con la prueba del §3.3**, no con `−min(agl)`. Si nos pasáis los IDs
   de las que dan el escalón, os decimos cuáles son go-around y cuáles datum 0; el `SKCL` de −915
   debería ser una de las dos.
5. **Confirmadnos que el `POST /landing` acepta `agl_ft: null`** (creemos que sí: la columna es
   nullable y el mismo endpoint ya acepta `radar_alt_ft: null`). Si no, el cambio 2 no sirve.

---

## Anexo: cómo se ha medido

- **Traza fina del flare:** la base local `landing_log.sqlite`, tablas `flights` (55 vuelos) y
  `flare_track` (13 vuelos con traza fina desde la 0.9.37). El vuelo 55 es `ez9OqzRnYxbnGLR8`
  (SKBO→SKBG, 18/11/2026).
- **NavData:** la caché local `NavData_cache.sqlite`, AIRAC 2611, bloque del aeropuerto
  (`airport_entries.data_type = 'block'`), consultado el 17/11/2026; y la **API en vivo** el
  18/11/2026 para `tch_ft`, `tch_source` y `tch_unavailable` (SKRG, SKCG, SKBO y SKBG), abriendo el
  sobre de phpVMS con la `vms_api_key` del piloto.
- **phpVMS:** `GET /api/pireps/{id}/landing` y `GET /api/pireps/{id}/acars/logs` de
  `9o45JZ5eAVZ11qAe` y `0aNnVLMo8pxpbNPq` (clave del piloto, sobre en `attrs`), más
  `GET /api/airports/{icao}` para contrastar las elevaciones de la tabla de phpVMS.
- **Nada de geometría inventada:** las distancias, las altitudes y los rumbos son los de las trazas
  y los PIREPs reales. Las referencias «del plan» se infieren del radar en el umbral y se marcan
  como tales; no se usan para calcular el AGL que se publica.
