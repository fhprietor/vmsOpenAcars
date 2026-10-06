# Despacho de SimBrief: hecho y verificado contra el servidor

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 6 de octubre de 2026 · **Responde a:** «Despacho de SimBrief desde vmsOpenACars»
> **Estado:** el cambio del cliente está publicado (**v0.9.29**) y comprobado en vivo

---

## El cambio

El botón **PLAN IN SIMBRIEF** ya **no monta la URL**: pide el despacho a
`GET /api/flights/{id}/dispatch?aircraft_id=…` y **abre `simbrief.url` tal cual**, sin añadir ni quitar
un solo parámetro. La llamada va **al pulsar el botón**, no al seleccionar el avión, como pedían.

El constructor local queda **sólo como respaldo**, y sólo cuando el endpoint **no contesta**: `5xx`,
timeout, red, `200` sin URL, o **`404` sin `flight_not_found`** —que es exactamente su detección de
servidor viejo—. **Los errores de negocio no caen al respaldo**: `401`, `404 flight_not_found`,
`422 aircraft_not_found` y `403 aircraft_not_allowed` tienen cada uno su aviso, en los dos idiomas.

**Un matiz sobre el `extrarmk`, y lo decimos en vez de esconderlo**: en el camino principal **no
enviamos el nuestro** —el Item 18 lo pone el servidor y sumar el nuestro daría dos, como avisaban—,
pero el **respaldo lo conserva**. Contra un phpVMS viejo nadie más lo pondría y el plan se quedaría
sin Item 18. Si prefieren que se vacíe del todo, lo hacemos; nos pareció peor perder el Item 18 en el
único caso en que el respaldo se usa.

## Checklist, punto por punto

| # | Punto | Resultado |
|---|---|---|
| 1 | La URL abre SimBrief con PAX y carga sugeridos | ✅ `pax=166`, `cargo=7991` |
| 2 | El nivel va en pies (`fl=35000`, no `fl=350`) | ✅ **`fl=33000`** en el vuelo probado |
| 3 | El Item 18 aparece en el plan | ✅ `extrarmk=CS/VHOLAR IVAOVA/VHR OPR/VHR` |
| 4 | Los mapas son `detail` | ✅ `maps=detail` — **el nuestro era `detailed`**; corregido |
| 5 | Un avión de otra subflota devuelve 403 | ✅ ejercitado, y el cliente muestra su aviso |
| 6 | Un charter/ferry abre SimBrief **sin** PAX/carga | ⏳ **no verificable hoy**: el único vuelo del piloto es regular, así que no hemos visto un `applicable: false` real (el código ya no lo pinta como error) |
| 7 | Con el endpoint caído, el cliente sigue despachando | ⏳ probado en **test** con respuestas simuladas, no con un servidor viejo real |
| 8 | `static_url` no existe y `fl` no está mal escalado | ✅ `static_url` **eliminado**; `fl` nunca estuvo escalado (los `/100` del repo son de presentación) y un test lo prohíbe |

**Probado en vivo** (vuelo real `SKBG→SKCG`, avión 36): `401` con clave mala, `404` de vuelo
inexistente, `404` de ruta no desplegada, `422` con avión `999999` y sin `aircraft_id`, y `403` con
dos aviones de subflota no permitida. Los `notes[]` que devuelven (plazas recortadas 178→166 por
elevación y temperatura, margen +20,0 %) se muestran **tal cual**, con su color.

**Lo que no pudimos verificar, y lo decimos**: `simbrief.params.route` no apareció —el vuelo probado
tiene `route=''` en phpVMS, coherente con «sólo si el vuelo la tiene»—, ningún caso real de
`applicable: false`, ningún servidor viejo de verdad, y **la apertura del navegador desde la
interfaz**: eso necesita un vuelo con piloto delante.

## Petición

Cuando alguien vuele con la 0.9.29, confírmanos los puntos 1, 3 y 6 con el plan ya generado —sobre
todo el **6**, que es el único que no hemos podido ver— y el §10 (adjuntar el OFP al PIREP) lo
dejamos para su propia conversación, como proponen.