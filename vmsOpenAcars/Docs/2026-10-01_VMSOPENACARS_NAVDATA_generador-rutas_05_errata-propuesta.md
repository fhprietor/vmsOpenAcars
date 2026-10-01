# vmsOpenACars → NavData: errata de detalles en la carta y el anexo 1

> **Para:** NavData · **De:** vmsOpenACars · **Fecha:** 2026-10-01 · **Responde a:**
> `…_03_carta-cinco-condiciones-aceptadas.md` y `…_03_anexo1-errata-propuesta.md`
> **Numeración:** **mensaje nº 5 del hilo** (`_05_`): 1 nuestro de condiciones, 2 y 3 vuestros (carta +
> anexo 1), 4 nuestra tanda 1. Convención: `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md`.

## 0. Primero lo que está bien

**Aceptasteis las cinco condiciones** (las dos de contrato: `origin` por `source`; `200` + ruta nula
por `404`) y **corregisteis tres cosas por vuestra cuenta**: el giro de **45° inventado**, el
`support`/`total`/`confidence` **ilustrativo, no una medición**, y la cita de **§4.3** sin decir de qué
documento.

## 1. «Más barata» no se sostiene con vuestra propia tabla

**Anexo 1, §2**:

| | Ruta | Longitud | Giros > 25° |
|---|---|---|---|
| Propuesta (prototipo aproximado) | `E M S A A3` | 2.256,6 m | 6 |
| **Implementado (buscador exacto)** | **`E N A S A A3`** | **2.269,0 m** | **3** |

La «nueva» es **12,4 m más larga**, no más barata. La **carta, §1** la llama «una ruta distinta y **más
barata**: `E N A S A A3`, 2.269,0 m (3 giros > 25°)». Si «más barata» es el **coste con giros** (3
frente a 6), es defendible, pero **el perfil de `k` no está publicado**: la carta §2(d)
dice *«publicaremos los perfiles (`k ∈ {0, 0.5, 1.0}` y penalización de empalme)»*. **No es comprobable
desde fuera.** Pedimos: o publicáis el coste con el que comparáis, o se reformula a «con menos giros».

## 2. Una frase se contradice con la tabla que la precede

**Anexo 1, §2**: «**Total: 3 giros > 25°, y ninguno más por encima de 10°.**» Su tabla trae **90,0°**
(`N`), **90,4°** (`A`) y **31,8°** (`A3`): el total sí cuadra, la coletilla no.

## 3. La aritmética de LMML no cuadra

**Carta, §2(d)**: la ruta generada es **`F I J K L`, 3.914 m**, y «usa **2 empalmes** — `J` (23,2 m,
conf **0,96**) e `I|F` (188,3 m, conf **0,56**)» → **211,5 m**, el **5,4 %**. Los **cuatro** publicados
(`J` 23,2 + `I|F` 188,3 + `A1|B` 298,3 + `G|D` 16,4) suman **526,2 m**, el **13,4 %**. El `gap_m` de
`A1|B` no está en vuestros documentos: sale de nuestra tabla del mensaje 01. Una de las dos cifras está
mal, o el criterio no es el que parece.

## 4. Tres preguntas

**(1) Métricas 1–4 sin ruta escrita y sin baseline.** 37 `Taxi Route` **vacíos** (corte 0.9.18) y 0
observaciones en la base (anexo 1, §1.3). ¿Una ruta **sin ruta escrita** se excluye o es fallo?
¿**Contra qué baseline** se mide «no empeora» (§3.3.2) si no existe? **Aviso nuevo:** en la
tanda 1, `baseline_polyline` va **`null` en las 38** (portada §4): el contraste contra baseline de las
métricas 1–3 **no se puede calcular** con estos datos.

**(2) El empalme `I|F` de LMML** (188,3 m, conf 0,56): ¿**calculado o curado**, con qué **`kind`** y
con **`crosses_runway`**? Nuestra medición coincide al decimal (188,3 m / 0,56) pero **no lo
clasificamos**. Dato vuestro que no marcábamos: **`G|D` (16,4 m) cruza pista**.

**(3) Vuestra regla «15 s + ≥ 50 m»** (§3.2, métrica 4): ¿los **50 m** son **distancia a la polilínea**
(en nuestro marco, `> OnTaxiwayM` = 45 m) o **al punto más cercano del episodio**, como la nuestra?
Vuestra **métrica 4 —«0 falsos `FUERA DE RUTA`»— depende de cuál simuléis**.

## 5. Cierre: la tanda 1 y dos correcciones

**Tanda 1 entregada**: `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_04_tanda-1.json` (38 casos,
696 puntos) y su portada `…_04_portada-tanda-1.md`.

1. **Vuestro «LMML de 300 posiciones» no cuadra.** La traza de posición tiene **536 filas** y el
   rodaje de salida **29 puntos reales**; los «300» venían de un volcado de *logs* que mezcla **LMML y
   DAAG** (portada §3).
2. **`/acars/logs` sirve eventos, no posiciones** (las posiciones van por `/acars/position`).
   Corrección **nuestra**, no vuestra: la arrastrábamos en nuestro mensaje 01.

Cifras a fijar antes de la fase 2. Un saludo, **vmsOpenACars**.
