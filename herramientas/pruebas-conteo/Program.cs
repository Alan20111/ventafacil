using VentaFacil.Core;

// Pruebas del ciclo de conteo con Excel: se escribe la hoja con el código real del programa,
// se vuelve a leer, y se comprueba lo que se interpreta de lo contado.
// Uso: dotnet run
int fallas = 0;
void Revisar(string nombre, bool ok, string detalle = "") { Console.WriteLine((ok ? "[OK]    " : "[FALLA] ") + nombre + (ok || detalle == "" ? "" : "  -> " + detalle)); if (!ok) fallas++; }

var filas = new List<FilaConteo>
{
    new FilaConteo { Codigo = "7501234567890", Producto = "CACAHUATE JAPONÉS ñ", Grupo = "JUANJO", EnSistema = 12.5m },
    new FilaConteo { Codigo = "00123",         Producto = "SEMILLA DE GIRASOL",  Grupo = "JUANJO", EnSistema = 3m },
    new FilaConteo { Codigo = "A-77",          Producto = "ALPISTE <a granel> & más", Grupo = "LA CASA", EnSistema = 0.75m },
    new FilaConteo { Codigo = "109",           Producto = "TAPIOCA",             Grupo = "LA CASA", EnSistema = 20m },
    new FilaConteo { Codigo = "555",           Producto = "SIN CONTAR",          Grupo = "LA CASA", EnSistema = 8m },
};
// Lo que "el dueño escribió" en CONTADO
var contado = new Dictionary<string, decimal?> { ["7501234567890"] = 10m, ["00123"] = 0m, ["A-77"] = 2.5m, ["109"] = 20m, ["555"] = null };

string ruta = Path.Combine(Path.GetTempPath(), "conteo_prueba_" + Guid.NewGuid().ToString("N") + ".xlsx");
try
{
    var hoja = ConteoExcel.ArmarHoja("MI NEGOCIO — HOJA DE CONTEO", filas, f => contado[f.Codigo]);
    Xlsx.Escribir(ruta, new List<Xlsx.HojaSalida> { hoja });
    var leidas = Xlsx.Leer(ruta);
    var r = ConteoExcel.Leer(leidas);

    Revisar("encuentra las columnas CÓDIGO y CONTADO (aunque el encabezado esté en el renglón 4)", r.HalloColumnas);
    Revisar("lee 4 renglones contados y 1 sin contar", r.Contados.Count == 4 && r.SinContar == 1, $"{r.Contados.Count} contados, {r.SinContar} sin contar");
    var d = r.Contados.ToDictionary(k => k.Key, k => k.Value);
    Revisar("código largo (13 dígitos) se conserva", d.ContainsKey("7501234567890") && d["7501234567890"] == 10m);
    Revisar("código con ceros a la izquierda ('00123') se conserva como texto", d.ContainsKey("00123"));
    Revisar("un CERO contado cuenta como contado (no como vacío)", d.ContainsKey("00123") && d["00123"] == 0m);
    Revisar("cantidad con decimales (2.5 kg) exacta", d["A-77"] == 2.5m);
    Revisar("producto con caracteres especiales no rompe el archivo", leidas[0].Filas.Count == 9);
    Revisar("no hay cantidades inválidas ni repetidas", r.Invalidos.Count == 0 && r.Repetidos == 0);

    // Excel convierte un código solo-dígitos en NÚMERO cuando el dueño lo retecla: se debe reconocer igual
    var raro = new Xlsx.HojaSalida { Nombre = "Hoja1" };
    raro.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = "Código" }, new Xlsx.Celda { Valor = "Producto" }, new Xlsx.Celda { Valor = " Contado " } });
    raro.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = 7501234567890d }, new Xlsx.Celda { Valor = "x" }, new Xlsx.Celda { Valor = 7d } });   // código numérico
    raro.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = "B1" }, new Xlsx.Celda { Valor = "x" }, new Xlsx.Celda { Valor = "3,5" } });          // coma decimal escrita como texto
    raro.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = "B2" }, new Xlsx.Celda { Valor = "x" }, new Xlsx.Celda { Valor = "abc" } });          // basura
    raro.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = "B3" }, new Xlsx.Celda { Valor = "x" }, new Xlsx.Celda { Valor = -4d } });            // negativo
    raro.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = "B1" }, new Xlsx.Celda { Valor = "x" }, new Xlsx.Celda { Valor = 9d } });             // repetido
    raro.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = "" }, new Xlsx.Celda { Valor = "renglón vacío" }, new Xlsx.Celda { Valor = 5d } });  // sin código
    string ruta2 = Path.Combine(Path.GetTempPath(), "conteo_prueba2_" + Guid.NewGuid().ToString("N") + ".xlsx");
    Xlsx.Escribir(ruta2, new List<Xlsx.HojaSalida> { raro });
    var r2 = ConteoExcel.Leer(Xlsx.Leer(ruta2));
    File.Delete(ruta2);
    var d2 = r2.Contados.ToDictionary(k => k.Key, k => k.Value);
    Revisar("encabezados con acento, minúsculas y espacios ('Código', ' Contado ') se reconocen", r2.HalloColumnas);
    Revisar("código numérico de Excel (7501234567890) se reconoce igual", d2.ContainsKey("7501234567890") && d2["7501234567890"] == 7m);
    Revisar("'3,5' escrito como texto se lee 3.5", d2.ContainsKey("B1") && d2["B1"] == 3.5m);
    Revisar("texto 'abc' y negativos se marcan como inválidos (no se aplican)", r2.Invalidos.Contains("B2") && r2.Invalidos.Contains("B3") && !d2.ContainsKey("B2") && !d2.ContainsKey("B3"));
    Revisar("código repetido usa el primero y lo avisa", r2.Repetidos == 1 && d2["B1"] == 3.5m);
    Revisar("renglón sin código se ignora", r2.Contados.Count == 2);

    var sinCols = new Xlsx.HojaSalida { Nombre = "Otra" };
    sinCols.Filas.Add(new List<Xlsx.Celda> { new Xlsx.Celda { Valor = "NO" }, new Xlsx.Celda { Valor = "PRODUCTO" }, new Xlsx.Celda { Valor = "COSTO" } });
    string ruta3 = Path.Combine(Path.GetTempPath(), "conteo_prueba3_" + Guid.NewGuid().ToString("N") + ".xlsx");
    Xlsx.Escribir(ruta3, new List<Xlsx.HojaSalida> { sinCols });
    Revisar("un Excel que no es hoja de conteo se rechaza con aviso", !ConteoExcel.Leer(Xlsx.Leer(ruta3)).HalloColumnas);
    File.Delete(ruta3);
}
finally { if (File.Exists(ruta)) File.Delete(ruta); }

Console.WriteLine(fallas == 0 ? "\nTodas las pruebas del conteo pasaron." : $"\n{fallas} prueba(s) fallaron.");
return fallas == 0 ? 0 : 1;
