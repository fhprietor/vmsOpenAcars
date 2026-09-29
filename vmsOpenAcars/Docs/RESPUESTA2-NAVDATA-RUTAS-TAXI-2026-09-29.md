# vmsOpenAcars → NavData: rutas de rodaje — respuesta a la segunda (banco de pruebas corrido)

> **Corrección posterior (29/09/2026, tras la tercera respuesta de NavData):** la lectura de §6 sobre
> las 3 observaciones de `F E M A A3` era **equivocada** — eran filas de prueba de su propia
> verificación del despliegue, borradas después, y la base quedó **vacía**. Los dos defectos que
> reportamos eran ciertos y ya están arreglados en su lado (la caché no se invalidaba con un borrado
> por `shell`). Ver `RESPUESTA3-NAVDATA-RUTAS-TAXI-2026-09-29.md`. El cuerpo se deja **tal como se
> envió**.

> **Para:** equipo de NavData · **De:** equipo de vmsOpenAcars · **Fecha:** 2026-09-29
> **Responde a:** `respuesta2-a-vmsOpenAcars-rutas-taxi-2026-09-29.md` y, ya en curso, a
> `aviso-vmsOpenAcars-rutas-taxi-disponible-2026-09-29.md` (la base de conocimiento, leída en §6)
> **Estado del cliente:** v0.9.16 (esto entra en la siguiente publicación). Suite completa **290/290**.

Pedisteis el resultado del banco de pruebas y ahí está lo interesante: **el banco cambió, y cambió a
mejor**. Abajo van las verificaciones, la secuencia antes/después con la causa medida, y **una
pregunta sobre `K1`/`V`** que nos gustaría que confirmaseis.

---

## 1. Verificado contra vuestro servicio (29/09/2026)

| Vuestra afirmación | Medición nuestra |
|---|---|
| `/holdshort/` publica `runway_names` y `type` | presentes ✅ |
| SKBO: 26 puntos (antes 35); 14L **6**, 14R **1**, 32L **11**, 32R **8** | exacto, los cuatro ✅ |
| 25 `hold_short` + 1 `ils_hold_short` | exacto ✅ (el de ILS: `32L`/`N`, `4.690323,-74.141785`) |
| El nodo de 40 m con calle `E` ya no sale | confirmado: no aparece ✅ |
| El cruce `A1/A2/A3/E` es el único punto de la 14L junto al umbral | confirmado ✅ |
| `node_id` de 52 bits | mínimo `917309580711`, máximo `4492977946105596` → cabe en `long` y es < 2^53 ✅ |

Y el cruce ahora sale con **`taxiway: "A1"`** (antes `A2`). Nos vale igual —seguimos usando
`taxiways`, no la sugerida—, pero lo anotamos porque refuerza lo que decíamos: **la sugerida es una
elección de la heurística, no el nombre que dice ATC**. En este nodo el piloto dice «A3».

---

## 2. El banco de pruebas: la secuencia cambió, y las dos diferencias se entienden

El banco es el **rodaje real completo** (`MNjR664PBAr25RbD`, SKBO, salida por la 14R): 42 posiciones
muestreadas, los 569 segmentos de rodaje, las 4 pistas y la traza real, alimentados al mismo motor de
avisos que usa la aplicación a 1 Hz. Lo hemos cambiado para que use **el selector de producción**, no
una copia de la regla: antes el banco tenía su propio bucle de «punto de espera más cercano» y **sin
filtro de pista**, así que no habría notado el aviso falso que arreglamos.

| | Secuencia de avisos |
|---|---|
| **Antes** (puntos geométricos) | `TurnAhead` · `HoldShortApproaching` · `HoldShortStop` · `HoldShortStop` · `RouteComplete` |
| **Ahora** (nodos del escenario) | `TurnAhead` · **`TurnNow`** · `HoldShortApproaching` · `HoldShortStop` · `RouteComplete` |

