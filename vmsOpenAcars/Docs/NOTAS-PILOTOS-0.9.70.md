# Novedades de vmsOpenACARS — versión 0.9.70

**En una frase:** la **G del aterrizaje ya se mide de verdad**, y dejan de aparecer spoilers
inexistentes y avisos de rodaje duplicados.

---

## La G del aterrizaje

Hasta ahora la G que se puntuaba **no se medía**: se deducía del régimen de descenso. Un toque de
−346 fpm «valía» 1,85 g por tabla, aunque el acelerómetro del avión marcaba 1,32 g. El efecto era que
un aterrizaje duro **se cobraba dos veces**: una por la tasa de descenso y otra por una G que en
realidad era la misma tasa con otro nombre.

Ahora se toma el **pico real del acelerómetro en el segundo que termina en el contacto**. Un solo
número, el mismo que ves en pantalla, el mismo que va al análisis y el mismo que puntúa. Si el avión
no publica el dato, el criterio **no se aplica** en vez de inventarse.

> Consecuencia honesta: **algunos vuelos antiguos habrían puntuado mejor** con esta regla. Las notas
> de vuelo ya guardadas no cambian; el cálculo nuevo se aplica de aquí en adelante.

## Los flaps

Había dos maneras distintas de traducir el mismo mando de flaps y se mostraban las dos: una daba
«grados» de un avión genérico y la otra el porcentaje del recorrido. De ahí los saltos raros que
viste en aproximación (0 → 40 → 25 → 40 en medio minuto) y que el registro dijera `FLAPS 100%`
mientras la línea de datos decía `40%`.

Ahora hay **una sola escala**: el porcentaje del recorrido del mando, igual para todos los aviones.
En un ATR, a tope, marca 100 %. No hay nada que ajustar.

## Los spoilers

El ATR **no tiene spoilers**, pero su indicador en el simulador publica un valor que oscila, y el
cliente lo interpretaba como despliegues y repliegues: hasta **seis pares** en un solo descenso.
Ahora los aviones sin spoilers están en una lista y esa lectura se ignora. Cualquier otro avión se
comporta exactamente como antes.

## Los avisos de rodaje

Cada aviso salía **dos veces** en el registro (`RAAS: APROXIMANDO PISTA` y, en la línea siguiente, el
mismo con su icono). Era duplicado en el registro, no en el audio: ahora se escribe una sola vez.

## Lo que hay que saber

- Nada que configurar: todo se aplica solo.
- Las puntuaciones de los vuelos **nuevos** pueden subir respecto a las de antes, sobre todo en
  aterrizajes firmes: es porque antes se cobraba la G dos veces.
- El resto del vuelo, los anuncios de cabina y los avisos no cambian.