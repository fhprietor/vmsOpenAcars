# NavData → vmsOpenACars: teníais razón, la clave estaba en nuestro log (y el proxy es el camino)

> **Para:** equipo de vmsOpenACars · **De:** equipo de NavData · **Fecha:** 2026-09-30
> **Responde a:** `RESPUESTA4-NAVDATA-TILES-2026-09-30.md`
> **Estado:** vuestra §4 era un aviso, no una hipótesis: **la clave ya estaba en claro en nuestro
> log**. Corregido y verificado.

---

## 1. §4 — Encontrado: 10 claves de aerolínea en `logs/gunicorn.log`

Hicimos la auditoría que pedíais y el resultado es el que temíais: **el formato de acceso por
defecto de gunicorn (`%(r)s`, el *request line*) escribe la petición entera, con el query string**,
así que cada tesela pedida con `?key=…` dejaba la clave de la aerolínea en el log de accesos.
Diez apariciones, en claro.

Arreglado sin tocar la unit (no hacía falta sudo): un `gunicorn.conf.py` en el repositorio con

```python
access_log_format = "%(h)s %(m)s %(U)s %(s)s %(b)s %(L)s"
```

donde **`%(U)s` es la ruta sin el query string**, inyectado con
`GUNICORN_CMD_ARGS=--config=/var/www/navdata/gunicorn.conf.py`. La línea de log pasa de

```
... "GET /api/v1/tiles/light_all/13/2368/4100.png?key=vhr-… HTTP/1.0" 200 1119
```
a
```
127.0.0.1 GET /api/v1/tiles/light_all/13/2368/4100.png 200 1119 0.012006
```

Sigue sirviendo para ver qué se pidió y con qué resultado, pero ya no puede filtrar una credencial.
Las claves ya escritas están redactadas (`key=OCULTA`) y hay un test que fija el formato para que no
vuelva. **Les agradecemos el aviso**: lo habríamos encontrado tarde o nunca, porque el log parecía
correcto.

Y un aviso operativo por nuestra parte, para que conste: al intentar el arreglo por
`GUNICORN_CMD_ARGS=--access-log-format=…` el servicio quedó caído unos minutos —gunicorn hace
`shlex.split()` del valor, así que los espacios del formato rompen el arranque, y ese flag no es
opción de CLI en gunicorn 21—. Revertido en el momento y resuelto por el fichero de configuración.
Lo decimos porque **nos lo habéis visto hacer**: si en vuestro lado veis el proxy caído y no
respondemos, mirad el log de accesos; ahora ya no puede ser por esto.

## 2. §2 — Nos parece bien que el proxy sea el camino, y nos obliga

Que el 100 % del tráfico pase por aquí convierte `tiles-stats` en el instrumento, no en el
diagnóstico: lo miraremos con vosotros cuando despleguéis v0.9.18, y si el ratio de aciertos se
hunde lo diremos **nosotros** antes de que toquéis nada, como acordamos.

Sobre el tercer escalón (CARTO directo **sin** clave → marca de agua): nos parece la decisión
correcta y nos deja un dato útil, porque el contador `placeholder` de nuestro lado y el vuestro de
teselas con marca de agua miden lo mismo desde los dos extremos. Si los dos números se mueven
juntos, el problema está aguas arriba (nuestra clave o la cuota); si sólo se mueve el vuestro, el
que no cumple es el proxy. Es una comprobación cruzada gratis.

## 3. Los otros escalones, confirmados desde fuera

Vuestra tabla de §1 coincide con la nuestra (`?key=` 200, `origin_domain` 200, sin clave 401, clave
en otra ruta 401, estilo inventado 400). Añadimos una cosa que ya fijamos: `?key=` **y**
`&origin_domain=` juntos era lo que queríamos como forma preferente, y confirmáis que mantenéis la
comprobación de dominio. Perfecto.
