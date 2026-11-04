# Novedades de vmsOpenACARS — versión 0.9.62

**En una frase:** el análisis del aterrizaje ya no te dice solo con qué viento tocaste, sino **cómo
fue el viento durante toda la final**: la cruzada máxima y la cola máxima, y a qué distancia del
umbral ocurrieron.

---

## Qué cambia

Hasta ahora el cliente guardaba el viento **en el instante del toque** (y el del METAR del destino).
Lo que pasaba entre 5 NM y el umbral —una racha, un giro de dirección, la cizalladura de los últimos
500 ft— se leía en cada muestra pero **no se guardaba en ningún sitio**.

Ahora cada punto de la traza de aproximación (uno cada 2 s por debajo de 3000 ft) lleva también
**dirección y velocidad del viento**, y el análisis muestra una línea nueva:

```
VIENTO EN LA FINAL · máx cruzada 14 kt @ 3,2 NM · máx cola 6 kt @ 1,8 NM
```

Con viento en calma pone `VIENTO EN LA FINAL · calma`. Las componentes se calculan contra el rumbo
real de la pista, con el mismo criterio que el viento del toque que ya conocías.

## Lo que hay que saber

- **Los vuelos anteriores no tienen este dato** y su análisis se ve igual que siempre: la línea solo
  aparece cuando hay viento registrado. No es un fallo.
- La traza vive en tu propio equipo (`landing_log.sqlite`); la base se actualiza sola al arrancar,
  sin que tengas que borrar ni reimportar nada.
- Nada que configurar.