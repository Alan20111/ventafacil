using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>Datos de VentaFácil que se pueden llenar desde una tabla de otro programa (Zafirah, un Excel…).</summary>
    public enum CampoZ { Codigo, Barras, Descripcion, Grupo, Proveedor, Unidad, Costo, Menudeo, Medio, DesdeMedio, Mayoreo, DesdeMayoreo, Existencia, Minimo, Almacen }

    public class InfoCampoZ
    {
        public CampoZ Campo;
        public string Nombre;
        public string Ayuda;
        public bool Obligatorio;
    }

    public class OpcionesZ
    {
        /// <summary>Si la tabla trae una fila por almacén: sumar las existencias de todos (si no, se usa solo AlmacenElegido).</summary>
        public bool SumarAlmacenes = true;
        public string AlmacenElegido = "";
        /// <summary>Clave del proveedor → su nombre (si el programa de origen guarda solo un número).</summary>
        public Dictionary<string, string> ProveedoresPorClave;
        /// <summary>Unidades que se venden a granel (kg, litro…). null = las de siempre. Se comparan sin acentos ni mayúsculas.</summary>
        public ICollection<string> UnidadesGranel;
    }

    public class ProductoZ
    {
        public int Fila;
        public string Codigo = "", Barras = "", Descripcion = "", Grupo = "", Proveedor = "";
        public bool Granel;
        public decimal Costo, Menudeo, Medio, DesdeMedio, Mayoreo, DesdeMayoreo;
        public decimal? Existencia, Minimo;
    }

    public class ResultadoZ
    {
        public List<ProductoZ> Productos = new List<ProductoZ>();
        public List<string> Avisos = new List<string>();
        public HashSet<CampoZ> Mapeados = new HashSet<CampoZ>();
        public int Filas, SinCodigo, SinDescripcion, SinPrecio, CodigosRepetidos, NivelesSinCantidad, PrecioBajoCosto, BarrasIgnorados;
        public bool Valido { get { return Productos.Count > 0; } }
    }

    /// <summary>
    /// Traducción de una tabla de otro programa (Zafirah, un Excel…) a productos de VentaFácil.
    /// No usa SQL ni pantallas: se prueba aparte (herramientas/pruebas-zafirah).
    ///
    /// NO ADIVINA EN SILENCIO: Sugerir() propone qué columna es cada dato, pero la pantalla lo muestra y se
    /// puede cambiar, con vista previa, antes de guardar nada. Los nombres reales de las columnas de la base de
    /// Zafirah se conocen por sus reportes (CodigoProducto, CodigoAlterno, Descripcion, Costo1, UltimoCosto,
    /// Precio1..4, PrecioNormal, LimitePrecio1..4, Existencia, NumAlmacen), pero no se han visto en una base real.
    /// </summary>
    public static class ZafirahMapeo
    {
        public static readonly InfoCampoZ[] Campos = {
            new InfoCampoZ { Campo = CampoZ.Codigo, Nombre = "Código del producto", Ayuda = "El código corto (el que se teclea en caja).", Obligatorio = true },
            new InfoCampoZ { Campo = CampoZ.Descripcion, Nombre = "Nombre del producto", Ayuda = "Cómo se llama.", Obligatorio = true },
            new InfoCampoZ { Campo = CampoZ.Menudeo, Nombre = "Precio de menudeo", Ayuda = "El precio normal al público. Los productos sin precio no se importan.", Obligatorio = true },
            new InfoCampoZ { Campo = CampoZ.Barras, Nombre = "Código de barras", Ayuda = "Se guarda como código de barras. Se ignora si es igual al código o está vacío." },
            new InfoCampoZ { Campo = CampoZ.Costo, Nombre = "Costo", Ayuda = "Lo que le cuesta. Sirve para calcular la ganancia." },
            new InfoCampoZ { Campo = CampoZ.Medio, Nombre = "Precio de medio mayoreo", Ayuda = "Solo aplica si también elige la cantidad mínima." },
            new InfoCampoZ { Campo = CampoZ.DesdeMedio, Nombre = "   … a partir de (cantidad)", Ayuda = "Desde cuántas piezas o kilos aplica el precio de medio mayoreo." },
            new InfoCampoZ { Campo = CampoZ.Mayoreo, Nombre = "Precio de mayoreo", Ayuda = "Solo aplica si también elige la cantidad mínima." },
            new InfoCampoZ { Campo = CampoZ.DesdeMayoreo, Nombre = "   … a partir de (cantidad)", Ayuda = "Desde cuántas piezas o kilos aplica el precio de mayoreo." },
            new InfoCampoZ { Campo = CampoZ.Existencia, Nombre = "Existencia (lo que hay)", Ayuda = "Cuánto hay en el negocio." },
            new InfoCampoZ { Campo = CampoZ.Minimo, Nombre = "Existencia mínima", Ayuda = "Para avisar cuando quede poco." },
            new InfoCampoZ { Campo = CampoZ.Almacen, Nombre = "Almacén / sucursal", Ayuda = "Si hay una fila por almacén para el mismo producto." },
            new InfoCampoZ { Campo = CampoZ.Grupo, Nombre = "Departamento / familia", Ayuda = "Para agrupar los productos." },
            new InfoCampoZ { Campo = CampoZ.Proveedor, Nombre = "Proveedor", Ayuda = "Su proveedor." },
            new InfoCampoZ { Campo = CampoZ.Unidad, Nombre = "Unidad (kg, pza…)", Ayuda = "Si es kilo o litro se vende a granel (con peso); si no, por pieza." }
        };

        // ------------------------------------------------------------------ nombres
        /// <summary>Minúsculas, sin acentos, solo letras y números: «Código_Producto» → «codigoproducto».</summary>
        public static string Norm(string texto)
        {
            var sb = new StringBuilder();
            foreach (char c in (texto ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        static readonly Dictionary<CampoZ, string[]> Exactos = new Dictionary<CampoZ, string[]>
        {
            { CampoZ.Codigo, new[] { "codigoproducto", "codigo", "codigoarticulo", "codart", "claveproducto", "clave", "cveart", "numarticulo", "sku" } },
            { CampoZ.Barras, new[] { "codigoalterno", "codigobarras", "codbarras", "barras", "ean", "upc", "codigoalt", "alterno" } },
            { CampoZ.Descripcion, new[] { "descripcion", "descripcionproducto", "nombreproducto", "nombre", "articulo", "producto" } },
            { CampoZ.Grupo, new[] { "departamento", "nombredepartamento", "familia", "linea", "grupo", "categoria" } },
            { CampoZ.Proveedor, new[] { "nombreproveedor", "proveedor", "numproveedor", "codigoproveedor", "clavedeproveedor" } },
            { CampoZ.Unidad, new[] { "unidad", "unidadventa", "unidadmedida", "um" } },
            { CampoZ.Costo, new[] { "ultimocosto", "costo1", "costo", "costopromedio", "costounitario" } },
            { CampoZ.Menudeo, new[] { "precionormal", "precio1", "precio", "preciopublico", "preciomenudeo" } },
            { CampoZ.Medio, new[] { "precio2" } },
            { CampoZ.Mayoreo, new[] { "precio3" } },
            { CampoZ.Existencia, new[] { "existencia", "existenciaactual", "stock", "cantidad" } },
            { CampoZ.Minimo, new[] { "minimo", "existenciaminima", "stockminimo", "puntoreorden", "min" } },
            { CampoZ.Almacen, new[] { "numalmacen", "codigoalmacen", "idalmacen", "almacen", "sucursal" } }
        };

        /// <summary>Las columnas candidatas de siempre para cada dato (una copia: sirve de base para un perfil).</summary>
        public static Dictionary<CampoZ, string[]> CandidatasPorDefecto()
        {
            var copia = new Dictionary<CampoZ, string[]>();
            foreach (var kv in Exactos) copia[kv.Key] = (string[])kv.Value.Clone();
            return copia;
        }

        /// <summary>
        /// De una lista de nombres candidatos, la columna de la tabla que corresponde. Si hay varias y el dato es numérico,
        /// elige la que trae más valores mayores que cero (p. ej. UltimoCosto vs Costo1). null si ninguna existe.
        /// </summary>
        public static string ElegirColumna(DataTable t, IEnumerable<string> candidatas, bool conDatos)
        {
            var halladas = new List<string>();
            foreach (string cand in candidatas)
            {
                string c = Norm(cand);
                foreach (DataColumn col in t.Columns)
                    if (Norm(col.ColumnName) == c && !halladas.Contains(col.ColumnName)) halladas.Add(col.ColumnName);
            }
            if (halladas.Count == 0) return null;
            string elegida = halladas[0];
            if (conDatos && halladas.Count > 1)
            {
                int mejor = Positivos(t, elegida);
                foreach (string otra in halladas.Skip(1).Take(2))
                {
                    int p = Positivos(t, otra);
                    if (p > mejor) { mejor = p; elegida = otra; }
                }
            }
            return elegida;
        }

        public static bool EsCampoNumerico(CampoZ campo) { return ConDatos.Contains(campo); }

        // Campos numéricos donde, si hay dos candidatas, conviene la que trae más datos con valor
        static readonly HashSet<CampoZ> ConDatos = new HashSet<CampoZ> { CampoZ.Costo, CampoZ.Menudeo, CampoZ.Medio, CampoZ.Mayoreo, CampoZ.Existencia };

        static int Positivos(DataTable t, string columna)
        {
            int n = 0;
            foreach (DataRow r in t.Rows) { decimal? v = Numero(r[columna]); if (v.HasValue && v.Value > 0) n++; }
            return n;
        }

        /// <summary>Qué columna parece ser cada dato. Solo propone: la persona lo confirma en pantalla.</summary>
        public static Dictionary<CampoZ, string> Sugerir(DataTable t)
        {
            var salida = new Dictionary<CampoZ, string>();
            var nombres = t.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            var usadas = new HashSet<string>();
            foreach (var kv in Exactos)
            {
                var halladas = new List<string>();
                foreach (string cand in kv.Value)
                    foreach (string col in nombres)
                        if (Norm(col) == cand && !halladas.Contains(col)) halladas.Add(col);
                halladas.RemoveAll(c => usadas.Contains(c));
                if (halladas.Count == 0) continue;
                string elegida = halladas[0];
                if (ConDatos.Contains(kv.Key) && halladas.Count > 1)
                {
                    int mejor = Positivos(t, elegida);
                    foreach (string otra in halladas.Skip(1).Take(2))
                    {
                        int p = Positivos(t, otra);
                        if (p > mejor) { mejor = p; elegida = otra; }
                    }
                }
                salida[kv.Key] = elegida;
                usadas.Add(elegida);
            }
            return salida;
        }

        // ------------------------------------------------------------------ valores
        public static string Texto(object o)
        {
            if (o == null || o is DBNull) return "";
            if (o is double) { double d = (double)o; return d == Math.Floor(d) && Math.Abs(d) < 1e15 ? d.ToString("0", CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture); }
            if (o is float) return Texto((double)(float)o);
            if (o is decimal) { decimal d = (decimal)o; return d == Math.Floor(d) ? d.ToString("0", CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture); }
            if (o is IFormattable) return ((IFormattable)o).ToString(null, CultureInfo.InvariantCulture).Trim();
            return Convert.ToString(o, CultureInfo.InvariantCulture).Trim();
        }

        /// <summary>Número o null si está vacío o no es un número.</summary>
        public static decimal? Numero(object o)
        {
            if (o == null || o is DBNull) return null;
            if (o is double || o is float || o is decimal) return Dinero.Leer(o);
            if (o is int || o is long || o is short || o is byte) return Convert.ToDecimal(o, CultureInfo.InvariantCulture);
            string s = Convert.ToString(o, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s)) return null;
            decimal d;
            return Fmt.TryDec(s, out d) ? (decimal?)Dinero.Leer(d) : null;
        }

        static readonly HashSet<string> UnidadesDeGranel = new HashSet<string> { "kg", "kgs", "kilo", "kilos", "kilogramo", "kilogramos", "gr", "grs", "gramo", "gramos", "lt", "lts", "litro", "litros", "mt", "mts", "metro", "metros", "ton", "tonelada" };

        // ------------------------------------------------------------------ proveedores
        /// <summary>Tabla de proveedores → clave y nombre, si se reconocen las columnas.</summary>
        public static Dictionary<string, string> DiccionarioProveedores(DataTable t)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (t == null) return d;
            string clave = Buscar(t, "codigoproveedor", "numproveedor", "claveproveedor", "idproveedor", "codigo", "clave", "id");
            string nombre = Buscar(t, "nombreproveedor", "razonsocial", "nombrecomercial", "nombre", "descripcion");
            if (clave == null || nombre == null) return d;
            foreach (DataRow r in t.Rows)
            {
                string k = Texto(r[clave]), n = Texto(r[nombre]);
                if (k != "" && n != "" && !d.ContainsKey(k)) d[k] = n;
            }
            return d;
        }

        static string Buscar(DataTable t, params string[] candidatos)
        {
            foreach (string c in candidatos)
                foreach (DataColumn col in t.Columns)
                    if (Norm(col.ColumnName) == c) return col.ColumnName;
            return null;
        }

        // ------------------------------------------------------------------ transformar
        public static ResultadoZ Transformar(DataTable origen, Dictionary<CampoZ, string> mapeo, OpcionesZ op)
        {
            op = op ?? new OpcionesZ();
            var res = new ResultadoZ { Filas = origen.Rows.Count };
            foreach (var kv in mapeo) if (!string.IsNullOrEmpty(kv.Value)) res.Mapeados.Add(kv.Key);

            foreach (var campo in Campos)
                if (campo.Obligatorio && !res.Mapeados.Contains(campo.Campo)) res.Avisos.Add("Falta elegir la columna de: " + campo.Nombre.Trim() + ".");
            foreach (var kv in mapeo)
                if (!string.IsNullOrEmpty(kv.Value) && !origen.Columns.Contains(kv.Value)) { res.Avisos.Add("La columna «" + kv.Value + "» ya no existe en la tabla."); res.Mapeados.Remove(kv.Key); }
            if (res.Avisos.Count > 0) return res;

            Func<CampoZ, string> col = c => res.Mapeados.Contains(c) ? mapeo[c] : null;
            bool sumar = op.SumarAlmacenes || col(CampoZ.Almacen) == null;
            var porCodigo = new Dictionary<string, ProductoZ>(StringComparer.OrdinalIgnoreCase);
            var granel = op.UnidadesGranel == null ? UnidadesDeGranel : new HashSet<string>(op.UnidadesGranel.Select(u => Norm(u)));
            var sinNombreProveedor = 0;
            int numFila = 0;

            foreach (DataRow r in origen.Rows)
            {
                numFila++;
                string codigo = Texto(r[col(CampoZ.Codigo)]);
                if (codigo == "") { res.SinCodigo++; continue; }
                string descripcion = Texto(r[col(CampoZ.Descripcion)]).Trim();
                if (descripcion == "") { res.SinDescripcion++; continue; }

                decimal? existenciaFila = col(CampoZ.Existencia) != null ? Numero(r[col(CampoZ.Existencia)]) : null;
                string almacenFila = col(CampoZ.Almacen) != null ? Texto(r[col(CampoZ.Almacen)]) : "";

                ProductoZ p;
                if (porCodigo.TryGetValue(codigo, out p))
                {
                    // El mismo producto otra vez: una fila por almacén (se suma o se escoge) o un repetido de verdad
                    if (col(CampoZ.Almacen) != null)
                    {
                        if (existenciaFila.HasValue)
                        {
                            if (sumar) p.Existencia = (p.Existencia ?? 0m) + existenciaFila.Value;
                            else if (string.Equals(almacenFila, op.AlmacenElegido, StringComparison.OrdinalIgnoreCase)) p.Existencia = (p.Existencia ?? 0m) + existenciaFila.Value;
                        }
                    }
                    else res.CodigosRepetidos++;
                    continue;
                }

                p = new ProductoZ { Fila = numFila, Codigo = codigo, Descripcion = descripcion };

                p.Menudeo = Numero(r[col(CampoZ.Menudeo)]) ?? 0m;
                if (col(CampoZ.Costo) != null) p.Costo = Numero(r[col(CampoZ.Costo)]) ?? 0m;

                if (col(CampoZ.Barras) != null)
                {
                    string b = Texto(r[col(CampoZ.Barras)]);
                    bool soloCeros = b.Length > 0 && b.All(ch => ch == '0');
                    if (b == "" || soloCeros || string.Equals(b, codigo, StringComparison.OrdinalIgnoreCase)) b = "";
                    else if (b.Length > 40 || b.IndexOfAny(new[] { ' ', '\t' }) >= 0) { res.BarrasIgnorados++; b = ""; }
                    p.Barras = b;
                }

                if (col(CampoZ.Grupo) != null) p.Grupo = Texto(r[col(CampoZ.Grupo)]).ToUpperInvariant();
                if (col(CampoZ.Proveedor) != null)
                {
                    string pv = Texto(r[col(CampoZ.Proveedor)]);
                    string nombre;
                    if (pv != "" && op.ProveedoresPorClave != null && op.ProveedoresPorClave.TryGetValue(pv, out nombre)) pv = nombre;
                    else if (pv != "" && op.ProveedoresPorClave != null && op.ProveedoresPorClave.Count > 0) sinNombreProveedor++;
                    p.Proveedor = pv.ToUpperInvariant();
                }
                if (col(CampoZ.Unidad) != null) p.Granel = granel.Contains(Norm(Texto(r[col(CampoZ.Unidad)])));
                if (col(CampoZ.Minimo) != null) p.Minimo = Numero(r[col(CampoZ.Minimo)]);

                // Niveles de precio: solo valen con su cantidad mínima (si no, se ignoran y se avisa)
                bool avisoNivel = false;
                decimal precioMedio = col(CampoZ.Medio) != null ? Numero(r[col(CampoZ.Medio)]) ?? 0m : 0m;
                decimal desdeMedio = col(CampoZ.DesdeMedio) != null ? Numero(r[col(CampoZ.DesdeMedio)]) ?? 0m : 0m;
                decimal precioMayoreo = col(CampoZ.Mayoreo) != null ? Numero(r[col(CampoZ.Mayoreo)]) ?? 0m : 0m;
                decimal desdeMayoreo = col(CampoZ.DesdeMayoreo) != null ? Numero(r[col(CampoZ.DesdeMayoreo)]) ?? 0m : 0m;
                if (precioMedio > 0) { if (desdeMedio > 0) { p.Medio = precioMedio; p.DesdeMedio = desdeMedio; } else avisoNivel = true; }
                if (precioMayoreo > 0) { if (desdeMayoreo > 0) { p.Mayoreo = precioMayoreo; p.DesdeMayoreo = desdeMayoreo; } else avisoNivel = true; }
                if (avisoNivel) res.NivelesSinCantidad++;

                if (existenciaFila.HasValue)
                {
                    if (sumar || string.Equals(almacenFila, op.AlmacenElegido, StringComparison.OrdinalIgnoreCase)) p.Existencia = existenciaFila.Value;
                }
                if (col(CampoZ.Existencia) != null && !p.Existencia.HasValue) p.Existencia = 0m;

                if (p.Menudeo <= 0) { res.SinPrecio++; porCodigo[codigo] = p; continue; }   // se recuerda el código para no contarlo como repetido
                if (p.Costo > 0 && p.Menudeo < p.Costo) res.PrecioBajoCosto++;
                if (p.Grupo == "") p.Grupo = "GENERAL";
                porCodigo[codigo] = p;
                res.Productos.Add(p);
            }

            // Los productos que solo aparecieron en una fila con otro almacén no tienen existencia en el elegido
            if (col(CampoZ.Almacen) != null && !sumar) foreach (var p in res.Productos) if (col(CampoZ.Existencia) != null && !p.Existencia.HasValue) p.Existencia = 0m;

            if (res.SinCodigo > 0) res.Avisos.Add(res.SinCodigo + " filas sin código (se omiten).");
            if (res.SinDescripcion > 0) res.Avisos.Add(res.SinDescripcion + " filas sin nombre (se omiten).");
            if (res.SinPrecio > 0) res.Avisos.Add(res.SinPrecio + " productos sin precio de menudeo (se omiten: no se podrían cobrar).");
            if (res.CodigosRepetidos > 0) res.Avisos.Add(res.CodigosRepetidos + " códigos repetidos (se usó la primera fila de cada uno).");
            if (res.NivelesSinCantidad > 0) res.Avisos.Add(res.NivelesSinCantidad + " productos traen precio de medio mayoreo/mayoreo pero sin cantidad mínima: ese precio no se aplica. Elija la columna «a partir de».");
            if (res.PrecioBajoCosto > 0) res.Avisos.Add(res.PrecioBajoCosto + " productos con precio MENOR a su costo. Revise si la columna de costo o de precio es la correcta.");
            if (res.BarrasIgnorados > 0) res.Avisos.Add(res.BarrasIgnorados + " códigos de barras no válidos (con espacios o muy largos) se ignoraron.");
            if (sinNombreProveedor > 0) res.Avisos.Add(sinNombreProveedor + " productos tienen un proveedor que no se encontró en la tabla de proveedores (se deja la clave).");
            if (col(CampoZ.Unidad) == null) res.Avisos.Add("Sin columna de unidad: todos se importan como PIEZA (si vende kilos, márquelos después).");
            return res;
        }
    }
}
