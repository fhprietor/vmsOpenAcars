# Respuesta a NavData — el TCH verificado y el `elevation_ft` del ILS, corregido

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData
> **Fecha:** 18/11/2026
> **Responde a:** vuestra respuesta sobre el TCH y la corrección del `elevation_ft` del ILS (18/11/2026)
> **Estado:** **verificado contra la API en vivo** esa misma tarde. Los tres TCH coinciden; el
> `elevation_ft` del ILS ya es `null` cuando era 0 y `threshold_elevation_ft` da el umbral. Queda una
> respuesta sobre `end_elevation_ft` (§5).

---

## 1. Verificado, no aceptado por fe

Abrimos el sobre, consultamos `/airport/{icao}/runways/` y `/airport/{icao}/ils/` y esto es lo que
devuelve la API hoy (AIRAC 2611):

| Aeródromo / pista | `tch_ft` | `tch_source` | `tch_unavailable` |
|---|---:|---|---|
| SKRG 01 | **53,7** | `derived` | — |
| SKCG 01 | **55,9** | `derived` | — |
| SKBO 14R | **54,4** | `derived` | — |
| SKBO 14L | — | — | `incoherent_geometry` |
| **SKBG 35** | — | — | **`displaced_threshold`** |
| SKBG 17 · SKRG 19 · SKCG 19 · SKBO 32L/32R | — | — | `no_ils` |

Los tres valores con TCH coinciden con los que publicasteis. La fórmula y la mediana del dataset no
las podemos comprobar sin el dataset completo; los tres casos y los tres motivos de
`tch_unavailable` sí.

## 2. Dos matices, uno ya cerrado (y uno de contrato)

**a. El `elevation_ft` del ILS: cerrado y verificado.** Lo corregisteis el mismo día: ahora es
**`null` cuando el registro venía a 0** (4.719 de 11.869, el 40 %) y publica
**`threshold_elevation_ft`**. Verificado en vivo: SKRG 01 → **6.965**, SKBG 35 → **3.854**, SKBO 14R
→ **8.348**, KJFK 04L → **12**, LEMD 18L → **1.922**. El `glideslope.altitude_ft` es la **antena de
senda**, no el umbral: en SKBO 14L son **8.425** frente a 8.354. Nuestro mapeo: umbral y TCH de
`/runways/`; del ILS, frecuencia, rumbo y senda. Ya no hay JOIN que explicar.

**b. SKBG 35 es el caso del hilo y no tiene TCH derivado.** `displaced_threshold` es exactamente
lo que necesitábamos saber: para la 35 de SKBG usaremos **el TCH de la carta**, no el derivado. Los
**179 de 4.104** y el **10 %** son la información que hace que el `null` sea honesto y no un hueco;
sin esos números, «no hay TCH» se leería como «no lo habéis mirado».

**c. `tch_source` se queda en el contrato.** Lo mandamos a phpVMS para que el gráfico pueda decir
«derivado» y no «de carta». Si algún día publicáis el TCH de carta, el campo ya tiene el sitio y no
hay que cambiar el contrato.

## 3. Lo que consumimos

- `tch_ft`, `tch_source` y `tch_unavailable` de `/runways/` (y de `/ils/` cuando venga).
- `threshold_elevation_ft` de `/ils/` (nuevo) y `elevation_ft` de `/runways/` como **elevación del
  umbral**; los dos coinciden en SKRG 01 (6.965).
- `ils_glideslope_angle` de `/runways/` como `glide_slope_deg`; `glideslope.pitch_deg` del ILS como
  comprobación.
- La pista **recíproca** para `end_elevation_ft`, mientras no se publique el campo (ver §5).

No pedimos nada más. El TCH de carta, si llega, es un campo nuevo, no un cambio de los que ya hay.

## 4. Lo que el TCH no arregla

El TCH no arregla el AGL del cliente: `agl_ft` sigue siendo **altitud − elevación del plan** (ver
la respuesta a phpVMS del 18/11/2026). El TCH es para la **Capa 3**, la desviación contra la senda,
no para el datum. Y para SKBG 35 la Capa 3 se queda, de momento, en «respecto a la senda de 3° que
pasa por el umbral», sin altura de cruce publicada: es la diferencia entre un dato derivado y el de
la carta, y el gráfico debe decirlo.

## 5. Sobre `end_elevation_ft`: no es bloqueante

**No os pedimos otra ronda por esto.** Si lo publicáis, que sea la elevación del **extremo físico**
(`end_lat`/`end_lon` de esa pista), no la del **umbral recíproco**. La diferencia no es teórica: el
umbral de la 17 de SKBG está **496 ft** dentro del pavimento, así que su `elevation_ft` (3.901) no
es la del extremo físico de la 35; para el perfil del flare el error es de pocos pies y no llega a la
zona de toma, pero un campo que se llame `end_elevation_ft` no debería heredar esa ambigüedad. Si lo
que publicáis es el umbral recíproco, no hace falta: ya lo sacamos nosotros y sería el mismo número.
Con la recíproca llegamos; el campo es una comodidad, no un requisito.
