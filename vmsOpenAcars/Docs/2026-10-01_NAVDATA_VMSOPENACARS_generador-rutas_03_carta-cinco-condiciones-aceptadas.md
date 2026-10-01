# NavData → vmsOpenACars: las cinco condiciones, aceptadas — y el criterio de la fase 3, por escrito

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_SUYA_generador-rutas_02_aprobacion-con-cinco-condiciones.md` (vuestro
> mensaje, que nos llegó como `vmsOpenACars-a-NAVDATA-generador-rutas-2026-10-01.md`)
> **Adjuntos:** **anexo 1** `2026-10-01_NUESTRA_generador-rutas_03_errata-propuesta.md` ·
> **anexo 2** `2026-10-01_NUESTRA_generador-rutas_03_formato-tanda-1.md`. El documento original **no se toca**: solo se corrige en el anexo 1.
> **Estado:** las **cinco condiciones se aceptan**. Quedan **7 puntos de los doce** sin cerrar; al
> final van con un valor por defecto para que no haga falta otra ronda. Nada de esto lleva fecha de
> fase 4: la decide vuestra medición.

---

## 0. Resumen en cinco líneas

1. Las tres incoherencias eran ciertas. **Dos eran errores nuestros** (un giro de 45° inventado y un
   recuento que no cuadraba con el JSON); la tercera era falta de explicación. Van corregidas en el
   **anexo 1 (errata)**, no en el documento original, que se queda como os llegó.
2. **Aceptamos las cinco condiciones**, incluidas las dos que cambian el contrato (`origin` en vez de
   `source`; `200` + ruta nula en vez de `404`).
3. **Reproducimos vuestros números con nuestro propio dataset**: LMML 64,2 m y 261,3 m clavados, la
   tabla de empalmes exacta (y con un `crosses_runway` que a vosotros se os escapó en `G|D`).
4. **El criterio de la fase 3 va en §3 de este documento, pre-registrado y con números**, incluido
   quién juzga y cómo se dirime una discrepancia.
5. Vuestra condición (c) es la más importante de las cinco y la aceptamos sin matices: con `SnapM`
   45 m y la regla de 15 s + ≥50 m, un origen a 262 m **genera falsos `FUERA DE RUTA`**. Eso se
   arregla publicando la plataforma y etiquetando la pata por donde se pisa, no subiendo el umbral.

---

## 1. Las tres incoherencias y las cifras corregidas

**El detalle va en el anexo 1** (`2026-10-01_NUESTRA_generador-rutas_03_errata-propuesta.md`),
para que quede junto al documento original que ya tenéis y no lo sustituya. Resumen:

- **Las tres eran ciertas.** Dos eran errores nuestros — un giro de **45° que no existía** (inventado
  al redactar el ejemplo) y un recuento de giros que no cuadraba con el JSON — y la tercera era falta
  de explicación: `joins_used: []` era **correcto** (esa ruta no usa ningún empalme), no un campo sin
  poblar.
- **Y una cuarta, que apareció al implementar el generador:** las cifras de la propuesta venían de un
  prototipo con búsqueda **aproximada**. El buscador definitivo encuentra con el perfil por defecto
  una ruta distinta y más barata: **`E N A S A A3`, 2.269,0 m** (3 giros > 25°), frente a la
  `E M S A A3` de 2.256,6 m (6 giros) que os propusimos.
- **El documento original lleva su `sha256` en el anexo 1** para que podáis comprobar que vuestra
  copia y la nuestra son la misma antes de discutir nada.

---

## 2. Las cinco condiciones

### (a) «Sin ruta» = `200` + ruta nula + `unions_missing[]`, nunca `404` — **aceptado**

Tenéis razón y además es coherente con lo ya acordado para `customary: null`. Contrato:

```
200 OK
{ "icao": "LMML", "origin": "computed", "route": null,
  "reason": "no_route_in_published_network",
  "unions_missing": [ { "taxiway_a": "...", "taxiway_b": "...", "gap_m": 340.2,
                        "lat_a": …, "lat_b": … } ] }
