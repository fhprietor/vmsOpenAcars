# vmsOpenACars — lo que hace tu ACARS

Bienvenido. Esto no es una lista de cambios: es **lo que el programa hace por ti**, con especial
atención a las partes que no vas a encontrar en un ACARS cualquiera. Si es tu primer vuelo, empieza
por [la guía de primeros pasos](GUIA-PILOTO-PRIMEROS-PASOS.md) y vuelve aquí después.

---

## 1. Lo primero: no hay nada que configurar

Abres el programa, entras con **el usuario y la contraseña de la web de tu aerolínea**, y ya está. El
cliente pide su clave sola y la guarda **cifrada en tu equipo**, atada a tu cuenta de Windows.

- **No hay que copiar ninguna clave** ni pegarla en ningún sitio.
- **Tu contraseña no se guarda**: se usa para entrar y se olvida.
- La clave **no viaja a otro equipo**: copiar la carpeta no se la lleva.

> **Si vienes de CrewSystem** tu cuenta ya existe pero no tiene contraseña. Antes de nada entra en
> https://vholar.co/login, pide el enlace con el email de la aerolínea y crea una. Está explicado paso
> a paso en la [guía de primeros pasos](GUIA-PILOTO-PRIMEROS-PASOS.md).

## 2. Lo que hace solo, sin que toques nada

Enciendes el simulador, pulsas START y el cliente se encarga:

- **Detecta las fases** del vuelo —pushback, rodaje, despegue, ascenso, crucero, descenso,
  aproximación, toma, rodaje de llegada, aparcado— con márgenes pensados para no confundirse con la
  turbulencia ni con un pico de velocidad en el rodaje.
- **Apunta el vuelo** y lo envía a phpVMS en forma de PIREP, con tu puntuación.
- **Te avisa en pantalla** de lo que va pasando: cambios de fase, penalizaciones, exceso de
  velocidad, espacios aéreos, y una **tarjeta del toque** con la tasa de descenso, la G y tu actitud.
- **Trae el METAR** del destino y del origen, y contrasta el QNH que llevas con el real.
- **Se conecta a SimBrief** para cargar tu plan de vuelo y leer tu Vref.

Y si aterrizas en un aeropuerto **distinto del planeado** —un desvío de verdad, una emergencia, un
regreso—, lo detecta y **corrige el PIREP** para que no te puntúe contra un destino al que nunca
llegaste.

## 3. Las cuatro cosas que no hace un ACARS normal

### La traza fina del aterrizaje, a 10 Hz

La mayoría de los ACARS guardan una posición cada pocos segundos. Con eso, el último tramo de la
aproximación son cuatro puntos. Este guarda **una traza aparte del toque**, a **10 Hz** (unas 8
muestras reales por segundo), desde **1.500 ft antes del umbral hasta dos segundos después del
contacto**.

Esa traza es la que permite ver *cómo* aterrizaste, no solo *cuánto*: si el avión se hundió al final,
si llegaste plano, si cortaste la potencia tarde.

### El análisis del aterrizaje, con el toque dibujado

La ventana de análisis pinta cuatro gráficos del último tramo —perfil vertical, desviación lateral,
velocidad y tasa de descenso— y un **closeup del toque** con la pista, el umbral, las bandas de la
zona de toma y **el punto exacto donde tocaste**.

Y añade dos cosas que no se ven en el resto:

- **Los flaps y el corte de potencia.** Se guarda el detent real del mando de flaps y la potencia de
  los motores en cada muestra, así que el gráfico dice **cuándo cortaste** y con cuánto N1, no una
  estimación.
- **Una cabecera de datos** con el avión, el peso de despegue, el viento y la versión, más un botón
  **PNG** para que puedas **compartir el gráfico** tal cual.

Todo eso se sube también a tu aerolínea, así que no es un dibujo local: es tu vuelo, analizado.

### La revisión de ruta **antes** de despegar

Esto nació de un caso real: un plan de SimBrief que entraba en un espacio aéreo restringido. El ACARS
lo detectó **en vuelo**, y para entonces ya no había nada que corregir.

