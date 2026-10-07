# Transcripción de comunicaciones ATC — diseño de la fase 2

**Estado:** diseño. **Nada implementado.**
**Pieza:** fase 2 de `2026-10-07_VMSOPENACARS_SAYINTENTIONS_sayintentions_01_base-integracion.md`
(*«`getCommsHistory` → transcripción a phpVMS. La capacidad nueva más grande»*).
**Fecha:** 2026-10-07.
**Fuente del contrato:** documentación oficial de SAPI del **2026-10-07**
(`https://portal.sayintentions.ai/p2d/docs/#api-getCommsHistory`), que es el documento que el
Help Center enlaza como «complete API documentation». El artículo del Help Center sobre VA-Link
**no** trae el contrato de este endpoint, y la KB **no se puede leer sin navegador** (es una SPA:
el HTML servido son plantillas `{{ … }}`); la copia legible se obtiene vía `r.jina.ai`.

---

## 0. Qué resuelve y qué no

**Resuelve:** tener, dentro del PIREP y en phpVMS, lo que se dijo por radio en el vuelo —
piloto y ATC, con estación, frecuencia, canal, posición y hora—. Es el dato que hoy no
existe en ninguna parte del ecosistema.

**No resuelve, y hay que decirlo:** la transcripción **no alimenta el scoring**, no es
crítica para el vuelo y no puede condicionar el PIREP. Si SayIntentions, el endpoint o
phpVMS fallan, el vuelo termina y se filea igual (regla §10 de la base).

---

## 1. El contrato, verificado

```
GET https://apipri.sayintentions.ai/sapi/getCommsHistory
```

| Parámetro | Tipo | Obligatorio | Qué hace |
|---|---|---|---|
| `api_key` | string | **Sí** | La clave **del piloto** (no la de la VA) |
| `since_id` | integer | No | Devuelve solo las entradas con `id` **mayor** que este valor. Es el mecanismo de sondeo incremental |

**Autenticación:** clave de piloto. La documentación lo clasifica como *API Key*, pero el
propio texto dice que el resultado está acotado a la **sesión de vuelo activa**, y entre los
errores documentados está `{"error":"No active flight could be found."}` — es decir, en la
práctica es *clave + vuelo activo*, que es como lo describía la base (§4).

**Respuesta:**

```json
{
  "flight_id": 987654321,
  "comm_history": [
    {
      "id": 12345,
      "ident": "KJFK",
      "copilot": 0,
      "lat": 40.6413,
      "lon": -73.7781,
      "frequency": "121.900",
      "is_acars": 0,
      "channel": "COM1",
      "stamp_zulu": "2024-01-15T14:30:00Z",
      "station_name": "JFK Ground",
      "incoming_message": "JFK Ground, N12345 at gate A1, ready to taxi",
      "incoming_message_english": "JFK Ground, N12345 at gate A1, ready to taxi",
      "outgoing_message": "N12345, taxi to runway 04L via Alpha, Bravo",
      "outgoing_message_english": "N12345, taxi to runway 04L via Alpha, Bravo",
      "language": "en-US",
      "atc_url": "https://...",
      "pilot_url": "https://..."
    }
  ],
  "mission": { "mission_id": 67890, "mission_type": "rescue", "status": "active" }
}
```

### 1.1 Las tres trampas del contrato

1. **Los nombres están al revés respecto a lo que uno espera.**
   `incoming_message` es lo que dijo **el piloto**; `outgoing_message` es lo que dijo
   **el ATC**. Lo dice la documentación explícitamente (*«fields use the server's
   perspective»*). Es la trampa número uno de esta integración: leerlo «por intuición»
   deja la transcripción con los dos interlocutores intercambiados y **no da ningún
   error**. El cliente **no** propaga esos nombres: los traduce a `pilot_text` /
   `atc_text` en el borde (`SayIntentionsCommsParser`), para que el nombre invertido no
   pueda sobrevivir más allá de una función con test.
