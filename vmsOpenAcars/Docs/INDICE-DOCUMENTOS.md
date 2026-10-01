> **Convención de nombres (2026-10-01, sustituye a VMSOPENACARS/NAVDATA):** el nombre dice **quién escribe y a quién**, no de quién es el fichero:
> `AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md` — p. ej. `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_carta-...md` es **de NavData, para nosotros**, del 01/10, hilo *generador-rutas*, mensaje **nº 3**. La **fecha delante** hace que el orden alfabético sea el cronológico.
> Los ficheros **históricos no se renombran** (los referencia `CLAUDE.md`): la regla aplica de aquí en adelante. Los de hoy sí se han renombrado.
> **Generador de rutas, estado:** nuestros #1 (`..._01_cinco-condiciones`) → su #2 (`respuesta-a-vmsOpenACars-generador-rutas-2026-10-01.md`, llegó ~17 min antes y **no está en Docs: queda SUPERADO**) → su #3, en tres ficheros (carta + anexo1 + anexo2), que es **lo último** del hilo.
# ÍNDICE DE DOCUMENTOS — `Docs/`

> **Para qué sirve:** saber de un vistazo **de qué hilo es cada documento** y, sobre todo,
> **cuál es el último de cada hilo**. Creado el **2026-10-01**.
> **Autor:** [VMSOPENACARS] = equipo de vmsOpenACars · [PHPVMS] = equipo de NavData / equipo de phpVMS.
> **Alcance:** solo lista; no sustituye a `README.md` (índice por audiencia) ni a `CLAUDE.md`.

---

## Regla de nombres (de aquí en adelante)

```
AAAA-MM-DD_<DE>_<PARA>_<hilo>_<n>_<asunto>.md
```

- **La fecha va delante** → el orden alfabético del gestor de archivos es el orden cronológico.
- `<hilo>`: uno de `tiles`, `conectividad`, `rutas-taxi`, `empalmes`, `generador-rutas`,
  `phpvms`, `navdata-api`, `otro`.
- `<n>`: número de mensaje dentro del hilo, dos dígitos (`01`, `02`, …).
- `<asunto>`: corto, en minúsculas y con guiones.
- Ejemplo: `2026-10-01_SUYA_generador-rutas_02_cinco-condiciones-aceptadas.md`

**Los ficheros históricos NO se renombran.** Los nombres actuales son confusos (`respuesta2-`,
`RESPUESTA4-`, `vmsOpenACars-a-…`) pero están citados desde la guía y desde otros documentos;
renombrarlos rompería enlaces. La regla aplica **solo a lo que se cree desde 2026-10-01**.

---

