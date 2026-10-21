# Novedades de vmsOpenACARS — versión 0.9.49

**En una frase:** en X-Plane el ACARS ya sabe **qué avión** llevas —lee el tipo ICAO del propio fichero
del avión—, así que un A320neo deja de figurar como A320 y deja de avisarte de que no coincide con tu
plan de vuelo.

---

## Qué cambia

- **El tipo de aeronave sale del avión, no del nombre de la carpeta.** X-Plane no publica el tipo por
  XUIPC; lo único que llegaba era el nombre de la carpeta (`ToLissA320_V1p2p1`), y de ahí se deducía
  `A320` aunque el avión sea un **A20N**. Ahora se lee del fichero del avión y el registro dice la
  verdad: `📋 ICAO: A20N  (del .acf del avión)`.
- **Se acabó el «AIRCRAFT MISMATCH» contra tu propio avión.** El aviso de discrepancia al pulsar START
  comparaba ese `A320` deducido con el `A20N` de tu OFP y te pedía confirmar que querías volar tu avión.
- **Si el fichero no se puede leer, el cliente lo dice y no acusa.** El registro marca
  `(deducido del título: el simulador no publica el tipo)` y la comprobación se queda en la **familia**:
  no puede distinguir un CEO de un NEO, así que no te avisa de ninguno de los dos.
- **La familia del A320 agrupa al CEO y al NEO.** Los códigos no se parecen (`A320` empieza por `A32` y
  `A20N` por `A20`), y esa era la otra mitad del problema: ahora `A319/A320/A321` y sus NEO son la misma
  familia cuando solo se puede comparar la familia.

## Lo que hay que saber

- **En MSFS no cambia nada.** Allí el simulador sí publica el tipo (`B738`, `B77L`…) y todo sigue igual.
- **No hay nada que configurar.** Se lee una vez, al conectar con el simulador.
- **Solo se lee el avión que tienes cargado**: la carpeta que el simulador declara, dentro de
  `<X-Plane>\Aircraft\`. El ACARS **no** recorre tu carpeta de aviones ni la de community.
- **El aviso de verdad sigue ahí.** Si tu plan es de un A320 (CEO) y vuelas el neo —o al revés—, te lo
  sigue diciendo: son aviones distintos.
- **Si cambias de avión, reinicia el ACARS**: el tipo se lee al conectar, igual que antes.
- El registro de aterrizaje guarda el tipo y **de dónde salió**; los vuelos ya guardados no cambian.