2. **Dos versiones del texto: localizada y en inglés.**
   `*_message` viene en el idioma del piloto (`language`), y `*_message_english` es el
   texto estable en inglés. Para mostrar al piloto en phpVMS manda la localizada; para
   agregar, contar o analizar, la inglesa. Hay que guardar **las dos** o se pierde una.
3. **El `id` es la clave de todo el sondeo.** Es lo único que permite pedir «lo nuevo»
   sin duplicar y sin perder entradas. Sin persistirlo entre sondeos, cada vuelta pide la
   historia entera y el POST acaba repitiendo todo.

### 1.2 Dos comprobaciones en vivo (sin clave de piloto)

Hecho hoy, 2026-10-07, contra el endpoint real:

| Prueba | Resultado |
|---|---|
| `GET /sapi/getCommsHistory` sin `api_key` | `200` · `{"error":"No api_key provided"}` |
| `GET /sapi/` (ruta raíz) | `200` · `{}` |

La primera es la prueba de que **el endpoint existe y valida la clave antes que nada**:
si no existiera, la respuesta sería `{}` como la raíz y como cualquier ruta inventada
(§7, prueba 9 de la base). **Lo que no se ha podido verificar** es la forma real de la
respuesta, porque hace falta una clave de piloto con vuelo activo y esa clave **no sale
del PC del piloto** (regla §10). El esquema de §1 es el de la documentación oficial, no
una observación propia — y así queda marcado en §9.

---

## 2. Lo que el cliente ya tiene hoy (respuestas a §11 de la base)

Las cuatro preguntas abiertas del documento base, contestadas contra el código.

**1 — ¿Cómo lee hoy los LVARs? ¿Puede escribir uno?**
**Hoy no lee ni escribe ninguno.** Cero coincidencias de `ReadLVar`, `WriteLVar`,
`MSFSVariableServices`, `FsLVar`, `L:` o `DataRef` en todo el proyecto; `SimAPI`, cero. Todo
el acceso al simulador es por **offsets crudos** (`Services/FsuipcService.cs:63-213`) con
un único `FSUIPCConnection.Process()` por ciclo (`:492`).

**Pero la capacidad está a un método de distancia, y esto cambia el plan de la fase 3.**
La DLL ya referenciada (`FSUIPCClientDLL 3.3.16`,
`vmsOpenAcars.csproj:47-49`) **documenta la API de LVARs** en el XML que viaja en el
paquete:

| Miembro | `packages/FSUIPCClientDLL.3.3.16/lib/net462/fsuipcClient.xml` |
|---|---|
| `FSUIPCConnection.ReadLVar(string)` / `(byte, string)` | 3798-3812 |
| `FSUIPCConnection.WriteLVar(string, double)` / `(byte, string, double)` | 3813-3827 |
| `FSUIPC.MSFSVariableServices` (`LVars`, `LVarsChanged`, `CreateLVar`, `ExecuteCalculatorCode`) | 5122-5233 |

No hay que añadir referencia, ni WASM propio, ni recompilar nada: **es el mismo paquete que
ya se distribuye**. Lo que falta **no es código nuestro, es runtime del piloto**: el XML
exige «John Dowson's WASM module» y `FSUIPC_WAPID.dll` accesible desde el `.exe` para
`MSFSVariableServices` (`fsuipcClient.xml:5122-5128`), y ese DLL **no viaja** ni en el
paquete ni en `bin/Release/`.

> **Lo que NO está verificado:** que `FSUIPCConnection.WriteLVar` funcione **sin** el WASM.
> El XML documenta la exigencia WASM/WAPID para `MSFSVariableServices` pero **no** la repite
> para `FSUIPCConnection.WriteLVar`. Hay que medirlo en un MSFS con y sin WASM antes de
> prometer §6 (arbitraje de anuncios) a los pilotos. **Y para X-Plane no hay camino**: los
> DataRef `siai/...` no los expone esta DLL (declara «Only works for MSFS») y el proyecto no
> tiene ninguna capa de DataRefs; X-Plane entra por XUIPC con los mismos offsets.

