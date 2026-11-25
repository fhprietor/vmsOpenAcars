# Novedades de vmsOpenACARS — versión 0.9.80

**En una frase:** dos avisos que saltaban por el ruido del instrumento dejan de saltar, y tres
detalles del registro que decían cosas que no eran.

---

## 1. Ya no se penaliza por 7 fpm ni se avisa por 1 pie

En el último vuelo salieron estos dos:

```
⚠️ BELOW MINIMUMS: 206 ft MSL < DA 207 ft     ← por un pie
⚠️ APPROACH GATE: Excessive descent -1007 fpm (−5)   ← por 7 fpm sobre el límite de 1000
```

Ninguno de los dos era una violación: son la resolución del altímetro y del variómetro. Ahora hay una
banda de margen:

- **Mínimos**: solo avisa si bajas **más de 15 pies** por debajo de la altitud de decisión.
- **Descenso excesivo**: el límite efectivo pasa a **−1.050 fpm** en vez de −1.000.

**Esto cambia la puntuación, y es a propósito**: los descensos entre −1.001 y −1.050 fpm dejan de
costar 5 puntos. Un descenso de −1.200 sigue contando igual. El resto de criterios de la compuerta
—velocidad, alabeo, cabeceo, tren y flaps— no se tocan.

## 2. El aviso de NavMap desaparece

`⚠️ NavMap: runway not found for…` venía de la integración con LittleNavMap, que solo funciona si
tienes esa base de datos instalada. Ya no sale en el registro, y tampoco saldrá la versión de
despegue, que era la siguiente que aparecería.

## 3. Dos textos que decían lo contrario de lo que mostraban

- El aviso de velocidad de la compuerta decía que «el Vref del plan no se parece al del avión» y
  luego enseñaba **el mismo número**. Ahora dice lo que pasa de verdad: cuántos nudos ibas por encima
  del Vref del plan, y que por eso no se evalúa.
- El registro de flaps a veces decía «desde 62 %» cuando la posición anterior era 75 %. El «desde» es
  ahora siempre el último valor anunciado.

## Sin cambios

No se toca el resto: ni el envío del PIREP, ni los anuncios, ni la guía de rodaje, ni el análisis del
aterrizaje.