using VentaFacil;
using VentaFacil.Core;
using VentaFacil.Data;

// ======================================================================
// Pruebas de INTEGRACIÓN con un SQLite de verdad. Ejecutan el código REAL de la capa de datos del programa
// (esquema, migración, productos, códigos de barras, ventas, cortes, conteo, respaldos, restauración y el
// importador de Zafirah), enlazado desde src/VentaFacil. Lo único distinto es el adaptador (AdaptadorSQLite.cs):
// el programa usa System.Data.SQLite (solo Windows) y aquí se usa Microsoft.Data.Sqlite con la misma API.
// Uso:  dotnet run
// ======================================================================
int fallas = 0;
void Revisar(string nombre, bool ok, string detalle = "") { Console.WriteLine((ok ? "[OK]    " : "[FALLA] ") + nombre + (ok || detalle == "" ? "" : "  -> " + detalle)); if (!ok) fallas++; }
void Seccion(string t) { Console.WriteLine("\n== " + t); }

void Limpiar()
{
    Db.PrepararRutas();
    SQLiteBorrar();
    Db.Inicializar();
    Config.Cargar();
}
void SQLiteBorrar()
{
    System.Data.SQLite.SQLiteConnection.ClearAllPools();
    foreach (var s in new[] { "", "-wal", "-shm" }) if (File.Exists(Db.Archivo + s)) File.Delete(Db.Archivo + s);
    string resp = Path.Combine(Db.Carpeta, "respaldos");
    if (Directory.Exists(resp)) Directory.Delete(resp, true);
}
long Cuenta(string sql, params object[] a) { return Convert.ToInt64(Db.Scalar(sql, a)); }
Usuario admin() { return UsuarioRepo.Validar("admin", "admin"); }

// ---------------------------------------------------------------- tabla estilo Zafirah (ver pruebas-zafirah)
System.Data.DataTable TablaZ()
{
    var t = new System.Data.DataTable();
    foreach (var c in new[] { "CodigoProducto", "CodigoAlterno", "Descripcion", "Costo1", "UltimoCosto", "Precio2", "Precio3", "PrecioNormal", "LimitePrecio1", "LimitePrecio2", "Existencia", "NumAlmacen", "Unidad", "Departamento", "NumProveedor" }) t.Columns.Add(c, typeof(object));
    t.Rows.Add("109", "7501234567890", "TAPIOCA", 60.5m, 0m, 95m, 90m, 100m, 5m, 10m, 20m, 1, "KG", "Semillas", 7);
    t.Rows.Add("109", "7501234567890", "TAPIOCA", 60.5m, 0m, 95m, 90m, 100m, 5m, 10m, 5m, 2, "KG", "Semillas", 7);
    t.Rows.Add("110", "0", "ALPISTE", 25m, 0m, 0m, 0m, 40m, 5m, 10m, 8m, 1, "PZA", "Semillas", 8);
    t.Rows.Add("00113", "00113", "GIRASOL", 35m, 0m, 0m, 0m, 30m, 5m, 10m, 4m, 1, "KG", "Semillas", 8);
    t.Rows.Add("115", "7509876543210", "CHILE", 70m, 0m, 105m, 0m, 110m, 5m, 10m, 2m, 1, "Kilo", "Chiles", 7);
    return t;
}
Dictionary<CampoZ, string> MapaZ() => new Dictionary<CampoZ, string> {
    [CampoZ.Codigo] = "CodigoProducto", [CampoZ.Barras] = "CodigoAlterno", [CampoZ.Descripcion] = "Descripcion", [CampoZ.Costo] = "Costo1", [CampoZ.Menudeo] = "PrecioNormal",
    [CampoZ.Medio] = "Precio2", [CampoZ.DesdeMedio] = "LimitePrecio1", [CampoZ.Mayoreo] = "Precio3", [CampoZ.DesdeMayoreo] = "LimitePrecio2",
    [CampoZ.Existencia] = "Existencia", [CampoZ.Almacen] = "NumAlmacen", [CampoZ.Unidad] = "Unidad", [CampoZ.Grupo] = "Departamento", [CampoZ.Proveedor] = "NumProveedor" };
