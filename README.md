# VentaFácil

Punto de venta sencillo para Windows 7 y Windows 10/11, inspirado en **Zafirah Retail System 2008**: las mismas teclas de función, pantallas simples, casi sin configuración, y cortes de caja claros para el administrador.

- **Ligero**: un `.exe` de menos de 1 MB más SQLite (unos 2 MB en total). Sin servidor de base de datos (Zafirah necesitaba SQL Server).
- **Compatible**: .NET Framework 4.0, que corre en Windows 7, 8, 10 y 11, en 32 y 64 bits.
- **Impresoras**: cualquier impresora instalada en Windows (por controlador) o térmicas de tickets en modo directo **ESC/POS** (Epson, 3nStar, EC Line, Bixolon, genéricas de 58 y 80 mm).
- **Cajón de dinero**: se abre con el pulso de la impresora (RJ11) o por puerto serial.
- **Lector de código de barras**: cualquiera que funcione "como teclado" (USB HID), que son casi todos. No necesita configuración.
- **Báscula**: captura del peso a mano, lectura por puerto serial (Torrey, Rhino y similares) o **etiquetas de báscula** (EAN-13 que empiezan con 2).

## Descarga

El programa listo para Windows está en el [último release](https://github.com/Alan20111/ventafacil/releases/latest):

- **VentaFacil.exe**: un solo archivo. Ábralo y se instala solo en `C:\VentaFacil\`, con acceso en el escritorio. No necesita nada más, salvo .NET Framework 4.8 en Windows 7.
- **VentaFacil-vX.Y-win.zip**: lo mismo en carpeta, por si prefiere copiarla a mano.

Cada cambio al proyecto publica un release nuevo.

## Instalación en la tienda

1. **Requisito, solo en Windows 7**: .NET Framework 4.0 o superior (se recomienda el 4.8). Windows 8, 10 y 11 ya lo traen.
   Para saber si ya está instalado: *Panel de control → Programas y características*, busque "Microsoft .NET Framework 4".
   Si no aparece, instale el instalador sin conexión de .NET Framework 4.8 (`ndp48-x86-x64-allos-enu.exe`, de microsoft.com). En Windows 7 necesita el Service Pack 1.
2. Copie la carpeta `VentaFacil` a `C:\VentaFacil` (no a *Archivos de programa*, para que pueda guardar los datos ahí mismo).
3. Abra `VentaFacil.exe`: se instala solo en `C:\VentaFacil` y crea el acceso en el escritorio.
4. La primera vez se abre el **asistente**: si es caja u oficina, la cuenta del administrador (sustituye a admin / admin), el negocio, los usuarios, los respaldos, la impresora, los productos y la licencia. Cada paso se guarda; se puede cerrar y seguir después en *F6 Ajustes → Negocio → Abrir asistente*. Después de **cada actualización**, al entrar un administrador pregunta **Configurar** o **No configurar**; los datos se conservan siempre.
5. La ventana **Impresora de tickets** detecta las impresoras instaladas en Windows, marca las de tickets y recomienda una. Pulse **F3 Imprimir prueba** y **Guardar**. Se puede volver a abrir en cualquier momento con **Ctrl+I** o en *F6 Ajustes → Equipos*.

Los datos quedan en `C:\VentaFacil\datos\ventafacil.db`. Cada día, y después de cada corte Z, se hace un respaldo automático en `datos\respaldos\` (se guardan los últimos 30).

### Impresora de tickets

VentaFácil **no usa puertos**: imprime por la cola de Windows. Si Windows imprime su página de prueba, VentaFácil también.

- **Detectar automáticamente**: busca una térmica de tickets por su nombre, su controlador y sus tamaños de papel (58/80 mm). Si no hay, usa la predeterminada. Ajusta solo el ancho y la forma de imprimir.
- **Automático**: usa siempre la impresora **predeterminada de Windows**.
- **Manual**: se elige de la lista, que muestra el tipo (tickets, normal o virtual), la conexión (USB, red, serial) y si está desconectada.
- Si la impresora elegida desaparece (por ejemplo, Windows la reinstala como "… (Copia 1)" al cambiar de puerto USB), VentaFácil busca la parecida o usa la predeterminada, en lugar de no imprimir.
- Si falla la impresión al cobrar, ofrece abrir la ventana de impresora en ese momento.

#### ¿Qué forma de imprimir elegir?

| Modo | Cuándo usarlo |
|---|---|
| **Controlador de Windows** (predeterminado) | Funciona con cualquier impresora que tenga su controlador instalado: térmica, de inyección o láser. Ajuste el ancho (58 u 80 mm) y el tamaño de letra. |
| **Directo ESC/POS** | Para impresoras térmicas de tickets. Es más rápido, corta el papel y abre el cajón en el mismo envío. Funciona aunque la impresora esté instalada con el controlador **"Generic / Text Only"**. |

- Papel de 80 mm: 42 columnas (48 en algunas impresoras). Papel de 58 mm: 32 columnas.
- Si el cajón no abre, cambie el *pin del cajón* (pin 2 o pin 5) en *F6 Ajustes → Equipos*, pestaña *Cajón de dinero*.

## Uso diario: teclas del punto de venta

| Tecla | Función |
|---|---|
| Escanear o escribir el código + **Enter** | Agrega el artículo |
| `3*` y luego escanear | Vende 3 piezas del artículo (multiplicar) |
| Escribir parte del nombre + **Enter** | Busca el artículo por nombre |
| **+** o **Ctrl+F1** | **Cobrar** (en la ventana de cobro: F2 efectivo, F3 tarjeta, F4 transferencia, F5 vales) |
| **Ctrl+F2** | Buscar artículo |
| **Ctrl+F3** o **Supr** | Quitar el artículo seleccionado (↑↓ para elegir) |
| **Ctrl+F4** | Consultar precio (Enter otra vez para venderlo) |
| **Ctrl+F5** | Venta en espera: guardar la venta actual o recuperar una |
| **Ctrl+F6** | Descuento por artículo (% o $), cambio de precio, o % a toda la venta |
| **Ctrl+F7** | Artículo varios: venta sin código, con precio libre |
| **Ctrl+F8** | Cancelar un ticket ya cobrado (regresa la mercancía al inventario) |
| **F1 … F6** | **Cambiar de módulo** (ver abajo); **Esc** regresa a Caja; **F10** cambia de usuario |
| **Ctrl+P** | Reimprimir el último ticket |
| **Ctrl+D** o **Shift+−** | Abrir el cajón |
| **Ctrl+I** | Elegir o detectar la impresora |
| **Ctrl+Supr** | Cancelar toda la venta en curso |
| **Esc** | Limpiar la captura o cerrar una ventana |

**Artículos a granel** (semillas, botanas, dulces): en el producto marque *se vende por kilo*. Al escanearlo, el programa pide el peso: puede escribirlo en kilos (`0.750`) o en gramos (`750`). Con F2 se cambia a importe en pesos ("deme $50 de cacahuate") y con F3 se lee la báscula.

### Lista de costos estilo semillera (Excel)

En *F3 Catálogo*:

- **Importar → Lista de costos de la semillera**: lee el archivo de costos tal cual se usa hoy (hojas con columnas NO, STOCK, PRODUCTO, BTO, CANT, COSTO, MAYOREO, CANT, MED. MAY., CANT, MENUDEO). Antes de importar muestra una vista previa:
  - Cada renglón de proveedor o grupo (JUANJO, SAMY, DURAN…) se vuelve el proveedor y grupo de los productos que le siguen.
  - El costo por kg es el precio del bulto ÷ lo que trae.
  - Los tres precios se aplican **solos en caja** según la cantidad: menudeo, medio mayoreo (desde 1 kg, 6 kg…) y mayoreo (desde 3 kg, 25 kg…).
  - Detecta qué se vende por kilo y qué por pieza (empacados, desechables, sobres…). Se puede corregir en cada producto.
  - Usted decide si la columna STOCK es el **stock mínimo** o la **existencia actual**.
  - Los productos nuevos reciben un código corto (1, 2, 3…) para teclearlo en caja. Si se vuelve a importar, actualiza costos y precios.
- **Exportar → Lista de costos**: genera un `.xlsx` con el mismo formato (agrupado por proveedor, con la fórmula de costo = BTO/CANT y las columnas L M M J V S D para las marcas de la semana), más una hoja de **precios para mostrador** sin costos.
- El formulario de producto calcula igual que la hoja: costo del bulto ÷ lo que trae = costo; a cada precio se le ve su % de ganancia. Con *mantener el %*, al cambiar el costo del bulto se actualizan solos los tres precios.
- Todos los reportes (ventas, cortes, inventario, kardex…) se exportan a Excel con el mismo estilo, con totales.

### Turnos y cortes

- Al abrir la caja se captura el **fondo** inicial.
- **Corte X**: solo informa. Se puede sacar las veces que sea, por cajero o de toda la caja.
- **Corte Z**: cierra el turno. Pide contar el efectivo y muestra si hay **sobrante o faltante** (fondo + ventas en efectivo + entradas − salidas − devoluciones). Imprime el ticket del corte, hace un respaldo y abre el siguiente turno.
- El corte muestra las ventas brutas, las cancelaciones, las ventas netas, el desglose por forma de pago y por departamento, y la ganancia estimada (venta − costo).

### Si se va la luz o algo se daña
- La base usa escritura completa: una venta ya cobrada no se pierde por un corte de energía.
- Después de cada venta o cambio se guarda una copia en `respaldos\ultimo.db` y otra en la carpeta que se eligió en el primer arranque (por defecto el escritorio). Además hay un respaldo diario (los últimos 30).
- Si la base se daña, el programa lo detecta al abrir y ofrece restaurar el último respaldo bueno. La base dañada se guarda aparte, no se borra.
- El acceso **VentaFácil - Reparar** del escritorio revisa el programa, la base, los respaldos y los accesos sin borrar datos.

### Importar los productos de Zafirah
*F3 Catálogo* → *Importar* → *Zafirah*. Origen: **la base de Zafirah** (SQL Server, **solo lectura**: únicamente `SELECT`, nunca se escribe en Zafirah; probable servidor `(local)` y base `smartdatabase`, tabla `ZFRADMARTICULOS`) o **un archivo Excel/CSV** (por ejemplo un reporte exportado). Los nombres de las columnas de Zafirah se conocen por sus reportes pero **no se habían visto en una base real**, así que el asistente **no adivina en silencio**: propone qué columna es cada dato (si hay dos candidatas de costo o precio, elige la que trae más valores), muestra un **ejemplo** de cada una, permite cambiarla y enseña una **vista previa con avisos** (sin precio, precio menor al costo, códigos repetidos, niveles de mayoreo sin cantidad mínima…). Las cantidades «a partir de» de medio mayoreo/mayoreo **no se proponen**: se eligen viendo los datos. Si la tabla trae **una fila por almacén**, suma las existencias o usa un almacén elegido.
Al importar: respaldo automático previo, **todo en una transacción**, y un resumen. Si un código ya es de **otro** producto (otro nombre) **no se pisa** (se lista; hay casilla para reemplazarlo); si el mismo **nombre** ya existe con otro código, se actualiza ese producto sin duplicarlo; un código de barras que ya es de otro producto no se duplica. Los productos conservan el código de Zafirah (el que se teclea en caja) y su código de barras. No se importan ventas, clientes ni contraseñas.

#### Respaldos `.BK` y perfiles de importación
- **Pestaña «Respaldo .BK»** (`.bk .bak .1bk .2bk .3bk`): un respaldo nativo de SQL Server no se puede leer sin SQL Server, así que se **restaura en una base temporal** (`vf_import_tmp_…`, en la carpeta de datos del servidor), se leen los productos con el mismo asistente y la temporal **se borra** al cerrar. La base real de Zafirah no se toca; el código solo borra bases con ese prefijo. Revisa antes con `RESTORE HEADERONLY/FILELISTONLY` (base, versión, tamaño, archivos), avisa de incompatibilidades de versión (un respaldo de SQL Server 2000 no entra en 2012 o más nuevo: usar 2005–2008 R2), muestra el avance y, si el servicio de SQL Server no puede leer la carpeta (error 3201), ofrece copiarlo a `C:\Users\Public`. El botón **Comandos…** muestra el T-SQL exacto para probarlo a mano.
- **Perfiles** (`<datos>\perfiles\*.json`): dicen en qué base/tabla y con qué nombres de columna buscar cada dato, y las reglas (sumar almacenes, unidades a granel). Hay uno incluido (Zafirah); se crean desde el asistente con **Guardar…** o a mano (el botón **Carpeta** deja un ejemplo). Aceptan comentarios `//` y comas finales; un perfil roto se explica (línea y columna) y se omite. Adaptar el programa a otro sistema = un archivo, sin recompilar.
- No probado contra un SQL Server real ni en Windows (solo la lógica pura y la compilación): ver pasos 45–48 de `docs/PRUEBAS_WINDOWS.md`.

### Módulos (barra de arriba)
La barra azul de arriba tiene los módulos, que se cambian con **F1–F6** o con el ratón; la línea blanca se desliza al elegido:

| Tecla | Módulo | Vistas y atajos (Ctrl+F1, Ctrl+F2…) |
|---|---|---|
| F1 | **Caja** | Cobrar · Buscar · Quitar · Precio · En espera · Descuento · Varios · Cancelar |
| F2 | **Turno** | Resumen · Entradas/salidas · Cortes X/Z · Abrir cajón · Reimprimir ticket · Recibir envío · Reporte a oficina (si está enlazada) · Cambiar usuario |
| F3 | **Catálogo** | Productos · Grupos · Códigos de barras |
| F4 | **Almacén** | Entrada de mercancía · Conteo con Excel · Ajustar un producto · Faltantes · Kardex · Valor · Envíos recibidos (si está enlazada). En la oficina: Llegó mercancía · Envíos |
| F5 | **Resultados** | Ventas por día · Tickets · Más vendidos · Por cajero · Forma de pago · Por grupo · Cortes de caja · Movimientos (se consultan solos al elegir fechas). En la oficina: ventas, existencias, cortes y diferencias de la tienda |
| F6 | **Ajustes** | Negocio · Equipos (impresora, cajón y báscula) · Usuarios · Respaldos · Envíos · Licencia · Teclas rápidas · Acerca de · Salir |

Cada módulo declara arriba sus botones con su atajo. Los módulos sin permiso piden la clave de alguien que sí lo tenga, y no se entra a F3–F6 con una venta a medias. Los botones son planos (sin borde): se distinguen por contraste, con el texto centrado.

### Usuarios y permisos
En *F6 Ajustes → Usuarios*. Cada quien tiene un **perfil** que marca sus casillas: **Cajero**, **Encargado** (todo menos Usuarios, Respaldos y el enlace de Envíos), **Administrador** (todo, siempre) o **Personalizado** (cuando se ajustan a mano). El **cajero**, de fábrica, solo cobra, ve *Turno*, registra entradas y salidas, abre el cajón y saca **su propio corte X**. Con las casillas *Puede usar* se le da o quita cada módulo (Turno, Catálogo, Almacén, Resultados, Ajustes) y cada acción (descuentos, cancelar, corte X de caja, corte Z…). Cobrar y el corte propio no se pueden quitar; *Usuarios* y *Respaldos* son solo del administrador. Los módulos sin permiso salen con candado y se abren únicamente con la clave de alguien que sí lo tenga; esa autorización se olvida al volver a Caja o al cambiar de usuario (F10), que siempre regresa a Caja. Al actualizar nadie pierde su cuenta: el corte X de caja, que antes veía cualquiera, ahora pide permiso.

### Envíos de la oficina a la tienda (USB)
Para que los cambios de la oficina no se capturen a mano en la caja. En *F6 Ajustes → Envíos* (solo administrador) la **caja** crea un *archivo de enlace* (protegido con contraseña) y la **computadora de la oficina** —instalación nueva, con su propia licencia— lo abre y queda como OFICINA: ahí no se cobra.

- **Oficina:** los cambios del Catálogo se anotan solos; la mercancía se captura en *F4 Almacén → Llegó mercancía* (o se carga un Excel con CÓDIGO y CANTIDAD) y en *Envíos* se presiona **Crear envío** (siempre pregunta la ruta).
- **Caja:** *F2 Turno → Recibir envío*: muestra qué cambia, avisa de precios raros y aplica. La mercancía se **suma** a lo que haya; los precios quedan como en la oficina; lo eliminado se desactiva. Deja un **acuse** en la USB. *F4 Almacén → Envíos recibidos* tiene el historial y **Deshacer el último** (no toca ventas).
- Al **conectar la USB** sale una franja amarilla (en la caja si trae un envío nuevo; en la oficina si trae un acuse) sin interrumpir la venta. También se puede abrir el archivo con **doble clic**.
- **Regreso a la oficina:** el acuse lleva además (cifrado) las existencias, las ventas por día y producto, los cortes y el catálogo de la tienda (3 meses). También se guarda cuando se quiera con *F2 Turno → Reporte a oficina*. La oficina lo ve en *F5 Resultados*: ventas, lo más vendido, existencias (y lo que va en camino), cortes y **Diferencias** de catálogo, donde se elige quedarse con lo de la tienda (incluye traer productos creados en la caja) o mandar lo de la oficina.
- De la oficina a la tienda no viajan ventas, cortes, usuarios ni conteos. Un envío repetido no hace nada; uno perdido lo recupera el siguiente; si falta uno, se niega. Todo se aplica o nada.
- El archivo (`.vfenvio`) va cifrado y **firmado por la oficina**: la llave que firma no sale de ahí. El primer envío lo confirma un administrador en la caja.
- Si la oficina cambia de computadora o restaura un respaldo viejo, hay que volver a enlazar. Una caja por tienda: con dos cajas (dos bases) las existencias no se comparten.

### Códigos de barras y etiquetas
La lista de la semillera no trae códigos de barras, así que lo importado se cobra con su **código corto** (1, 2, 3…). Para escanear: *F3 Catálogo* → **Códigos de barras**. Se toma el producto, se pasa el lector por su empaque y el código se guarda solo (en el *código alterno*; el código corto no se toca) y salta al siguiente. `Ctrl+S` saltar · `Ctrl+K` «sin código de barras» · `Ctrl+Q` quitar · `Ctrl+T` etiqueta. Es una vista del módulo, no una ventana aparte. Un código no puede ser de dos productos: si ya existe, avisa de quién es y ofrece pasarlo.
Lo que no trae código (granel, verdura): **Etiqueta** imprime una tira con nombre, precio y el código corto en Code 128 por la impresora de tickets (por controlador de Windows o como imagen en modo ESC/POS, sin depender del comando de barras de la impresora).

### Conteo de inventario con Excel
*F4 Almacén* → **Conteo con Excel**: se exporta la hoja (todo o por proveedor), se llena la columna CONTADO en el negocio y se importa; antes de aplicar se ven los faltantes y sobrantes con su valor.

### Licencia
30 días de prueba; después una clave por computadora (permanente). Se genera con `herramientas/licencias` (ver [docs/LICENCIAS.md](docs/LICENCIAS.md)).

### Pruebas
- [docs/PRUEBAS_WINDOWS.md](docs/PRUEBAS_WINDOWS.md): 81 pasos para la laptop.
- **Windows real (GitHub Actions, `.github/workflows/windows.yml`)**: corre las pruebas de datos con .NET Framework y `System.Data.SQLite` de verdad, abre **todas** las pantallas (`herramientas/verificar-ui`), revisa encimados, textos cortados, alineación y botones, guarda capturas y arranca el `.exe`. Se lanza a mano (*Actions → Verificar en Windows*) o al subir a una rama `verificar/…`.
- `dotnet run` en `herramientas/licencias -- --probar`, `herramientas/pruebas-dinero`, `herramientas/pruebas-conteo` y `herramientas/pruebas-barras` (código de barras contra una librería independiente), `herramientas/pruebas-zafirah` (importador) y `herramientas/pruebas-datos` (**la capa de datos real contra un SQLite de verdad**: esquema, migración, cobros, cortes, dinero exacto, conteo, respaldos, base dañada, restauración, importador y **envíos** repetidos, perdidos, alterados e interrumpidos, y el **regreso de ventas y existencias**; usa un adaptador mínimo porque `System.Data.SQLite` solo corre en Windows): pruebas automáticas (se corren en Mac o Windows).

## Desarrollo

Código en C# (WinForms, .NET Framework 4.0), en `src/VentaFacil`. Se compila desde Windows, macOS o Linux con el SDK de .NET:

```bash
cd src/VentaFacil && dotnet build -c Release
```

Para generar la carpeta lista para copiar a la tienda (`publish/VentaFacil` y `publish/VentaFacil.zip`):

```bash
./scripts/publicar.sh
```

Para publicar un release nuevo con el `.exe` (después de hacer commit y push):

```bash
scripts/publicar_release.sh "Qué cambió"
```

Sube la versión sola (v1.0 → v1.1 → …) y adjunta `VentaFacil.exe` y el zip.

Estructura:

- `Data/`: SQLite (esquema, modelos, consultas)
- `Core/`: configuración, sesión, formatos, importe con letra
- `Printing/`: tickets, ESC/POS directo, impresión por Windows, cajón, báscula
- `Forms/Pos/`: pantalla de venta y sus ventanas (cobro, búsqueda, cortes…)
- `Forms/Admin/`: administración
- `docs/ANALISIS_ZAFIRAH.md`: análisis del sistema original y qué se tomó de él
