# Respuesta (5) al equipo de phpVMS — vuestros tres arreglos, comprobados contra la traza

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra respuesta (5): el gráfico del flare corregido
> **Estado:** **los tres arreglos son correctos** y los hemos comprobado contra nuestra traza local.
> Van dos matices de la deducción del eje que conviene escribir, **el piloto que os falta** para
> cerrar los 10 vuelos, y **una errata nuestra**: eran 16 muestras en tierra, no 17.

---

## 1. Lo que hemos podido comprobar (y cuadra)

| Vuestra afirmación | Nuestra traza |
|---|---|
| Reproducción de −923 / −919 / 6,6° y 81 muestras en 10 s = 8,1 Hz | ✓ exacto |
| «VS a 10 ft» no sostenido por ningún dato | ✓ **no hay ninguna muestra a 10 ft AGL** |
| Vuelo 43: tramo de tierra clavado a 0 y `touchdown_dist_ft` = 1156 | ✓ 16 muestras a **0,0** y **1156,7 ft** |
| Deducción del umbral: `dist_toque_traza + touchdown_dist_ft` | ✓ −698,3 + 202,3 = **−495,7** |

Con el eje ya alineado (umbral legal en 0), las puertas de esta traza quedan así — os sirve para
contrastar las marcas:

| Puerta | Distancia al umbral | ¿Tiene muestras a los dos lados? |
|---|---|---|
| Cruce de **50 ft** AGL | **+493 ft** (51 ft arriba, 48 ft abajo) | **sí** |
| Cruce de **30 ft** AGL | **+144 ft** (30 ft a los dos lados) | **sí** |
| Cruce de **10 ft** AGL | — | **no**: solo hay 15 ft por arriba |
| Mínimo de tasa (**−939 fpm**) | +346 ft | — |
| Cabeceo máximo (**6,6°**) y **toque** | **−202,6 ft** (el `touchdown_dist_ft` del payload dice 202,3) | — |

Es decir: cuando el chip de 10 ft desaparece, **desaparece porque el dato no lo sostiene**, y los de
50 y 30 se quedan porque sí. Nada que objetar.

## 2. La deducción del eje: correcta, con dos cosas que conviene dejar escritas

Vuestra fórmula es la buena y funciona. Dos matices, porque los dos pueden morder más adelante:

**a. Tiene que SUMAR, y eso es una trampa de nuestro contrato.** `touchdown_dist_ft` viaja como
**magnitud positiva** (202,3 ft *pasada* la cabecera, medido desde el umbral legal), mientras la
traza usa **negativo pasado el umbral**. Quien combine las dos con el mismo signo —`−698 − 202` =
−900— se equivoca **404 ft y no se entera**. Lo anotamos en nuestro lado, pero mejor que esté escrito
en los dos.

**b. La deducción hereda la fiabilidad del flag `on_ground` de la traza.** En el vuelo 44 el flag
cambia exactamente en el toque (seq 65, y ahí está el cabeceo máximo de 6,6°), y por eso sale bien.
Pero en nuestras trazas ese flag puede llegar **hasta ~0,9 s tarde** —está en nuestro
`ESTADO-FLARE`: el AGL cruza 0 y el flag no cambia hasta casi un segundo después—, y a 150 kt son
**~230 ft de error en el umbral deducido**, en silencio. Vuestro chequeo de pista no lo caza siempre.
Sugerencia concreta: **exigir monotonía** (cada muestra más cerca del umbral que la anterior, con
tolerancia) antes de deducir; si no se cumple, no hay referencia que deducir.

**Y sobre el vuelo 43: la decisión es la correcta, pero el motivo que dais no es el que la sostiene.**
**1156 ft pasada la cabecera caben en cualquier pista de ese tipo**, así que un chequeo de «dentro de
la pista» **habría pasado**. Lo que hace imposible el 43 es que su traza es **internamente
discontinua**: la última muestra **en el aire** está a **−1143,8 ft** (y con AGL **−6 ft**, que es
otra señal de que ese tramo no es una medida) y las 16 de tierra están a **0**. No hay un
desplazamiento único que haga cuadrar las dos, así que no hay nada que deducir. Esa comprobación
—«las muestras en tierra no pueden estar *más cerca* del umbral que la última en el aire»— es la que
la caza, y es la que os recomendamos añadir junto a la de monotonía.

## 3. «VS a 10 ft»: de acuerdo con el arreglo, y con dejar las puertas en AGL

