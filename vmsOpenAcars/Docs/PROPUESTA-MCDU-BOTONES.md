# Propuesta — botones retroiluminados estilo MCDU A320 (cuerpo negro, luz del propio color)

> **Estado: aprobada e implementada en v0.9.45** — variante **A**, y el barrido alcanzó **los 34 botones de
> la solución** (no solo la barra principal). Lo que sigue es el documento de la propuesta tal como se
> presentó; las diferencias entre lo propuesto y lo entregado están en **§9 (cierre)**, al final.

La idea, en una frase: **la barra de botones deja de ser rectángulos de color con letra blanca y pasa a
ser teclas de MCDU —cuerpo negro— retroiluminadas desde atrás con el color de cada botón**: la leyenda
se enciende en su color y esa luz se ve salir por el plástico. No se toca ni la geometría, ni los
rótulos, ni los manejadores.

| Lámina (en `Tools/McduPreview/out/`) | Qué enseña |
|---|---|
| `01-antes-y-despues.png` | La barra de hoy y las dos variantes de paleta, a 2:1 |
| `02-estados.png` | Normal · puntero encima · pulsada · deshabilitada, y los botones que cambian de color en vuelo |
| `03-tamano-real.png` | Las tres barras a 1:1, que es donde se juzga si se lee |
| `04-en-contexto.png` | La barra dentro de la ventana, con el final del log encima y la tira de estado debajo |
| `05-otros-formularios.png` | Los botones de los demás formularios, con sus emoji y sus aspas de cerrar |

> Los PNG son artefactos generados: no se commitean (regla de `Docs/README.md`). Se regeneran con el
> comando de §7.

---

## 1. Cómo se consigue que parezca retroiluminado

No es una letra de color sobre negro. Una tecla retroiluminada tiene **cuatro** señales, y las cuatro
están en `McduKeyRenderer`:

1. **La leyenda** en el color del botón, con su brillo real. El cuerpo no lleva color: el capuchón es
   negro en los cuatro estados.
2. **El lavado interior** (*wash*): la luz que atraviesa el plástico desde detrás. Es un relleno del
   propio color dentro del capuchón, **más denso en el centro** —que es donde está la leyenda— y
   desvanecido hacia el borde, hecho con un `PathGradientBrush` sobre la misma forma del capuchón
   (`SetSigmaBellShape` para que la caída sea suave). Por eso se ve *de dónde viene* la luz y no un
   tinte plano.
3. **El halo** de la leyenda: la luz que sangra por la serigrafía. Es la propia leyenda **desenfocada**
   —se dibuja en una capa aparte, se reduce a un cuarto y se vuelve a ampliar con bicúbica— y se echa
   debajo con la opacidad del estado (80 % normal, 95 % con el puntero encima, 125 % pulsada, 22 %
   deshabilitada). Al ser un desenfoque del rótulo real, el halo **pega con la forma de cada palabra**
   sin dibujar ninguna aureola a mano.
4. **La fuga por la junta**: un filo del propio color en el borde inferior del capuchón. Es el detalle
   que cierra el efecto: en una tecla retroiluminada la luz escapa por la junta del plástico.

Lo que se copia del MCDU real (la foto adjunta, A320-200 / LEAP-1A26) es lo demás: **capuchón negro**
con degradado suave, **esquinas redondeadas** (4 px), **bisel de dos tonos** (filo iluminado arriba y
sombra pegada al borde inferior, que es lo que da volumen de tecla y no de rectángulo) y **leyenda
espaciada** (0,7 px de *tracking*, que con Consolas se consigue sin cambiar de tipografía).

Estados:

| Estado | Qué hace la tecla |
|---|---|
| **Normal** | Cuerpo negro, lavado y halo al 100 % de lo previsto, filo iluminado arriba |
| **Puntero encima** | El capuchón y su borde suben de brillo; el halo también (95 %) |
| **Pulsada** | El cuerpo se apaga del todo, el bisel se invierte (sombra interior arriba, sin filo), la leyenda sube de brillo (×1,18) y el halo sube al 125 %: **la tecla se enciende al ceder**, que es lo que el ojo lee como respuesta |
| **Deshabilitada** | Cuerpo aplanado, leyenda **en su color pero hundida al 45 %** y halo casi apagado (22 %). Un `OFP` apagado sigue siendo azul y se distingue de un `MAPA` apagado; no se convierte en gris |

