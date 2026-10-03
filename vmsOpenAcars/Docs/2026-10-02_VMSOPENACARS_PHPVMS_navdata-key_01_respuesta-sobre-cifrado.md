# NavData — respuesta al hilo `navdata-key`: el sobre cifrado

**De:** equipo de vmsOpenACars
**Para:** equipo de phpVMS
**Fecha:** 2026-10-02
**Responde a:** «Entrega de la clave de NavData — phpVMS → vmsOpenACars»
**Hilo:** `navdata-key` (nuevo)

## 1. Confirmamos las tres cosas que piden

**(a) Sí abrimos el sobre, y aquí está la prueba.** Llamada en vivo a `GET /api/navdata` con
**`X-API-KEY`**: el sobre abrió y devolvió

```
url        https://navdata.vholar.co
key_id     604ec20719402901
issued_at / expires_at   vigencia de 6 h
clave      longitud 20, prefijo vhr-
```

Para que puedan comparar **sin que la clave vuelva a viajar en un mensaje**, esta es su huella
(SHA-256 del valor de la clave en **UTF-8**, hex en minúsculas):

```
ff58c26bf505c6033f6feb435cc934881ffeb26b8b720121e25630842fb5441a
```

**(b) Nos quedamos en CBC+HMAC.** Implementado con **BCL pura** de .NET Framework (`Aes`,
`HMACSHA256`, HKDF-SHA256 propio contra los vectores del RFC 5869). GCM exigiría BouncyCastle en un
cliente que se distribuye a los pilotos, y no compensa. El contrato se respeta al pie: el **MAC se
comprueba antes de descifrar** y con comparación en tiempo constante, el **IV va autenticado**
(`AAD || iv || ct`) y una `vms_api_key` ajena no abre nada.

**(c) `navdata_api_key` vacía en el `.config` publicado.** Confirmado: ese `.config` ya no lleva la
clave. La plantilla que se publica conserva únicamente el marcador `your_navdata_apikey` como guía
del campo que rellena la aerolínea virtual, nunca un valor real.

## 2. Lo que ya está hecho de nuestro lado

- El sobre se pide **una vez por sesión** (su límite es 30/min por piloto) y vive **solo en memoria**
  hasta `expires_at`. **Nunca** se escribe en el `.config`, en los logs ni en la telemetría; lo único
  que se registra es el `key_id`, que no revela la clave.
- `503 navdata-not-configured`, `401` y `400 unsupported-cipher` dejan la sesión **sin NavData**
  —el vuelo sigue, sin scoring NavData— y **no se reintentan en bucle**.
- Si cambia el `key_id` (rotación dentro del mismo AIRAC), **purgamos la caché de NavData** antes de
  usarla.
- Añadimos la cabecera **`User-Agent: vmsOpenACars/<versión>`** que piden para la auditoría.

## 3. Un desajuste que hay que corregir en su lado

El contrato dice que la `url` es la **base completa** (p. ej. `https://navdata.vholar.co/api/v1`),
no el host. La entrega que hemos abierto trajo **`https://navdata.vholar.co`, sin `/api/v1`**:
su setting está mal configurado. Nuestro cliente la usa **tal cual**, como manda el contrato, así
que con el valor actual las llamadas a NavData fallarían. **Que lo ajusten a
`https://navdata.vholar.co/api/v1`.** Si prefieren que toleremos las dos formas (host y base
completa), que lo digan en el contrato y lo haremos — pero preferimos no adivinar rutas.

## 4. La rotación

Hemos comprobado —**sin imprimirla**— que la clave que entrega el sobre es **idéntica** a la que
estaba publicada en el `.config`, y que **sigue autenticando** contra NavData. Es decir: este cambio
evita que la clave viaje **de aquí en adelante**, pero **no invalida la filtrada**. Recomendamos
**rotarla**, y con el mecanismo nuevo ya no tiene coste para nadie: el sobre la entrega solo.

## 5. Cierre

Pedimos **una entrega de prueba** cuando roten la clave o cuando corrijan el setting de la `url`,
para re-confirmar el descifrado de punta a punta. Confirmamos que **`X-API-KEY` es el header
correcto** (probamos el endpoint y `Bearer` no vale, como avisaban). Sin fechas comprometidas por
nuestra parte.
