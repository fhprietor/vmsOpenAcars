# Novedades de vmsOpenACARS — versión 1.0.3

**En una frase:** el cliente ya usa la clave que te da el inicio de sesión. Antes, en una instalación
nueva, mandaba al servidor el texto de ejemplo de la plantilla y por eso todo daba error de
autorización.

---

## 1. El error de autorización del primer arranque

Si acabas de instalar el cliente, al arrancar verás en el registro:

```
🔑 INICIANDO SESIÓN
❌ Error de login: Server Error (401 Unauthorized). Check URL: https://vholar.co/api/user
```

No era tu usuario ni el servidor. El cliente leía la clave de **dos sitios distintos**: para decidir
si tenía que pedirte el login miraba la clave guardada (la buena), pero para hablar con el servidor
usaba lo que hubiera escrito en el fichero de configuración, que en una instalación nueva es el texto
de ejemplo `your_phpvms_apikey`. Ahora **los dos miran lo mismo**: la clave que te dio el login.

## 2. Y en Ajustes

Los campos de **URL API** y **API Key** enseñaban ese mismo valor crudo, así que al guardar podían
volver a escribir el texto de ejemplo. Ahora muestran lo que el cliente usa de verdad.

---

## Lo que hay que saber

- **Mismo zip, mismo actualizador, tu configuración y tu acceso intactos.**
- Si ya habías puesto tu clave a mano en Ajustes, esto no cambia nada para ti: se sigue usando la
  misma.
- Si no habías entrado nunca y el cliente te dio ese 401, con esta versión funciona sin que toques
  nada: la clave que guardaste al iniciar sesión es la que se usará.