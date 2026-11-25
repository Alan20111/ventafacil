using System.Data;
using VentaFacil.Core;
using VentaFacil.Data;

// Pruebas del importador de Zafirah (sin SQL Server ni pantallas). Uso:  dotnet run
// La tabla de prueba imita ZFRADMARTICULOS con los nombres de columna que salen de los reportes de Zafirah.
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); // .NET moderno: la página 1252 de Windows (en .NET Framework ya viene)
int fallas = 0;
void Seccion(string t) { Console.WriteLine("\n== " + t); }
void Revisar(string nombre, bool ok, string detalle = "") { Console.WriteLine((ok ? "[OK]    " : "[FALLA] ") + nombre + (ok || detalle == "" ? "" : "  -> " + detalle)); if (!ok) fallas++; }

DataTable Tabla(bool ultimoCostoLleno = false)
{
    var t = new DataTable();
    foreach (var c in new[] { "CodigoProducto", "CodigoAlterno", "Descripcion", "Costo1", "UltimoCosto", "Precio1", "Precio2", "Precio3", "Precio4", "PrecioNormal",
        "LimitePrecio1", "LimitePrecio2", "LimitePrecio3", "Existencia", "NumAlmacen", "Unidad", "Departamento", "NumProveedor" }) t.Columns.Add(c, typeof(object));
    // codigo, alterno, descripcion, costo1, ultimo, p1, p2, p3, p4, normal, l1, l2, l3, existencia, almacen, unidad, depto, proveedor
    object[] F(object cod, object alt, object desc, object c1, object cu, object p1, object p2, object p3, object pn, object ex, object alm, object uni, object dep, object prov) =>
        new object[] { cod, alt, desc, c1, ultimoCostoLleno ? cu : 0m, p1, p2, p3, 0m, pn, 5m, 10m, 20m, ex, alm, uni, dep, prov };
    t.Rows.Add(F("109", "7501234567890", "TAPIOCA", 60.5m, 62m, 100m, 95m, 90m, 100m, 20m, 1, "KG", "Semillas", 7));
    t.Rows.Add(F("109", "7501234567890", "TAPIOCA", 60.5m, 62m, 100m, 95m, 90m, 100m, 5m, 2, "KG", "Semillas", 7));      // el mismo producto en el almacén 2
    t.Rows.Add(F(110m, "0", "ALPISTE", 25m, 26m, 40m, 0m, 0m, 40m, 8m, 1, "PZA", "Semillas", 8));                         // código numérico (decimal), alterno "0"
    t.Rows.Add(F("", "", "SIN CODIGO", 1m, 1m, 5m, 0m, 0m, 5m, 1m, 1, "PZA", "X", 7));                                     // sin código
    t.Rows.Add(F("111", "", "", 1m, 1m, 5m, 0m, 0m, 5m, 1m, 1, "PZA", "X", 7));                                            // sin nombre
    t.Rows.Add(F("112", "", "AVENA", 10m, 11m, 0m, 0m, 0m, 0m, 3m, 1, "KG", "Semillas", 7));                               // sin precio
    t.Rows.Add(F("00113", "00113", "GIRASOL", 35m, 36m, 30m, 0m, 0m, 30m, 4m, 1, "KG", "Semillas", 8));                    // alterno = código; precio < costo
    t.Rows.Add(F("114", "ABC 123", "LENTEJA", 20m, 21m, 45m, 0m, 0m, 45m, 9m, 1, "KG", "Granos", 99));                     // alterno con espacio; proveedor sin nombre
    t.Rows.Add(F(115.0, "7509876543210", "CHILE", 70m, 72m, 110m, 105m, 0m, 110m, 2m, 1, "Kilo", "Chiles", 7));            // código double
    return t;
}

