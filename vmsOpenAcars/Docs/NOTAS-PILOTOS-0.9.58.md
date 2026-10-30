# Novedades de vmsOpenACARS — versión 0.9.58

**En una frase:** la línea de NavData que ves al conectar dice ahora **hasta cuándo** es válida tu base
AIRAC, no solo qué ciclo llevas.

---

## Qué pasa ahora

Antes, al conectar, el registro decía solo esto:

`🗺️ NavData API: AIRAC 2611 — activo`

Y no había forma de saber si esa base estaba a punto de caducar. Ahora:

`🗺️ NavData API: AIRAC 2611 — activo hasta 2026-11-12`

La fecha va en formato **año-mes-día** a propósito: `12/11/2026` se lee como 12 de noviembre aquí y como
11 de diciembre en Estados Unidos, y este log lo comparte gente de los dos lados del Atlántico.

## Qué cambia

- **Se informa la vigencia** del ciclo AIRAC en la misma línea del conectar.
- **Si NavData no manda la fecha**, la línea se queda como estaba: no aparece un «hasta» vacío.
- **Si el ciclo está caducado**, ya no se dicen dos cosas contradictorias seguidas («activo» y luego
  «expirado»): se avisa solo de lo que importa, que está expirado y desde cuándo.

## Lo que hay que saber

Nada que configurar. Y un recordatorio útil: cuando el AIRAC caduca, **la caché local de NavData se
purga** y se vuelve a descargar con el ciclo nuevo. Si ves la fecha muy cerca, renueva NavData antes de
volar y te ahorras esa descarga en mitad de la preparación.