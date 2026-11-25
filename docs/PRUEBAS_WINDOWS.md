# Lista de pruebas en la laptop (Windows)

Hacer **antes de entregar**, en la computadora real, con la impresora y el cajón conectados.
Tiempo estimado: 1.5 a 2 horas. Marque cada paso. Si algo falla, anote el paso y mande una foto de la pantalla.

> Lo que se probó fuera de Windows (en una Mac): claves de licencia, lectura/escritura de Excel del conteo,
> redondeo de dinero, detección de base dañada, y el código de barras Code 128 (contra una librería independiente) y la trama ESC/POS de las etiquetas.
> **El dibujo de la etiqueta, su impresión real y la lectura con el lector NO se han probado: son el paso 36. Tampoco se han visto en pantalla los módulos (37 a 40b), el asistente de importación (41 a 48) ni su conexión a un SQL Server real, los permisos (49 a 56) ni los envíos y el reporte (57 a 81).**
>
> **Sí se probó con un SQLite de verdad** (herramientas/pruebas-datos, con un adaptador porque `System.Data.SQLite` solo existe para Windows): el código real de esquema, migración, productos, códigos de barras, cobros, cancelaciones, cortes, dinero exacto, conteo, respaldos, base dañada, restauración y la **aplicación** del importador. **Nada de lo que sigue se había probado en Windows.**

## A. Instalación
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 1 | Copiar `VentaFacil.exe` a la laptop y abrirlo | Se copia a `C:\VentaFacil`, aparecen **VentaFácil** y **VentaFácil - Reparar** en el escritorio y se abre el programa |
| 2 | Primer arranque | Pregunta dónde guardar los respaldos (por defecto escritorio\VentaFacil_Respaldos) |
| 3 | Entrar con `admin` / `admin` | Entra y avisa de la impresora la primera vez |
| 4 | F6 Ajustes → Usuarios | Cambiar la contraseña de `admin` y crear un cajero |

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
| 14 | Ctrl+F5: poner en espera y recuperar | La venta vuelve completa |
| 15 | Ctrl+F8: cancelar un ticket (con admin) | El producto vuelve al inventario |
| 16 | F2 Turno → Entradas / salidas: una entrada y una salida de efectivo | Salen en el corte |
| 17 | F2 Turno → Cortes X / Z → corte X | Totales iguales a lo vendido |
| 18 | F2 Turno → Cortes X / Z → corte Z contando efectivo | Dice sobrante/faltante correcto y abre un turno nuevo |

## D. Productos e inventario
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 19 | F3 Catálogo → Importar Excel | Vista previa, importa ~490 productos sin errores |
| 20 | F3 Catálogo → Lista costos; abrir el Excel | Mismo formato que el original |
| 21 | F4 Almacén → entrada de mercancía | Sube la existencia |
| 22 | F4 Almacén → **Conteo con Excel**: exportar hoja, llenar CONTADO en 5 productos, importar | Muestra diferencias con valor, se aplican y salen en el kardex |
| 23 | F5 Resultados: reportes con filtros y exportar a Excel | Totales correctos |

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
| 30 | F6 Ajustes → **Licencia** | Muestra el código del equipo y "le quedan 30 días" |
| 31 | En tu Mac: `cd herramientas/licencias && dotnet run`, pegar el código, generar la clave. Escribirla en la laptop (el `.exe` debe haberse compilado en tu Mac con `publicar_release.sh`) | "VentaFácil quedó activado". Reabrir el programa: no vuelve a pedirla |
| 32 | Cambiar la fecha de Windows 40 días adelante **antes** de activar (en otra PC de prueba) | Pide la clave y no deja entrar sin ella. Devolver la fecha |

## G. Windows 7 (si la tienda lo usa)
- Debe tener **.NET Framework 4.8** instalado (viene en Windows 10/11; en Windows 7 se instala aparte).
- Repetir al menos los pasos 1, 6, 12, 18 y 27.

## H. Códigos de barras y etiquetas
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 33 | F3 Catálogo → **Códigos de barras**; tomar 3 productos con empaque y pasar el lector | Cada código se guarda y salta al siguiente; la columna «Cód. barras» de Productos lo muestra |
| 34 | Pasar el MISMO código en otro producto | Avisa de quién es y ofrece pasarlo; no deja duplicar |
| 35 | En la pantalla de cobro, escanear un empaque ya asignado | Entra el producto con su precio |
| 36 | Un producto de granel → **Etiqueta** (F5) → ver la vista previa → Imprimir; luego escanear la etiqueta impresa **con el lector** | La etiqueta sale legible (nombre, precio, barras) y el lector la lee y entra el producto |

