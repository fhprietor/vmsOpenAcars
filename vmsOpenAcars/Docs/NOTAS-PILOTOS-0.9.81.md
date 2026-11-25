# Novedades de vmsOpenACARS — versión 0.9.81

**En una frase:** los avisos en pantalla cambian de aspecto —tipografía y colores nuevos— y el
aterrizaje tiene su propia tarjeta con los datos del toque.

---

## 1. Los avisos del OSD, con otro estilo

Los mensajes que salen sobre el simulador dejan de ser una caja con una línea en letra de consola y
pasan a tener el mismo lenguaje visual: fondo oscuro, esquinas redondeadas, **rótulos en versalitas
espaciadas** y el mensaje en grande. La letra es ahora **Segoe UI**, no Consolas.

Los textos **no cambian**: los mismos avisos de siempre (penalizaciones, cambios de fase, espacio
aéreo, velocidad excesiva…), con otro aspecto. Cuando el aviso tiene dos partes —`PENALTY` y
`NAV LIGHTS −5 PTS`— la primera sale como rótulo y la segunda como detalle.

## 2. El aterrizaje tiene tarjeta propia

Al tocar tierra, en vez de una línea sale una tarjeta con:

- **Arriba**: `LANDED`, el aeropuerto y su nombre, la hora del contacto en UTC, y a la derecha el
  estado con la velocidad en tierra.
- **Tasa de descenso** en grande, con su unidad.
- **Fuerza G** del toque, en grande.
- **Velocidad** de aproximación y de tierra, **actitud** (cabeceo y alabeo) y desde cuándo estás en
  el suelo.
- **Una etiqueta de calificación** —`Perfect`, `Normal`, `Hard`, `Crash`— que sale de **la misma
  tabla que la puntuación**, así que dice lo mismo que el registro del vuelo y que el PIREP.

La tarjeta no lleva contador de toques.

## Lo que hay que saber

- **La tarjeta es una foto del contacto**, no un panel en vivo: la velocidad en tierra y la hora son
  las de ese instante y no se actualizan mientras ruedas.
- La posición, el tamaño y la opacidad siguen siendo los de Ajustes (`Screen`, `Opacity`,
  `Duration`), y el OSD sigue **sin robar clics** al simulador.
- El aviso de acceso denegado al simulador se sigue viendo, porque es accionable: no se ha tocado.