// ================= Sugerir =================
var t1 = Tabla();
var sug = ZafirahMapeo.Sugerir(t1);
Revisar("sugiere código = CodigoProducto, nombre = Descripcion, barras = CodigoAlterno", sug[CampoZ.Codigo] == "CodigoProducto" && sug[CampoZ.Descripcion] == "Descripcion" && sug[CampoZ.Barras] == "CodigoAlterno");
Revisar("sugiere menudeo = PrecioNormal, medio = Precio2, mayoreo = Precio3", sug[CampoZ.Menudeo] == "PrecioNormal" && sug[CampoZ.Medio] == "Precio2" && sug[CampoZ.Mayoreo] == "Precio3");
Revisar("sugiere existencia = Existencia y almacén = NumAlmacen", sug[CampoZ.Existencia] == "Existencia" && sug[CampoZ.Almacen] == "NumAlmacen");
Revisar("costo: si UltimoCosto está en ceros, prefiere Costo1 (la que trae datos)", sug[CampoZ.Costo] == "Costo1", sug.ContainsKey(CampoZ.Costo) ? sug[CampoZ.Costo] : "(ninguna)");
Revisar("costo: si UltimoCosto trae datos, lo prefiere", ZafirahMapeo.Sugerir(Tabla(true))[CampoZ.Costo] == "UltimoCosto");
Revisar("NO adivina las cantidades mínimas (LimitePrecio): las deja para que la persona decida", !sug.ContainsKey(CampoZ.DesdeMedio) && !sug.ContainsKey(CampoZ.DesdeMayoreo));
Revisar("los nombres de columna se reconocen sin importar mayúsculas, acentos ni guiones bajos", ZafirahMapeo.Norm("Código_Producto") == "codigoproducto" && ZafirahMapeo.Norm("  PRECIO NORMAL ") == "precionormal");

var mapa = new Dictionary<CampoZ, string>(sug);
mapa[CampoZ.Unidad] = "Unidad"; mapa[CampoZ.Grupo] = "Departamento"; mapa[CampoZ.Proveedor] = "NumProveedor";

// ================= Transformar: varios almacenes =================
var prov = new DataTable(); prov.Columns.Add("CodigoProveedor", typeof(object)); prov.Columns.Add("Nombre", typeof(object));
prov.Rows.Add(7, "JUANJO"); prov.Rows.Add(8m, "LA CASA");
var dic = ZafirahMapeo.DiccionarioProveedores(prov);
Revisar("proveedores: se arma clave → nombre aunque la clave venga como entero o decimal", dic.Count == 2 && dic["7"] == "JUANJO" && dic["8"] == "LA CASA");

var r = ZafirahMapeo.Transformar(t1, mapa, new OpcionesZ { SumarAlmacenes = true, ProveedoresPorClave = dic });
var porCod = r.Productos.ToDictionary(p => p.Codigo);
Revisar("filas leídas = 9; se importan 5 productos (109, 110, 00113, 114, 115)", r.Filas == 9 && r.Productos.Count == 5, r.Productos.Count.ToString());
Revisar("omitidos: 1 sin código, 1 sin nombre, 1 sin precio", r.SinCodigo == 1 && r.SinDescripcion == 1 && r.SinPrecio == 1);
Revisar("varios almacenes SUMADOS: TAPIOCA = 20 + 5 = 25 (no cuenta como repetido)", porCod["109"].Existencia == 25m && r.CodigosRepetidos == 0);
var elegido = ZafirahMapeo.Transformar(t1, mapa, new OpcionesZ { SumarAlmacenes = false, AlmacenElegido = "2", ProveedoresPorClave = dic }).Productos.First(p => p.Codigo == "109");
Revisar("almacén ELEGIDO (el 2): TAPIOCA = 5", elegido.Existencia == 5m, elegido.Existencia.ToString());
var otro = ZafirahMapeo.Transformar(t1, mapa, new OpcionesZ { SumarAlmacenes = false, AlmacenElegido = "1", ProveedoresPorClave = dic }).Productos.First(p => p.Codigo == "109");
Revisar("almacén ELEGIDO (el 1): TAPIOCA = 20", otro.Existencia == 20m);

// ================= códigos y barras =================
Revisar("código numérico (decimal 110 y double 115) se vuelve texto sin decimales", porCod.ContainsKey("110") && porCod.ContainsKey("115"));
Revisar("los ceros a la izquierda de un código de texto se conservan (00113)", porCod.ContainsKey("00113"));
Revisar("código de barras igual al código o «0» se ignora; el real se guarda", porCod["00113"].Barras == "" && porCod["110"].Barras == "" && porCod["109"].Barras == "7501234567890" && porCod["115"].Barras == "7509876543210");
Revisar("código de barras con espacio se ignora y se avisa", porCod["114"].Barras == "" && r.BarrasIgnorados == 1);

// ================= unidad, grupo, proveedor =================
Revisar("unidad KG / Kilo → a granel; PZA → pieza", porCod["109"].Granel && porCod["115"].Granel && !porCod["110"].Granel);
Revisar("departamento en mayúsculas", porCod["109"].Grupo == "SEMILLAS" && porCod["115"].Grupo == "CHILES");
Revisar("proveedor 7 → JUANJO; proveedor 99 sin nombre se queda con su clave y se avisa", porCod["109"].Proveedor == "JUANJO" && porCod["114"].Proveedor == "99" && r.Avisos.Any(a => a.Contains("proveedor que no se encontró")));

