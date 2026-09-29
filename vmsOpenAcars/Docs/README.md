# Documentación de vmsOpenAcars

Los archivos **`.md` son la única fuente de verdad**. No se guardan artefactos generados
(PDF, HTML, PNG, JPEG) en el repositorio: pesaban ~12,5 MB, se quedaban desactualizados en
silencio y llegaron a publicarse versiones con 5 releases de retraso.

| Documento | Para quién | Contenido |
|---|---|---|
| `BRIEFING.md` | **Pilotos** | Guía de usuario: configuración, flujo de vuelo, scoring, mapa, LOGBOOK |
| `PRIMEROS_PASOS.md` | **Pilotos nuevos** | Alta en phpVMS, API key, descarga e instalación |
| `architecture.md` | **Desarrolladores** | Arquitectura, módulos, esquemas de BD, notas de build |
| `SETUP-ENTORNO.md` | **Desarrolladores** | Cómo montar el proyecto en otro equipo: qué no viaja por git (`App.config`, `packages/`), toolchain, y `dsh` (DeepSeek Harness) |
| `PEDIDO-PHPVMS-PRUEBAS.md` | **Equipo de phpVMS** | Lo que hace falta para validar con PIREPs reales, **solo los producidos por vmsOpenACars**: filtro por productor y sin el tope de 20 (el único vuelo con datos de rodaje cae fuera), `source_name` en los 13 que lo traen vacío, la pista usada, la ruta de rodaje declarada y el estado de moderación |
| `RESPUESTA-PHPVMS-PRUEBAS-2026-09-29.md` | **Equipo de phpVMS** | Nuestra respuesta: aceptadas sus dos correcciones (la fase va en `status` y no en `phase`; `state=2` es `ACCEPTED`), los nombres de campo decididos (`departure-runway`, `arrival-runway`, `Taxi Route`) y nuestra postura sobre `fuel`, el backfill y el endpoint global |
| `ANADIDO-PHPVMS-CIERRE-2026-09-29.md` | **Equipo de phpVMS** | Añadido al cierre, con la prueba en vivo: `type = 2` se acepta y **no se sirve** (corregido en el cliente) y **`fuel` sigue sin persistirse** en su lado, con la petición y la respuesta exactas |
| `api_vms.md` | **Equipo de phpVMS** (copiado) | Contrato de su API REST: la **tabla de tipos** de `acars` (`0` posiciones, `1` ruta, `2` mensajes), `acars/logs` GET y POST, `fields`, y la nota de que en `fields` va el **nombre** y no el slug |
| `PEDIDO-NAVDATA-TILES.md` | **Equipo de NavData** | Pedido de un endpoint proxy de teselas de CARTO con caché (para dejar de distribuir la clave del mapa) |
| `PEDIDO-NAVDATA-RUTAS-TAXI.md` | **Equipo de NavData** | Pedido de una base de rutas de rodaje acostumbradas (puesto → pista) que NavData agregue y sirva a toda la comunidad: observaciones, agregación, moderación experta y los tres campos aditivos que hacen falta |
- `PEDIDO-NAVDATA-EMPALMES-2026-09-29.md` — pedido a NavData de **empalmes calculados** (no curados a
- `RESPUESTA5-NAVDATA-EMPALMES-2026-09-29.md` — su respuesta: `stats`, `version`, `confidence` y
- `RESPUESTA-NAVDATA-TILES-2026-09-29.md` — **nuestra respuesta** al proxy de teselas: verificado en
- `RESPUESTA2-NAVDATA-TILES-2026-09-29.md` — **nuestra segunda respuesta**: los tres arreglos
- `RESPUESTA3-NAVDATA-TILES-2026-09-30.md` — **nuestra tercera** (cierre): verificado todo lo suyo y el
  **bloqueante de una línea** para adoptar el proxy — GMap.NET no permite cabeceras propias, así que la
  clave de teselas tiene que poder ir como parámetro (`?key=`), como ya hace CARTO.
- `RESPUESTA9-NAVDATA-TILES-2026-09-29.md` — **su tercera**: purga hecha, `criterion_version` entregado,
- `RESPUESTA10-NAVDATA-TILES-2026-09-30.md` — **su cuarta**: la clave **por la URL** (`?key=`) admitida
- `RESPUESTA4-NAVDATA-TILES-2026-09-30.md` — **nuestra cuarta**: con la clave admitida en la URL, el
- `RESPUESTA11-NAVDATA-TILES-2026-09-30.md` — **su quinta**: el aviso de la clave en el log era real
- `RESPUESTA5-NAVDATA-TILES-2026-09-30.md` — **nuestra quinta (cierre)**: verificacion externa tras su
- `RESPUESTA6-NAVDATA-TILES-2026-09-30.md` — **nuestra sexta (cierre del cierre)**: aceptados los **28
- `CIERRE-NAVDATA-2026-09-30.md` — **su cierre de hilo completo** (9 documentos en dos dias): la tabla de
  lo entregado (`mag_var`, espacios aereos, AIRAC desde fichero, KB de rodaje, puntos de espera, `node_id`,
  empalmes, teselas, `expires_at`), lo que queda nuestro (v0.9.18, el contador de caidas, la clave nueva de
  VHR) y **lo unico suyo: `runway-crossings`**, donde el caso que falta es `has_hold_short: false`.
  dias** de gracia (un ciclo AIRAC), **si al `expires_at`** para que la clave vieja muera sola el
  2026-10-28 —un corte que dependa de que alguien se acuerde es el mismo fallo que ya hemos visto dos
  veces— y **si al porcentaje** de cuota con la etiqueta de proxy de mapas.
  incidente, la auditoria de la clave en nuestro lado (limpia: ni en el historial de git, ni en Docs/, ni
  en `vmsOpenACars.txt`) y **la rotacion que vamos a hacer**, con la unica pregunta que la frena —la
  **ventana de gracia**—, porque cada cliente de piloto usa esa clave.
  (**10 claves de aerolinea en claro en `logs/gunicorn.log`**, `%(r)s` de gunicorn escribe el query string),
  arreglado con `gunicorn.conf.py` y un test que fija el formato; admiten que el primer intento **tumbo el
  servicio unos minutos**; aceptan que el proxy sea el camino y proponen la comprobacion cruzada
  `placeholder` suyo vs teselas con marca de agua nuestras; prefieren `?key=` **y** `origin_domain` juntos.
  **proxy pasa a ser el camino principal** (no la opción) y la caída va en tres escalones: proxy →
  CARTO con la clave del piloto → CARTO sin clave, **con marca de agua**, que es el escalón que se usará
  mientras `carto_api_key` siga vacío en la distribución.
  **solo en la ruta de teselas** (en el resto sigue habiendo 401, con test que lo fija), `origin_domain`
  opcional en esa ruta, y la aclaracion de que `month`/`plan_limit` van dentro de `tiles`. Con esto el
  proxy queda desbloqueado para el cliente.
  el consumo del mes como número, y la adenda donde admiten que **en los endpoints de rodaje el token no
  estaba en la clave de caché** (era decorativo y los incidentes los arregló el vaciado manual).
  verificados con `cf-cache-status` (`private` + `BYPASS` ya desplegados; la copia vieja del borde sigue),
  y la petición de que **el token de formato cambie con el criterio**, porque nuestra invalidación va por el
  hash de nodos y con él habríamos servido empalmes viejos.
- `RESPUESTA8-NAVDATA-TILES-2026-09-29.md` — **su segunda respuesta**, guardada tal cual: el 401 era
  **Cloudflare por delante** de su servidor, `voyager` era un bug real de URL, y su tabla de CYUL estaba
  desactualizada por una caché de 24 h sin subir el token.
  vivo (200, `X-Cache` HIT, `max-age` de 28 días, `tiles-stats`), el fallo de **autenticación después
  de la caché** (las teselas cacheadas se sirven sin clave), la discrepancia de CYUL en su tabla, y lo
  que asumimos nosotros (purga a 30 días, adopción del proxy con caída a CARTO directo).
- `RESPUESTA7-NAVDATA-TILES-2026-09-29.md` — **su respuesta** al pedido, guardada tal cual.
  empalmes **calculados** ya desplegados; su medición de KMIA corrige la nuestra (2 componentes, y el
  destino de esa ruta cae en el componente de los muñones de pista).
  mano), con `confidence`, estadísticas por aeropuerto y criterio que sirva en cualquier red: la fusión
  por proximidad con un umbral fijo de 45 m no puede cubrir a la vez a SKBO y a KMIA (medido).
