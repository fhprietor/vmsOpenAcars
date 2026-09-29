# vmsOpenAcars → NavData: rutas de rodaje — respuesta y verificación

> **Para:** equipo de NavData · **De:** equipo de vmsOpenAcars · **Fecha:** 2026-09-29
> **Responde a:** `respuesta-a-vmsOpenAcars-rutas-taxi-2026-09-29.md`
> **Estado del cliente:** v0.9.16 (el trabajo de esta respuesta entra en la siguiente publicación)

Gracias por el detalle. Hemos **reproducido en vivo** vuestros tres puntos antes de contestar, y los
tres se confirman. Lo que sigue son las verificaciones, la respuesta a vuestra pregunta de §4.1, lo
que ya hemos cambiado en el cliente y **tres cosas que necesitamos de vosotros**.

---

## 1. Verificado contra vuestro servicio (2026-09-29)

| Vuestra afirmación | Medición nuestra | |
|---|---|---|
| `/holdshort/` publica `taxiway` y `taxiways` | presentes en la respuesta | ✅ |
| `/taxiways/` publica `start_node_id` y `end_node_id` | presentes; **569 segmentos / 515 nodos únicos** en SKBO, como vuestra tabla | ✅ |
| El punto que llamábamos «A3» es un cruce de cuatro calles | a menos de 2 m de `4.712803,-74.152382` tocan segmentos de **`A1`, `A2`, `A3` y `E`** | ✅ |

**Y una corrección que es nuestra.** Nuestro «46 segmentos por debajo del radio» estaba medido en
**pies** (< 50 ft) mientras que el umbral del código son **45 m**: son dos umbrales distintos, no un
error de cálculo, pero presentado como estaba **infravaloraba el problema**. Vuestro número es el que
importa y lo damos por bueno: **318 de 569 segmentos (56%) por debajo de 45 m**, mediana 39,8 m,
mínimo 2,1 m. Es decir: la fusión por proximidad no afectaba a una minoría del grafo, sino a más de
la mitad. Con `node_id` desaparece, y estamos en ello (§4).

---

## 2. Vuestra pregunta de §4.1: **sí, queremos el arreglo, y nos corre prisa**

Hemos medido lo que cuesta la sobre-generación. Los 14 puntos de la 14L, por **distancia
perpendicular al eje de la pista**:

| Distancia al eje | Puntos | Calles |
|---|---|---|
| 40 m | 1 | `E` |
| 74 m | 1 | cruce `A1/A2/A3/E` |
| 139 – 254 m | **12** | `A`, `A1`, `A2`, `L` |

**Solo 2 de los 14 son accesos reales**; los otros 12 son nodos de las paralelas. Y ahí está el
problema operativo, que no es teórico: nuestro RAAS avisa del punto de espera **más cercano dentro de
200 m que el avión tenga por delante**. Rodando *sobre* la paralela `A` —hacia la 14R, que está a
1,5 km— esos nodos están a metros y **en el cono de rumbo**, así que el piloto recibía «espera antes
de pista **14L**» yendo a la 14R. Lo teníamos como defecto sospechado; ahora está medido y fijado en
un test.

- **Pedimos vuestra versión** («solo los nodos cuyo segmento alcanza el pavimento → un punto por
  acceso»). Contad con que la queremos **en cuanto la tengáis**, aunque no bloquee nada.
- **No esperamos sentados**: hemos añadido en el cliente el **filtro por pista de destino**, que
  resuelve el caso cruzado (ir a la 14R y oír 14L). Lo que **no** puede resolver nuestro filtro es el
  caso de la *misma* pista —yendo a la 14L, los nodos de la paralela siguen ahí—: eso es vuestro
  arreglo y por eso lo pedimos.
- Avisadnos cuando despleguéis: es **cambio de comportamiento** en un dato que ya consumimos y
  queremos re-correr nuestro banco de pruebas (el rodaje real completo) antes de publicar.

---

## 3. Sobre `taxiway` (sugerida) y `taxiways` (lista): confirmamos vuestro consejo, con un matiz

En el cruce de la 14L vuestra sugerida sale **`A2`** y el piloto dice **`A3`**. Tenéis razón en que
los cuatro nombres son ciertos y en que la lista es lo que resuelve la ambigüedad: nosotros **no
usamos `taxiway` como nombre del punto de espera**, y creemos que no deberíais usarlo tampoco como
`entry` en la base de conocimiento (**la sugerida se elige por ser la más perpendicular, no por ser
la que dice ATC**).

Lo que hacemos ahora: para el aviso, el nombre es **la calle por la que llega el avión**, y solo si
esa calle está en `taxiways`; si no, se usa vuestra sugerida; y si no hay lista, el muestreo
geométrico de siempre. Así, llegando por `A3`, el aviso dice `A3`; y si el avión va por una calle que
no toca ese nodo, no se le nombra un vecino. Igual para el `entry` de la observación: mandamos **la
calle de nuestra ruta** que esté en `taxiways`, más las coordenadas exactas.

---

## 4. Lo que ya hemos cambiado en el cliente (entra en la siguiente publicación)

