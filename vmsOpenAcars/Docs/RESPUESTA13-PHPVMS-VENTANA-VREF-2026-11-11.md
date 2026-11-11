# Respuesta (13) al equipo de phpVMS — cruzado con nuestro lado, y las dos cosas hechas

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/11/2026
> **Respecto a:** vuestra confirmación de los heavies, los 13 IDs y las dos respuestas (Vapp y peso de planeo)
> **Estado:** **las dos cosas están hechas** y salen en la **0.9.72**. Y hemos cruzado el caso real contra
> vuestra base: la sanción de `Rpe7RLqkYmg4PanO` es exactamente lo que decíais.

---

## 1. El caso real, confirmado con sus datos y con los nuestros

`GET /api/pireps/Rpe7RLqkYmg4PanO` → ESSA→LIRF, **A339**, `vmsOpenACars/0.9.46`, score **62**. Sus
`metrics`: **`vref_kt` 136**, **`landing_weight_lbs` 400.099,69**, `wind_dir_deg` 109,98,
`wind_speed_kt` 8, `runway_true_deg` 162,68, `flaps_pct` 100.

Y en su log, a las 19:44:27:

```
⚠️ COMPUERTA DE APROX: Velocidad 137 kts fuera de [140–185] kts (−5)
⚠️ COMPUERTA DE APROXIMACIÓN (1000 ft AGL): INESTABILIZADA — 5 pts deducidos
```

Con esos datos: en cara = `8 · cos(109,98 − 162,68)` = **4,85 kt** → **Vapp 141**, que es justo la
cifra que nos disteis. El piloto a 137 iba **Vref+1**, dentro de `[131–151]`. Con la 0.9.72 ese vuelo
habría puntuado **67** en vez de 62.

## 2. La Vapp, hecha como la pedisteis (informativa, sin mover la ventana)

`Helpers/ApproachSpeedWindow.Vapp`: **`Vref + clamp(round(viento en cara / 3), 5, 15)`**. Sale en las
dos líneas del veredicto:

```
⚠️ COMPUERTA DE APROX: Velocidad 137 kts fuera de [131–151] kts (Vref 136 kt, Vapp 141 kt, peso de planeo 400.100 lb) (−5)
⚠️ COMPUERTA DE APROXIMACIÓN (1000 ft AGL): INESTABILIZADA — 5 pts deducidos · Vref 136 kt, ventana 131–151 kt, Vapp 141 kt, peso de planeo 400.100 lb
```

Tres detalles para que no haya sorpresas al leerlo:

1. **No mueve el criterio.** La ventana sigue siendo `[Vref − 5, Vref + 15]` y no depende del viento,
   como acordamos. Hay un comentario en el código que lo dice explícitamente para que nadie lo «mejore»
   después.
2. **Redondeo alejándose del cero**, no el bancario de .NET: 19,5 kt → 6,5 → **7** (el bancario daría 6).
   Con vuestro 4,85 no se nota porque cae en el suelo de 5; se nota por encima de 15 kt.
3. **El viento es el del simulador en el ciclo de la compuerta**, no el del METAR de llegada. Si el
   simulador lo diera en referencia magnética en vez de verdadera, el error de la variación local
   movería el aditivo como mucho ~1 kt —y cae en un suelo de 5—, así que en un dato informativo es
   irrelevante. Sin viento o sin Vref, la línea no se añade.

## 3. `planned_landing_weight_lbs`, en `metrics`

Va en el payload, **aparte** de `landing_weight_lbs` y sin sustituirlo:

```json
"vref_kt": 136,
"landing_weight_lbs": 400100,
"planned_landing_weight_lbs": 400100
```

Es el `tlr.landing.conditions.planned_weight` del OFP convertido a libras (la unidad del bloque TLR es
la del plan; con el vuestro, `247261` junto a la tabla `LDW 2400/2480/2560`, son **kg**). `null` sin
TLR. También se guarda en nuestro log local, para que el relleno histórico lo lleve.

## 4. Los heavies

Con vuestra lista queda cerrado por los dos lados: 2 en curso, 3 de 0.9.30/0.9.33/0.9.36 sin fila y
los otros 10 con métricas. **No hay regresión** y no tenemos nada que arreglar ahí.

Un aviso de cortesía, porque nos pasó a nosotros al cruzar: `GET /api/pireps/{id}/landing` devuelve el
sobre en **`attrs`**, no en `data`. Nuestro primer barrido daba «sin fila» en todos los vuelos por eso;
con `attrs` salen todos. No es un fallo vuestro —el contrato está bien—, es que leímos el campo
equivocado.

## 5. Lo que queda abierto de nuestro lado

- Los `flaps_*` del payload siguen saliendo de la traza del flare: un vuelo **sin traza** los manda
  `null`. Mandarlos siempre, sin depender de la traza, está en la lista (requiere guardar el ajuste del
  toque en el vuelo).
- Nada más. El resto del contrato está como lo dejamos en la (7) y la (11).

Gracias por el detalle de los IDs: ha servido para cerrar el caso con números en vez de con
impresiones.
