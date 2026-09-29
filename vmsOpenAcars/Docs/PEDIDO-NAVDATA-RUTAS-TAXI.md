# Pedido al proyecto NavData — rutas de rodaje acostumbradas (base de conocimiento compartida)

> **De:** equipo de vmsOpenAcars (cliente ACARS, WinForms/.NET 4.8)
> **Para:** equipo del proyecto NavData
> **Estado del cliente al escribir esto:** v0.9.16
> **Origen:** el cliente ya propone una ruta de rodaje, pero la calcula **por geometría** (el camino
> más corto sobre `/taxiways/`). Un piloto de SKBO corrigió esa ruta y **la diferencia no se puede
> deducir de ningún dato que exista hoy**: ver §2. Este pedido es para que esa información la
> mantenga NavData y la use **toda la comunidad**, no cada cliente por su cuenta.

---

## 1. Lo que pedimos, en una frase

Una **base de conocimiento de rutas de rodaje por aeropuerto** (puesto → pista), alimentada con las
observaciones que enviamos los clientes y **agregada, validada y servida por NavData**, con dos
lecturas: la **ruta acostumbrada** (para proponerla) y las **estadísticas de uso por calle** (para
saber qué calles son de tránsito y cuáles no).

## 2. Por qué: el caso que lo demuestra (medido, no opinado)

En SKBO, del **puesto G74 al punto de espera A3 de la pista 14L**, esto es lo que da la geometría
sobre vuestros `/taxiways/` reales (237 segmentos de las calles que intervienen, calculado con el
código del cliente):

| Variante | Distancia | Ruta |
|---|---|---|
| El grafo, hoy | 1.869 m | `F E X A B5 A A3` |
| Sin `X` | 1.930 m | `F E M A B5 A A3` |
| Sin `B5` | 2.051 m | `F E M S A A3` |
| Sin `X` ni `B5` | 2.051 m | `F E M S A A3` |
| **Lo que se hace** (piloto de SKBO) | — | **`F E M A A3`** |

**Ninguna de las cuatro variantes produce la ruta real**, y las dos reglas que faltan no están en
ningún dataset:

- **`X` «no es ruta»**: es un conector válido y más corto que `M`, así que el Dijkstra lo prefiere.
- **A `B5` «no se entra para continuar»**: está perfectamente formada —sus dos extremos caen exactos
  sobre `A` y sobre un cruce `A`/`S`— y **ahorra metros**. Nada en la geometría dice que no se use.

Esto no se arregla afinando el optimizador: es **conocimiento operativo** que hoy solo tienen los
pilotos que ruedan allí. Además, el punto de espera «A3» **no se puede nombrar**: `/holdshort/` solo
publica `runway_name`, así que hoy se identifica por geometría (su extremo coincide al sexto decimal
con la calle `A3`).

## 3. Reparto de trabajo: qué ponemos nosotros y qué pedimos

- **Nosotros ponemos las observaciones.** El cliente ya sabe, de cada vuelo: de qué **puesto** salió
  (resuelto contra vuestros parkings), a qué **pista** entró, y la **secuencia de calles** que rodó.
  Y tiene una fuente mejor que la traza: **el texto de la ruta que el piloto escribe o edita** en el
  diálogo de rodaje, que es la autorización de ATC tal como la entendió, sin ruido de GPS.
- **Pedimos a NavData la agregación y el servicio**: guardar, contar, validar contra vuestro propio
  dataset vigente, moderar y publicar. Es exactamente lo que ya hacéis con los espacios aéreos.
- **El valor es de toda la comunidad**: «ruta de rodaje acostumbrada por puesto y pista» es un dato
  que sirve a cualquier consumidor de NavData (clientes ACARS, herramientas de cartas, add-ons), no
  solo a nosotros. Y mejora con cada piloto que vuela.

## 4. Endpoints que pedimos

### 4.1 Enviar observaciones (lo que alimenta la base)

```
POST /api/v1/taxi-routes/observations
```
Cuerpo (lote de 1..N, autenticado con `X-API-Key` + `X-Origin-Domain`, como el resto):

```json
{ "observations": [ {
    "icao": "SKBO",
    "stand":   { "name": "G", "number": 74, "suffix": "", "lat": 4.698871, "lon": -74.144562 },
    "runway":  "14L",
    "entry":   { "kind": "hold_short", "lat": 4.712803, "lon": -74.152382, "taxiway": "A3" },
    "route":   ["F","E","M","A","A3"],
    "source":  "typed",
    "aircraft_class": "H",
    "quality": { "took_off_from_runway": true, "off_route_episodes": 0, "reached_runway": true },
    "observed_at": "2026-09-29T14:03:11Z",
    "client": { "name": "vmsOpenAcars", "version": "0.9.16" }
} ] }
```