// ================= precios y niveles =================
Revisar("precio de menudeo y costo leídos", porCod["109"].Menudeo == 100m && porCod["109"].Costo == 60.5m);
Revisar("precio menor al costo se importa pero se AVISA (GIRASOL)", r.PrecioBajoCosto == 1 && porCod["00113"].Menudeo == 30m);
Revisar("medio mayoreo y mayoreo SIN cantidad mínima: no se aplican y se avisa", porCod["109"].Medio == 0m && porCod["109"].Mayoreo == 0m && r.NivelesSinCantidad >= 1 && r.Avisos.Any(a => a.Contains("sin cantidad mínima")));
mapa[CampoZ.DesdeMedio] = "LimitePrecio1"; mapa[CampoZ.DesdeMayoreo] = "LimitePrecio2";
var r2 = ZafirahMapeo.Transformar(t1, mapa, new OpcionesZ { ProveedoresPorClave = dic });
var t109 = r2.Productos.First(p => p.Codigo == "109");
Revisar("con la cantidad mínima elegida: medio 95 desde 5 y mayoreo 90 desde 10", t109.Medio == 95m && t109.DesdeMedio == 5m && t109.Mayoreo == 90m && t109.DesdeMayoreo == 10m);
Revisar("un nivel con precio 0 no se crea", r2.Productos.First(p => p.Codigo == "110").Medio == 0m);

// ================= validaciones =================
var sinPrecio = new Dictionary<CampoZ, string>(mapa); sinPrecio.Remove(CampoZ.Menudeo);
var rx = ZafirahMapeo.Transformar(t1, sinPrecio, null);
Revisar("sin columna de precio de menudeo: no importa nada y lo dice", !rx.Valido && rx.Avisos.Any(a => a.Contains("Precio de menudeo")));
var rotas = new Dictionary<CampoZ, string>(mapa) { [CampoZ.Costo] = "ColumnaQueNoExiste" };
Revisar("una columna que ya no existe se avisa en vez de fallar", ZafirahMapeo.Transformar(t1, rotas, null).Avisos.Any(a => a.Contains("ColumnaQueNoExiste")));
var sinUnidad = new Dictionary<CampoZ, string>(mapa); sinUnidad.Remove(CampoZ.Unidad);
Revisar("sin columna de unidad: todo pieza y lo avisa", ZafirahMapeo.Transformar(t1, sinUnidad, null).Productos.All(p => !p.Granel) && ZafirahMapeo.Transformar(t1, sinUnidad, null).Avisos.Any(a => a.Contains("PIEZA")));

// ================= números escritos de formas distintas =================
var t3 = new DataTable(); foreach (var c in new[] { "Codigo", "Nombre", "Precio", "Costo" }) t3.Columns.Add(c, typeof(object));
t3.Rows.Add("1", "A", "12,50", "$1,200.00"); t3.Rows.Add("2", "B", 7.5, 3); t3.Rows.Add("3", "C", "abc", "");
var r3 = ZafirahMapeo.Transformar(t3, ZafirahMapeo.Sugerir(t3), null);
Revisar("números en texto con coma decimal, con $ y con miles se leen bien; texto basura = sin precio", r3.Productos.Count == 2 && r3.Productos[0].Menudeo == 12.5m && r3.Productos[0].Costo == 1200m && r3.Productos[1].Menudeo == 7.5m && r3.SinPrecio == 1);
Revisar("el ruido de decimales de un double no se cuela (0.1 + 0.2 = 0.3)", ZafirahMapeo.Numero(0.1 + 0.2) == 0.3m);

