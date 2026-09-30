# vmsOpenACars → NavData — los tres descuadres, el hallazgo que confirma el criterio y el corpus

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `RESPUESTA-NAVDATA-CONECTIVIDAD-2026-09-30.md`
> **Estado:** §4.1, §4.2 y §4.4 **verificados desde fuera**; vuestro bug corregido y la unión que
> necesitaba el vuelo **ya está publicada**. Vamos con los tres descuadres y el corpus.

---

## 1. Verificado, y el hallazgo que cierra el caso de LMML

| | escenario | con empalmes | publicados |
|---|---|---|---|
| **LMML** | 5 | **2** | **3** |
| KMIA | 2 | **1** | 1 (`L1|K1`, 2,8 m, 179,6°, `crosses_runway`) |
| LEMD | 3 | 2 | 2 |

`gaps_unaccounted = 0` en los tres ✓, y **KMIA de 0 a 1** es el muñón de 2,8 m que teníamos
documentado: la plataforma se conecta.

Y esto es lo importante, medido desde fuera:

```
via='I|F'   gap=188,3 m   turn=3,3°   conf=0,42   kind=component_bridge
```

**F está en la comp 2 y I en la comp 1.** Ésa es **exactamente la unión que el piloto necesitaba** para
que su `T J K L` fuera trazable — la que no existía cuando voló — y existe por las dos cosas que
salieron de esta conversación: **el umbral de 200 m** que propusimos (188,3 m quedaba fuera del derivado
de la mediana) y **vuestro bug de los cuatro grupos**. Con ésa y con `J` (comp 1↔4), la ruta escrita se
traza hoy entera: `T`(2) → `F|I`(2→1) → `J`(1) → `K`(4).

Un detalle que nos gusta: **conf 0,42** está por debajo de nuestro suelo de 0,5, así que cae en el
**segundo nivel** de nuestro umbral y el grafo la usa **sólo si sin ella no hay ruta**. Un puente dudoso
publicado no ensucia una ruta que ya se puede hacer bien: es justo el reparto que queríamos.

## 2. Los tres descuadres (medidos ahora mismo, no son hipótesis)

1. **`gaps_published` no viene en la respuesta.** Vuestro ejemplo lo promete, pero en la real sale
   vacío: LMML `considered=3, published=<vacío>, rejected=0`; KMIA `13 / <vacío> / 24`.
2. **Los contadores no cuadran entre sí**: KMIA dice **13 huecos considerados** y su `rejected[]` lista
   **24**; LEMD, **7** contra **10**. O `considered` cuenta otra cosa, o `rejected[]` incluye más de lo
   que se considera — y como `gaps_unaccounted` es el número que queremos vigilar, conviene que la
   aritmética cierre.
3. **`crosses_runway` aparece como MOTIVO DE RECHAZO** en `rejected[]` (KMIA 45 m/giro 1° y 50 m/giro
   22°; LEMD 54 m/giro 34°), mientras en el mensaje decís que un cruce «ya no se rechaza: se publica
   con su bandera». Una de las dos no es cierta, y querríamos saber cuál para no leer mal vuestro dato.

Y un apunte menor de LMML: `G|D` se publica como puente con **giro 162,9°** y `crosses_runway: true`,
mientras rechazáis `turn_too_sharp` a 180° con 2–4 m. Entendemos la regla —un puente no se descarta por
el giro—, pero un «puente» de 162,9° puede ser dos nodos enfrentados a los lados de una franja. Con
0,38 sólo entra en nuestro segundo nivel, así que el riesgo está acotado; lo decimos para que lo miréis
con vuestros ojos, no porque nos bloquee.

## 3. §4.3 — El corpus de rutas escritas

Es lo correcto y os lo mandamos. Formato que proponemos, un caso por vuelo:

```
icao,runway,ruta_escrita,lat,lon
LMML,05,"T J K L",35.85055,14.48869
```

con la **posición inicial** —porque el caso de LMML no se entiende sin ella: el avión estaba a **64 m de
F** y la primera calle escrita (`T`) a **262 m**—, y con la pista declarada. De nuestro corpus salen las
rutas que el piloto tecleó de verdad en el popup, que son las mismas observaciones que ya enviamos a la
base de conocimiento: la prueba de aceptación y la base se alimentan del mismo dato.

## 4. Nuestro lado, ya en marcha

El ruido es nuestro y lo estamos tocando con vuestros números delante: **la ruta escrita ya es trazable
en 5 de 7 aeropuertos**, así que tiene sentido apagar la fusión por proximidad — y antes, tres cosas
que son independientes del dato:

1. **Nunca guiar una ruta distinta de la anunciada.** Si la escrita no es trazable, se dice.
2. **Enlazar hasta la primera calle escrita** (el tramo `puesto → T`) en vez de sustituir la ruta entera.
3. **Zona muerta de `FUERA DE RUTA`** hasta que el avión haya estado en ruta al menos una vez: medido,
   el puesto estaba a 64 m de la red, así que el aviso disparaba desde el primer sondeo y se re-armaba
   cada 20 s. Los 15 avisos de LMML salen enteros de ahí.

Os avisamos cuando esté, y os pasamos el test de replay de ese vuelo como caso del corpus.