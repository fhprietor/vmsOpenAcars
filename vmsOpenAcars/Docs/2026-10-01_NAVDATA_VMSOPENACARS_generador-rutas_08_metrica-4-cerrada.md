# NavData → vmsOpenACars: con vuestra regla, la métrica 4 da 9 — y el criterio falla

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_07_decisiones-medicion.md` y a la
> portada `…_04_portada-tanda-1.md` corregida (v2: 4.876 B, `sha256 84ca1b24…`; la v1 tenía 4.783 B
> y `b29f3358…`)
> **Adjunto:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_08_metricas-su-regla.csv`
> **Estado:** las cuatro decisiones están cerradas. **Con vuestra definición, la métrica 4 no pasa:
> 9 eventos en 33 rutas**, y 8 de las 33 con al menos un aviso. Ya no hay interpretación: es un
> resultado.

---

## 0. Lo que cambia con vuestra definición

Teníais razón en lo de fondo: **mis dos lecturas medían otra cosa.** Yo usaba como referencia el
**acercamiento al umbral de pista**; la vuestra es el **progreso respecto al punto más cercano del
episodio**. Son criterios distintos, y por eso daban 0 y 22. Implementada la vuestra tal cual (fuera
de ruta = > 45 m de la polilínea, 15 s, sin progresar ≥ 50 m respecto al punto más cercano del
episodio, habiendo estado antes en ruta, enfriamiento 20 s):

| Lectura simulada | Eventos en 33 rutas |
|---|---|
| A (mía, v1): cualquier acercamiento a la **pista** anula el aviso | 0 |
| B (mía, v1): acercamiento a la **pista** ≥ 50 m anula el aviso | 22 |
| **Vuestra regla** (progreso ≥ 50 m respecto al **punto más cercano del episodio**) | **9** |

**9 eventos, y el umbral es 0: la métrica 4 FALLA.** Y es una **cota inferior**: con 30 s por punto,
una excursión de 15 s entre muestras no se ve, así que a 1 Hz el número será ≥ 9. La corrección de la
simulación es mérito vuestro, y queda anotada como errata nuestra de la medición #6.

---

## 1. Los 8 casos, para que sea accionable

| Aeropuerto | Pista | Eventos | Cobertura | Lateral | Puntos | Ruta generada |
|---|---|---|---|---|---|---|
| SKBO | 14R | **2** | 73,5 % | 4,6 m | 34 | `C B8 B N K K1` |
| SEGU | 21 | 1 | 40,0 % | **83,0 m** | 15 | `B` |
| SKRG | 01 | 1 | 50,0 % | **49,3 m** | 22 | `G A A2` |
| SKBO | 14R | 1 | 84,2 % | 3,1 m | 19 | `W G N K K1` |
| SKBO | 14L | 1 | 64,7 % | 11,8 m | 17 | `W G M S A A3` |
| SKBQ | 05 | 1 | 64,3 % | 4,5 m | 14 | `D A A3` |
| MMGL | 11R | 1 | 65,0 % | 3,1 m | 20 | `B A` |
| SKRG | 01 | 1 | 71,4 % | 2,9 m | 14 | `H A A2` |

Dos casos son de ruta claramente distinta (`SEGU/21`, 83 m de lateral, y `SKRG/01`, 49 m); los otros
seis van pegados (2,9–11,8 m) pero tienen **una excursión corta**. Con la traza a 30 s no podemos
decir si es la plataforma de salida, un corte de esquina o un punto de muestreo: **para arreglarlos
hace falta la traza a 1 Hz** que os comprometisteis a mandar. Aquí sí os damos la razón: es
exactamente donde la densidad cambia el diagnóstico.

---

## 2. Las cuatro decisiones: cerradas

| # | Decisión | Cómo queda |
|---|---|---|
| 1 | Métrica 4 | **Cerrada con vuestra definición** (§0). Simulamos esa y ninguna otra. El número de hoy: **9**, cota inferior |
| 2 | Cobertura | **Aceptada la enmienda**: ventana del primer punto de red al punto de espera. Publicamos **65,0 %** (definición original) y **73,3 %** (enmendada) |
| 3 | Métrica 3 sin ruta escrita | **Excluida, nunca fallo.** Sin mínimo de muestra (con 1 no hay base medida); el «≥ 25 rutas» queda como **propuesta**, no como cifra |
| 4 | Baseline | **Aceptamos vuestra salida (ii)**: persistir la polilínea y el texto que propone el cliente. Es la única que da muestra a la métrica 3 y deja el §3.3.2 verificable. Confirmadnos en qué versión entra y lo medimos |

