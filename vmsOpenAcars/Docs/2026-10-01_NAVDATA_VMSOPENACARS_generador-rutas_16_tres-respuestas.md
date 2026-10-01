# NavData → vmsOpenACars: las tres respuestas — y **sí, vuestro POST rebotaría**

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-10-01
> **Responde a:** `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_15_entregado-y-tres-preguntas.md`
> **Adjunto:** `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_16_metricas-con-cerrojo.csv`
> **Estado:** las tres preguntas contestadas. **No mandéis el `planned` todavía**: hoy lo rechazaría,
> y os decimos por qué y qué vamos a cambiar nosotros.

---

## 1. ¿La medición corrió con la puerta cerrada? **No**, y lo demuestro de dos formas

**(a) No podía.** `exclude_runway_crossing` **no existe** como parámetro del arnés de medición: no
está en `evaluar()` ni en el comando. El CSV que tenéis salió del camino por defecto, con la puerta
**abierta**. No es una afirmación de intención: es que la instrumentación para cerrarla no estaba.

**(b) Y cerrarla no cambia nada.** He vuelto a generar las 33 rutas **con la puerta abierta y con la
puerta cerrada**, una a una:

| | Resultado |
|---|---|
| Rutas comparadas (las que tienen pista derivable) | **37** |
| Idénticas con la puerta abierta y cerrada | **37** |
| Distintas | **0** |
| Empalmes que cruzan pista usados **con la puerta abierta** | **0** |

Así que el cero no es la puerta cerrada: es que **ninguna ruta de esta tanda necesita un cruce**. `A1|B`
y `G|D` no aparecen en ninguna de las 33, y solo 3 rutas usan algún empalme (`SKPE/08` → `A1`;
`MMGL/11R` → `B|A`; `LMML/05` → `I|F`, `J`).

**Y acepto la parte metodológica de vuestro aviso**: un cero tiene que decir **con qué puerta** se
midió. El CSV adjunto lleva ya una columna `exclude_runway_crossing` por fila, así que a partir de
ahora se lee en el propio fichero y no hay que creerse a nadie.

---

## 2. El 33 de 33, ahora **respaldado en el fichero**

Teníais razón: el número estaba en la carta y no en el CSV. Ya está implementado en el arnés y
publicado **por caso**:

| Columna | Qué lleva |
|---|---|
| `lock_armed` | `True`/`False` por ruta: ¿alguna muestra tiene la calle activa **en** el plan? |
| `active_streets_in_plan` | **qué** calles del plan se vieron (para poder revisar los casos límite) |

Resumen: **33 de 33** con `lock_armed: true`, 0 sin armar. La receta es la de #12 §2 y sigue con sus
dos limitaciones: el rumbo lo derivo del track entre muestras, y ahora sé —por vuestra §2 de #13— que
vuestro `NearestTaxiway` **también** resuelve por segmento, así que ahí no hay diferencia.

---

## 3. Vuestra pregunta 3: **sí, rebotaría.** No lo mandéis aún

Contrastado contra el validador de la ingesta, el cuerpo que describís
(`icao`, `runway`, `observed_at`, `client`, `planned{…}`) **se rechaza**, y por dos motivos exactos:

| Campo | Qué hace hoy el endpoint | Vuestro cuerpo |
|---|---|---|
| `icao` | obligatorio, 3–4 caracteres | lo traéis ✅ |
| `runway` | obligatorio | lo traéis ✅ |
| `observed_at` | obligatorio, ISO, ≤ 90 días y no futuro | lo traéis ✅ |
| **`stand`** | **obligatorio** → `missing_stand` | **no lo traéis** ❌ |
| **`route`** | **obligatorio, ≥ 2 calles** → `route_too_short` | **no lo traéis** ❌ |
| `entry`, `source`, `quality`, `client` | opcionales | ✅ |

Y **no vale meter la propuesta en `route`**: `route` es la ruta que el piloto **escribió**, y alimenta
las votaciones de la ruta acostumbrada y la métrica 3. Si el `planned` entrara por ahí, contaminaría
la base de conocimiento con propuestas del cliente como si fueran rutas escritas. Es exactamente la
separación que vosotros mismos pedisteis en #15 §1 («no es lo mismo») y la compartimos.

**Qué vamos a hacer nosotros** (es trabajo nuestro, no vuestro):

1. Aceptar en `POST /taxi-routes/observations` una observación **solo con `planned`**, sin `stand` ni
   `route`, guardándola en un almacén **propio** (`TaxiRoutePlanned`: `icao`, `runway`, `observed_at`,
   `text`, `polyline`, `dataset_version`, `node_ids?`, cliente) — **sin tocar** la tabla de
   observaciones ni las votaciones. Requiere modelo y migración en MariaDB.
2. Devolver en la respuesta de ingesta un `planned: {accepted, reason}` por ítem, para que sepáis qué
   pasó sin mirar logs.
3. **Avisaros aquí, con la forma exacta, cuando esté** — y entonces mandáis el primero.

Hasta entonces: si el piloto no escribe ruta, lo que hoy funciona sigue funcionando (vuestras
observaciones `typed`/`traced` con `route`), y el `planned` **no se manda**. Preferimos que os
rebote en un mensaje y no en el log de un vuelo real.

---

## 4. Y lo demás, aceptado

- **5 s en vez de 1 Hz**: nos parece la decisión correcta y la suscribimos — 30× el tráfico durante
  todo el vuelo guiado para una métrica no se sostiene, y con 5 s una traza de rodaje de 2.500 m pasa
  de 9–34 puntos a algo medible. Lo llamaremos «densa» y no «1 Hz» para no mentir en el nombre.
- **Los tres topes de los 30 s** (intervalo, suelo de 5 s y la deduplicación de ~33 m): gran
  hallazgo; explica que aflojar solo el primero no habría cambiado nada.
- **La confianza real en 0.9.22**: ya lo cerramos en #13 y vuestro matiz sigue en pie — degrada al
  segundo nivel, no excluye.

Fase 4: lo que falta es **nuestra re-medición** con la métrica reexpresada y vuestros datos densos
(que ya existen) más el **corpus escrito** creciendo solo. Y una cosa más que ya es nuestra: los 3
casos de ruta distinta y los 4 sin camino publicado, que son los que de verdad van a mover el número.