Lo que **no** se toca: ancho 90, alto 34, hueco 4, margen 6, `Consolas 10 Bold`, el orden de los ocho
botones, sus claves de idioma y sus `case` en `MainForm`. **La barra sigue pidiendo 754 px**, así que
`MainButtonBarTests` (que la mide contra el `MinimumSize` de 800) no se entera del cambio.

---

## 2. El color cambia de sitio, y la luz se come margen

Un color que funciona como **fondo** con letra blanca encima no funciona como **letra** sobre negro. Los
ocho colores de `MainButtonBar` suspenden el mínimo de contraste de la WCAG (4,5:1 para texto normal)
cuando se usan como leyenda:

| Botón | Hoy (fondo) | El color de hoy **usado como leyenda** |
|---|---|---|
| LOGIN | `rgb(0,120,200)` | 3,62:1 |
| OFP | `rgb(0,100,200)` | 2,93:1 |
| MAPA | `rgb(60,70,80)` | **1,75:1** |
| METEO | `rgb(60,70,80)` | **1,75:1** |
| DISPATCH | `rgb(0,150,0)` | 4,29:1 |
| BITÁCORA | `rgb(60,90,110)` | 2,30:1 |
| INICIAR | `rgb(200,100,0)` | 4,22:1 |
| CANCELAR | `rgb(150,0,0)` | **1,84:1** |

Y hay un segundo efecto que solo aparece cuando se enciende la retroiluminación: **el lavado sube el
brillo del fondo justo detrás de la leyenda**, así que el contraste efectivo es menor que contra el
negro puro. Medido con `ColorContrast` contra `McduKeyRenderer.BacklitBody(color)` —el capuchón con su
lavado encima, que es el fondo real—:

- Los ocho colores de la variante A pasan de 5,82–8,70:1 (sobre negro) a **5,82–6,93:1 con la luz
  encendida**: se pierde en torno a un punto de margen.
- **El rojo puro de hoy (`Color.Red`) no llega**: se queda en **4,39:1** con la luz puesta. Por eso el
  rojo de la propuesta es `rgb(255,118,110)` y no `Color.Red`, y por eso el test que se propone en §4
  mide **contra `BacklitBody` y no contra el negro**: si alguien sube el lavado para que luzca más,
  la prueba se pone roja antes de que el piloto se encuentre un botón que no se lee.

Este es el acoplamiento que hay que dejar por escrito: **`WashAlpha` no es solo estética**. Es una
constante del renderizador y tiene un tope medido.

---

## 3. Las dos variantes de paleta

### Variante A — «los mismos tonos, aclarados» (recomendada)

Cada botón conserva su familia de color —azul, pizarra, verde, ámbar, rojo— subida hasta que se lee
**con la luz encendida**. Es la que menos obliga a reaprender: el que hoy sabe que el azul es «sesión» y
el ámbar es «arrancar» sigue sabiéndolo.

| Botón | Hoy (fondo) | A (leyenda) | Sobre negro | **Con la luz puesta** |
|---|---|---|---|---|
| LOGIN | `rgb(0,120,200)` | `rgb(74,178,255)` | 7,28:1 | **5,82:1** |
| OFP | `rgb(0,100,200)` | `rgb(74,178,255)` | 7,28:1 | **5,82:1** |
| MAPA | `rgb(60,70,80)` | `rgb(166,180,196)` | 7,96:1 | **6,24:1** |
| METEO | `rgb(60,70,80)` | `rgb(166,180,196)` | 7,96:1 | **6,24:1** |
| DISPATCH | `rgb(0,150,0)` | `rgb(0,214,110)` | 8,68:1 | **6,93:1** |
| BITÁCORA | `rgb(60,90,110)` | `rgb(138,186,226)` | 8,16:1 | **6,36:1** |
| INICIAR | `rgb(200,100,0)` | `rgb(255,168,32)` | 8,70:1 | **6,80:1** |
| CANCELAR | `rgb(150,0,0)` | `rgb(255,118,110)` | 6,46:1 | **5,25:1** |
| *ABORTAR* (en vuelo) | `Color.Red` | `rgb(255,118,110)` | 6,46:1 | 5,25:1 |
| *ENVIAR* (PIREP listo) | `Color.Green` | `rgb(0,214,110)` | 8,68:1 | 6,93:1 |

Los tres estados en vuelo del botón grande —`START` ámbar, `ABORT` rojo, `SEND PIREP` verde— **viajan
igual que hoy**: `MainViewModel` y `AcarsReporter` siguen mandando su `Color` por
`OnButtonStateChanged` y lo único que cambia es que ese color acaba encendiendo la tecla en lugar de
rellenarla. Cero cambios en el ViewModel.

