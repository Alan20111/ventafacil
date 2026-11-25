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
void QuitarDisparadores() { foreach (var t in Db.Lista("SELECT name FROM sqlite_master WHERE type='trigger'", r => Db.S(r, "name"))) Db.Exec("DROP TRIGGER " + t); }
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
Revisar("la base nueva se crea, es de la versión 6 y pasa la revisión de integridad", Cuenta("PRAGMA user_version") == 6 && Salud.Revisar(Db.Archivo) == "ok");
Revisar("existen las 10 tablas del programa", Cuenta("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('config','usuarios','departamentos','productos','turnos','ventas','venta_detalle','movimientos_caja','movimientos_inv','suspendidas')") == 10);
Revisar("el usuario admin/admin existe y entra; una clave mala no", admin() != null && UsuarioRepo.Validar("admin", "mala") == null);
Revisar("la columna sin_barras (v3) existe", Cuenta("SELECT COUNT(*) FROM pragma_table_info('productos') WHERE name='sin_barras'") == 1);
// Una base de la versión 2 (sin sin_barras) se actualiza sola y conserva sus datos
ProductoRepo.Guardar(new Producto { Codigo = "1", Descripcion = "VIEJO", Precio = 10m, Costo = 6m }, admin().Id);
QuitarDisparadores(); Db.Exec("ALTER TABLE productos DROP COLUMN sin_barras"); Db.Exec("PRAGMA user_version = 2");
Db.Inicializar();
Revisar("migración v2 → v6: agrega la columna y no pierde el producto", Cuenta("PRAGMA user_version") == 6 && Cuenta("SELECT COUNT(*) FROM pragma_table_info('productos') WHERE name='sin_barras'") == 1 && ProductoRepo.PorCodigo("1") != null);

// Una base de la versión 3 (la de la v1.17, con usuarios ya creados) pasa a la 4 sin que nadie gane ni pierda acceso
UsuarioRepo.Guardar(new Usuario { Login = "caja1", Nombre = "Cajera Uno", Rol = "CAJERO" }, "1234");
Db.Exec("ALTER TABLE usuarios DROP COLUMN permisos"); Db.Exec("PRAGMA user_version = 3");
Db.Inicializar(); Db.Inicializar();   // dos veces: no debe fallar al volver a abrir
Revisar("migración v3 → v6: agrega permisos, conserva usuarios y claves", Cuenta("PRAGMA user_version") == 6 && admin() != null && UsuarioRepo.Validar("caja1", "1234") != null);

Seccion("Permisos por usuario");
{
    var adm = admin(); var caj = UsuarioRepo.Validar("caja1", "1234");
    Func<Usuario, string, bool> P = (u, k) => Permisos.Tiene(u, k, false);
    Revisar("el administrador puede todo, incluso lo exclusivo", Permisos.Todos.All(i => P(adm, i.Clave)) && P(adm, Permisos.SoloAdmin));
    Revisar("el cajero de fábrica: solo Turno, entradas/salidas, cajón y recibir envíos", string.Join(",", Permisos.Todos.Where(i => P(caj, i.Clave)).Select(i => i.Clave)) == "turno,movimientos,cajon,envios");
    Revisar("el cajero no tiene corte de caja, corte Z ni lo exclusivo de administrador", !P(caj, Permisos.CorteCaja) && !P(caj, Permisos.CorteZ) && !P(caj, Permisos.SoloAdmin));
    Revisar("el ajuste general da cancelar y descuentos al cajero de fábrica, y nada más", Permisos.Tiene(caj, Permisos.Cancelar, true) && Permisos.Tiene(caj, Permisos.Descuentos, true) && !Permisos.Tiene(caj, Permisos.Catalogo, true));
    caj.Permisos = Permisos.Guardar(new[] { Permisos.Turno, Permisos.Almacen }, false); UsuarioRepo.Guardar(caj, null);
    caj = UsuarioRepo.Obtener(caj.Id);
    Revisar("las casillas propias se guardan y mandan (dan Almacén, quitan el cajón)", P(caj, Permisos.Almacen) && P(caj, Permisos.Turno) && !P(caj, Permisos.Cajon) && !P(caj, Permisos.Catalogo));
    Revisar("con casillas propias, el ajuste general ya no da cancelar", !Permisos.Tiene(caj, Permisos.Cancelar, true));
    Revisar("guardar permisos no cambia la contraseña", UsuarioRepo.Validar("caja1", "1234") != null);
    caj.Permisos = Permisos.Guardar(new string[0], false); UsuarioRepo.Guardar(caj, null); caj = UsuarioRepo.Obtener(caj.Id);
    Revisar("sin ninguna casilla no puede nada (no vuelve a lo de fábrica)", caj.Permisos == "=" && Permisos.Todos.All(i => !P(caj, i.Clave)));
    Revisar("marcar justo lo de fábrica se guarda como «de fábrica»", Permisos.Guardar(new[] { Permisos.Cajon, Permisos.Turno, Permisos.Movimientos, Permisos.Envios }, false) == "");
    Revisar("todas las casillas no dan lo exclusivo de administrador, ni escribiéndolo a mano", !P(new Usuario { Permisos = Permisos.Guardar(Permisos.Todos.Select(i => i.Clave).ToList(), false) }, Permisos.SoloAdmin) && !P(new Usuario { Permisos = "=solo_admin" }, Permisos.SoloAdmin));
    var todas = Permisos.Todos.Select(i => i.Clave).ToList();
    Func<Usuario, bool, string> Pf = (u, c) => Permisos.PerfilDe(u, c);
    Revisar("perfil Encargado: todas las casillas; puede todo menos lo exclusivo de administrador", Permisos.Guardar(todas, false) == "*" && Pf(new Usuario { Permisos = "*" }, false) == "Encargado" && todas.All(k => P(new Usuario { Permisos = "*" }, k)) && !P(new Usuario { Permisos = "*" }, Permisos.SoloAdmin) && !P(new Usuario { Permisos = "*" }, "otra"));
    Revisar("los perfiles se deducen: Cajero, Personalizado y Administrador", Pf(new Usuario(), false) == "Cajero" && Pf(new Usuario { Permisos = "=turno" }, false) == "Personalizado" && Pf(new Usuario { Permisos = "=" }, false) == "Personalizado" && Pf(new Usuario { Rol = "ADMIN" }, false) == "Administrador" && Pf(new Usuario { Activo = false }, false) == "Cajero");
    Revisar("cada perfil vuelve a ser el mismo al guardarlo, con y sin el ajuste general", new[] { false, true }.All(c => new[] { "Cajero", "Encargado" }.All(n => Permisos.PerfilDe(Permisos.DePerfil(n, c), c) == n && Pf(new Usuario { Permisos = Permisos.Guardar(Permisos.DePerfil(n, c), c) }, c) == n)) && Permisos.DePerfil("Personalizado", false) == null);
    Revisar("con el ajuste general, el Cajero sigue viéndose Cajero y conserva cancelar y descuentos", Pf(new Usuario(), true) == "Cajero" && Permisos.Tiene(new Usuario(), Permisos.Cancelar, true));
    Revisar("lo guardado antes no cambia: lista completa = mismos permisos (se ve Encargado); lista de la v1.18 sin envíos sigue sin ellos", Pf(new Usuario { Permisos = "=" + string.Join(",", todas) }, false) == "Encargado" && !P(new Usuario { Permisos = "=turno,catalogo,almacen,resultados,ajustes,movimientos,cajon,descuentos,cancelar,corte_caja,corte_z" }, Permisos.Envios));
    Revisar("todas las combinaciones de casillas se guardan y se leen igual (4096 × 2)", Enumerable.Range(0, 1 << todas.Count).All(n => new[] { false, true }.All(c => { var m = todas.Where((k, i) => (n >> i & 1) == 1).ToList(); var u = new Usuario { Permisos = Permisos.Guardar(m, c) }; return todas.All(k => Permisos.Tiene(u, k, c) == m.Contains(k)); })));
    Revisar("un nombre parecido no cuenta (corte_zz, turnos)", !P(new Usuario { Permisos = "=corte_zz,turnos" }, Permisos.CorteZ) && !P(new Usuario { Permisos = "=corte_zz,turnos" }, Permisos.Turno));
    Revisar("un usuario inactivo o nulo no puede nada", !P(new Usuario { Rol = "ADMIN", Activo = false }, Permisos.Turno) && !P(null, Permisos.Turno));
}