var proveedoresZ = new Dictionary<string, string> { ["7"] = "JUANJO", ["8"] = "LA CASA" };
ResultadoZ Traducir(System.Data.DataTable t) { return ZafirahMapeo.Transformar(t, MapaZ(), new OpcionesZ { ProveedoresPorClave = proveedoresZ }); }

// ================================================================ 1. Esquema y migración
Seccion("Esquema y migración");
Limpiar();
Revisar("la base nueva se crea, es de la versión 4 y pasa la revisión de integridad", Cuenta("PRAGMA user_version") == 4 && Salud.Revisar(Db.Archivo) == "ok");
Revisar("existen las 10 tablas del programa", Cuenta("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('config','usuarios','departamentos','productos','turnos','ventas','venta_detalle','movimientos_caja','movimientos_inv','suspendidas')") == 10);
Revisar("el usuario admin/admin existe y entra; una clave mala no", admin() != null && UsuarioRepo.Validar("admin", "mala") == null);
Revisar("la columna sin_barras (v3) existe", Cuenta("SELECT COUNT(*) FROM pragma_table_info('productos') WHERE name='sin_barras'") == 1);
// Una base de la versión 2 (sin sin_barras) se actualiza sola y conserva sus datos
ProductoRepo.Guardar(new Producto { Codigo = "1", Descripcion = "VIEJO", Precio = 10m, Costo = 6m }, admin().Id);
Db.Exec("ALTER TABLE productos DROP COLUMN sin_barras"); Db.Exec("PRAGMA user_version = 2");
Db.Inicializar();
Revisar("migración v2 → v4: agrega la columna y no pierde el producto", Cuenta("PRAGMA user_version") == 4 && Cuenta("SELECT COUNT(*) FROM pragma_table_info('productos') WHERE name='sin_barras'") == 1 && ProductoRepo.PorCodigo("1") != null);

// Una base de la versión 3 (la de la v1.17, con usuarios ya creados) pasa a la 4 sin que nadie gane ni pierda acceso
UsuarioRepo.Guardar(new Usuario { Login = "caja1", Nombre = "Cajera Uno", Rol = "CAJERO" }, "1234");
Db.Exec("ALTER TABLE usuarios DROP COLUMN permisos"); Db.Exec("PRAGMA user_version = 3");
Db.Inicializar(); Db.Inicializar();   // dos veces: no debe fallar al volver a abrir
Revisar("migración v3 → v4: agrega permisos, conserva usuarios y claves", Cuenta("PRAGMA user_version") == 4 && admin() != null && UsuarioRepo.Validar("caja1", "1234") != null);

Seccion("Permisos por usuario");
{
    var adm = admin(); var caj = UsuarioRepo.Validar("caja1", "1234");
    Func<Usuario, string, bool> P = (u, k) => Permisos.Tiene(u, k, false);
    Revisar("el administrador puede todo, incluso lo exclusivo", Permisos.Todos.All(i => P(adm, i.Clave)) && P(adm, Permisos.SoloAdmin));
    Revisar("el cajero de fábrica: solo Turno, entradas/salidas y cajón", string.Join(",", Permisos.Todos.Where(i => P(caj, i.Clave)).Select(i => i.Clave)) == "turno,movimientos,cajon");
    Revisar("el cajero no tiene corte de caja, corte Z ni lo exclusivo de administrador", !P(caj, Permisos.CorteCaja) && !P(caj, Permisos.CorteZ) && !P(caj, Permisos.SoloAdmin));
    Revisar("el ajuste general da cancelar y descuentos al cajero de fábrica, y nada más", Permisos.Tiene(caj, Permisos.Cancelar, true) && Permisos.Tiene(caj, Permisos.Descuentos, true) && !Permisos.Tiene(caj, Permisos.Catalogo, true));
    caj.Permisos = Permisos.Guardar(new[] { Permisos.Turno, Permisos.Almacen }, false); UsuarioRepo.Guardar(caj, null);
    caj = UsuarioRepo.Obtener(caj.Id);
    Revisar("las casillas propias se guardan y mandan (dan Almacén, quitan el cajón)", P(caj, Permisos.Almacen) && P(caj, Permisos.Turno) && !P(caj, Permisos.Cajon) && !P(caj, Permisos.Catalogo));
    Revisar("con casillas propias, el ajuste general ya no da cancelar", !Permisos.Tiene(caj, Permisos.Cancelar, true));
    Revisar("guardar permisos no cambia la contraseña", UsuarioRepo.Validar("caja1", "1234") != null);
    caj.Permisos = Permisos.Guardar(new string[0], false); UsuarioRepo.Guardar(caj, null); caj = UsuarioRepo.Obtener(caj.Id);
    Revisar("sin ninguna casilla no puede nada (no vuelve a lo de fábrica)", caj.Permisos == "=" && Permisos.Todos.All(i => !P(caj, i.Clave)));
    Revisar("marcar justo lo de fábrica se guarda como «de fábrica»", Permisos.Guardar(new[] { Permisos.Cajon, Permisos.Turno, Permisos.Movimientos }, false) == "");
    Revisar("todas las casillas no dan lo exclusivo de administrador, ni escribiéndolo a mano", !P(new Usuario { Permisos = Permisos.Guardar(Permisos.Todos.Select(i => i.Clave).ToList(), false) }, Permisos.SoloAdmin) && !P(new Usuario { Permisos = "=solo_admin" }, Permisos.SoloAdmin));
    Revisar("un nombre parecido no cuenta (corte_zz, turnos)", !P(new Usuario { Permisos = "=corte_zz,turnos" }, Permisos.CorteZ) && !P(new Usuario { Permisos = "=corte_zz,turnos" }, Permisos.Turno));
    Revisar("un usuario inactivo o nulo no puede nada", !P(new Usuario { Rol = "ADMIN", Activo = false }, Permisos.Turno) && !P(null, Permisos.Turno));
}