### Variante B — «paleta MCDU pura»

Solo los cinco colores del MCDU: blanco para utilidades, cian para consulta, verde para lo que el piloto
introduce, ámbar para la acción armada y rojo para cancelar. Contraste con la luz puesta: 6,70–9,15:1.
Es la más parecida a la foto, pero **pierde la identidad de cada botón** (MAPA, METEO y BITÁCORA pasan a
ser tres botones blancos que solo se distinguen por el texto).

Se enseña en las láminas para poder elegir, pero la recomendación es la **A**.

---

## 4. Cómo queda el código: el diff exacto

### Archivos nuevos

| Archivo | Qué es | Tamaño |
|---|---|---|
| `Controls/McduKeyRenderer.cs` | El dibujo de la tecla: capuchón, bisel, lavado, halo y fuga. `internal static`, recibe `Graphics` + rectángulo + color + estado: **no depende de ninguna ventana**, así que se previsualiza y se prueba sin abrir un formulario (mismo patrón que `GeoMath` o `TouchdownZonePolicy`) | 343 líneas |
| `Controls/McduKeyButton.cs` | `Button` con `ControlStyles.UserPaint`: `Text`, `Enabled`, `Click` y el foco siguen funcionando igual; añade `LegendColor` y traduce ratón/teclado a los cuatro estados | 149 líneas |
| `UI/ColorContrast.cs` | Luminancia relativa y ratio de la WCAG 2.1. Helper puro, con test | 48 líneas |

### Archivos que se tocan (y cuánto)

**`Helpers/MainButtonBar.cs`** — el nombre del campo, para que no mienta:

```csharp
// antes
new MainButtonSpec("LOGIN", "BtnLogin", Color.FromArgb(  0, 120, 200)),
// después — el mismo convenio, ahora encendiendo la tecla
new MainButtonSpec("LOGIN", "BtnLogin", Color.FromArgb( 74, 178, 255)),
```

y `MainButtonSpec.BackColor` → `MainButtonSpec.LegendColor` (el campo, el constructor y su comentario).
Los ocho valores son los de la tabla de la variante A.

**`UI/Forms/MainForm.cs`** — cuatro líneas:

```csharp
// antes
Button btn = new Button { …, BackColor = spec.BackColor, ForeColor = Color.White, … };
// después
McduKeyButton btn = new McduKeyButton { …, LegendColor = spec.LegendColor, … };

// antes (UpdateButtonState)
btnStartStop.BackColor = backColor;
// después
btnStartStop.LegendColor = backColor;
```

El `BackColor` del botón **no se fija**: al quedar sin asignar hereda el del panel (`pnlButtons`,
`rgb(30,40,50)`), que es lo que tiene que asomar por las esquinas redondeadas. Si alguien le pone
`BackColor`, lo que cambia es el fondo de la tecla, no su significado — y además taparía la luz.

**`vmsOpenACars.csproj`** — tres `<Compile Include>` (el proyecto es de lista explícita: un archivo
nuevo no se compila solo).

**`vmsOpenACars.Tests/MainButtonBarTests.cs`** — un test nuevo, que es la red que evita que esto se
degrade:

```csharp
[TestMethod]
public void CadaLeyendaSeLeeConLaRetroiluminacionEncendida()
{
    foreach (var spec in MainButtonBar.Specs)
    {
        // Contra el fondo REAL: el capuchón con su lavado de color, no contra el negro puro.
        double ratio = ColorContrast.Ratio(spec.LegendColor, McduKeyRenderer.BacklitBody(spec.LegendColor));
        Assert.IsTrue(ratio >= ColorContrast.MinimumForText,
            $"«{spec.Name}» se quedaría sin leerse con la luz encendida: {ratio:F2}:1");
    }
}
```

Es el mismo tipo de prueba que ya protege el ancho de la barra: fija por escrito lo que el ojo no ve en
un monitor bueno y lo que el compilador no puede saber.

### Lo que NO se toca

`MainViewModel`, `AcarsReporter`, `AcarsReporterCallbacks`, `Theme.cs`, las claves de idioma, el
`MinimumSize` de la ventana, la geometría de la barra y los 804 tests existentes. Esto es **pintura**:
no roza telemetría, PIREP ni scoring.

---

## 5. Fases

**F1 — la barra principal** (lo de esta propuesta). Ocho botones, `McduKeyButton`, las cuatro líneas de
`MainForm`, el test de contraste. Es donde está el 90 % del valor visual y donde el diff es mínimo.