- **`HoldShortSelector`** (`Helpers/HoldShortSelector.cs`): la decisión de qué punto de espera está
  delante, **pura y con seis tests** que usan los **14 puntos reales de la 14L y los 5 de la 14R**,
  sin geometría inventada. Incluye el test del defecto (sin filtro gana el nodo de la 14L yendo a la
  14R) y el del arreglo (con filtro, no se avisa de otra pista).
- **Filtro por pista de destino** en los dos sitios que consultan puntos de espera (el aviso del RAAS
  y la línea del log), alimentado por la pista que el piloto declara en el diálogo de rodaje; si no
  hay pista declarada, **no se filtra** (se degrada al comportamiento anterior: preferimos callar el
  filtro antes que silenciar un aviso legítimo).
- **Nombre desde `taxiways`**, con la regla de §3.
- **Pendiente nuestro, ya en marcha**: `node_id` en el grafo de rodaje **para borrar el umbral de
  45 m**. `NavTaxiway` ya lo mapea; falta que `TaxiGraph` construya los nodos por id (con la fusión
  por proximidad como respaldo cuando el dato no venga, para no romper con cachés viejas).

---

## 5. Vuestras respuestas: de acuerdo, con dos notas

- **P1 (modelo)**: aceptamos las tres precisiones. `stand` como objeto; `entry` con calle y
  coordenadas; y sobre todo: **que no exijáis que la ruta sea un camino conexo en vuestro grafo** nos
  parece la decisión correcta — es exactamente el caso `B5`/`X`, donde el conocimiento operativo usa
  enlaces que el grafo no ve.
- **P2 (límites)**: nos cuadran. Un POST al terminar el rodaje de salida, otro al filear si falló
  antes; sin reintentos en bucle; `429` + `Retry-After` respetado.
- **P3 (umbrales)**: de acuerdo, incluido el **peso doble a `typed`** y la ventana con decaimiento.
  Publicar `support`/`total`/`confidence`/`updated_at` siempre es lo que hace utilizable el dato.
- **P4 (moderación)**: sí, y nos parece mejor que la votación sola. Dos cosas que pediríamos a la
  ruta fijada: que pueda llevar **también el nombre del punto de espera** (`entry_taxiway`) y una
  **nota**; y que la respuesta distinga `source: "curated"` frente a `"community"` — ya lo dice, lo
  usaremos para escribir «ruta fijada» en pantalla en vez de «7 de 9».
- **P5 (orden)**: `crossings` cuando llegue; **la consumiremos** — es el aviso que hoy no tenemos
  (cruzarse con una paralela por una calle sin hold-short no dispara nada).
- **P6 (clase)**: la mandamos desde el primer día. Estamos de acuerdo en no partir la agregación por
  clase al principio: con pocos datos no se publicaría nada.

---

## 6. Tres cosas que necesitamos de vosotros

1. **Confirmad el tipo de `node_id`.** Los valores que vemos **no caben en un entero con signo de 64
   bits** (p. ej. `15748507198978893803` > 9.223.372.036.854.775.807). Los estamos tratando como
   **`ulong` (64 bits sin signo)**; si en algún caso pudieran venir como cadena o superar ese rango,
   decidlo, porque un cliente que los leyera como `long` reventaría. Y confirmad que **dos extremos
   con el mismo id son el mismo nodo** también cuando el id procede de coordenadas distintas en el
   séptimo decimal: vuestra tabla dice que coinciden a 6–7 decimales, así que lo damos por seguro,
   pero es la base de borrar nuestro umbral.
2. **Fecha del arreglo de hold-shorts** (§2) y, si os cabe, **empezad por los aeropuertos con más
   observaciones**: es donde el RAAS avisa más.
3. **Disponibilidad del endpoint de la base de conocimiento** para planificar el envío desde el
   cliente. Con los límites de P2 y `observed_at` aceptando 90 días, no nos corre prisa *enviar*;
   nos interesa saber *cuándo* podremos **leer** `taxi-routes` y `taxiway-stats` para decidir si la
   propuesta de ruta pasa a venir de la comunidad o sigue viniendo del grafo.

---

## 7. Recordatorio del orden que proponemos nosotros

Nada de esto bloquea que empecéis: con `node_id` podemos montar el grafo exacto y **ya podemos enviar
observaciones**. El orden que nos encaja, de mayor a menor impacto en el cliente:

1. **Base de conocimiento** (`taxi-routes` + `taxiway-stats`): es lo que convierte «la ruta más corta»
   en «la ruta que se hace».
2. **Hold-shorts de acceso** (§2): quita avisos falsos de un sitio donde el piloto ya mira.
3. **`crossings`**: capacidad nueva.

---

### Anexo — cómo lo hemos medido

- `/airport/SKBO/holdshort/`, `/taxiways/`, `/runways/` en vivo el 29/09/2026.
- Distancia perpendicular de cada punto de espera al eje de la pista, proyectando sobre el segmento
  umbral→extremo de la 14L (`4.713392,-74.152061` → `4.692726,-74.124752`).
- Los 14 puntos de la 14L y los 5 de la 14R están **copiados literalmente** en
  `vmsOpenAcars.Tests/HoldShortSelectorTests.cs`; los tests no salen a la red.
- El caso `G74 → A3` sigue en `vmsOpenAcars.Tests/TaxiRouteCaseTests.cs` con sus 237 segmentos.
