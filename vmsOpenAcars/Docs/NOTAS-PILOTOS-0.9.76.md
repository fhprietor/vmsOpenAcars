# Novedades de vmsOpenACARS — versión 0.9.76

**En una frase:** desde la 0.9.72, la ruta se revisa **antes de despegar**, el aterrizaje deja de
contradecirse, la altura se mide **de ruedas** y la pista de la toma llega con su umbral, su TCH y su
senda.

---

## 0.9.73 — la ruta se revisa antes de despegar

El ACARS manda el **navlog** del plan en el prefile y recibe el aviso de zonas restringidas. Si el plan
cruza una —el caso que lo motivó fue **SKR11 / BARRANQUILLA, GND–FL999**—, el aviso sale **antes de
conectar**, con dos salidas: **«RUTA REVISADA»** sigue y deja constancia, **«CORREGIR PLAN»** no inicia
el vuelo. Es un aviso previo, no una puerta: cerrar la ventana continúa sin acuse. La comprobación
local va **por tramos del navlog**, no por muestras, porque una zona de 72 × 53 nm se cruza con los
dos extremos de la pierna fuera.

## 0.9.74 — el aterrizaje deja de contradecirse

- El **G del toque** ya no dice «Normal» mientras te descuenta: el rótulo y los puntos salen de **la
  misma tabla**.
- Los **avisos de pista** solo suenan yendo **hacia el punto de espera** (cono de morro de 45°), no
  rodando en paralelo.
- La **reversa fantasma** del rodaje —un 100 % con el avión ya rodando— se ignora; el despliegue real
  del toque sigue contando.
- **`ON BLOCK` en 5 s** con el freno de parqueo puesto y los motores parados.
- La **Vapp nunca baja del Vref** de referencia y se marca **«(del plan)»** cuando el Vref es del plan;
  si la velocidad se explica por otra configuración de flaps, el criterio de velocidad no se aplica
  en vez de penalizarte.

## 0.9.75 — la altura del aterrizaje, de ruedas

El AGL dejó de ser «altitud menos la elevación del plan». Ahora viajan, por cada muestra, la altitud
real (`alt_msl_ft`) y la **altura de las ruedas** (`radio_alt_ft`), con el **cero del radar medido en
cada vuelo**: **0,41 ft** en el ToLiss A320, **12,15** en el Fenix, **8,05** en el B738 de la primera
prueba. La **puerta de 10 ft mide 10**. En tierra el AGL ya no se fuerza a 0 —puede ser negativo por
el desfase del aeropuerto— y en un vuelo retomado va **nulo** en vez de confundir MSL con AGL. Un
**go-around** ya no vuelve a la altitud del origen. El análisis recibe además **umbral, extremo, TCH
y senda** de la pista.

## 0.9.76 — la pista de la toma y el toque en la traza

El primer vuelo de la 0.9.75 llegó con la pista **sin resolver**: el aviso de «toque» del simulador
llegó **33 s tarde**, cuando el avión ya estaba pasado el extremo, y el análisis se quedó sin umbral,
sin TCH y sin distancia de toma. Ahora la pista se resuelve con la **posición del primer contacto**:
en SKBG 35, umbral **3.854**, TCH **`displaced_threshold`**, senda **3,0°** y distancia de toma real.
La traza incluye la **muestra del toque** (`on_ground = true`, radar de ruedas ≈ 0) y el **G del frame
del toque** ya no se pierde.

## Lo que hay que saber

- **No hay nada que configurar.**
- **Los vuelos anteriores no se reescriben**: su traza y su análisis se quedan como estaban.
- **Los umbrales de puntuación no cambian.** Lo que cambia es de dónde salen los números y cuándo un
  criterio no se aplica por falta de un dato fiable.
- **La 0.9.76 es la primera que trae la pista y el toque juntos**; el primer vuelo con ella cierra la
  verificación: debe traer **3.854**, `displaced_threshold`, **3,0°** y una distancia de toma distinta
  de 0.