**2 — ¿Hay canal para enviar datos nuevos a phpVMS?**
**Sí, y es la puerta prevista.** `IApiService.PostJsonAsync(path, payload)` →
`Services/ApiService.cs:784-795` (`Services/Interfaces/IApiService.cs:39`), creado
exactamente para esto («rutas de phpVMS que no tienen método propio»), con el cuerpo
también disponible cuando la respuesta no es 2xx. El `HttpClient` autenticado **no se
expone en la interfaz a propósito** (`IApiService.cs:10-14`: exponerlo filtraba la clave a
hosts de terceros), así que el cliente **nunca** debe usar ese cliente contra
SayIntentions: para terceros está `Services/Http/HttpClientProvider.cs:15-66`.

**3 — ¿Cómo detecta el inicio y el fin del vuelo?**
- **Inicio:** el vuelo queda iniciado cuando `PrefileFlight` devuelve el id del PIREP
  (`Core/Flight/FlightManager.Lifecycle.cs:115-117`), **no** con el primer dato de
  simulador. El punto de enganche es `MainViewModel.OnFlightStartedAsync`
  (`ViewModels/MainViewModel.cs:722`), donde ya se lanza el prefetch de anuncios
  (`:755`) y la carga de espacios aéreos (`:759`).
- **Fin:** `OnFlightEnded` (`ViewModels/MainViewModel.cs:73`), que cubre los **tres**
  caminos: PIREP fileado (`ViewModels/AcarsReporter.cs:162`, cableado en
  `MainViewModel.cs:159`), cancelar (`:773`) y abortar (`:792`). **No** se dispara al
  aterrizar ni en OnBlock — el vuelo sigue vivo hasta que se filea.
- **Dos huecos que el diseño tiene que asumir** (los encontró el reconocimiento, no son
  hipótesis):
  1. **`OnFlightEnded` no se dispara si `FilePirep` falla** (`AcarsReporter.cs:170-173`):
     el vuelo sigue y el sondeo debe seguir también.
  2. **Un vuelo reanudado no pasa por `OnFlightStartedAsync`**: `CheckAndResumeFlight`
     (`AcarsReporter.cs:501-552`, llamado desde `Login`, `MainViewModel.cs:823`) reanuda
     por `ResumeFlight` sin disparar `OnFlightStarted` ni el arranque de servicios. Un
     sondeo colgado solo de `OnFlightStartedAsync` **no arrancaría tras un reinicio de la
     aplicación a mitad de vuelo**.

**4 — ¿Qué hace hoy si el piloto no tiene SayIntentions?**
**Nada: no existe ni el servicio.** El patrón de la casa para un servicio opcional está
establecido y es el que se copia aquí (§7): bandera de disponibilidad probada **una vez por
sesión** con aviso único del motivo, como NavData
(`MainViewModel.LogLnmDatabaseStatus`, `MainViewModel.cs:398-434`), y salidas tempranas
que devuelven `null`/vacío sin tocar el vuelo (`Services/NavDataService.cs:22-23`).

---

## 3. Arquitectura

Cuatro piezas, en el orden en que se pueden probar.