// ================================================================ 2. Productos y códigos de barras
Seccion("Productos y códigos de barras");
Limpiar();
var uid = admin().Id;
var pA = new Producto { Codigo = "1", Descripcion = "TAPIOCA", Precio = 100m, Costo = 60.5m, PrecioMedio = 95m, MedioDesde = 5m, PrecioMayoreo = 90m, MayoreoDesde = 10m, Granel = true, Existencia = 25m };
ProductoRepo.Guardar(pA, uid);
var leida = ProductoRepo.PorCodigo("1");
Revisar("guardar y leer un producto conserva precios, niveles y existencia", leida != null && leida.Precio == 100m && leida.PrecioMedio == 95m && leida.MayoreoDesde == 10m && leida.Existencia == 25m && leida.Granel);
Revisar("PrecioPara aplica menudeo, medio mayoreo y mayoreo según la cantidad", leida.PrecioPara(3) == 100m && leida.PrecioPara(5) == 95m && leida.PrecioPara(10) == 90m);
Revisar("el alta con existencia deja su renglón en el kardex", Cuenta("SELECT COUNT(*) FROM movimientos_inv WHERE producto_id=@p0 AND referencia='Alta de producto'", leida.Id) == 1);
ProductoRepo.Guardar(new Producto { Codigo = "2", Descripcion = "ALPISTE", Precio = 40m }, uid);
ProductoRepo.Guardar(new Producto { Codigo = "3", Descripcion = "AVENA", Precio = 30m }, uid);
var p1 = ProductoRepo.PorCodigo("1"); var p2 = ProductoRepo.PorCodigo("2"); var p3 = ProductoRepo.PorCodigo("3");
Revisar("asignar un código de barras: se guarda en el código alterno y el cobro lo encuentra", ProductoRepo.AsignarCodigoBarras(p1.Id, "7501234567890") == null && ProductoRepo.PorCodigo("7501234567890").Id == p1.Id && ProductoRepo.PorCodigo("1").Id == p1.Id);
var dueno = ProductoRepo.AsignarCodigoBarras(p2.Id, "7501234567890");
Revisar("el mismo código en otro producto NO se asigna y dice de quién es", dueno != null && dueno.Descripcion == "TAPIOCA" && ProductoRepo.Obtener(p2.Id).Codigo2 == "");
var corto = ProductoRepo.AsignarCodigoBarras(p2.Id, "3");
Revisar("un código que es el CÓDIGO CORTO de otro producto tampoco se asigna", corto != null && corto.Descripcion == "AVENA");
Revisar("un código con espacios o vacío se rechaza con un error claro", Lanza(() => ProductoRepo.AsignarCodigoBarras(p2.Id, "75 012")) && Lanza(() => ProductoRepo.AsignarCodigoBarras(p2.Id, "  ")));
ProductoRepo.MarcarSinBarras(p3.Id, true);
int con, pend, marc; ProductoRepo.ResumenBarras(out con, out pend, out marc);
Revisar("resumen de códigos: 1 con código, 1 pendiente, 1 marcado «sin código»", con == 1 && pend == 1 && marc == 1, con + "/" + pend + "/" + marc);
ProductoRepo.QuitarCodigoBarras(p1.Id);
Revisar("quitar el código lo deja pendiente y el código vuelve a estar libre", ProductoRepo.PorCodigo("7501234567890") == null && ProductoRepo.AsignarCodigoBarras(p2.Id, "7501234567890") == null);
bool Lanza(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }

