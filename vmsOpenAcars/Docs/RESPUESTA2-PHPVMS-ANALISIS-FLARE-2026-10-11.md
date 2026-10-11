# Respuesta (2) al equipo de phpVMS — rellenos, METAR y aviso de publicación

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra verificación de los dos aterrizajes
> **Estado:** vuestras tres peticiones contestadas **y la serie histórica ya enviada** —30 aterrizajes
> dentro y verificados, todos marcados como relleno—. De ella salen dos avisos que os contamos en el
> punto 3, uno de ellos sobre un campo vuestro que querríamos entender.

---

## 1. La cadena exacta de `source_name` para los rellenos

```text
vmsOpenACars/0.9.37-relleno
```

Fija y sin variantes. Nuestro lado la compone así: `vmsOpenACars/` + la versión informativa del
cliente + `-relleno`, y hay una prueba que **falla si alguien la cambia** sin tocar la constante
(`LandingUploadPayload.BackfillTag`), para que no se nos escape en un refactor. Documentadla con
esa grafía, que es la que va a aparecer.

**Y una cosa que ya hemos hecho, para que no tengáis que migrar nada:** los **dos aterrizajes que ya
os habíamos mandado iban sin la marca** —los subimos antes de que nos la pidierais—, así que **los
hemos vuelto a enviar** con el `source_name` corregido. Vuestra ingesta es un UPSERT por `pirep_id` y
el cuerpo va completo, así que el reemplazo es total y limpio: al releerlos con vuestro `GET`,
`source_name` ya es `vmsOpenACars/0.9.37-relleno` y las **97** y **81** muestras siguen ahí. Si
preferís que los dos primeros vuelos cuenten como en vivo y no como relleno, decidlo y los
revertimos: es un reenvío más.

## 2. Aviso de publicación de la 0.9.37

**Os avisamos en cuanto la publiquemos**, que es cuando empieza el volumen real. Estado de hoy: la
versión está **sin publicar** y el interruptor (`pirep_landing_analysis_enabled`) ya va **encendido
por defecto** en la plantilla que se distribuye, así que el primer vuelo de cada piloto con la
versión nueva sube solo. Serán ~1 aterrizaje por piloto y día de vuelo, con el mismo contrato que
habéis validado.

## 3. La serie histórica: **enviada** (30 aterrizajes)

Vuestra guarda para no reanalizar un aterrizaje sin traza es lo que nos faltaba para decidirlo: sin
eso, rellenar decenas de vuelos habría sido pagar llamadas de IA para repetir lo que ya teníais del
log ACARS. **Ya está dentro**, así que podéis mirar las líneas base mientras entran.

| | |
|---|---|
| Aterrizajes enviados | **30**, todos `200 · Landing stored`, todos con `count = 0` (sin traza) |
| `source_name` | `vmsOpenACars/0.9.37-relleno` en los 30 |
| Verificación | releídos con vuestro `GET`: **0 campos divergentes**, 30/30 con 0 muestras |
| `trace` | `[]` en los 30, y `flare_capture_armed: false`. **No inventamos muestras** |

**Cómo se emparejó cada vuelo, porque aquí estaba el riesgo.** Nuestro log local **no guarda el
`pirep_id`**, así que había que casar 44 vuelos con vuestros PIREPs. La regla, para que la podáis
auditar:

- **Número de vuelo + origen + destino**, y entre los candidatos el `submitted_at` más cercano a la
  hora de fileo que guardamos en local.
- **Si el desfase pasa de 400 s, exigimos además que la tasa de descenso coincida exactamente.** Si
  no, no se manda. Así entraron dos vuelos con 31 y 37 min de retraso en el fileo pero con la tasa
  idéntica.
- **Los 14 vuelos del log que se quedan fuera, y por qué:**
  - **2** casaban solo con un PIREP a **días** de distancia y con otra tasa (`+864.968 s` y
    `+6.212.786 s`): son PIREPs de **otro vuelo con el mismo número y ruta** —una rotación
    repetida—. **No los mandamos**: preferimos 30 correctos a 32 con dos contaminados.
  - **10** no tienen PIREP en vuestra API que les corresponda (vuelos de mayo y junio, y el
    `SKCG→KBOS` del 22-23/09 que ya sabíamos que no aparece). Si queréis que los busquemos, decidlo.
  - Los **2** que ya teníais con traza (vuelos 43 y 44) no se repiten aquí.

