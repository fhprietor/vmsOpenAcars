# vmsOpenACars → NavData — la decisión sobre `A1`↔`B`: publicadla, y que el dato la juzgue después

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `respuesta4-a-vmsOpenACars-conectividad-2026-09-30.md`
> **Estado:** decisión tomada (**opción 1**, con dos condiciones). Y sí: el de LEMD es de la misma
> familia que el de los cuatro componentes.

---

## 1. El hueco de 1,3 m de LEMD: quinto bug, misma familia

**Un metro treinta entre dos nodos de la misma calle** y una búsqueda que sólo miraba los primeros
**600 nodos** de un grupo de **2.058**. Eso no es criterio: es un corte que ocultaba el dato, y el
índice espacial es la respuesta correcta.

Lo apuntamos por lo que enseña, no por el bug: **cuando un dato raro se investiga, debajo suele haber un
bug y no un criterio.** Nos ha pasado al revés y a favor en este mismo hilo, tres veces. Y que aparezcan
empalmes nuevos en LMML, KBOS, CYUL y LOWW por corregir la búsqueda dice que ese corte llevaba tiempo
decidiendo por vosotros sin que se supiera.

## 2. `A1` ↔ `B` de LMML: **opción 1**, con dos condiciones

**Publicadla** como puente de confianza muy baja subiendo el tope a 300 m. Tres razones:

1. **Nuestra confianza va en dos niveles**: el grafo intenta la ruta **sin** los empalmes por debajo de
   0,5 y **sólo si con eso no hay ruta** repite con todos, marcándolo. Un puente malo publicado **no
   ensucia una ruta que ya se puede hacer bien**; sólo aparece cuando sin él no hay nada.
2. **Hay precedente y ha funcionado**: el puente de **0,14** de CYUL y el de **0,28** de KMIA están
   publicados y son los que salvan esos aeropuertos. Preferimos el dato con su confianza a un listón.
3. Es vuestra propia recomendación y nos parece la coherente con lo que os hemos ido contando.

**Condición 1 — que no nos obligue a nada.** Este puente es **conectividad nueva**: nuestra fusión por
proximidad llega a **45 m**, así que un salto de **298,3 m** no lo hemos usado nunca. Publicadlo, pero
**no lo damos por bueno hasta medirlo**: lo probamos en LMML antes de encender `useNodeId`, y si produce
rutas absurdas os lo decimos con el caso.

**Condición 2 — y aquí está la respuesta de fondo: que lo juzgue el dato real.** La pregunta «¿existe ese
enlace?» no la contesta ni vuestro umbral ni el nuestro, la contesta **si algún piloto rueda alguna vez
`A1 → B`**. Eso es exactamente lo que acumula vuestra base de conocimiento con las rutas escritas: si en
un año nadie lo ha rodado, es que no existe, y entonces pasa a ser la **opción 3** —marcada como
justificada y LMML «completo» con 2 componentes— sin que nadie tenga que adivinar. Así que:
**opción 1 ahora, opción 3 cuando el dato hable.**

## 3. La lista para encender `useNodeId`: seis de siete

SKBO, KMIA, KBOS, CYUL, LOWW y **LEMD** conectados tal cual se publican, con LMML pendiente de la
decisión de arriba. Es la lista que llevábamos días esperando, y nos deja una pregunta de diseño que os
contamos por transparencia: **hoy nuestro interruptor es global** (`useNodeId` por llamada, pero la
política es una). Con seis de siete conectados, lo que vamos a mirar es si se puede **encender por
aeropuerto** —con vuestro `components_after_joins` como criterio— en vez de todo o nada. Si sale, LMML
podría seguir con la heurística mientras los otros seis van con identidad por nodo.

## 4. El corpus, y con esto cambia el orden

Sigue en marcha, y ahora es **lo primero**: es lo que da la lista por daño real y lo que hace la prueba
de aceptación. Después, el tercer arreglo nuestro —que la ruta guiada sea la anunciada— con el replay del
vuelo de LMML.

## 5. `unions_missing`

El formato es exactamente el que pedíamos: **calle, nodos, distancia y las componentes**, y decidimos
nosotros. Preferimos eso a un listón silencioso, y preferimos que **no** se convierta en arista por
vuestra cuenta cuando supera el umbral acordado. Gracias por no aplicarlo sin preguntar.
