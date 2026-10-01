# NavData → vmsOpenACars: el respaldo del «0 de 33» — y solo 3 rutas usan empalmes

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_13_erratas-nuestras-y-respuestas.md`
> **Adjunto:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_14_empalmes-y-metricas-por-ruta.csv`
> **Estado:** la única cosa nueva que pedisteis, entregada y **verificable**. Y una corrección vuestra
> que acepto: mi limitación declarada estaba mal en un eje.

---

## 1. §3 — el respaldo del «0 de las 33», con nombres

Teníais razón en que un recuento no se comprueba. El CSV adjunto trae, por ruta:
`joins_used_names`, `joins_used_detail` (nombre, `gap_m`, `confidence`, `kind`, `crosses_runway`),
`crosses_runway_joins` y `crosses_runway_steps`.

Y el respaldo es más corto de lo que yo mismo insinué: **solo 3 de las 33 rutas usan algún empalme**,
y estos son, con nombre:

| Ruta | Empalmes usados | ¿Cruzan pista? |
|---|---|---|
| `SKPE/08` | `A1` | no |
| `MMGL/11R` | `B\|A` | no |
| `LMML/05` | `I\|F`, `J` | no |
| las otras 30 | ninguno | — |

**Ninguna de las 33 usa `A1|B` ni `G|D`.** Así que el «0» no es una afirmación sin respaldo: es que
las dos únicas aristas que cruzan pista **no aparecen en ninguna ruta de esta tanda**. Lo podéis
comprobar ruta a ruta en el CSV, que es como debe estar.

---

## 2. §2 — vuestra corrección, aceptada (y una población más que es nuestra)

**Mi «mido por segmento, no por nodo» era falsa en un eje**: vuestro `NearestTaxiway` también
resuelve por segmento. El *proxy* que queda es el **rumbo**, que derivo del track entre muestras a
30 s. Queda dicho así, sin la limitación inventada.

Y adopto vuestra tabla de poblaciones, porque es más precisa que la mía: **el cero no desaparece, se
desplaza**. Con una fila que añado y que es responsabilidad **nuestra**, no de los datos:

| Universo | Cero estructural |
|---|---|
| 37/38 sin ruta escrita (cliente real: el plan lo confirma el piloto) | **intacto** |
| **5/38 sin ruta generada** | **nuevo, declarado — y es defecto nuestro** |
| 33/33 con ruta generada | 0 por el cerrojo (33 de 33) |

Esos **5** son 4 `no_route_in_published_network` (`SKLT/21`, `SKYP/05`, `SKCG/01` ×2) y 1
`unknown_runway` (el caso mal cortado, vuestro 1.764 s). Los cuatro primeros son **nuestra red**: no
hay camino publicado entre ese origen y esa pista, y eso es un defecto que hay que mirar, no un
«sin datos». Van a la lista de trabajo, al lado de los tres de ruta distinta (`SKPE/08`, `KBOS/09`,
`SEGU/21`).

---

## 3. §1 — vuestro arreglo de la confianza (0.9.22), y cómo encaja con el modo nuestro

Con `JoinToSegment` copiando `j.Confidence`, `A1|B` (0,30) y `G|D` (0,27) **bajan al segundo nivel**:
degradadas, no excluidas; `J` (0,96) e `I|F` (0,56) no se tocan. Suscribo el matiz tal como lo
dejáis.

Los dos ajustes son **complementarios y conviene que existan los dos**, porque responden a preguntas
distintas:

| Ajuste | Qué hace | Para qué |
|---|---|---|
| Vuestro umbral en dos niveles (0.9.22) | **degrada** el empalme dudoso | que la ruta siga existiendo cuando es la única salida |
| Nuestro `?exclude_runway_crossing=1` | **excluye** lo que cruza pista | que una ruta no cruce una pista en silencio, aunque exista |

Y el dato de §1 dice que hoy la discusión es preventiva: en esta tanda ningún cruce entra en ninguna
ruta.

---

## 4. Cierre

- **No pedimos nada nuevo.** Lo que falta —traza a 1 Hz con guía activa y persistir el `planned`— está
  en vuestra cola, sin fecha, y lo entendemos.
- **Sin observaciones sintéticas**, de acuerdo: el corpus crecerá solo con 0.9.18+ y la métrica 3
  esperará a que tenga muestra.
- **Nuestro lado**, ya sin nada pendiente de vosotros: los 3 casos de ruta distinta (`SKPE/08` con
  745 m primero) y los 4 sin camino publicado. Cuando estén, re-medimos con la métrica ya reexpresada
  y con `lock_armed` por caso en el CSV.

La fase 4 sigue sin evidencia —y ahora la frase se puede terminar: **falta corpus escrito y una
re-medición**, no una definición.
