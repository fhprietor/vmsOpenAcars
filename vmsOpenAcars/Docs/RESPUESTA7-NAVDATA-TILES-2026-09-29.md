# NavData → vmsOpenACars: el proxy de teselas está **en marcha**, y la cláusula de CARTO ya está aplicada

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-29
> **Responde a:** `PEDIDO-NAVDATA-TILES.md`
> **Estado:** clave puesta en el servidor, endpoint **activo y probado**. Sin cambios que os afecten
> en la URL.

---

## 1. Probado de punta a punta (esta misma tarde)

```
1ª petición  /api/v1/tiles/light_all/12/1184/2050.png -> 200, 2.355 bytes, 0,41 s, X-Cache: MISS
2ª petición  (la misma)                              -> 200, 2.355 bytes, 0,007 s, X-Cache: HIT
/api/v1/tiles/dark_all/14/4736/8200.png              -> 200, 1.203 bytes, X-Cache: MISS
```

**58 veces más rápido en el acierto** (0,41 s → 7 ms), y ya no es una estimación: el contador de
marca de agua está a **0**, o sea que la clave sirve teselas de verdad. Los contadores los tenéis en
`GET /api/v1/tiles-stats/` (`served`, `hit`, `miss`, `stale`, `upstream`, `placeholder`, `evicted`,
tamaño en disco).

## 2. La cláusula de CARTO, traducida a lo que hace el servidor

Lo que habéis recibido dice: nada de cachear en el dispositivo del usuario final **más de 30 días**,
y nada de **retener** la caché cuando se deje de usar el servicio. Son dos obligaciones y así están
cubiertas:

| Obligación | Qué hacemos |
|---|---|
| ≤ 30 días de caché | **28 días** en el servidor (un ciclo AIRAC, con margen) y `Cache-Control: public, max-age=2419200` en la respuesta: lo que cachee vuestro cliente también queda dentro |
| No retener al dejar de usar el servicio | `manage.py purge_tiles` borra la caché entera (con `--dry-run` para ver qué borraría), y queda como el paso obligatorio si algún día se apaga el proxy |

**Lo que os pedimos a vosotros:** que **no subáis** ese límite en vuestro lado. Si GMap.NET o vuestra
capa ignoran nuestras cabeceras y cachean en disco del piloto, mantenedlo en **30 días como máximo**
(y si deja de usarse la aplicación, esa caché local también debe poder borrarse). Lo demás —`ETag` +
`304`, `X-Cache` para el diagnóstico— funciona sin que hagáis nada.

## 3. Un aviso sobre los empalmes (§3.1 del otro hilo), por si os afecta

Al unificar el criterio, la **regla de continuidad (giro ≤ 60°) se aplica igual a los huecos y a los
puentes entre componentes**. Efecto medido:

| | componentes | empalmes publicados |
|---|---|---|
| SKBO | 3 | 14 (1 puente, 1 curado) |
| LEMD | 3 | 2 (1 puente) |
| **CYUL** | 2 | **0** ← su único candidato tenía 47,7 m y un giro de **68°**, y queda fuera |
| KMIA / KBOS | 2 / 1 | 0 (el de KMIA es el caso de los muñones de pista, ya explicado) |

Preferimos publicar la regla tal cual y **deciros el hueco** antes que bajar el listón en silencio y
meter un empalme de 68° que quizá no queréis para rodar. Si para vuestro grafo CYUL merece la pena,
decidlo y lo relajamos **sólo para puentes** (≤ 90°), que es una línea de código; la decisión es
vuestra porque afecta a las rutas que sugiere vuestro cliente.
