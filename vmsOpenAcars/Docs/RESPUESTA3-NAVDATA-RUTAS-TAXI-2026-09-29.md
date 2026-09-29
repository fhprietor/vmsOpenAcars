# vmsOpenAcars → NavData: rutas de rodaje — respuesta a la tercera

> **Para:** equipo de NavData · **De:** equipo de vmsOpenAcars · **Fecha:** 2026-09-29
> **Responde a:** `respuesta3-a-vmsOpenAcars-rutas-taxi-2026-09-29.md`
> **Estado del cliente:** v0.9.16 (esto entra en la siguiente publicación). Suite completa **290/290**.

Verificados vuestros tres puntos desde nuestro lado, y **una corrección nuestra**: lo que leímos en
§6 de nuestra respuesta anterior era vuestro dato de prueba, no la base poblada. Lo decimos abajo y
queda corregido también en nuestro repositorio.

---

## 1. `K1`/`V`: reproducido al metro, y explica nuestro banco

| Vuestro número | Nuestra medición |
|---|---|
| `K1`/`V` a **58 m** del umbral de la 14R | **58,4 m** ✅ |
| **+48 m** a lo largo del eje, **−34 m** lateral | **+48 / −34** ✅ |
| Único `HSND` de la 14R | confirmado: es el único ✅ |
| Único `ils_hold_short` de SKBO: `32L`/`N` | confirmado, y a **58 m** del umbral publicado de la 32L |

**Lo que sí conviene alinear** (no es un error, es el origen de la medida): los 1.304 m / 1.905 m que
dais a los nodos siguientes están medidos **desde el umbral**; desde `K1`/`V` son **1.254 m** y
**1.847 m**. Y vuestro umbral de la 14R (`4.710678,-74.169418`) está a **31 m** del que publica
`/runways/` (`4.7104945,-74.1691589`). Los dos conjuntos son correctos, pero si publicáis cifras,
decid de qué punto salen: nos ahorra la comprobación de si lo que no cuadra es el cálculo o el
origen.

**Y vuestra respuesta cierra nuestro banco de pruebas.** El avión estuvo 135 s parado **65–170 m
antes** del `HSND`, así que en ese momento no había «ESPERA» que dar; la que sí sale (22:12:19,
`ESPERA ANTES DE PISTA 14R`) es posterior, cuando de verdad llegó a menos de 40 m del nodo, y entra
a pista 30 s después. Es decir: **el dato del escenario y la traza real cuentan la misma historia**,
que es justo lo que el banco tenía que comprobar cuando cambiastéis la fuente de los puntos.

---

## 2. Las dos incoherencias: verificadas arregladas, y gracias por el borrado

Desde nuestro lado, los tres endpoints coinciden ahora en cero:

```
/airport/SKBO/taxi-routes/?stand=G74&runway=14L  -> customary: null, alternatives: [], total: 0
/airport/SKBO/taxi-routes/                       -> count: 0, routes: []
/airport/SKBO/taxiway-stats/                     -> observations: 0, taxiways: {}
```

Y **la corrección que nos toca**: en nuestra respuesta anterior leímos esas 3 observaciones como «el
caso ya está en vuestra base». Eran **vuestro dato de prueba de la verificación del despliegue**.
Estuvimos a punto de celebrarlo como el caso resuelto; ahora sabemos que la base está **vacía** y que
la primera observación real será la del mantenedor. Lo dejamos escrito así en nuestro `CHANGELOG`,
porque un repo que dice «la base ya trae el caso» cuando eran filas de prueba es exactamente el tipo
de afirmación que no queremos que sobreviva a esta conversación.

La causa raíz —la caché de 24 h que un borrado por `shell` no invalidaba— nos parece **el mejor
hallazgo de los tres documentos**: no era un filtro mal escrito, era un dato viejo conviviendo con
datos frescos, que es el fallo que más cuesta ver porque cada endpoint, por separado, parece correcto.
Que ahora lo invalide cualquier escritura (admin, `shell` o ingesta) cierra la clase entera.

---

## 3. `crossings`: nos vale el criterio, con una observación

El criterio (extremos fuera del pavimento y en lados opuestos, emparejados por alineación **y**
continuidad de calle, punto publicado sobre el eje con la pareja y `has_hold_short` a 40 m) nos
parece el correcto, y preferimos esperar a que esté validado en varios aeropuertos: 34 falsos en la
04L de KORD es exactamente el ruido que no queremos en un aviso de pista.

Una observación útil para cuando lo cerréis: **40 m es también nuestro radio de insistencia** —el
`ESPERA ANTES DE PISTA` sale a 40 m del punto de espera—, así que un cruce con
`has_hold_short: true` cae donde ya estamos avisando. El que nos falta de verdad es
`has_hold_short: false`: cruzar pavimento sin nodo de espera y sin decir nada.

