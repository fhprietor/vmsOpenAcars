# Novedades de vmsOpenACARS — versiones 0.9.25 a 0.9.27

**En una frase:** la clave de NavData ya **no se guarda en el archivo de configuración** del
programa. Ahora el ACARS la pide a la web de la aerolínea al arrancar, cifrada, y la mantiene
**solo en memoria** mientras dura la sesión.

---

## Qué cambia para ti

- **No tienes que hacer nada.** No hay que pegar ninguna clave ni editar ningún archivo. Si en su
  día te dijeron dónde poner tu clave de NavData, **ya no hace falta**.
- **Ajustes, más simple.** Han desaparecido los campos de *URL de NavData* y *clave de NavData*.
  Se queda el botón **TEST**, que ahora comprueba la conexión con la clave que el programa recibe
  por su cuenta.
- **Todo lo de NavData sigue igual**: pistas, calles de rodaje, puntos de espera y las ayudas a la
  aproximación, además de los cuatro criterios de puntuación que dependen de ello (zona de toma,
  desviación del eje, localizador y mínimos).
- **Si la web no puede entregar la clave, el vuelo no se resiente.** Se pierden únicamente esos
  cuatro criterios de NavData, el programa lo deja anotado en el registro y todo lo demás
  funciona con normalidad. Nada de errores ni de ventanas emergentes.

## Por qué se ha cambiado

Antes, la clave de NavData viajaba **dentro del archivo de configuración** que se descarga junto
con el programa: cualquiera que tuviera ese archivo podía usarla. Ahora llega **cifrada con la
clave personal de cada piloto** en la web de la aerolínea, se pide **una sola vez por sesión** y
**no se guarda en ningún sitio** —ni en el disco, ni en los registros—.

## Recomendación

**Actualiza si sigues en una versión anterior a la 0.9.25.** Las versiones antiguas leen la clave
del archivo de configuración, y ese archivo **ya no la lleva**: en esas versiones se pierden los
cuatro criterios de NavData de la puntuación. Al actualizar, vuelven solos.

---

*El resto del ACARS no cambia: ni el registro de vuelo, ni las puntuaciones, ni la conexión con la
web de la aerolínea.*