```
                 ┌──────────────────────────────────────────────┐
 flight.json ──► │ SayIntentionsSession                         │  api_key en memoria,
 (HTTP o fichero)│  · TryLoadAsync() → api_key, flight_id, …    │  nunca a disco/log
                 └───────────────┬──────────────────────────────┘
                                 │ clave del piloto (en tránsito)
                 ┌───────────────▼──────────────────────────────┐
 getCommsHistory │ SayIntentionsCommsClient                     │  HttpClient propio
 (SAPI) ◄────────│  · GetSinceAsync(flightId, sinceId)          │  (no el de phpVMS)
                 └───────────────┬──────────────────────────────┘
                                 │ entradas crudas
                 ┌───────────────▼──────────────────────────────┐
                 │ SayIntentionsCommsParser  (puro, con test)   │  incoming→pilot,
                 │  · Parse(json, lastId) → entries + newLastId │  outgoing→atc
                 └───────────────┬──────────────────────────────┘
                                 │ payload ya en nuestro vocabulario
                 ┌───────────────▼──────────────────────────────┐
                 │ AtcTranscriptService  (cola acotada, en      │
                 │ memoria; sube por lotes a phpVMS)            │
                 └───────────────┬──────────────────────────────┘
                                 │ POST (IApiService)
                        phpVMS  api/pireps/{id}/atc-transcript
```

**Dónde vive cada pieza:**

| Pieza | Fichero previsto | Por qué ahí |
|---|---|---|
| `SayIntentionsSession` | `Services/SayIntentions/SayIntentionsSession.cs` | Lee `flight.json`; sin credenciales propias |
| Cliente HTTP | `Services/Http/HttpClientProvider.cs` (+`SayIntentions`) | Es el sitio de los clientes **de terceros**: el autenticado de phpVMS no se toca (`HttpClientProvider.cs:29-34`) |
| `SayIntentionsCommsClient` | `Services/SayIntentions/SayIntentionsCommsClient.cs` | `GET getCommsHistory`, timeout 15 s, sin `EnsureSuccessStatusCode` (los errores vienen con 200) |
| `SayIntentionsCommsParser` | `Helpers/SayIntentionsCommsParser.cs` | **Puro y `internal static`**: la regla de nombres invertidos y la de `since_id` van aquí, con su test |
| `AtcTranscriptService` | `Services/SayIntentions/AtcTranscriptService.cs` | Cola en memoria + subida por lotes + contadores para el log |

**Regla de la casa que se respeta:** un helper puro por regla, con su test, sin red ni
WinForms (`CLAUDE.md` → *Diseño*). El parser es el candidato obvio; el cliente y el
servicio no llevan lógica decidible.

---

## 4. Ciclo de vida del sondeo

**Dónde engancha.** El bucle periódico **ya existe**: `MainViewModel.RunTimerLoopAsync`
(`ViewModels/MainViewModel.cs:492-501`) es un tick de 1 s que llama a `OnTimerTickAsync`
(`:503`), y ahí ya viven dos trabajos periódicos guardados por `ActivePirepId`: el envío de
posiciones (`:509-521`) y el checkpoint de score cada 60 s (`:523-525`). La transcripción
es **el tercero**, con el mismo criterio de parada.

**Cadencia: 30 s.** SayIntentions actualiza `flight.json` cada 3–30 s, así que sondear más
rápido no aporta nada nuevo a la transcripción y solo suma peticiones. Además, un
sondeo **inmediato** (fuera de la cadencia) en tres momentos que el cliente ya detecta:
`TaxiOut`, `Approach` y la entrada en `OnBlock`. Y uno **final** en `OnFlightEnded`, para
recoger la cola (el PIREP ya está fileado, pero el servidor puede adjuntar por `pirepId`).

**Criterio de parada:** `string.IsNullOrEmpty(ActivePirepId)` — el mismo que los otros dos
trabajos. Eso cubre por construcción el caso «`FilePirep` falló»: si el PIREP sigue vivo,
el sondeo sigue; si se fileó, `ActivePirepId` quedó vacío (`FlightManager.Lifecycle.cs:18`).
Pero **no** cubre el vuelo reanudado: hay que arrancar el servicio también desde el camino
de `CheckAndResumeFlight` (`AcarsReporter.cs:501-552`), o el sondeo no existe tras un
reinicio. Es el arreglo más barato y el que más se va a olvidar.