Seccion("Asistente: cuándo sale");
{
    Limpiar();
    Revisar("base recién creada: es instalación nueva", PrimeraVez.EsInstalacionNueva() && PrimeraVez.ClaveDeFabrica());
    Revisar("y volver a abrir sin terminar sigue siendo nueva", Con3(() => { Db.Inicializar(); Config.Cargar(); return PrimeraVez.EsInstalacionNueva(); }));
    Revisar("la revisión dice lo que falta (contraseña, negocio, respaldos, cajera, impresora y productos)", PrimeraVez.Revision().Count == 6 && PrimeraVez.Revision().All(x => !x.Bien));
    Revisar("como oficina no pide cajera, impresora ni productos, pero sí enlazarla", PrimeraVez.Revision(true).Count == 4 && PrimeraVez.Revision(true)[3].Texto.Contains("enlazar"));
    var mal = new[] { new[] { "", "A", "1234", "1234" }, new[] { "a", " ", "1234", "1234" }, new[] { "a", "A", "123", "123" }, new[] { "x", "A", "Admin", "Admin" }, new[] { "dueno", "A", "DUENO", "DUENO" }, new[] { "a", "A", "1234", "12345" } };
    Revisar("no acepta cuentas malas: vacías, clave corta, «admin», igual al usuario o que no coincide", mal.All(m => PrimeraVez.RevisarCuenta(m[0], m[1], m[2], m[3]) != null) && PrimeraVez.RevisarCuenta("alan", "Alan", "semilla9", "semilla9") == null);
    var u0 = admin(); u0.Login = "alan"; u0.Nombre = "Alan"; UsuarioRepo.Guardar(u0, "semilla9");
    Revisar("al crear la cuenta, admin/admin deja de servir, la nueva entra y sigue siendo administrador", admin() == null && !PrimeraVez.ClaveDeFabrica() && UsuarioRepo.Validar("alan", "semilla9").EsAdmin && UsuarioRepo.AdminsActivos() == 1);
    Revisar("con la cuenta hecha ya no es instalación nueva", !PrimeraVez.EsInstalacionNueva());
    var ad = UsuarioRepo.Validar("alan", "semilla9"); var cj = new Usuario { Rol = "CAJERO" };
    PrimeraVez.MarcarVista("1.23"); Config.Cargar();
    Revisar("misma versión: no pregunta; al actualizar pregunta al administrador y no al cajero ni a nadie sin sesión", !PrimeraVez.HayQuePreguntar("1.23", ad) && PrimeraVez.HayQuePreguntar("1.24", ad) && !PrimeraVez.HayQuePreguntar("1.24", cj) && !PrimeraVez.HayQuePreguntar("1.24", null));
    PrimeraVez.MarcarVista("1.24"); Config.Cargar();
    Revisar("«No configurar» queda guardado y no vuelve a preguntar", !PrimeraVez.HayQuePreguntar("1.24", ad));
    // Tiendas que ya existían (sin el dato de versión): nunca se tratan como nuevas
    Limpiar(); ProductoRepo.Guardar(new Producto { Codigo = "1", Descripcion = "X", Precio = 1m }, admin().Id);
    Revisar("una base con productos y admin/admin NO es nueva, pero sí se pregunta al actualizar", !PrimeraVez.EsInstalacionNueva() && PrimeraVez.HayQuePreguntar("1.23", admin()));
    Limpiar(); UsuarioRepo.Guardar(new Usuario { Login = "caja", Nombre = "Caja" }, "1234");
    Revisar("una base con otro usuario NO es nueva", !PrimeraVez.EsInstalacionNueva());
    Limpiar(); TurnoRepo.Abrir(1, admin().Id, 0); { var v = new Venta { TurnoId = 1, Caja = 1, Articulos = 1, Total = 1 }; v.Lineas.Add(new LineaVenta { Descripcion = "V", Cantidad = 1, Precio = 1 }); VentaRepo.Guardar(v, admin().Id); }
    Revisar("una base con ventas NO es nueva", !PrimeraVez.EsInstalacionNueva());
}
bool Con3(Func<bool> f) => f();

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

