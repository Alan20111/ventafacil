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
4. Entre con el usuario **admin** y la contraseña **admin**. **Cámbiela de inmediato** en *F11 Administración → Usuarios*.
5. La primera vez se abre sola la ventana **Impresora de tickets**: detecta las impresoras instaladas en Windows, marca las de tickets y recomienda una. Pulse **F3 Imprimir prueba** y **Guardar**. Se puede volver a abrir en cualquier momento con **Ctrl+I** o en *F11 → Configuración → Impresora*.
6. En *F11 → Configuración*, capture los datos del negocio.

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
- Si el cajón no abre, cambie en Configuración el *pin del cajón* (pin 2 o pin 5).

## Uso diario: teclas del punto de venta

| Tecla | Función |
|---|---|
| Escanear o escribir el código + **Enter** | Agrega el artículo |
| `3*` y luego escanear | Vende 3 piezas del artículo (multiplicar) |
| Escribir parte del nombre + **Enter** | Busca el artículo por nombre |
| **+** o **F1** | **Cobrar** (F2 efectivo, F3 tarjeta, F4 transferencia, F5 vales) |
| **F2** | Buscar artículo |
| **F3** o **Supr** | Quitar el artículo seleccionado (↑↓ para elegir) |
| **F4** | Consultar precio (Enter otra vez para venderlo) |
| **F5** | Venta en espera: guardar la venta actual o recuperar una |
| **F6** | Descuento por artículo (% o $), cambio de precio, o % a toda la venta |
| **F7** | Entradas y salidas de efectivo (retiros, pagos a proveedores, cambio) |
| **F8** | Artículo varios: venta sin código, con precio libre |
| **F9** | Cortes: X de cajero, X de caja (parciales) y **Z** (cierre del día) |
| **F10** | Cambiar de usuario sin cerrar el programa |
| **F11** | Administración (productos, inventario, reportes, usuarios, configuración) |
| **F12** | Cancelar un ticket ya cobrado (regresa la mercancía al inventario) |
| **Ctrl+P** | Reimprimir el último ticket |
| **Ctrl+D** o **Shift+−** | Abrir el cajón |
| **Ctrl+I** | Elegir o detectar la impresora |
| **Ctrl+Supr** | Cancelar toda la venta en curso |
| **Esc** | Limpiar la captura o cerrar una ventana |

**Artículos a granel** (semillas, botanas, dulces): en el producto marque *se vende por kilo*. Al escanearlo, el programa pide el peso: puede escribirlo en kilos (`0.750`) o en gramos (`750`). Con F2 se cambia a importe en pesos ("deme $50 de cacahuate") y con F3 se lee la báscula.

### Lista de costos estilo semillera (Excel)

En *F11 → Productos*:

- **Importar Excel semillera**: lee el archivo de costos tal cual se usa hoy (hojas con columnas NO, STOCK, PRODUCTO, BTO, CANT, COSTO, MAYOREO, CANT, MED. MAY., CANT, MENUDEO). Antes de importar muestra una vista previa:
  - Cada renglón de proveedor o grupo (JUANJO, SAMY, DURAN…) se vuelve el proveedor y grupo de los productos que le siguen.
  - El costo por kg es el precio del bulto ÷ lo que trae.
  - Los tres precios se aplican **solos en caja** según la cantidad: menudeo, medio mayoreo (desde 1 kg, 6 kg…) y mayoreo (desde 3 kg, 25 kg…).
  - Detecta qué se vende por kilo y qué por pieza (empacados, desechables, sobres…). Se puede corregir en cada producto.
  - Usted decide si la columna STOCK es el **stock mínimo** o la **existencia actual**.
  - Los productos nuevos reciben un código corto (1, 2, 3…) para teclearlo en caja. Si se vuelve a importar, actualiza costos y precios.
- **Lista de costos (Excel)**: genera un `.xlsx` con el mismo formato (agrupado por proveedor, con la fórmula de costo = BTO/CANT y las columnas L M M J V S D para las marcas de la semana), más una hoja de **precios para mostrador** sin costos.
- El formulario de producto calcula igual que la hoja: costo del bulto ÷ lo que trae = costo; a cada precio se le ve su % de ganancia. Con *mantener el %*, al cambiar el costo del bulto se actualizan solos los tres precios.
- Todos los reportes (ventas, cortes, inventario, kardex…) se exportan a Excel con el mismo estilo, con totales.

### Turnos y cortes

- Al abrir la caja se captura el **fondo** inicial.
- **Corte X**: solo informa. Se puede sacar las veces que sea, por cajero o de toda la caja.
- **Corte Z**: cierra el turno. Pide contar el efectivo y muestra si hay **sobrante o faltante** (fondo + ventas en efectivo + entradas − salidas − devoluciones). Imprime el ticket del corte, hace un respaldo y abre el siguiente turno.
- El corte muestra las ventas brutas, las cancelaciones, las ventas netas, el desglose por forma de pago y por departamento, y la ganancia estimada (venta − costo).

### Permisos

- **Administrador**: todo.
- **Cajero**: vende, cobra, hace entradas y salidas, y saca su corte X. Para descuentos, cancelar tickets, el corte Z y Administración se pide la clave de un administrador (se puede permitir al cajero en Configuración).

### Si se va la luz o algo se daña
- La base usa escritura completa: una venta ya cobrada no se pierde por un corte de energía.
- Después de cada venta o cambio se guarda una copia en `respaldos\ultimo.db` y otra en la carpeta que se eligió en el primer arranque (por defecto el escritorio). Además hay un respaldo diario (los últimos 30).
- Si la base se daña, el programa lo detecta al abrir y ofrece restaurar el último respaldo bueno. La base dañada se guarda aparte, no se borra.
- El acceso **VentaFácil - Reparar** del escritorio revisa el programa, la base, los respaldos y los accesos sin borrar datos.

### Códigos de barras y etiquetas
La lista de la semillera no trae códigos de barras, así que lo importado se cobra con su **código corto** (1, 2, 3…). Para escanear: Administración → Productos → **Códigos de barras…**. Se toma el producto, se pasa el lector por su empaque y el código se guarda solo (en el *código alterno*; el código corto no se toca) y salta al siguiente. `F2` saltar · `F3` «sin código de barras» · `F4` quitar · `F5` etiqueta. Un código no puede ser de dos productos: si ya existe, avisa de quién es y ofrece pasarlo.
Lo que no trae código (granel, verdura): **Etiqueta** imprime una tira con nombre, precio y el código corto en Code 128 por la impresora de tickets (por controlador de Windows o como imagen en modo ESC/POS, sin depender del comando de barras de la impresora).

### Conteo de inventario con Excel
Inventario → **Conteo con Excel**: se exporta la hoja (todo o por proveedor), se llena la columna CONTADO en el negocio y se importa; antes de aplicar se ven los faltantes y sobrantes con su valor.

### Licencia
30 días de prueba; después una clave por computadora (permanente). Se genera con `herramientas/licencias` (ver [docs/LICENCIAS.md](docs/LICENCIAS.md)).

### Pruebas
- [docs/PRUEBAS_WINDOWS.md](docs/PRUEBAS_WINDOWS.md): 36 pasos para la laptop.
- `dotnet run` en `herramientas/licencias -- --probar`, `herramientas/pruebas-dinero`, `herramientas/pruebas-conteo` y `herramientas/pruebas-barras` (código de barras contra una librería independiente): pruebas automáticas (se corren en Mac o Windows).

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
