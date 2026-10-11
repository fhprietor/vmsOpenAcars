# Respuesta (3) al equipo de phpVMS — el tercer positivo, la lista y una corrección

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra respuesta (3): rellenos verificados y el origen de `pireps.landing_rate`
> **Estado:** el tercer positivo **reenviado**, la lista de los 10 **va abajo con una advertencia
> importante**, y vuestra explicación del campo nos ha llevado a **un fallo nuestro que no
> conocíamos**. Los 43 y 44 se quedan como relleno, como recomendáis.

---

## 1. El tercer positivo: teníais razón, y se me escapó por un motivo que se puede contar

**Reenviado** `9E20We81wlBgp3Nj` con la tasa normalizada a **−74**, leído de vuelta para confirmarlo:

```text
POST /api/pireps/9E20We81wlBgp3Nj/landing  →  200 · count 0
GET  →  source_name = vmsOpenACars/0.9.37-relleno · landing_rate_fpm = −74
```

Y he repasado **los 32** que hemos subido: **ya no queda ningún positivo en vuestra tabla.**

**Por qué se me escapó, dicho sin adornos:** en el relleno yo solo listaba las tasas que
**diferían** de la vuestra, para buscar el origen de la discrepancia. La del vuelo 9247 **coincidía**
con la vuestra —`+74` en los dos sitios—, así que no apareció en esa lista, y reenvié solo las dos
que sí divergían. Lo peor es que **yo había medido tres** positivas en el log local y no las cruce
con las que había reenviado. La lección es concreta: **una comprobación que solo mira las diferencias
no ve lo que está mal en los dos lados.** Lo aplico también al METAR de la ronda pasada, que fue el
mismo error de forma: tenía el dato delante.

## 2. La lista de los 10 vuelos, con una advertencia que importa

Antes de dárosla: **mi lista de PIREPs está incompleta, y lo sé por vosotros.** Vuestro
`gjyvJa7qekg21yLB` es del **1 de octubre**, o sea que debería estar en una consulta de «los más
recientes»… y **no venía**. Lo comprobé: `GET /api/user/pireps` me devuelve **60** (con `limit=60`;
`limit=120` y `limit=200` dan **503**), **ignora `offset` y `page`** (`?page=2` → 503, `?offset=60` →
los mismos 60), y esa ventana **no incluye** ese PIREP del 1 de octubre aunque sí incluya siete de
octubre del 4 al 9. Conclusión: **la ventana de 60 no es «los 60 más recientes»** y se deja PIREPs
fuera. **Tratad la lista como candidatos, no como un hecho**: son los vuelos de nuestro log local
para los que no encontré PIREP en esa ventana.

| # | Vuelo | Fecha (UTC, fileo) | Ruta |
|---|---|---|---|
| 1 | *(sin número)* | 2026-05-04 12:03 | — |
| 2 | *(sin número)* | 2026-05-06 01:52 | — |
| 3 | *(sin número)* | 2026-05-06 03:24 | — |
| 4 | 875 | 2026-05-16 14:11 | SKSM → MPTO |
| 5 | 62 | 2026-05-19 02:41 | SKCL → KJFK |
| 6 | 8422 | 2026-05-20 11:27 | SKCL → SKBO |
| 7 | 8180 | 2026-05-27 00:04 | SKPE → SKRG |
| 8 | 55CH | 2026-06-02 01:05 | SKPS → SKSM |
| 9 | 7743 | 2026-06-21 19:10 | SKCG → SKVP |
| 10 | 208 | 2026-08-26 02:44 | SKRG → TNCC |

Los tres primeros **no tienen ni número de vuelo ni ruta** en el log local (son de los primeros días
de mayo, cuando el cliente aún no lo escribía), así que por ahí no hay nada que buscar. Y ojo con
uno: el `SKCG → KBOS` del 22-23/09 que ya sabíamos que no aparece **no está en esta lista** porque
tampoco está en el log local de este equipo.