Sobre vuestra lectura de la cobertura, un matiz con datos, porque afecta a lo que esperáis de la
traza densa: **la cobertura no falla por el muestreo, falla por los extremos.** 28 de 33 trazas
acaban **más allá del punto de espera** (en la pista, y ahí nuestra ruta termina a propósito) y 19 de
33 empiezan en la plataforma. Con la ventana enmendada el número sube a 73,3 %, y lo que queda por
debajo son **3 casos de ruta distinta** (KBOS, SEGU, SKRG/19) más el efecto de tener 9–34 puntos por
traza. Es decir: la traza a 1 Hz mejorará la métrica 4 y afinará el diagnóstico, pero **la cobertura
seguirá fallando** hasta que arreglemos esos 3 casos. No queremos que la densidad tape ese trabajo.

---

## 3. Vuestra §5: los dos empalmes y la tabla de coste

**Los dos empalmes que usa la ruta de LMML son `J` (23,2 m, conf 0,96) e `I|F` (188,3 m, conf 0,56)** —
los otros dos publicados (`A1|B` 298,3 m y `G|D` 16,4 m) unen **otras** piezas y esa ruta no los pisa.
La red necesita los cuatro para quedar en 1 componente; una ruta usa los que cruza.

**Y tenéis razón en la tabla: la que os di en #6 mezclaba dos cosas.** La exacta, con el perfil que
sirve (`k = 0,5 m/grado` sobre la suma de **todos** los giros, no solo los > 25°):

| Ruta | Distancia | Giros acumulados | Coste (k = 0,5) |
|---|---|---|---|
| **`E N A S A A3`** (la que sirve) | **2.269,3 m** | **338,0°** | **2.438,3** |
| Alternativa más cercana (vetando `N`) | 2.265,2 m | 346,9° | 2.438,6 |
| Solo distancia (`k = 0`) | 2.220,9 m | 839,7° | 2.640,7 |
| `E M S A A3` del **prototipo retirado** | 2.256,6 m | — | **no reproducible** |

El `E M S A A3` de 2.256,6 m salió de la búsqueda aproximada que retiramos: **no es el óptimo de la
función de coste que se sirve**, así que no es comparable y no debió entrar en la tabla (de ahí el
«Δ 12,4 m»). Con los números buenos: la servida es **4,1 m más larga** que la alternativa más cercana
y le gana **0,3 de coste (0,01 %)** — un empate técnico. Se reformula a «**menos giros** (338° frente
a 840° de la ruta de solo distancia), misma distancia», y el perfil queda aquí publicado para que sea
comprobable desde fuera.

---

## 4. Vuestra §6: la discrepancia de forma, explicada

Los dos contamos bien y contamos cosas distintas, por cómo se rellena el CSV:

- **36 filas** traen `runway_distance_m` con valor < 300 m, y su mediana es **69,6 m** — es lo que veis
  vosotros, y es correcto.
- **37 de 38** casos tienen pista derivable con extremo a < 300 m, mediana **62 m** — es lo que dije
  yo, y también es correcto.

La diferencia es **una fila**: LMML, cuya pista (`05`) viene del PIREP y no se deriva, así que su
`runway_distance_m` va **vacío** en el CSV. Si se le deriva igual, son 37 y 92 m. Lo dejamos escrito
como «37 de 38 medibles; 36 con distancia derivada en el CSV» para que no vuelva a parecer un
descuadre.

---

## 5. Qué hacemos ahora

1. **Los 8 casos de §1**, uno a uno, con la traza a 1 Hz cuando llegue: los dos de ruta distinta ya
   están identificados.
2. **La enmienda de cobertura** aplicada y publicada junto a la original en la próxima medición.
3. **Esperamos vuestra confirmación** de la salida (ii) para la baseline (versión y forma de la
   polilínea persistida) y de si la traza densa entra en la próxima tanda.

Con la métrica 4 cerrada, **la fase 4 sigue sin poder plantearse**: 9 avisos sobre 33 vuelos no es
«0 falsos `FUERA DE RUTA`». Preferimos decirlo con el número en la mano que dejarlo en una
interpretación.
