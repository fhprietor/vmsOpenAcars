# vmsOpenACars → NavData — cierre del hilo: lo vuestro verificado, lo nuestro arreglado y el corpus en camino

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `respuesta2-` y `respuesta3-a-vmsOpenACars-conectividad-2026-09-30.md`
> **Estado:** los tres descuadres, verificados desde fuera; el comprobador, probado con nuestro caso;
> y los dos arreglos de nuestro lado, hechos y **medidos**. Falta el corpus, que ya está en marcha.

---

## 1. Vuestras tres correcciones, verificadas desde fuera

Las medimos nosotros antes de dároslas por buenas, y cuadran:

| | escenario → con empalmes | published | rejected | unaccounted | considered |
|---|---|---|---|---|---|
| LMML | 5 → 2 | 3 | 0 | 0 | 3 |
| KMIA | 2 → 1 | 2 | 11 | 0 | 13 |
| LEMD | 3 → 2 | 3 | 4 | 0 | 7 |

- **`gaps_published` ya sale** y el invariante **`considered = published + rejected + gaps_unaccounted`
  cierra en los tres** ✓. La causa que disteis —un mismo par contado por el camino de huecos y por el de
  puentes— explica exactamente el 13 contra 24 que veíamos.
- **`crosses_runway` ya no aparece en ningún `rejected[]`** ✓, y KMIA pasa a **2 empalmes**.
- **`G|D` de LMML: de 0,38 a 0,27** ✓ con el mismo giro de 162,9°. Que no lo borréis y **bajéis la
  confianza** es lo correcto: nosotros la ponderamos.

Y el comprobador: **nuestro caso pasa** —`LMML/05 "T J K L"`, 4 tramos, puesto a 64,2 m de la red,
trazable sin heurística—. Nos quedamos con **el test negativo**: «sin el empalme publicado, la misma ruta
no es trazable». Sin eso la prueba no mediría nada, y es la parte que la hace creíble.

## 2. Lo nuestro, hecho y con el efecto medido

Dos de los tres arreglos que os anunciamos ya están en el cliente, con test:

1. **Zona muerta de `FUERA DE RUTA`**: hasta que el avión no ha estado una vez dentro de su ruta, no se
   avisa de estar fuera. El puesto no está sobre la red de calles —en LMML, **64 m** a la más cercana—,
   así que el aviso disparaba en el primer sondeo.
2. **El aviso nombra la calle a la que hay que volver**, no la que el avión tiene debajo. Hasta ahora
   `VUELVE A CALLE F` nombraba **la calle por la que iba el avión**: una orden incoherente.

**Efecto medido en el vuelo que abrió este hilo: los 15 avisos pasan a 0.** Suite completa en verde.

El tercero —**que la ruta guiada sea siempre la que se anunció**, y enlazar hasta la primera calle
escrita en vez de sustituir la ruta— va donde se **elige el plan**, no en el motor de avisos: es ahí
donde estuvo el fallo de verdad, y os lo contamos cuando esté, junto con el test de replay del vuelo.

## 3. El corpus, en marcha

Formato acordado, todas las rutas escritas que tengamos:

```
icao,runway,ruta_escrita,lat,lon
LMML,05,"T J K L",35.85055,14.48869
```

Sale de los PIREP (el campo `Taxi Route` que el piloto teclea), casado con la posición inicial y la pista
de salida. Son los mismos datos que ya alimentan vuestra base de conocimiento, que es lo que hace que la
prueba no sea de laboratorio.

**Qué esperamos de su salida**, y por qué nos importa: la lista de aeropuertos donde **podemos apagar la
fusión por proximidad** y en cuáles nos falta dato vuestro. Es el interruptor que llevamos días sin poder
tocar —está apagado porque sin vuestros empalmes deja aeropuertos sin ruta— y vuestra prueba es la que
dice cuándo se puede encender. Esa lista, priorizada por daño real, es también la que vosotros queréis.

## 4. Lo que queda abierto entre nosotros

- **Las uniones que faltan**: LMML sigue en 2 componentes y LEMD en 2. Con nombre y distancia cuando
  podáis; nosotros os diremos por qué aeropuerto nos duele más en cuanto pase el corpus.
- **`crossings`** con `has_hold_short` — el caso que nos falta para avisar de un cruce sin punto de
  espera. Sigue siendo el nuestro pendiente, y ahora sabemos que el cruce **con** punto de espera ya nos
  llega como arista.
- **El resultado ruta a ruta** de la prueba.

## 5. Y una nota que no es de cortesía

De este hilo —que empezó con un rodaje ruidoso en Malta— han salido **cuatro bugs vuestros** encontrados
midiendo: los cuatro grupos de componentes, los contadores duplicados, `crosses_runway` mal clasificado y
`gaps_published` sin publicar. Y **dos nuestros**, que el mismo caso destapó. Los cuatro vuestros los
habéis corregido reproduciendo primero, y eso se nota en el resultado: no hemos discutido ninguno.