// ================================================================ 7. Envíos de la oficina a la tienda (dos bases: se intercambian)
string maquina = "tienda", usb = Path.Combine(Path.GetTempPath(), "vf_usb_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(usb);
string De(string m) => Path.Combine(Db.Carpeta, "maq_" + m + ".db");
void En(string m)      // «sentarse» en la otra computadora
{
    if (m == maquina) return;
    System.Data.SQLite.SQLiteConnection.ClearAllPools();
    foreach (var x in new[] { "-wal", "-shm" }) if (File.Exists(Db.Archivo + x)) throw new Exception("quedó " + x + " sin cerrar");
    File.Move(Db.Archivo, De(maquina), true);
    if (File.Exists(De(m))) File.Move(De(m), Db.Archivo);
    maquina = m;
    Db.Inicializar(); Config.Cargar();
}
string Aparte(string f) { string d = Path.Combine(usb, Guid.NewGuid().ToString("N") + ".db"); File.Copy(f, d); return d; }
string Falla(Action x) { try { x(); return ""; } catch (Exception e) { return e.Message; } }
string Foto() => string.Join("|", Db.Lista("SELECT codigo,codigo2,descripcion,precio,costo,existencia,activo,uid FROM productos ORDER BY id", r => string.Join(",", Enumerable.Range(0, r.FieldCount).Select(k => Convert.ToString(r.GetValue(k))))))
    + "#" + Cuenta("SELECT COUNT(*) FROM movimientos_inv") + "#" + Cuenta("SELECT COUNT(*) FROM recibidos") + "#" + Cuenta("SELECT COUNT(*) FROM envios_aplicados") + "#" + Cuenta("SELECT COUNT(*) FROM departamentos");
string Enviar() { En("oficina"); var ruta = Envios.Crear(usb, "Administrador"); En("tienda"); return ruta; }
ResultadoEnvio Aplica(string ruta) { En("tienda"); return Envios.Aplicar(ruta, admin().Id, "Cajera"); }
void Acuse() { En("tienda"); string ac = Envios.EscribirAcuse(usb); En("oficina"); Envios.LeerAcuse(ac); }
void Precio(string cod, decimal v) { var p = ProductoRepo.PorCodigo(cod); p.Precio = v; ProductoRepo.Guardar(p, admin().Id); }
void Mercancia(string cod, decimal q, decimal? costo = null) => Envios.RegistrarMercancia(new List<RenglonMercancia> { new RenglonMercancia { ProductoId = ProductoRepo.PorCodigo(cod).Id, Cantidad = q, Costo = costo } }, "Factura 1");
void Vender(string cod, decimal q) { var p = ProductoRepo.PorCodigo(cod); var v = new Venta { TurnoId = TurnoRepo.Abierto(1)?.Id ?? TurnoRepo.Abrir(1, admin().Id, 0).Id, Caja = 1, Articulos = q }; v.Lineas.Add(new LineaVenta { ProductoId = p.Id, Codigo = cod, Descripcion = p.Descripcion, Cantidad = q, Precio = p.Precio, PrecioLista = p.Precio }); v.Subtotal = v.Total = v.Lineas[0].Importe; v.PagoCon = v.Total; VentaRepo.Guardar(v, admin().Id); }
Func<string, decimal> Hay = c => ProductoRepo.PorCodigo(c).Existencia;

void Enlazar()
{
    foreach (var f in Directory.GetFiles(Db.Carpeta, "maq_*.db")) File.Delete(f);
    foreach (var f in Directory.GetFiles(usb)) File.Delete(f);
    maquina = "tienda"; Limpiar();
    var u = admin().Id;
    foreach (var (c, n, p, k) in new[] { ("1", "GIRASOL PELON", 92m, 66.4m), ("2", "CALABAZA PEPITA", 206m, 150m), ("3", "CHIA", 66m, 48m) })
        ProductoRepo.Guardar(new Producto { Codigo = c, Descripcion = n, Precio = p, Costo = k, Existencia = 20m, Granel = true }, u);
    Envios.CrearEnlace(Path.Combine(usb, "t.vfenlace"), "secreta1", "SEMILLAS Y BOTANAS");
    En("oficina");
    Envios.ConvertirEnOficina(Path.Combine(usb, "t.vfenlace"), "secreta1");
}

Seccion("Envíos: enlace entre oficina y tienda");
Limpiar();
ProductoRepo.Guardar(new Producto { Codigo = "1", Descripcion = "X", Precio = 1m }, admin().Id);
Revisar("cada producto nace con su identificador único", Cuenta("SELECT COUNT(*) FROM productos WHERE length(uid)=32") == 1);
Precio("1", 2m);
Revisar("una caja normal no anota cambios", Cuenta("SELECT COUNT(*) FROM cambios") == 0);
Db.Exec("UPDATE productos SET uid=''"); Db.Exec("PRAGMA user_version = 4"); Db.Inicializar(); Db.Inicializar();
Revisar("migración v4 → v6: pone identificador a los productos que ya había", Cuenta("SELECT COUNT(*) FROM productos WHERE length(uid)=32") == 1 && Cuenta("PRAGMA user_version") == 6);
Enlazar();
Revisar("la oficina toma el catálogo de la tienda, con los mismos identificadores y existencia en cero", Cuenta("SELECT COUNT(*) FROM productos") == 3 && Hay("1") == 0m && ProductoRepo.PorCodigo("1").Precio == 92m && Envios.EsOficina);
Revisar("copiar el catálogo no cuenta como cambios", Cuenta("SELECT COUNT(*) FROM cambios") == 0);
Revisar("sin cambios no se crea envío", Falla(() => Envios.Crear(usb, "A")).Contains("No hay cambios"));
Revisar("con otra contraseña el enlace no se abre", Falla(() => Envios.ConvertirEnOficina(Path.Combine(usb, "t.vfenlace"), "otra-clave")).Contains("contraseña"));
Revisar("el archivo de enlace no deja leer costos ni nombres", !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(usb, "t.vfenlace"))).Contains("GIRASOL"));
En("tienda");
Revisar("la llave que firma no está en la tienda", Envios.Dato("privada") == "");
Vender("1", 1m);
Revisar("una base con ventas no se puede convertir en oficina", Falla(() => Envios.ConvertirEnOficina(Path.Combine(usb, "t.vfenlace"), "secreta1")).Contains("ventas"));

