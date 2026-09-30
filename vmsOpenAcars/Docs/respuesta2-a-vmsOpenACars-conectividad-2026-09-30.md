# NavData → vmsOpenACars: los tres descuadres eran míos, y están corregidos

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** `RESPUESTA2-NAVDATA-CONECTIVIDAD-2026-09-30.md`
> **Estado:** los tres descuadres corregidos y verificados. El corpus: formato aceptado, y la prueba
> de aceptación es lo siguiente que hacemos.

Los tres eran ciertos y los tres eran nuestros. Los hemos reproducido antes de tocar nada, y la
aritmética ahora cierra en los cuatro aeropuertos que habéis medido.

---

## 1. `gaps_published` no venía — era verdad

Estaba calculado y no se publicaba. Ya sale:

```
LMML   comps 5 → 2   published 3   considered 3    rejected 0
KMIA   comps 2 → 1   published 2   considered 13   rejected 11
LEMD   comps 3 → 2   published 3   considered 7    rejected 4
SKBO   comps 3 → 1   published 14  considered 101  rejected 87
```

## 2. Los contadores no cuadraban porque un hueco se contaba dos veces

Vuestro 13 contra 24 era exactamente esto: un mismo par puede verse desde el **camino de huecos**
(extremos sueltos) y desde el **de puentes** (componentes), y se registraba un rechazo por cada
camino, mientras `considered` contaba pares distintos. Ahora se deduplica por par y la aritmética
cierra por construcción:

```
considered = published + rejected + gaps_unaccounted
```

Y hay un test que lo fija, para que no vuelva a descuadrar sin que salte.

## 3. `crosses_runway` como motivo de rechazo: teníais razón, y la contradicción era mía

Lo quité del camino de **puentes** y lo dejé en el de **huecos**, así que mi mensaje era cierto a
medias. Corregido: **un cruce de pista se publica con su bandera en los dos caminos**, nunca se
rechaza. Verificado: `crosses_runway` ya **no aparece** en ningún `rejected[]` de los cuatro
aeropuertos.

Con eso, además, **KMIA pasa a 2 empalmes** (45,1 m conf 0,37 `T3|U` y 2,8 m conf 0,28 `L1|K1`) y
sigue en `components_after_joins: 1`.

## 4. `G|D` a 162,9°: lo miramos, y teníais razón en sospechar

Es lo que decís —dos nodos casi enfrentados a los lados de una franja—, y no lo descartamos porque
preferís el dato con su confianza a un listón silencioso. Lo que hemos hecho es que **el giro casi
frontal (más de 120°) baje más la confianza**: ese puente pasa de **0,38 a 0,27**. Sigue en vuestro
segundo nivel, ahora con menos margen. Si alguna vez veis uno que os parece imposible, decidlo con el
par de nodos y lo miramos con los ojos puestos.

## 5. §4.3 — El corpus: formato aceptado, y es lo siguiente

Aceptamos vuestro formato tal cual:

```
icao,runway,ruta_escrita,lat,lon
LMML,05,"T J K L",35.85055,14.48869
```

Con la **posición inicial** y la pista, que es lo que hace el caso comprobable —y tenéis razón en que
LMML no se entiende sin ella: 64 m hasta `F` y 262 m hasta `T`. La prueba de aceptación será: para
cada caso, la secuencia de calles escrita tiene que ser **trazable de punta a punta** sobre la red
**publicada** (segmentos + empalmes), sin heurística del cliente; y publicaremos el resultado ruta a
ruta. Mandadlo cuando lo tengáis y es lo primero que hacemos.

Y sí: **la prueba y la base de conocimiento se alimentan del mismo dato**, que es lo que hace que
esto no sea un test de laboratorio.

## 6. Vuestro §4, anotado

Nunca guiar una ruta distinta de la anunciada; enlazar hasta la primera calle escrita en vez de
sustituir la ruta entera; y la zona muerta de `FUERA DE RUTA` hasta haber estado en ruta una vez.
Las tres son vuestras y nos parecen las correctas; la tercera explica los 15 avisos enteros. Cuando
lo tengáis, el test de replay del vuelo nos vale como caso del corpus.
