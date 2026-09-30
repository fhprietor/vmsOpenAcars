# vmsOpenACars → NavData — Los huecos de empalme: un criterio común, no un parche para LMML

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Asunto:** la red publicada tiene que ser **recorrible tal cual se publica**. Proponemos el criterio
> que lo mide y el reparto de trabajo; **no** pedimos que arregléis un aeropuerto.
> **Origen:** un rodaje real (LMML) donde al piloto se le pidió 15 veces volver a calles que no usó.

---

## 0. En una línea

Su dataset tiene los empalmes bien hechos donde los publica —el único de LMML es **exactamente** el
cruce de pista que el piloto hizo— pero **no publica los suficientes para que la red sea recorrible**,
así que **nuestro cliente se inventa la conectividad que falta, en silencio**, y de ahí salen rutas
equivocadas y avisos absurdos. El arreglo óptimo es de los dos lados y se mide con dos números.

## 1. El caso que lo ha hecho visible (LMML, y su dato está bien)

Vuelo `V15MObj3MxOdAZab`, LMML→DAAG, pista 05, ruta **escrita a mano** por el piloto: `T J K L`.
Su recorrido real, contado por él: por **T**, luego **J** oblicua a la derecha, **cruce del umbral de
la 23**, **K a la izquierda** y al final **zigzag con L**. O sea: la ruta escrita era la buena.

Lo que dice el dataset de LMML:

| | |
|---|---|
| nodos / segmentos | 927 / 938 |
| **componentes** | **5** (418 / 340 / 86 / 48 / 35) |
| extremos sueltos | **24** |
| huecos entre extremos | 13 de 0–25 m, 9 de 25–50 m, 1 de 50–100 m, 1 de 100–200 m |
| **empalmes publicados** | **1** |

Y el que publican es, literalmente, el cruce que hizo el piloto:

```
comp1 <-> comp4    gap=23,2 m    turn=0,2°    conf=0,74    via='J'    kind=component_bridge
```

**No tenemos ninguna queja de ese empalme**: 23 m de hueco, giro de 0,2° —cruzar una pista no es un
giro—, confianza 0,74 y el nombre de la calle. Está bien hecho y es el que el piloto usó.
**El problema es la cobertura**: es el **único** de todo el aeropuerto.

Lo que eso significa, medido con las componentes:

- El avión estaba en el puesto, y su calle más cercana era **F (64 m)**, con **T a 262 m**. F y T están
  en la **comp 2**.
- **J** está en la **comp 1** y **K** y **L** en la **comp 4**.
- Por tanto **`T → J` no tiene camino con lo publicado**: la única unión entre componentes es la que va
  de la 1 a la 4, **no** de la 2 a la 1.

## 2. Lo que hacemos hoy mientras ese empalme no existe (y lo que eso les oculta)

Nuestro grafo **fusiona extremos de segmento a ≤45 m como si fueran el mismo nodo** (es el
comportamiento por defecto: el interruptor de identidad por `node_id` está implementado y **apagado**).
Es decir: **reconstruimos por proximidad los empalmes que no publicáis**, y con esos 22 huecos de menos
de 50 m de LMML la red «se arregla» sola… **en silencio**.

Consecuencias, medidas en otros aeropuertos:

- **SKBO: 567 pares de nodos distintos a menos de 45 m** — eso es lo que la fusión **inventa**, y es
  mucho más de lo que parece: pasar a `node_id` cambia el grafo **más que quitar un umbral**.
- **KMIA**: sin empalmes publicados, la fusión por proximidad proponía la ruta de **veintiocho calles**
  para llegar a la pista, donde con identidad por nodo son seis.
- **LMML**: la ruta que el piloto escribió **no es trazable con lo publicado**; sólo existe porque
  nosotros la inventamos por proximidad.

Y aquí está la parte que les afecta a vosotros: **ese parche nuestro tapa el indicador que os diría
dónde falta trabajo**. Con la fusión por proximidad, `components: 5` no se nota en ningún sitio, porque
nuestro cliente los une de todos modos — y no siempre bien.

## 3. El daño visible: ruido en la cabina

Con la ruta escrita no trazable, nuestro cliente **anunció `VÍA T J K L` y guió la ruta del grafo**
(`F → I → … → J → K`) sin decírselo al piloto. Resultado, del log del vuelo:

```
00:09:07  FUERA DE RUTA, VUELVE A CALLE F      ← 16 s después de empezar
… 15 avisos, uno cada 20 s, nombrando F e I …
00:14:13  APROXIMANDO PISTA 23                 ← ya en J: las dos rutas convergen y el ruido para
00:14:36  CALLE K A LA IZQUIERDA EN 250 METROS ← a partir de aquí, avisos correctos
```

Cinco minutos pidiéndole volver a calles que **no usó**, mientras seguía la autorización de ATC que él
mismo había escrito. **Esa parte es un bug nuestro** (§5) y lo estamos arreglando; lo contamos porque
el ruido es lo que hace visible el hueco de datos, no al revés.

## 4. El criterio que proponemos (esto es lo que pedimos, y sirve para todos los aeropuertos)

**Principio: lo que se publica tiene que ser recorrible sin heurística del cliente.** Nada nuevo en su
API: `kind`, `confidence`, `turn_deg`, `invalid[]` y `stats` ya dan para todo esto. Lo que hace falta es
completar la cobertura y **poder medirla**:

1. **`components = 1` por aeropuerto**, o los que queden **justificados y publicados** (pavimento sin
   rodaje posible, un patio realmente aislado). Su `stats.components` ya lo dice; sólo pedimos que sea
   **un objetivo**, no un dato informativo.
2. **Todo hueco por debajo del umbral que acordemos (proponemos 200 m) tiene o un empalme o una entrada
   en `invalid[]` con su razón.** Con eso **no queda nada sin contabilizar**, y nosotros podemos apagar
   la fusión por proximidad sin quedarnos sin rutas. Es la diferencia entre «no hay empalme» y «no hay
   empalme **y sabemos por qué**».
3. **La prueba de aceptación es la ruta que el piloto escribe**: una secuencia de calles autorizada por
   ATC tiene que ser **trazable de punta a punta** en el dataset publicado. Eso se automatiza —os damos
   nuestras rutas escritas reales, que son justo las que la base de conocimiento acumula— y convierte
   esto en una medida, no en una opinión.
4. **Un número que se pueda vigilar**: junto a `joins_published` y `dangling_ends`, un
   `gaps_unaccounted` (huecos por debajo del umbral sin empalme ni `invalid`). Si baja a 0, la red es
   recorrible; mientras no lo sea, sabemos exactamente en qué aeropuertos.

## 5. El reparto, y por qué las dos mitades tienen que ir juntas

**Vuestro**: completar el emparejamiento de extremos a cada lado de la pista —los `crossings` que ya
teníais pendientes y que **este caso demuestra que no son cosméticos**— con el criterio de §4, y
publicar los recuentos por aeropuerto.

**Nuestro**, y lo hacemos con o sin vuestros empalmes porque son bugs nuestros:
- **Apagar la fusión por proximidad** (`useNodeId`) y **no inventar conectividad**. Medido ya sobre 37
  PIREPs: las dos políticas empatan en cobertura (31/31) y en recall (0,494 contra 0,488), y la
  identidad por nodo gana en **precisión** (0,615 contra 0,560). Está apagado porque sin vuestros
  empalmes deja aeropuertos sin ruta — **es exactamente la mitad que os toca**.
- **No sustituir en silencio la ruta del piloto**: si la escrita no es trazable, se dice y se pregunta,
  en vez de anunciar una y guiar otra.
- **Reportaros** los aeropuertos donde una ruta escrita no es trazable, con las componentes implicadas.
  Eso os da una lista priorizada por daño real, no por inventario.

**Ninguna de las dos sirve sola**: si apagamos la heurística sin vuestros empalmes, dejamos aeropuertos
sin ruta; si los publicáis y seguimos fusionando, el dato bueno se tira a la basura y no se nota.

## 6. Cómo sabremos los dos que funciona

| | número |
|---|---|
| Vuestro | `components` por aeropuerto y `gaps_unaccounted` (o el nombre que prefiráis) |
| Vuestro | rutas escritas del corpus trazables: **de 0 a 1** por ruta, sin heurística del cliente |
| Nuestro | la misma medición con `useNodeId` encendido, contra la traza real del piloto |
| Ambos | el test de replay del vuelo de LMML, que ya tenemos con 300 posiciones reales |

## 7. Lo que no pedimos

**No queremos un parche para LMML.** LMML es sólo el caso que lo ha hecho visible — y además es la
prueba de que **el criterio que proponemos os sale bien cuando lo aplicáis**: el único empalme que
publicáis ahí es el cruce correcto, con la calle nombrada, el hueco medido y la confianza puesta. Lo que
falta no es criterio: es cobertura, y una forma de saber cuánta falta sin que la tape nuestro parche.
