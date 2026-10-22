# Novedades de vmsOpenACARS — versión 0.9.51

**En una frase:** el registro llevaba tiempo diciendo dos cosas que no eran —que el piloto automático
estaba puesto y que el autobrake estaba en RTO— y ahora dice lo que el avión publica de verdad, más una
línea de diagnóstico para esta prueba.

---

## Qué cambia

- **Piloto automático.** Se deducía de un offset que en realidad es **el rumbo del bug**, así que salía
  «AP ENGAGED» prácticamente siempre, también volando a mano, y el modo era siempre `HDG ALT`. Ahora se
  leen las **banderas de modo** del avión (NAV, HDG, ALT, VS, IAS, APR, GS…) y el anuncio dice lo que
  hay: `NAV+ALT`, `ILS+GS`, etc.
- **Soltar el AP exige sostenido.** Antes bastaba un parpadeo de una muestra para cantar
  «AP DISENGAGED» a 200 ft, en plena final.
- **Autobrake.** El registro decía `RTO` **en todos los vuelos**, se hubiera puesto RTO, MAX o nada
  (se leía un offset que no es el selector y encima con dos valores invertidos). Ahora sale el ajuste
  real —`RTO`, `OFF`, `1`, `2`, `3`, `MAX`— y `---` cuando el avión no demuestra publicarlo.
- **Carteles de cabina.** El cliente ya lee la señal de **cinturones** y la de **no fumar**. Todavía
  **no cambia ningún anuncio**: primero hay que ver qué aviones los publican de verdad.
- **Una línea `🛠` al entrar en crucero y otra en aproximación** con todo lo que el avión publica del
  panel. Es el motivo de esta versión.

## Lo que hay que saber

- **Esta versión es para probar.** La línea `🛠` es información de diagnóstico, no un aviso al piloto.
- **Qué hacer en el vuelo** (para que los offsets se muevan):
  - **Autobrake:** RTO antes del despegue → **OFF** al despegar → **2 o 3** antes de aterrizar → OFF en
    el rodaje.
  - **Piloto automático:** encender con HDG+ALT, pasar a LNAV/VNAV o HDG/VS, y **APP** antes del FAF.
  - **Carteles:** encender y apagar una vez el de cinturones y el de no fumar.
- **Si en la línea `🛠` sale `publicado=no`**, ese avión no escribe esos offsets: el cliente se calla y
  no inventa el dato. **Mandad el log** de este vuelo: con él se afinan los umbrales que falten.
- **Los vuelos ya enviados no cambian de nota.**
