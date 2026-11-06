# Novedades de vmsOpenACARS — versión 0.9.66

**En una frase:** los anuncios de cabina llegan cuando toca: el de pushback **antes** de moverse, y el
de desarmar toboganes **en el puesto**, no en plena pista.

---

## Qué cambia

**Pushback.** Antes se anunciaba al soltar el freno de parqueo — que es justo cuando el avión ya empieza
a moverse — y encima perdía casi tres segundos en un aviso sonoro previo. Ahora:

- Se anuncia **en cuanto se cierran las puertas**, que es el hecho que el propio anuncio describe.
- Si no se puede saber, se anuncia **al encender el beacon con el freno todavía puesto** (la señal de
  «vamos a empujar» de verdad), luego al encender el beacon, luego al soltar el freno y, como última
  red, al entrar en pushback.
- **Sin el aviso sonoro previo**: la voz empieza de inmediato.

**Llegada.** El «desarmen toboganes» se lanzaba en cuanto la geometría decía que habías salido de la
pista — y en un retroceso por la pista eso se cumple con la pista aún delante, así que sonaba con el
avión todavía dentro. Ahora se anuncia **al poner el freno de parqueo en el puesto**. La bienvenida
(«hemos llegado a…») sigue saliendo al abandonar la pista, que ahí sí es el momento.

## Lo que hay que saber

- **Todo esto se aplica solo**; no hay nada que configurar ni que activar.
- Si vuelas con los anuncios de cabina apagados, esto no te toca.
- El aviso de «puertas cerradas» depende de que tu avión publique el estado de las puertas. Si el tuyo
  no lo hace, el anuncio sigue saliendo por el beacon o el freno, como hasta ahora: **nunca se queda
  sin anunciar**.