# Análisis de Suite Zafirah 2008 (base de VentaFácil)

Este análisis se hizo con la carpeta `Suite Zafirah 2008` (1,282 archivos, 96 MB): sus manuales en HTML, sus archivos de configuración, sus 139 reportes y un ticket real.

## Qué es

Zafirah Retail System 2008, de Cophen Soluciones (México). Está hecho en **Visual Basic 6**, con controles OCX de terceros (Infragistics UltraGrid, ProtoView) y licencia por llave **HASP**. Tiene dos programas:

| Programa | Archivo | Para qué sirve |
|---|---|---|
| **ZafirahPDV2008** | `POS.exe` | Punto de venta (cajero) |
| **ZafirahADM2008** | `Admin.exe` | Administración: catálogos, compras, inventario, ventas, reportes |

Necesita **SQL Server** (base de datos `smartdatabase`, proveedor `SQLOLEDB` en `(local)`) y **MDAC**. Por eso es pesado de instalar y frágil en Windows nuevos: depende de OCX registrados, VB6 y SQL Server.

El ticket encontrado (`tickets/Caja1Ticket42730.txt`) es de **"SEMILLAS Y BOTANAS"**: un ticket de *Venta Departamental* de **0.750 kg de TAPIOCA a $112.50/kg = $84.38**, pagado con $200 (cambio $115.62), con el importe con letra, sucursal 1, caja 1 y ticket 42730. Es decir, el uso real es una **tienda de granel**: venta por peso, departamentos y cobro rápido en efectivo.

## Punto de venta de Zafirah: funciones y teclas

> Desde la v1.15 las teclas de VentaFácil cambiaron: F1–F6 son los módulos y estas funciones se hacen con Ctrl+F# (ver README). La tabla compara las funciones, no la tecla actual.

| Tecla | Zafirah | VentaFácil |
|---|---|---|
| F2 | Búsqueda de artículos (código, alterno, descripción) | ✅ igual; busca por varias palabras mientras se escribe |
| F3 | Cancelar artículo (navegando o por código) | ✅ quita el renglón seleccionado o parte de la cantidad |
| F4 | Consulta de precios (y opción de vender) | ✅ igual |
| F5 | Cuentas suspendidas (suspender y recuperar) | ✅ "Venta en espera", con nota e impresión del folio |
| F6 | Descuentos y ajustes (% , importe, precio nuevo) | ✅ igual, más % a toda la venta |
| F7 | Fondos y retiros de dinero | ✅ Entradas y salidas, con comprobante y apertura del cajón |
| F8 | Cobros y pagos de servicios | ➡️ cambia a **Artículo varios** (venta sin código / departamental) |
| F9 | Cortes de cajero (X/Z) y de caja (X/Z), con declaración de valores | ✅ X de cajero, X de caja, Z con efectivo contado y sobrante/faltante |
| F10 | Cambio de usuario | ✅ igual |
| F11 | Configuración general | ➡️ Administración (configuración incluida) |
| F12 | Cancelación total de tickets | ✅ igual; regresa el inventario |
| + | Totalizar (F2 efectivo, F3 tarjeta, F4 cheque, F5 vales, F6 dólares) | ✅ F2 efectivo, F3 tarjeta, F4 transferencia, F5 vales |
| * | Multiplicar cantidad | ✅ igual (`3*` + escanear) |
| Shift+C | Cancelar la venta antes de cobrar | ✅ Ctrl+Supr |
| Shift+− | Abrir cajón | ✅ igual, y también Ctrl+D |
| Shift+/ | Activar la báscula | ✅ al escanear un artículo a granel se pide el peso o se lee la báscula |
| Shift+V | Asignar vendedor | ❌ no se incluyó (se puede agregar) |
| Shift+P | Nivel de precio | ➡️ precio de mayoreo automático por cantidad |
| — | Reimprimir ticket y el último ticket | ✅ Ctrl+P y desde Reportes |

Configuración de Zafirah (F11): sucursal, caja y almacén, logotipo, desglose de impuestos, importe con letra, aviso de artículo no encontrado, **declaración de valores en el corte**, mensaje publicitario, impresora (serial/paralelo/USB, cajón, cuchilla), escáner, torreta, báscula y 4 mensajes del ticket. VentaFácil conserva lo útil (datos del ticket, caja, importe con letra, aviso de no encontrado, declaración de efectivo, banner, impresora/cajón/cuchilla, báscula) y deja fuera lo que ya no se usa (torreta, escáner serial, validación de formas de pago).

## Administración de Zafirah: módulos

- **Catálogos**: artículos (3 hojas de captura), almacenes, aduanas, avisos, bloqueos, clientes, tipos de cliente, formas de pago, impuestos, kits, presentaciones, promociones, proveedores, series, tiendas, tipos de movimiento, unidades, vendedores.
- **Compras**: órdenes de compra, recepción, devoluciones a proveedor, cuentas por pagar.
- **Inventario**: departamentos (5 niveles), movimientos, inventario físico, kardex, actualización de costos y precios.
- **Ventas**: cotizaciones, remisiones, facturas, apartados, devoluciones, cuentas por cobrar.
- **Herramientas**: importar y exportar catálogos, depuración, mantenimiento, reimpresión, códigos de barras.
- **Configuración**: empresa, grupos, cuentas y permisos de usuario.
- **139 reportes** (Crystal Reports): cortes, ventas por cajero, departamento, producto y hora, existencias, costos, márgenes, kardex, etc.

## Qué se tomó para VentaFácil (versión 1)

La meta es lo que **realmente se usa todos los días** en un negocio pequeño, sin la carga de configuración de Zafirah:

1. **Punto de venta** con las mismas teclas y el mismo flujo (ver tabla).
2. **Productos** con código de barras, código alterno, departamento, precio, costo, utilidad, mayoreo, granel e inventario. **Importación desde CSV/Excel** para pasar el catálogo de golpe.
3. **Inventario**: entradas de mercancía, ajuste por conteo físico, productos bajo el mínimo, kardex y valor del inventario.
4. **Cortes** X y Z, con sobrante/faltante, por forma de pago y por departamento, con ganancia estimada.
5. **Reportes** esenciales: ventas por día, tickets (con reimpresión y cancelación), productos más vendidos, ventas por cajero, por forma de pago y por departamento, historial de cortes, y entradas y salidas de caja.
6. **Usuarios** con dos roles (Administrador y Cajero) y autorización del administrador para descuentos y cancelaciones.
7. **Respaldos automáticos** diarios y en cada corte Z (Zafirah dependía de respaldar SQL Server a mano).

### Pendiente para versiones siguientes (según lo pida el negocio)

- Clientes con **crédito** (cuentas por cobrar y abonos) y apartados.
- Proveedores, órdenes de compra y cuentas por pagar.
- Impresión de etiquetas con código de barras.
- Varias cajas en red compartiendo la misma base de datos (hoy cada caja tiene su propia base).
- Facturación electrónica CFDI 4.0 (requiere un PAC).
- Vendedores y comisiones, promociones y kits.

## Decisiones técnicas

| Tema | Zafirah 2008 | VentaFácil |
|---|---|---|
| Lenguaje | Visual Basic 6 + OCX | C# WinForms, .NET Framework 4.0 |
| Base de datos | SQL Server (servicio aparte) | SQLite, en un solo archivo |
| Instalación | Instalador, registro de OCX, MDAC y SQL Server | Copiar la carpeta (portátil) |
| Licencia | Llave HASP | Ninguna |
| Impresión | Driver OPOS / serial | Controlador de Windows **o** ESC/POS directo |
| Tamaño | 96 MB, sin contar SQL Server | ~2 MB |
