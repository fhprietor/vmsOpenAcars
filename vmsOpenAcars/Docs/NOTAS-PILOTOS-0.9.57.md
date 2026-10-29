# Novedades de vmsOpenACARS — versión 0.9.57

**En una frase:** los anuncios de cabina dejan de llegar tarde — el de pushback suena **antes** de que
el avión se mueva, y los de llegada **cuando ya has despejado la pista**.

---

## Qué pasaba

El PA de pushback («cerramos puertas, vamos a empujar») se lanzaba al entrar en la fase `Pushback`, y
esa fase solo se declara cuando el avión lleva **~11 segundos** moviéndose. En el vuelo `r4JNk2b2bBMJd8lD`
el freno se liberó a las **01:54:34**, el beacon se encendió a las **01:54:42** y el anuncio sonó con el
remolque ya terminando (**freno puesto 01:55:24**).

Y el de «desarmar toboganes y verificar» iba en la fase `AfterLanding`, que entra **en el mismo instante
del toque**: se oía a **145 kt**, con las reversas al 100 %, mientras el avión todavía frenaba por la
pista.

## Qué cambia

- **Pushback:** se anuncia con la primera señal de que la tripulación va a mover el avión — **beacon
  encendido** o **freno de parqueo liberado** con el avión aún en el puesto. Si el vuelo empieza con el
  empuje ya en marcha, sigue habiendo aviso (con la entrada en fase como red).
- **Llegada:** la orden de cabina y la bienvenida a destino esperan a que el avión **ya no esté sobre la
  pista**, comprobado con los datos de pista de NavData. Sin datos de pista se comporta como antes: el
  aviso no se pierde.
- **El registro marca el principio del anuncio**, no su final. Antes la hora apuntada era cuando acababa
  de hablar, lo que hacía imposible juzgar por el log si el aviso llegaba a tiempo.

## Lo que hay que saber

Nada que configurar: se aplica solo. Si vuelas con los anuncios de cabina apagados, esto no te toca.