**Cancelación:** hoy **no hay `CancellationToken` por vuelo** — el único CTS es el de sesión
(`_timerCts`, `MainViewModel.cs:393-394`, cancelado en `:486`). El servicio se diseña con
su propio `CancellationTokenSource` creado en el arranque y cancelado en el fin, **sin**
tocar el CTS de sesión.

**Concurrencia:** el hilo del tick **nunca** se bloquea. El servicio corre en su propia
tarea; el tick solo marca «toca sondear». Es el patrón de `RaasVoice`
(`Services/RaasVoice.cs:9-20`), hilo propio con cola, para que la telemetría no espere.

**Un `flight_id` nuevo es una sesión nueva.** Si el piloto reinicia SayIntentions a mitad
de vuelo, `flight_id` cambia y `since_id` **deja de tener sentido**: hay que reiniciar el
contador a 0 y **marcar el tramo** (`segment`: 1, 2, …) para que el servidor sepa que son
dos sesiones del mismo PIREP y no una historia duplicada. Es exactamente el motivo por el
que la base §3.1 lo señala como «identifica la sesión de vuelo».

---

## 5. El payload a phpVMS

**Propuesta de endpoint** (a acordar con phpVMS; hoy **no existe**):

```
POST {vms_api_url}/api/pireps/{pirepId}/atc-transcript
X-API-KEY: <la clave del piloto en phpVMS, ya la pone IApiService>
```

```json
{
  "source": "sayintentions",
  "session": {
    "flight_id": 987654321,
    "segment": 1,
    "started_utc": "2024-01-15T14:28:02Z"
  },
  "entries": [
    {
      "id": 12345,
      "stamp_zulu": "2024-01-15T14:30:00Z",
      "channel": "COM1",
      "frequency": "121.900",
      "station_name": "JFK Ground",
      "ident": "KJFK",
      "is_acars": 0,
      "copilot": 0,
      "lat": 40.6413,
      "lon": -73.7781,
      "language": "en-US",
      "pilot_text": "JFK Ground, N12345 at gate A1, ready to taxi",
      "atc_text": "N12345, taxi to runway 04L via Alpha, Bravo",
      "pilot_text_english": "JFK Ground, N12345 at gate A1, ready to taxi",
      "atc_text_english": "N12345, taxi to runway 04L via Alpha, Bravo"
    }
  ]
}
```

**Por qué el payload renombra los campos.** `pilot_text` / `atc_text` en lugar de
`incoming_message` / `outgoing_message`: el nombre invertido no sale del parser. Quien lea
la tabla de phpVMS no tiene que saber nada del punto de vista del servidor de
SayIntentions, y un futuro cambio de su nomenclatura se absorbe en un solo sitio.

**Por qué un endpoint propio y no `/acars/logs`.** `POST api/pireps/{id}/acars/logs` existe
y funciona (`Services/ApiService.cs:540-556`), y phpVMS lo recomienda para mensajes frente
a posiciones. Pero es `type = 2` (**LOG**) y ahí ya van los avisos del RAAS
(`FlightManager.LogRaasCallout`): una transcripción de 200 líneas por vuelo convertiría
ese canal en ruido y rompería la lectura del log. La transcripción es **su propia cosa**:
tiene estación, frecuencia, canal, posición, hora y dos idiomas por entrada, y se consulta
distinto.

**Mientras phpVMS no publique el endpoint, la función va apagada.** Es el precedente de la
casa: `taxi_planned_observation_enabled` está apagado por defecto hasta que NavData publicó
su almacén (`CLAUDE.md` → *RAAS y guía de rodaje*). Interruptor `sayintentions_transcript_enabled`
en `App.config`, **por defecto apagado**; con el interruptor apagado no se construye el
cuerpo ni se sale a la red.

---

## 6. Idempotencia, reintentos y cola

- **No duplicar en el origen:** `since_id` = el `id` más alto ya procesado. El estado
  `_lastId` se reinicia a 0 al cambiar `flight_id` (§4).