Siguen siendo **cinco avisos, sin ruido** (ni un «FUERA DE RUTA», que es lo que el banco vigila
desde v0.9.15). Las dos diferencias tienen la **misma causa**, y es la que veníamos midiendo:

- **Antes no salía `TurnNow`.** A las 22:08:19 el avión estaba a menos de 200 m de uno de los nodos
  de la paralela que publicaba la geometría **y yendo hacia él**, así que el aviso de punto de espera
  le ganaba el turno al de giro. Con el `HSND` real —más lejos— el giro se anuncia entero: «gira ahora
  a la derecha en calle K2». Es decir: **la sobre-generación no solo avisaba de la pista equivocada,
  también se comía avisos correctos.**
- **Antes salían dos `HoldShortStop`** (el de 40 m y la insistencia al reanudar tras los 135 s
  parado). Con un solo punto el episodio es uno.

La secuencia nueva queda: `GIRA AHORA A LA DERECHA EN CALLE K2` (22:08:19) → `APROXIMANDO PISTA 14R`
(22:11:49) → `ESPERA ANTES DE PISTA 14R` (22:12:19) → `RUTA DE RODAJE COMPLETA` (22:12:50). Los 30 s
entre «aproximando» y «espera» son coherentes con la velocidad de rodaje (~120 m).

Nuestro lado está en verde con vuestros datos: **290/290**, con el inventario nuevo
fijado en un test (`26 / 6 / 1 / 11 / 8`, y que el punto de `E` de 40 m ya no existe).

---

## 3. Una pregunta: **¿`K1`/`V` es el `HSND` de la entrada a la 14R?**

Es lo único que no nos cuadra, y lo preguntamos antes de darlo por bueno. Los cinco puntos que
publicaba la geometría para la 14R estaban entre `4.7117` y `4.7125` de latitud; el `HSND` nuevo
(`K1`/`V`, `4.710662,-74.168892`) está **unos 110–200 m al sur** de todos ellos. El log del vuelo
real dice que el avión **estuvo 135 s parado a 22 m** de uno de aquellos puntos. Si el punto de
espera de verdad es `K1`/`V`, entonces el piloto se detuvo **antes** de llegar a él (o el `HSND` que
falta está más al norte). No lo damos por error —puede ser perfectamente que el avión parase antes de
la raya, que en el simulador pasa—, pero **es la clase de dato que solo puede resolver quien tiene el
escenario**, y de ahí que lo preguntemos: ¿hay un `HSND` que no estemos viendo en la entrada de la
14R, o la parada del piloto fue realmente a ~110 m de `K1`/`V`?

---

## 4. Lo que hemos cambiado en el cliente

- **El filtro por pista pregunta a `runway_names`**, como pedisteis, y no a `runway_name`. Teníais
  razón en que hacía falta: hay puntos etiquetados `32L` cuya pareja es `["14R","32L"]` —están en la
  franja que se va a usar yendo a la 14R—, y filtrando por la etiqueta se **descartaban por error**.
  Con la pareja, entran. Queda fijado en un test.
- **`type`** mapeado (`hold_short` / `ils_hold_short`). Todavía no cambia ningún aviso: el más
  cercano por delante sigue siendo el bueno, y el de ILS, al estar más lejos, sale cuando toca.
- **`node_id`** mapeado como `long?`. Con el cambio a 52 bits entra sin problemas.
- **Corrección de una inexactitud nuestra**: en la respuesta anterior escribimos que
  «`NavTaxiway` ya lo mapea». **No era cierto** —lo teníamos pendiente—; ahora sí lo mapea. Lo
  decimos porque el documento anterior lo afirmaba.

---

## 5. Lo siguiente por nuestro lado, y lo que esperamos del vuestro

1. **Grafo de rodaje exacto con `node_id`** (borrar el umbral de 45 m): es lo siguiente, ya con los
   ids en `long`. Los **318 de 569 segmentos** de SKBO por debajo del radio son el argumento, y con
   ids no hay umbral que elegir. Reportaremos si la ruta sugerida del caso `G74 → A3` cambia.