```

Precisión que sí mantenemos, para no confundir «no hay dato» con «petición mal formada»:

| Caso | Respuesta |
|---|---|
| No existe camino en la red publicada | **`200` + `route: null`** + `unions_missing[]` + `reason` |
| Pista o puesto inexistentes / parámetros inválidos | `400 BAD_REQUEST` (`stand` o `runway` desconocidos) |
| Fallo real de un servicio externo | `503 UPSTREAM_UNAVAILABLE` — **no aplica aquí**: el generador no tiene upstream |

El `404` queda descartado para este endpoint. (Nota interna: `/nearest/approach-airport/` sí usa `404`
para «sin resultado»; es una incoherencia **nuestra** entre endpoints que arrastramos, y este contrato
la deja señalada.)

### (b) `source` → `origin` — **aceptado**

Tenéis razón en el diagnóstico, y es peor de lo que decís: `source` ya significa **tres** cosas
distintas en la API — `msfs|airac` en `/parkings/`, `computed|curated` en `/taxiway-joins/` y
`community|curated` en `/taxi-routes/`. El endpoint nuevo usará:

```
"origin": "computed" | "customary" | "curated"
```

Y dejamos escrito lo que **no** vamos a hacer sin acuerdo: **no renombramos los `source` ya
desplegados** en `/parkings/`, `/taxiway-joins/` ni `/taxi-routes/`, porque tienen consumidores
(vosotros). Si queréis unificarlos, se hace con un cambio versionado y avisado, no de tapadillo.

### (c) El origen es el punto débil — **aceptado, y vuestros números son exactos**

Reproducidos con nuestro dataset, sin usar vuestro fichero:

| Medición | Vuestro valor | Nuestro valor | Veredicto |
|---|---|---|---|
| LMML: posición del PIREP → nodo más cercano | 64,2 m | **64,2 m** (nodo en `F`) | exacto |
| LMML: posición del PIREP → primera calle escrita (`T`) | 262 m | **261,3 m** | exacto |
| LMML: puesto → red (60 puestos) | — | mín **14,8** · mediana **42,0** · máx **271,6** m | vuestro punto, confirmado |
| SKBO: `G74` → nodo | 0,0 m | **0,0 m** | es una propiedad de SKBO, no del dataset |

Es decir: nuestra frase «el puesto cae a 0,0 m de un nodo» describía SKBO y la escribimos como si
fuera general. **Corregido.** Diseño que proponemos, que ataca las dos mitades de vuestra condición:

1. **La plataforma va explícita, como paso propio**, nunca dentro de una calle:
   `{"seq": 0, "kind": "apron", "taxiway": null, "distance_m": 64.2, "bearing": 318,
     "from": {"lat": …, "lon": …}, "to": {"lat": …, "lon": …}}`
   y un campo de cabecera `stand_to_network_m`. Si supera un umbral (p. ej. 150 m) se añade
   `warnings[]` con `kind: "long_apron_leg"`, porque significa que el escenario no modela esa
   plataforma.
2. **El nodo de entrada se elige por el rumbo del puesto, no por distancia en línea recta.** En
   `/parkings/` ya publicamos `heading` (magnético): la idea es coger el nodo que el avión **va a
   pisar** rodando en ese rumbo, no el más cercano a plomo. **Honestidad sobre esto: es una
   propuesta, no una medición.** En el caso LMML lo único que sabemos medido es que el nodo más
   cercano está en `F` y la ruta escrita empieza por `T` (261,3 m); si el rumbo lo resuelve o no, lo
   mediremos y lo publicaremos **antes** de defenderlo.
3. **La primera pata se etiqueta por la calle que se pisa**, y si el primer tramo de red no
   coincide con la primera calle escrita del plan, sale en `warnings[]` (`street_label_differs`).

Lo que **no** podemos hacer y conviene decirlo: si el escenario no modela la plataforma (no hay
segmentos `taxi_path` de tipo `P` entre el puesto y la red), el tramo de plataforma es una **recta
declarada como tal** — no un camino inventado. Donde sí haya pavimento modelado (`P`), se rutea por
él. Así vuestra regla de 15 s + ≥50 m no dispara contra una calle que aún no se ha alcanzado.

### (d) El coste lo elegís vosotros — **aceptado**

Aceptamos las tres partes:

1. **2–3 alternativas con sus costes**, y decidís en el cliente según lo que autorice ATC.
2. **Cada alternativa con su propio `joins_used`**, con `gap_m`, `confidence` y `crosses_runway`.
3. **Modo de exclusión**: `?min_join_confidence=0.5` (por defecto 0: nada se excluye) para poder
   medir el efecto de no usar empalmes dudosos.

Y aceptamos la crítica de fondo: **`k ≈ 0,5` está calibrado sobre un solo caso** (SKBO G74→14L) y es
sobreajuste hasta que lo midamos con más rutas. Por eso el coste viaja como **perfil**, no como
constante escondida: cada alternativa dirá con qué coste se calculó, y publicaremos los perfiles
(`k ∈ {0, 0.5, 1.0}` y penalización de empalme) para que podáis reproducir y comparar.

> **Medido, y con vuestro caso de LMML** (PIREP `T J K L` → hold-short de la 05, el que está a
> **76,7 m** del umbral AIRAC): la ruta generada sale **`F I J K L`, 3.914 m**, y usa **2 empalmes —
> `J` (23,2 m, conf **0,96**) e `I|F` (188,3 m, conf **0,56**)**— evitando los dos de baja confianza,
> `A1|B` (0,30, cruza pista) y `G|D` (0,27). Con `min_join_confidence=0.6` el `I|F` se cae y la ruta
> cambia: es exactamente el experimento que pedís y ya lo podemos correr. De paso confirma la
> condición (c) en un segundo aeropuerto: **el primer tramo es `F` y la ruta escrita empieza por
> `T`**.

### (e) El criterio de la fase 3 — **aquí está, en §3**

Es el punto que más nos importaba de vuestra respuesta, y estamos de acuerdo: sin criterio previo la
fase 4 sería una discusión sobre qué ruta «quedaba mejor». Va pre-registrado abajo.

---

## 3. Criterio de la fase 3, pre-registrado

**Se fija hoy, antes de medir. Cambiar cualquier umbral exige una versión nueva y fechada de este
documento con su justificación.** Nada de mover la portería.

### 3.1 Material

- **Tanda 1 (congelada)**: los **37 PIREPs** con traza de rodaje y el caso **LMML de 300 posiciones**.
  Se miden contra el `version` de red que se congele con la tanda y se publica ese hash.
- **Formato de intercambio** (propuesta, decid lo que os sea más cómodo): un JSON/CSV por tanda con
  `icao, stand, runway, route_written[], trace[{lat, lon, t}]`. No hace falta endpoint nuevo: es un
  fichero de validación, no un dato de producción.
- **Baseline obligatoria**: las mismas métricas sobre (i) la ruta `customary` cuando exista y (ii) el
  comportamiento actual con vuestro `SnapM`. La decisión de la fase 4 es *generador contra el
  estado actual*, no *generador contra la perfección*.

### 3.2 Las cinco métricas

| # | Métrica | Definición | Umbral |
|---|---|---|---|
| 1 | `trace_coverage_pct` | % de puntos de traza a ≤ `OnTaxiwayM` (**45 m**) de la polilínea generada | mediana **≥ 95 %** y ninguna ruta **< 80 %** |
| 2 | `median_lateral_m` | mediana de la distancia traza→polilínea | **≤ 25 m** |
| 3 | `street_match_pct` | coincidencia de secuencia con la ruta escrita, en los dos sentidos: generada ⊆ escrita (no inventamos calles) y escrita ⊆ generada (no nos falta ninguna), preservando orden | **≥ 80 %** de rutas en ambos sentidos |
| 4 | `false_offroute_events` | simulacro de **vuestra** regla RAAS (15 s + ≥ 50 m, enfriamiento 20 s) sobre la traza real contra la ruta generada | **0** en la tanda 1 |
| 5 | `low_conf_joins_declared_pct` | % de empalmes con `confidence < 0.5` usados que aparecen declarados en `joins_used` | **100 %** (es seguridad, no calidad: no se negocia) |

La métrica 4 es **la que decide**. Las 1–3 dicen si la ruta es buena; la 4 dice si vuestro piloto ve
un `FUERA DE RUTA` falso, que es el dolor real. Pedimos que nos confirméis la lista exacta de
parámetros RAAS que simulamos (nos disteis: hold-short 150/40 m, giro 250/60 m, fuera de ruta 15 s y
≥ 50 m, enfriamiento 20 s) para implementarla idéntica.

### 3.3 Regla de decisión para la fase 4 (apagar `SnapM`)

Se apaga si **y solo si**, sobre la tanda 1 congelada:

1. se cumplen los cinco umbrales de §3.2, **y**
2. el generador **no empeora** el baseline en las métricas 1–3, **y**
3. la métrica 4 es **0**.

Si la 4 no es 0, **no se baja el umbral**: se publican los casos que fallan, uno por uno, con su
nodo y su distancia, y se arreglan (origen por rumbo, plataforma explícita, empalme que falta). La
tanda no se repite hasta que haya una corrección real que medir.

### 3.4 Quién juzga y cómo se dirime

- **Juzga la traza**, no la opinión. Las dos partes calculamos las mismas cinco métricas con la misma
  definición y el mismo `version`.
- Publicamos **el CSV por ruta** con todas las métricas, los parámetros usados y el hash de red: la
  discusión se tiene sobre ese fichero, no sobre una impresión.
- **Discrepancia > 5 %** en cualquier métrica: se re-ejecuta sobre el mismo `version` y el mismo
  fichero de entrada. Si persiste, la **definición** es ambigua y se publica una v2 de la definición
  **antes** de decidir nada.
- Con el corpus creciendo solo (desde 0.9.18), cada **tanda nueva de ≥ 25 rutas** se mide con el
  mismo criterio; una regresión en cualquier métrica **reabre** la decisión.

---

## 4. Lo que sigue abierto de los doce puntos (con valor por defecto)

Contestasteis 5 de los 12. De los 7 restantes, **si no decís lo contrario haremos lo de la derecha**
— así no hace falta otra ronda para empezar la fase 1:

| # | Punto | Nuestro valor por defecto |
|---|---|---|
| 3 | Sentido de marcha (`start_dir`/`end_dir`) | **Bidireccional** en v1 (como hoy). Exponemos el dato del escenario como `?one_way=1` en v2, porque respetarlo puede romper rutas que hoy funcionan |
| 4 | Nivel de detalle | **Los tres**: pasos con giro+grados+rumbo, polilínea y `node_id`s (para vuestro `useNodeId`) |
| 5 | Unidades y referencia | **Metros** (coherente con `gap_m`) y rumbo **magnético** |
| 6b | Entrada a pista (`A1`/`A2`/`A3`) | Devolvemos las válidas como alternativas y elegimos la más barata por defecto; podéis fijarla con `?entry=A3` |
| 8 | Reencaminamiento | **Sí** en v1: `?lat=&lon=` (clave de caché redondeada a 0,0005°) |
| 9 | Caché | TTL **24 h** + `version`, con `Cache-Control: private, max-age=86400` (regla del borde: `/api/v1/*` **no** se cachea en Cloudflare) |
| 12 | Plan y slug | Slug **`taxi_route`**; activo en todos los planes durante el piloto y gateado después |

Aparte, dos cosas vuestras que apoyamos y que nos sirven:

- **`useNodeId`**: vuestro resultado (31/31 de cobertura, 14 idénticas, 17 distintas con +329 m,
  +15 % a favor de la identidad por nodo) es exactamente el marco en el que está construido nuestro
  grafo: nodos por hash de 52 bits, sin fusión. Podéis reproducir el experimento contra nuestro
  `version` y las dos partes miramos lo mismo.
- **`G|D` cruza pista.** En vuestra tabla de LMML sale sin marcar; en nuestro dato es
  `crosses_runway: true`, igual que `A1|B`. Si vais a excluir empalmes por confianza, conviene que
  ese flag entre en la decisión: `G|D` es corto (16,4 m) pero cruza. Confirmadnos si os cuadra.

---

## 5. Lo que ya está hecho y lo que falta

Sin fechas de fase 4 —la fija vuestra medición—, pero sin esperar:

| Pieza | Estado |
|---|---|
| **Generador** (grafo publicado, coste con giros, alternativas, `joins_used`, `min_join_confidence`, plataforma explícita, `200` + ruta nula) | **hecho** — `apps/navdata/taxi_route.py`, con las cifras de este documento fijadas en tests |
| **Arnés de las cinco métricas** + simulacro de la regla RAAS | **hecho** — `apps/navdata/taxi_eval.py` y el comando `check_taxi_routes` |
| **Formato de la tanda** | **hecho** — `docs/2026-10-01_NUESTRA_generador-rutas_03_formato-tanda-1.md`, con un ejemplo ejecutable (trazas sintéticas, marcadas como tales) |
| Origen por **rumbo** del puesto (la mitad fina de vuestra condición (c)) | **pendiente a propósito**: no está implementado porque su efecto **no está medido**. El tramo de plataforma explícito sí lo está |
| Endpoint HTTP detrás del slug | pendiente de que cerréis §4 (los siete puntos con valor por defecto) |

Lo que os pedimos para encender la fase 2: **la tanda 1** (37 PIREPs + LMML 300) en el formato de
`docs/2026-10-01_NUESTRA_generador-rutas_03_formato-tanda-1.md`, y la **confirmación de los parámetros RAAS** que simulamos.

---

## 6. Qué os adjuntamos, y qué no

| # | Fichero | Qué es |
|---|---|---|
| **1** | `2026-10-01_NUESTRA_generador-rutas_03_cinco-condiciones-aceptadas.md` | **Esta respuesta.** Es lo último y lo que se contesta |
| **2** | `2026-10-01_NUESTRA_generador-rutas_03_errata-propuesta.md` | **Anexo 1**: la errata de la propuesta, con el `sha256` del original |
| **3** | `2026-10-01_NUESTRA_generador-rutas_03_formato-tanda-1.md` | **Anexo 2**: el formato con el que nos tenéis que mandar la tanda 1 |
| — | `propuesta-a-vmsOpenAcars-generador-rutas-taxi-2026-10-01.md` | **No se reenvía y no se modifica**: es el documento que ya tenéis, congelado en `sha256 e005abce691468b4ae658298234b3fe191e667c662807f97613932f89c2a5bb5` |

Evidencia de la propuesta, sin cambios (solo si queréis volver a ella):

| Fichero | `sha256` |
|---|---|
| `skbo-ruta-F-E-M-A-A1-RW14L.geojson` | `6fcb65239b4053f338f1ecf444a7136a7c8d96b5e20d13d99de30e62d8453778` |
| `skbo-taxiway-a-recta-paralela.geojson` | `89d739171fc8775539cc00285767995b9b80df12e02e1ecd7ff795f7109c58a8` |

Los tres ficheros de arriba están en el repo sin commitear: los entrega quien corresponda, no salen
solos.

Quedamos a la espera de vuestras respuestas a **§4** (los siete puntos que siguen abiertos, con su
valor por defecto para que baste un «ok») y de **la tanda 1**.

Un saludo,
**equipo de NavData**
