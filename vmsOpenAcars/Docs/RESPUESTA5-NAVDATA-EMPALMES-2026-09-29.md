# NavData → vmsOpenAcars: empalmes calculados, confianza y estadísticas

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-29
> **Responde a:** `PEDIDO-NAVDATA-EMPALMES-2026-09-29.md`
> **Estado:** §3.1, §3.2, §3.3 y §3.5 **implementados**. §3.4 (cruces) sigue pendiente. Y **vuestro
> caso de KMIA no es lo que parecía**: al medirlo, el criterio lo rechaza con razón.

Aceptamos el cambio de enfoque: **el criterio se calcula de los datos**, lo curado queda encima
para corregir. Es lo que hemos implementado, con vuestro criterio de §4 como base.

---

## 1. Lo que ya devuelve `/airport/{icao}/taxiway-joins/`

```json
{ "icao": "SKBO", "version": "2816413215c8",
  "stats": { "nodes": 515, "segments": 569, "dangling_ends": 100,
             "median_gap_m": 34.9, "median_segment_m": 39.7, "components": 3,
             "gaps": {"0-25": 27, "25-50": 43, "50-100": 26, "100-200": 4, ">200": 0},
             "joins_published": 13, "joins_curated": 1 },
  "count": 14,
  "joins": [ { "node_a": ..., "node_b": ..., "taxiway": "C", "gap_m": 30.8,
               "turn_deg": 0.6, "confidence": 0.88, "kind": "gap",
               "source": "computed" }, ... ],
  "invalid": [] }
```

- **`source: "computed" | "curated"`** y **`confidence` (0-1)**, como pedisteis: podéis poner
  umbral por confianza. Lo curado gana si coincide en el mismo par de nodos.
- **`stats`** por aeropuerto: nodos, segmentos, extremos sueltos, mediana de hueco y de segmento,
  histograma de huecos y **`components`** (componentes conexas). Con eso deriváis vuestro radio de
  fusión y sabéis de antemano si la red está completa.
- **`version`**: hash de 12 caracteres del conjunto de nodos del aeropuerto. Si cambia, invalidáis
  caché (§3.5).
- Sobre `runs` de vuestra lista: lo hemos interpretado como **componentes conexas** (`components`).
  Si queríais otra cosa, decidlo y lo añadimos.

Medido en vuestros aeropuertos:

| | componentes | extremos sueltos | empalmes | de ellos |
|---|---|---|---|---|
| SKBO | 3 | 100 | **14** | 13 calculados + 1 curado |
| LEMD | 3 | 52 | 2 | 2 calculados |
| CYUL | 2 | 27 | 1 | 1 calculado |
| KBOS | **1** | 56 | 0 | — (no hay nada que unir) |
| KMIA | 2 | 78 | **0** | ver §2 |

## 2. El criterio, y las dos cosas que añadimos al vuestro

Vuestros cinco puntos, y dos reglas más que salieron de medir:

1. Candidato: dos nodos de grado 1 que no son del mismo segmento.
2. Distancia: `gap ≤ min(200 m, max(3 × mediana de segmento, 40 m))`.
3. Continuidad: giro ≤ 60°.
4. Nombre: mismo `taxiway` puntúa más.
5. **Añadido — ni sobre ni a través del pavimento**: si la recta del empalme cae dentro del
   rectángulo de una pista o lo cruza, no se publica. Eso es un **cruce**, no un empalme, y es la
   regla que evita inventar atajos por la pista (vuestro punto 5).
6. **Añadido — puentes entre componentes**: además de los huecos entre extremos sueltos,
   publicamos el par más cercano entre componentes distintas, con `kind: "component_bridge"`. Es
   la diferencia entre «falta un trozo de plataforma» y «la red está partida», y es lo que de
   verdad impide rutear.
7. `confidence` = 0,45 × distancia normalizada + 0,35 × giro + 0,20 × nombre.

## 3. KMIA: medido, y no es un hueco de 3 m

Es lo único de vuestro documento que **no** vamos a poder arreglar con empalmes, y conviene que lo
sepáis antes de encender el interruptor allí:

- La red de KMIA tiene **2 componentes**: la principal (2.728 nodos) y otra de **16 nodos**.
- Esos 16 nodos **no son plataforma**: son **muñones del eje de pista** (`L1` y `K1`) que están
  *dentro* del pavimento (lateral 1-4 m del eje de la pista 2/3), es decir, la pista modelada como
  caminos.
- El par más cercano entre componentes está a **2,8 m**, pero con **giro de 179,6°** y **ambos
  nodos sobre la pista**: por eso el criterio lo rechaza, y hace bien. Unirlos sería coser dos
  muñones de pista opuestos, no unir plataforma.

Consecuencia para vuestro caso `VHR31` (`G181` → `08R`): con identidad por nodo, KMIA **sí tiene una
red de plataforma conexa** (2.728 nodos, un solo componente), y lo que queda aparte son los muñones
de pista. Si la ruta no se encuentra, el motivo está en el **destino** de esa ruta (que apunte a un
nodo del componente de pista) y no en un empalme que falte. Con `stats.components: 2` ya podéis
detectarlo desde nuestro lado, y con `version` saber cuándo cambia.

## 4. Lo que sigue pendiente

**§3.4 — `crossings`.** Vuestra evidencia de KMIA (avisó bien de los puntos de espera de la 27 y la
30 yendo a la 08R) es exactamente el caso que justifica el endpoint, y sigue en cola: el
emparejamiento de extremos a cada lado de la pista necesita validarse en varios aeropuertos antes de
publicarse. La otra mitad de §3.4 —la calle de entrada de cada pista— ya la tenéis: `/holdshort/`
publica los puntos de espera de **todos** los extremos de pista con `taxiways`, `type` y
`runway_names`.
