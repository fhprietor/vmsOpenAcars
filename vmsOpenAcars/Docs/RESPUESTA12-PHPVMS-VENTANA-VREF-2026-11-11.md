# Respuesta (12) al equipo de phpVMS — la compuerta usa el Vref, y el registro ya se puede auditar

> **De:** equipo de vmsOpenACars · **Para:** equipo de phpVMS
> **Fecha:** 11/11/2026
> **Respecto a:** vuestro informe «la compuerta de aproximación usa una ventana fija y contradice la
> Vref del propio cliente» (PIREP ESSA→LIRF, A339, `vref_kt = 136`, `landing_weight_lbs = 400.100`)
> **Estado:** **teníais razón y está corregido en la 0.9.71.** Ventana dependiente del peso, Vref y peso
> de planeo en la propia línea del veredicto, y respuesta sobre los heavies sin fila.

---

## 1. Sí: la ventana era del tipo, no del peso

La compuerta pedía la banda a una **tabla de categorías PANS-OPS** (`ApproachSpeedRange`): cualquier
wide-body cae en Cat D → **[140–185] kt**, sea un A339 a 400.000 lb o vacío. El `vref_kt` que os
mandamos desde la 0.9.38 **no lo leía nadie**. Con Vref 136, la banda estaba **3 kt por encima de la
velocidad de referencia**: un piloto a Vref+1 no podía cumplirla. El −5 era imputable al cliente, no
al vuelo.

## 2. Qué cambia en la 0.9.71

- **Con Vref del plan: `[Vref − 5, Vref + 15]`.** Es la ventana que pedisteis, sin más adornos.
- **Sin Vref** (plan sin análisis de aterrizaje, o vuelo retomado —no hay OFP que releer—): la tabla por
  tipo **exactamente como estaba**. Un dato que falta no puede inventarse ni suprimir la comprobación.
- **Vref implausible** (fuera de 60–200 kt: campo en otra unidad, plan corrupto) → también tabla. No
  construimos una ventana `[0–20]` alrededor de un número que no es un Vref.
- **El indicador STABLE/UNSTABLE del cliente lee la misma fuente**, así que ya no puede decir «estable»
  mientras el gate cobra, ni al revés.

**Lo que no ha cambiado, y conviene que quede escrito:** seguimos **sin derivar** un Vref del tipo y el
peso. Lo que cambia es que la **ventana** usa el Vref que publica SimBrief — un dato del plan, a su peso
de planeo —. Nuestra respuesta (7) sigue en pie.

**Sobre el viento:** no hay término de viento en la ventana. El `+15` ya absorbe la corrección de Vapp
(1/3 del viento en cara; con viento fuerte son 10–15 kt) y mover la ventana con la meteorología haría
que el mismo pilotaje puntuara distinto según el día. **Tampoco emitimos un campo `Vapp`**: no tenemos
política de aditivo y no íbamos a inventarla. Si lo queréis explícito, decidnos la fórmula (¿`1/3` del
viento en cara, mínimo 5, máximo 15?) y sale en la misma línea.

## 3. El log, ya auditable

El renglón del criterio y el del veredicto llevan ahora el Vref, la ventana y el peso **de planeo** (el
peso al que pertenece ese Vref: `tlr.landing.conditions.planned_weight`, convertido a libras; medido en
vuestro propio hilo, `247261` con la tabla en `LDW 2400/2480/2560` está en **kg**).

```
⚠️ COMPUERTA DE APROX: Velocidad 137 kts fuera de [131–151] kts (Vref 136 kt, peso de planeo 400.100 lb) (−5)
⚠️ COMPUERTA DE APROXIMACIÓN (1000 ft AGL): INESTABILIZADA — 5 pts deducidos · Vref 136 kt, ventana 131–151 kt, peso de planeo 400.100 lb
```

En inglés, las mismas tres claves (`Log_ApproachGateSpeed`, `Log_ApproachGateUnstable`,
`Log_ApproachGateOk`). **Sin Vref no se añade nada**: las líneas quedan literalmente como estaban.

## 4. Los heavies sin fila de aterrizaje

**`metrics` se manda en todos los PIREPs desde la 0.9.38** (11/10/2026, cuando se desplegó
`POST /pireps/{id}/landing`): la subida es incondicional al filear (`pirep_landing_analysis_enabled`,
encendido por defecto) y **no depende de nuestro log local**. En los 60 PIREPs más recientes de nuestro
entorno hay clientes desde la **0.9.1**, así que lo más probable es que esos heavies se volaran con un
cliente **anterior a la 0.9.38** y no sea una regresión.

**Lo que no pudimos comprobar, y lo decimos:** no hemos podido listar vuestros 15 heavies. `GET
/api/pireps?limit=60` es el máximo sin filtro y **100 o más devuelve 503**; `page=2` repite las mismas
60, así que no hay paginación. Con el filtro por piloto solo vemos nuestros propios 25. **Pasadnos los
IDs (o el `id` de piloto) y os decimos cuáles son y de qué versión vienen.**

**Lo que sí queda como hueco real:** los `flaps_*` del payload salen de la traza del flare, así que un
vuelo **sin traza** los manda `null`. Y `vref_kt` va `null` cuando el plan no trae TLR, por la decisión
de la respuesta (7). Mandar el ajuste de flaps del toque **sin depender de la traza** está en la lista.

## 5. Lo que no cambia

- El **peso real medido** sigue en `landing_weight_lbs`; el de planeo es otro dato y solo va al log.
- **Ningún otro criterio** de la compuerta (descenso, banco, cabeceo, tren, flaps) se toca.
- **Las sanciones ya emitidas no se re-puntúan**: el cliente no reenvía PIREPs.

## 6. Estado

En **0.9.71**: compila en Debug y Release, **1124 tests** (1123 pasan, 1 se omite sola). Los PIREPs
volados a partir de esa versión son los que traen la ventana por peso y el Vref en la línea. Mientras
tanto, vuestra decisión de marcar esas sanciones **«a revisión»** es la correcta.

## 7. Lo que os pedimos

1. **La fórmula de Vapp**, si la queréis en el log (apartado 2). Sin ella, la ventana `[Vref − 5, Vref + 15]`
   es autosuficiente y auditable.
2. **¿Os sirve el peso de planeo como campo aparte** (`planned_landing_weight_lbs`)? Hoy solo va al log.
   Si lo queréis en `metrics`, es aditivo y lo mandamos; si os basta con el medido, no lo añadimos.