2. **`crossings`**: vuestra §4 explica por qué no salen aún, y **encaja con lo que medimos**: en
   `/taxiways/` de SKBO no hay ni un segmento de pista (569 de 569 son de rodadura), así que no es
   «un segmento que atraviesa» sino **dos extremos emparejados** a cada lado. Preferimos que llegue
   validado —34 falsos en la 04L de KORD es exactamente el ruido que no queremos en un aviso de
   pista— y os pedimos una cosa: contadnos el criterio de emparejamiento cuando lo cerréis, porque
   nos sirve para saber qué esperar de `has_hold_short`.
3. **Base de conocimiento: ya la hemos leído** (vuestro aviso de hoy). Va abajo, con dos cosas que
   no cuadran entre endpoints.

---

## 6. La base de conocimiento, leída en vivo (y dos cosas que no cuadran)

Primero, **nuestros tests no cambian**: he vuelto a medir `/holdshort/` y `/taxiways/` después de
vuestro despliegue y el inventario es idéntico al que fijamos (26 / 6 / 1 / 11 / 8, 25+1 tipos, 569
segmentos / 515 nodos, `node_id` máximo `4492977946105596`). Y el caso que traíamos **está en vuestra
base**:

```
GET /airport/SKBO/taxi-routes/                 -> count: 1
  {"stand":"G74","runway":"14L","customary":null,
   "alternatives":[{"route":["F","E","M","A","A3"],"entry_taxiway":"A3",
                    "support":3,"total":3,"confidence":1.0}],
   "total":3,"min_total":5,"min_confidence":0.6}
```

**Es exactamente la ruta que nuestro grafo no sabe encontrar** (`F E M A A3`), y con los umbrales
funcionando como acordamos: `customary: null` a 3 observaciones porque el mínimo son 5, y la
alternativa publicada con sus cuentas. Es el caso de `TaxiRouteCaseTests` dejando de ser una
hipótesis.

### Lo que no cuadra

1. **La consulta por puesto y pista no ve lo que ve la lista.** Para el mismo par, en la misma
   sesión:

   ```
   GET /airport/SKBO/taxi-routes/?stand=G74&runway=14L
     -> {"customary":null,"alternatives":[],"total":0}

   GET /airport/SKBO/taxi-routes/
     -> el mismo par con {"alternatives":[["F","E","M","A","A3"], support 3],"total":3}
   ```

   Nos importa porque **la consulta por par es la que usaría el cliente** al preparar el rodaje: tal
   como está, leería `total: 0` y no propondría nunca lo que la base sí tiene.

2. **`taxiway-stats` dice cero observaciones** (`{"observations":0,"taxiways":{}}`) cuando la ruta
   del mismo aeropuerto suma 3. Si el contador es de otra ventana o de otra agregación, decidlo y lo
   documentamos; y si aún no está poblado, es el endpoint del que dependen el criterio nº 4 de
   nuestra §8 (`B5` no debe salir como calle de paso) y **nuestra ponderación del grafo**, así que
   nos interesa saber cuándo estará vivo.

Lo demás verificado y correcto: `200` con `customary: null` para un par sin datos (probado en SKBO
**y** en SKCG, nunca 503), y el endpoint de ingesta existe en las dos formas de escribirlo —`GET`
devuelve `405`, no `404`—.

**Y una cosa que no hemos hecho a propósito**: no hemos enviado observaciones para empujar el caso
por encima de `min_total: 5`. Dos observaciones sintéticas lo pondrían en verde y **corromperían la
base**: las tienen que poner dos vuelos reales del mantenedor por SKBO. Nuestro `POST` llega cuando
implementemos el envío, con datos de verdad.

---

Y una nota de aprecio que también es técnica: **encontrar `start_type`/`end_type` en la auditoría y
cambiar una heurística por el dato del escenario es la clase de arreglo que no vuelve**. Nuestro
selector se queda igualmente, porque el filtro por pista sigue haciendo falta cuando el avión rueda
en paralelo, pero el conjunto que recibe ya no tiene ruido.
