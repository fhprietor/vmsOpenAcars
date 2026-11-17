# Novedades de vmsOpenACARS — versión 0.9.73

**En una frase:** si tu plan de vuelo entra en un espacio aéreo **restringido o prohibido**, el ACARS te
lo dice **antes de despegar** —con el área, sus límites y el tramo exacto— y deja constancia en el PIREP
si decides volar igualmente.

---

## Aviso de ruta antes de conectar

Al pulsar **START**, después de prefilar, el ACARS comprueba la ruta del OFP contra los espacios aéreos
y, si hay un cruce, abre un aviso con estas tres líneas:

```
Área:   SKR11 / BARRANQUILLA
Límites: GND–FL999
Tramo:  KAGEM–EVPIP
```

Es el dato que necesitas para corregir el plan en SimBrief, y llegas a tiempo: en ese momento todavía
estás en tierra.

Dos salidas:

- **CORREGIR PLAN** — no inicia el vuelo. Vuelves a SimBrief, arreglas la ruta y cargas el OFP nuevo.
- **RUTA REVISADA** — sigues con el vuelo y el PIREP lo registra, para que quien lo revise sepa que
  fuiste advertido.

Y si cierras la ventana con la ✕, el vuelo **sigue** y no queda constancia: no has dicho que la
revisaras.

## Lo que hay que saber

- **El aviso es solo un aviso: no bloquea nada.** Ni la conexión, ni el vuelo, ni el envío del PIREP.
  Lo único que puede no iniciar el vuelo es pulsar «CORREGIR PLAN», y eso lo decides tú.
- **Solo cuenta lo restringido y lo prohibido.** Las zonas de **peligro** (`Danger`) se avisan en
  pantalla y en el mapa, pero **no** mandan el vuelo a revisión: de los 10 PIREP que se marcaron por
  esto, 2 eran de ese tipo y no procedía.
- **La altitud se tiene en cuenta.** Sobrevolar no es invadir: si el tramo va por encima del techo de
  la zona, no hay aviso. Sin dato de altitud, se avisa igual.
- El aviso puede venir del servidor (con tu OFP) o de la comprobación del propio ACARS; la ventana dice
  cuál de las dos, para no confundirlos al revisar el vuelo.
- Todo lo demás —compuerta, scoring, análisis del aterrizaje— igual que en la 0.9.72.
