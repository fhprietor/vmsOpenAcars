# `fuel_used` y `block_fuel`: confirmado, es nuestro, y ya está corregido

> **De:** equipo de vmsOpenACars · **Para:** Vholar Virtual Airlines
> **Fecha:** 5 de octubre de 2026 · **Responde a:** «Aviso: `fuel_used` y `block_fuel` se envían en
> KILOGRAMOS y phpVMS los interpreta como LIBRAS» (5/10/2026)

---

## 1. Confirmado — y fue por omisión, no a propósito

Tienen razón, y su tabla de 8 de 8 es correcta. La causa está de nuestro lado: del simulador
(`0x126C`, `CurrentFuelLbs`) el cliente **convierte a kilogramos para el piloto** — la línea
`Combustible: … kg` del log **no miente**, son kg de verdad — y **enviaba ese mismo número**, sin
declarar unidad alguna, a una API cuya masa interna son libras. Nadie lo notó porque, como bien
dicen, **los dos números son plausibles**.

**Lo que cierra el caso no es nuestra etiqueta, sino su propio contrato**, y queremos dejarlo
escrito para que no vuelva a depender de una etiqueta:

- La respuesta de su API lo declara en cada PIREP: `"block_fuel":{"localUnit":"kg","internalUnit":"lbs"}`.
- `config('phpvms.internal_units.fuel')` es `lbs`, y `FuelCast::set()` **devuelve el número plano sin
  convertir** (su propio test lo fija: *«no conversion with plain numbers»*).
- Y el cruce interno: **4444 kg × 2,20462 = 9797 lbs**, exactamente el `fuel` que enviábamos en las
  **posiciones**, que sí viajaba en libras. Ese cruce es lo que convierte una coincidencia de cifras
  en una unidad demostrada, y es el que nos faltaba para no «arreglar» a ciegas.

## 2. Corregido y publicado — v0.9.28

Opción A, la que recomiendan: **`block_fuel` (prefile y file) y `fuel_used` (file) salen ya en
libras**. La conversión vive en un helper puro con sus tests, con el caso real (4444 → 9797) y el
contrafactual (4444 sin convertir = 2015,76) para que el fallo quede fijado.

- El **`fuel` de las posiciones no se toca**: ya iba en libras.
- El **log sigue en kg** a propósito: es la cifra que el piloto entiende. La conversión está sólo en
  el payload.
- Su comprobación es exactamente nuestro criterio: en el próximo vuelo, `fuel_used ≈ Z × 2,20462`.
- **Los PIREPs ya registrados siguen 2,20462 por debajo.** El arreglo aplica **de aquí en adelante**;
  corregir el histórico es decisión suya. El precio del combustible **no** lo tocamos, de acuerdo con
  ustedes.

## 3. Sí, procede trasladarlo — y hay un campo hermano sin resolver

El defecto de fondo es el que señalan: **el contrato no declara unidad y el cast no valida**, así que
**cualquier cliente que envíe kg estará mal en silencio**. La opción B (un campo de unidad, o rechazar
valores fuera de rango) es la buena a largo plazo, y la apoyamos.

Y añadimos uno que hemos encontrado al revisar **todos** los campos de masa del payload: **`zfw`**.
phpVMS lo declara `float` **sin cast**, así que no tiene unidad declarada en ninguna parte. Hoy
enviamos el valor del **OFP en kilogramos**. **¿En qué unidad lo espera phpVMS?** Hasta saberlo no lo
tocamos, para no cambiar un error silencioso por otro.

*Los demás campos de masa —`cargo`, `payload`, `pax_weight`— no se envían: sólo se muestran.*

## 4. Petición

1. Que confirmen qué unidad espera `pireps.zfw`.
2. Que la próxima línea `Combustible: … kg` del log y `pireps.fuel_used` **no** coincidan en crudo:
   deben diferir en el factor 2,20462. Si coinciden, avísenos y lo revisamos de inmediato.