**F2 — los demás formularios**, con el mismo control y sin tocar su lógica: `EcamDialog` (OK / Cancelar /
Sí / No), `SettingsForm` (Guardar, Cancelar, TEST, REFRESH), `TaxiRouteForm` (Recalcular, Iniciar, Más
tarde), `FlightSelectorForm`, `FlightHistoryForm`, `FlightPlannerForm`, `LandingAnalysisForm`,
`FlareAnalysisForm`, `MapForm` y el `SidebarController`. Aquí hay una decisión de diseño real: el OK
verde y el Cancelar rojo de `EcamDialog` hoy son **relleno**, y pasarán a ser luz. En una ventana modal
sin barra de color el verde/rojo pierde peso, así que F2 se puede hacer formulario a formulario;
ninguno depende de los otros.

**F3 — opcional, y ya es otro encargo**: llevar el lenguaje MCDU más allá de los botones (columna de
teclas laterales tipo *line select* junto a la caja de estado, pantalla con texto verde/cian/ámbar para
el log ACARS). No es necesario para lo que se pide y cambiaría la pantalla, no solo los botones.

---

## 6. Riesgos y decisiones abiertas

1. **A o B.** Es la única decisión de diseño. La recomendación es A por identidad de botón (§3).
2. **La luz y el contraste están acoplados.** `WashAlpha` (hoy 26) y el brillo del halo se pueden subir
   para que la tecla luzca más, pero cada punto de lavado baja el contraste de la leyenda. El test de
   §4 es el tope: con el rojo en `rgb(255,118,110)` el margen es de 0,75 puntos sobre el umbral, el más
   estrecho de la barra.
3. **El halo cuesta un mapa de bits por repintado** (una capa de 90×34 y otra de 23×9, más dos
   `DrawImage`). Es despreciable para ocho botones que se repintan al pasar el ratón, pero si algún día
   esto se aplica a una rejilla de 60 teclas (el keypad del MCDU), hay que cachear el halo por
   rótulo+color en vez de rehacerlo.
4. **El rojo y el verde de los formularios (F2)** dejan de ser relleno: hay que mirar `EcamDialog` antes
   de tocarlo.
5. **Tema de Windows.** El control pinta a mano e ignora `SystemColors`: en modo de alto contraste o con
   un tema claro las teclas siguen siendo negras. El ACARS ya es un tema propio cerrado
   (`Theme.CockpitBackground`), así que es coherente, pero conviene saberlo.
6. **El foco del teclado.** Al pintar a mano se pierde el rectángulo de foco; el control lo vuelve a
   dibujar (`ControlPaint.DrawFocusRectangle`) cuando Windows pide enseñarlo. Sin eso, `Tab` no se ve.
7. **`DrawToBitmap`.** El fondo se pinta dentro de `OnPaint` y no en `OnPaintBackground`: si no, las
   esquinas redondeadas salen negras en cualquier captura (`WM_PRINTCLIENT` no pasa por el segundo).

---

## 7. Cómo se ve, y cómo se regenera

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:exe /out:"Tools\McduPreview\McduPreview.exe" /r:System.dll /r:System.Drawing.dll `
       /r:System.Windows.Forms.dll Controls\McduKeyRenderer.cs Controls\McduKeyButton.cs `
       UI\ColorContrast.cs UI\McduLegend.cs Tools\McduPreview\PreviewMain.cs