- **`source`**: `typed` (el piloto escribió/editó la ruta — **la buena**) o `traced` (deducida de la
  telemetría). Que la agregación pueda ponderarlas distinto es cosa vuestra; nosotros las etiquetamos.
- **Sin identidad del piloto.** Ni cuenta, ni aerolínea, ni matrícula: el dato es la ruta.
- **Sin traza cruda.** No mandamos posiciones: la ruta ya viene reducida a nombres de calle.
- Idempotencia por `(icao, stand, runway, route, observed_at)` para que un reintento no cuente doble.

### 4.2 Leer la ruta acostumbrada

```
GET /api/v1/airport/{icao}/taxi-routes/?stand=G74&runway=14L
```

```json
{ "icao": "SKBO", "stand": "G74", "runway": "14L",
  "customary": { "route": ["F","E","M","A","A3"], "entry_taxiway": "A3",
                 "support": 7, "total": 9, "confidence": 0.78, "updated_at": "..." },
  "alternatives": [ { "route": ["F","E","X","A","B5","A","A3"], "support": 2, "confidence": 0.22 } ],
  "source": "community" }
```

- **Sin datos suficientes**: `200` con `customary: null` (y `total`), **nunca** una ruta inventada ni
  un `503`. El cliente cae a su grafo y lo dice en pantalla.
- Que venga **`support`/`total`/`confidence`** es requisito, no adorno: el cliente lo enseña
  («ruta habitual, 7 de 9 vuelos») para que el piloto sepa **de dónde sale** lo que le proponemos.
- Opcional pero útil: `GET /api/v1/airport/{icao}/taxi-routes/` para precargar el aeropuerto de
  salida del OFP de una vez.

### 4.3 Estadísticas de uso por calle (el otro uso, y el que más nos sirve)

```
GET /api/v1/airport/{icao}/taxiway-stats/
```
Por calle: cuántas veces aparece **en rutas de paso** y cuántas solo de entrada y salida (un
**stub**, como `B5`). Con eso el cliente puede **ponderar su grafo**: una calle que nunca forma parte
de una ruta real deja de ser candidata aunque sea la más corta. Esto arregla la propuesta también en
los puestos donde nadie ha rodado todavía, que es donde la base está vacía.

### 4.4 Tres campos que hacen falta para que la clave sea limpia (aditivos, sin romper nada)

1. **`/holdshort/` → nombre de la calle** (`taxiway`, o `taxiways: []`): es el **nombre del punto de
   espera** («A3»). Hoy solo llega la pista, y sin esto la clave `entry` solo puede ser geométrica.
2. **`/taxiways/` → `node_id` en cada extremo** (o una tabla `nodes` + índices): elimina nuestra
   fusión por proximidad (45 m) y hace el grafo exacto. Hoy 46 de los 569 segmentos de SKBO miden
   menos que ese radio, así que la conectividad depende de un umbral que elegimos nosotros.
3. **Cruces de pista** (`crossings: [{ runway, taxiway, lat, lon }]`): el aviso que no tenemos.
   Cruzarse con una paralela por una calle sin hold-short hoy **no dispara nada**.

## 5. Agregación y mantenimiento (lo que pedimos que sea vuestro)

Propuesta, ajustable por vosotros:

- **Solo cuentan** las observaciones con `took_off_from_runway` y sin episodios de fuera de ruta.
- **Agrupar** por `(icao, stand, runway)`; devolver la ruta con más apoyo y su cuota.
- **Umbral** para publicar como acostumbrada (p. ej. `total ≥ 5` y `confidence ≥ 0.6`); por debajo,
  `customary: null` y las alternativas con sus cuentas.
- **Ventana temporal** y decaimiento a vuestro criterio: una ruta de hace tres años puede ya no ser
  la de hoy.
- **Validación contra vuestro dataset vigente, en la ingesta y en la lectura**: si una ruta nombra
  una calle que ya no existe, se marca y **no se sirve**. Es el freno que evita que la base se pudra
  cuando cambien las calles.
- **Moderación con criterio experto**: permitir que un colaborador de confianza (piloto local, ATC)
  **fije** la ruta autoritativa de un aeropuerto. `B5` «no se entra para continuar» es juicio
  operativo: ninguna votación lo descubre el primer día, y el caso de §2 es exactamente eso.
- **Retención**: lo que decidáis; nosotros solo pedimos que la respuesta diga `updated_at`.

