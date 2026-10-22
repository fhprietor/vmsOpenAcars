# Novedades de vmsOpenACARS — versión 0.9.52

**En una frase:** en los aviones de **PMDG** el piloto automático, el autobrake y los carteles de
cabina **no están** en los offsets estándar de MSFS, y ahora se leen de **su propio bloque SDK**. Para
eso hace falta **un interruptor que se enciende una vez**, en el avión, no en el cliente.

---

## Lo que hay que hacer UNA vez (2 minutos)

1. Cierra MSFS.
2. Abre con el Bloc de notas el fichero de opciones del 737 (o del 777):

   - **737** — `737_Options.ini`
     - Steam: `%APPDATA%\Microsoft Flight Simulator\Packages\pmdg-aircraft-737\work\`
     - MS Store: `%LOCALAPPDATA%\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalState\packages\pmdg-aircraft-737\work\`
   - **777** — `777_Options.ini`, en la misma carpeta con `pmdg-aircraft-777`.

3. Añade **al final** estas dos líneas:

   ```ini
   [SDK]
   EnableDataBroadcast=1
   ```

4. Guarda y arranca MSFS.

> Es lo que pide la propia documentación de PMDG para que sus datos salgan por FSUIPC. **No lo puede
> hacer el ACARS**: es configuración del avión, en tu disco.

---

## Qué cambia

- **Piloto automático**: se lee de los **anunciadores del MCP** de PMDG —`CMD A/B`, `CWS`, y **un
  offset por modo**: `LNAV`, `VNAV`, `LVL CHG`, `HDG SEL`, `ALT HOLD`, `VS`, `VOR LOC`, `APP`—. Antes,
  en un PMDG, el cliente no veía nada: las doce banderas estándar están muertas porque PMDG vuela con
  su propio sistema, no con el del simulador. Por eso en el vuelo de prueba salieron **todas a 0** con
  el avión en crucero.
- **Autobrake**: del selector real (`MAIN_AutobrakeSelector` en el 737, `BRAKES_AutobrakeSelector` en el
  777), con la misma tabla de siempre: `RTO`, `OFF`, `1`, `2`, `3`, `MAX`.
- **Carteles de cabina**: del selector (`OFF` / `AUTO` / `ON`). `AUTO` **no** se cuenta como encendido:
  lo decide el avión según la fase.
- **Si el bloque no está encendido**, el cliente **lo dice una vez** en el registro, con la línea exacta
  que falta, y sigue con los offsets estándar. No inventa: donde no hay dato pone `---`.

## Qué vas a ver en el registro

La línea `🛠` del vuelo anterior cambia de forma:

```
🛠 Enroute: PMDG: powered=1 cmd=10 cws=00 ap=00 fd=11 lnav=1 vnav=1 flch=0 hdg=0 alt=0 vs=0 loc=0 app=1 at=1 -> LNAV+VNAV · CAB cint=ON nofum=ON publicado=si (SDK) · ABRK 2 act=0 publicado=si
```

Si sale `⚠️ B737: bloque SDK apagado — añade «[SDK] EnableDataBroadcast=1» …`, es que el paso de
arriba no está hecho (o MSFS no se reinició).

## Lo que no cambia

Los arreglos de la 0.9.51 que ya viste funcionando siguen igual: **spoilers** por posición (solo
despliegues reales; en tu vuelo, 6 eventos y `3 %` en el toque), **reversas** al desplegarse (`5 % / 11 %`
a los 6 s) y el autobrake que ya no dice «RTO» cuando no lo sabe.
