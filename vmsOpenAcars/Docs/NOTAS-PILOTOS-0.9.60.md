# Novedades de vmsOpenACARS — versión 0.9.60

**En una frase:** el aviso de «actualización disponible» deja de enseñarte el Markdown en crudo y
muestra un resumen que se lee de un vistazo.

---

## Qué cambia

El diálogo **Actualización disponible** metía en el cuadro el fichero de novedades tal cual, con sus
`#` de título y sus `**negritas**` a la vista. Ahora lo convierte a texto plano: quita las marcas,
pone viñetas `•` y recorta a unos 600 caracteres, cortando en una palabra completa y terminando en
`…`, porque las notas completas ocupan unos 1.500 y no caben con holgura en la ventana.

## Lo que hay que saber

- **Si venías sin poder actualizar, esta es tu versión.** El fallo `The file '…\pdfium.dll' already
  exists` era del **paquete** que se descargaba —traía el mismo fichero dos veces y le faltaban las
  carpetas `x64\`, `x86\` y `nl\`—, no de tu equipo. El paquete de esta release está corregido, así
  que se instala sola: no hay que borrar nada ni descargar nada a mano.
- Nada que configurar.