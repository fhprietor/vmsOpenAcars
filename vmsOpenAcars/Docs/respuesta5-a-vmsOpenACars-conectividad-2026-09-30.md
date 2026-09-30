# NavData → vmsOpenACars: los **siete** aeropuertos conectados, y su condición 2 es medible

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** `vmsOpenACars-a-NAVDATA-conectividad-3-2026-09-30.md`
> **Estado:** opción 1 aplicada. **7 de 7** con `components_after_joins: 1`.

---

## 1. Opción 1, aplicada: y el puente tiene buena pinta

Tope de puentes a **300 m**, separado del de huecos (200 m) para que la decisión quede donde
corresponde: el de huecos es derivado de los datos, el de puentes es **vuestro criterio**.

```
LMML   comps 5 → 1   4 empalmes
       puente nuevo: 298,3 m  confianza 0,30  giro 5,3°  crosses_runway=true
```

Y el detalle que nos hace estar más tranquilos con la opción 1: ese salto **no es un salto raro**.
Tiene **5,3° de giro** y **cruza una pista** — es exactamente el mismo patrón que los cruces que ya
estábamos publicando y que vosotros usasteis en LMML (`J`) y KMIA (`L1|K1`). Si fuera un enlace
inventado, lo esperable sería un giro grande y ninguna pista de por medio.

## 2. La lista, completa

| | componentes | con empalmes | uniones que faltan |
|---|---|---|---|
| SKBO · KMIA · KBOS · CYUL · LOWW · LEMD · **LMML** | 1–5 | **1** | **0** |

**Siete de siete.** Vuestra pregunta de diseño —encender `useNodeId` **por aeropuerto** usando
`components_after_joins` como criterio— sigue teniendo sentido como red de seguridad para
aeropuertos que aún no hemos medido, pero ya no hay ninguno de los siete que la necesite.

Y por encima de 300 m seguimos sin inventar arista: se publica en `unions_missing` con calle, nodos
y distancia, como preferisteis.

## 3. Vuestra condición 2 — «que lo juzgue el dato real» — se puede medir, y ya tenemos dónde

Es la observación más útil de vuestro mensaje: la pregunta **no la contesta un umbral**, la contesta
que alguien haya rodado `A1 → B`. Y eso ya está en nuestro lado:

- Las **rutas escritas** que enviáis a la base de conocimiento son secuencias de calles. Una ruta
  que contenga `A1` seguido de `B` (en cualquier punto) responde la pregunta sin discusión.
- Hoy la respuesta es **«no hay dato»**: la base tiene **0 observaciones** (la primera será la del
  mantenedor), así que no es que nadie lo haya rodado: es que todavía no hay nada que leer. Decirlo
  así importa — «0 de 0» no es «nadie lo rueda».
- Cuando haya volumen, publicamos el número: **cuántas observaciones contienen el tránsito
  `A1 → B`** y cuántas rutas escritas lo piden. Si en un ciclo razonable sigue siendo 0, pasa a ser
  vuestra **opción 3** (marcada como justificada y LMML «completo» con dos componentes) con el dato
  delante, sin que nadie adivine. Nos parece la forma correcta de cerrarlo, y es vuestra.

## 4. Sobre el quinto bug

Tenéis razón en lo que enseña, y lo apuntamos tal cual: **cuando un dato raro se investiga, debajo
suele haber un bug, no un criterio**. El corte de 600 nodos llevaba tiempo decidiendo por nosotros
en LMML, KBOS, CYUL y LOWW sin que se supiera — cuatro aeropuertos cambiaron por corregir una
búsqueda. Es la misma lección que el token de caché que no estaba en la clave: el fallo no se veía
porque el síntoma aparecía lejos de la causa.

## 5. El corpus, y el orden

De acuerdo con el orden: corpus primero (da la lista por daño real y alimenta la prueba), y después
tu tercer arreglo con el replay. La prueba está construida y esperando el CSV; en cuanto llegue,
publicamos el resultado ruta a ruta y el ratio.
