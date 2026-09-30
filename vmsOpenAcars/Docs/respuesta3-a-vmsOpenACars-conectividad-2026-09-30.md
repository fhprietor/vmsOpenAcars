# NavData → vmsOpenACars: la prueba de aceptación está construida, y **vuestro caso de LMML pasa**

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** vuestra §4.3 (el corpus de rutas escritas)
> **Estado:** el comprobador está hecho con vuestro formato. **`LMML/05 "T J K L"` es trazable**
> sobre la red publicada, sin heurística. Mandad el corpus y lo pasamos entero.

---

## 1. Vuestro caso, hoy

```
python manage.py check_written_routes docs/rutas-escritas.csv
  OK   LMML/05 T J K L — 4 tramos (puesto a 64,2 m de la red)
  1/1 rutas trazables sin heurística (1.00)
```

Ese es **el vuelo que abrió este hilo**, con vuestra propia cifra de distancia al puesto (64 m), y
hoy su ruta escrita se traza **entera y sin heurística**: `T` → empalme `F|I` → `J` → `K`. Es la
misma ruta que en el log daba 15 avisos de `FUERA DE RUTA`.

## 2. Cómo se comprueba (y por qué NO usa la fusión por proximidad)

- Se entra por el **nodo más cercano a la posición del piloto** y se recorre la secuencia de calles
  escrita **en anchura**, sobre el grafo **publicado**: segmentos **más los empalmes de
  `/taxiway-joins/`**. Nada más.
- Si para trazar el siguiente tramo hiciera falta unir nodos por proximidad, la respuesta es **«no
  trazable»** y se dice por qué tramo se rompe (`failed_at`) con el motivo
  (`calle_no_existe`, `no_alcanzable`).
- Devuelve además la **distancia del puesto a la red**, que es lo que hace comprobable el caso de
  LMML: no es lo mismo fallar en la primera calle que a 60 m del puesto.
- Hay test del caso contrario: **sin el empalme publicado, la misma ruta NO es trazable**. Sin eso,
  la prueba no mediría nada.

## 3. Lo que necesitamos: el corpus

Vuestro formato, tal cual:

```
icao,runway,ruta_escrita,lat,lon
LMML,05,"T J K L",35.85055,14.48869
```

Con todas las que tengáis (y el replay del vuelo de LMML cuando esté). Publicamos el resultado
**ruta a ruta** y el ratio, que es vuestra métrica de §6. Con eso sabréis en qué aeropuertos podéis
apagar la fusión por proximidad y en cuáles os falta dato nuestro — y a nosotros nos da la lista
**priorizada por daño real**, que es la que queremos para seguir con las uniones que faltan
(LMML sigue en 2 componentes, LEMD en 2).

## 4. Nota de método

Este comprobador no es un test de laboratorio: el CSV que lee es **el mismo dato** que alimenta la
base de conocimiento (las rutas que el piloto teclea son las observaciones que enviáis). Así que
cada ruta nueva que acumuléis sirve a la vez para la prueba y para la base.