Seccion("Envíos: lo normal");
Enlazar();
Precio("1", 95m); Precio("1", 98m); Mercancia("1", 50m); Mercancia("2", 30m, 160m);
ProductoRepo.Guardar(new Producto { Codigo = "50", Descripcion = "AMARANTO", Precio = 40m, Costo = 20m, Existencia = 7m, Granel = true, DepartamentoId = DeptoNuevo("Cereales") }, admin().Id);
ProductoRepo.Eliminar(ProductoRepo.PorCodigo("3").Id);
long DeptoNuevo(string n) { DepartamentoRepo.Guardar(new Departamento { Nombre = n }); return Cuenta("SELECT id FROM departamentos WHERE nombre=@p0", n); }
var vp = Envios.VistaPendientes();
Revisar("la oficina ve su resumen: 1 precio, 1 costo, 3 entradas, 1 nuevo, 1 baja", vp.Resumen == "1 precio  ·  1 costo  ·  3 entradas de mercancía  ·  1 nuevo  ·  1 dado de baja", vp.Resumen);
Revisar("«Llegó mercancía» no cambia la existencia de la oficina", Hay("1") == 0m);
string e1 = Enviar();
Revisar("el archivo va cifrado (no se leen nombres ni costos)", !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(e1)).Contains("AMARANTO") && Path.GetFileName(e1).StartsWith("ENVIO_SEMILLAS-Y-BOTANAS_0001_"), Path.GetFileName(e1));
Vender("1", 8m);     // la tienda vendió mientras tanto: 20 → 12
string foto = Foto();
var nov = Envios.Novedad(e1);
Revisar("aviso de USB: dice el número de envío y cuántos cambios nuevos trae, sin cambiar nada", nov != null && nov.Num == 1 && nov.Cambios > 5 && Foto() == foto);
var rv = Envios.Revisar(e1);
Revisar("revisar el envío muestra lo mismo que la oficina y NO cambia nada", rv.Resumen == vp.Resumen && Foto() == foto && rv.PrimerEnvio && Envios.Dato("oficina_pub") == "", rv.Resumen);
var ra = Aplica(e1);
Revisar("la mercancía se SUMA a lo que hay: 12 + 50 = 62", Hay("1") == 62m);
Revisar("el precio queda en el último (98), el costo en 160 y entra la mercancía (20 + 30)", ProductoRepo.PorCodigo("1").Precio == 98m && ProductoRepo.PorCodigo("2").Costo == 160m && Hay("2") == 50m);
var am = ProductoRepo.PorCodigo("50");
Revisar("el producto nuevo llega completo, con su grupo y su existencia inicial", am != null && am.Existencia == 7m && am.Precio == 40m && am.Granel && ProductoRepo.Obtener(am.Id).Departamento == "Cereales");
Revisar("el eliminado queda desactivado, no borrado", ProductoRepo.PorCodigo("3") == null && Cuenta("SELECT COUNT(*) FROM productos WHERE codigo='3' AND activo=0") == 1);
Revisar("cada entrada queda en el kardex con el número de envío", Cuenta("SELECT COUNT(*) FROM movimientos_inv WHERE tipo='ENTRADA' AND referencia='Envío 1: Factura 1'") == 2);
Revisar("las ventas de la tienda no se tocan y hay respaldo previo sano", Cuenta("SELECT COUNT(*) FROM ventas") == 1 && Salud.Revisar(ra.Respaldo) == "ok");
Revisar("la tienda no anota cambios propios al aplicar", Cuenta("SELECT COUNT(*) FROM cambios") == 0);

Seccion("Envíos: repetido, perdido y en desorden");
Revisar("aviso de USB: un envío ya aplicado no avisa", Envios.Novedad(e1) == null);
foto = Foto();
Revisar("el mismo envío otra vez no hace nada", Aplica(e1).YaAplicado && Foto() == foto);
En("oficina"); Mercancia("1", 5m); string perdido = Enviar();   // esta USB se pierde
En("oficina"); Mercancia("1", 3m); Precio("2", 210m); string e3 = Enviar();
var r3e = Aplica(e3);
Revisar("un envío perdido lo recupera el siguiente: 62 + 5 + 3 = 70", Hay("1") == 70m && ProductoRepo.PorCodigo("2").Precio == 210m);
foto = Foto();
Revisar("el perdido que aparece después ya no hace nada", Aplica(perdido).YaAplicado && Foto() == foto);
Acuse();
long sc, ya; Envios.ContarPendientes(out sc, out ya);
Revisar("con el acuse la oficina sabe que la tienda está al día y que se aplicaron el 1 y el 3", sc == 0 && Envios.Hechos().Where(h => h.Estado == "APLICADO").Select(h => h.Num).OrderBy(n => n).SequenceEqual(new long[] { 1, 3 }));
Precio("2", 211m); string e4 = Enviar();
En("oficina"); Precio("2", 212m); string e5 = Enviar();
En("tienda"); Revisar("aviso de USB: con dos envíos, el que llega más lejos es el último", Envios.Novedad(e5).Hasta > Envios.Novedad(e4).Hasta);
Aplica(e5); foto = Foto();
Revisar("aplicar el 5 y luego el 4 (viejo) no regresa el precio", Aplica(e4).YaAplicado && ProductoRepo.PorCodigo("2").Precio == 212m && Foto() == foto);
En("tienda"); string respTienda = Aparte(Db.Respaldar());
En("oficina"); Mercancia("2", 10m); string e6 = Enviar(); Aplica(e6); decimal conE6 = Hay("2"); Acuse();
En("oficina"); Mercancia("2", 1m); string e7 = Enviar();
En("tienda"); System.Data.SQLite.SQLiteConnection.ClearAllPools(); Salud.Restaurar(respTienda); Db.Inicializar();   // la tienda regresa a un respaldo viejo
foto = Foto();
Revisar("si a la tienda le falta un envío (restauró un respaldo) se niega y no cambia nada", Falla(() => Aplica(e7)).Contains("Falta un envío anterior") && Foto() == foto);
Acuse(); string e8 = Enviar(); Aplica(e8);
Revisar("con el acuse, la oficina vuelve a mandar lo que falta y queda igual que antes más lo nuevo", Hay("2") == conE6 + 1m, Hay("2") + " vs " + (conE6 + 1m));
En("oficina"); Envios.Reenviar(Envios.Hechos().First(h => h.Num == 1).Id); string todoOtraVez = Enviar(); foto = Foto();
Revisar("«Reenviar» todo desde el principio no duplica nada", Aplica(todoOtraVez).YaAplicado && Foto() == foto);