// ================= archivos Excel y CSV =================
string dir = Path.Combine(Path.GetTempPath(), "zafirah_prueba_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
try
{
    var hoja = new Xlsx.HojaSalida { Nombre = "Reporte" };
    Xlsx.Celda C(object v, Xlsx.Estilo e = Xlsx.Estilo.Normal) => new Xlsx.Celda { Valor = v, Estilo = e };
    hoja.Filas.Add(new List<Xlsx.Celda> { C("SEMILLAS Y BOTANAS — LISTA DE PRODUCTOS", Xlsx.Estilo.Titulo) });
    hoja.Filas.Add(new List<Xlsx.Celda>());
    hoja.Filas.Add(new List<Xlsx.Celda> { C("CodigoProducto", Xlsx.Estilo.Encabezado), C("Descripción", Xlsx.Estilo.Encabezado), C("Precio Normal", Xlsx.Estilo.Encabezado), C("Costo1", Xlsx.Estilo.Encabezado), C("Existencia", Xlsx.Estilo.Encabezado) });
    hoja.Filas.Add(new List<Xlsx.Celda> { C("00109"), C("TAPIOCA"), C(100.0), C(60.5), C(25.0) });
    hoja.Filas.Add(new List<Xlsx.Celda> { C(110.0), C("ALPISTE"), C(40.0), C(25.0), C(8.0) });
    string xlsx = Path.Combine(dir, "reporte.xlsx"); Xlsx.Escribir(xlsx, new List<Xlsx.HojaSalida> { hoja });
    var tx = TablaArchivo.Leer(xlsx);
    Revisar("Excel: encuentra el encabezado aunque haya título y renglones vacíos arriba", tx.Columns.Count == 5 && tx.Columns[1].ColumnName == "Descripción" && tx.Rows.Count == 2);
    var rxl = ZafirahMapeo.Transformar(tx, ZafirahMapeo.Sugerir(tx), null);
    Revisar("Excel → mapeo sugerido → productos: código con ceros y código numérico, precios y existencia", rxl.Productos.Count == 2 && rxl.Productos[0].Codigo == "00109" && rxl.Productos[1].Codigo == "110" && rxl.Productos[0].Menudeo == 100m && rxl.Productos[0].Existencia == 25m);

    string csv = "﻿Código;Nombre;Precio;Existencia\r\n\"00109\";\"TAPIOCA \"\"ESPECIAL\"\"\";\"100,50\";25\r\n110;ÑANDÚ;40;8\r\n\r\n";
    string rutaCsv = Path.Combine(dir, "lista.csv"); File.WriteAllText(rutaCsv, csv, new System.Text.UTF8Encoding(false));
    var tc = TablaArchivo.Leer(rutaCsv);
    Revisar("CSV con punto y coma, comillas, acentos y BOM: 2 filas, ceros a la izquierda conservados", tc.Rows.Count == 2 && Convert.ToString(tc.Rows[0][0]) == "00109" && Convert.ToString(tc.Rows[0][1]) == "TAPIOCA \"ESPECIAL\"" && Convert.ToString(tc.Rows[1][1]) == "ÑANDÚ");
    var rcs = ZafirahMapeo.Transformar(tc, ZafirahMapeo.Sugerir(tc), null);
    Revisar("CSV → productos con precio en coma decimal (100,50)", rcs.Productos.Count == 2 && rcs.Productos[0].Menudeo == 100.5m);

    string csvComas = "codigo,descripcion,precio\n1,A,10\n2,B,20\n";
    string rutaComas = Path.Combine(dir, "comas.csv"); File.WriteAllText(rutaComas, csvComas);
    Revisar("CSV separado por comas se detecta solo", TablaArchivo.Leer(rutaComas).Rows.Count == 2);
    // Windows-1252 (lo normal en programas viejos)
    string rutaAnsi = Path.Combine(dir, "ansi.csv"); File.WriteAllBytes(rutaAnsi, System.Text.Encoding.GetEncoding(1252).GetBytes("codigo;descripcion;precio\n1;PIÑA;10\n"));
    Revisar("CSV en Windows-1252 (ñ, acentos) se lee bien", Convert.ToString(TablaArchivo.Leer(rutaAnsi).Rows[0][1]) == "PIÑA");
    bool lanzo = false; try { TablaArchivo.Leer(Path.Combine(dir, "x.pdf")); } catch (InvalidOperationException) { lanzo = true; }
    Revisar("un archivo que no es Excel ni CSV se rechaza con un mensaje claro", lanzo);
}
finally { Directory.Delete(dir, true); }

// ================= capa de SQL Server (lo que se puede probar sin un servidor) =================
{
    Revisar("un nombre de tabla nunca se interpreta como SQL (corchetes duplicados)", VentaFacil.Data.ZafirahSql.Cita("a]b; DROP TABLE x") == "[a]]b; DROP TABLE x]" && VentaFacil.Data.ZafirahSql.Cita("ZFRADMARTICULOS") == "[ZFRADMARTICULOS]");
    var t = new VentaFacil.Data.TablaSql { Esquema = "dbo", Nombre = "ZFRADMARTICULOS", Filas = 492 };
    Revisar("la tabla se escribe como [dbo].[ZFRADMARTICULOS] y se muestra con su número de filas", t.Completo == "[dbo].[ZFRADMARTICULOS]" && t.ToString().Contains("492 filas"));
    string win = VentaFacil.Data.ZafirahSql.Cadena("(local)", "smartdatabase", "", "");
    string sql = VentaFacil.Data.ZafirahSql.Cadena(@".\SQLEXPRESS", "smartdatabase", "lector", "p;w=1");
    Revisar("cadena con usuario de Windows: seguridad integrada, sin usuario ni contraseña, 6 s de espera", win.Contains("Integrated Security=True") && !win.Contains("Password") && win.Contains("Connect Timeout=6") && win.Contains("Initial Catalog=smartdatabase"));
    Revisar("cadena con usuario de SQL: la contraseña con punto y coma no rompe la cadena", sql.Contains("User ID=lector") && sql.Contains("\"p;w=1\"") || sql.Contains("Password=\"p;w=1\""), sql);
    Revisar("sin base de datos elegida no pone Initial Catalog (para listar las bases)", !VentaFacil.Data.ZafirahSql.Cadena("(local)", null, "", "").Contains("Initial Catalog"));
    var probables = VentaFacil.Data.ZafirahSql.ServidoresProbables();
    Revisar("servidores probables: (local) primero, sin repetidos", probables[0] == "(local)" && probables.Length == probables.Distinct(StringComparer.OrdinalIgnoreCase).Count() && probables.Any(x => x.Contains("SQLEXPRESS")));
    Revisar("un error que no es de SQL se muestra tal cual", VentaFacil.Data.ZafirahSql.Explicar(new InvalidOperationException("algo")) == "algo");
}

// ================= JSON mínimo =================
Seccion("JSON mínimo");
{
    var d = JsonMini.Leer("{ \"a\": 1.5, \"b\": [\"x\", \"y\"], \"c\": { \"d\": true, \"e\": null }, \"f\": \"\\u00d1andú \\\"q\\\"\\n\" }") as Dictionary<string, object>;
    Revisar("lee números, listas, objetos anidados, true/null y textos con \\u y comillas escapadas", d != null && (double)d["a"] == 1.5 && ((List<object>)d["b"]).Count == 2 && (bool)((Dictionary<string, object>)d["c"])["d"] && ((Dictionary<string, object>)d["c"])["e"] == null && (string)d["f"] == "Ñandú \"q\"\n");
    var c = JsonMini.Leer("// perfil\n{ \"a\": 1, /* nota */ \"b\": [1, 2,], }") as Dictionary<string, object>;
    Revisar("perdona lo que una persona escribe a mano: comentarios // y /* */ y comas de más", c != null && ((List<object>)c["b"]).Count == 2);
    string error = ""; try { JsonMini.Leer("{\n  \"a\": 1\n  \"b\": 2\n}"); } catch (FormatException ex) { error = ex.Message; }
    Revisar("un error dice la línea y la columna (falta una coma en la línea 3)", error.Contains("línea 3") && error.Contains("coma"), error);
    Revisar("textos sin cerrar, valores raros y basura al final se rechazan con mensaje claro", new[] { "{\"a\": \"sin cerrar", "{\"a\": @}", "{} sobra", "[1, 2" }.All(t => { try { JsonMini.Leer(t); return false; } catch (FormatException) { return true; } }));
    var origen = new Dictionary<string, object> { ["nombre"] = "Ñ \"x\"\n", ["n"] = 3, ["lista"] = new List<object> { "a", "b" }, ["vacio"] = new Dictionary<string, object>(), ["si"] = true, ["nada"] = null, ["objs"] = new List<object> { new Dictionary<string, object> { ["k"] = 1 } } };
    var ida = JsonMini.Leer(JsonMini.Escribir(origen)) as Dictionary<string, object>;
    Revisar("escribir y volver a leer da lo mismo (ida y vuelta)", (string)ida["nombre"] == "Ñ \"x\"\n" && (double)ida["n"] == 3 && ((List<object>)ida["lista"]).Count == 2 && (bool)ida["si"] && ida["nada"] == null && ((List<object>)ida["objs"]).Count == 1);
    Revisar("las listas de textos cortos salen en una línea y los objetos con sangría", JsonMini.Escribir(new Dictionary<string, object> { ["l"] = new List<object> { "a", "b" } }) == "{\n  \"l\": [\"a\", \"b\"]\n}");
}

// ================= Perfiles de importación =================
Seccion("Perfiles de importación");
{
    var z = PerfilImportacion.Zafirah();
    Revisar("el perfil incluido de Zafirah trae base, tablas y columnas candidatas", z.Integrado && z.Base == "smartdatabase" && z.PuntajeTabla("ZFRADMARTICULOS") == 100 && z.PuntajeTabla("zfradmarticulos") == 100 && z.Columnas[CampoZ.Menudeo].Contains("PrecioNormal"));
    Revisar("elige la tabla de artículos: exacta 100, que contenga *ARTICULO* 49, otras 0", z.PuntajeTabla("ZFRADMARTICULOS") > z.PuntajeTabla("MisArticulosViejos") && z.PuntajeTabla("MisArticulosViejos") > 0 && z.PuntajeTabla("ZFRADMCLIENTES") == 0);
    var sug1 = z.Sugerir(Tabla());
    Revisar("el perfil de Zafirah propone lo mismo que las reglas generales en la tabla de prueba", sug1[CampoZ.Codigo] == "CodigoProducto" && sug1[CampoZ.Menudeo] == "PrecioNormal" && sug1[CampoZ.Costo] == "Costo1");

    // Otro programa, con otros nombres, solo con un archivo de texto
    string json = "// POS de prueba\n{ \"nombre\": \"Otro POS\", \"tablas\": [\"Articulos\"], \"columnas\": { \"Codigo\": [\"Cve_Art\"], \"Descripcion\": \"Nom_Art\", \"Menudeo\": [\"P_Publico\"], \"Costo\": [\"No_Existe\"] }, \"reglas\": { \"unidadesGranel\": [\"caja\"], \"sumarAlmacenes\": false } }";
    var otroPos = PerfilImportacion.Desde(json, "otroPos.json");
    var t = new DataTable(); foreach (var c in new[] { "Cve_Art", "Nom_Art", "P_Publico", "Costo1", "Existencia", "Um" }) t.Columns.Add(c, typeof(object));
    t.Rows.Add("A1", "TORNILLO", 5.5m, 3m, 10m, "caja"); t.Rows.Add("A2", "LIJA", 8m, 4m, 5m, "pza");
    var s2 = otroPos.Sugerir(t);
    Revisar("un perfil escrito a mano elige las columnas que dice (y acepta un texto suelto en lugar de lista)", s2[CampoZ.Codigo] == "Cve_Art" && s2[CampoZ.Descripcion] == "Nom_Art" && s2[CampoZ.Menudeo] == "P_Publico");
    Revisar("lo que el perfil menciona y NO encuentra se deja sin elegir (no se adivina): Costo", !s2.ContainsKey(CampoZ.Costo));
    Revisar("lo que el perfil no menciona se detecta con las reglas generales: Existencia", s2[CampoZ.Existencia] == "Existencia");
    Revisar("reglas del perfil: sumarAlmacenes=false", otroPos.SumarAlmacenes == false && otroPos.Nombre == "Otro POS");
    var m = new Dictionary<CampoZ, string>(s2); m[CampoZ.Unidad] = "Um";
    var conPerfil = ZafirahMapeo.Transformar(t, m, otroPos.AplicarA(new OpcionesZ()));
    var sinPerfil = ZafirahMapeo.Transformar(t, m, new OpcionesZ());
    Revisar("unidadesGranel del perfil (solo «caja») cambian qué se vende a granel", conPerfil.Productos.First(p => p.Codigo == "A1").Granel && !conPerfil.Productos.First(p => p.Codigo == "A2").Granel && !sinPerfil.Productos.First(p => p.Codigo == "A1").Granel);

    // guardar desde el asistente y volver a cargar
    var creado = PerfilImportacion.DesdeMapeo("Mi mapeo / de prueba", m, false);
    var vuelto = PerfilImportacion.Desde(creado.ComoJson(), "x.json");
    Revisar("guardar el mapeo como perfil y volver a leerlo da el mismo mapeo", vuelto.Nombre == "Mi mapeo / de prueba" && vuelto.Columnas[CampoZ.Codigo][0] == "Cve_Art" && vuelto.Columnas[CampoZ.Unidad][0] == "Um" && vuelto.SumarAlmacenes == false);
    Revisar("ese perfil reproduce el mismo mapeo sobre la misma tabla", vuelto.Sugerir(t)[CampoZ.Menudeo] == "P_Publico" && vuelto.Sugerir(t)[CampoZ.Unidad] == "Um");
    Revisar("el nombre de archivo se limpia (sin / \\ : ni puntos)", PerfilImportacion.ArchivoPara("Mi mapeo / de prueba") == "Mi mapeo _ de prueba.json" && PerfilImportacion.ArchivoPara("  ") == "perfil.json" && PerfilImportacion.ArchivoPara("a.b.c") == "a_b_c.json");
    string errorCampo = ""; try { PerfilImportacion.Desde("{ \"columnas\": { \"Precio\": [\"x\"] } }", "p.json"); } catch (FormatException ex) { errorCampo = ex.Message; }
    Revisar("un dato que no existe en el perfil se rechaza y se listan los válidos", errorCampo.Contains("«Precio»") && errorCampo.Contains("Menudeo"));

    // carpeta de perfiles con uno bueno, uno roto y un archivo ajeno
    string carpeta = Path.Combine(Path.GetTempPath(), "perfiles_prueba_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(carpeta);
    try
    {
        File.WriteAllText(Path.Combine(carpeta, "bueno.json"), json);
        File.WriteAllText(Path.Combine(carpeta, "roto.json"), "{ \"nombre\": ");
        File.WriteAllText(Path.Combine(carpeta, "notas.txt"), "no soy un perfil");
        List<string> problemas;
        var todos = PerfilImportacion.Cargar(carpeta, out problemas);
        Revisar("la carpeta de perfiles: se cargan el incluido y el bueno; el roto se explica y se salta", todos.Count == 2 && todos[1].Nombre == "Otro POS" && problemas.Count == 1 && problemas[0].StartsWith("roto.json"), string.Join(" | ", problemas));
        List<string> p2; Revisar("sin carpeta (o inexistente) queda solo el perfil incluido", PerfilImportacion.Cargar(Path.Combine(carpeta, "no_existe"), out p2).Count == 1 && p2.Count == 0);
    }
    finally { Directory.Delete(carpeta, true); }
}

Seccion("Respaldos .BK (ayudantes)");
{
    string tmp = ZafirahBk.NombreTemporal();
    Revisar("el nombre temporal empieza con el prefijo y es válido", tmp.StartsWith("vf_import_tmp_") && ZafirahBk.EsTemporal(tmp));
    Revisar("dos nombres temporales no se repiten", ZafirahBk.NombreTemporal() != ZafirahBk.NombreTemporal());
    Revisar("NUNCA se acepta la base real de Zafirah ni nombres peligrosos",
        !ZafirahBk.EsTemporal("smartdatabase") && !ZafirahBk.EsTemporal("master") && !ZafirahBk.EsTemporal("") && !ZafirahBk.EsTemporal(null)
        && !ZafirahBk.EsTemporal("vf_import_tmp_x]; DROP DATABASE master;--") && !ZafirahBk.EsTemporal("vf_import_tmp_") && !ZafirahBk.EsTemporal("xvf_import_tmp_abc"));
    string rechazo = ""; try { ZafirahBk.Eliminar("Server=.", "smartdatabase"); } catch (ArgumentException ex) { rechazo = ex.Message; }
    Revisar("Eliminar se niega a borrar otra base (sin siquiera conectarse)", rechazo.Contains("temporales"));
    string rechazo2 = ""; try { ZafirahBk.ComandoRestaurar("master", "a.bk", 1, new List<ArchivoRespaldo>(), "C:\\x"); } catch (ArgumentException ex) { rechazo2 = ex.Message; }
    Revisar("ComandoRestaurar rechaza un destino que no sea temporal", rechazo2.Contains("vf_import_tmp_"));

    Revisar("Literal duplica comillas", ZafirahBk.Literal("C:\\o'brien\\a.bk") == "N'C:\\o''brien\\a.bk'");
    var arch = new List<ArchivoRespaldo> {
        new ArchivoRespaldo { Logico = "Smart_Data", Tipo = "D" }, new ArchivoRespaldo { Logico = "Smart_Log", Tipo = "L" }, new ArchivoRespaldo { Logico = "Extra", Tipo = "D" } };
    string cmd = ZafirahBk.ComandoRestaurar("vf_import_tmp_ab12cd34", "C:\\Users\\Public\\o'x.bk", 2, arch, "C:\\SQL\\DATA\\");
    Revisar("el RESTORE trae el archivo, la posición y REPLACE", cmd.Contains("FROM DISK = N'C:\\Users\\Public\\o''x.bk'") && cmd.Contains("WITH FILE = 2") && cmd.Contains("REPLACE"), cmd);
    Revisar("cada archivo se mueve a la carpeta de datos con extensión según su tipo",
        cmd.Contains("MOVE N'Smart_Data' TO N'C:\\SQL\\DATA\\vf_import_tmp_ab12cd34.mdf'") && cmd.Contains("MOVE N'Smart_Log' TO N'C:\\SQL\\DATA\\vf_import_tmp_ab12cd34.ldf'") && cmd.Contains("vf_import_tmp_ab12cd34_2.ndf"), cmd);
    Revisar("ComandosRevisar trae HEADERONLY y FILELISTONLY", ZafirahBk.ComandosRevisar("D:\\a.bk").Contains("RESTORE HEADERONLY") && ZafirahBk.ComandosRevisar("D:\\a.bk").Contains("RESTORE FILELISTONLY"));

    Revisar("porcentaje en inglés", ZafirahBk.Porcentaje("35 percent processed.") == 35);
    Revisar("porcentaje en español", ZafirahBk.Porcentaje("10 por ciento procesado.") == 10);
    Revisar("un mensaje sin porcentaje da null", ZafirahBk.Porcentaje("RESTORE DATABASE successfully processed 1234 pages") == null);
    Revisar("un porcentaje imposible se ignora", ZafirahBk.Porcentaje("500 percent") == null);

    Revisar("nombres de versión", ZafirahBk.NombreVersion(8, 0) == "SQL Server 2000" && ZafirahBk.NombreVersion(10, 50) == "SQL Server 2008 R2" && ZafirahBk.NombreVersion(10, 0) == "SQL Server 2008" && ZafirahBk.NombreVersion(16, 0) == "SQL Server 2022" && ZafirahBk.NombreVersion(99, 1) == "SQL Server 99.1");
    string av; bool imp;
    Revisar("mismo servidor: compatible", ZafirahBk.Compatible(10, 10, out av, out imp) && av == "" && !imp);
    Revisar("respaldo de 2000 en servidor 2008: compatible", ZafirahBk.Compatible(8, 10, out av, out imp) && !imp);
    Revisar("respaldo de 2000 en servidor 2019: imposible", !ZafirahBk.Compatible(8, 15, out av, out imp) && imp && av.Contains("2005, 2008"), av);
    Revisar("respaldo más nuevo que el servidor: imposible", !ZafirahBk.Compatible(15, 10, out av, out imp) && imp && av.Contains("MÁS NUEVA"), av);
    Revisar("respaldo de 2005 en servidor 2019: aviso, no imposible", !ZafirahBk.Compatible(9, 15, out av, out imp) && !imp && av != "");
    Revisar("versión desconocida (0) no bloquea", ZafirahBk.Compatible(0, 15, out av, out imp));

    Revisar("error 3201 explica el permiso de la carpeta", ZafirahBk.ExplicarNumero(3201, "x").Contains("C:\\Users\\Public"));
    Revisar("error 3241 dice que no es un respaldo válido", ZafirahBk.ExplicarNumero(3241, "x").Contains("no parece un respaldo"));
    Revisar("error de disco lleno", ZafirahBk.ExplicarNumero(3202, "x").Contains("espacio"));
    Revisar("un error de conexión cae al explicador general", ZafirahBk.ExplicarNumero(18456, "x").Contains("usuario o la contraseña"));
    Revisar("un número desconocido da null", ZafirahBk.ExplicarNumero(99999, "x") == null);

    var inf = new InfoRespaldo { Base = "smartdatabase", VersionMayor = 8, TamanoBytes = 5L * 1048576, Fecha = "18/07/2026 10:00", Archivos = arch };
    Revisar("el resumen del respaldo es legible", inf.Resumen().Contains("«smartdatabase»") && inf.Resumen().Contains("SQL Server 2000") && inf.Resumen().Contains("5.0 MB") && inf.Resumen().Contains("3 archivos"), inf.Resumen());
}

Seccion("CSV de práctica (ejemplos/productos_prueba_100.csv)");
{
    string raiz = AppContext.BaseDirectory;
    while (raiz != null && !File.Exists(Path.Combine(raiz, "ejemplos", "productos_prueba_100.csv"))) raiz = Path.GetDirectoryName(raiz);
    var dt = TablaArchivo.Leer(Path.Combine(raiz, "ejemplos", "productos_prueba_100.csv"));
    var res = ZafirahMapeo.Transformar(dt, PerfilImportacion.Zafirah().Sugerir(dt), new OpcionesZ());
    Revisar("se leen los 100 productos con el perfil que viene elegido", res.Valido && res.Productos.Count == 100, res.Productos.Count + " " + string.Join(" | ", res.Avisos));
    Revisar("los de granel entran por peso y los demás por pieza", res.Productos.Count(p => p.Granel) == dt.Rows.Cast<DataRow>().Count(r => r["unidad"].ToString() == "kg") && res.Productos.Any(p => !p.Granel));
    Revisar("precio, costo, existencia y grupo vienen llenos, y el precio supera al costo", res.Productos.All(p => p.Menudeo > p.Costo && p.Costo > 0 && p.Existencia.HasValue && p.Grupo != ""));
    Revisar("códigos 9001-9100 y nombres con PRUEBA (no chocan con la tienda)", res.Productos.All(p => int.Parse(p.Codigo) >= 9001 && int.Parse(p.Codigo) <= 9100 && p.Descripcion.EndsWith("PRUEBA")));
}

Console.WriteLine(fallas == 0 ? "\nTodas las pruebas del importador pasaron." : $"\n{fallas} prueba(s) fallaron.");
return fallas == 0 ? 0 : 1;