// ================================================================ 3. Importador de Zafirah (Aplicar)
Seccion("Importador de Zafirah");
Limpiar(); uid = admin().Id;
var res = Traducir(TablaZ());
var r1 = ImportarProductosZ.Aplicar(res, new OpcionesAplicarZ(), uid);
Revisar("base vacía: se importan 4 productos nuevos (109, 110, 00113, 115) sin errores", r1.Nuevos == 4 && r1.Actualizados == 0 && r1.Errores.Count == 0 && Cuenta("SELECT COUNT(*) FROM productos") == 4, r1.Nuevos + " nuevos, " + r1.Errores.Count + " errores");
var t109 = ProductoRepo.PorCodigo("109");
Revisar("TAPIOCA: precio 100, costo 60.5, medio 95 desde 5, mayoreo 90 desde 10, a granel", t109 != null && t109.Precio == 100m && t109.Costo == 60.5m && t109.PrecioMedio == 95m && t109.MedioDesde == 5m && t109.PrecioMayoreo == 90m && t109.MayoreoDesde == 10m && t109.Granel);
Revisar("existencias de los dos almacenes sumadas (20 + 5 = 25) y con su renglón de kardex", t109.Existencia == 25m && Cuenta("SELECT COUNT(*) FROM movimientos_inv WHERE producto_id=@p0 AND cantidad=25", t109.Id) == 1);
Revisar("el cobro encuentra TAPIOCA por su código de barras y por el código corto", ProductoRepo.PorCodigo("7501234567890").Id == t109.Id);
Revisar("proveedor traducido por la clave (7 → JUANJO) y departamento creado", t109.Proveedor == "JUANJO" && Cuenta("SELECT COUNT(*) FROM departamentos WHERE nombre IN ('SEMILLAS','CHILES')") == 2);
var g = ProductoRepo.PorCodigo("00113");
Revisar("código con ceros a la izquierda conservado; barras igual al código no se guarda", g != null && g.Codigo2 == "" && ProductoRepo.PorCodigo("110").Codigo2 == "");
Revisar("el respaldo previo a la importación existe y se puede leer", r1.Respaldo != "" && File.Exists(r1.Respaldo) && Salud.Revisar(r1.Respaldo) == "ok");

// repetir la importación con precios nuevos
var t2 = TablaZ(); foreach (System.Data.DataRow fila in t2.Rows)
    if ((string)fila["CodigoProducto"] == "109")
    {
        fila["PrecioNormal"] = 110m;                                   // precio nuevo en las dos filas
        if (Convert.ToInt32(fila["NumAlmacen"]) == 1) fila["Existencia"] = 30m;   // almacén 1: 30 (antes 20); almacén 2 sigue en 5 → suman 35
    }
var r2 = ImportarProductosZ.Aplicar(Traducir(t2), new OpcionesAplicarZ(), uid);
var t109b = ProductoRepo.PorCodigo("109");
Revisar("al repetir: nadie se duplica, se actualizan 4 y el precio nuevo (110) queda", r2.Nuevos == 0 && r2.Actualizados == 4 && Cuenta("SELECT COUNT(*) FROM productos") == 4 && t109b.Precio == 110m);
Revisar("sin la casilla de existencias, el stock NO se toca (sigue 25 aunque Zafirah diga 30 + 5)", t109b.Existencia == 25m);
var r3 = ImportarProductosZ.Aplicar(Traducir(t2), new OpcionesAplicarZ { ActualizarExistencias = true }, uid);
Revisar("con la casilla de existencias: se ajusta a la de Zafirah (35) y queda en el kardex como AJUSTE", ProductoRepo.PorCodigo("109").Existencia == 35m && Cuenta("SELECT COUNT(*) FROM movimientos_inv WHERE producto_id=@p0 AND tipo='AJUSTE' AND referencia='Importación de Zafirah'", t109b.Id) == 1);
var r4 = ImportarProductosZ.Aplicar(Traducir(t2), new OpcionesAplicarZ { ActualizarExistentes = false }, uid);
Revisar("sin «actualizar existentes»: todo se omite y no cambia nada", r4.Omitidos == 4 && r4.Actualizados == 0 && r4.Nuevos == 0);