## Hilo 1 — Teselas y proxy de CARTO

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-TILES.md` | 2026-09-29 | [VMSOPENACARS] | Pedido del proxy de teselas con caché; incluye la pregunta para CARTO. |
| 2 | `RESPUESTA7-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [NAVDATA] | El proxy está en marcha y probado de punta a punta. |
| 3 | `RESPUESTA-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Verificado en vivo; dos cosas que devolver y una discrepancia texto/servidor. |
| 4 | `RESPUESTA8-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [NAVDATA] | Los tres hallazgos corregidos (el `401` era Cloudflare). |
| 5 | `RESPUESTA2-NAVDATA-TILES-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Arreglos verificados + petición de `criterion_version` y consumo. |
| 6 | `RESPUESTA9-NAVDATA-TILES-2026-09-29.md` | 2026-09-30 | [NAVDATA] | `criterion_version`, consumo del mes y purga del borde. |
| 7 | `RESPUESTA3-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cerrado por VMSOPENACARS parte; bloqueante de una línea (clave en la URL). |
| 8 | `RESPUESTA10-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [NAVDATA] | La clave en la URL, resuelta (solo en la ruta de teselas). |
| 9 | `RESPUESTA4-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Todo el tráfico de teselas al proxy y la caída en tres escalones. |
| 10 | `RESPUESTA11-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Teníamos razón: la clave estaba en claro en su log; corregido. |
| 11 | `RESPUESTA5-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cierre: la rotación de la clave la hacemos nosotros. |
| 12 | `RESPUESTA6-NAVDATA-TILES-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cerrado: 28 días, sí al `expires_at` y sí al porcentaje. |

> **ÚLTIMO DE ESTE HILO: `RESPUESTA6-NAVDATA-TILES-2026-09-30.md`** [VMSOPENACARS].
> Responde a `RESPUESTA12-NAVDATA-TILES-2026-09-30.md` [NAVDATA], **que no está en `Docs/`**.

---

## Hilo 2 — Rutas de rodaje acostumbradas (base de conocimiento compartida)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-RUTAS-TAXI.md` | 2026-09-29 | [VMSOPENACARS] | Pedido de la base compartida: contrato, umbrales y moderación experta. |
| 2 | `RESPUESTA-NAVDATA-RUTAS-TAXI-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Respuesta y verificación en vivo de sus tres puntos. |
| 3 | `RESPUESTA2-NAVDATA-RUTAS-TAXI-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Segunda respuesta (banco de pruebas corrido); lleva corrección posterior en cabecera. |
| 4 | `RESPUESTA3-NAVDATA-RUTAS-TAXI-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Tercera respuesta; corrige la lectura de las 3 observaciones (eran filas de prueba). |

> **ÚLTIMO DE ESTE HILO: `RESPUESTA3-NAVDATA-RUTAS-TAXI-2026-09-29.md`** [VMSOPENACARS].
> Sus `respuesta-a-…`, `respuesta2-a-…` y `respuesta3-a-…` de este hilo **no están en `Docs/`**.

---

## Hilo 3 — Empalmes y `node_id`

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-EMPALMES-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Empalmes y criterios para encender `node_id` en cualquier aeropuerto. |
| 2 | `RESPUESTA5-NAVDATA-EMPALMES-2026-09-29.md` | 2026-09-29 | [NAVDATA] | Empalmes calculados, confianza y estadísticas; cruces pendientes. |

> **ÚLTIMO DE ESTE HILO: `RESPUESTA5-NAVDATA-EMPALMES-2026-09-29.md`** [NAVDATA].

---

## Hilo 4 — Conectividad de la red publicada (LMML / `unions_missing`)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-NAVDATA-CONECTIVIDAD-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | El criterio común para los huecos de empalme; origen, el rodaje real de LMML. |
| 2 | `respuesta-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | La cobertura medida y un bug suyo que LMML destapó; piden el corpus. |
| 3 | `vmsOpenACars-a-NAVDATA-conectividad-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Los tres descuadres, el hallazgo que confirma el criterio y el corpus. |
| 4 | `respuesta2-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Los tres descuadres eran suyos y están corregidos. |
| 5 | `respuesta3-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Prueba de aceptación construida; `LMML/05 "T J K L"` pasa. |
| 6 | `vmsOpenACars-a-NAVDATA-conectividad-2-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Cierre del hilo: lo verificado, lo arreglado y el corpus en camino. |
| 7 | `respuesta4-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Las uniones que faltan, con nombre y distancia (LEMD era un hueco de 1,3 m). |
| 8 | `vmsOpenACars-a-NAVDATA-conectividad-3-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Decisión sobre `A1`↔`B` (opción 1) con dos condiciones. |
| 9 | `respuesta5-a-vmsOpenACars-conectividad-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Los siete aeropuertos conectados: 7 de 7 con `components_after_joins: 1`. |
| 10 | `vmsOpenACars-a-NAVDATA-conectividad-4-2026-09-30.md` | 2026-09-30 | [VMSOPENACARS] | Entrega el corpus (una ruta) y explica por qué el `Taxi Route` de los 37 está vacío. |

> **ÚLTIMO DE ESTE HILO: `vmsOpenACars-a-NAVDATA-conectividad-4-2026-09-30.md`** [VMSOPENACARS].
> No hay respuesta NAVDATA posterior a este documento; el estado global lo recoge
> `CIERRE-NAVDATA-2026-09-30.md` (ver «Cierre general»). Su `RESPUESTA-NAVDATA-CONECTIVIDAD-2026-09-30.md`
> (citado por nuestro #3) **no está en `Docs/`**.

---

## Hilo 5 — Generador de rutas de rodaje (endpoint nuevo)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `2026-10-01_VMSOPENACARS_NAVDATA_generador-rutas_01_cinco-condiciones.md` | 2026-10-01 | [VMSOPENACARS] | Respuesta a su propuesta: aprobamos el contrato con **cinco condiciones**. |
| 2 | `respuesta-a-vmsOpenACars-generador-rutas-2026-10-01.md` | 2026-10-01 | [NAVDATA] | **Recibido hoy:** acepta las cinco condiciones, aplica la errata y pre-registra el criterio de la fase 3. |
| 3 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_carta-cinco-condiciones-aceptadas.md` | 2026-10-01 | [NAVDATA] | Carta de la respuesta: acepta las cinco condiciones, reproduce LMML y pre-registra las cinco métricas de la fase 3. |
| 4 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_anexo1-errata-propuesta.md` | 2026-10-01 | [NAVDATA] | **Anexo 1:** corrige la propuesta (el giro de 45° inventado, `joins_used: []`, la cita de §4.3) sin tocar el original, con su `sha256` verificado. |
| 5 | `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_anexo2-formato-tanda-1.md` | 2026-10-01 | [NAVDATA] | **Anexo 2:** formato JSON de la tanda 1 que hay que entregar para arrancar la fase 2. |

> **ÚLTIMO DE ESTE HILO: `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_carta-cinco-condiciones-aceptadas.md`** [NAVDATA].
> Los tres ficheros de arriba (#3–#5) llegaron el **2026-10-01** como **un solo mensaje** —su carta es
> #3 y #4–#5 son sus anexos 1 y 2, en ese orden de lectura—, así que **el último es la carta**, la del
> `_03_` `cinco-condiciones-aceptadas`: es la que lleva el criterio de la fase 3 pre-registrado y lo
> que hay que contestar. Los tres comparten el `_03_` del nombre porque quien los bautizó los numeró
> como un único mensaje.
>
> **Tres avisos sobre #3–#5, que se copiaron con el nombre que traían (no se renombran aquí):**
> 1. **Su contenido los firma el equipo de NavData** («Para: equipo de vmsOpenACars · De: equipo de
>    NavData», cierre «equipo de NavData»), no nosotros: por contenido son [NAVDATA], aunque el nombre
>    diga `VMSOPENACARS`. Se listan con la etiqueta del nombre, y el mantenedor decide si la corrige.
> 2. **El nombre no sigue la convención de arriba**: por contenido serían
>    `2026-10-01_NAVDATA_VMSOPENACARS_generador-rutas_03_<asunto>.md` (el hilo tiene hoy los mensajes #1 y #2).
>    Los tres comparten el `_03_`, así que tampoco se numeran entre sí.
> 3. **No son los mismos ficheros que #2.** #2, `respuesta-a-vmsOpenACars-generador-rutas-2026-10-01.md`,
>    llegó **antes** (misma fecha, misma respuesta de NavData) y **no está en `Docs/`**: se lista aquí
>    porque pertenece al hilo. Los #3–#5 son la versión que NavData reenvió después, ya con la errata
>    **como anexo aparte** en vez de marcada dentro del original.
>
> Su propuesta inicial, `propuesta-a-vmsOpenACars-generador-rutas-taxi-2026-10-01.md`, **no está en
> `Docs/`** (su `sha256` es `e005abce691468b4ae658298234b3fe191e667c662807f97613932f89c2a5bb5`,
> verificado contra el adjunto el 2026-10-01).

---

## Hilo 6 — phpVMS: pruebas con PIREPs reales

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `PEDIDO-PHPVMS-PRUEBAS.md` | 2026-09-29 | [VMSOPENACARS] | Pedido de datos para validar con PIREPs reales (solo de vmsOpenACars). |
| 2 | `RESPUESTA-PHPVMS-PRUEBAS-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Respuesta: dos premisas nuestras eran falsas (la fase va en `status`). |
| 3 | `ANADIDO-PHPVMS-CIERRE-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Añadido al cierre, con prueba en vivo (`type = 2`, `fuel`). |

> **ÚLTIMO DE ESTE HILO: `ANADIDO-PHPVMS-CIERRE-2026-09-29.md`** [VMSOPENACARS].
> Sus `RESPUESTA-PHPVMS-PRUEBAS.md` y `CIERRE-PHPVMS-PRUEBAS.md` **no están en `Docs/`**.

---

## Hilo 7 — Aviso de cambios de la API de NavData (29/09)

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `RESPUESTA-NAVDATA-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Respuesta al aviso: los seis puntos, verificados en vivo. |
| 2 | `CONFIRMACION-NAVDATA-2026-09-29.md` | 2026-09-29 | [VMSOPENACARS] | Confirmación y verificación del arreglo, punto por punto. |