La regla de «solo si las dos muestras que rodean la puerta están en el aire» es exactamente lo que
hace falta, y en esta traza deja fuera al 10 ft por el motivo correcto. **Y nos parece bien no
pasarlas a distancia**: en AGL es como el piloto piensa el flare, y con esa regla ya son honestas.
Solo lo dejamos apuntado para el día que queráis las dos cosas: la puerta en distancia no depende de
la cuantización del AGL cerca del suelo —el vuelo 43 tiene una muestra **en el aire con AGL −6 ft**—,
así que sería la única que sobrevive *siempre*.

Lo de que el número fabricado también iba al bloque del modelo nos parece el hallazgo importante de
esta ronda: un chip inventado no se queda en la pantalla, **entra en el análisis**. Los chips
`Muestras · Frecuencia · Duración` son la respuesta correcta a eso.

## 4. El gráfico: las tres marcas caen donde dice el dato

Hemos contrastado vuestras marcas contra la traza y **coinciden**: umbral en 0, toque en −202,6 —la
primera muestra con `on_ground`, y el `touchdown_dist_ft` del payload dice 202,3, así que los dos
caminos se separan **0,3 ft**— y el **inicio de flare en la banda correcta** —el cruce de 50 ft
AGL está en **+493** y el mínimo de tasa en **+346**—. Si nos decís con qué definición lo calculáis,
os damos el valor exacto para que lo fijéis como referencia.

**Y una corrección a lo que dice vuestra respuesta (5), que es culpa de nuestro texto:** con los
rellenos que os mandamos **el agregado de perfiles no gana masa**, porque **los 30 van con
`trace: []`** (0 muestras, comprobado en los 30); traza solo la llevan los vuelos 43 y 44. Lo que
gana masa con los rellenos es vuestro agregado **escalar** (cuantiles de tasa, G, corte…), que es el
que ya tenéis. Para la banda P50 sobre la curva harán falta **vuelos nuevos** con la 0.9.37 — una
razón más para que el primer vuelo importe.

Lo demás del dibujo ya no lo tocaríamos: los dos paneles con el mismo eje y el AGL como segunda
serie, las tres líneas verticales, el trazo discontinuo y la nota de la derotación debajo del panel
derecho. Menor, sin prisa: las escalas del AGL y del IAS siguen autoajustándose, y el eje Y de la
tasa invertido lo dejamos a vuestro criterio (rotularlo, si os lo preguntan).

## 5. Errata nuestra: eran 16 muestras en tierra, no 17

En nuestra respuesta (4) dijimos «**17** de las 81 muestras tienen `agl_ft = 0`» y son **16**
(seq 65 a 80). Vuestra respuesta (5) repite la cifra, así que la corregimos en los dos sitios: la
errata es nuestra y no cambia nada del razonamiento —el 20% de la traza en tierra y el pico vertical
del panel antiguo siguen siendo el mismo caso—, pero es una cifra y estaba mal.

## 6. El piloto que os falta para cerrar los 10 vuelos

Los 10 vuelos de la lista de nuestra respuesta (3) son **todos del mismo piloto**, el de pruebas de
este cliente:

| | |
|---|---|
| `pilot_id` | **55** |
| `ident` | **VHR055** |

Con eso los podéis buscar en vuestro lado. **El nombre privado no os lo mandamos**: con el id os
basta para localizarlos y es dato que no hace falta mover. Y recordad lo que ya avisamos: **tres de
los diez no tienen número de vuelo ni ruta** en el log local (son del 4 al 6 de mayo, cuando el
cliente aún no lo escribía), así que esos tres solo se pueden buscar por **piloto + fecha**:

| # | Fecha (UTC) | Vuelo | Ruta |
|---|---|---|---|
| 1 | 2026-05-04 12:03 | *(sin número)* | — |
| 2 | 2026-05-06 01:52 | *(sin número)* | — |
| 3 | 2026-05-06 03:24 | *(sin número)* | — |
| 4-10 | 2026-05-16 … 2026-08-26 | 875 · 62 · 8422 · 8180 · 55CH · 7743 · 208 | las de la tabla de la respuesta (3) |

---

## 7. Lo que nos queda

1. **Los 10 vuelos**: con el piloto ya podéis buscarlos; nos decís qué encontráis.
2. **El primer vuelo con la 0.9.37**: es el que dirá si la traza nueva llega ya alineada — así el
   camino de deducción pasa a ser solo para el histórico— y si logbook y PIREP dicen la misma tasa
   de contacto. Os contamos lo que salga.
