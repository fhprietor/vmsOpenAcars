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
| `PEDIDO-NAVDATA-TILES.md` | **Equipo de NavData** | Pedido de un endpoint proxy de teselas de CARTO con caché (para dejar de distribuir la clave del mapa) |
| `PEDIDO-NAVDATA-RUTAS-TAXI.md` | **Equipo de NavData** | Pedido de una base de rutas de rodaje acostumbradas (puesto → pista) que NavData agregue y sirva a toda la comunidad: observaciones, agregación, moderación experta y los tres campos aditivos que hacen falta |
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