// conflicto de código: el código 109 ya es de OTRO producto
Limpiar(); uid = admin().Id;
ProductoRepo.Guardar(new Producto { Codigo = "109", Descripcion = "OTRA COSA", Precio = 1m }, uid);
var rc = ImportarProductosZ.Aplicar(Traducir(TablaZ()), new OpcionesAplicarZ(), uid);
Revisar("código ya usado por otro producto: NO se pisa y se lista el conflicto", rc.Conflictos.Count == 1 && rc.Conflictos[0].Contains("OTRA COSA") && rc.Conflictos[0].Contains("TAPIOCA") && ProductoRepo.PorCodigo("109").Descripcion == "OTRA COSA" && rc.Nuevos == 3);
var rs = ImportarProductosZ.Aplicar(Traducir(TablaZ()), new OpcionesAplicarZ { SobrescribirConflictos = true }, uid);
Revisar("con «reemplazar»: el producto toma el nombre y los precios de Zafirah", ProductoRepo.PorCodigo("109").Descripcion == "TAPIOCA" && ProductoRepo.PorCodigo("109").Precio == 100m && rs.Conflictos.Count == 0);

// mismo producto con otro código (ya importado antes del Excel de la semillera)
Limpiar(); uid = admin().Id;
ProductoRepo.Guardar(new Producto { Codigo = "7", Descripcion = "alpiste", Precio = 20m, Costo = 5m }, uid);
var rn = ImportarProductosZ.Aplicar(Traducir(TablaZ()), new OpcionesAplicarZ(), uid);
var alpiste = ProductoRepo.PorCodigo("7");
Revisar("mismo NOMBRE con otro código (7 vs 110): es el mismo producto, se actualiza y conserva su código", alpiste != null && alpiste.Precio == 40m && ProductoRepo.PorCodigo("110") == null && Cuenta("SELECT COUNT(*) FROM productos WHERE descripcion='ALPISTE' COLLATE NOCASE") == 1);

// código de barras que ya es de otro producto
Limpiar(); uid = admin().Id;
ProductoRepo.Guardar(new Producto { Codigo = "8", Descripcion = "COSA", Precio = 5m, Codigo2 = "7509876543210" }, uid);
var rb = ImportarProductosZ.Aplicar(Traducir(TablaZ()), new OpcionesAplicarZ(), uid);
Revisar("un código de barras ya usado por otro producto no se duplica (CHILE queda sin él) y se cuenta", rb.BarrasRepetidos == 1 && ProductoRepo.PorCodigo("115") != null && ProductoRepo.PorCodigo("115").Codigo2 == "" && ProductoRepo.PorCodigo("7509876543210").Descripcion == "COSA");

// un producto que falla no tira a los demás
Limpiar(); uid = admin().Id;
Db.Exec("CREATE TRIGGER t_malo BEFORE INSERT ON productos WHEN NEW.descripcion='ALPISTE' BEGIN SELECT RAISE(ABORT, 'producto prohibido'); END");
var re = ImportarProductosZ.Aplicar(Traducir(TablaZ()), new OpcionesAplicarZ(), uid);
Revisar("si un producto falla, se anota el error y los demás SÍ se importan", re.Errores.Count == 1 && re.Errores[0].Contains("ALPISTE") && re.Nuevos == 3 && ProductoRepo.PorCodigo("110") == null);

