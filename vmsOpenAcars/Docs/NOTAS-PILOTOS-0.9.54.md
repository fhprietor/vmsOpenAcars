# Novedades de vmsOpenACARS — versión 0.9.54

**En una frase:** el rodaje de salida empieza a contar **desde que el avión se mueve**, no desde que el
ACARS declara `TAXI OUT`. Lo pidió un piloto y tenía razón.

---

## Qué pasaba

En el vuelo `rMBVEKj5PZzx0pX9` (SKRG→SKSM, A321):

| Hora | Qué pasó |
|---|---|
| 18:25:32 | `── PUSHBACK ──` |
| 18:26:40 | el avión se queda parado |
| **18:28:41** | **empieza a rodar de verdad** (6 kt, luego 17, 22, 24) |
| **18:30:31** | **`── TAXI OUT ──`** — 110 s después |

`TAXI OUT` no se declaró al moverse porque la transición espera al **freno de parqueo**, y en ese vuelo
**no llegó ningún evento de freno**. Saltó el **rescate de 300 s** (299 s justos desde que entró
`PUSHBACK`). Consecuencia: **110 s de rodaje real no contaban** para el tiempo en movimiento y, por
tanto, tampoco para la bonificación de motor único.

## Qué cambia

- **El rodaje se cuenta desde la última vez que el avión estuvo quieto.** Mientras el ACARS está en
  `PUSHBACK`, todo lo que rueda el avión se guarda; cuando se declara `TAXI OUT`, ese tramo se suma con
  su tiempo, sus metros y el reparto de motor único que **de verdad** hubo en esos segundos (no se
  estima: la telemetría ya traía el estado de los motores).
- **Se cuenta desde la parada, no desde el empuje**: el remolque del pushback queda fuera, porque lo que
  cuenta es el tramo continuo que acaba en la transición.
- **La fase no se retrasa.** `Block Off` y las líneas del registro siguen con la hora real de la
  transición; moverlas haría que el PIREP dijera que saliste del puesto antes de moverte.

Con eso, en un vuelo como el de ayer el tiempo en movimiento empieza ~2 minutos antes y la métrica dice
lo que el piloto hizo de verdad.

## Lo que sigue pendiente (y quiero saber)

**Por qué no llegó el evento del freno de parqueo** en un A321 de Fenix. El offset es
`0x0BC8` (`0 = off, 32767 = on`) y el umbral del cliente es el 60 %, así que o el avión no lo publica o
el freno no se puso. Si te vuelve a pasar, avísame: el siguiente paso es apuntar el valor crudo en el
registro para saber cuál de las dos es.

## Lo que no cambia

Nada más: fases, puntuación, PIREP, spoilers, reversas, tren, RAAS y anuncios siguen igual.
