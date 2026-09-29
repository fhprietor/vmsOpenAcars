# vmsOpenAcars → NavData: confirmación y verificación del arreglo del 29/09/2026

> **Para:** equipo de NavData · **De:** equipo de vmsOpenAcars
> **Cliente:** v0.9.16 · **Fecha:** 2026-09-29

Recibido y **verificado en vivo**, punto por punto. Nuestras mediciones coinciden con las vuestras
al decimal, así que no hay nada que cuadrar. Resumen de lo que hemos comprobado y de lo que hemos
cambiado en el cliente.

---

## 1. Espacios aéreos: confirmado, y se nota

Los cinco puntos de nuestra ruta real (SKCG→KBOS), medidos hoy contra vuestro servicio:

| Punto | Antes | Ahora | `source` | `radius_nm` | `capped` | Países |
|---|---|---|---|---|---|---|
| 10.4424, -75.5130 (SKCG) | 11 | **21** | local | 200 | false | CO, VE |
| 12.5737, -75.2735 | 0 | **17** | local | 200 | false | CO, CW, VE |
| 18.9662, -74.5275 | 0 | **2** | local | 200 | false | CW, US |
| 31.7400, -72.8260 | 0 | **5** | local | 200 | false | US |
| 42.3630, -71.0064 (KBOS) | 503 ×3 | **417** | local | 200 | false | CA, US |

Tiempos entre 0,35 y 0,9 s; **ningún 503** en toda la ruta.

Recorriendo la ruta entera con nuestro muestreo: **24/24 consultas OK**, `source=local` en todas y
**613 espacios aéreos** en la unión (con el servicio caído eran 11). Es un cambio de orden de
magnitud, no un arreglo cosmético.

**Zona densa reproducida**: `lat=51.5, lon=-0.1` → `count=500, radius_nm=91, capped=true,
source=local, countries=BE,FR,GB,NL`. Exactamente como avisasteis.

### Lo que hemos cambiado en el cliente con los campos nuevos

- **`source` y `countries` se pintan en el log de la ruta** (`· source=local · CO,CW,VE,US,CA`). Es
  justo la señal que nos ofrecisteis para reportar huecos: si vemos `source=openaip_api` o un país
  ausente en un corredor, os lo mandamos con los puntos.
- **`capped` también se pinta**, porque implica que la cobertura real es menor que el radio nominal.
- **Tope de muestreo de 16 → 24 puntos**, precisamente por el caso `capped`: con el radio reducido a
  91 nm, 16 puntos habrían dejado saltos de 129 nm en una ruta de 1.900 nm; con 24 el salto es de
  84 nm y **no hay huecos** (hay un test con ese escenario). Con 200 nm la misma ruta son 13
  peticiones, así que el tope no cuesta nada en el caso normal.
- Seguimos leyendo `radius_nm` de cada respuesta en vez de asumir un valor, y el `503` +
  `Retry-After` se maneja **por punto** (un bloqueo global habría dejado sin pedir el resto de la
  ruta cuando sólo un punto falla).

---

## 2. Rumbo verdadero: confirmado y adoptado

Verificado con vuestro caso de SKCG, y nos sale **idéntico**:

| Llamada | `heading_diff_deg` | `score` |
|---|---|---|
| `heading=3&heading_reference=magnetic` (como antes) | 7,8 | 0,235 |
| `heading=3&heading_reference=true` | **0,7** | **0,164** |

El cliente **ya envía `heading_reference=true`** en `/nearest/approach-airport/`, así que el sesgo
del `mag_var` en el camino de desvíos queda cerrado por nuestra parte, sin compensaciones locales.
Gracias por implementar además el `mag_var` en la respuesta: lo leemos y lo guardamos.

**Queda un ámbito donde no nos sirve todavía, y es una petición pequeña si os parece bien:** las
comparaciones **locales** de pista. Para elegir la pista del destino comparamos nuestro rumbo
(verdadero) contra `runway.heading` (magnético) con una tolerancia de 15°. En SKCG el sesgo es de
8,4° y no se nota; en **KBOS es de 13,7°**, así que el margen real se queda en ~1,3° y el matcher
puede no encontrar la final del destino. Se arreglaría convirtiendo antes de comparar, y para eso
necesitamos el `mag_var` **del aeropuerto consultado**:

- Hoy **no** viene en `/airport/{icao}/` ni en `/runways/` (verificado: cero ocurrencias de
  `mag_var` en la respuesta de SKBO). Sí viene en `/nearest/approach-airport/`.
- Si os es fácil añadirlo al bloque del aeropuerto (un campo, ya lo tenéis calculado), lo usamos y
  cerramos el tema. Si preferís no tocar ese contrato, lo tomamos de otro lado y no pasa nada.

Y una nota de método: **descartamos la alternativa de "aceptar magnético o verdadero"** en ese
filtro. El que para un falso desvío real (KOWD) es el cono angular, así que ensanchar la puerta lo
reabriría. Preferimos convertir bien a ensanchar.

---

## 3. Resto de vuestras respuestas

- **(b) `ils_candidates`**: entendido, no hace falta. Con una frecuencia por pista nos vale.
- **(c) 404 en `approach-airport`**: perfecto, no cambiéis nada.
- **Sondeo cada 5 s**: gracias por el dato de ~1 ms por llamada; lo dejamos como está. Si algún día
  crece, avisadnos y lo espaciamos nosotros (no hace falta que lo cacheéis por nosotros).
- **Precalentado por país**: nos parece mejor que por aeropuerto, por lo que decís de los huecos
  entre ellos. Tomamos nota.

---

## 4. Verificación de nuestro lado

- Build **Debug** y **Release** en verde; suite **273/273** (un test nuevo para el caso `capped`).
- Volcado del muestreo de la ruta para las comprobaciones en vivo:
  `%TEMP%\airspace_samples_skcg_kbos.txt`.

Gracias por la rapidez: entre el aviso y el arreglo pasó un día, y el diagnóstico que nos pedisteis
era el mismo que teníais — la diferencia fue poder decir **cuánto** fallaba y **dónde**.
