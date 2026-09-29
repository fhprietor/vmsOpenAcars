# Pedido a NavData — empalmes y criterios que sirvan en la mayoría de los aeropuertos

**Fecha:** 29/09/2026 · **De:** vmsOpenACars (cliente ACARS) · **Para:** equipo de NavData
**Antecedente:** `Docs/PEDIDO-NAVDATA-RUTAS-TAXI.md` y `Docs/RESPUESTA-NAVDATA-RUTAS-TAXI-2026-09-29.md`.
**Estado:** los `node_id` en `/taxiways/`, los puntos de espera desde los tipos de nodo del escenario y
el endpoint de empalmes curados **ya los hemos integrado**. Esto es lo que falta para poder encender la
identidad por nodo **en cualquier aeropuerto**, no en uno.

---

## 1. Qué medimos, y de qué nos corrigió la medición

Corrimos el grafo sobre **37 PIREPs reales, 8 pilotos y 14 aeropuertos** (clientes 0.8.9–0.9.16),
comparando las dos políticas de identidad sobre los **mismos** puntos de origen y destino:

| | ruta | |
|---|---|---|
| fusión por proximidad (la que usa hoy el cliente) | **30** de 36 medibles | SKYP no publica red |
| identidad por `node_id` | **29** | |
| misma ruta con las dos | **13** | |
| ruta distinta | **16** | |
| solo con proximidad / solo con ids | **1 / 0** | |
| longitud media | 2.130 m → 2.432 m | **+303 m (+14%)** |

Y el caso que lo ilustra es **KMIA** (3.138 segmentos, **0 empalmes publicados**), el vuelo `VHR31`
KMIA→SKRG: con `node_id`, del puesto **G181 a la entrada de la 08R no encuentra ninguna ruta**. Es el
único vuelo del corpus donde una política encuentra ruta y la otra no.

**Nos corrigió en algo importante:** al principio atribuimos la ruta mala de ese vuelo al *destino* del
grafo (el umbral de Navigraph en vez del punto de espera). Lo probamos con el código de producción y
**era falso**: el punto de espera de acceso de la 08R está a **84 m** del umbral, o sea en el mismo sitio
para el grafo, y las dos variantes dan la misma ruta de 28 calles (+`L1`). La causa es la **topología**
de la red, no el destino. El test `TaxiSuggestionKmIaTests` reproduce, calle por calle, la ruta que ese
vuelo anunció en su log — así que lo que sigue es un hecho reproducible, no una impresión.

## 2. El problema de fondo: un umbral fijo no puede servir a todos los aeropuertos

Hoy la fusión por proximidad usa **45 m**, un número que no sale de ningún dato. Medido sobre vuestro
dataset:

- **SKBO**: 515 nodos, **100 extremos sueltos**, **36 huecos de 45–200 m**, mediana de 35,3 m al nodo más
  cercano… y **567 pares de nodos distintos a menos de 45 m** que la fusión **inventa** como unión sin
  que exista en el escenario. El umbral afecta a **318 de los 569 segmentos** (el 56%).
- **KMIA**: 3.138 segmentos, **0 empalmes**. Una red así, con la identidad por nodo y sin empalmes, es
  inutilizable: es el caso de arriba.
- **KBOS** 1.477 segmentos / **0** empalmes · **CYUL** 608 / **0** · en todo el corpus hay **un** empalme
  publicado: el `K2`→`K1` de SKBO (76,0 m).
- Y los umbrales que sí existen en los datos **varían hasta 5×** entre fuentes: el desfase entre el
  umbral de Navigraph y el punto de espera del escenario va de **33 m** (SKBO) a **163 m** (LEMD).

Conclusión: **ni 45 m ni ninguno otro valor fijo sirve** para un aeropuerto denso tipo KMIA y para uno
disperso tipo SKBO a la vez. Lo que sirve es que el criterio se **calcule de los propios datos**.

## 3. Lo que pedimos — cinco cosas, todas generales

### 3.1 Empalmes **calculados**, no curados a mano

No pedimos una lista para KMIA: eso no escala a 30.000 aeropuertos y os deja de mantenedores a vosotros
para siempre. Pedimos que **el servidor detecte los huecos de forma automática** y publique los
candidatos **con el criterio a la vista** (abajo), de modo que el resultado sea el mismo para cualquier
aeropuerto del mundo sin intervención manual. Lo curado (`source: "curated"`) sigue existiendo **encima**
del cálculo, para corregir donde el criterio se equivoque — no como único mecanismo.

### 3.2 Que cada empalme diga de dónde sale y con cuánta confianza

`GET /airport/{icao}/taxiway-joins/` ya trae `node_a`, `node_b`, `taxiway`, `gap_m`, `source`, `invalid[]`.
Pedimos añadir **`confidence`** (0–1) y distinguir `source: "computed" | "curated"`. Con eso el cliente
puede decidir un umbral por confianza —y no por distancia— y **dejar fuera** los empalmes dudosos sin
perder los buenos. Sin ese campo, o los aceptamos todos o ninguno.

### 3.3 Estadísticas por aeropuerto (lo que hace que el cliente sepa cuánto fiarse)

Un objeto pequeño por aeropuerto, o un campo en la respuesta de los empalmes:

```
nodes, segments, dangling_ends,
median_gap_m, gaps: { "0-25": n, "25-50": n, "50-100": n, "100-200": n, ">200": n },
median_segment_m, joins_published, joins_curated, runs
```

Con eso el cliente **deriva su umbral de vuestros datos** en vez de llevar el 45 m escrito a mano, y
sabe **de antemano** si en ese aeropuerto la red está completa o no. Es también vuestro mejor indicador
de calidad: un aeropuerto con 100 extremos sueltos y 0 empalmes es un aeropuerto donde el grafo miente.

### 3.4 La calle de entrada de **cada** pista, no la de un caso

Ya publicáis `taxiways` y `runway_names` en los puntos de espera, y con eso resolvemos el acceso. Pedimos
que el **cruce pista ↔ nodo de entrada** esté calculado para **todas** las pistas de **todos** los
aeropuertos (es lo que hace que `HoldShortSelector` no tenga que adivinar por geometría), y que el mismo
cálculo alimente `crossings` —los cruces de pista con `has_hold_short`—, que sigue pendiente y que el
rodaje real de KMIA demuestra que hace falta: yendo a la 08R el cliente avisó correctamente de
**hold short de la 27 y de la 30**, que son cruces reales de la ruta.

### 3.5 Una versión del dataset de calles

Un `version` o `hash` por aeropuerto: cuando cambie la red, el cliente invalida su caché en vez de
arrastrar empalmes de una versión anterior. Hoy no hay forma de saberlo.

## 4. El criterio que proponemos (abierto a lo que midáis vosotros)

Para que se entienda qué pedimos, esta es nuestra propuesta — **a validar con vuestros datos**, y
preferimos que la mejore quien ve todas las redes:

1. **Candidato**: dos nodos de **grado 1** (extremo suelto) que no pertenezcan al mismo segmento.
2. **Distancia**: `gap ≤ max(3 × mediana_segmento_aeropuerto, 40 m)`, con tope de **200 m**. En SKBO
   (mediana 35 m) eso da ~105 m, que cubre los 36 huecos medidos sin inventar los 567 pares <45 m.
3. **Continuidad**: el cambio de rumbo entre el segmento que llega a un extremo y el que sale del otro
   **≤ 60°** — dos calles que se cruzan en ángulo recto casi nunca son la misma calle.
4. **Compatibilidad de nombre**: mismo `taxiway`, o uno que la geometría sugiera como continuación. Un
   empalme que uniría `A` con `B` en perpendicular es candidato a **no** publicarse.
5. **Que no acorte por donde no se rueda**: el empalme no puede crear un camino más corto hacia la pista
   que pase **por dentro** de otra pista o que evite un punto de espera. Aquí es donde `crossings` ayuda.
6. **`confidence`** = combinación de (2) normalizada, (3) y (4). Publicar todo, pero con la nota puesta:
   el cliente decide.

Si preferís otro camino —por ejemplo detectar el hueco por **continuidad de la polilínea** en vez de por
distancia—, nos sirve igual: lo que necesitamos es que el criterio **sea calculable en cualquier
aeropuerto** y que venga acompañado de la confianza y de las estadísticas.

## 5. Lo que haremos nosotros con eso (la inteligencia, del lado del cliente)

Para que se vea que no os pedimos el trabajo entero:

- **Umbral derivado de los datos**, no fijo: `TaxiGraph` pasará a calcular su radio de fusión desde
  `median_segment_m` y la distribución de huecos que nos deis, con el 45 m actual solo como respaldo
  cuando no haya estadísticas.
- **El interruptor de `node_id` deja de ser global y pasa a ser por aeropuerto**: se enciende donde los
  empalmes cubran los huecos (medido con el banco del corpus, que ya compara las dos políticas sobre 37
  vuelos reales) y se queda apagado donde no. Hoy está apagado en todos porque en ninguno está cubierto.
- **Criterio de aceptación, publicado**: se enciende cuando la cobertura con ids **iguale la de hoy sin
  alargar las rutas**. La medición está en `TaxiCorpusMeasurementTests` y se vuelve a correr tal cual.
- **Nada de observaciones sintéticas**: el banco de rutas reales se llena con lo que ruedan los pilotos.
  Lo que os mandamos son los casos medidos y los tests, no votos inventados.

## 6. Cómo reproducir todo lo que decimos

- `vmsOpenACars.Tests/TaxiCorpusMeasurementTests.cs` — las dos políticas sobre el corpus, con la tabla en
  `%TEMP%\taxi_corpus_measurement.txt`. Fixtures: `Fixtures/taxi-corpus-2026-09-29.csv` (37 vuelos) y
  `Fixtures/taxi-networks-2026-09-29.csv` (13 redes, 6.637 líneas, KMIA incluido).
- `vmsOpenACars.Tests/TaxiSuggestionKmIaTests.cs` — el caso de KMIA con la ruta del log **reproducida**
  calle por calle, y el punto de espera a 84 m del umbral. Fixture:
  `Fixtures/KMIA-holds-runways-2026-09-29.csv` (8 pistas, 170 puntos de espera).
- `vmsOpenACars.Tests/TaxiRouteCaseTests.cs` — el caso `G74 → A3` de la 14L con 237 segmentos reales.

Los tres corren **sin red**: cualquiera puede verificar lo que afirmamos en un `vstest`.
