# SayIntentions.AI — base para la integración con vmsOpenAcars

**Estado:** investigación cerrada y **verificada contra la API real** (07-10-2026).
**Alcance:** pilotos que tengan suscripción a SayIntentions.AI (el *trial* también funcionó en las pruebas, ver §7).
**Implementación:** **nada de esto está hecho todavía.** Este documento es la base del hilo de integración.

> Documento de partida. El diseño concreto de la primera pieza —la transcripción
> de comunicaciones ATC— está en **`TRANSCRIPCION-ATC.md`**.

---

## 1. Por qué esto es trabajo del cliente y no de phpVMS

Es la conclusión más importante de toda la investigación, así que va primero.

phpVMS es un servidor: no está en la máquina del piloto, no habla con el simulador
y **no tiene la clave del piloto** (ni debe tenerla). Por eso, desde el servidor solo
se puede usar **un** endpoint de SayIntentions: `importVAData`, con la clave de la VA.

vmsOpenAcars corre en el PC del piloto, al lado del simulador y al lado de
SayIntentions. Eso le da **tres canales** que el servidor no puede tocar:

| Canal | Dónde vive | Credenciales | Qué desbloquea |
|---|---|---|---|
| **`flight.json`** | `localhost:43117` o `%LOCALAPPDATA%` | **Ninguna** | Puerta, pista asignada, `taxi_path`, clearances en vivo |
| **SAPI** | `apipri.sayintentions.ai` | La clave **del piloto**, que se lee de `flight.json` | Transcripción ATC, `sayAs`, `assignGate`, `setFreq`, `getWX` |
| **LVARs / DataRefs** | Dentro del simulador | **Ninguna** | Estado ATC en vivo sin pasar por la API |

Y hay una razón de seguridad que refuerza el reparto: los dos secretos no pueden
cruzarse de lado.

> **Regla de oro:** la `va_api_key` **nunca** va al cliente (es un secreto de toda la
> aerolínea: una filtración compromete el contexto de todos los pilotos a la vez).
> La `api_key` del piloto **nunca** va a phpVMS (es la credencial de su cuenta).

Reparto: **phpVMS = cerebro y caja fuerte** (arma el contenido, guarda la clave VA,
persiste y audita). **vmsOpenAcars = manos** (guarda la del piloto, habla con la
sesión viva, mueve cosas).

---

## 2. El programa: VA Alliance y VA-Link

### 2.1 Qué es