| `RESPUESTA-NAVDATA-RUTAS-TAXI-2026-09-29.md` | **Equipo de NavData** | Respuesta a su respuesta: verificación de `taxiway`/`taxiways` y `node_id`, el defecto del punto de espera medido (14 puntos, 12 en las paralelas), sí al arreglo de sobre-generación, y tres cosas que necesitamos de ellos |
| `RESPUESTA2-NAVDATA-RUTAS-TAXI-2026-09-29.md` | **Equipo de NavData** | Informe del **banco de pruebas** tras pasar ellos a los tipos de nodo del escenario: la secuencia de avisos del rodaje real cambió (aparece el giro que la sobre-generación se comía, y el hold-short episodio baja a uno), el inventario nuevo fijado en un test, y una pregunta sobre `K1`/`V` |
| `RESPUESTA3-NAVDATA-RUTAS-TAXI-2026-09-29.md` | **Equipo de NavData** | Cierre del caso `K1`/`V` (verificado al metro), confirmación de que los tres endpoints de la base coinciden en cero tras su arreglo de caché, el criterio de `crossings` aceptado, y la corrección de nuestra lectura de las 3 observaciones de prueba |
| `RESPUESTA-NAVDATA-2026-09-29.md` | **Equipo de NavData** | Respuesta al aviso de cambios del 29/09/2026: impacto punto por punto, lo corregido y las tres respuestas que nos piden |
| `CONFIRMACION-NAVDATA-2026-09-29.md` | **Equipo de NavData** | Confirmación y verificación del arreglo de espacios aéreos y del rumbo verdadero, más una petición pequeña (`mag_var` del aeropuerto) |
| `CHANGELOG.md` | Todos | Historial de versiones, con causa raíz de cada corrección |
| `CHANGELOG-0.9.2-0.9.9.md` | Todos | Resumen corto de lo cambiado entre v0.9.2 y v0.9.9 (el detalle está en `CHANGELOG.md`) |
| `feature_map_sidebar.md` | Desarrolladores | Notas de la feature del sidebar de procedimientos |
| `MEMORY.md` | Mantenedor | Índice de mantenimiento — **no es fuente de verdad**, se queda atrás |
| `project_vmsOpenAcars.md` | Mantenedor | Estado de alto nivel (⚠️ escrito a v0.9.2) |

La guía técnica para agentes y mantenedores está en `CLAUDE.md`, en la **raíz** del repo.

---

## Generar los formatos publicables

Cuando haga falta entregar un PDF o HTML (por ejemplo para publicarlo en la web de la
aerolínea), se generan **en el momento** y **fuera del control de versiones**, no se
commitean:

```bash
# PDF (requiere pandoc)
pandoc BRIEFING.md -o BRIEFING.pdf

# HTML autocontenido
pandoc BRIEFING.md -s --metadata title="vmsOpenAcars — Guía del Usuario" -o BRIEFING.html
```

> **Regla:** si generas un artefacto, no lo añadas a git. Un PDF commiteado envejece sin
> avisar y acaba describiendo un producto que ya no existe — que es exactamente lo que pasó
> con el `BRIEFING` de v0.8.7 que se publicó junto a un cliente v0.9.8.
