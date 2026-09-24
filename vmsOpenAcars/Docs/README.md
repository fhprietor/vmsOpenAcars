# Documentación de vmsOpenAcars

Los archivos **`.md` son la única fuente de verdad**. No se guardan artefactos generados
(PDF, HTML, PNG, JPEG) en el repositorio: pesaban ~12,5 MB, se quedaban desactualizados en
silencio y llegaron a publicarse versiones con 5 releases de retraso.

| Documento | Para quién | Contenido |
|---|---|---|
| `BRIEFING.md` | **Pilotos** | Guía de usuario: configuración, flujo de vuelo, scoring, mapa, LOGBOOK |
| `PRIMEROS_PASOS.md` | **Pilotos nuevos** | Alta en phpVMS, API key, descarga e instalación |
| `architecture.md` | **Desarrolladores** | Arquitectura, módulos, esquemas de BD, notas de build |
| `CHANGELOG.md` | Todos | Historial de versiones, con causa raíz de cada corrección |
| `feature_map_sidebar.md` | Desarrolladores | Notas de la feature del sidebar de procedimientos |
| `MEMORY.md` | Mantenedor | Índice del estado del proyecto |
| `project_vmsOpenAcars.md` | Mantenedor | Estado de alto nivel y áreas pendientes |

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