// ================================================================ 4. Cobro, corte y dinero exacto
Seccion("Cobro, cancelación, corte y dinero exacto");
Limpiar(); uid = admin().Id;
ProductoRepo.Guardar(new Producto { Codigo = "1", Descripcion = "PIEZA", Precio = 12.5m, Costo = 8m, Existencia = 10m }, uid);
ProductoRepo.Guardar(new Producto { Codigo = "2", Descripcion = "GRANEL", Precio = 33.33m, Costo = 20m, Granel = true, Existencia = 5m }, uid);
var turno = TurnoRepo.Abrir(1, uid, 500m);
var a = ProductoRepo.PorCodigo("1"); var b = ProductoRepo.PorCodigo("2");
var venta = new Venta { TurnoId = turno.Id, Caja = 1, FormaPago = "EFECTIVO", PagoCon = 100m };
venta.Lineas.Add(new LineaVenta { ProductoId = a.Id, Codigo = "1", Descripcion = a.Descripcion, Cantidad = 2m, PrecioLista = 12.5m, Precio = 12.5m, Costo = 8m });
venta.Lineas.Add(new LineaVenta { ProductoId = b.Id, Codigo = "2", Descripcion = b.Descripcion, Cantidad = 0.750m, PrecioLista = 33.33m, Precio = 33.33m, Costo = 20m, Granel = true });
venta.Articulos = 2.75m; venta.Subtotal = venta.Lineas.Sum(l => l.Importe); venta.Total = venta.Subtotal; venta.Cambio = 100m - venta.Total;
long ticket = VentaRepo.Guardar(venta, uid);
Revisar("0.750 kg × $33.33 = $24.9975 se redondea a $25.00 y la venta suma $50.00", venta.Lineas[1].Importe == 25.00m && venta.Total == 50.00m);
Revisar("la venta descuenta la existencia (10 → 8 y 5 → 4.250) y deja su kardex", ProductoRepo.Obtener(a.Id).Existencia == 8m && ProductoRepo.Obtener(b.Id).Existencia == 4.250m && Cuenta("SELECT COUNT(*) FROM movimientos_inv WHERE tipo='VENTA'") == 2);
var corte = TurnoRepo.Resumen(turno.Id);
Revisar("corte: efectivo esperado = fondo 500 + ventas 50 = 550.00 exacto", corte.VentasEfectivo == 50m && corte.EfectivoEsperado == 550m && corte.NumVentas == 1);
TurnoRepo.Movimiento(turno.Id, uid, "SALIDA", 20.25m, "gasto");
TurnoRepo.Movimiento(turno.Id, uid, "ENTRADA", 100.10m, "cambio");
Revisar("entradas y salidas de efectivo entran al esperado: 550 − 20.25 + 100.10 = 629.85", TurnoRepo.Resumen(turno.Id).EfectivoEsperado == 629.85m);
VentaRepo.Cancelar(ticket, turno.Id, uid);
Revisar("cancelar el ticket devuelve la existencia y lo sale del neto", ProductoRepo.Obtener(a.Id).Existencia == 10m && ProductoRepo.Obtener(b.Id).Existencia == 5m && TurnoRepo.Resumen(turno.Id).NumCanceladas == 1 && TurnoRepo.Resumen(turno.Id).VentasEfectivo == 0m);
// 300 ventas de $0.10: la suma debe ser EXACTAMENTE $30.00 (con double puro saldría 29.999999…)
for (int i = 0; i < 300; i++)
{
    var v = new Venta { TurnoId = turno.Id, Caja = 1, FormaPago = "EFECTIVO", PagoCon = 1m, Articulos = 1m };
    v.Lineas.Add(new LineaVenta { ProductoId = 0, Codigo = "X", Descripcion = "ARTICULO VARIOS", Cantidad = 1m, PrecioLista = 0.10m, Precio = 0.10m });
    v.Subtotal = 0.10m; v.Total = 0.10m; v.Cambio = 0.90m;
    VentaRepo.Guardar(v, uid);
}
var c2 = TurnoRepo.Resumen(turno.Id);
Revisar("300 ventas de $0.10 suman EXACTAMENTE $30.00 en el corte (sin ruido de decimales)", c2.VentasEfectivo == 30.00m && c2.EfectivoEsperado == 500m + 30m + 100.10m - 20.25m, c2.VentasEfectivo.ToString("R"));
Revisar("la suma por SQL sobre los renglones también es exacta", Db.Dec(Db.Scalar("SELECT SUM(total) FROM ventas WHERE estado='PAGADA'")) == 30.00m);

