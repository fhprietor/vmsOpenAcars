# Novedades de vmsOpenACARS — versión 0.9.71

**En una frase:** la compuerta de los 1.000 ft ya no te cobra por volar a **tu** Vref — la ventana de
velocidad sale del Vref de tu plan, que depende del peso, y el registro dice con qué Vref y a qué peso
te juzgó.

---

## La velocidad de la compuerta de 1.000 ft

La compuerta comparaba tu velocidad con una **tabla por tipo de avión**: cualquier avión grande caía en
la misma banda, **[140–185] kt**, pesara 330 o 400 toneladas. Pero el ACARS ya conoce el **Vref de tu
OFP** (el del análisis de aterrizaje de SimBrief, que es del peso de planeo) y lo envía a la web.

El caso que lo destapó, reportado por la aerolínea: un **A339** con **Vref 136 kt** y 400.100 lb de
peso. El piloto iba a **137 kt —Vref + 1—**, que es exactamente donde debe ir, y la banda le cobró
**−5 puntos** por «fuera de [140–185]». La banda estaba 3 kt por encima de su propio Vref: **no había
forma de cumplirla** sin volar por encima de la velocidad de referencia.

Ahora, cuando el plan trae Vref, la ventana es **`[Vref − 5, Vref + 15]`** (la que pidió el equipo de
phpVMS). El margen de arriba es ancho a propósito: cubre el viento en cara sin mover la ventana con la
meteorología. **Sin Vref** —un plan sin análisis de aterrizaje, o un vuelo retomado— la ventana sigue
siendo la de siempre, sin cambios.

## El registro, ahora auditable

Antes solo decía «fuera de [140–185]». Ahora dice de dónde sale la ventana y con qué cifras:

```
⚠️ COMPUERTA DE APROX: Velocidad 137 kts fuera de [131–151] kts (Vref 136 kt, peso de planeo 400.100 lb) (−5)
⚠️ COMPUERTA DE APROXIMACIÓN (1000 ft AGL): INESTABILIZADA — 5 pts deducidos · Vref 136 kt, ventana 131–151 kt, peso de planeo 400.100 lb
```

El peso es el **del plan** (`planned_weight` del análisis de aterrizaje), no el real de la toma: es el
peso al que pertenece ese Vref. El peso real medido sigue yendo aparte, como siempre.

## Lo que hay que saber

- Nada que configurar: se aplica solo si tu OFP trae el análisis de aterrizaje (lo normal en SimBrief).
- **Las notas de los vuelos nuevos pueden subir** en aviones grandes: antes se cobraba una velocidad
  que en muchos casos era la correcta.
- Los vuelos **ya enviados no cambian**: esto se aplica de aquí en adelante.
- El resto de la compuerta (descenso, banco, cabeceo, tren, flaps) y el resto del vuelo, igual.