SayIntentions tiene un programa de alianza para aerolíneas virtuales
([VA Alliance](https://www.sayintentions.ai/virtual-airline-alliance)) del que Vholar
ya forma parte. La parte técnica se llama **VA-Link** y su documentación vive en el
Help Center, no en la página de la API:
[VA-Link – Virtual Airline Integration API](https://kb.sayintentions.ai/article/va-link-virtual-airline-integration-api).

### 2.2 Qué da a los pilotos

- **Callsign propio**: si el plan se presenta en SimBrief con el ICAO de la VA (`VHR`,
  que es lo que ya manda nuestro despacho), el ATC dice *"Vholar 378"* en vez del
  número de matrícula.
- **Logo de Vholar** en la esquina superior de su app.
- **10 % de descuento permanente** en la suscripción Premium, más códigos cupón a medida.
- **Frecuencia de despacho propia: 118.94**, atendida por un dispatcher IA que responde
  con nuestros datos.
- **Cabina, copiloto y SkyOps personalizados** con `crew_data`, `dispatcher_data`,
  `copilot_data`, `skyops_data`.

### 2.3 El requisito que rompe todo si se ignora

Los datos se pueden empujar, pero **el ATC los ignora hasta que el piloto selecciona
Vholar en su portal**: *Settings → Virtual Airline*. La propia API lo avisa en el campo
`warnings` (ver §6, prueba 4). Es el paso de onboarding que más se va a olvidar.

### 2.4 Qué NO es

No es una plataforma de hosting de VAs. No sustituye a phpVMS. Es personalización de la
experiencia de la IA.

### 2.5 Requisitos del piloto

- Suscripción de pago (*"This API is not available to demo customers"*). En nuestras
  pruebas una cuenta **trial** sí funcionó para `getWX` e `importVAData`, pero no lo
  damos por garantizado para todas las funciones.
- Seleccionar Vholar en el portal (§2.3).
- Tener SayIntentions en marcha durante el vuelo.

---

## 3. `flight.json`: el canal gratis y sin credenciales

SayIntentions sirve el estado del vuelo en dos sitios con el mismo JSON:

- **HTTP (recomendado):** `GET http://localhost:43117/flightJSON` — CORS `*`,
  alcanzable desde la LAN, actualiza cada **3–30 s**. Devuelve `{}` si no hay vuelo activo.
- **Fichero:** `%LOCALAPPDATA%\SayIntentionsAI\flight.json` — solo existe durante un vuelo.

### 3.1 Campos que nos interesan

| Campo | Para qué |
|---|---|
| `current_flight.flight_origin` / `flight_destination` | Correlacionar con el PIREP |
| `current_flight.assigned_gate` (+ `_lat`/`_lon`) | **Reconciliar con nuestras puertas** (`departure-gate`/`arrival-gate`) |
| `current_flight.flight_plan_departing_runway` / `arriving_runway` | Pista **asignada**, no la planificada |
| `current_flight.destination_arriving_runways` | Pistas disponibles en destino |
| `current_flight.flight_plan_sid` / `flight_plan_star` | Procedimiento en vigor |
| `current_flight.taxi_path` | Waypoints de rodadura → mapa |
| `cleared_for_takeoff` / `cleared_for_landing` | Autorización real |
| `distance_to_runway` | Distancia a la pista asignada |
| `current_airport`, `atis_airports` | Contexto |
| `flight_id` | **Identifica la sesión de vuelo** (cambia si se reinicia SayIntentions) |
| `api_key` | La clave del piloto, para SAPI |

### 3.2 Dos advertencias

1. **`api_key`, `Email` y `userid` van en claro** en ese JSON. Es local, pero el
   endpoint tiene **CORS abierto**: cualquier página web que el piloto tenga abierta
   puede leerlos desde `localhost`. Por eso **el fetch de `localhost:43117` nunca debe
   hacerse desde el navegador** (ni desde una vista de phpVMS). Desde el cliente, sí.
2. Es **de solo lectura**. No se escribe ahí.

---

## 4. SAPI: catálogo de endpoints y autenticación

Base: `https://apipri.sayintentions.ai/sapi/`. Todos son **GET** salvo `importVAData` (POST).

| Endpoint | Auth | Para qué |
|---|---|---|
| `importVAData` | Clave VA + clave piloto | **El único de la alianza.** Empuja el contexto de la VA |
| `getWX` | Clave piloto | ATIS, METAR, TAF, **pista activa**, viento, frecuencias |
| `getTFRs` | Clave piloto | TFRs en GeoJSON |
| `getVATSIM` | Clave piloto | Tráfico/ATC de VATSIM en GeoJSON |
| `getCurrentFrequencies` | Clave piloto | Frecuencias actuales |
| **`getCommsHistory`** | Clave piloto **+ vuelo activo** | **Transcripción piloto↔ATC** (ver `TRANSCRIPCION-ATC.md`) |
| `sayAs` | Clave piloto **+ sesión** | Hacer hablar a ATC/tripulación/pasajeros, o mandar ACARS |
| `assignGate` / `getParking` / `getAirport` | Clave piloto **+ sesión** | Puerta y datos del aeropuerto del plan |
| `setFreq` / `setVar` / `setPause` | Clave piloto **+ sesión** | Control del simulador / radio |

> **La clave de la VA no sirve para nada de esta tabla salvo `importVAData`.**
> Comprobado: `getWX` con nuestra `va_api_key` devuelve
> `{"error":"The provided API key is invalid."}`, igual que con una clave inventada.

Detalle de `sayAs`: los canales `*_IN` (por ejemplo `ACARS_IN`) permiten hablar **al**
piloto; `COM1`/`COM2` simulan al piloto hablando al ATC. Para ACARS hay `from`
(estación), `response_code` y `message_type` (`cpdlc`/`telex`). Límite: 255 caracteres
(128 en `ACARS_IN`). `rephrase=1` solo en canales `_IN`.

---

## 5. LVARs: estado ATC en vivo, sin API

SayIntentions expone LVARs de MSFS con equivalente en DataRef de X-Plane
(`siai/...`). Los relevantes:

| LVAR | Modo | Para qué |
|---|---|---|
| `L:SIAI_CABIN_ANNOUNCEMENTS_DISABLE` | **Escribible** | **Silenciar los anuncios automáticos de la IA** (ver §6) |
| `L:SIAI_CLEARED_FOR_TAKEOFF` | Lectura | Autorización de despegue real |
| `L:SIAI_CLEARED_FOR_LANDING` | Lectura | Autorización de aterrizaje real |
| `L:SIAI_TAXIPATH` | Lectura | Rodadura autorizada |
| `L:SIAI_FLIGHT_PHASE` | Lectura | Fase actual |
| `L:SIAI_COM` / `L:SIAI_INTERCOM` | Lectura | Qué hay sintonizado |
| `L:SIAI_RADIO_PTT` / `L:SIAI_INTERCOM_PTT` | Lectura | PTT pulsado |
| `L:SIAI_SELCAL_PENDING_COM` (+ COM2) | Lectura | Aviso SELCAL pendiente |
| `L:SIAI_MECH_ATTEND` / `L:SIAI_CREW_ATTEND` | **Escribible** | Botones de llamada (incrementar) |
| `L:SIAI_ATC_MASTER_OFF` | — | ATC silenciado |

**Limitación:** los LVAR solo existen con la integración nativa de MSFS/X-Plane. Con un
adaptador **SimAPI** puede que no estén, así que hay que degradar con elegancia.

---

## 6. La colisión que hay que resolver sí o sí: anuncios de cabina

vmsOpenAcars **ya reproduce 7 anuncios pregrabados** (`boarding`, `taxi_out`, `on_runway`,
`cruise`, `top_of_descent`, `approach`, `taxi_in`) servidos por la NavData API.
SayIntentions tiene su propia tripulación de cabina IA, que se personaliza precisamente
con el `crew_data` de la VA.

En cuanto el `crew_data` de Vholar esté poblado, **los dos van a hablar a la vez**.

La solución está documentada y es exactamente para esto:

> `L:SIAI_CABIN_ANNOUNCEMENTS_DISABLE` — settable. *Write 1 to suppress the automated
> phase announcements (boarding, safety demo, cruise, descent, arrival) without the
> pilot having to change their portal setting.*

Y el matiz importa mucho: la supresión afecta **solo a los anuncios automáticos de fase**.
Las interacciones iniciadas por el piloto, las de crisis, los seguimientos de la
tripulación y **los anuncios que pidamos nosotros por API siguen pasando**.

Es decir: el cliente calla a la IA mientras suena nuestro audio de marca, y la IA sigue
disponible para interactuar. Es un LVAR, así que **solo el cliente puede hacerlo**.

---

## 7. Lo verificado contra la API real

Probado el 07-10-2026 con una clave de piloto real. **Esto ya no hay que redescubrirlo:**

| # | Prueba | Resultado |
|---|---|---|
| 1 | `getWX?icao=SKBO&with_comms=1` con clave de piloto | ✅ ATIS 127.8, TWR 118.1, GND 121.8, pistas activas 14R/14L, densidad de altitud 10.200 ft |
| 2 | `importVAData` con clave de piloto + `va_api_key` de Vholar | ✅ `{"status":"OK"}` |
| 3 | `importVAData` con una `va_api_key` inventada | ❌ `{"error":"Invalid va_api_key: ..."}` |
| 4 | Respuesta de la prueba 2 | ⚠️ `warnings`: *"Data stored, but this pilot currently has a DIFFERENT virtual airline selected... The pilot must select this airline under Settings > Virtual Airline."* |
| 5 | `importVAData` **sin** clave de piloto | ❌ `{"error":"No api_key provided"}` |
| 6 | **Límite de tamaño de los campos** | ⚠️ **4.096 BYTES, no caracteres.** Enviados 3.000 `á` (6.000 bytes) → guardó 2.048 caracteres (4.096 bytes), cortando donde cayó |
| 7 | Todos los campos vacíos | ❌ `{"error":"Missing required field - crew_data, dispatcher_data, copilot_data, or skyops_data"}` |
| 8 | Código HTTP de todo lo anterior | ⚠️ **Siempre 200**, incluso en los errores |
| 9 | Ruta inexistente (`/sapi/totalGarbageXYZ`) | `200 {}` — **sondear endpoints no sirve de nada** |
| 10 | `getWX` con la `va_api_key` | ❌ `{"error":"The provided API key is invalid."}` — no es una clave de usuario |
| 11 | Cuenta **trial** | ✅ Funcionó para 1 y 2 |

**Consecuencias directas para el cliente:**

1. **Nunca** comprobar el éxito por el código HTTP: hay que mirar el cuerpo (`error`).
2. Al construir textos para `importVAData`, truncar **por bytes y en frontera de carácter**
   (`mb_strcut`), no por caracteres. Con acentos y `ñ`, el margen real ronda los 2.048
   caracteres por campo.
3. Enviar **siempre** al menos un campo no vacío.
4. **No** usar la clave de la VA en el cliente. Para `importVAData` el cliente nunca debe
   llamar directamente: se lo pide a phpVMS (que es quien tiene la otra clave).

---

## 8. Reparto de responsabilidades

| Función | phpVMS | vmsOpenAcars |
|---|---|---|
| `importVAData` (contexto de la VA) | ✅ dueño de la `va_api_key` | ❌ nunca |
| Datos autoritativos (SimBrief, PAX, puertas, METAR) | ✅ origen | los consume |
| `getCommsHistory` (transcripción ATC) | ❌ | ✅ dueño de la clave del piloto |
| `sayAs`, `assignGate`, `setFreq` | ❌ | ✅ |
| `flight.json` / LVARs | ❌ | ✅ |
| Almacenar y auditar (PIREP, transcripción, scoring) | ✅ | ❌ |
| Arbitraje de anuncios de cabina (LVAR) | ❌ | ✅ |

---

## 9. Trabajo propuesto, por fases

1. **`flight.json` + LVARs → mapa en vivo.** Leer puerta asignada, pista autorizada,
   `taxi_path` y clearances. El mapa ya tiene capa ROUTE y sidebar de procedimientos:
   hoy muestran lo **planificado**, con esto mostrarían lo **autorizado**. Coste casi
   cero, sin credenciales. Reconciliar la puerta con nuestros campos.
2. **`getCommsHistory` → transcripción a phpVMS.** La capacidad nueva más grande.
   Diseño completo en **`TRANSCRIPCION-ATC.md`**.
3. **Arbitraje de anuncios de cabina** (§6). Uno o dos LVARs; evita un choque que
   ocurrirá seguro.
4. *(Servidor, no cliente)* **`importVAData`**, con el truncado por bytes y el aviso de
   vinculación ya resueltos.
5. `sayAs` / `assignGate` / `setFreq` como pulido: mandar el despacho como telex ACARS,
   imponer nuestra puerta a la IA, auto-sintonizar la salida.

**Orden recomendado: 1 → 2 → 3.** Las tres son del cliente y no dependen de nadie más.

---

## 10. Reglas que no se pueden romper

- **La `va_api_key` no sale del servidor.** Ni en el binario, ni en configuración, ni en
  un fichero del cliente.
- **La `api_key` del piloto no sale de su PC.** El cliente la lee de `flight.json` y la
  usa en tránsito; no se envía a phpVMS ni se guarda en disco más allá de lo que ya
  hace SayIntentions.
- **Nada de `localhost:43117` desde el navegador.**
- **Contenido:** *"family friendly"* y temática aeronáutica. Lo contrario es
  **terminación inmediata de la cuenta**.
- **No abusar de `sayAs`:** genera voz que les cuesta dinero y el abuso conlleva
  restricción de acceso. Unas pocas docenas de mensajes por vuelo como máximo.
- **Nada de esto puede ser crítico para el vuelo.** Si SayIntentions falla, el vuelo y el
  PIREP continúan con normalidad.

---

## 11. Preguntas abiertas

**Para el equipo del cliente:**

1. ¿Cómo lee hoy los LVARs? ¿Solo MSFS/X-Plane nativo o también a través de SimAPI?
   ¿Puede **escribir** uno (necesario para §6)?
2. ¿Tiene ya un canal para enviar datos nuevos a phpVMS fuera de los endpoints ACARS
   existentes, o hay que añadirlo?
3. ¿Cómo detecta el inicio y el fin del vuelo? Es lo que decide cuándo hacer polling
   de la transcripción.
4. ¿Qué hace hoy si el piloto no tiene SayIntentions instalado? La función debe quedar
   muda sin afectar a nada.

**Para SayIntentions (vía `va@sayintentions.ai`):**

1. ¿La vinculación en el portal es requisito para que `importVAData` surta efecto, o solo
   para el logo y el descuento? (La API dice que sin ella *"ATC will ignore it"*.)
2. ¿El callsign propio ya está activo para Vholar? Se comprueba volando, no por API.
3. ¿Amplían el límite de 4.096 bytes si lo necesitamos? (La doc dice que se puede pedir.)

---

## Nota de archivo

Documento de partida del hilo, archivado el **2026-10-07** tal como lo entregó el
mantenedor. Es la **fuente de verdad de la investigación** (lo verificado contra la API
real el 07-10-2026 está en §7 y **no se repite en ningún otro sitio**).

- El diseño de la fase 2 vive en **`TRANSCRIPCION-ATC.md`** (mismo directorio).
- Las respuestas del código de este cliente a las preguntas de §11 están recogidas en
  `TRANSCRIPCION-ATC.md` → *«Lo que el cliente ya ofrece hoy»*, con rutas y líneas.
- No se ha implementado **nada** de este documento: es base de diseño, no changelog.
- El **anexo** de abajo recoge lo que la documentación oficial de SAPI añade o corrige
  sobre este texto. El cuerpo de arriba **no se ha tocado**: es el original del mantenedor.

---

## Anexo — verificación contra la documentación oficial de SAPI (2026-10-07)

**Fuente:** `https://portal.sayintentions.ai/p2d/docs/` — *SAPI Documentation*, con fecha
**«Last updated: 10/7/2026»** en su pie. Es el documento que el propio artículo de VA-Link
del Help Center publica como *«complete API documentation»*. Se obtuvo el 2026-10-07; la KB
del Help Center **no se puede leer sin navegador** (es una SPA: el HTML servido son
plantillas `{{ … }}` sin datos).

### Lo que confirma

- El reparto de endpoints de §4 y sus clases de autenticación (*API Key* / *API Key + Session*).
- `sayAs`: canales, el límite de 255 caracteres (128 en `ACARS_IN`), `rephrase` solo en los
  canales `_IN`, y `from` / `response_code` / `message_type` (`cpdlc` / `telex`) para ACARS.
- El límite de **4.096 bytes** de `importVAData` y su truncado.
- Que hacen falta **dos** claves para `importVAData` (la del piloto y la de la VA) y que el
  texto de §2.3 sobre la vinculación en el portal es literal.
- El mapa MSFS ↔ X-Plane de los LVARs (`L:SIAI_…` ↔ `siai/…`).
- `getCommsHistory` **exige vuelo activo**: entre los errores documentados está
  `{"error":"No active flight could be found."}`.

### Lo que añade o corrige

1. **`flight.json` va envuelto en `flight_details`.** La estructura real es
   `flight_details.api_key`, `flight_details.flight_id`, `flight_details.current_flight.*`
   (origen, destino, pistas, `taxi_path`, `assigned_gate`…). La tabla de §3.1 da los campos
   casi planos: **leer `current_flight` en la raíz fallaría**. Lo mismo con `cleared_for_takeoff`,
   `cleared_for_landing` y `distance_to_runway`.
2. **El contrato completo de `getCommsHistory`**, que es lo que faltaba para diseñar la fase 2:
   parámetros `api_key` + `since_id` (sondeo incremental por `id`) y respuesta
   `{flight_id, comm_history[…], mission}`. **La trampa**: los nombres están desde el punto de
   vista del servidor — `incoming_message` es lo que dijo **el piloto** y `outgoing_message` lo
   que dijo **el ATC** — y hay dos versiones del texto, la localizada y la `*_english`. Todo el
   detalle, con el payload propuesto para phpVMS, está en **`TRANSCRIPCION-ATC.md`**.
3. **Dos LVARs de §5 no se llaman así.** `L:SIAI_COM` y `L:SIAI_INTERCOM` no existen: son
   `L:SIAI_COM1_POSITION` / `L:SIAI_INTERCOM1_POSITION` (enums con los valores posibles) y
   `L:SIAI_COM1_RECEIVING` / `L:SIAI_INTERCOM{1,2,3}_RECEIVING`. Igual con
   `L:SIAI_SELCAL_PENDING_COM`, que es `…_COM1` y `…_COM2`.
4. **LVARs que la tabla de §5 no recoge** y son escribibles: `L:SIAI_COPILOT` (quién lleva las
   comunicaciones), `L:SIAI_COM1_VOLUME_SET` / `L:SIAI_COM2_VOLUME_SET` /
   `L:SIAI_AUDIO_PANEL_VOLUME_SET` / `L:SIAI_AUDIO_PANEL2_VOLUME_SET` (0-100), y
   `L:SIAI_ATC_MASTER_OFF` (que **Entourage fuerza a 1** y que **se recomienda poner a 1**
   mientras se vuela en VATSIM/IVAO).
5. **El matiz que cambia el diseño de la fase 3 (§6).** La supresión de
   `L:SIAI_CABIN_ANNOUNCEMENTS_DISABLE` es **unidireccional**: escribir `0` devuelve el control
   al ajuste del portal y **nunca enciende** los anuncios a quien los tiene apagados. Y mientras
   el override está puesto **también callan los botones del piloto** («Announce Boarding» y los
   anuncios personalizados), no solo los automáticos. Consecuencia: hay que **soltar el override
   al terminar** nuestro audio, no dejarlo puesto «por si acaso»; y no sirve para forzar
   anuncios, solo para callarlos.
6. **No hay límite de peticiones estricto.** La guía es «unas pocas docenas de peticiones de
   **voz** por vuelo» y lo que cuesta dinero es `sayAs`, no leer. La advertencia de §10 sobre no
   abusar de `sayAs` queda confirmada y acotada a los endpoints que **generan** audio.
7. **El artículo canónico de LVARs del Help Center va por detrás** de la documentación del
   portal: no lista `SIAI_CABIN_ANNOUNCEMENTS_DISABLE`, ni los SELCAL, ni los botones de llamada
   —que el portal marca como «New»—, y trae dos erratas de bulto (describe `SIAI_COM1_RECEIVING`
   como si fuera el COM2, y escribe `siai/audio_ponel2_volume_set`). **Manda el portal.**
8. **SimAPI** (el adaptador para cualquier simulador) comunica por ficheros JSON y tiene
   limitaciones explícitas: no inyecta objetos 3D, y eso deja fuera pushback, control de rampa,
   inyección y consciencia de tráfico y **las guías de rodaje iluminadas**. Relevante para §5: si
   un piloto entra por un adaptador SimAPI, los LVARs pueden no existir, y **no está documentado
   que la salida del adaptador (`simAPI_output.jsonl`) pueda escribir un LVAR** — el único
   ejemplo que publican es una K-Var. Es pregunta abierta para SayIntentions.

### Una prueba propia (2026-10-07, sin clave de piloto)

`GET /sapi/getCommsHistory` sin `api_key` devuelve `200 {"error":"No api_key provided"}`, mientras
que la raíz `/sapi/` y cualquier ruta inventada devuelven `200 {}`. Es decir: **el endpoint existe
y valida la clave antes que nada** — se puede comprobar que un endpoint vive sin tener clave, y la
prueba 9 de §7 solo aplica a rutas que no existen.
