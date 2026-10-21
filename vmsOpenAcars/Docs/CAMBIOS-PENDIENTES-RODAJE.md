# Rodaje: lo que queda por hacer

Estado de partida: la medida del rodaje ya es de fiar (desplazamiento, no velocidad de tierra) y la
línea del log publica las cifras. Lo que sigue son los tres pasos que **no** se pueden hacer a ciegas,
con lo que hay que leer antes de tocar y por qué. Sale del PIREP real `29Z3A8r748rGPnzq` (SKRG,
20/10/2026), reclamado por el piloto en vuelo.

## 1. Publicar las cifras a phpVMS (bloqueado por la aerolínea)

Los campos custom del PIREP **los crea la aerolínea**; el cliente solo los rellena
(`Helpers/PirepFields.cs`, que usa el **NOMBRE** del campo y no el slug, y omite los que no tienen
valor para no borrar con `""` lo que ya estaba). Mandar un campo que no existe puede costar el PIREP
entero, así que **no se manda nada hasta que el campo exista en phpVMS**.

Propuesta: `Taxi Out` / `Taxi In` con los segundos con un motor y los metros, o un solo campo
`Taxi Engine Split`. Cuando la aerolínea lo cree, se añade la constante en `PirepFields`, se rellena en
`PirepBuilder` y **se enciende con un interruptor** en `AppConfig` (patrón
`taxi_planned_observation_enabled`: apagado no se construye el cuerpo ni se sale a la red).

## 2. Guardar las cifras en local (migración)

`landing_log.sqlite` guarda `taxiOutMoving` / `taxiOutSingleEngineSeconds`, pero **no** los segundos
sin motor ni los metros. Añadirlos toca cuatro sitios y uno de ellos es delicado:

- `LandingLogService`: el `INSERT` por parámetros con nombre (fácil) **y el `SELECT`, que mapea por
  ÍNDICE** (`r.GetDouble(28)`, `(30)`…). Añadir columnas **al final** del `SELECT` y numerar solo lo
  nuevo; no reordenar lo existente.
- `FlightRecord`: las propiedades nuevas (`TaxiOutNoEngineSeconds`, `TaxiOutMetres`…).
- `AcarsReporter.SnapshotLandingRecord`: rellenarlas desde `_pen`.
- Migración: `ALTER TABLE flights ADD COLUMN …` en el `Initialize()` que ya crea la tabla, envuelto
  para que una base vieja no rompa (comprobar `PRAGMA table_info` antes de añadir).

**Por qué no se hizo ya**: una migración mal hecha toca el histórico del piloto y no se prueba de
memoria. Va como cambio propio, con una base de prueba y un vuelo guardado de antes.

## 3. La gracia de las luces, desde el movimiento real

El −5 de «Luces TAXI APAGADAS durante el rodaje» se cobró en ese vuelo a las **19:46:51**, cinco
segundos después de abrirse `TaxiOut`, con los motores **apagados** y el avión **aparcado**: la fase se
abrió doce minutos antes de que rodara. El instrumento ya existe (`GroundMotion`): es cambiar
`TickDeferredTaxiOutLights` para que la gracia (o la evaluación) exija **movimiento real** y no solo el
tiempo transcurrido desde la entrada de fase.

**Ojo**: cambia puntuación (deja de cobrar donde el avión no rodaba), así que conviene un vuelo de
prueba antes de publicarlo.

## 4. Y entonces sí, el crédito proporcional

Con las cifras persistidas hay corpus para evaluar el punto 1 —**+5 × (proporción / 50)**, sin quitar
nada a quien hoy cobra— sobre los vuelos ya guardados, y decidir con la distribución delante en vez de
a ojo. Medido en este vuelo: **49 %**, es decir, el veredicto depende de un punto. Eso es lo que el
crédito proporcional arregla.

## Lo que NO hay que tocar sin datos

- **El umbral del 50 %**: este vuelo, bien medido, ronda el 49 %; bajarlo al 40 % taparía el problema en
  vez de resolverlo.
- **El rodaje con dos motores**: hay aeropuertos y SOP donde es lo correcto (cuestas, contaminación,
  cruces de pista, OAT alto). La bonificación no debe convertirse en un impuesto.
