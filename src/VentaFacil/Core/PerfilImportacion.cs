using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>
    /// Un PERFIL de importación dice, en un archivo de texto (JSON) que se edita a mano o se crea desde el asistente,
    /// todo lo que depende del programa de origen: en qué base y tabla están los productos y cómo se llama cada columna.
    /// Así adaptar la importación a otra versión de Zafirah u otro programa es copiar un archivo y cambiar nombres,
    /// sin programar ni recompilar. El perfil solo PROPONE: el asistente siempre muestra el mapeo y la vista previa.
    ///
    /// Ejemplo (C:\VentaFacil\perfiles\mi_programa.json):
    /// {
    ///   "nombre": "Mi programa",
    ///   "base": "mibase",
    ///   "tablas": ["Articulos", "*PRODUCTO*"],         // nombre exacto, o *texto* = que lo contenga
    ///   "tablaProveedores": "Proveedores",
    ///   "columnas": { "Codigo": ["Clave", "Cod"], "Descripcion": ["Nombre"], "Menudeo": ["Precio1"] },
    ///   "reglas": { "sumarAlmacenes": true, "unidadesGranel": ["kg", "litro"] }
    /// }
    /// Los datos que el perfil no menciona se detectan con las reglas generales; los que menciona y no encuentra
    /// se dejan sin elegir (no se adivinan).
    /// </summary>
    public class PerfilImportacion
    {
        public string Nombre = "", Descripcion = "", Base = "", TablaProveedores = "", Origen = "";
        public List<string> Tablas = new List<string>();
        public Dictionary<CampoZ, List<string>> Columnas = new Dictionary<CampoZ, List<string>>();
        public bool SumarAlmacenes = true;
        public List<string> UnidadesGranel;   // null = las de siempre

        public bool Integrado { get { return Origen == "integrado"; } }
        public override string ToString() { return Nombre + (Integrado ? "  (incluido)" : ""); }

        // ------------------------------------------------------------------ el perfil incluido
        public static PerfilImportacion Zafirah()
        {
            var p = new PerfilImportacion
            {
                Nombre = "Zafirah 2008 (smartdatabase)",
                Descripcion = "Suite Zafirah 2008: base smartdatabase, tabla ZFRADMARTICULOS. Los nombres de columnas salen de sus reportes; se confirman en pantalla.",
                Base = "smartdatabase", TablaProveedores = "ZFRADMPROVEEDORES", Origen = "integrado"
            };
            p.Tablas.Add("ZFRADMARTICULOS");
            p.Tablas.Add("*ARTICULO*");
            // Con sus nombres reales (los de los reportes de Zafirah): así se leen bien si alguien exporta este perfil como plantilla.
            // Las cantidades «a partir de» (LimitePrecio…) NO se listan a propósito: se eligen viendo los datos.
            p.Columnas[CampoZ.Codigo] = new List<string> { "CodigoProducto", "Codigo", "CodigoArticulo", "CodArt", "Clave" };
            p.Columnas[CampoZ.Barras] = new List<string> { "CodigoAlterno", "CodigoBarras", "CodBarras", "Barras", "EAN" };
            p.Columnas[CampoZ.Descripcion] = new List<string> { "Descripcion", "NombreProducto", "Nombre", "Articulo" };
            p.Columnas[CampoZ.Grupo] = new List<string> { "Departamento", "Familia", "Linea", "Grupo", "Categoria" };
            p.Columnas[CampoZ.Proveedor] = new List<string> { "NombreProveedor", "Proveedor", "NumProveedor", "CodigoProveedor" };
            p.Columnas[CampoZ.Unidad] = new List<string> { "Unidad", "UnidadVenta" };
            p.Columnas[CampoZ.Costo] = new List<string> { "UltimoCosto", "Costo1", "Costo" };
            p.Columnas[CampoZ.Menudeo] = new List<string> { "PrecioNormal", "Precio1", "Precio" };
            p.Columnas[CampoZ.Medio] = new List<string> { "Precio2" };
            p.Columnas[CampoZ.Mayoreo] = new List<string> { "Precio3" };
            p.Columnas[CampoZ.Existencia] = new List<string> { "Existencia", "ExistenciaActual" };
            p.Columnas[CampoZ.Minimo] = new List<string> { "Minimo", "ExistenciaMinima", "StockMinimo" };
            p.Columnas[CampoZ.Almacen] = new List<string> { "NumAlmacen", "Almacen" };
            return p;
        }

        // ------------------------------------------------------------------ leer
        static string Txt(IDictionary<string, object> d, string clave)
        {
            object v;
            return d.TryGetValue(clave, out v) && v is string ? ((string)v).Trim() : "";
        }

        static List<string> Lista(object v)
        {
            var l = new List<string>();
            if (v is string) { if (((string)v).Trim() != "") l.Add(((string)v).Trim()); }
            else if (v is IEnumerable<object>) foreach (object o in (IEnumerable<object>)v) { if (o is string && ((string)o).Trim() != "") l.Add(((string)o).Trim()); }
            return l;
        }

        public static PerfilImportacion Desde(string json, string origen)
        {
            var raiz = JsonMini.Leer(json) as Dictionary<string, object>;
            if (raiz == null) throw new FormatException("El perfil debe ser un objeto { … }.");
            var p = new PerfilImportacion { Origen = origen };
            p.Nombre = Txt(raiz, "nombre");
            if (p.Nombre == "") p.Nombre = Path.GetFileNameWithoutExtension(origen ?? "") ?? "Perfil";
            p.Descripcion = Txt(raiz, "descripcion");
            p.Base = Txt(raiz, "base");
            p.TablaProveedores = Txt(raiz, "tablaProveedores");
            object t;
            if (raiz.TryGetValue("tablas", out t)) p.Tablas = Lista(t);

            object cols;
            if (raiz.TryGetValue("columnas", out cols))
            {
                var dic = cols as Dictionary<string, object>;
                if (dic == null) throw new FormatException("«columnas» debe ser un objeto { \"Codigo\": [\"…\"], … }.");
                foreach (var kv in dic)
                {
                    CampoZ campo;
                    if (!Enum.TryParse(kv.Key, true, out campo) || !Enum.IsDefined(typeof(CampoZ), campo))
                        throw new FormatException("«" + kv.Key + "» no es un dato de VentaFácil. Los válidos son: " + string.Join(", ", Enum.GetNames(typeof(CampoZ))) + ".");
                    p.Columnas[campo] = Lista(kv.Value);
                }
            }

            object reglas;
            if (raiz.TryGetValue("reglas", out reglas) && reglas is Dictionary<string, object>)
            {
                var r = (Dictionary<string, object>)reglas;
                object v;
                if (r.TryGetValue("sumarAlmacenes", out v) && v is bool) p.SumarAlmacenes = (bool)v;
                if (r.TryGetValue("unidadesGranel", out v)) p.UnidadesGranel = Lista(v);
            }
            return p;
        }

        /// <summary>El perfil incluido más los archivos *.json de la carpeta. Los archivos con errores se saltan y se explican en `problemas`.</summary>
        public static List<PerfilImportacion> Cargar(string carpeta, out List<string> problemas)
        {
            problemas = new List<string>();
            var lista = new List<PerfilImportacion> { Zafirah() };
            if (string.IsNullOrEmpty(carpeta) || !Directory.Exists(carpeta)) return lista;
            var archivos = Directory.GetFiles(carpeta, "*.json");
            Array.Sort(archivos, StringComparer.OrdinalIgnoreCase);
            foreach (string ruta in archivos)
            {
                try { lista.Add(Desde(File.ReadAllText(ruta, Encoding.UTF8), ruta)); }
                catch (Exception ex) { problemas.Add(Path.GetFileName(ruta) + ": " + ex.Message); }
            }
            return lista;
        }

        // ------------------------------------------------------------------ escribir
        public static PerfilImportacion DesdeMapeo(string nombre, Dictionary<CampoZ, string> mapeo, bool sumarAlmacenes)
        {
            var p = new PerfilImportacion { Nombre = nombre, SumarAlmacenes = sumarAlmacenes, Descripcion = "Creado desde el asistente de importación." };
            foreach (var kv in mapeo) if (!string.IsNullOrEmpty(kv.Value)) p.Columnas[kv.Key] = new List<string> { kv.Value };
            return p;
        }

        public string ComoJson()
        {
            var raiz = new Dictionary<string, object>();
            raiz["nombre"] = Nombre;
            if (Descripcion != "") raiz["descripcion"] = Descripcion;
            if (Base != "") raiz["base"] = Base;
            if (Tablas.Count > 0) raiz["tablas"] = Tablas.Cast<object>().ToList();
            if (TablaProveedores != "") raiz["tablaProveedores"] = TablaProveedores;
            var cols = new Dictionary<string, object>();
            foreach (CampoZ campo in Enum.GetValues(typeof(CampoZ))) { List<string> c; if (Columnas.TryGetValue(campo, out c) && c.Count > 0) cols[campo.ToString()] = c.Cast<object>().ToList(); }
            raiz["columnas"] = cols;
            var reglas = new Dictionary<string, object>();
            reglas["sumarAlmacenes"] = SumarAlmacenes;
            if (UnidadesGranel != null) reglas["unidadesGranel"] = UnidadesGranel.Cast<object>().ToList();
            raiz["reglas"] = reglas;
            return JsonMini.Escribir(raiz) + "\n";
        }

        /// <summary>Nombre de archivo seguro para guardar el perfil.</summary>
        public static string ArchivoPara(string nombre)
        {
            var sb = new StringBuilder();
            foreach (char c in (nombre ?? "").Trim())
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == '.' ? '_' : c);
            string s = sb.ToString().Trim('_', ' ');
            return (s == "" ? "perfil" : s) + ".json";
        }

        // ------------------------------------------------------------------ usar
        /// <summary>Qué tan buena candidata es una tabla por su nombre (100 = nombre exacto, 50 = lo contiene, 0 = no).</summary>
        public int PuntajeTabla(string nombreTabla)
        {
            for (int i = 0; i < Tablas.Count; i++)
            {
                string t = Tablas[i];
                if (t.Length > 2 && t.StartsWith("*") && t.EndsWith("*"))
                { if ((nombreTabla ?? "").IndexOf(t.Trim('*'), StringComparison.OrdinalIgnoreCase) >= 0) return 50 - i; }
                else if (string.Equals(t, nombreTabla, StringComparison.OrdinalIgnoreCase)) return 100 - i;
            }
            return 0;
        }

        /// <summary>Columna de la tabla para cada dato. Lo que el perfil menciona y no encuentra queda sin elegir; lo que no menciona se detecta.</summary>
        public Dictionary<CampoZ, string> Sugerir(DataTable t)
        {
            var salida = new Dictionary<CampoZ, string>();
            var general = ZafirahMapeo.Sugerir(t);
            foreach (CampoZ campo in Enum.GetValues(typeof(CampoZ)))
            {
                List<string> cand;
                if (Columnas.TryGetValue(campo, out cand))
                {
                    string col = ZafirahMapeo.ElegirColumna(t, cand, ZafirahMapeo.EsCampoNumerico(campo));
                    if (col != null) salida[campo] = col;
                }
                else
                {
                    string g;
                    if (general.TryGetValue(campo, out g)) salida[campo] = g;
                }
            }
            return salida;
        }

        public OpcionesZ AplicarA(OpcionesZ op)
        {
            op.UnidadesGranel = UnidadesGranel;
            return op;
        }
    }
}