Ahora, **antes de que despegues**, el cliente revisa la ruta del plan contra los espacios aéreos
restringidos, **tramo por tramo** —no por muestras: una zona estrecha puede cruzar la ruta con los dos
extremos fuera— y te avisa con el área, sus límites y el tramo exacto. Tú decides: seguir (y queda
registrado que lo revisaste) o corregir el plan, que es cuando aún sirve de algo.

### El RAAS y la guía de rodaje, con voz

Te avisa como un RAAS de verdad: **`APROXIMANDO PISTA 14R`**, **`ESPERA ANTES DE PISTA`**,
**`CALLE M A LA DERECHA EN 120 METROS`**, **`GIRA AHORA`**, **`FUERA DE RUTA`**. Con voz del sistema,
sin instalar nada.

Y no es una cinta fija: **sugiere la ruta por las calles del aeropuerto**, con la pista que elijas, y
**tú puedes editarla** si ATC te dio otra. Los avisos se dicen **una sola vez por maniobra**, no cada
pocos segundos.

## 4. La puntuación: 17 criterios, no solo la tasa de descenso

Un ACARS que solo mide los pies por minuto te cuenta la mitad de la historia. Aquí se puntúa:

| | |
|---|---|
| **El toque** | tasa de descenso, **fuerza G medida** (no deducida), alabeo, cabeceo |
| **La aproximación** | si llegaste estabilizado a 1.000 ft, y **a qué velocidad respecto a tu Vref** |
| **La pista** | zona de toma **proporcional a la longitud de la pista**, desviación del eje |
| **Las ayudas** | localizer, mínimos de la aproximación |
| **La configuración** | luces, exceso de velocidad, velocidad en las restricciones de la SID/STAR |
| **Los motores** | establecidos al entrar en pista, y **bonificación por rodar con un solo motor** |
| **La salida** | QNH contra el METAR, presión estándar al cruzar la altitud de transición, puntualidad |

Cada penalización aparece en el registro con su motivo y sus puntos, así que no hay notas que
aparecen de la nada.

## 5. La cabina, los avisos y el mapa

- **Anuncios de cabina en trece momentos** del vuelo, con la voz de Windows si no hay grabación,
  **en tu idioma**, y con un aviso específico cuando hay **turbulencia** de verdad (medida por las
  oscilaciones del avión, no por la racha de viento).
- **Mapa en movimiento** con la ruta, los **espacios aéreos** a color y el **ATC de IVAO** que esté
  en tu zona, con sus frecuencias.
- **Avisos en pantalla (OSD)** que no roban clics al simulador y se pueden apagar sin dejar de
  registrarse en el vuelo.

## 6. Funciona con tu simulador, sea cual sea

MSFS 2020 y 2024, Prepar3D, FSX y **X-Plane**. En X-Plane el tipo de avión se lee **del propio
avión** (de su archivo `.acf`), no del nombre de la carpeta, que muchas veces miente: un A320 que en
realidad es un **A20N** se identifica como A20N.

## 7. Y tu privacidad

- Tu **contraseña no se guarda** en ningún sitio, ni en el registro, ni en la telemetría.
- Tu **clave de acceso** se guarda **cifrada** con la protección de Windows, atada a tu cuenta y a tu
  equipo.
- La clave de NavData **no se configura ni se escribe**: llega cifrada desde phpVMS y vive **solo en
  memoria** mientras el programa está abierto.

---

## Lo que necesitas para volar

| | |
|---|---|
| **Sistema** | Windows de 32 o de 64 bits, con **.NET Framework 4.8** (viene en Windows 10 y 11) |
| **Simulador** | MSFS 2020/2024, Prepar3D, FSX o X-Plane, con su enlace: **FSUIPC7** para MSFS, **XUIPC** para X-Plane |
| **Cuenta** | La de la web de tu aerolínea. Si vienes de CrewSystem, créale una contraseña primero |
| **SimBrief** | Opcional pero recomendable: sin plan, el cliente vuela igual, con menos detalle en el análisis |

Y si algo no funciona, el registro del programa dice **qué** pasó y **dónde**: casi todos los avisos
llevan el motivo concreto, no un «error» a secas.
