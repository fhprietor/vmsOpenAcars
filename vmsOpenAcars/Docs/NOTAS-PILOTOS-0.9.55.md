# Novedades de vmsOpenACARS — versión 0.9.55

**En una frase:** el `TAXI OUT` deja de esperar cinco minutos cuando el ACARS no ve el freno de parqueo:
ahora también cierra cuando el avión **rueda de verdad**.

---

## Qué pasaba

La salida de `PUSHBACK` tenía dos puertas: el **freno de parqueo** puesto (el fin del empuje) y un
**rescate de 300 s** por si el freno no llega. El problema es que el freno **solo avisa cuando cambia**:
si lo sueltas antes de abrir el ACARS y no lo vuelves a poner —que es exactamente lo que pasó en el vuelo
`rMBVEKj5PZzx0pX9`— no hay ningún evento que registrar, la puerta no se arma, y el ACARS espera los
5 minutos con el avión ya rodando.

**No era el avión**: ese mismo A321 reportó cinco eventos de freno en otro vuelo, y el código que lo lee
no ha cambiado entre la 0.9.46 y hoy.

## Qué cambia

Tercera puerta: **8 nudos sostenidos 15 segundos con un motor en marcha** (62 metros de recorrido).

- Un **tug** empuja a 4-6 nudos: no la abre.
- El **rodaje real** va a 9-24 nudos: la abre a los 15 s.
- Si el avión **no publica el estado de los motores**, la puerta no se abre y siguen decidiendo las otras
  dos: no se pierde nada.

En un vuelo como el de ayer, `TAXI OUT` se habría declarado **95 segundos antes**, sin tocar el freno.

## Lo que esto arregla de verdad

- El `TAXI OUT` aparece cuando empiezas a rodar, no cinco minutos después.
- La comprobación de **luces de taxi** cae donde tiene que caer, no al final del rodaje.
- El **rodaje de salida** se mide entero (ya desde la 0.9.54 cuenta desde que el avión se mueve, y ahora
  la fase tampoco llega tarde).

## Lo que no cambia

Nada más: fases, puntuación, PIREP, spoilers, reversas, tren, RAAS y anuncios siguen igual. Y si tu
avión sí publica el freno de parqueo, la puerta de siempre sigue siendo la primera en disparar.
