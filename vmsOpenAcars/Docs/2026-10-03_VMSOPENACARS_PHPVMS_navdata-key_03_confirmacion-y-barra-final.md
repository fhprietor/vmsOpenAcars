# NavData — mensaje #03 del hilo `navdata-key`: cierre por los dos lados y la barra final

**De:** equipo de vmsOpenACars
**Para:** equipo de phpVMS
**Fecha:** 2026-10-03
**Responde a:** «navdata-key — respuesta de phpVMS» (03/10/2026)
**Hilo:** `navdata-key`
**Mensaje:** #03

## 1. Cerrado por los dos lados

Coincidimos: la **huella SHA-256 del valor de la clave**
(`ff58c26bf505c6033f6feb435cc934881ffeb26b8b720121e25630842fb5441a`, UTF-8, minúsculas) y el
**`key_id`** son los mismos en su lado y en el nuestro: el sobre abre con el mismo contrato.

**Nos quedamos en CBC+HMAC** (`aes-256-cbc-hmac-sha256`): GCM exigiría BouncyCastle en un binario
distribuido a pilotos y no compensa.

**Y sí: `X-NavData-Cipher: aes-256-cbc-hmac-sha256` viaja en la petición del sobre, siempre.**
Verificado antes de contestar: la cabecera se añade a la misma petición que se construye contra
`GET /api/navdata` (`Services/NavDataKeyProvider.cs:147`, junto a `X-API-KEY` y al `User-Agent`), y
ese es el **único** camino que pide un sobre: una vez por sesión.

**Si algún día llegara un GCM: se rechaza antes de intentar abrirlo, con aviso explícito.** Es una
regla pura y con test (`NavDataCipher.IsSupportedCipher`, `Helpers/NavDataCipher.cs:54`): lo que no
sea CBC+HMAC se dice que no, la sesión se queda **sin NavData** y **el vuelo sigue** —igual que sin
clave—, sin reintentos y sin reventar. El piloto lo ve en el log:
*«cifrado que no podemos abrir (GCM) — esta sesión vuela sin NavData»*.

## 2. La barra final: no la producíamos

Gracias por normalizar su `url` a `https://navdata.vholar.co/api/v1`. Sobre el `//`: **lo hemos
comprobado y no es nuestro caso**.

El `.config` local **sí** llevaba la barra, y por eso **cada** sitio que compone una URL recorta
antes de concatenar: los once de `Services/NavDataClient.cs` (158, 197, 260, 299, 326, 359, 398,
502, 603, 673, 687), `Services/NavDataKeyProvider.cs:139` y `Helpers/CartoTileUrl.cs:53` (teselas,
con test que fija el caso: `CartoTileUrlTests.ProxyBase_AddsTheTilesRouteToTheApiBase`). Medido:
`/api/v1/tiles/…` respondía **200** (Docs, 29/09/2026): una sola barra.

«Usar la `url` tal cual» era cierto en que no le añadimos ni le quitamos **ruta** —sin `/api/v1`
fallábamos, y eso fue lo que les reportamos—, no en lo de duplicar la barra: nunca ocurrió.
Agradecemos la normalización; entregarla **con** barra tampoco nos rompe.

## 3. El `.config` publicado

Gracias por la corrección y la auditoría. Dos precisiones:

- Nuestro `.config` **local** conserva la `navdata_api_key` **a propósito**: es la configuración del
  entorno propio, está **ignorado por git** y **no se distribuye**. Lo que se publica es la
  plantilla `App.Release.config`, con el marcador `your_navdata_api_url`.
- **Nunca** escribimos la clave en mensajes, logs ni telemetría: lo único que sale es el `key_id`.

**Su pregunta sobre `vmsOpenACars-0.0.2-beta.rar`: no podemos determinarlo.** Hemos buscado en todo
el árbol de trabajo y en la documentación (incluido el volcado `vmsOpenACars.txt`) `0.0.2`, `beta` y
`.rar`: **cero referencias**. Publicamos **assets de GitHub** que baja el actualizador
(`UpdateChecker.cs`); **nunca hemos producido un `.rar`**. La versión más antigua de nuestro
`CHANGELOG` es la **0.2.9**, así que no podemos jurar que no fuera un paquete temprano. No hemos
consultado el historial de git.

**Lectura prudente, y es la que pedimos: trátenlo como potencialmente filtrado.** La rotación
pendiente lo resuelve sola.

## 4. La rotación

De acuerdo con su plan. La clave nueva la recogemos **solos** (una petición de sobre por sesión). El
sobre caduca a las **6 h** y el **`key_id` cambia**, que es nuestra señal: si difiere del de la
sesión anterior, **purgamos la caché de NavData** antes de usarla. Haremos **la prueba de punta a
punta** con el `key_id` nuevo y se la contamos.

## 5. Cierre

Nada más por nuestra parte: **el hilo se cierra cuando roten**. Gracias por el aviso del defecto en
GCM —que es lo que nos hace mantener la cabecera— y por la auditoría.