- **No duplicar en el destino:** el `id` de cada entrada viaja en el payload, así que el
  servidor puede hacer *upsert* por `(pirep_id, flight_id, id)`. Un reintento tras un
  timeout no puede duplicar la transcripción.
- **Cola acotada en memoria, sin disco.** El buffer de no enviados vive en memoria con un
  techo (p. ej. 500 entradas); al desbordar se descartan **las más viejas** y se cuenta.
  **No se escribe a disco a propósito**: la transcripción lleva las palabras del piloto y
  phpVMS es el almacén de referencia; un *outbox* en disco sería una copia local
  permanente de esas conversaciones que nadie ha pedido. El precio asumido —si la
  aplicación cae a mitad de vuelo se pierde lo no enviado— es aceptable por la regla de
  que nada de esto puede ser crítico.
- **Reintentos:** un POST fallido reintenta en la siguiente vuelta (30 s), no en bucle.
  Un `401`/`403` **para** el servicio (la clave de phpVMS no es el problema aquí, es
  configuración) y se avisa una vez.

---

## 7. Límites, coste y degradación

**Coste.** Cero en voz: `getCommsHistory` **lee**, no genera audio. El único endpoint que
cuesta dinero es `sayAs`, y este diseño **no lo llama nunca** (`importVAData` aparte, y ese
es del servidor). La advertencia de la documentación sobre «unas pocas docenas de
peticiones de voz por vuelo» **no aplica** aquí.

**Límites.** La documentación dice que **no hay límite estricto** y que el abuso se castiga
con restricción de acceso; la guía es «evitar llamadas en ráfaga». Un sondeo cada 30 s
durante un vuelo de 3 h son ~360 peticiones, muy por debajo de cualquier umbral razonable,
pero conviene que la cadencia sea **configurable** por si su lado cambia de criterio.

**Degradación, con los cuatro motivos distintos en el log (una vez cada uno):**

| Situación | Detección | Qué hace el cliente |
|---|---|---|
| SayIntentions no está instalado | `localhost:43117` rechaza la conexión y no existe el fichero | Servicio mudo. Nada en la UI, nada en el PIREP |
| SayIntentions está pero no hay vuelo | El JSON no trae `flight_details` | Sondea; cuando aparezca, arranca |
| No hay `api_key` utilizable | El campo falta o está vacío | Aviso una vez; servicio mudo |
| phpVMS no tiene el endpoint | `404` en el POST | Aviso una vez; la cola deja de crecer (se descarta) y no se reintenta |

**El JSON de `flight.json` va envuelto en `flight_details`.** La estructura oficial es
`flight_details.api_key`, `flight_details.flight_id`, `flight_details.current_flight.*`
(la tabla de la base §3.1 lo omite y da los campos casi planos: leer `current_flight` en la
raíz **fallaría**). El cliente lee el sobre y tolera que falte cualquier campo.

---

## 8. Seguridad y privacidad

Lo que la base §10 prohíbe y cómo lo respeta este diseño:

1. **La `api_key` del piloto no sale de su PC y no se persiste.** Vive en memoria del
   proceso, se lee de `flight.json` y se usa **en tránsito** en la URL de SAPI. No va a
   `App.config`, no va a los `.json` de idioma, no va a `logs/`, **no va a phpVMS** y
   **no se escribe en `landing_log.sqlite` ni en `NavData_cache.sqlite`**.
2. **Nunca se registra.** Regla explícita del servicio: ninguna línea de log, ningún
   mensaje de error y ningún cuerpo de respuesta se vuelca sin pasar por un redactor que
   sustituye el valor de `api_key` por `***`. Un `GET` fallido con la clave en el query
   string acaba en el log de `HttpClient`/excepción si nadie lo evita.