### Dos cosas que la serie ha destapado

**1. Una tasa de descenso en positivo, nuestra, ya corregida.** Encontramos que **3 de los 41 vuelos
del log con dato (7%)** tienen la tasa capturada en **positivo** —`+156` y `+34` fpm entre ellos, los
tres con el toque a 0 ft y sin captura de flare—. Una tasa positiva en el toque es un artefacto de
nuestro detector de contacto, no una subida. Nuestro scoring no se equivoca (usa `Math.Abs`, así que
`+156` puntúa como `−156`: esos vuelos dan 95 y 100), pero **vuestra mediana sí**, porque se calcula
sobre el valor con signo: un positivo se va al otro lado del cero y la arrastra. **Lo normalizamos en
el borde** —se publica siempre en negativo, sin perder la magnitud, que es el dato— y **reenviamos los
dos vuelos afectados**, así que en vuestra tabla ya están en `−156` y `−34`. Misma marca de relleno.

**2. Vuestra `pireps.landing_rate` no siempre coincide con lo que capturamos, y queremos saber por
qué.** De los 30: **20 coinciden exactamente** y **10 difieren**. Ocho entre **1 y 9 fpm** (redondeo,
probablemente), y **dos con el signo cambiado**: el vuelo con `+156` capturado lo teníais como
`−155`, y el de `+34` como `−26`.

| Vuelo | Nuestra captura | Vuestro `pireps.landing_rate` |
|---|---|---|
| 5 | −426 | −425 |
| 9 | −455 | −463 |
| 18 | −240 | −249 |
| 20 | +156 | **−155** |
| 23 | +34 | **−26** |

**¿De dónde sale ese campo en vuestro lado?** Lo preguntamos porque, si lo usáis para las líneas base
y viene de una ruta distinta a la del log ACARS, conviene saberlo antes de que alguien compare dos
medianas que no miden lo mismo. Nosotros no tocamos el `pireps.landing_rate` —ni lo tocábamos antes—:
lo que subimos va solo a `pirep_landings`.


## 4. El METAR: ahora tenemos el número, y era pequeño

Vuestra pregunta de la ronda anterior («avisad si puede pasar de 400») **ya la podemos contestar con
datos**, medida sobre los 44 vuelos del log:

| | Caracteres |
|---|---|
| El más largo (vuelo 35, `SKPE`, con `RMK TCU/E`) | **81** |
| El más largo del destino de un aterrizaje analizado (vuelo 44) | 78 |
| Media de los 44 | 14 |
| **Cuántos pasaban de 400** | **0** |
| Cuántos pasan de 500 | 0 |

Así que vuestro tope de 400 ya nos sobraba y el de **500 lo hemos adoptado en cliente** en el mismo
cambio: un METAR de 520 se recorta a 500 y el resto del cuerpo viaja entero. Gracias por subirlo
igual —un METAR de EE.UU. con `RMK` largo puede pasar de 200, y no queríamos que el día que pasara de
400 nos costara **la traza entera** por vuestra validación todo-o-nada—.

## 5. Sobre vuestra verificación: es la que valía

Lo que habéis hecho con el `ts_utc` es lo que nosotros no podíamos hacer: cruzarlo contra **vuestro
log ACARS**, que se escribió **en vivo y por otro camino**. Un desfase de **1,3 s** en los dos vuelos
es exactamente el que tiene que haber (la traza se cierra 2 s después del toque y la línea se escribe
al terminar de leerla), así que el arreglo queda cerrado contra una fuente independiente y no contra
nuestra palabra. Eso es más de lo que pedíamos.

Y el cruce de métricas del punto 3 vuestro —cabeceo, corte y flaps contra lo que el cliente escribió
aquel día— es la misma clase de prueba: dos caminos que producen el mismo número. Que la IA del vuelo
44 dijera «flare iniciado a **40 ft** con **682 ft** de flotación», que salen de nuestra traza, cierra
el círculo de las fases 1 a 4 con datos reales.

Lo único que sentimos es no haber medido el METAR antes de preguntaros: teníamos el dato delante y lo
tratamos como desconocido. Queda aprendido: **antes de preguntar por un tope, medirlo en el log**.

Quedamos a vuestro aviso para la serie histórica.
