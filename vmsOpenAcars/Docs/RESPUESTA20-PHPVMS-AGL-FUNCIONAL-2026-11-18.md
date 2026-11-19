# Respuesta (20) al equipo de phpVMS — la Capa 1, el `agl_ft` en tierra y dos avisos de fórmula

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 18/11/2026
> **Respecto a:** vuestra respuesta (19)
> **Estado:** la Capa 1 es la correcta y el cambio del `agl_ft` en tierra queda **documentado**; el
> orden de fuentes es bueno, pero el `pista` **no es altura de ruedas** sin restar el cero, y la
> fórmula de la Capa 3 que habéis escrito **resta dos veces**. Los dos avisos son pequeños y van con
> números.

---

## 1. El `agl_ft` en tierra: sí, es un cambio de semántica

Tenéis razón y lo hemos documentado en el `CHANGELOG` y en `CLAUDE.md`:

> En la 0.9.75, en tierra, `agl_ft` ya **no es 0**: es `altitud − referencia` y puede ser negativo (el
> desfase del datum). Las trazas anteriores a la 0.9.75 siguen llevando 0 en tierra. El filtro que
> descarta «el cierre» es **`on_ground && agl == 0`**; un valor en tierra distinto de 0 es una medida
> y se conserva.

Es exactamente la regla que habéis puesto. Y la lección es la que dices: cuando cambia el
**significado** de un campo, no solo su presencia, lo decimos en una línea. Lo hemos hecho en el
`CHANGELOG` y en la guía; a partir de ahora, cada cambio de semántica irá con su aviso en la respuesta
y en el `CHANGELOG`, no solo en el código.

## 2. Capa 1: el orden es correcto, pero `pista` no es altura de ruedas

El orden `radio` → `pista` → `agl` es el bueno. Una precisión sobre la fuente `pista`, que es la
misma trampa del hilo con otro disfraz:

```
pista = alt_msl_ft − elevación_de_pista   →  altura del PUNTO DE REFERENCIA del modelo,
                                              no de las ruedas
```

`alt_msl_ft` sale de `0x0570`, la altitud del **punto de referencia** del avión, y el radar mide desde
ese mismo punto: por eso su cero en tierra es **9,5 ft** en el B738, **12,15** en el Fenix y **0,41**
en el ToLiss. La altura de ruedas es `pista − radio_alt_zero_ft`. Sin restar el cero, la fuente
`pista` está **0–13 ft por encima** de la de ruedas, y la puerta de 10 ft vuelve a medir 19,5 ft de
referencia en un B738 (10 de ruedas) o 22 en un Fenix.

Dos formas de arreglarlo, elegid:

1. **Si llega `radio_alt_zero_ft`** (lo manda el cliente cuando el addon publica el radar), usad
   `pista − radio_alt_zero_ft`. Es la altura de ruedas y coincide con `radio` en el tramo bajo.
2. **Si no llega** (addon sin radar), la fuente `pista` es **altura de referencia**; no la comparéis
   con el TCH (que es altura de ruedas) sin decirlo. Etiquetadla como tal (`pista_ref`) o mantened
   el aviso de que esa puerta no está a la altura de las ruedas.

El `radio` es la única fuente que ya es altura de ruedas; el `pista` es mejor que el `agl` (que lleva
el datum del plan además del del modelo), pero no es equivalente.

## 3. Capa 3: la fórmula que habéis escrito resta dos veces

La definición que queréis es esta:

```text
D = max(0, dist_ft)                                  // pies ANTES del umbral (dist_ft > 0)
altura_senda_sobre_umbral_ft = tch_ft + D · tan(glide_slope_deg)
desviacion_senda_ft = (alt_msl_ft − threshold_elevation_ft) − altura_senda_sobre_umbral_ft
                      // + = por encima de la senda, − = por debajo
```

La vuestra dice:

```text
altura_senda_ft = (alt_msl − threshold_elevation) − (tch + d · tan(glide_slope))
desviacion_senda_ft = altura_msl_sobre_umbral − altura_senda
```

La primera línea **ya es la desviación**, no la altura de la senda. Al restarla otra vez en la
segunda, el resultado es la altura de la senda, no la desviación. Con un caso real:

| SKRG 01, a 1 NM del umbral | Valor |
|---|---:|
| `alt_msl_ft` | 7.365 |
| `threshold_elevation_ft` | 6.965 |
| altura sobre el umbral | **400 ft** |
| `tch_ft` + 6.076 · tan(3°) | **372,1 ft** |
| desviación correcta | **+27,9 ft** (alto) |
| con vuestra segunda línea | **372,1 ft** (la altura de la senda) |

No es un cambio de signo: es una resta de más. La fórmula correcta es la de arriba, y el signo
positivo es «por encima».

**Y el caso sin TCH.** Para SKBG 35 (`tch_unavailable = displaced_threshold`) no hay TCH derivado. Si
ponéis `tch = 0`, la referencia es una **senda de 3° con TCH 0**, que no es la senda publicada (la
carta cruza a ~50 ft): la desviación saldrá ~50 ft alta de forma sistemática. Decidlo en la frase
—«respecto a la senda de 3° con TCH 0»— o usad el TCH de la carta; lo que no se puede es llamarlo
«senda publicada» sin el TCH. Con el TCH derivado (`tch_source = derived`) la frase es «respecto a la
senda derivada», no «de carta»; la diferencia es de pocos pies, pero es la misma distinción que ya
aceptasteis para el TCH.

## 4. El primer vuelo 0.9.75

Todavía no hay ninguno: la base local no tiene vuelos posteriores a la 0.9.75, y el primero que se
vuele es la verificación. La lista de los cuatro puntos del §4 de la (18) sigue siendo la buena:

1. **SKBG 35**: `tch_ft` nulo + `displaced_threshold`, umbral 3.854, y la puerta de 10 ft de ruedas
   donde caía la de 57 ft de AGL.
2. **Addon con cero alto**: `radio_alt_zero_ft` 12–13, `radio_alt_ft` ≈ 0 en el toque, y
   `agl_ft − radio_alt_ft` ≈ el cero, no cientos de pies.
3. **Vuelo retomado**: `agl_ft` nulo en toda la traza, `alt_msl_ft` y `radio_alt_ft` poblados,
   `altura_fuente = radio`.
4. **Go-around**: sin salto de AGL al pasar a `Climb`, captura cerrada en el ascenso.

Cuando se vuele, pasadnos el `pirep_id` y os devolvemos la tabla con los cuatro puntos medidos y el
desfase estimado del §2 de la (16). Hasta entonces, lo que hay es lo que dicen los tests: los números
reales de SKBG, MPTO, SKRG y los ceros del Fenix/ToLiss, no una simulación.

## 5. Cierre

De acuerdo con el §5. El hilo ha ido de «el avión vuela bajo tierra» a una altura de ruedas medida,
declarada y con la fuente a la vista; y las dos correcciones que quedan —el cero del `pista` y la
resta de más de la Capa 3— son de la misma familia que las anteriores: una altura relativa a una
referencia tomada por la altura sobre otra. La diferencia es que ahora la referencia viaja en el
payload y se puede ver.
