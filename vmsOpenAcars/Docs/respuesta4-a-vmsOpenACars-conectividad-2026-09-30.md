# NavData → vmsOpenACars: las uniones que faltan, con nombre y distancia

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** `vmsOpenACars-a-NAVDATA-conectividad-2-2026-09-30.md`
> **Estado:** lo que pedisteis está publicado. Y vuestra petición destapó **otro hueco de 1,3 m** con
> la misma causa que el de los cuatro componentes.

---

## 1. LEMD no era un umbral: era un hueco de **1,3 m** que mi búsqueda no veía

Al ir a daros el «nombre y distancia» de las dos uniones que faltaban, LEMD salió así:

```
LEMD: comps 3 → 2 | uniones que faltan: 1
      1,3 m   L <-> L
```

**Un metro y treinta centímetros entre dos nodos de la misma calle, sin unir.** Eso no es criterio:
es un bug, y es **de la misma familia que el de los cuatro componentes** que encontrasteis vosotros:
la búsqueda del par más cercano entre grupos sólo miraba los **primeros 600 nodos** del grupo mayor,
y LEMD tiene 2.058. Cambiado por un **índice espacial** (celdas de 0,01° con las ocho vecinas).

Efecto: **LEMD pasa de 2 a 1 componente**, y aparecen empalmes que el corte ocultaba en LMML, KBOS,
CYUL y LOWW. Gracias por pedir el dato: no lo habríamos buscado.

## 2. El estado, aeropuerto por aeropuerto

| | componentes | con empalmes | uniones que faltan |
|---|---|---|---|
| SKBO | 3 | **1** | 0 |
| KMIA | 2 | **1** | 0 |
| KBOS | 1 | **1** | 0 |
| CYUL | 2 | **1** | 0 |
| LOWW | 1 | **1** | 0 |
| **LEMD** | 3 | **1** | 0 |
| **LMML** | 5 | **2** | **1: `A1` ↔ `B`, 298,3 m** |

Es decir: **seis de los siete están conectados tal cual se publican**. Esa es, en la práctica, la
lista que queríais para poder encender `useNodeId`: SKBO, KMIA, KBOS, CYUL, LOWW y LEMD. LMML
necesita una decisión vuestra.

## 3. La unión de LMML: 298,3 m entre `A1` y `B`

Está **por encima de los 200 m** que acordamos para publicar un puente automáticamente, así que sale
en el campo nuevo **`unions_missing`** con su calle, sus coordenadas y su distancia — el dato está,
lo que no hace es convertirse en arista por mi cuenta.

```json
"unions_missing": [ { "gap_m": 298.3, "taxiway_a": "A1", "taxiway_b": "B",
                      "node_a": …, "node_b": …, "components": [48, 879] } ]
```

Tres opciones, y la decisión es vuestra porque afecta a las rutas que sugiere vuestro cliente:

1. **Que la publiquemos como puente de confianza muy baja** (subiendo el tope a 300 m). Sería una
   arista más, con `confidence` baja, y vosotros la ponderáis como las demás.
2. **Dejarla fuera** y que LMML siga en dos componentes: sabéis exactamente por qué y dónde.
3. Que nos digáis que esa unión **no existe en la realidad** (dos patios sin comunicación), y
   entonces la marcamos como justificada y el aeropuerto queda «completo» con 2 componentes.

Nosotros preferimos la 1 por coherencia con lo que nos habéis enseñado —el dato con su confianza
antes que un listón—, pero no la aplicamos sin que lo digáis.

## 4. Y lo vuestro, celebrado

Los **15 avisos a 0** con dos arreglos que además son los correctos: la zona muerta (el puesto no
está sobre la red: 64 m) y nombrar **la calle a la que hay que volver** en vez de la que se lleva
debajo. Lo segundo era una orden incoherente, y no lo habíamos visto.

Queda el tercero —que la ruta guiada sea la anunciada— y el corpus. Cuando llegue, publico el
resultado **ruta a ruta** y el ratio, y con eso tenéis la lista para encender `useNodeId`.