Seccion("Envíos: alterado, ajeno o incompleto");
Enlazar(); Precio("1", 99m); string ok1 = Enviar();
byte[] bytes = File.ReadAllBytes(ok1); foto = Foto(); int aceptados = 0;
for (int i = 0; i < bytes.Length; i += 7) { var m = (byte[])bytes.Clone(); m[i] ^= 1; var ruta = Path.Combine(usb, "mal.vfenvio"); File.WriteAllBytes(ruta, m); if (Falla(() => Aplica(ruta)) == "") aceptados++; }
Revisar("cambiar un solo bit en cualquier parte lo invalida (" + (bytes.Length / 7 + 1) + " intentos)", aceptados == 0 && Foto() == foto, aceptados + " aceptados");
aceptados = 0;
for (int i = 0; i < bytes.Length; i += 41) { var ruta = Path.Combine(usb, "mal.vfenvio"); File.WriteAllBytes(ruta, bytes.Take(i).ToArray()); if (Falla(() => Aplica(ruta)) == "") aceptados++; }
Revisar("un archivo cortado en cualquier punto se rechaza", aceptados == 0 && Foto() == foto, aceptados + " aceptados");
File.WriteAllBytes(Path.Combine(usb, "mas.vfenvio"), bytes.Concat(new byte[] { 0 }).ToArray());
Revisar("con un byte de más también se rechaza", Falla(() => Aplica(Path.Combine(usb, "mas.vfenvio"))) != "" && Foto() == foto);
Aplica(ok1);
// Alguien con acceso a la PC de la caja tiene la clave compartida, pero no la llave que firma
string falso = Path.Combine(usb, "falso.vfenvio");
var texto = EnvioArchivo.Abrir(bytes, EnvioArchivo.Envio, Convert.FromBase64String(Envios.Dato("clave"))).Replace("\"99\"", "\"1\"");
File.WriteAllBytes(falso, EnvioArchivo.Cerrar(EnvioArchivo.Envio, Envios.Dato("tienda_id"), texto, Convert.FromBase64String(Envios.Dato("clave")), EnvioArchivo.NuevaLlavePrivada()));
foto = Foto();
Revisar("un envío fabricado con la clave de la caja pero otra firma se rechaza", Falla(() => Aplica(falso)).Contains("firmado") && Foto() == foto);
byte[] deOtra = (byte[])bytes.Clone(); for (int i = 4; i < 36; i++) deOtra[i] = (byte)'a'; File.WriteAllBytes(falso, deOtra);
Revisar("un envío de otra tienda se rechaza y lo dice", Falla(() => Aplica(falso)).Contains("otra tienda") && Foto() == foto);
File.WriteAllText(falso, "hola"); Revisar("un archivo cualquiera se rechaza con un mensaje claro", Falla(() => Aplica(falso)).Contains("no es un envío"));
Revisar("aviso de USB: ni archivos raros, ni de otra tienda, ni alterados avisan (y no truenan)", Envios.Novedad(falso) == null && Envios.Novedad(Path.Combine(usb, "mal.vfenvio")) == null && Envios.Novedad(Path.Combine(usb, "no-esta.vfenvio")) == null);
{ string ac = Envios.EscribirAcuse(usb); En("oficina"); bool antesA = Envios.AcuseNuevo(ac); Envios.LeerAcuse(ac); Revisar("aviso de USB en la oficina: un acuse avisa una sola vez", antesA && !Envios.AcuseNuevo(ac) && !Envios.AcuseNuevo(falso) && Envios.Novedad(ok1) == null); }
En("oficina"); Revisar("la oficina no puede aplicarse envíos", Falla(() => Envios.Revisar(ok1)).Contains("oficina"));
string respOfi = Aparte(Db.Respaldar()); Precio("1", 100m); string e2b = Enviar(); Aplica(e2b);
En("oficina"); System.Data.SQLite.SQLiteConnection.ClearAllPools(); Salud.Restaurar(respOfi); Db.Inicializar(); Precio("2", 1m); string torcido = Enviar(); foto = Foto();
Revisar("si la oficina se restauró y sus números ya no coinciden, se rechaza sin cambiar nada", Falla(() => Aplica(torcido)).Contains("no coincide") && Foto() == foto);
string acT = Envios.EscribirAcuse(usb); En("oficina");
Revisar("y la oficina también lo detecta al leer el acuse", Falla(() => Envios.LeerAcuse(acT)).Contains("volver a enlazarla"));

Seccion("Envíos: interrumpido a la mitad");
Enlazar(); Precio("1", 91m); Precio("2", 201m); Mercancia("1", 5m); Mercancia("3", 5m); string ei = Enviar();
Db.Exec("CREATE TRIGGER corte BEFORE INSERT ON movimientos_inv WHEN NEW.producto_id=(SELECT id FROM productos WHERE codigo='3') BEGIN SELECT RAISE(ABORT,'se fue la luz'); END");
foto = Foto();
Revisar("si falla en el último renglón no queda nada a medias", Falla(() => Aplica(ei)).Contains("se fue la luz") && Foto() == foto);
Db.Exec("DROP TRIGGER corte");
Aplica(ei);
Revisar("y al repetirlo se aplica completo", Hay("1") == 25m && Hay("3") == 25m && ProductoRepo.PorCodigo("1").Precio == 91m);
Revisar("una carpeta que no existe (USB fuera) no deja envío registrado", Con("oficina", () => { Precio("1", 93m); return Falla(() => Envios.Crear(Path.Combine(usb, "no-existe"), "A")) != "" && Cuenta("SELECT COUNT(*) FROM envios") == 1; }));
bool Con(string m, Func<bool> f) { En(m); return f(); }

