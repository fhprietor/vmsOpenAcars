# vmsOpenACars → NavData — respuesta al generador de rutas de rodaje

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 01/10/2026
> **Responde a:** `propuesta-a-vmsOpenACars-generador-rutas-taxi-2026-10-01.md`
> **Estado:** aprobamos el contrato y el enfoque por capas; hay **cinco condiciones** que cerrar
> antes de construir encima. Ninguna es cosmética: cambian lo que el cliente puede hacer con la
> respuesta. Nuestra validación empírica no espera a que estén, pero no las sustituye.

---

## 1. Lo que aprobamos sin condiciones

| Elemento | Por qué |
|---|---|
| `GET /api/v1/airport/{icao}/taxi-route/` | Encaja con `/airport/{icao}/…`; cacheable por AIRAC como ya hacemos. |
| `curated > customary > computed` | Única jerarquía honesta: el dato moderado gana y lo observado no oculta que hay una ruta generada debajo. |
| Enrutar al **punto de espera**, no al umbral | Es el error que arreglamos en v0.9.16: en SKBO 14L el umbral de Navigraph está a **75 m** del punto de espera y los nodos MSFS más cercanos son de la calle `E`. |
| `warnings[]` en vez de tapar el hueco del tramo `S` | Un aviso explícito es mejor que una ruta que parece completa. |
| `version` + `joins_used` | Sin esto no se audita una ruta ni se reproduce una discusión. |

## 2. Cinco condiciones

**a. «Sin ruta» = `200` + ruta nula + `unions_missing[]`, nunca `404`.** Acordamos «sin datos =
`200` + `customary: null`, nunca error», y nuestro cliente lee cualquier no-200 como fallo del
servicio: un `404` hace indistinguible «aquí no hay nada» de una caída. `unions_missing[]` es el
dato útil — qué empalme falta.

**b. El eje `source` colisiona entre endpoints.** En `/taxiway-joins/` `computed` es «calculado *vs*
`curated`»; en `taxi-route` sería «generado *vs* `customary` *vs* curado». La misma palabra en dos
ejes distintos ya nos obliga a ramificar por endpoint. Renombradlo (p. ej.
`origin: computed|customary|curated`) mientras no hay consumidores.

**c. El origen es el punto débil, y tenemos el caso.** Vuestra premisa «el puesto cae a 0,0 m de un
nodo» describe **SKBO**, no el dataset:

| Medición | Valor |
|---|---|
| LMML: puesto → red de calles | **64,2 m** |
| LMML: puesto → primera calle que el piloto escribió | **262 m** |
| Prototipo G74: ruta generada | `E M S A A3`, **2.257 m** |
| G74: ruta real (7 de 9 pilotos + mantenedor) | `F E M A A3` |

Pedimos dos cosas: **(i)** que el tramo de plataforma desde el puesto vaya **explícito** en la
respuesta, y **(ii)** que la primera pata se etiquete por la calle que el piloto **va a estar
pisando**, no por el nodo más cercano. Si no, nuestra guía dará `FUERA DE RUTA` falso: la regla
exige **15 s** fuera de ruta **y ≥50 m** sin acercarse a la pista, y mitiga, pero **no lo elimina**
—un avión a 262 m de la primera calle del plan los cumple de sobra—.

**d. El coste lo elegimos nosotros.** Devolved **2–3 alternativas con sus costes** (vuestra opción 2)
y decidimos en el cliente según lo que autorice ATC. Vuestro `k≈0,5` está calibrado **sobre un solo
caso** (SKBO G74→14L): sobreajuste hasta que lo midáis con más rutas. Y si una alternativa usa un
empalme de baja confianza, que lo diga:

| Empalme LMML | `gap_m` | Confianza | Nota |
|---|---|---|---|
| `J` | 23,2 | **0,96** | |
| `I\|F` | 188,3 | 0,56 | |
| `A1\|B` | 298,3 | **0,30** | `crosses_runway` |
| `G\|D` | 16,4 | 0,27 | |

`A1|B` cruza pista con confianza 0,30: queremos verlo **en `joins_used` con su confianza** y un modo
que permita **excluir** empalmes por debajo de un umbral, para poder medir el efecto.

**e. El criterio de la fase 3 tiene que ser un número explícito.** «Rutas correctas / total», el
umbral y **quién juzga**. Hoy no es medible: vuestra comparación ruta-escrita-*vs*-generada necesita
corpus y **la base tiene 0 observaciones** —el campo `Taxi Route` sólo se guarda desde el cliente
**0.9.18** y los **37 vuelos anteriores** del corpus lo tienen vacío—. Eso ya está arreglado en
nuestro lado, así que **el corpus crecerá solo**; lo que no se arregla solo es fijar el criterio
**por escrito de antemano**, o la fase 4 será una discusión sobre qué ruta «quedaba mejor».

## 3. Tres incoherencias de forma

1. §3.2 enumera **cuatro** giros (91°, 89°, 32° y **ninguno** en `A`) y cuenta «**6 giros >25°**»;
   vuestro JSON le asigna **45°** a `A`. Tabla y JSON no cuentan lo mismo.
2. En vuestro propio ejemplo, `joins_used` vuelve **vacío** pese a declarar que marca los empalmes
   usados: o no se está poblando, o el ejemplo está desactualizado.
3. Citáis «§4.3» y «la prueba de aceptación de §4.3» y **no existen en el documento que recibimos**
   (¿otro fichero?), y §2.3 habla de «vuestra observación» con nuestra base a **0 observaciones**.

Lo preguntamos sin acritud: son cosas que conviene arreglar **antes** de construir encima.

## 4. Lo que ya tenemos hecho y os afecta

| Pieza | Estado |
|---|---|
| Conectividad cerrada | **7 de 7** aeropuertos con `components_after_joins: 1` |
| Fusionado por proximidad | **45 m** (`SnapM`); **la apagaremos** cuando el ratio nos convenza |
| Radio de pavimento | **45 m** (`OnTaxiwayM`), para no llamar «fuera de ruta» a un avión que aún no ha llegado a su calle |
| Grafo SKBO | **515 nodos**, **569 segmentos** |
| RAAS | hold-short **150/40 m**, giro **250/60 m**, `FUERA DE RUTA` **15 s** y **≥50 m**, enfriamiento **20 s** |

## 5. Nuestro plan de validación

Consumir el endpoint y medir vuestras rutas contra la **traza real**: **37 PIREPs** con traza de
rodaje y el caso de **LMML con 300 posiciones**. Con esa medición delante decidimos `useNodeId` —hoy
en dos políticas: **31/31 de cobertura**, 14 idénticas y 17 distintas con **+329 m (+15%)** a favor
de la identidad por nodo—. **Sin fechas comprometidas** hasta ver el resultado.

## 6. Cierre

Necesitamos las respuestas a las **cinco condiciones** (§2.a–e), el criterio numérico de la fase 3 y
la aclaración de §3.3. Y una cosa clara: que la ruta sea **determinista y auditable** —misma
entrada, misma salida, con `version` y `joins_used`— es lo que permite discutir una ruta sin discutir
una impresión. Eso es lo bueno de la propuesta, y por eso la vamos a medir contra datos.
