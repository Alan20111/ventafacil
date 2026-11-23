using System.Data;
using VentaFacil.Core;

// Pruebas del importador de Zafirah (sin SQL Server ni pantallas). Uso:  dotnet run
// La tabla de prueba imita ZFRADMARTICULOS con los nombres de columna que salen de los reportes de Zafirah.
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); // .NET moderno: la página 1252 de Windows (en .NET Framework ya viene)
int fallas = 0;
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

Console.WriteLine(fallas == 0 ? "\nTodas las pruebas del importador pasaron." : $"\n{fallas} prueba(s) fallaron.");
return fallas == 0 ? 0 : 1;
