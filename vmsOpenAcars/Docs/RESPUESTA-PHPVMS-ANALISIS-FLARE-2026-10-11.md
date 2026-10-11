# Respuesta al equipo de phpVMS — ingesta del análisis del aterrizaje

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/10/2026
> **Respecto a:** vuestra respuesta a `PEDIDO-PHPVMS-ANALISIS-FLARE.md`
> **Estado:** **implementado y probado contra dos aterrizajes reales.** Los dos vuelos con traza de
> flare que quedaban sin subir están dentro y verificados campo por campo contra vuestra lectura.
> Lo que falta ya no es nuestro: que lo miréis con vuestros ojos.

---

## 1. Vuestras seis preguntas, contestadas contra el código

**1. Sí, `landing_rate_fpm`, `score` y `flaps_index` son enteros.** El primero y el segundo son `int`
en nuestro registro del vuelo; el tercero es `int?`. **Un aviso sobre `landing_rate_fpm`:** cuando el
vuelo termina **sin capturar el toque**, nuestro registro guarda el centinela **`-1`**
(`ScoringService.NoLandingData`). Ese `-1` **no viaja**: en el payload va **`null`**, porque en
vuestra agregación entraría como una tasa real y desplazaría la mediana hacia arriba. Mismo criterio
que ya aplicamos al `landing_rate` del PIREP.

**2. `landing_metar`: no lo truncamos, y no podemos daros el máximo real desde aquí.** Es el texto
`raw` que publica nuestro proveedor de METAR, solo con `Trim()`; no hay recorte en cliente. Los
típicos son 50–120 caracteres, y los METAR con bloque `RMK` (EEUU) se van por encima de 200. **No
podemos medirlo contra nuestros vuelos porque el `landing_log.sqlite` es local y no viaja por git**,
y estos días trabajamos en el portátil, que no tiene el historial. **Subidlo a 500** y nos quitamos el
riesgo; nosotros, por nuestra parte, **recortamos en cliente a vuestro tope actual (400)** para que un
METAR largo no nos tumbe el envío entero. Si lo subís, avisad y subimos el recorte en el mismo sitio.

**3. La traza no puede pasar de 2000 muestras: nuestro tope es 400.** La captura se arma a 1 500 ft
del umbral (o 300 ft AGL como respaldo), se cierra 2 s después del toque y **corta a 400 muestras**;
un vuelo normal deja 60–100 (nuestro récord son 97). Vuestros 346 son coherentes. **Sin riesgo.**

**4. El PIREP real: enviados dos, y verificados contra vuestra lectura.** No esperamos a un vuelo
nuevo: subimos los **dos aterrizajes que ya teníamos capturados** con traza de flare y que no estaban
en vuestra tabla —los últimos del log local, anteriores a que existiera la ingesta—.

| | Vuelo 43 | Vuelo 44 |
|---|---|---|
| `pirep_id` | `Me7BMgzvAwylvDan` | `kP9PDeXLPrNlY9me` |
| Ruta / pista | SKCL → SKCC, 16 | SKCC → SKBG, 17 |
| Muestras enviadas | **97** | **81** |
| Respuesta | `200 · Landing stored · count 97 · trace_samples 97` | `200 · Landing stored · count 81 · trace_samples 81` |
| Recuento tras releerlo | **97** | **81** |
| Campos divergentes al releer | **ninguno** | **ninguno** |

La comprobación no fue «respondió 200»: se volvió a leer con vuestro `GET /api/pireps/{id}/landing` y
se comparó **campo a campo y muestra a muestra** contra lo enviado. Cero divergencias más allá del
redondeo a dos decimales de vuestras columnas `float`, y los huecos siguen siendo huecos —**194** y
**162** valores `null` en las trazas, conservados; `n2_*_pct`, `radar_alt_ft` y `wind_gust_kt` incluidos—.

**Y os tenemos que contar un fallo nuestro que destapó justo esto.** Para rellenar esos dos vuelos hay
que releer la traza del log local, y ahí apareció que **nuestro lector devolvía la marca de tiempo en
hora local** mientras la propiedad se llama `TimestampUtc`, y el cuerpo la escribía como si ya fuera
UTC: cada muestra salía **5 horas antes** (el huso de Colombia). El camino en vivo **nunca estuvo
afectado** —recibe las marcas en UTC directamente del simulador—, y por eso no lo habíamos visto: es el
primer camino que lee la traza de la base en vez de recibirla en vuelo. Está corregido en los dos
sitios (el lector y el compositor del cuerpo) **con una prueba que ahora fija el caso**, y lo que hay
en vuestra tabla es la versión corregida: `2026-10-09T11:26:39.733Z` para la primera muestra del vuelo
44, que es exactamente la hora de captura. Si algún día veis un `ts_utc` de un vuelo nuestro que no
cuadra con su hora de salida, decidlo: sería esto.

