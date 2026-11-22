# Lista de pruebas en la laptop (Windows)

Hacer **antes de entregar**, en la computadora real, con la impresora y el cajón conectados.
Tiempo estimado: 1.5 a 2 horas. Marque cada paso. Si algo falla, anote el paso y mande una foto de la pantalla.

> Lo que se probó fuera de Windows (en una Mac): claves de licencia, lectura/escritura de Excel del conteo,
> redondeo de dinero, detección de base dañada, y el código de barras Code 128 (contra una librería independiente) y la trama ESC/POS de las etiquetas.
> **El dibujo de la etiqueta, su impresión real y la lectura con el lector NO se han probado: son el paso 36. Tampoco se ha visto en pantalla el menú superior (pasos 37 a 40).** **Nada de lo que sigue se había probado en Windows.**

## A. Instalación
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 1 | Copiar `VentaFacil.exe` a la laptop y abrirlo | Se copia a `C:\VentaFacil`, aparecen **VentaFácil** y **VentaFácil - Reparar** en el escritorio y se abre el programa |
| 2 | Primer arranque | Pregunta dónde guardar los respaldos (por defecto escritorio\VentaFacil_Respaldos) |
| 3 | Entrar con `admin` / `admin` | Entra y avisa de la impresora la primera vez |
| 4 | Configuración → Usuarios | Cambiar la contraseña de `admin` y crear un cajero |

## B. Impresora y cajón
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 5 | Asistente de impresora → detectar | Aparece la impresora de tickets (anotar modelo y si es 58 u 80 mm) |
| 6 | Ticket de prueba | Sale legible, sin letras raras, con corte |
| 7 | Probar cajón | Se abre. Si no: probar el otro pin (2 o 5) |

## C. Venta diaria
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 8 | Abrir turno con fondo $500 | Abre sin errores |
| 9 | Escanear un producto con lector | Entra el renglón con su precio |
| 10 | `3*` + producto (multiplicar) | Cantidad 3 correcta |
| 11 | Producto a granel (kg) con importe y con báscula (si hay) | Peso y total correctos |
| 12 | Cobrar en efectivo con cambio | Ticket, cajón y cambio correctos |
| 13 | Cobrar con tarjeta y con transferencia | Queda registrada la forma de pago |
| 14 | F5 poner en espera y recuperar | La venta vuelve completa |
| 15 | F12 cancelar un ticket (con admin) | El producto vuelve al inventario |
| 16 | F7 entrada y salida de efectivo | Salen en el corte |
| 17 | F9 → corte X | Totales iguales a lo vendido |
| 18 | F9 → corte Z contando efectivo | Dice sobrante/faltante correcto y abre un turno nuevo |

## D. Productos e inventario
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 19 | Productos → importar Excel de la semillera | Vista previa, importa ~490 productos sin errores |
| 20 | Exportar lista de costos a Excel y abrirla | Mismo formato que el original |
| 21 | Inventario → entrada de mercancía | Sube la existencia |
| 22 | Inventario → **Conteo con Excel**: exportar hoja, llenar CONTADO en 5 productos, importar | Muestra diferencias con valor, se aplican y salen en el kardex |
| 23 | Reportes con filtros y exportar a Excel | Totales correctos |

## E. Fallas (lo que más le importa al dueño)
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 24 | Hacer una venta y **matar el programa** desde el Administrador de tareas a media operación; reabrir | No se pierde ninguna venta ya cobrada |
| 25 | Desconectar el cable de corriente de la laptop (con batería quitada si se puede) durante una venta; reencender | Abre normal, ventas cobradas siguen |
| 26 | Ver la carpeta de respaldos | Existe `VentaFacil_ultimo.db` actualizado con la hora de la última venta |
| 27 | Con el programa **cerrado**: abrir el acceso **VentaFácil - Reparar** | Revisión con [OK] en todo y botón de restaurar apagado |
| 28 | Simular daño: cerrar el programa, copiar `C:\VentaFacil\datos\ventafacil.db` a otro lado y arruinar la original (abrir con el Bloc de notas, escribir basura al centro, guardar). Abrir VentaFácil | Avisa que la base está dañada y ofrece restaurar el último respaldo; la dañada queda en `respaldos\danada_*.db` |
| 29 | Borrar `C:\VentaFacil\VentaFacil.exe` y volver a abrir el `.exe` descargado | Se reinstala solo |

## F. Licencia
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 30 | Configuración → **Licencia…** | Muestra el código del equipo y "le quedan 30 días" |
| 31 | En tu Mac: `cd herramientas/licencias && dotnet run`, pegar el código, generar la clave. Escribirla en la laptop (el `.exe` debe haberse compilado en tu Mac con `publicar_release.sh`) | "VentaFácil quedó activado". Reabrir el programa: no vuelve a pedirla |
| 32 | Cambiar la fecha de Windows 40 días adelante **antes** de activar (en otra PC de prueba) | Pide la clave y no deja entrar sin ella. Devolver la fecha |

## G. Windows 7 (si la tienda lo usa)
- Debe tener **.NET Framework 4.8** instalado (viene en Windows 10/11; en Windows 7 se instala aparte).
- Repetir al menos los pasos 1, 6, 12, 18 y 27.

## H. Códigos de barras y etiquetas
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 33 | Productos → **Códigos de barras…**; tomar 3 productos con empaque y pasar el lector | Cada código se guarda y salta al siguiente; la columna «Cód. barras» de Productos lo muestra |
| 34 | Pasar el MISMO código en otro producto | Avisa de quién es y ofrece pasarlo; no deja duplicar |
| 35 | En la pantalla de cobro, escanear un empaque ya asignado | Entra el producto con su precio |
| 36 | Un producto de granel → **Etiqueta** (F5) → ver la vista previa → Imprimir; luego escanear la etiqueta impresa **con el lector** | La etiqueta sale legible (nombre, precio, barras) y el lector la lee y entra el producto |

## I. Menú superior
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 37 | Abrir la pantalla de venta | Se ven el encabezado azul, el **menú** (Venta, Caja, Productos, Inventario, Reportes, Sistema, Ayuda) y la barra de 9 botones; la lista de venta ocupa todo el ancho; nada se encima ni se corta con 1366x768 |
| 38 | Abrir cada menú con el ratón y con Alt+V, Alt+C… | Se despliegan legibles (la opción seleccionada se ve en blanco sobre azul) y muestran su tecla a la derecha |
| 39 | Hacer una venta usando solo el menú (Venta → Buscar artículo, Cobrar) y otra solo con la barra de botones | Funcionan igual que con las teclas; al cerrar un menú el cursor vuelve a la captura y el lector sigue escaneando |
| 40 | Pulsar **F10** con la caja abierta; luego Productos → Códigos de barras… | F10 cambia de usuario **sin** abrir el menú de Windows; la opción de Productos abre la Administración directo en la asignación de códigos |

Resultado: ______ de 40 pasos bien.   Fallas anotadas: ____________________________