3. **`localhost:43117` nunca desde el navegador.** El consumo es del cliente de escritorio
   (`HttpClient` propio). Ni una vista de phpVMS ni el `WebBrowser` de ningún formulario
   tocan ese puerto: el endpoint tiene CORS `*` y sirve la clave en claro a cualquier
   página abierta.
4. **La `va_api_key` no aparece por aquí.** Este diseño no llama a `importVAData`; eso es
   del servidor (base §8).
5. **Contenido.** No se genera ni se reescribe texto: se transcribe lo que ya se dijo. El
   cliente no expone ningún camino para inyectar texto hacia SayIntentions desde esta
   pieza.

**Privacidad — lo que hay que decidir, no asumir.** La transcripción son las palabras del
piloto y de la IA, con posición y hora. El diseño **no** envía los `atc_url`/`pilot_url`
(audio): solo texto. Aun así, **retención y visibilidad son decisión de phpVMS y del
mantenedor**, no del cliente: si se guarda, ¿durante cuánto?, ¿la ve el piloto?, ¿la ve el
staff?, ¿se anonimiza al exportar? El cliente hace lo que le digan; lo que **no** hará es
guardar copia local por su cuenta.

---

## 9. Plan de implementación, por pasos

Cada paso compila, se prueba y **no rompe nada si el siguiente no llega**.

1. **`SayIntentionsSession` + prueba de disponibilidad.** Leer `flight.json` (HTTP primero,
   fichero después), extraer `api_key`/`flight_id`/origen/destino, sondear **una vez por
   sesión** en `MainViewModel.LogLnmDatabaseStatus` y avisar del motivo. Sin sondeo de
   transcripción todavía: este paso solo contesta «¿está SayIntentions ahí?».
2. **`SayIntentionsCommsParser` (puro, con test).** Entra el JSON de la respuesta, sale
   `(entries, newLastId, flightId)`. Es donde viven las tres trampas de §1.1: nombres
   invertidos, las dos versiones de texto y el `since_id`. **Test con un JSON real de la
   documentación** y casos de: campo ausente, `comm_history` vacío, `id` repetido, idioma
   sin `*_english`.