Y una nota de diseño: con `node_id` y la continuidad de calle podríamos calcular los cruces en el
cliente, pero **no lo vamos a duplicar**. Un dato, una fuente.

---

## 4. Las dos adendas: la `V` verificada, y el dato que cambia mi plan del grafo

**La `V` (Adenda 2), verificada desde nuestro lado**: el punto de la 14R publica ya `taxiways:
["K1"]`, **no queda ninguna `V` en ningún `taxiways`**, los recuentos siguen en 26 / 6 · 1 · 11 · 8 y
`V` sigue existiendo en `/taxiways/` como un único segmento. Nuestro test del inventario ahora
comprueba además que no reaparezca, así que si alguien la vuelve a publicar lo dice. Buena caza la de
las cartas: un nombre que no está en ninguna carta no debería ser el que le demos al piloto.

**Los huecos (Adenda 1): coincidimos en el orden de magnitud, y hay un número que cambia mi plan.**
Medido por nuestro lado en SKBO: **515 nodos** con id, **100 extremos sueltos**, distancia al nodo más
cercano mediana **35,3 m** (p75 51,1 · p90 73,8 · máx 144,4) y **36 huecos entre 45 y 200 m**. Los
vuestros son 470 nodos / 89 extremos / mediana 33,6 / 25 huecos: **el conjunto de nodos es distinto**
(el nuestro cuenta todos los ids de `/taxiways/`, incluidos los `P` de plataforma), no el cálculo.
Cuando publiquéis cifras, decid sobre qué conjunto salen — con esto me pasó lo mismo que con el umbral
del §1: la mitad del tiempo se va en decidir si lo que no cuadra es el número o la referencia.

Y el número que **no teníamos y que decide el trabajo pendiente: 567 pares de nodos con id distinto a
menos de 45 m** en SKBO. Eso es lo que la fusión por proximidad **está inventando hoy** —no estaba
arreglando huecos, estaba uniendo nodos que el escenario dice que son distintos—. Consecuencia
directa: **pasar a `node_id` no es «quitar un umbral», es cambiar el grafo en cientos de sitios**, y
puede desconectar rutas que hoy funcionan. Así que lo haré como pedís y como prometimos: **midiendo el
caso `G74 → A3` y el banco de pruebas del rodaje real antes y después**, y os mando el resultado. Si
algo se rompe, lo veréis con cifras, no con un «ya está».

**Sobre publicar los empalmes como conocimiento curado: sí, y es la respuesta coherente.** La
alternativa —subir el umbral de proximidad para tapar los huecos de 76 m— es exactamente la heurística
arbitraria que estamos quitando; cambiarla por otra más gorda sería empeorar. Lo que sí os pediría es
que el empalme curado lleve **los dos nodos y la calle que lo pide** (`K2`→`K1`), no solo un punto
medio: para el grafo hace falta la arista, no la coordenada.

**Y una consecuencia de la Adenda 1 que os afecta a vosotros también**: si un avión entra por `K2` y
`K2` **no toca** el nodo del punto de espera, entonces ese punto **no se puede nombrar por la calle por
la que llega el avión** —nuestro aviso diría `K1`, y el piloto viene por `K2`—. Es un caso pequeño
(hoy el aviso solo sale a menos de 40 m, y por `K2` la parada está a 120–145 m) pero es real, y me
apunta a un arreglo de mi lado: **nombrar la calle solo cuando es una de las del nodo**, y callarla
cuando no, en vez de decir la de al lado. Lo tengo anotado para el mismo cambio del grafo.

---

## 5. Lo siguiente por nuestro lado

1. **Grafo de rodaje exacto con `node_id`** (borrar el umbral de 45 m). Es lo siguiente que hacemos, y
   reportaremos si cambia la ruta sugerida del caso `G74 → A3`. Si en el camino vemos que el grafo
   necesita el emparejamiento de extremos que estáis validando, os lo decimos: sería la señal de que
   los cruces también nos hacen falta como dato y no como cálculo.
2. **La base de conocimiento**: el envío de observaciones reales entra cuando implementemos el
   rodaje con lectura; con vuestros límites (200 por POST, 60 POST/min, 5.000/día) y un POST por
   rodaje de salida no hay nada que ajustar por nuestra parte.
3. **`/holdshort/`**: seguimos con el inventario de 26 puntos fijado en un test. Si vuelve a cambiar
   la fuente, salta solo.

Nada pendiente de vuestro lado nos bloquea, y `crossings` puede llegar cuando esté bien.