Seccion("Envíos: choques con lo que pasó en la tienda");
Enlazar();
En("tienda");
ProductoRepo.Guardar(new Producto { Codigo = "60", Descripcion = "COSA DE TIENDA", Precio = 5m, Existencia = 1m }, admin().Id);
ProductoRepo.Guardar(new Producto { Codigo = "61", Descripcion = "Ajonjolí", Precio = 5m, Existencia = 4m }, admin().Id);
Precio("1", 90m);      // la cajera cambió el precio en la tienda
En("oficina");
ProductoRepo.Guardar(new Producto { Codigo = "60", Descripcion = "OTRA COSA", Precio = 9m, Existencia = 6m }, admin().Id);
ProductoRepo.Guardar(new Producto { Codigo = "61", Descripcion = "AJONJOLI", Precio = 70m, Existencia = 2m }, admin().Id);
Precio("1", 97m); Mercancia("60", 3m);
var rch = Aplica(Enviar());
Revisar("código ocupado por otro producto activo: no se pisa y sale como PROBLEMA (alta y su mercancía)", rch.Problemas == 2 && ProductoRepo.PorCodigo("60").Descripcion == "COSA DE TIENDA" && Hay("60") == 1m && rch.Lineas.Any(l => l.Nota.Contains("no se sumaron 9")), rch.Problemas.ToString());
Revisar("el mismo producto dado de alta en los dos lados se enlaza, no se duplica (4 + 2 = 6)", Cuenta("SELECT COUNT(*) FROM productos WHERE codigo='61'") == 1 && Hay("61") == 6m && ProductoRepo.PorCodigo("61").Precio == 70m);
Revisar("precio cambiado también en la tienda: gana la oficina y queda avisado", ProductoRepo.PorCodigo("1").Precio == 97m && rch.Lineas.Any(l => l.Producto == "GIRASOL PELON" && l.Nota.Contains("se había cambiado")));
Acuse();
Revisar("la oficina ve los problemas en el acuse", Envios.Hechos()[0].Problemas.Contains("COSA DE TIENDA") && Envios.Hechos()[0].Estado == "APLICADO");
{ var p = ProductoRepo.PorCodigo("60"); p.Codigo = "62"; ProductoRepo.Guardar(p, admin().Id); Mercancia("62", 9m); }
Aplica(Enviar());
Revisar("al corregir el código en la oficina, el producto entra en el siguiente envío con su mercancía", ProductoRepo.PorCodigo("62") != null && ProductoRepo.PorCodigo("62").Descripcion == "OTRA COSA" && Hay("62") == 9m && ProductoRepo.PorCodigo("60").Descripcion == "COSA DE TIENDA");
En("oficina"); Acuse(); En("oficina");
ProductoRepo.Eliminar(ProductoRepo.PorCodigo("3").Id); ProductoRepo.Guardar(new Producto { Codigo = "3", Descripcion = "LINAZA", Precio = 30m, Existencia = 5m }, admin().Id);
var rr = Aplica(Enviar());
Revisar("reutilizar el código de un producto dado de baja funciona", rr.Problemas == 0 && ProductoRepo.PorCodigo("3").Descripcion == "LINAZA" && Hay("3") == 5m && Cuenta("SELECT COUNT(*) FROM productos WHERE descripcion='CHIA' AND activo=0") == 1);
En("oficina"); ProductoRepo.Guardar(new Producto { Codigo = "77", Descripcion = "EFIMERO", Precio = 1m, Existencia = 4m }, admin().Id); ProductoRepo.Eliminar(ProductoRepo.PorCodigo("77").Id); Precio("2", 222m);
rr = Aplica(Enviar());
Revisar("lo que se creó y se borró en la oficina antes de enviarse no deja rastro ni problemas", rr.Problemas == 0 && Cuenta("SELECT COUNT(*) FROM productos WHERE codigo='77'") == 0 && rr.Resumen == "1 precio", rr.Resumen);
En("oficina"); { var dep = DepartamentoRepo.Todos().First(d => d.Nombre == "General"); var p = ProductoRepo.PorCodigo("2"); p.DepartamentoId = dep.Id; ProductoRepo.Guardar(p, admin().Id); dep.Nombre = "Semillas"; DepartamentoRepo.Guardar(dep); }
Aplica(Enviar());
Revisar("renombrar un grupo en la oficina llega a sus productos", ProductoRepo.Obtener(ProductoRepo.PorCodigo("2").Id).Departamento == "Semillas");
En("oficina"); Mercancia("2", -3m); decimal h2 = Con2("tienda", () => Hay("2")); rr = Aplica(Enviar());
decimal Con2(string m, Func<decimal> f) { En(m); return f(); }
Revisar("una salida o corrección resta (−3)", Hay("2") == h2 - 3m && rr.Resumen == "1 salida de mercancía", rr.Resumen);

Seccion("Envíos: deshacer");
Enlazar();
Precio("1", 9.20m); Mercancia("1", 50m); Mercancia("2", 10m);
ProductoRepo.Guardar(new Producto { Codigo = "50", Descripcion = "AMARANTO", Precio = 40m, Existencia = 7m }, admin().Id);
ProductoRepo.Eliminar(ProductoRepo.PorCodigo("3").Id);
string ed = Enviar();
var rd = Envios.Revisar(ed);
Revisar("alerta de precio raro (de $92.00 a $9.20) y por debajo del costo", rd.Alertas.Count(x => x.Contains("GIRASOL")) == 2, string.Join(" / ", rd.Alertas));
Aplica(ed); Vender("1", 4m);
Precio("2", 300m);     // alguien cambia otra cosa después
long ordenD = Envios.Recibidos()[0].Orden;
var pendD = Envios.Deshacer(ordenD, admin().Id);
Revisar("deshacer regresa precio y existencias SIN borrar la venta: 20 − 4 = 16", ProductoRepo.PorCodigo("1").Precio == 92m && Hay("1") == 16m && Hay("2") == 20m && Cuenta("SELECT COUNT(*) FROM ventas") == 1 && pendD.Count == 0, string.Join(" / ", pendD));
Revisar("el nuevo se desactiva, el dado de baja vuelve y lo cambiado después se respeta", ProductoRepo.PorCodigo("50") == null && ProductoRepo.PorCodigo("3") != null && ProductoRepo.PorCodigo("2").Precio == 300m);
Revisar("no se puede deshacer dos veces", Falla(() => Envios.Deshacer(ordenD, admin().Id)).Contains("último"));
En("oficina"); Precio("1", 94m); Mercancia("1", -2m);
var rdd = Aplica(Enviar());
Revisar("el envío corregido trae todo junto y bien: 94, 16 + 48 = 64 y AMARANTO vuelve con 7", ProductoRepo.PorCodigo("1").Precio == 94m && Hay("1") == 64m && ProductoRepo.PorCodigo("50") != null && Hay("50") == 7m && ProductoRepo.PorCodigo("3") == null, Hay("1") + " " + rdd.Resumen);
En("oficina"); Precio("1", 96m); Aplica(Enviar()); Precio("1", 97m);
var pd2 = Envios.Deshacer(Envios.Recibidos()[0].Orden, admin().Id);
Revisar("si el precio se cambió después del envío, deshacer no lo pisa y lo dice", ProductoRepo.PorCodigo("1").Precio == 97m && pd2.Count == 1 && pd2[0].Contains("se cambió después"));