& "Tools\McduPreview\McduPreview.exe"   # tabla de contraste + los cinco PNG en Tools\McduPreview\out
```

Las barras de las láminas **no son un dibujo aparte**: se pintan con controles de verdad (`Button` para
el «hoy», `McduKeyButton` para la propuesta) en un formulario fuera de pantalla y se capturan con
`DrawToBitmap`, así que lo que se ve es lo que pintaría la aplicación. Los estados que un control vivo
no deja forzar (puntero encima, pulsada) van contra `McduKeyRenderer.Paint`, que es exactamente lo que
llama el control.

Los diales del efecto, todos al principio de `McduKeyRenderer`: `CornerRadius` (4), `Tracking` (0,7),
`WashAlpha` (26 — **con tope de contraste**), la opacidad del halo por estado (0,8 / 0,95 / 1,25 / 0,22)
y la fuga de la junta (55).

`Tools/McduPreview/` es **material de la propuesta**: si se aprueba, el renderizador y el control se
mueven a `Controls/` (ya están escritos en el *namespace* `vmsOpenAcars.UI.Controls`) y lo que sobre
—el arnés y los PNG— se puede borrar.

---

## 8. Lo que no está verificado

- **No se ha compilado dentro del proyecto ni se ha visto la ventana real.** El arnés usa los mismos
  controles y la misma geometría, pero no es la aplicación: al aprobarlo hay que compilar en el IDE
  (Debug **y** Release) y mirar la barra con los dos botones que arrancan deshabilitados (`OFP` y
  `MAPA`), que es el caso que peor se lee.
- **No se ha medido el contraste en pantalla**, solo calculado sobre el valor nominal del color. Un
  monitor con gamma rara puede pedir subir un paso el gris de `MAPA`/`BITÁCORA`.
- **No se ha medido el coste del halo en la ventana real** (repintados por segundo con el ratón encima
  de la barra). La estimación es de milisegundos, pero es una estimación.
- **El aspecto no se ha comparado lado a lado con el MCDU real** más allá de la foto adjunta
  (413×638 px): el MCDU real **no está retroiluminado** en el sentido de esta propuesta —sus teclas son
  serigrafía blanca mate y la luz está en la pantalla—, así que esto es una lectura del lenguaje visual
  del MCDU, no una copia literal. Si se quiere acercar más al aparato, el dial es el lavado y el halo
  hacia abajo, no hacia arriba.

---

## 9. Cierre — lo que cambió al implementarlo (v0.9.45)

Se aprobó la **variante A** y el mantenedor pidió **el barrido completo**: «haz lo mismo con todos los
botones de la solución». Cuatro cosas se apartaron de lo escrito arriba, y todas por un motivo medido:

1. **El espacio de nombres.** El renderizador y el control viven en `vmsOpenAcars.Controls` (la carpeta
   `Controls/`, como `GaugeControl` y `LinearGauge`), **no** en `vmsOpenAcars.UI.Controls` como decía §4:
   ese nombre **tapaba** a `vmsOpenACars.Controls` y rompía `MainForm` —`Controls.EngineMonitorPanel`
   dejaba de resolverse—. Lo cazó el compilador, no el ojo.
2. **La leyenda se pinta con GDI**, no con GDI+, y **por elementos de texto** (`StringInfo`), no por
   `char`. GDI+ no hace enlace de fuentes: el engranaje de la cabecera (`⚙️`), el disquete de `💾 PNG`,
   la papelera de `🗑 DELETE BID` y las flechas de `🔄 FETCH OFP` salían como **cuadros vacíos**; y al
   recorrer el rótulo carácter a carácter, los pares surogados (`📋` = U+1F4CB) y el selector de variación
   (`U+FE0F`) se partían en **dos cuadros** cada uno. Lo destapó la **sonda de glifos**
   (`Tools/McduPreview/probe/glifos-gdi+vs-gdi.png`) y la **lámina 5**, no el compilador.
3. **F2 está hecho** en la misma versión: los 34 botones de la aplicación —barra, cabecera, `EcamDialog`,
   `SettingsForm`, `MapForm`, `FlightPlannerForm`, `FlightSelectorForm`, `FlightHistoryForm`,
   `LandingAnalysisForm`, `FlareAnalysisForm`, `TaxiRouteForm` y el `SidebarController`— pasan por el
   mismo control. El rojo y el verde de los popups **dejaron de ser relleno** y son leyenda, como avisaba
   §6: se leen bien, pero pierden peso en una ventana modal.
4. **La paleta es un módulo compartido** (`UI/McduLegend.cs`, seis colores) y no una lista dentro de
   `MainButtonBar`: el mismo verde significa «confirmar» en la barra y en un popup. `MainButtonBarTests`
   comprueba los ocho botones **y** los seis colores del convenio.

Lo demás se cumplió: geometría intacta (la barra sigue pidiendo 754 px), **cero cambios en los
ViewModels**, el rojo aclarado a `rgb(255,118,110)` —5,25:1 con la luz encendida, el más justo— y el
barrido de `MainForm.UpdateCancelButton` (que aclaraba el fondo de CANCELAR según el estado) retirado
porque un rojo apagado sobre negro deja de leerse: la diferencia la dice el rótulo.

Verificado con **build Debug y Release** y la suite completa (**807 tests, 0 fallos**, con los dos
nuevos). Sigue **sin verificar la ventana real** —eso es de §8— y queda pendiente el paso de
publicación (notas al piloto, zip sin el `.config`, etiqueta), que es la receta `PUBLICA-RELEASE.md`.
