# Novedades de vmsOpenACARS — versión 0.9.72

**En una frase:** el registro de la compuerta dice ahora también la **Vapp** (la velocidad que te
tocaba según el viento) y el análisis que manda el ACARS incluye el **peso de planeo** del plan, aparte
del real.

---

## La Vapp en el registro

Cuando la compuerta de 1.000 ft juzga la velocidad, ahora deja escritas las tres cifras con las que se
decidió: el **Vref** de tu plan, la **ventana** que se aplicó y la **Vapp**.

La Vapp es la velocidad de referencia **más la corrección por viento en cara**: un tercio del viento,
con un mínimo de 5 nudos y un máximo de 15. Es **informativa**: sirve para leer el veredicto, no para
mover la ventana. La ventana sigue siendo la del Vref —`Vref − 5` a `Vref + 15`— y **no cambia con el
viento**, para que el mismo pilotaje puntúe igual un día de calma que uno de racha.

Ejemplo real, del vuelo que abrió esto (A339, Vref 136, 4,85 kt en cara):

```
⚠️ COMPUERTA DE APROX: Velocidad 137 kts fuera de [131–151] kts (Vref 136 kt, Vapp 141 kt, peso de planeo 400.100 lb) (−5)
```

## El peso de planeo en el análisis

El análisis del aterrizaje que se manda a la web llevaba el peso **real** medido en la toma. Ahora lleva
también el **peso de planeo** del OFP, que es el peso al que pertenece el Vref. Los dos van juntos y
separados: uno dice cuánto decía el plan y el otro cuánto pesaba el avión de verdad. Nada que configurar.

## Lo que hay que saber

- Las dos cosas necesitan que tu OFP traiga el análisis de aterrizaje de SimBrief (lo normal). Sin él,
  ni Vref ni peso de planeo: el registro queda exactamente como antes.
- **La puntuación no cambia por la Vapp**: es un dato para leer, no un criterio.
- El resto del vuelo, la compuerta y el análisis, igual que en la 0.9.71.