Seccion("Regreso de datos: la oficina ve la tienda");
Enlazar();
Revisar("sin reporte, la oficina no inventa datos", ReporteTienda.Fecha == "" && ReporteTienda.Existencias(false).Rows.Count == 0 && ReporteTienda.Diferencias().Count == 0);
Precio("1", 100m); Aplica(Enviar());
Vender("1", 3m); Vender("1", 2m); Vender("2", 1m);
long aCancelar = VentaRepo.UltimoTicket(1); VentaRepo.Cancelar(aCancelar, TurnoRepo.Abierto(1).Id, admin().Id);
{ var v = new Venta { TurnoId = TurnoRepo.Abierto(1).Id, Caja = 1, FormaPago = "TARJETA", Articulos = 1 }; v.Lineas.Add(new LineaVenta { Codigo = "VARIOS", Descripcion = "ARTICULO VARIOS", Cantidad = 1, Precio = 12.34m, PrecioLista = 12.34m }); v.Subtotal = v.Total = 12.34m; VentaRepo.Guardar(v, admin().Id); }
Db.Exec("UPDATE ventas SET fecha='2020-01-01 10:00:00' WHERE id=1");   // una venta vieja, fuera de los 3 meses
TurnoRepo.Movimiento(TurnoRepo.Abierto(1).Id, admin().Id, "SALIDA", 50m, "gasto");
var tc = TurnoRepo.Abierto(1); var corteReal = TurnoRepo.Resumen(tc.Id); TurnoRepo.Cerrar(tc.Id, admin().Id, corteReal.EfectivoEsperado - 5m);
string fotoT = Foto(); Acuse();
var ex1 = ReporteTienda.Existencias(false);
Func<System.Data.DataTable, string, System.Data.DataRow> FilaDe = (t, c) => t.Rows.Cast<System.Data.DataRow>().FirstOrDefault(r => Convert.ToString(r["Codigo"]) == c);
Revisar("existencias reales de la tienda: 20 − 5 = 15 y la cancelada regresó a 20", Db.Dec(FilaDe(ex1, "1")["Existencia"]) == 15m && Db.Dec(FilaDe(ex1, "2")["Existencia"]) == 20m);
Revisar("armar y leer el reporte no cambia nada en la tienda", Con("tienda", () => Foto() == fotoT));
En("oficina");
var vd = ReporteTienda.VentasPorDia(DateTime.Today, DateTime.Today);
Revisar("ventas del día: 2 tickets pagados por $212.34 ($200.00 en efectivo) y 1 cancelado; sin la de 2020", vd.Rows.Count == 1 && Convert.ToInt64(vd.Rows[0]["Tickets"]) == 2 && Db.Dec(vd.Rows[0]["Total"]) == 212.34m && Db.Dec(vd.Rows[0]["Efectivo"]) == 200m && Convert.ToInt64(vd.Rows[0]["Canceladas"]) == 1, vd.Rows.Count == 0 ? "vacío" : string.Join(",", vd.Rows[0].ItemArray));
var pv = ReporteTienda.ProductosVendidos(DateTime.Today, DateTime.Today);
Revisar("por producto: 2 kg de GIRASOL por $200 y el artículo varios; el cancelado no sale", pv.Rows.Count == 2 && Db.Dec(FilaDe(pv, "1")["Importe"]) == 200m && Db.Dec(FilaDe(pv, "VARIOS")["Importe"]) == 12.34m && FilaDe(pv, "2") == null);
var co = ReporteTienda.Cortes(DateTime.Today, DateTime.Today);
Revisar("el corte llega con el mismo esperado que en la caja y la diferencia de −5", co.Rows.Count == 1 && Db.Dec(co.Rows[0]["Esperado"]) == corteReal.EfectivoEsperado && Db.Dec(co.Rows[0]["Ventas"]) == corteReal.VentasNetas && Db.Dec(co.Rows[0]["Diferencia"]) == -5m);
Revisar("recién enlazadas y al día no hay diferencias de catálogo", ReporteTienda.Diferencias().Count == 0, string.Join(" / ", ReporteTienda.Diferencias().Select(d => d.Producto + " " + d.Campo)));
Acuse();
Revisar("leer otro reporte igual no duplica ventas ni cortes", Cuenta("SELECT COUNT(*) FROM tienda_ventas") == 2 && Cuenta("SELECT COUNT(*) FROM tienda_cortes") == 1 && Cuenta("SELECT COUNT(*) FROM tienda_productos") == 3);
Db.Exec("INSERT INTO tienda_dias(dia,tickets,total) VALUES('2021-05-05',9,900)"); Acuse();
Revisar("los días viejos que ya tenía la oficina se conservan", Cuenta("SELECT tickets FROM tienda_dias WHERE dia='2021-05-05'") == 9);
Mercancia("1", 50m);
Revisar("lo que va en camino se ve aparte (existencia 15, por enviar 50)", Db.Dec(FilaDe(ReporteTienda.Existencias(false), "1")["PorEnviar"]) == 50m && ReporteTienda.Existencia(ProductoRepo.PorCodigo("1").Id) == 15m);
En("tienda");
Precio("2", 199m); ProductoRepo.Guardar(new Producto { Codigo = "80", Descripcion = "NUEZ DE TIENDA", Precio = 250m, Costo = 180m, Existencia = 3m, Granel = true }, admin().Id);
ProductoRepo.Guardar(new Producto { Codigo = "81", Descripcion = "PASA DE TIENDA", Precio = 90m }, admin().Id);
Acuse();
ProductoRepo.Guardar(new Producto { Codigo = "81", Descripcion = "OTRO 81", Precio = 1m }, admin().Id);
var dif = ReporteTienda.Diferencias();
Revisar("la oficina ve lo distinto: 1 precio cambiado en la tienda y 2 productos que solo existen allá (lo pendiente no cuenta)", dif.Count == 3 && dif.Count(d => d.Tipo == ReporteTienda.Distinto && d.Campo == "precio" && d.Oficina == "206" && d.Tienda == "199") == 1 && dif.Count(d => d.Tipo == ReporteTienda.SoloTienda) == 2, string.Join(" / ", dif.Select(d => d.Tipo + " " + d.Producto + " " + d.Campo)));
Revisar("traer a la oficina un producto de la tienda", ReporteTienda.TomarDeTienda(dif.First(d => d.Codigo == "80")) == null && ProductoRepo.PorCodigo("80").Costo == 180m);
Revisar("si el código ya está ocupado en la oficina, no lo trae y dice de quién es", (ReporteTienda.TomarDeTienda(dif.First(d => d.Codigo == "81")) ?? "").Contains("OTRO 81"));
Revisar("mandar de nuevo el precio de la oficina", ReporteTienda.MandarDeOficina(dif.First(d => d.Codigo == "2")) == null && ReporteTienda.Diferencias().Count(d => d.Tipo == ReporteTienda.Distinto) == 0);
var rreg = Aplica(Enviar());
Revisar("el envío corrige el precio en la tienda y el producto traído no se duplica ni se toca", ProductoRepo.PorCodigo("2").Precio == 206m && Cuenta("SELECT COUNT(*) FROM productos WHERE codigo='80'") == 1 && Hay("80") == 3m && rreg.Lineas.All(l => l.Codigo != "80"), rreg.Resumen);
Precio("2", 201m); Acuse();
var dt2 = ReporteTienda.Diferencias().First(d => d.Campo == "precio" && d.Codigo == "2");
ReporteTienda.TomarDeTienda(dt2);
Revisar("o quedarse con el precio de la tienda (201)", ProductoRepo.PorCodigo("2").Precio == 201m);
rreg = Aplica(Enviar());
Revisar("y ese envío no cambia el precio de la tienda", ProductoRepo.PorCodigo("2").Precio == 201m && rreg.Lineas.All(l => l.Codigo != "2"));
{ string ac = Envios.EscribirAcuse(usb); Revisar("el reporte va cifrado (no se leen nombres)", !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(ac)).Contains("GIRASOL")); }
Db.Exec("ALTER TABLE turnos RENAME TO turnos_x");
{ string ac = Envios.EscribirAcuse(usb); Db.Exec("ALTER TABLE turnos_x RENAME TO turnos"); En("oficina"); Envios.LeerAcuse(ac); long sc2, ya2; Envios.ContarPendientes(out sc2, out ya2); Revisar("si el reporte falla al armarse, el acuse sale de todos modos", sc2 == 0); }

