# vmsOpenACars → NavData — el corpus, y por qué hoy trae una sola ruta

> **De:** equipo de vmsOpenACars · **Para:** equipo de NavData · **Fecha:** 30/09/2026
> **Responde a:** `respuesta5-a-vmsOpenACars-conectividad-2026-09-30.md`
> **Adjunto:** `rutas-escritas-2026-09-30.csv` (`icao,runway,ruta_escrita,lat,lon`)

---

## 1. El corpus, con una sola ruta — y el motivo es nuestro

```
LMML,05,"T J K L",35.85055,14.48869
```

Es el vuelo que abrió todo este hilo. **No hay más, y no es un error de formato**: hemos consultado
los **37 vuelos** de nuestro corpus de rodaje (el del 29/09) uno a uno contra `/api/pireps/{id}` y
**todos tienen el campo `Taxi Route` vacío**, con la versión 0.9.16 ya en vuelo.

**La causa no es la falta de uso del popup, y por eso conviene leerlo despacio**: lo comprobamos
mirando el log de esos vuelos. El del 29/09 en KMIA (`Z8pORZd86Zr68OL1`) **sí confirmó el popup** —
su log tiene `🎙️ GUÍA DE RODAJE: PISTA 08R VÍA 26 24 Q Q8 T 20 T P S P 16 13 Y1 W Y1 HH JJ M Z M3 M M2 N P Q Q1 M1`
— y sin embargo su `Taxi Route` está vacío. Ese vuelo se hizo con **0.9.16**; el de LMML, que sí lo
tiene, con **0.9.18**. Es decir: **el campo sólo se guarda desde la versión en la que se arregló el
envío**, y los 37 vuelos del corpus de rodaje son anteriores.

Así que no hay nada que arreglar: **la base empezará a llenarse sola** en cuanto los pilotos vuelen con
0.9.18 o posterior. Lo que sí hace falta es que **usen el popup** —y ahí entra lo nuestro: la ruta que
el grafo sugiere en ese vuelo de KMIA eran **28 calles** contra las 6 que resultan con identidad por
nodo, así que una propuesta así no ayuda a que nadie la acepte—.
El único vuelo que lo tiene es el que él escribió a mano.

Y encaja con lo que nos contasteis vosotros: **«0 de 0» no es «nadie lo rueda»**, y «0 de 37» tampoco.
Es el mismo hecho medido desde los dos extremos: la base no puede crecer con lo que enviamos hoy.
La pregunta que dejasteis abierta sobre `A1 → B` **todavía no se puede contestar**, y la razón está
en nuestro lado, no en vuestro umbral.

**Lo que vamos a arreglar por nuestra parte**: enviar la ruta que el piloto **realmente usa**, también
cuando acepta la sugerida y aunque el popup no se abra. Es la precondición para que vuestra base tenga
observaciones —y por tanto para `customary`, para vuestra prueba y para que nosotros podamos decidir
`useNodeId` con datos—. Cuando esté, el corpus crecerá solo.

## 2. `useNodeId`: verificado por nuestra parte, y desbloqueado

Medido desde fuera, los siete tal como se publican:

| | componentes → con empalmes | empalmes | sin contabilizar |
|---|---|---|---|
| SKBO · KMIA · KBOS · CYUL · LOWW · LEMD · **LMML** | 1–5 → **1** | 2–14 | **0** |

**Siete de siete.** Con eso desaparece la razón por la que nuestro interruptor estaba apagado («sin
empalmes publicados deja aeropuertos sin ruta») y nuestra propia medición dice que encenderlo gana:
cobertura y recall empatados (31/31; 0,494 contra 0,488) y **precisión 0,615 contra 0,560**. Lo
encenderemos con el resultado de vuestra prueba delante.

## 3. El puente de `A1`↔`B` y el de LEMD

Del puente nuevo de LMML nos convence el patrón: **5,3° de giro y cruza una pista**, lo mismo que los
cruces que ya publicabais y que nosotros usamos (`J`, `L1|K1`). Un enlace inventado tendría un giro
grande y ninguna pista de por medio. Con **0,30** sólo entra en nuestro segundo nivel, así que no puede
degradar una ruta que ya se pueda hacer bien.

Y el de LEMD —**1,3 m** entre dos nodos de la misma calle, con la búsqueda limitada a 600 nodos— es el
quinto bug que este hilo os ha encontrado. Que aparecieran empalmes nuevos en LMML, KBOS, CYUL y LOWW
al corregir esa búsqueda dice cuánto tiempo llevaba decidiendo por vosotros sin que se supiera. **Es la
misma lección que el token de caché que no estaba en la clave**: el fallo no se veía porque el síntoma
aparecía lejos de la causa.

## 4. Lo que queda, por orden

1. **Nuestro**: enviar la ruta realmente usada (arriba) y la elección del plan de rodaje —que la ruta
   guiada sea **siempre** la anunciada—, con el replay de este vuelo como caso.
2. **Vuestro**: el resultado de la prueba ruta a ruta y, con volumen, el recuento del tránsito
   `A1 → B` para cerrar esa decisión con el dato delante.
3. **Común**: cuando el corpus tenga rutas de verdad, la lista de aeropuertos donde encender
   `useNodeId` sin red de seguridad, que es la métrica que acordamos.

---

## Corrección (30/09/2026) — el plan de rodaje **siempre** fue el del piloto

Escribimos en §4 que el cliente «anunciaba una ruta y guiaba otra». **Es falso, y lo hemos comprobado
en nuestro propio código**: `StartTaxiGuidance` construye **un solo** plan con el texto del piloto
(`_raasPlan = TaxiRoutePlan.Parse(routeText)`), lo anuncia con ese mismo texto y **es ese mismo objeto**
el que recibe la guía (`ResolveGuidance(..., _raasPlan, ...)`). No hay ninguna sustitución por la ruta
del grafo.

El fallo real, medido sobre la traza de LMML: nuestro **resolutor de calle activa** penaliza ×2,5 los
segmentos a más de 50° del rumbo y devolvía **F** y luego **I** —calles por las que el avión **no**
rodaba—. Con el avión en el puesto, F está a **64 m** y la primera calle de la ruta escrita, `T`, a
**262 m**; como 64 × 2,5 = 160 < 262, **F gana con cualquier rumbo**. La guía creía entonces que el
avión estaba fuera de **su** ruta cuando todavía no había llegado a ella.

El arreglo va **en el resolutor** (no nombrar calle cuando el segmento más cercano está a más de ~45 m,
que es el `SnapM` del grafo), no en la elección del plan. Y el plan de esta corrección es nuestro, no
vuestro: ninguno de los dos empalmes ni umbrales de esta conversación habría cambiado nada aquí.
