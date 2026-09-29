# NavData → vmsOpenACars: la clave en la URL, resuelta (solo en la ruta de teselas)

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** `RESPUESTA3-NAVDATA-TILES-2026-09-30.md`
> **Estado:** el bloqueante está resuelto y verificado. Podéis pedir teselas al proxy.

---

## 1. La URL que podéis construir (una línea, como decíais)

```
GET {navdata_api_url}tiles/{style}/{z}/{x}/{y}.png?key={api_key}
```

Verificado ahora mismo, **solo con la clave en la URL y sin ninguna cabecera**:

```
?key=... por la URL pública -> 200, 2.355 bytes
/airport/SKBO/?key=...      -> 401   (la concesión es solo de la ruta de teselas)
tesela sin clave            -> 401
```

Así que GMap.NET puede construirla tal cual. Si además podéis añadir
**`&origin_domain=vholar.co`**, mejor: mantiene la comprobación de dominio que el resto de la API
exige. Si no lo añadís, en esa ruta se da por buena la del dominio registrado de la propia
aerolínea; funciona igual, simplemente se comprueba una cosa menos.

**El compromiso, dicho claro:** una clave en una URL está más expuesta que en una cabecera (viaja
en registros y cachés de quien la maneje). Por eso la admitimos **solo** en esta ruta —en el resto
sigue habiendo `401` sin cabeceras, y hay un test que lo fija—, la respuesta va `private` para que
ninguna caché compartida la guarde, y lo que desbloquea esa clave ahí son teselas de mapa: el daño
de una filtración se limita a consumo de cuota, no a datos de la API.

## 2. Sobre `month`/`plan_limit`: sí, van dentro de `tiles`

Bien visto, y es deliberado: `{ "tiles": { ..., "month": "2026-09", "month_tiles": 0,
"plan_limit": null, "month_used_pct": null } }`. Vuestro parser lo lee donde está; lo aclaramos por
escrito para que no se lea como si fuera hermano de `tiles`.

Y nos parece bien que pintéis **«—»** mientras venga nulo: prefiero que un dato ausente se vea como
ausente.

## 3. Lo vuestro, anotado

- **v0.9.17**: purga de teselas de más de 30 días al abrir el mapa, acción «Borrar caché del mapa»
  y `CartoTileUrl.TileFailures`. Esa es exactamente la obligación que os toca cumplir, y la tenéis.
- **El contador de fallos** pasa a ser, en la práctica, el de las caídas a CARTO directo: cuando
  adoptéis el proxy, mandadnos el número de vez en cuando. Es lo que nos dirá si el proxy está
  cumpliendo o si os está costando más de lo que ahorra.
- Cuando los dos proveedores apunten al proxy, avisad: miramos juntos el `tiles-stats` unos días
  (aciertos, consumo aguas arriba y `placeholder`) para confirmar que la caché amortigua lo que
  esperamos.
