# Respuesta de vmsOpenAcars al aviso de NavData del 29/09/2026

> **Para:** equipo de NavData · **De:** equipo de vmsOpenAcars
> **Cliente:** v0.9.16 · **Fecha:** 2026-09-29
> **Verificación:** los seis puntos del aviso se comprobaron contra el servicio en vivo antes de
> tocar nada; los resultados esperados del apartado 4 del aviso se reproducen todos.

---

## 0. Resumen del impacto y de lo hecho

| # | Cambio | Impacto real en nuestro cliente | Acción |
|---|---|---|---|
| 1.1 | `parkings[].heading` magnético | **Nulo**: nunca leímos ese campo (`FindNearestParking` usa `name`/`number`/`suffix` para el nombre del puesto) | Ninguna |
| 1.2 | `ils_freq_mhz` puede ser `null` | **Nulo**: ya era `double?` y se comprueba `HasValue` antes de usarlo | Ninguna |
| 1.3 | `/airspaces/`: 54 nm, `partial`, 503 | **Nos rompía**, y en silencio: un 503 se convertía en lista vacía y **pisaba** los espacios ya cargados | **Corregido hoy** (apartado 2) |
| 1.4 | `airway.direction = backward` | **Nulo**: no consumimos airways de NavData; la ruta viene de SimBrief y `via_airway` es una etiqueta | Ninguna |
| 1.5 | `has_vertical_angle` y `"advisory"` | **Nulo**: `has_vertical_angle` no lo usamos; `vertical_guidance` sólo se usa para dibujar la guía en la carta, y `advisory` ya se dibujaba discontinuo | Ninguna |
| 1.6 | `/nearest/approach-airport/` 404 | **Nulo**: ya lo tratábamos como «sin resultado» y no reintentamos | Contestamos vuestra pregunta (apartado 3) |

Los puntos del apartado 2 (1 fila por pista, nearest con radios grandes, ±180°, hold-short
compartido, AIRAC del METAR) nos vienen bien o nos son indiferentes: **no deduplicábamos pistas a
mano** y el resto son mejoras.

---

## 1. Verificación de vuestro apartado 4 (contra el servicio en vivo)

Reproducidos todos, con vuestros valores esperados:

| Comprobación | Resultado medido |
|---|---|
| SBGR parkings magnético | `heading = 117.7` ✓ |
| EDDF pistas | **8** filas, `07L → has_ils, 110.3` ✓ |
| `/airspaces/?lat=4.7&lon=-74.1` | `count = 32`, `radius_nm = 54`, `partial = false` ✓ |
| L602 direcciones | `backward = 64`, `both = 4` ✓ |
| SKBO aproximaciones | 20, con **2 `advisory`** ✓ |
| `/nearest/airport/` radio 500 nm | **EDFJ** primero ✓ |

---

## 2. 1.3 — lo que hemos arreglado, y lo que hemos medido

### El fallo

Nuestro cliente hacía `if (!resp.IsSuccessStatusCode) return null;` y convertía ese `null` en **lista
vacía**. Con vuestro cambio, un 503 pasó a ser indistinguible de «aquí no hay espacios aéreos», y
además el resultado vacío **sustituía** al conjunto ya cargado: el cielo se vaciaba.

### Lo corregido

- **«No pude preguntar» ≠ «aquí no hay nada».** El resultado ahora lleva `Unavailable` y el llamante
  no pisa lo que ya tenía si no hay datos frescos.
- **`Retry-After` respetado, y por punto.** Lo leemos de la cabecera (Delta o Date) y bloqueamos ese
  punto concreto entre 1 minuto y 1 hora. Lo medido abajo obligó a que fuera **por punto y no
  global**: con un bloqueo global, el primer 503 dejaría sin pedir los otros 15 puntos de la ruta.
- **`partial` no se cachea a disco.** Se marca y se usa en la sesión, pero no se guarda en la caché
  de 7 días: si no, una truncadura puntual se convertiría en una falta permanente.
- **`radius_nm` se lee del servidor**, no se supone.
- **La cobertura deja de ser 3 puntos.** El cliente pedía origen, destino y —a veces— el punto
  medio. Con 54 nm eso deja la ruta a trozos. Ahora se muestrea el arco cada `0.75 × radio`, con
  tope de 16 peticiones, **interpolando sobre la esfera** (en SKBO→KBOS el punto medio del arco se
  separa **12,9 nm** del promedio en línea recta, que sería un punto por el que el avión no pasa).
  Con 200 nm de radio, el mismo tope no deja ningún hueco; con 54 nm cubre más que antes, pero no
  todo — de ahí la petición (a) del apartado 4.
- **El log deja de mentir**: cuando no hay datos frescos dice `Airspace data unavailable (NavData
  503)`, no «0 espacios aéreos».

### Lo medido en vivo **hoy**, con una ruta real del piloto (SKCG→KBOS, 1.931 nm, 16 muestras)

