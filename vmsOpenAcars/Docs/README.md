# Documentación de vmsOpenAcars

Los archivos **`.md` son la única fuente de verdad**. No se guardan artefactos generados
(PDF, HTML, PNG, JPEG) en el repositorio: pesaban ~12,5 MB, se quedaban desactualizados en
silencio y llegaron a publicarse versiones con 5 releases de retraso.

| Documento | Para quién | Contenido |
|---|---|---|
| `BRIEFING.md` | **Pilotos** | Guía de usuario: configuración, flujo de vuelo, scoring, mapa, LOGBOOK |
| `PRIMEROS_PASOS.md` | **Pilotos nuevos** | Alta en phpVMS, API key, descarga e instalación |
| `NOTAS-PILOTOS-0.9.25-a-0.9.27.md` | **Pilotos** | La clave de NavData **deja de viajar en el `.config`** (llega en un sobre desde phpVMS), el **combustible en las unidades que espera la web** y la retirada de la URL/clave de NavData del formulario |
| `NOTAS-PILOTOS-0.9.30-a-0.9.33.md` | **Pilotos** | Zona de toma **proporcional a la pista**, la guía de rodaje que se puede apagar, la **meteorología del aterrizaje** en el logbook y el análisis del **flare** (traza fina a 10 Hz, window propia y closeup) |
| `NOTAS-PILOTOS-0.9.34.md` | **Pilotos** | La traza fina del flare **se medía y se tiraba antes de guardarse**: qué falló, qué **no** se vio afectado y qué no se puede recuperar |
| `NOTAS-PILOTOS-0.9.35-a-0.9.36.md` | **Pilotos** | Los **flaps** y el **corte de potencia** en el analisis del aterrizaje (y por que la etiqueta de flaps a veces lleva `~`), y la **cabecera de datos para compartir el grafico del flare** con su boton PNG y el **LTOW** |
| `NOTAS-PILOTOS-0.9.37.md` | **Pilotos** | La traza del flare **ya guarda el tramo de la frenada** (antes caía en `dist = 0`) y mide desde el **mismo umbral** que la toma —496 ft de desfase en la 17 de SKBG, **sin volar todavía**—, y la cadencia real de la traza: **~8 Hz, no 10** |
| `NOTAS-PILOTOS-0.9.43.md` | **Pilotos** | El vuelo que **no se podía enviar** (el falso touch-and-go del rebote en la toma, la fase que se quedaba en el aire y el AGL medido contra el origen) con el **ON BLOCK manual** como salida, por qué el motor único **no dio los +5** (46 % contra el 50 %), y las tres cosas que cobraban o avisaban **sin motivo** (luces de taxi del pushback, tope del pushback y el aviso de otro aeropuerto) |
| `NOTAS-PILOTOS-0.9.44.md` | **Pilotos** | Los **botones** del aviso de confirmación, en tu idioma: estaban escritos a mano en inglés dentro del popup que usa toda la aplicación (el de «¿Salir de la aplicación?» se leía con YES y NO debajo) |
| `NOTAS-PILOTOS-0.9.45.md` | **Pilotos** | Los botones como **teclas encendidas de MCDU** (y por qué hubo que aclarar los colores: de 1,75:1 a 4,29:1 de contraste, por debajo del 4,5:1 que se pide), la **carcasa y las pantallas** del MCDU —que se apagan con `mcdu_look`— y los **28 rótulos** que seguían en inglés con la traducción ya escrita |
| `NOTAS-PILOTOS-0.9.49.md` | **Pilotos** | En X-Plane el tipo de avión se lee del propio avión (no del nombre de la carpeta): el A320neo deja de figurar como A320 y deja de avisar de discrepancia contra tu OFP, qué pasa si el fichero no se puede leer y por qué el aviso de un CEO real sigue apareciendo |
| `NOTAS-PILOTOS-0.9.51.md` | **Pilotos** | El registro dejaba de decir la verdad: el **piloto automático** salía «enganchado» siempre (se deducía del rumbo) y el **autobrake** decía «RTO» en todos los vuelos; ahora se leen las banderas y el selector reales, se leen los **carteles de cabina** y hay una línea `🛠` de diagnóstico para el vuelo de prueba |
| `NOTAS-PILOTOS-0.9.50.md` | **Pilotos** | El registro decía lo que no era: turbulencia en pleno ascenso (se mide la **oscilación**, y «pueden moverse» solo en crucero), spoilers leídos como interruptor (**armado no es desplegado**; 26 líneas en 44 s), reversas informadas al **0 % antes de aplicarlas**, la desviación del localizador **cobrada dos veces** y la rotación duplicada |
| `NOTAS-PILOTOS-0.9.48.md` | **Pilotos** | Los anuncios de cabina ya no dependen de que NavData tenga la grabación (si no la tiene, los dice la voz de Windows): tres anuncios nuevos, el aviso de turbulencia al entrar y al salir, el destino por su nombre completo, la hora local y lo que hay que saber (voz instalada, hora aproximada, la turbulencia se mide y no se lee el letrero) |
| `NOTAS-PILOTOS-0.9.47.md` | **Pilotos** | El rodaje se medía con la velocidad de tierra y esa velocidad miente con el avión parado: lo que cambia (posición en vez de velocidad, sin motor no hay rodaje, los metros en el registro) y lo que **no** cambia (la regla sigue en el 50 %; los vuelos ya enviados no cambian de nota) |
| `NOTAS-PILOTOS-0.9.46.md` | **Pilotos** | Por qué hay una 0.9.46 con el mismo contenido que la 0.9.45: el arreglo se republicó **con el mismo número** y el Updater compara números, así que quien hubiera instalado la primera 0.9.45 (la rota) no lo habría recibido nunca |
| `CAMBIOS-PENDIENTES-RODAJE.md` | **Mantenedor** y **Desarrolladores** | Lo que queda del criterio de motor único tras medir el movimiento por desplazamiento (v0.9.47): publicar las cifras a phpVMS (el campo lo crea la aerolínea), la migración de `landing_log.sqlite` —con el aviso de que el `SELECT` mapea **por índice**—, la gracia de las luces desde el movimiento real y el crédito proporcional, que necesita corpus |
| `COMUNICADO-PILOTOS-ZONA-DE-TOMA.md` | **Pilotos** | Comunicado a los pilotos sobre la penalización de la **zona de toma**: la regla proporcional, el techo de 3.000 ft que no se mueve y las pistas cortas que no cambian |
| `PROPUESTA-MCDU-BOTONES.md` | **Mantenedor** y **Desarrolladores** | La propuesta que se aprobó y **se implementó en v0.9.45**: los botones con cuerpo negro estilo MCDU A320 **retroiluminados desde atrás** con el color de cada uno (lavado interior, halo de la leyenda y fuga por la junta). Con el argumento medido (los ocho colores de v0.9.44 dan 1,75–4,29:1 como leyenda sobre negro, y el rojo puro se queda en 4,39:1 con la luz encendida), las dos variantes de paleta, el diff, el test de contraste contra el fondo iluminado y **§9, el cierre con las cuatro diferencias entre lo propuesto y lo entregado**. Láminas en `Tools/McduPreview/out` |
| `PROPUESTA-MCDU-CARCASA.md` | **Mantenedor** y **Desarrolladores** | La propuesta que se aprobó y **se implementó en v0.9.45**: los botones con cuerpo negro estilo MCDU A320 **retroiluminados desde atrás** con el color de cada uno (lavado interior, halo de la leyenda y fuga por la junta). Con el argumento medido (los ocho colores de v0.9.44 dan 1,75–4,29:1 como leyenda sobre negro, y el rojo puro se queda en 4,39:1 con la luz encendida), las dos variantes de paleta, el diff, el test de contraste contra el fondo iluminado y **§9, el cierre con las cuatro diferencias entre lo propuesto y lo entregado**. Láminas en `Tools/McduPreview/out` |
| `ESTADO-FLARE-2026-10-08.md` | **Desarrolladores** | Estado y **deuda de verificacion** del bloque del flare (0.9.31 a 0.9.37): lo entregado con punteros al CHANGELOG, **lo que NO esta verificado** (la cadencia real de la traza, el desfase del umbral legal, LTOW, las escalas de flaps/N1, el radioaltimetro por addon y el camino de `SendPirep`) y los abiertos del tema, para continuar en un hilo nuevo |
| `architecture.md` | **Desarrolladores** | Arquitectura, módulos, esquemas de BD, notas de build |
| `SETUP-ENTORNO.md` | **Desarrolladores** | Cómo montar el proyecto en otro equipo: qué no viaja por git (`App.config`, `packages/`), toolchain, y `dsh` (DeepSeek Harness) |
| `PUBLICA-RELEASE.md` | **Desarrolladores** y **agente** | La receta de **«PUBLICA RELEASE»** paso a paso: versión, CHANGELOG, notas del piloto, build, el zip sin el `.config`, commit y push, etiqueta, Release con el binario y verificación. Con las trampas del entorno del agente (los pathspecs de git y el caso `vmsOpenACars`/`vmsOpenAcars`) |
| `2026-10-08-DSH-SHELL-BLOQUEADO-WINDOWS.md` | **Desarrolladores** | Cuando `dsh` deja de ejecutar **cualquier** comando en Windows: **las dos causas** —el `SeSecurityPrivilege` que falta si el servidor no se lanzó elevado (PC de casa) y el permiso que falta en la raíz del workspace (portátil, §6)—, cómo distinguirlas con el script de diagnóstico, reproducción, reparación y verificación. Nota de entorno, no del cliente |
| `PEDIDO-PHPVMS-PRUEBAS.md` | **Equipo de phpVMS** | Lo que hace falta para validar con PIREPs reales, **solo los producidos por vmsOpenACars**: filtro por productor y sin el tope de 20 (el único vuelo con datos de rodaje cae fuera), `source_name` en los 13 que lo traen vacío, la pista usada, la ruta de rodaje declarada y el estado de moderación |
| `PEDIDO-PHPVMS-ANALISIS-FLARE.md` | **Equipo de phpVMS** | Pedido para **almacenar y servir el análisis del aterrizaje** (traza fina del flare + métricas): endpoint de ingesta `POST /pireps/{id}/landing`, migración (métricas en columnas + traza en JSON), convenciones del payload (signo de `dist_ft`, `null`=sin dato), volumen (~1 MB/mes) y hoja de ruta (agregación → IA → web) |
| `RESPUESTA-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Nuestra respuesta a su aceptación**: las seis preguntas que nos hicieron contestadas contra el código (tipos enteros, el centinela `-1` que no viaja, los topes de longitud, `null` explícito), cómo se comporta el envío en el cliente y qué queda por verificar (el vuelo real) |
| `RESPUESTA2-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Segunda respuesta**, tras verificar ellos los dos aterrizajes contra su log ACARS: la cadena exacta de `source_name` de los rellenos, el aviso de publicación de la 0.9.37 y cómo se mandaría la serie histórica, más el METAR medido (81 caracteres el más largo de 44 vuelos) |
| `RESPUESTA3-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Tercera respuesta**: el tercer positivo reenviado (y por qué se escapó), la lista de los 10 vuelos sin PIREP **con la advertencia de que la ventana de 60 PIREPs está incompleta**, y la corrección a su modelo del campo que destapó **un fallo nuestro**: la copia del toque puede discrepar del fileo |
| `RESPUESTA4-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Cuarta respuesta**, sobre su gráfico del flare: sus chips comprobados muestra a muestra contra nuestra traza (la aritmética cuadra), los **496 ft de desfase del cero** en el vuelo 44 —dato nuestro, previo a la 0.9.37, con la traza alineada ofrecida—, la pregunta del **«VS a 10 ft»** (que no sale de ninguna de las dos alturas de la traza) y el mismo eje X para los dos paneles |
| `RESPUESTA5-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Quinta respuesta**, tras corregir ellos el gráfico: sus tres arreglos **comprobados contra la traza** (las puertas de 50/30 ft sí están sostenidas y la de 10 ft no), los dos matices de la **deducción del eje** (el signo que hay que sumar y el flag `on_ground`, con la monotonía como guarda), por qué el vuelo 43 es **discontinuo** y no «fuera de pista», la **errata del 17 → 16** y el **piloto** (`pilot_id 55`) que les faltaba para cerrar los 10 vuelos |
| `RESPUESTA6-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Sexta respuesta**: el fallo de **mi regla de emparejamiento** (un campo vacío es comodín, no igualdad) con la regla corregida reproduciendo sus 2 de 10, el **#1 cerrado** como sin PIREP, los **7 vuelos** con hora exacta y la pregunta que toca (buscar por piloto + ventana y con `deleted_at`, no por número) y el **inicio de flare** como meseta en vez de mínimo exacto |
| `RESPUESTA7-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Séptima respuesta**: los dos campos que piden, **aceptados** con la regla del `0` contra `null` escrita; de dónde sale cada dato (`offset_threshold_ft` de NavData, `vref_kt` del TLR de SimBrief) y por qué **no** derivamos uno del tipo de avión; y los cinco opcionales uno a uno —**el viento instantáneo y el peso ya los tienen**, el A/P y el motivo del cierre de la traza se pueden mandar, el A/T y el CG no |
| `RESPUESTA8-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Octava respuesta** (y **no llegó a su destino**): **las siete horas** que les faltaban (no les llegó la (6)) con la calibración de la ventana (**+79 s / +302 s**, siempre positivo), el **`forceDelete()`** que cierra los 6 números como no recuperables, la **errata nuestra** de `trace_stop_reason` (lo propusimos y no lo enviábamos: ya viaja, con el `null` como «no hubo cierre») y los tres campos confirmados bajo **0.9.38** |
| `RESPUESTA9-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Novena respuesta, y la que sí lleva todo** (tampoco les llegó la (8), así que es autosuficiente): **las siete horas** otra vez con su calibración, la **corrección del offset aceptada** —la 0.9.37 ya alinea en cliente, así que el desplazamiento es `−residual` y el offset queda como red de seguridad y validación— escrita en el CHANGELOG y en `ESTADO-FLARE`, el **`trace_stop_reason`** con sus cuatro motivos y el `null` como «no hubo cierre», los tres campos bajo **0.9.38**, y la **verificación en vuelo** de la alineación (residual +0,003 en `mdXr7GYgEp1GwN3o`) |
| `RESPUESTA11-PHPVMS-ANALISIS-FLARE-2026-10-12.md` | **Equipo de phpVMS** | **Respuesta (11)**: la **clave exacta** de `trace_stop_reason` —nombre, que va **dentro de `metrics`** y no en la raiz ni en cada muestra—, con su tope de **32 recortado por la cola** (el prefijo sobrevive), los **cuatro motivos** de hoy, el `null` como «no hubo cierre» y las dos versiones: viaja **desde la 0.9.38**, no en la 0.9.37 |
| `RESPUESTA10-PHPVMS-ANALISIS-FLARE-2026-10-11.md` | **Equipo de phpVMS** | **Décima respuesta**: los 6 números **cerrados con causa** por los dos lados (cron horario que purga los PIREPs **cancelados** a las 12 h, con `forceDelete`; los rechazados sobreviven), y **un fallo nuestro que su respuesta destapó**: recortábamos `trace_stop_reason` a **64** cuando su validación admite **32**, así que un motivo largo habría **tumbado el envío entero** —corregido, con prueba, y con el prefijo a salvo—. Incluye la pregunta de si la purga arrastra la fila del análisis, y la sugerencia de añadir `source_name` a su línea de log |
| `RESPUESTA-PHPVMS-PRUEBAS-2026-09-29.md` | **Equipo de phpVMS** | Nuestra respuesta: aceptadas sus dos correcciones (la fase va en `status` y no en `phase`; `state=2` es `ACCEPTED`), los nombres de campo decididos (`departure-runway`, `arrival-runway`, `Taxi Route`) y nuestra postura sobre `fuel`, el backfill y el endpoint global |
| `ANADIDO-PHPVMS-CIERRE-2026-09-29.md` | **Equipo de phpVMS** | Añadido al cierre, con la prueba en vivo: `type = 2` se acepta y **no se sirve** (corregido en el cliente) y **`fuel` sigue sin persistirse** en su lado, con la petición y la respuesta exactas |
| `api_vms.md` | **Equipo de phpVMS** (copiado) | Contrato de su API REST: la **tabla de tipos** de `acars` (`0` posiciones, `1` ruta, `2` mensajes), `acars/logs` GET y POST, `fields`, y la nota de que en `fields` va el **nombre** y no el slug |
| `PEDIDO-NAVDATA-TILES.md` | **Equipo de NavData** | Pedido de un endpoint proxy de teselas de CARTO con caché (para dejar de distribuir la clave del mapa) |
| `2026-10-07_VMSOPENACARS_SAYINTENTIONS_sayintentions_01_base-integracion.md` | **Desarrolladores** | Base del hilo de **SayIntentions.AI**: qué canal es de quién (§1), el programa VA-Link, `flight.json`, el catálogo de SAPI, los LVARs, la colisión de anuncios de cabina y lo verificado contra la API real — más un **anexo** que contrasta todo con la documentación oficial de SAPI del 07/10/2026 |
| `TRANSCRIPCION-ATC.md` | **Desarrolladores** | Diseño de la fase 2 del hilo de SayIntentions: el contrato de `getCommsHistory`, el sondeo incremental por `since_id`, el ciclo de vida del vuelo, el payload propuesto a phpVMS y **lo que no está verificado**. Nada implementado |
- `PEDIDO-NAVDATA-CONECTIVIDAD-2026-09-30.md` — **el pedido de conectividad** (destapado por el rodaje de
- `vmsOpenACars-a-NAVDATA-conectividad-4-2026-09-30.md` — **[NUESTRA]** entrega el **corpus de rutas escritas**
  (`rutas-escritas-2026-09-30.csv`: **una** ruta, LMML/05 `T J K L`) y explica el hallazgo: **los 37 vuelos del
  corpus tienen `Taxi Route` vacio**, porque el cliente solo lo envia al confirmar el popup con texto editado
  —«0 de 0» suyo y «0 de 37» nuestro son el mismo hecho—. Con `useNodeId` verificado y desbloqueado (7 de 7).- `respuesta5-a-vmsOpenACars-conectividad-2026-09-30.md` — **[SUYA]** la opcion 1 aplicada (tope de puentes a
  **300 m**, separado del de huecos) y con ella **7 de 7 aeropuertos con `components_after_joins: 1`**: LMML pasa a
  **1** con el puente nuevo de 298,3 m (conf **0,30**, giro **5,3 grados**, `crosses_runway`). Su §3 convierte
  nuestra condicion 2 en medible (cuantas observaciones contienen el transito `A1 -> B`) y avisa de que hoy la
  respuesta es **"0 de 0", que no es "nadie lo rueda"**.- `vmsOpenACars-a-NAVDATA-conectividad-3-2026-09-30.md` — **[NUESTRA]** la decision sobre `A1`<->`B` (**opcion 1**,
  publicarla con confianza muy baja, con dos condiciones: no obliga a nada hasta medirla, y la juzga el dato real
  de la base de conocimiento), el quinto bug suyo (el hueco de **1,3 m** de LEMD y la busqueda de 600 nodos) y la
  lista de **seis de siete** aeropuertos conectados para poder pensar en encender `useNodeId`.
- `respuesta4-a-vmsOpenACars-conectividad-2026-09-30.md` — **[SUYA]** las uniones que faltan con nombre y
  distancia: **LEMD era un hueco de 1,3 m** (busqueda limitada a 600 nodos; ahora indice espacial, y pasa a
  **1 componente**), `unions_missing` nuevo, 6 de 7 conectados y **LMML con `A1`<->`B` a 298,3 m** pidiendo
  decision nuestra.- `vmsOpenACars-a-NAVDATA-conectividad-2-2026-09-30.md` — **[NUESTRA]** el **cierre del hilo** y el documento a
  entregar: sus tres correcciones verificadas desde fuera (con la tabla y el invariante), nuestro caso pasando su
  comprobador, **los dos arreglos hechos y medidos (15 avisos -> 0)**, el corpus en marcha y lo que queda abierto.- `vmsOpenACars-a-NAVDATA-conectividad-2026-09-30.md` — **[NUESTRA]** verificados sus numeros y el
- `respuesta2-a-vmsOpenACars-conectividad-2026-09-30.md` — **[SUYA]** los **tres descuadres eran suyos y estan
- `respuesta3-a-vmsOpenACars-conectividad-2026-09-30.md` — **[SUYA]** construida la **prueba de aceptacion**
  (`check_written_routes`) y **nuestro caso de LMML pasa**: `LMML/05 "T J K L"` se traza **entero y sin
  heuristica** (4 tramos, puesto a 64,2 m de la red) — la misma ruta que dio 15 avisos de FUERA DE RUTA.
  Traza en anchura sobre el grafo **publicado** (segmentos + empalmes), con test negativo: sin el empalme,
  la misma ruta NO es trazable. Esperan nuestro corpus de rutas escritas.
  corregidos** (`gaps_published` ya sale, contadores deduplicados con el invariante `considered = published +
  rejected + gaps_unaccounted` y un test que lo fija, y `crosses_runway` ya no es motivo de rechazo -> KMIA
  pasa a **2 empalmes**). El puente `G|D` de 162,9 grados baja de 0,38 a **0,27**. Formato del corpus aceptado.
  hallazgo del caso (**`I|F` a 188,3 m es la union que el piloto necesitaba**, publicada gracias al umbral de
  200 m), **tres descuadres** medidos (falta `gaps_published`, los contadores no cuadran y `crosses_runway`
  aparece como motivo de rechazo), el formato del corpus de rutas escritas y nuestro plan para el ruido.
- `respuesta-a-vmsOpenACars-conectividad-2026-09-30.md` — **[SUYA]** el caso destapo **un bug suyo** (solo
  miraban 4 grupos de componentes, y LMML tiene 5), publican `components_after_joins` (**LMML 5->2, KMIA 2->1**),
  los puentes usan el umbral de 200 m que propusimos, el cruce de pista ya no se rechaza (`crosses_runway`) y
  `gaps_unaccounted` = 0 en los siete aeropuertos. Pendiente suyo: la prueba con nuestras rutas escritas.
  LMML `V15MObj3MxOdAZab`): LMML publica **1 empalme** con **5 componentes** y 24 extremos sueltos, asi que la
  ruta que el piloto escribe (`T J K L`) **no es trazable** y el cliente la sustituye por la del grafo —que es
  la que produjo 15 avisos de FUERA DE RUTA en 5 min—. No pide parche de aeropuerto: pide un **criterio comun**
  (`components = 1`, todo hueco <200 m con empalme o `invalid[]`, `gaps_unaccounted`) y apagar la fusion por
  proximidad en el cliente a la vez.
| `PEDIDO-NAVDATA-RUTAS-TAXI.md` | **Equipo de NavData** | Pedido de una base de rutas de rodaje acostumbradas (puesto → pista) que NavData agregue y sirva a toda la comunidad: observaciones, agregación, moderación experta y los tres campos aditivos que hacen falta |
- `PEDIDO-NAVDATA-EMPALMES-2026-09-29.md` — pedido a NavData de **empalmes calculados** (no curados a
- `RESPUESTA5-NAVDATA-EMPALMES-2026-09-29.md` — su respuesta: `stats`, `version`, `confidence` y
- `RESPUESTA-NAVDATA-TILES-2026-09-29.md` — **nuestra respuesta** al proxy de teselas: verificado en
- `RESPUESTA2-NAVDATA-TILES-2026-09-29.md` — **nuestra segunda respuesta**: los tres arreglos
- `RESPUESTA3-NAVDATA-TILES-2026-09-30.md` — **nuestra tercera** (cierre): verificado todo lo suyo y el
  **bloqueante de una línea** para adoptar el proxy — GMap.NET no permite cabeceras propias, así que la
  clave de teselas tiene que poder ir como parámetro (`?key=`), como ya hace CARTO.
- `RESPUESTA9-NAVDATA-TILES-2026-09-29.md` — **su tercera**: purga hecha, `criterion_version` entregado,
- `RESPUESTA10-NAVDATA-TILES-2026-09-30.md` — **su cuarta**: la clave **por la URL** (`?key=`) admitida
- `RESPUESTA4-NAVDATA-TILES-2026-09-30.md` — **nuestra cuarta**: con la clave admitida en la URL, el
- `RESPUESTA11-NAVDATA-TILES-2026-09-30.md` — **su quinta**: el aviso de la clave en el log era real
- `RESPUESTA5-NAVDATA-TILES-2026-09-30.md` — **nuestra quinta (cierre)**: verificacion externa tras su
- `RESPUESTA6-NAVDATA-TILES-2026-09-30.md` — **nuestra sexta (cierre del cierre)**: aceptados los **28
- `CIERRE-NAVDATA-2026-09-30.md` — **su cierre de hilo completo** (9 documentos en dos dias): la tabla de
  lo entregado (`mag_var`, espacios aereos, AIRAC desde fichero, KB de rodaje, puntos de espera, `node_id`,
  empalmes, teselas, `expires_at`), lo que queda nuestro (v0.9.18, el contador de caidas, la clave nueva de
  VHR) y **lo unico suyo: `runway-crossings`**, donde el caso que falta es `has_hold_short: false`.
  dias** de gracia (un ciclo AIRAC), **si al `expires_at`** para que la clave vieja muera sola el
  2026-10-28 —un corte que dependa de que alguien se acuerde es el mismo fallo que ya hemos visto dos
  veces— y **si al porcentaje** de cuota con la etiqueta de proxy de mapas.
  incidente, la auditoria de la clave en nuestro lado (limpia: ni en el historial de git, ni en Docs/, ni
  en `vmsOpenACars.txt`) y **la rotacion que vamos a hacer**, con la unica pregunta que la frena —la
  **ventana de gracia**—, porque cada cliente de piloto usa esa clave.
  (**10 claves de aerolinea en claro en `logs/gunicorn.log`**, `%(r)s` de gunicorn escribe el query string),
  arreglado con `gunicorn.conf.py` y un test que fija el formato; admiten que el primer intento **tumbo el
  servicio unos minutos**; aceptan que el proxy sea el camino y proponen la comprobacion cruzada
  `placeholder` suyo vs teselas con marca de agua nuestras; prefieren `?key=` **y** `origin_domain` juntos.
  **proxy pasa a ser el camino principal** (no la opción) y la caída va en tres escalones: proxy →
  CARTO con la clave del piloto → CARTO sin clave, **con marca de agua**, que es el escalón que se usará
  mientras `carto_api_key` siga vacío en la distribución.
  **solo en la ruta de teselas** (en el resto sigue habiendo 401, con test que lo fija), `origin_domain`
  opcional en esa ruta, y la aclaracion de que `month`/`plan_limit` van dentro de `tiles`. Con esto el
  proxy queda desbloqueado para el cliente.
  el consumo del mes como número, y la adenda donde admiten que **en los endpoints de rodaje el token no
  estaba en la clave de caché** (era decorativo y los incidentes los arregló el vaciado manual).
  verificados con `cf-cache-status` (`private` + `BYPASS` ya desplegados; la copia vieja del borde sigue),
  y la petición de que **el token de formato cambie con el criterio**, porque nuestra invalidación va por el
  hash de nodos y con él habríamos servido empalmes viejos.
- `RESPUESTA8-NAVDATA-TILES-2026-09-29.md` — **su segunda respuesta**, guardada tal cual: el 401 era
  **Cloudflare por delante** de su servidor, `voyager` era un bug real de URL, y su tabla de CYUL estaba
  desactualizada por una caché de 24 h sin subir el token.
  vivo (200, `X-Cache` HIT, `max-age` de 28 días, `tiles-stats`), el fallo de **autenticación después
  de la caché** (las teselas cacheadas se sirven sin clave), la discrepancia de CYUL en su tabla, y lo
  que asumimos nosotros (purga a 30 días, adopción del proxy con caída a CARTO directo).
- `RESPUESTA7-NAVDATA-TILES-2026-09-29.md` — **su respuesta** al pedido, guardada tal cual.
  empalmes **calculados** ya desplegados; su medición de KMIA corrige la nuestra (2 componentes, y el
  destino de esa ruta cae en el componente de los muñones de pista).
  mano), con `confidence`, estadísticas por aeropuerto y criterio que sirva en cualquier red: la fusión
  por proximidad con un umbral fijo de 45 m no puede cubrir a la vez a SKBO y a KMIA (medido).
| `RESPUESTA-NAVDATA-RUTAS-TAXI-2026-09-29.md` | **Equipo de NavData** | Respuesta a su respuesta: verificación de `taxiway`/`taxiways` y `node_id`, el defecto del punto de espera medido (14 puntos, 12 en las paralelas), sí al arreglo de sobre-generación, y tres cosas que necesitamos de ellos |
| `RESPUESTA2-NAVDATA-RUTAS-TAXI-2026-09-29.md` | **Equipo de NavData** | Informe del **banco de pruebas** tras pasar ellos a los tipos de nodo del escenario: la secuencia de avisos del rodaje real cambió (aparece el giro que la sobre-generación se comía, y el hold-short episodio baja a uno), el inventario nuevo fijado en un test, y una pregunta sobre `K1`/`V` |
| `RESPUESTA3-NAVDATA-RUTAS-TAXI-2026-09-29.md` | **Equipo de NavData** | Cierre del caso `K1`/`V` (verificado al metro), confirmación de que los tres endpoints de la base coinciden en cero tras su arreglo de caché, el criterio de `crossings` aceptado, y la corrección de nuestra lectura de las 3 observaciones de prueba |
| `RESPUESTA-NAVDATA-2026-09-29.md` | **Equipo de NavData** | Respuesta al aviso de cambios del 29/09/2026: impacto punto por punto, lo corregido y las tres respuestas que nos piden |
| `CONFIRMACION-NAVDATA-2026-09-29.md` | **Equipo de NavData** | Confirmación y verificación del arreglo de espacios aéreos y del rumbo verdadero, más una petición pequeña (`mag_var` del aeropuerto) |
| `CHANGELOG.md` | Todos | Historial de versiones, con causa raíz de cada corrección |
| `CHANGELOG-0.9.2-0.9.9.md` | Todos | Resumen corto de lo cambiado entre v0.9.2 y v0.9.9 (el detalle está en `CHANGELOG.md`) |
| `feature_map_sidebar.md` | Desarrolladores | Notas de la feature del sidebar de procedimientos |
| `MEMORY.md` | Mantenedor | Índice de mantenimiento — **no es fuente de verdad**, se queda atrás |
| `project_vmsOpenAcars.md` | Mantenedor | Estado de alto nivel (⚠️ escrito a v0.9.2) |

La guía técnica para agentes y mantenedores está en `CLAUDE.md`, en la **raíz** del repo.

---

## Generar los formatos publicables

Cuando haga falta entregar un PDF o HTML (por ejemplo para publicarlo en la web de la
aerolínea), se generan **en el momento** y **fuera del control de versiones**, no se
commitean:

```bash
# PDF (requiere pandoc)
pandoc BRIEFING.md -o BRIEFING.pdf

# HTML autocontenido
pandoc BRIEFING.md -s --metadata title="vmsOpenAcars — Guía del Usuario" -o BRIEFING.html
```

> **Regla:** si generas un artefacto, no lo añadas a git. Un PDF commiteado envejece sin
> avisar y acaba describiendo un producto que ya no existe — que es exactamente lo que pasó
> con el `BRIEFING` de v0.8.7 que se publicó junto a un cliente v0.9.8.
