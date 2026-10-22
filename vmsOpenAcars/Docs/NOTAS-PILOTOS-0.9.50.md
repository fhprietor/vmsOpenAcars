# Novedades de vmsOpenACARS — versión 0.9.50

**En una frase:** el registro y los avisos de cabina decían cosas que no eran —turbulencia en pleno
ascenso, «pueden moverse por la cabina» dos minutos después del despegue, spoilers parpadeando decenas de
veces y reversas «0 %» justo cuando no podían tener dato— y ya no las dicen.

---

## Qué cambia

- **Turbulencia: se mide la oscilación, no el desvío.** Un ascenso acelerando da `g` por debajo de 1 de
  forma sostenida y eso se estaba anunciando como zona de turbulencia. Ahora hacen falta **oscilaciones**
  (tres cruces en seis segundos) y la tasa vertical no decide nada.
- **«Pueden moverse por la cabina» solo en crucero.** Es un permiso: en subida la tripulación va amarrada
  y en descenso la cabina dice justo lo contrario.
- **Spoilers: se lee la posición.** El cliente los trataba como un interruptor, así que el **0,006 %** de
  recorrido —o simplemente **tenerlos armados**— contaba como «EXTENDIDOS». Adiós a las 26 líneas en 44
  segundos de final. Y el `Spoilers: 100 %` del bloque de aterrizaje ahora es la posición **real**.
- **Reversas: el aviso sale cuando se despliegan.** Antes se publicaba `0 %` en el instante del toque —con
  las reversas todavía guardadas— y el aviso de verdad llegaba **44 s después**, al entrar en el rodaje.
  El umbral de 0,6 % (que no filtraba nada) pasa a **10 %**.
- **Una desviación del localizador, un punto.** Un solo apartamiento de −5,1° que duró segundo y medio
  gastaba el tope entero de −2. Ahora se cuenta una vez y se rearma al volver al eje.
- **Menos ruido en el registro:** una línea por cambio de tren y de spoilers, y la «Velocidad de Rotación»
  ya no sale dos veces con números distintos.

## Lo que hay que saber

- **No hay nada que configurar.** Todo lo de arriba viene activado.
- **Los vuelos ya enviados no cambian de nota.** Esto solo afecta a los vuelos nuevos.
- **Si un avión no publica la posición de los spoilers**, el registro no dirá «EXTENDIDOS» en vez de
  inventarlo: los aviones de MSFS que publican los offsets estándar de FSUIPC los dan sin problema.
- **Sigue sin oírse el letrero de cinturones.** La turbulencia se mide con lo que el avión siente (factor
  de carga), porque el avión no publica la señal de cabina; el texto nunca dice «el comandante ha
  encendido la señal».
- Todo esto sale del vuelo **KSEA→SKRG del 21/10/2026** (PIREP `5ow69vzDNW5ZMayB`): es su registro y sus
  cifras, no una prueba de laboratorio.