```
distribución de códigos en los 16 puntos:   200 → 4        503 → 12
los que responden (zona de Cartagena, la que tenéis en caché):
   10.4424,-75.5130 → 11 espacios aéreos
   12.5737,-75.2735 →  0 espacios aéreos   (200 con lista vacía = «aquí no hay», correcto)
   18.9662,-74.5275 →  0 espacios aéreos
   31.7400,-72.8260 →  0 espacios aéreos
KBOS (42.363,-71.0064), tres intentos seguidos: 503, 503, 503
cabecera del 503:  Retry-After: 300
cuerpo:            {"error":"La fuente de espacios aéreos (OpenAIP) no está disponible temporalmente",
                    "code":"UPSTREAM_UNAVAILABLE"}
```

**Traducción:** hoy el monitoreo de espacios aéreos está caído en toda la ruta salvo donde tenéis
caché. Nuestro cliente ya no lo disfraza de cielo vacío, pero el dato no está — es lo que más nos
urge de todo este aviso.

---

## 3. Vuestra pregunta 1: enviamos el rumbo **verdadero**

Y no es una suposición nuestra, es lo que miden nuestros registros de vuelos reales:

- Despegue de **SKCG**: la aeronave sobre el eje reportó **3°**, con el eje verdadero en 2,31° y el
  magnético en 10,8° (vuestros propios datos). El 3° es verdadero.
- Final de **KBOS**: reportó **19°**, contra verdadero 19,67° y magnético 33,4°.
- En los dos casos con la aeronave centrada (desviación de 9 ft y 15 ft) y viento flojo, así que no
  es abate (crab).

**Consecuencia que confirma vuestro aviso**: vuestro `score` y `heading_diff_deg` llevan un sesgo
igual al `mag_var` del aeropuerto, hasta ±25°.

**Lo que necesitamos para arreglarlo bien** (elegid vosotros, preferimos la segunda):

1. Un parámetro `heading_reference=magnetic|true` en la llamada, o
2. **que nos devolváis el `mag_var` del aeropuerto emparejado** — nos sirve además para el resto de
   comparaciones, porque el simulador nos da verdadero y **todos** vuestros rumbos son magnéticos
   (pista, parkings, STAR).

Nota: el `mag_var` lo necesitamos **antes** de saber qué aeropuerto es, así que la primera llamada
siempre sale con lo que tengamos a bordo. Con vuestro `mag_var` en la respuesta podemos corregir en
la segunda.

---

## 4. Vuestras preguntas 2 y 3

**2. ¿Aplicabais `mag_var` a los parkings por vuestra cuenta? → No.** No leímos nunca el campo
`heading` de los parkings; el único consumidor (`FindNearestParking`) usa `name`, `number` y
`suffix` para componer el nombre del puesto. No hay compensación que quitar.

**3. Qué necesitamos:**

- **(a) Sí, más cobertura en `/airspaces/`.** Es nuestra petición principal: **radio configurable, y
  de vuelta a ≥200 nm** (o el máximo que permita OpenAIP). Con 54 nm y un tope razonable de
  peticiones no se cubre una ruta de 1.900 nm. Mientras llega, el muestreo funciona con cualquier
  radio que pongáis.
- **(b) No** necesitamos todas las frecuencias LOC por pista: con una por pista (la de categoría más
  alta) nos alcanza — elegimos por nombre de pista y comprobamos la frecuencia sintonizada con ±0.05
  MHz. Si algún día hay dobles ILS simultáneos, os pediremos `ils_candidates`.
- **(c) Preferimos seguir con 404** en `approach-airport`: ya lo tratamos como «sin resultado», no es
  un error y no reintentamos. **No cambiéis el contrato por nosotros.**

  Y un dato para vuestro conteo de carga: **llamamos cada 5 segundos** durante Descenso y
  Aproximación, así que buena parte de esas 9.267 llamadas son nuestras. Si os interesa bajar carga,
  decídnoslo y lo espaciamos (p. ej. a 15 s); no lo hemos hecho ya porque nuestro filtro de desvío
  exige persistencia de dos sondeos.

---

## 5. Sugerencia (no es un pedido)

Si el 503 depende de OpenAIP sin caché, un **precalentado de los aeropuertos más volados** (o un TTL
mayor) reduciría mucho el impacto que hemos medido hoy. En nuestro lado ya no se convierte en un dato
falso, pero el dato sigue faltando.

---

## 6. Verificación de nuestro cambio

- Build **Debug** y **Release** en verde; suite **272/272** (7 tests nuevos del muestreo, con las
  coordenadas reales del vuelo SKCG→KBOS y comprobaciones de reparto uniforme, ausencia de huecos y
  comportamiento degenerado).
- Volcado de las 16 muestras para la comprobación en vivo:
  `%TEMP%\airspace_samples_skcg_kbos.txt`.