## I. Módulos y atajos
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 37 | Abrir la pantalla de venta | Se ven el encabezado, la **barra de módulos** (F1 Caja … F6 Ajustes) y la barra de 8 botones «Ctrl+F#»; nada se encima ni se corta con 1366x768 |
| 38 | Pulsar F2, F3, F4, F5, F6 y F1 con el teclado, y luego con el ratón | Cambia de módulo; la línea blanca se desliza; F1 regresa a Caja con el cursor en la captura |
| 39 | En cada módulo, ver la barra de atajos de arriba y probar Ctrl+F1, Ctrl+F2… | Cada botón hace lo que dice (cambia de vista o abre su diálogo); Esc regresa a Caja |
| 40 | En Caja: Ctrl+F1 cobra (y también «+»), Ctrl+F2 busca, Ctrl+F8 cancela; F10 cambia de usuario | Funcionan; con una venta a medias, F3–F6 avisan y no entran |
| 40b | Pasar el ratón y pulsar botones secundarios y principales | Borde azul de 2 px **liso** (sin puntitos), el color cambia suave, el botón se hunde al pulsar |

## J. Importar de Zafirah (pasos 41–48)
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 41 | F3 Catálogo → icono pequeño al final de la barra (importar de Zafirah). Pestaña «Archivo»: elegir un Excel o CSV de prueba | Se abre el asistente completo (cabe en 1366x768, sin cortes). Aparecen las columnas propuestas con su ejemplo y la vista previa con avisos |
| 42 | Si Zafirah está en esa PC: pestaña «Base de Zafirah» → servidor `(local)` (o `.\SQLEXPRESS`), **Conectar** (Windows o usuario SQL) → base `smartdatabase` → tabla `ZFRADMARTICULOS` → **Leer tabla** | Conecta sin escribir nada en Zafirah; propone Código, Nombre, Precio, Costo, Existencia… **Comprobar a ojo** que Menudeo, Costo y Existencia sean las columnas correctas; elegir «a partir de» para medio mayoreo/mayoreo si hace falta. Si falla, el mensaje debe ser claro (usuario, permiso, servidor) |
| 43 | **Importar** | Pide confirmar, hace respaldo, muestra el resumen; los productos salen en Productos y se cobran por su código corto y por su código de barras |
| 44 | Repetir la importación | No duplica nada; actualiza; si un código era de otro producto lo lista y no lo pisa |

| 45 | Pestaña **«Respaldo .BK»** → **Elegir respaldo…** (el .BK de 5 MB, 2020) | Muestra base, versión de SQL Server, tamaño y archivos; si la versión no sirve en ese servidor lo dice con claridad |
| 46 | **Restaurar y leer** | Barra de avance y tiempo; al terminar lee los productos y propone columnas. Si falla con permiso (3201) ofrece copiar a `C:\Users\Public` |
| 47 | **Comandos…** y, en Management Studio/`osql`, ver que `master` ya no tiene bases `vf_import_tmp_…` tras cerrar el asistente | El T-SQL se ve completo; la temporal se borró |
| 48 | **Guardar…** un perfil, cerrar y reabrir; **Carpeta** y editar el .json con el Bloc de notas | El perfil aparece en la lista y propone las mismas columnas; un .json con error se avisa sin tumbar el asistente |

## K. Usuarios y permisos
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 49 | Entrar como cajero | Catálogo, Almacén, Resultados y Ajustes salen con candado; F3–F6 piden clave |
| 50 | Cajero: F2 Turno → Cortes | Abre **su** corte; la lista solo trae su nombre; corte de caja y Z piden clave |
| 51 | Administrador dentro de Catálogo → F10 → entrar como cajero | Regresa a Caja; F3 vuelve a pedir clave |
| 52 | Cajero entra a Ajustes con clave de admin, vuelve a Caja y pulsa F6 | Vuelve a pedir clave |
| 53 | Ajustes → Usuarios → un cajero: marcar Almacén, quitar cajón; entrar con él | Almacén abre sin clave; abrir cajón la pide |
| 54 | Dar Ajustes a un cajero y entrar con él | No ve Usuarios ni Respaldos |
| 55 | En Catálogo, Grupos y Usuarios: F1–F6 y Ctrl+N / Ctrl+M / Ctrl+E | F1–F6 cambian de módulo siempre (F5 ya **no** elimina); Ctrl+letra hace la acción |
| 56 | Abrir con la base de la v1.17 | Siguen todos los usuarios con su clave |