Una nota honesta sobre el **vuelo 43**: es **anterior** al arreglo del tramo de frenada del que os
hablamos, así que sus **16 muestras en tierra llevan `dist_ft = 0`**. Va tal cual se capturó, sin
retocar: es lo que el cliente midió ese día, y preferimos que el replay enseñe eso a inventarle una
posición. Sus **métricas están perfectas** (la tasa, el toque y el eje no dependen de eso).

**5. Sí: los campos sin dato llegan como `null`, y los escribimos explícitamente.** No usamos
`NullValueHandling.Ignore`, así que el `null` **está en el cuerpo**, no ausente; y hay una prueba que
se pone roja si alguien lo cambia. **Dos excepciones que os declaramos, para que no sorprendan:**
`dist_ft` y `on_ground` son los dos únicos campos de la muestra **no anulables** en nuestro modelo, y
viajan siempre con valor. Y una más que sí os importa: **un `0` nunca significa «sin dato»**. Para
conseguirlo traducimos en cliente tres ceros que en nuestro modelo local sí son huecos —el `GForce`,
la desviación de eje y el centinela de la tasa—; **`touchdown_dist_ft` no entra en esa lista**,
porque tocar justo en el umbral es un `0` de verdad y ahí el 0 es una medida.

**6. El filtro por aeropuerto de salida: no hace falta por ahora.** Con llegada, pista y tipo cubrimos
el caso que queríamos. Si más adelante alguien necesita comparar salidas, lo pedimos.

---

## 2. Lo que ya está hecho en el cliente, y cómo se comporta

- **Un solo POST por vuelo**, **después** de filear y **en segundo plano** (`POST
  /api/pireps/{id}/landing`). Nunca mientras se vuela, así que no hay streaming ni carga durante el
  vuelo.
- **Si el endpoint falla, el vuelo se filea igual.** Un `400`/`401`/`403`/`404` deja el análisis
  **solo en el log local** del piloto y anota el motivo en pantalla. Esto es un extra del análisis,
  no un requisito para acreditar el vuelo, y está escrito así en el código.
- **Reemplazo total respetado:** siempre mandamos **el análisis completo** en un único envío. Nuestro
  constructor del payload no sabe hacer envíos parciales, así que no puede dejaros campos a `null`
  por accidente.
- **Las convenciones, tal cual las pedisteis:** `dist_ft` negativo pasado el umbral y **sin tocar el
  signo**; distancias en **pies crudos**, sin conversión interna; `touchdown_dist_ft` y
  `centerline_dev_ft` **no se recalculan** (son los que ya puntúan el PIREP); `flaps_label` conserva
  el `≈` cuando sale de traducir el porcentaje del mando y va **sin** `≈` cuando sale del detente real
  (`flaps_index`).
- **`schema_version` = 1** en todo lo que enviemos hasta que cambiemos el payload.

## 3. Dos detalles que os tocan a vosotros

1. **Los topes de longitud los respetamos recortando en cliente**, porque vuestra validación es todo
   o nada y un METAR de 420 caracteres no nos costaría el METAR: nos costaría **la traza entera del
   vuelo**. Preferimos perder los últimos caracteres de un texto informativo. Si subís los topes,
   subimos el recorte.
2. **Vuestra `count`: ya no hace falta que la confirméis, la hemos medido.** En los dos envíos
   `count` fue **97** y **81**, exactamente las muestras que mandamos, y coincidió con
   `attrs.trace_samples`. Así que la tratamos como «muestras guardadas» y **la usaremos como
   comprobación automática en cada envío**: si algún día `count` no cuadra con nuestro recuento, es
   que se ha perdido algo por el camino y lo veremos en el log del piloto.

---

## 4. Lo que viene después, de nuestro lado

- **La 0.9.37 se publica** con la subida activada por defecto, así que a partir de ahí **todos los
  aterrizajes de nuestros pilotos llegan solos**, sin rellenos manuales. El interruptor ya está
  encendido en nuestro `.config` de desarrollo.
- **Los vuelos anteriores seguían sin subir**: ahora mismo solo hemos rellenado los dos que tenían
  traza de flare. Si os interesa la serie histórica completa (métricas sin traza, que es lo que hay
  en los vuelos anteriores a la 0.9.32), decidlo y la subimos: son ~40 vuelos por piloto y el cuerpo
  es pequeño, pero **no lo hacemos por iniciativa propia** porque son datos que ya disteis por
  cerrados en vuestros PIREPs.
- **Dos preguntas que nos quedan, sin prisa:** si preferís que esos rellenos lleven un
  `source_name` distinto para poder distinguirlos de los envíos en vivo, y si el tope de 400
  caracteres del METAR lo subís o lo dejamos como está.

Y una nota de agradecimiento con fundamento: habéis hecho las **fases 3 y 4** (IA y web) sin que las
pidiéramos, y eso cambia el valor del dato —la traza deja de ser un PNG que un piloto comparte a mano
y pasa a estar en la página del PIREP—. Nuestro lado era el más pequeño de los dos.
