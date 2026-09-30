# NavData → vmsOpenACars: la cobertura, medida — y un bug nuestro que LMML destapó

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** `PEDIDO-NAVDATA-CONECTIVIDAD-2026-09-30.md`
> **Estado:** §4.1, §4.2 y §4.4 **implementados y medidos**. §4.3 (la ruta escrita como prueba de
> aceptación) **pendiente y es lo siguiente**: os pedimos el corpus.

Teníais razón en el diagnóstico y el caso de LMML destapó **un bug nuestro**, no sólo falta de
cobertura.

---

## 1. El bug: sólo mirábamos 4 componentes, y LMML tiene 5

```
grupos[:4]  y  grupos[i+1:5]        # ← así estaba
```

Con cinco componentes (LMML: 418/340/86/48/35) eso dejaba **pares de componentes sin considerar
nunca**. Corregido: se evalúan **todos** los pares.

Y dos cosas más que salieron al mirarlo:

- **Los puentes usan el umbral que proponéis (200 m)**, no el derivado de la mediana del aeropuerto:
  unir componentes es el objetivo, y la confianza —que baja con la distancia— es la que os dice
  cuáles son dudosos. Con el umbral corto, LMML seguía partido.
- **Un cruce de pista ya no se rechaza: se publica con su bandera.** Es lo que nos enseñasteis vosotros
  con vuestro propio ejemplo: el único empalme de LMML es un cruce, y es correcto. Cruza una pista es
  una maniobra legítima, así que va publicado con `crosses_runway: true` y algo menos de confianza,
  y decidís vosotros. (Con esto **KMIA pasa de 0 empalmes a 1** y se conecta.)

## 2. §4.1 — `components` con los empalmes aplicados

`stats.components` **no podía bajar al publicar empalmes**: cuenta la red tal cual viene del
escenario. La métrica que vuestro §4.1 pide de verdad es la de después, así que se publica aparte:

| | componentes (escenario) | **con los empalmes** | empalmes publicados |
|---|---|---|---|
| **LMML** | 5 | **2** | 3 |
| SKBO | 3 | **1** | 15 |
| KMIA | 2 | **1** | 1 |
| CYUL | 2 | **1** | 1 |
| LEMD | 3 | **2** | 2 |
| LOWW / KBOS | 1 | 1 | 0 |

Es decir: **la red es recorrible tal cual se publica en 5 de los 7** que hemos medido. En LMML
queda **una** unión por resolver y en LEMD otra; esas dos os las debemos con nombre y distancia, y
para eso necesitamos lo del §4.3.

## 3. §4.2 y §4.4 — Nada sin contabilizar, y el número que se vigila

```json
"stats": { "components": 5, "components_after_joins": 2,
           "gaps_considered": 3, "gaps_published": 3, "gaps_rejected": 0,
           "gaps_unaccounted": 0, "joins_published": 3, "joins_curated": 0 },
"rejected": [ { "node_a": …, "node_b": …, "gap_m": 41.2, "turn_deg": 137.6,
                "reason": "turn_too_sharp", "confidence": 0.21 }, … ]
```

- **`rejected[]`**: todo hueco considerado y no publicado, con su motivo
  (`turn_too_sharp`, `low_confidence`, `already_connected`, `no_segment`). Es la diferencia que
  pedisteis: «no hay empalme» frente a «no hay empalme **y sabemos por qué**».
- **`gaps_unaccounted`**: huecos sin empalme **ni** explicación. Es 0 en los siete aeropuertos
  medidos, y es un número que se autovigila: si algún día no es 0, hay un camino del criterio que
  descarta un hueco sin dejar constancia.
- En LMML, con el criterio nuevo, **no queda ningún rechazo**: sus tres huecos útiles están
  publicados.

## 4. §4.3 — La prueba de aceptación: sí, y necesitamos vuestro corpus

Es la parte que **no** está hecha, y no quiero decir que lo esté. Vuestra propuesta es la correcta:
una ruta escrita por ATC tiene que ser **trazable de punta a punta** en lo publicado, y eso se
automatiza. Mandadnos las rutas escritas reales (las del vuelo de LMML y las que acumule el corpus)
y las metemos como casos: cada una se comprueba contra la red publicada, sin heurística del cliente.

Es, además, exactamente lo que la base de conocimiento acumula: esas rutas escritas son el dato que
ya estáis enviando como observaciones, así que la prueba sale del mismo sitio.

## 5. Lo que queda de nuestro lado

1. **La prueba de aceptación con vuestras rutas escritas** (en cuanto llegue el corpus).
2. **Las dos uniones que faltan** (LMML, LEMD) con nombre y distancia, para llegar a
   `components_after_joins: 1` o justificar por qué no.
3. **`crossings`** como endpoint propio con `has_hold_short` — el caso que de verdad os falta, como
   dijisteis. Lo de hoy lo deja a medio camino a propósito: publica el cruce como arista con su
   bandera, que ya desbloquea la conectividad, pero todavía no dice si ese cruce tiene punto de
   espera.

## 6. Y sobre vuestro lado

Que apaguéis la fusión por proximidad cuando esto esté, y que **una ruta escrita no trazable se diga
en vez de sustituirse en silencio**, nos parece lo correcto. El «15 avisos nombrando calles que el
piloto no usó» es el síntoma que hace visible el hueco, y el arreglo del ruido es vuestro; el dato
que lo evita es nuestro, y hoy es más completo que ayer: **5 de 7 aeropuertos conectados tal cual se
publican**.
