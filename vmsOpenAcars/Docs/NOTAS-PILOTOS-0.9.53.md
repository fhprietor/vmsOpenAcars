# Novedades de vmsOpenACARS — versión 0.9.53

**En una frase:** se retira lo que pedía la 0.9.52. **No hay que editar ningún archivo.**

---

## Qué pasó

La 0.9.52 leía el bloque de datos de PMDG, pero para que PMDG lo publique hace falta activar
`[SDK] EnableDataBroadcast=1` en el `.ini` del avión — y la versión pedía hacerlo en el registro de
vuelo. Estaba mal: es trabajo técnico que no le toca al piloto, no depende del ACARS y no todos los
equipos pueden editar ese archivo. **Se retira el aviso y la instrucción.**

## Qué hay ahora

- **No tienes que hacer nada.** Ni editar archivos, ni reiniciar nada, ni configurar el avión.
- Si **ya** tienes ese ajuste activo (lo necesitan otras herramientas como FS2Crew o algunas de
  AOC), el ACARS **lo aprovecha**: verás el piloto automático con sus modos y el autobrake real.
- Si no lo tienes, el registro pone **`---`** donde no hay dato, y **no te pide nada**.

## Por qué no se puede evitar

El dato lo emite **el avión**, no FSUIPC: PMDG publica su estado por el canal de su SDK y FSUIPC se
limita a escucharlo. Por eso **conectarse por SimConnect directamente no cambia nada** — se choca con
la misma puerta. Las otras vías se evaluaron y se descartaron: leer variables internas (LVars) exige
nombres que PMDG no publica, y deducirlo «por comportamiento» es una suposición que puede mentir.

Y lo que se pierde es cosmético: el piloto automático, el autobrake y los carteles de PMDG **no
puntúan nada**; solo alimentaban una línea del registro y el `nav_type` de la posición.

## Lo que no cambia

Nada del vuelo: fases, puntuación, PIREP, spoilers por posición, reversas al desplegarse, tren,
carteles de cabina estándar, RAAS, anuncios. Todo igual.