// ================================================================ 5. Conteo físico
Seccion("Conteo físico (ajuste en bloque)");
int cambiados = ProductoRepo.AjustarVarios(new List<KeyValuePair<long, decimal>> { new KeyValuePair<long, decimal>(a.Id, 7m), new KeyValuePair<long, decimal>(b.Id, 5m) }, "Conteo físico 22/11/2026", uid);
Revisar("solo cambian los que tienen diferencia (1 de 2) y queda en el kardex", cambiados == 1 && ProductoRepo.Obtener(a.Id).Existencia == 7m && Cuenta("SELECT COUNT(*) FROM movimientos_inv WHERE tipo='AJUSTE' AND referencia='Conteo físico 22/11/2026'") == 1);

// ================================================================ 6. Respaldos, daño y recuperación
Seccion("Respaldos, base dañada y recuperación");
long productosAntes = Cuenta("SELECT COUNT(*) FROM productos"), ventasAntes = Cuenta("SELECT COUNT(*) FROM ventas");
string respaldo = Db.Respaldar();
Revisar("el respaldo se puede leer y tiene los mismos datos", File.Exists(respaldo) && Salud.Revisar(respaldo) == "ok");
Respaldo.Pedir(); Respaldo.Final();
string ultimo = Path.Combine(Db.Carpeta, "respaldos", "ultimo.db");
Revisar("la copia continua (ultimo.db) se escribe y se puede leer", File.Exists(ultimo) && Salud.Revisar(ultimo) == "ok");
string externa = Path.Combine(Path.GetTempPath(), "vf_resp_" + Guid.NewGuid().ToString("N"));
var cfg = Config.Actual.Copia(); cfg.CarpetaRespaldo = externa; cfg.Guardar(); Config.Cargar();
Respaldo.Pedir(); Respaldo.Final();
Revisar("la copia continua también llega a la carpeta externa elegida (escritorio/USB)", File.Exists(Path.Combine(externa, "VentaFacil_ultimo.db")));
Directory.Delete(externa, true);

// Se daña la base viva (bytes al azar en medio del archivo)
System.Data.SQLite.SQLiteConnection.ClearAllPools();
using (var fs = new FileStream(Db.Archivo, FileMode.Open, FileAccess.ReadWrite)) { var azar = new byte[3000]; new Random(5).NextBytes(azar); fs.Seek(4096, SeekOrigin.Begin); fs.Write(azar, 0, azar.Length); }
Revisar("una base con bytes dañados NO pasa la revisión", Salud.Revisar(Db.Archivo) != "ok");
bool lanzo = false; try { Db.Inicializar(); } catch (BaseDanadaException) { lanzo = true; }
Revisar("al abrir el programa con la base dañada se levanta BaseDanadaException (y no se usa)", lanzo);
var buenos = Salud.RespaldosBuenos();
Revisar("hay respaldos buenos y ninguno es la base dañada", buenos.Count >= 1 && buenos.All(x => Salud.Revisar(x) == "ok") && buenos.All(x => !Path.GetFileName(x).StartsWith("danada_")));
string guardada = Salud.Restaurar(buenos[0]);
Revisar("restaurar deja una base sana", Salud.Revisar(Db.Archivo) == "ok");
Revisar("la base dañada se guardó aparte (danada_*.db), no se borró", File.Exists(guardada) && Path.GetFileName(guardada).StartsWith("danada_"));
Revisar("después de restaurar están los mismos productos y ventas", Cuenta("SELECT COUNT(*) FROM productos") == productosAntes && Cuenta("SELECT COUNT(*) FROM ventas") == ventasAntes);
Db.Inicializar();
Revisar("y el programa vuelve a abrir la base normalmente", Cuenta("SELECT COUNT(*) FROM productos") == productosAntes);
Revisar("las danadas_* no cuentan como respaldo bueno aunque estén en la misma carpeta", !Salud.RespaldosBuenos().Any(x => Path.GetFileName(x).StartsWith("danada_")));

SQLiteBorrar();
Console.WriteLine(fallas == 0 ? "\nTodas las pruebas de integración pasaron." : $"\n{fallas} prueba(s) fallaron.");
return fallas == 0 ? 0 : 1;