**Y `gjyvJa7qekg21yLB` no lo podemos rellenar: no tenemos su captura.** Es un 4865 del **1 de
octubre** y nuestros dos vuelos 4865 son del 2 y el 16 de julio. Ese vuelo no está en el log local
que tenemos aquí, así que no hay nada que subir por nuestra parte.

## 3. Vuestra explicación del campo nos corrige a nosotros, no a vosotros

Lo que contáis de `pireps.landing_rate` —que es **nuestro propio campo del `file`**, guardado tal
cual— nos hizo revisar el camino, y **vuestra lectura de las dos filas es la correcta pero el motivo
no**: la fila local **no** sale de releer el log. Sale **en vivo, al filear**, y ese es justo el
problema.

En nuestro cliente, el orden es:

```text
SnapshotLandingRecord()   ← aquí se COPIA la tasa (y el resto del toque)   … ANTES del await
await FilePirep(...)      ← aquí se construye el payload con la tasa       … DESPUÉS del await
SaveLandingRecord(...)    ← y aquí se guarda en el log local la copia de arriba
```

O sea que las dos tasas que veis —la de `pireps.landing_rate` y la de `pirep_landings`— son **dos
lecturas en vivo del mismo toque, separadas por ese `await`** (dentro del cual la telemetría sigue
corriendo a 20 Hz). **La de la copia es la vieja, y en esos vuelos es la mala**: las dos filas
afectadas tienen `touchdown_dist_ft = 0` y sin captura de flare, que es la firma de una copia hecha
**antes de que el toque estuviera resuelto**. La relectura del log no inventa nada: reproduce
fielmente lo que la copia dejó escrito.

**Qué significa para vosotros:** nada que arreglar en vuestro lado; el vivo manda y por eso
`pireps.landing_rate` es el bueno. Ya estáis viendo la magnitud correcta.

**Qué significa para nosotros:** un fallo real que no conocíamos y que sí muerde: **nuestro log
local puede guardar un toque distinto del que se fileó**, así que el logbook del piloto puede
discrepar del PIREP en la tasa. En un caso eso hace que la traza del flare y el análisis se
construyan sobre una copia incompleta. Queda anotado como abierto y con esto ya sabemos dónde
mirar: la copia se hace antes del `await` por un motivo que sigue siendo válido —el fileo resetea el
estado del vuelo—, así que el arreglo no es moverla, es que **el toque se congele en el momento del
contacto** en vez de leerse al filear. Gracias: no lo habríamos encontrado sin vuestra explicación.

## 4. Los 43 y 44 se quedan como relleno

**Confirmado, y nos quedamos con vuestro razonamiento**: se recuperaron releyendo el log local, que
es exactamente el camino donde apareció el fallo del `ts_utc`. Marcarlos como «vivo» sería mentir
sobre su procedencia, y no cambia ninguna agregación. No los tocamos.

## 5. Y sobre la corrección que hicisteis: `-ABS()` y no `ABS()`

Está bien pensado y queremos decirlo: **`-ABS()` es lo correcto** y `ABS()` a secas habría sido peor
que el fallo original —una media de tasas de descenso publicada en positivo se leería como un
ascenso, y el modelo la habría usado como tal—. Que además comprobarais que es un no-op sobre los
544 vuelos del piloto (`−301` antes y `−301` después) es la parte que hace que uno se fíe.

Y el fallo de la división entera que salió al escribir la prueba es exactamente el tipo de cosa que
solo aparece cuando alguien pregunta. Nos alegramos de que la pregunta saliera de aquí.

---

## 6. Lo que nos queda

1. **La 0.9.37 sin publicar**: os avisamos el día que salga (sigue en pie).
2. **La lista de los 10**, con la advertencia del punto 2: miradla en vuestro lado, que es donde
   está la información buena.
3. **El fallo de la copia del toque**: es nuestro, está anotado y no bloquea nada de lo vuestro.
