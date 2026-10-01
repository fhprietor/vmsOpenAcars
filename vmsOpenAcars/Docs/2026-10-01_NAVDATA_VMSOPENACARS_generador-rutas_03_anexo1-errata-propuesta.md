# NavData → vmsOpenACars: errata de la propuesta del generador de rutas

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Errata de:** `propuesta-a-vmsOpenAcars-generador-rutas-taxi-2026-10-01.md`
> **SHA-256 de ese documento, tal como os lo enviamos:**
> `e005abce691468b4ae658298234b3fe191e667c662807f97613932f89c2a5bb5`
> **Nota de capitalización:** vuestra respuesta lo cita como `…vmsOpenACars…` (**C** mayúscula); en
> nuestro repo está como `…vmsOpenAcars…` (**c** minúscula). Es el mismo documento — el `sha256` de
> arriba es el del **contenido** —, pero lo decimos para que nadie busque dos ficheros distintos.
> **Estado:** el documento original **no se toca** (es el que tenéis y así se puede comparar).
> Todas las correcciones van aquí. Es el anexo 1 de la respuesta.

Este fichero existe por una razón práctica: corregir el documento original lo convertiría en otro
documento distinto del que os llegó, y la conversación dejaría de ser verificable. Si vuestro
`sha256` del original no coincide con el de arriba, decidlo y lo alineamos antes de seguir.

---

## 1. Las tres incoherencias que señalasteis: las tres eran ciertas

### 1.1 §3.2 contra el JSON — **error nuestro**

Teníais razón: la tabla listaba cuatro giros, el texto decía «6 giros > 25°» y el JSON le ponía 45°
a la pata `A`. Las tres cosas no podían ser ciertas a la vez, y el «45°» era directamente
**inventado al redactar el ejemplo** — el tipo de dato que un generador no debe fabricar.

### 1.2 `joins_used` vacío — **no era un bug; faltaba explicarlo**

El campo está bien y **sí** se puebla. En ese ejemplo sale `[]` porque **la ruta no usa ningún
empalme**: `G74 → hold-short 14L` son **25 aristas y 0 empalmes**, todo por nodos compartidos.
Donde la red está partida sí lleva aristas, con su `gap_m` y su `confidence`: en LMML la misma ruta
usa **2** (`J`, 23,2 m, conf 0,96 e `I|F`, 188,3 m, conf 0,56).

### 1.3 Las citas de §2.3 y §4.3 — **aclarado**

- **«§4.3»** era el §4.3 de **vuestro** `PEDIDO-NAVDATA-CONECTIVIDAD-2026-09-30.md` (la ruta escrita
  como prueba de aceptación). Citarlo sin decir de qué documento era un defecto nuestro.
- **«vuestra observación»**: la ruta `F E M A A3` con hold-short en (4.712803,-74.152382) salió del
  ejemplo que quedó fijado en nuestro `aviso-vmsOpenAcars-rutas-taxi-disponible-2026-09-29.md`, donde
  **nosotros** la llamamos «vuestro ejemplo». Con vuestra base a **0 observaciones** (el campo solo
  se guarda desde 0.9.18), tenéis razón: no es una observación vuestra y no debimos etiquetarla así.
- **Y una culpa mayor, que no mencionasteis**: el JSON de ejemplo de aquel aviso (`support: 7`,
  `total: 9`, `confidence: 0.78`) fue **ilustrativo del contrato, no una medición**. Presentado como
  ejemplo sin decirlo, inducía a leer cifras que no existían. Nuestra culpa. A partir de ahora todo
  número que publiquemos irá medido, o marcado como `"ejemplo_ilustrativo": true`.

---

## 2. Y una cuarta incoherencia, que apareció al **implementar** el generador

Las cifras de la propuesta salían de un prototipo con búsqueda **aproximada** (Dijkstra por nodo,
sin recordar el rumbo de llegada). El generador implementado busca sobre el estado
`(nodo, nodo previo)` y con el perfil por defecto (`k = 0,5`) encuentra una ruta **distinta y más
barata**. Las cifras buenas son las del código que se sirve:

| | Ruta | Longitud | Giros > 25° |
|---|---|---|---|
| Propuesta (prototipo aproximado) | `E M S A A3` | 2.256,6 m | 6 |
| **Implementado (buscador exacto)** | **`E N A S A A3`** | **2.269,0 m** | **3** |

Desglose real del perfil por defecto:

| Pata | Longitud | Giro |
|---|---|---|
| `E` | 419,0 m | ninguno (es la salida del puesto) |
| `N` | 480,9 m | **90,0°** a la derecha |
| `A` | 93,4 m | **90,4°** a la izquierda |
| `S` | 362,5 m | recto (0,3°) |
| `A` | 697,4 m | recto (0,1°) |
| `A3` | 215,8 m | **31,8°** a la derecha |

**Total: 3 giros > 25°, y ninguno más por encima de 10°.** El JSON del original ya no aplica: la
pata `A` va recta, y el cierre al umbral son **74,5 m** (umbral AIRAC; 66,3 m con el marco del
escenario). La corrección de §3.1 también matiza el ejemplo: con `k = 1,0` sale **la misma ruta**
que con `0,5`, así que aquel ejemplo no distinguía dos perfiles.

Dos consecuencias, y las dos os dan la razón:

1. **Un dato publicado tiene que salir del código que se sirve, no de un prototipo.** Estas cifras
   están ahora fijadas en tests (`apps/navdata/tests/test_taxi_route.py`): si el escenario cambia y
   la ruta cambia, salta el test.
2. **Refuerza vuestra crítica de que `k ≈ 0,5` está sin calibrar**: al pasar de la búsqueda
   aproximada a la exacta, la ruta cambió. Calibrarlo con las 37 trazas es la fase 2.

---

## 3. Lo que **no** cambia de la propuesta

Para que no se lleve por delante lo que sí era sólido: el grafo (nuestros nodos y empalmes), que el
destino sea el **punto de espera** y no el umbral, la jerarquía `curated > customary > computed`,
`version` + `joins_used` para auditar, y publicar el tramo renombrado `S` como aviso en vez de
taparlo. Eso estaba medido y sigue en pie.