## 6. Privacidad y consentimiento

El dato sale del vuelo del piloto. Nuestra parte: mandamos la ruta **ya reducida a nombres de
calle**, sin identidad y sin traza, y el envío será **desactivable** en el cliente (ajuste visible,
por defecto activado, documentado en el briefing del piloto). Si preferís que el defecto sea lo
contrario, decidlo y lo cambiamos.

## 7. Lo que implementaremos en el cliente (contrato)

1. **Enviar** las observaciones al terminar el rodaje de salida (y al filear el PIREP, si falló
   antes). Si el envío falla, se descarta sin molestar al piloto; no se reintenta eternamente.
2. **Leer** antes de calcular: si hay acostumbrada con confianza suficiente, se propone **esa**;
   si no, el grafo como hoy. Nunca se sustituye en silencio: el diálogo dirá de dónde sale.
3. **Ponderar** el grafo con `taxiway-stats` (calles de paso frente a stubs).
4. **Caché local de las respuestas** (TTL un día, por aeropuerto) — es una caché de la API, **no** una
   base de conocimiento local: la verdad está en NavData.
5. **Degradar sin ruido**: si el endpoint no responde, el rodaje sigue funcionando con el grafo (y el
   RAAS no se entera).
6. **Fixture de test**: el cliente mantiene un CSV con los taxiways reales de SKBO **solo** para el
   test que fija el caso de §2 sin salir a la red. No es fuente de datos ni se distribuye.

## 8. Criterios de aceptación — cómo lo verificaremos

1. `GET` del caso de §2 (`SKBO`, puesto `G74`, pista `14L`) devuelve **`F E M A A3`** con `support`
   y `confidence` coherentes, después de enviar observaciones de esa ruta.
2. Un par sin datos devuelve `200` con `customary: null` — **nunca 503 ni una ruta inventada**.
3. Una observación con una calle inexistente se **rechaza o se marca**, y no llega a servirse.
4. `taxiway-stats` de SKBO identifica `B5` como calle de entrada/salida (stub) y no de paso.
5. El envío es idempotente: dos veces el mismo lote no duplica el apoyo.
6. Los campos aditivos de §4.4 no rompen a los clientes actuales (nosotros comprobamos que siguen
   parseando sin cambios).

## 9. Lo que NO pedimos

- No pedimos que construyáis nuestra interfaz ni nuestros avisos: eso es del cliente.
- No pedimos una autorización de ATC en vivo ni tráfico: esto es **conocimiento acumulado**, no un
  servicio de tiempo real.
- No pedimos trazas de posición ni datos por piloto.
- No pedimos que inventéis la ruta de un aeropuerto sin observaciones: con `customary: null` nos
  basta y el grafo cubre el hueco.

## 10. Preguntas que necesitamos respondidas

1. ¿Os encaja el modelo de observaciones y agregación, o preferís que enviemos otra cosa (por
   ejemplo, la ruta ya agrupada por lote, o solo la traza reducida)?
2. ¿Límites de tasa y tamaño de lote que debemos respetar? ¿Cada cuánto conviene enviar?
3. ¿Ventana temporal y umbrales que consideráis razonables para publicar como «acostumbrada»?
4. ¿Queréis la **moderación experta** (ruta fijada por un colaborador local) desde el principio, o
   preferís arrancar solo con la votación?
5. De §4.4, ¿cuál de los tres veis factible y en qué orden? El nombre de la calle en el hold-short es
   el que más nos desbloquea, y el `node_id` el que más exactitud aporta.
6. ¿Queréis que la observación incluya la **clase de aeronave**? En algunos aeropuertos los pesados
   ruedan por otras calles, y eso cambia qué es «acostumbrada» según quién pregunte.

---

### Anexo — de dónde salen los números de §2

- `/airport/SKBO/taxiways/`, `/parkings/`, `/holdshort/` y `/runways/` consultados en vivo el
  **29/09/2026** (569 segmentos, 101 parkings, 35 hold-shorts, 4 pistas).
- Las cuatro rutas y sus distancias están calculadas con el **mismo código que usa el cliente**, sobre
  los 237 segmentos de las calles que intervienen.
- El caso está fijado como test en `vmsOpenAcars.Tests/TaxiRouteCaseTests.cs`, con los 237 segmentos
  reales en `Fixtures/SKBO-taxi-2026-09-29.csv` (precisión completa: este nudo del apron se decide al
  centímetro entre rutas casi empatadas).
- Las dos reglas (`X` no es ruta; a `B5` no se entra para continuar) las aportó el **mantenedor del
  cliente**, que vuela SKBO.