Seccion("Envíos: volumen");
Enlazar();
using (var cn = Db.Abrir()) using (var tx = cn.BeginTransaction())
{ for (int i = 0; i < 3000; i++) ProductoRepo.Guardar(cn, tx, new Producto { Codigo = "V" + i, Descripcion = "PRODUCTO " + i, Precio = 10 + i, Costo = 5, Existencia = 1 }, 1); tx.Commit(); }
var reloj = System.Diagnostics.Stopwatch.StartNew();
string ev = Enviar(); Envios.Revisar(ev); Aplica(ev);
Revisar("3000 productos nuevos: crear, revisar y aplicar (" + reloj.ElapsedMilliseconds + " ms, " + new FileInfo(ev).Length / 1024 + " KB)", Cuenta("SELECT COUNT(*) FROM productos") == 3003 && reloj.ElapsedMilliseconds < 20000);
using (var cn = Db.Abrir()) using (var tx = cn.BeginTransaction())
{
    long tv = Db.InsertId(cn, tx, "INSERT INTO turnos(caja,apertura,abierto_por,estado) VALUES(1,@p0,1,'ABIERTO')", Db.Ahora());
    for (int d = 0; d < 90; d++)
    {
        long vv = Db.InsertId(cn, tx, "INSERT INTO ventas(turno_id,usuario_id,fecha,total) VALUES(@p0,1,@p1,10)", tv, Db.Fecha(DateTime.Today.AddDays(-d)));
        for (int i = 0; i < 250; i++) Db.Exec(cn, tx, "INSERT INTO venta_detalle(venta_id,producto_id,codigo,descripcion,cantidad,precio,importe) SELECT @p0,id,codigo,descripcion,1,1,1 FROM productos WHERE codigo=@p1", vv, "V" + (i * 7));
    }
    tx.Commit();
}
reloj.Restart(); string acv = Envios.EscribirAcuse(usb); long msArmar = reloj.ElapsedMilliseconds; En("oficina"); reloj.Restart(); Envios.LeerAcuse(acv);
Revisar("reporte de 3 meses con 250 productos al día (armar " + msArmar + " ms, leer " + reloj.ElapsedMilliseconds + " ms, " + new FileInfo(acv).Length / 1024 + " KB)", Cuenta("SELECT COUNT(*) FROM tienda_ventas") == 22500 && msArmar < 15000 && reloj.ElapsedMilliseconds < 15000);
reloj.Restart(); int nd = ReporteTienda.Diferencias().Count; Revisar("comparar 3000 productos (" + reloj.ElapsedMilliseconds + " ms)", nd == 0 && reloj.ElapsedMilliseconds < 8000, nd.ToString());
Revisar("la base queda sana", Salud.Revisar(Db.Archivo) == "ok");
foreach (var f in Directory.GetFiles(Db.Carpeta, "maq_*.db")) File.Delete(f);
Directory.Delete(usb, true);

SQLiteBorrar();
Console.WriteLine(fallas == 0 ? "\nTodas las pruebas de integración pasaron." : $"\n{fallas} prueba(s) fallaron.");
return fallas == 0 ? 0 : 1;
