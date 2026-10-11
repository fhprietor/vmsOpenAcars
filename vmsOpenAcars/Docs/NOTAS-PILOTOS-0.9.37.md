# Novedades de vmsOpenACARS — versión 0.9.37

**En una frase:** la traza fina del flare ya se guarda **entera** y mide **desde el mismo punto** que
la toma, así que el gráfico de la frenada y el dato de la distancia al umbral dicen por fin lo mismo.

---

## La traza del flare ya no pierde el tramo de la frenada

Si abrías el gráfico del flare de los últimos vuelos, **todo lo que pasaba después de tocar** caía en
una línea vertical al principio: la traza seguía capturando **2 segundos más** (la frenada y el morro
bajando), pero esas muestras se quedaban **sin sitio en la pista**. La causa era que, al dejar de estar
en aproximación, el cliente soltaba el umbral con el que medía, y las muestras de tierra se guardaban
con **distancia 0**.

Ahora el umbral **se congela al armar la captura** y la distancia se sigue midiendo contra él aunque el
avión ya esté en tierra. Está **verificado con un vuelo real** (SKCC → SKBG): las 15 muestras en tierra
ya ocupan su sitio, de **−698 a −1.196 ft**, medio millar de pies de rodaje en menos de 2 segundos.

## El flare y la toma ya miden desde el mismo punto

En pistas con **umbral desplazado** —donde el inicio del pavimento no es el punto desde el que se puede
tocar— la traza del flare medía desde el **borde del asfalto**, mientras que la distancia de toma se
medía desde el **umbral legal**. En la pista 17 de SKBG eso eran **496 ft de diferencia**, y se veía en
el gráfico: la marca de la toma y las bandas de la zona de toma salían corridas respecto a la traza.
Ahora las dos usan el **mismo cero**.

> **Honestidad sobre esto:** el arreglo está hecho y contrastado con los números del vuelo que lo
> destapó, pero **todavía no se ha volado con él**. Si en tu próximo aterrizaje ves la marca de la toma
> donde toca, queda cerrado.

---

## Lo que hay que saber

- La traza fina **no es exactamente de 10 Hz**: el ciclo real de telemetría, con el simulador cargado,
  corre a unos **8 Hz**. A 150 kt eso son unos **32 ft entre muestras**, no los 25 que decíamos. No
  cambia nada de lo que ves; cambia lo fino que se puede afirmar sobre la distancia de toma.
- Los vuelos **anteriores a la 0.9.34** no tienen traza fina: el gráfico usa la de 2 segundos y **dice
  cuál está usando**.
- Nada de esto toca tu **puntuación**: es análisis, no castigo.
