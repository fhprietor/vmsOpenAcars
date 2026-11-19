# Novedades de vmsOpenACARS — versión 0.9.75

**En una frase:** la altura del aterrizaje deja de medirse contra una referencia fija y pasa a ser la
**altura real de las ruedas**; el go-around ya no vuelve a la altitud del origen, y el análisis recibe
la pista y el TCH para medir la senda.

---

## La altura del aterrizaje, medida de verdad

El AGL que viajaba con la traza era **altitud menos la elevación del plan**. En SKBG esa referencia
estaba **47 ft por encima del umbral**, así que la gráfica decía que el avión volaba **bajo tierra**
(−23 ft) cuando en realidad estaba a 24 ft sobre la pista. No era un caso aislado: en SKMD el desfase
era 28 ft, en SKRG 59 y en MPTO 03R **114**.

Ahora el cliente manda, por cada muestra del flare:

- **`alt_msl_ft`**: la altitud real, sin restar nada.
- **`radio_alt_ft`**: la **altura de las ruedas** sobre el terreno, no la indicación cruda del radar.
- **`agl_ft`**: se queda como dato histórico, y **sin el 0 forzado en tierra**. En tierra ya no es 0:
  es la altura real, que puede ser negativa por el desfase del aeropuerto.

Con eso, la «puerta de 10 ft» del análisis deja de medir 57 ft en SKBG o 112 en MPTO: mide 10.

## El cero del radar no es igual en todos los aviones

El radar mide desde un punto del modelo, no desde las ruedas: el **mismo A320** da **0,41 ft** en el
ToLiss y **12,15** en el Fenix; un 737, unos 9,5. Por eso el cliente mide el cero **al principio del
vuelo** y lo manda con la traza. Sin ese cero, un «10 ft» significaba 0,5 ft de ruedas en un 737 y
9,8 en un A320; ahora son 10 ft en cualquier avión.

## El go-around ya no salta

Si el avión iniciaba un go-around, la fase pasaba a `Climb` y la referencia volvía al **origen**: en
el PIREP `0aNnVLMo8pxpbNPq` el AGL saltó de **395 a −1.076 ft en una muestra** estando sobre el
destino. Ahora, una vez que el vuelo entra en la llegada, la referencia es el **destino** aunque la
fase sea `Climb`.

## El vuelo retomado ya no confunde MSL con AGL

Un vuelo retomado en el aire reconstruía el plan sin elevación y el AGL quedaba igual a la altitud
(MSL): el `9o45JZ5eAVZ11qAe` mostraba **+6.964 ft** de «AGL» sobre un campo a 6.955. Ahora, si el plan
no trae la elevación, el AGL de la traza va **`null`** y la altitud MSL y el radar siguen viajando.

## La pista y el TCH

El análisis recibe también la **elevación del umbral**, la del extremo opuesto, el **TCH derivado**
(con su procedencia y el motivo cuando no se puede derivar) y el **ángulo de la senda**. Es lo que
permite medir la desviación contra la senda publicada. En SKBG 35 el TCH derivado no se publica
(umbral desplazado), así que se usa la senda sin TCH y se dice.

## Lo que hay que saber

- **No hay nada que configurar.**
- **Los vuelos antiguos no se reescriben**: su traza se queda como estaba. El análisis nuevo usa los
  campos nuevos; las trazas viejas conservan el aviso de «altura no fiable».
- **Los umbrales de puntuación no cambian.** Lo que cambia es la altura con la que se juzga el flare,
  no los criterios.
- La primera verificación en vuelo es un aterrizaje en SKBG 35, un addon con cero alto (Fenix o iFly),
  un vuelo retomado y un go-around.