> **ÚLTIMO DE ESTE HILO: `CONFIRMACION-NAVDATA-2026-09-29.md`** [VMSOPENACARS].
> Su `aviso-vmsOpenACars-…` original **no está en `Docs/`**.

---

## Cierre general

| # | Fichero | Fecha | Autor | Qué es |
|---|---|---|---|---|
| 1 | `CIERRE-NAVDATA-2026-09-30.md` | 2026-09-30 | [NAVDATA] | Cierra **todos** los hilos abiertos (mag_var, espacios aéreos, rutas, puntos de espera, `node_id`, empalmes, teselas, `expires_at`); único pendiente suyo: `runway-crossings`. |

> **ÚLTIMO DOCUMENTO GLOBAL DEL INTERCAMBIO: `CIERRE-NAVDATA-2026-09-30.md`** [NAVDATA].
> Es anterior al hilo del generador de rutas (hilo 5), que es lo más reciente.

---

## Otros

Documentos del repositorio que no pertenecen a ninguno de los hilos anteriores. Todos son
**internos** ([VMSOPENACARS]) salvo donde se indique.

| Fichero | Fecha | Autor | Qué es |
|---|---|---|---|
| `README.md` | 2026-09-29 | [VMSOPENACARS] | Índice de la documentación por audiencia (pilotos / desarrolladores / equipos externos). |
| `BRIEFING.md` | 2026-09-29 | [VMSOPENACARS] | Guía del usuario/piloto (su cabecera dice «Versión 0.9.3»). |
| `PRIMEROS_PASOS.md` | 2026-09-19 | [VMSOPENACARS] | Alta en phpVMS (VHOLAR), API key, descarga e instalación. |
| `architecture.md` | 2026-09-29 | [VMSOPENACARS] | Arquitectura, módulos, esquemas de BD y notas de build (documento largo). |
| `SETUP-ENTORNO.md` | 2026-09-24 | [VMSOPENACARS] | Montar el proyecto en otro equipo: qué no viaja por git, toolchain y `dsh`. |
| `CHANGELOG.md` | 2026-09-29 | [VMSOPENACARS] | Changelog **canónico** (última entrada: `[0.9.20] — 30/09/2026`). |
| `CHANGELOG-0.9.2-0.9.9.md` | 2026-09-24 | [VMSOPENACARS] | Resumen breve del rango v0.9.2 → v0.9.9. |
| `api_vms.md` | 2026-09-29 | [VMSOPENACARS] (copia) | Copia del contrato de la API REST de phpVMS v7 (endpoints, tipos de `acars`). |
| `MEMORY.md` | 2026-09-28 | [VMSOPENACARS] | Índice de mantenimiento; **no es fuente de verdad** (lo dice el propio fichero). |
| `project_vmsOpenAcars.md` | 2026-09-20 | [VMSOPENACARS] | Estado del proyecto (desfasado: dice v0.9.2); **no es fuente de verdad**. |
| `feature_map_sidebar.md` | 2026-09-15 | [VMSOPENACARS] | Nota de memoria: sidebar de procedimientos en `MapForm` (v0.6.5–v0.8.7). |
| `INDICE-DOCUMENTOS.md` | 2026-10-01 | [VMSOPENACARS] | **Este índice.** |

Las fechas de esta sección son las del fichero en disco; donde el documento declara otra fecha en
su cabecera, manda la cabecera en el resto de secciones de este índice.