3. **Sondeo y log local, sin subir nada.** El bucle de 30 s, la parada por
   `ActivePirepId`, el reinicio por cambio de `flight_id` y una línea de log por lote
   («transcripción: N entradas nuevas, última #id»). Aquí se mide de verdad el contrato:
   **primera verificación contra la API real con una clave de piloto**, antes de escribir
   el cliente de subida.
4. **Subida a phpVMS, con el interruptor apagado.** `AtcTranscriptService` y el POST, con
   `sayintentions_transcript_enabled` en `App.config` **a `false`**. Se enciende solo
   cuando phpVMS confirme el endpoint de §5.
5. **Arranque en vuelo reanudado.** Enganchar también `CheckAndResumeFlight` (§4). Es un
   arreglo pequeño y sin él la fase 2 no funciona tras un reinicio.
6. **Contadores para el log de cierre.** Entradas recibidas / enviadas / descartadas y
   motivo, en una línea, para poder decir «funcionó» sin volcar la conversación.

**Tests** (en `vmsOpenAcars.Tests/`, con el patrón de la casa: datos reales, nada de
geometría inventada):
- `SayIntentionsCommsParserTests` — el parser puro, con el JSON de la documentación.
- `SayIntentionsSessionTests` — lectura del sobre `flight_details`, campos ausentes, JSON
  vacío.
- Sin test para el cliente HTTP ni para el servicio: son I/O, y la casa no los prueba con
  mocks (no hay ninguno en el proyecto).

---

## 10. Lo verificado y lo NO verificado

**Verificado hoy (2026-10-07):**

| Qué | Cómo |
|---|---|
| El endpoint existe y valida la clave antes que nada | `GET /sapi/getCommsHistory` sin clave → `200 {"error":"No api_key provided"}`. Una ruta inventada devuelve `200 {}` |
| El contrato de parámetros y respuesta de §1 | Documentación oficial de SAPI (`portal.sayintentions.ai/p2d/docs/`), actualizada el **2026-10-07** |
| Los nombres invertidos y el `*_english` | Texto explícito de esa misma documentación |
| `IApiService.PostJsonAsync` sirve para una ruta nueva | `Services/ApiService.cs:784-795` |
| El arranque y el fin del vuelo, con sus dos huecos | Código: `FlightManager.Lifecycle.cs:115-117`, `AcarsReporter.cs:162/170-173/501-552`, `MainViewModel.cs:73/159/773/792` |
| La API de LVARs ya está en la DLL distribuida | `fsuipcClient.xml:3798-3827`, `5122-5233` |

**NO verificado — y por tanto no se promete:**

1. **La forma real de la respuesta** de `getCommsHistory` con una clave de piloto y vuelo
   activo. §1 es la documentación, no una observación nuestra. Hay que hacerlo en el paso
   3 del plan, con la clave de un piloto, en su PC.
2. **Si los errores llegan con HTTP 200 también en este endpoint.** La medición de «siempre
   200» (§7 de la base) es de `importVAData` y `getWX`, no de este. El cliente **no** se fía
   del código HTTP: mira el cuerpo, siempre.
3. **Si `comm_history` viene acotada** (¿las últimas N?, ¿todo el vuelo?) y **si `since_id`
   sobrevive a un cambio de `flight_id`**. El diseño asume lo conservador: contador a cero
   cuando cambia la sesión y tramos numerados.
4. **Que `FSUIPCConnection.WriteLVar` funcione sin el WASM de FSUIPC7.** Afecta a la fase 3
   (anuncios de cabina), no a esta, pero conviene medirlo antes de prometerla.
5. **Que un adaptador SimAPI pueda escribir un LVAR.** La documentación dice que los
   adaptadores pueden leer y escribir LVARs «donde sean escribibles», pero el fichero de
   salida que documenta (`simAPI_output.jsonl`) trae un ejemplo de **K-Var**
   (`{"setvar": "COM_STBY_RADIO_SET_HZ", …}`) y no de LVAR. Es pregunta para SayIntentions.

---

## 11. Preguntas que hay que cerrar

**Para phpVMS:**

1. ¿Quieren el endpoint de §5 (`api/pireps/{id}/atc-transcript`) o prefieren que la
   transcripción entre por otro sitio? ¿Qué forma de payload les sirve?
2. **Idempotencia**: ¿pueden hacer *upsert* por `(pirep_id, flight_id, id)`, o hay que
   enviar cada lote una sola vez y asumir que un reintento duplica?
3. ¿Guardan la transcripción **localizada**, la **inglesa** o las dos? (Nosotros enviamos
   las dos y que decidan.)
4. **Retención y visibilidad** (§8): cuánto se guarda, quién lo ve y si hay que anonimizar.
5. ¿Quieren los `lat`/`lon` por entrada y la `frequency`, o es ruido para su modelo?

**Para SayIntentions (vía `va@sayintentions.ai`), añadidas a las tres de la base §11:**

6. ¿`comm_history` viene acotada en tamaño o hay que recorrerla por tramos? ¿`since_id`
   se puede usar aunque haya cambiado el `flight_id`?
7. ¿Un adaptador **SimAPI** puede escribir un LVAR (por ejemplo
   `SIAI_CABIN_ANNOUNCEMENTS_DISABLE`) desde `simAPI_output.jsonl`, o la salida es solo de
   K-Vars? Es la pregunta que decide si la fase 3 funciona también con adaptadores.
8. El artículo canónico del Help Center sobre LVARs **no lista** `SIAI_CABIN_ANNOUNCEMENTS_DISABLE`,
   `SIAI_SELCAL_PENDING_COM1/2` ni los botones de llamada, que sí están en la documentación
   del portal fechada el 2026-10-07. ¿Cuál es la buena, y se puede actualizar la otra?