## L. Envíos oficina → tienda (dos computadoras y una USB)
| # | Qué hacer | Debe pasar |
|---|-----------|-----------|
| 57 | CAJA: F6 Ajustes → **Envíos** → Crear archivo de enlace, guardarlo en la USB | Pide contraseña dos veces, pregunta la carpeta y dice cuántos productos lleva |
| 58 | OFICINA (instalación nueva, con licencia): Ajustes → Envíos → **Convertir…** | Pide la contraseña; el título dice OFICINA; el Catálogo es el de la tienda |
| 59 | OFICINA: intentar cobrar | Avisa que ahí no se cobra |
| 60 | OFICINA: cambiar 2 precios, crear 1 producto, eliminar 1; F4 → **Llegó mercancía**: 3 productos (uno con −2) | Se guardan; salir a otro módulo a medio capturar NO pierde lo capturado |
| 61 | OFICINA: F4 → Envíos → **Crear envío** | El resumen coincide con lo hecho; pregunta la ruta; queda el `.vfenvio` en la USB |
| 62 | CAJA: vender 1 pieza de un producto que viene en el envío; luego F2 Turno → **Recibir envío** | Encuentra el envío solo, muestra el resumen; como es el primero pide administrador |
| 63 | Aplicar | Existencia = lo que había − venta + lo que llegó; precios nuevos al cobrar; queda el acuse en la USB |
| 64 | Aplicar el mismo otra vez | Dice que ya se aplicó; nada cambia |
| 65 | OFICINA: Leer acuse (o crear otro envío en esa USB) | «La tienda está al día»; el historial dice Aplicado |
| 66 | Crear envío A, no llevarlo; más cambios, crear B; aplicar solo B como **cajero** | Entran los cambios de A y B; al cajero no le pide clave |
| 67 | Poner un precio en 9.20 en vez de 92 y crear el envío | Avisa en la oficina y en la caja (cuadro amarillo) |
| 68 | CAJA: F4 → Envíos recibidos → **Deshacer el último** | Regresan precios y existencias; la venta del paso 62 sigue |
| 69 | Con una venta a medias, Recibir envío; y sacar la USB a la mitad de aplicar | Pide terminar la venta; si se saca la USB, o se aplicó todo o nada |
| 70 | Revisar las pantallas de envíos en 1366x768 | Nada se corta ni se encima |
| 71 | CAJA, con una venta a medias: conectar la USB con un envío nuevo | En 2 o 3 segundos sale la franja amarilla; el cursor **sigue** en la captura y se puede seguir escaneando; «Ver envío» pide terminar la venta |
| 72 | Cobrar y pulsar «Ver envío»; luego sacar y volver a meter la USB ya aplicada | Abre el resumen; después de aplicar ya no avisa |
| 73 | Doble clic en el `.vfenvio` con VentaFácil **cerrado** y luego **abierto** (también minimizado) | Cerrado: abre, pide usuario y muestra el resumen. Abierto: se pone al frente y lo muestra, sin abrir otra copia |
| 74 | OFICINA: meter la USB con el acuse; doble clic en un `.vfenvio` | Franja con «Leer acuse»; el envío dice que se aplica en la caja |
| 75 | CAJA: hacer 3 ventas (una con tarjeta), cancelar 1, cerrar turno; F2 → **Reporte a oficina** | Pregunta la carpeta y guarda el archivo en la USB |
| 76 | OFICINA: meter la USB → «Leer acuse»; F5 Resultados | El título dice la fecha del reporte; **Ventas por día** coincide con el reporte de la caja |
| 77 | OFICINA: F5 → Lo más vendido, Existencias y Cortes | Existencias iguales a las de la caja; el corte con el mismo esperado, declarado y diferencia |
| 78 | Leer el mismo archivo otra vez | Los totales no se duplican |
| 79 | CAJA: cambiar un precio y crear un producto; mandar reporte. OFICINA: F5 → **Diferencias** | Salen las dos cosas |
| 80 | En Diferencias: traer el producto y mandar el precio de la oficina; crear envío y aplicarlo | El producto queda una sola vez; el precio de la tienda vuelve al de la oficina |
| 81 | Revisar las pantallas de F5 en la oficina en 1366x768, y los 8 botones de Turno en la caja | Nada se corta; los textos de los botones se leen completos |

Resultado: ______ de 81 pasos bien.   Fallas anotadas: ____________________________
