using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>
    /// Lista de costos estilo semillera (archivo "COSTOS SEMILLERA.xlsx"):
    ///   NO | STOCK | PRODUCTO | BTO (costo del bulto) | CANT (kg que trae) | COSTO (por kg) |
    ///   MAYOREO | CANT | MED. MAY. | CANT | MENUDEO | L M M J V S D …
    /// Los renglones con texto solo en la columna A son el proveedor o grupo de los productos que siguen.
    /// </summary>
    public static class ExcelSemillera
    {
        // ======================================================================
        //  Importación
        // ======================================================================
        public static bool Importar(IWin32Window dueno)
        {
            string ruta;
            using (var d = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Lista de costos de la semillera" })
            {
                if (d.ShowDialog(dueno) != DialogResult.OK) return false;
                ruta = d.FileName;
            }
            List<ListaCostos.Renglon> datos;
            List<string> avisos;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                datos = ListaCostos.Leer(ruta, out avisos);
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo leer el archivo. ¿Es un .xlsx de Excel?"); return false; }
            finally { Cursor.Current = Cursors.Default; }
            if (datos.Count == 0) { Ui.Aviso("No se encontraron productos en el archivo." + (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos) : "")); return false; }

            using (var f = new FormVistaImportacion(datos, avisos))
            {
                if (f.ShowDialog(dueno) != DialogResult.OK) return false;
                return Aplicar(datos, f.StockEsExistencia, f.ActualizarExistentes);
            }
        }

        static bool Aplicar(List<ListaCostos.Renglon> datos, bool stockEsExistencia, bool actualizar)
        {
            int nuevos = 0, actualizados = 0, omitidos = 0;
            var errores = new List<string>();
            long usuario = Sesion.Usuario != null ? Sesion.Usuario.Id : 0;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                using (var cn = Db.Abrir())
                using (var tx = cn.BeginTransaction())
                {
                    var max = Db.Scalar(cn, tx, "SELECT MAX(CAST(codigo AS INTEGER)) FROM productos WHERE codigo GLOB '[0-9]*' AND LENGTH(codigo) <= 6");
                    long siguiente = (max == null ? 0 : Convert.ToInt64(max)) + 1;
                    var deptos = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                    foreach (var x in datos)
                    {
                        try
                        {
                            decimal menudeo = x.Menudeo ?? x.Medio ?? x.Mayoreo ?? 0;
                            if (menudeo <= 0) { omitidos++; continue; }

                            string grupoP = x.Grupo == "" ? "GENERAL" : x.Grupo.ToUpperInvariant();
                            var idExistente = Db.Scalar(cn, tx, "SELECT id FROM productos WHERE descripcion=@p0 COLLATE NOCASE AND (proveedor=@p1 COLLATE NOCASE OR proveedor='') ORDER BY proveedor DESC LIMIT 1", x.Producto, grupoP);
                            Producto p;
                            if (idExistente != null)
                            {
                                if (!actualizar) { omitidos++; continue; }
                                p = Db.Lista(cn, tx, "SELECT p.*, '' AS departamento FROM productos p WHERE p.id=@p0", Producto.De, Convert.ToInt64(idExistente)).First();
                            }
                            else p = new Producto { Codigo = (siguiente++).ToString(), UsaInventario = true, Activo = true };

                            string grupo = grupoP;
                            long dep;
                            if (!deptos.TryGetValue(grupo, out dep)) { dep = DepartamentoRepo.ObtenerOCrear(cn, tx, grupo); deptos[grupo] = dep; }

                            p.Descripcion = x.Producto;
                            p.DepartamentoId = dep;
                            p.Proveedor = grupo;
                            p.CostoBulto = x.Bulto ?? 0;
                            p.CantBulto = x.Trae ?? 0;
                            p.Costo = x.Costo ?? 0;
                            p.Precio = menudeo;
                            bool hayMedio = x.Medio.HasValue && x.Medio > 0 && x.MedioDesde.HasValue && x.Menudeo.HasValue;
                            p.PrecioMedio = hayMedio ? x.Medio.Value : 0;
                            p.MedioDesde = hayMedio ? x.MedioDesde.Value : 0;
                            bool hayMayoreo = x.Mayoreo.HasValue && x.Mayoreo > 0 && x.MayoreoDesde.HasValue;
                            p.PrecioMayoreo = hayMayoreo ? x.Mayoreo.Value : 0;
                            p.MayoreoDesde = hayMayoreo ? x.MayoreoDesde.Value : 0;
                            p.Granel = !x.PorPieza;
                            p.Orden = x.Orden;
                            if (!stockEsExistencia && x.Stock.HasValue) p.Minimo = x.Stock.Value;

                            bool esNuevo = p.Id == 0;
                            if (esNuevo && stockEsExistencia) p.Existencia = x.Stock ?? 0;
                            ProductoRepo.Guardar(cn, tx, p, usuario);
                            if (!esNuevo && stockEsExistencia && x.Stock.HasValue)
                            {
                                decimal actual = Db.Dec(Db.Scalar(cn, tx, "SELECT existencia FROM productos WHERE id=@p0", p.Id));
                                if (actual != x.Stock.Value)
                                    ProductoRepo.MoverExistencia(cn, tx, p.Id, x.Stock.Value - actual, "AJUSTE", "Importación lista de costos", usuario);
                            }
                            if (esNuevo) nuevos++; else actualizados++;
                        }
                        catch (Exception ex) { errores.Add(x.Producto + ": " + ex.Message); }
                    }
                    tx.Commit();
                }
            }
            catch (Exception ex) { Ui.Error(ex, "Importación"); return false; }
            finally { Cursor.Current = Cursors.Default; }

            Ui.Info("Importación terminada.\n\nProductos nuevos: " + nuevos + "\nActualizados: " + actualizados + "\nOmitidos (sin precio o ya existían): " + omitidos +
                (errores.Count > 0 ? "\nCon error: " + errores.Count + "\n\n" + string.Join("\n", errores.Take(10)) : "") +
                "\n\nA los productos nuevos se les dio un código corto (1, 2, 3…) para teclearlo en caja. Puede cambiarlo o ponerles código de barras.");
            return true;
        }

        // ======================================================================
        //  Exportación: lista de costos con el mismo formato
        // ======================================================================
        static Xlsx.Celda C(object v, Xlsx.Estilo e = Xlsx.Estilo.Normal, string formula = null) { return new Xlsx.Celda { Valor = v, Estilo = e, Formula = formula }; }

        static string CantTexto(decimal desde, bool granel) { return desde > 0 ? Fmt.Dec(desde) + (granel ? "KG" : "PZ") : ""; }

        public static void ExportarLista(IWin32Window dueno)
        {
            string ruta;
            using (var d = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "COSTOS " + Config.Actual.NegocioNombre + " " + DateTime.Now.ToString("dd-MM-yyyy") + ".xlsx" })
            {
                if (d.ShowDialog(dueno) != DialogResult.OK) return;
                ruta = d.FileName;
            }
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                var productos = Db.Lista("SELECT p.*, IFNULL(d.nombre,'') AS departamento FROM productos p LEFT JOIN departamentos d ON d.id=p.departamento_id WHERE p.activo=1 ORDER BY p.orden, p.id", Producto.De);
                var grupos = productos.GroupBy(p => p.Proveedor != "" ? p.Proveedor : (p.Departamento != "" ? p.Departamento : "SIN PROVEEDOR"));

                // --- Hoja 1: costos (igual que la de la semillera) ---
                var costos = new Xlsx.HojaSalida { Nombre = "COSTOS", FilasCongeladas = 3 };
                costos.AnchosColumna.AddRange(new double[] { 6, 8, 34, 11, 7, 11, 11, 7, 11, 7, 11 });
                for (int i = 0; i < 14; i++) costos.AnchosColumna.Add(3.5);
                costos.Filas.Add(new List<Xlsx.Celda> { C(Config.Actual.NegocioNombre + " — LISTA DE COSTOS Y PRECIOS  " + DateTime.Now.ToString("dd/MM/yyyy"), Xlsx.Estilo.Titulo) });
                costos.Filas.Add(new List<Xlsx.Celda>());
                var enc = new List<Xlsx.Celda>();
                foreach (var t in new[] { "NO", "STOCK", "PRODUCTO", "BTO", "CANT", "COSTO", "MAYOREO", "CANT", "MED. MAY.", "CANT", "MENUDEO", "L", "M", "M", "J", "V", "S", "D", "L", "M", "M", "J", "V", "S", "D" })
                    enc.Add(C(t, Xlsx.Estilo.Encabezado));
                costos.Filas.Add(enc);

                foreach (var g in grupos)
                {
                    costos.Filas.Add(new List<Xlsx.Celda> { C(g.Key, Xlsx.Estilo.Grupo), C(null, Xlsx.Estilo.Grupo), C(null, Xlsx.Estilo.Grupo) });
                    int no = 0;
                    foreach (var p in g)
                    {
                        int fila = costos.Filas.Count + 1; // número de renglón en Excel
                        bool conBulto = p.CostoBulto > 0 && p.CantBulto > 0;
                        costos.Filas.Add(new List<Xlsx.Celda>
                        {
                            C(++no, Xlsx.Estilo.Numero),
                            C(p.UsaInventario ? (object)p.Existencia : null, Xlsx.Estilo.Numero),
                            C(p.Descripcion),
                            C(p.CostoBulto > 0 ? (object)p.CostoBulto : null, Xlsx.Estilo.Dinero),
                            C(p.CantBulto > 0 ? (object)p.CantBulto : null, Xlsx.Estilo.Numero),
                            C(p.Costo, Xlsx.Estilo.Dinero, conBulto ? "D" + fila + "/E" + fila : null),
                            C(p.PrecioMayoreo > 0 ? (object)p.PrecioMayoreo : null, Xlsx.Estilo.Dinero),
                            C(CantTexto(p.MayoreoDesde, p.Granel)),
                            C(p.PrecioMedio > 0 ? (object)p.PrecioMedio : null, Xlsx.Estilo.Dinero),
                            C(CantTexto(p.MedioDesde, p.Granel)),
                            C(p.Precio, Xlsx.Estilo.DineroNegrita)
                        });
                    }
                }

                // --- Hoja 2: precios para mostrador (sin costos) ---
                var precios = new Xlsx.HojaSalida { Nombre = "PRECIOS MOSTRADOR", FilasCongeladas = 2 };
                precios.AnchosColumna.AddRange(new double[] { 8, 36, 13, 13, 13 });
                precios.Filas.Add(new List<Xlsx.Celda> { C(Config.Actual.NegocioNombre + " — PRECIOS " + DateTime.Now.ToString("dd/MM/yyyy"), Xlsx.Estilo.Titulo) });
                precios.Filas.Add(new List<Xlsx.Celda> { C("CÓDIGO", Xlsx.Estilo.Encabezado), C("PRODUCTO", Xlsx.Estilo.Encabezado), C("MENUDEO", Xlsx.Estilo.Encabezado), C("MED. MAY.", Xlsx.Estilo.Encabezado), C("MAYOREO", Xlsx.Estilo.Encabezado) });
                foreach (var p in productos.OrderBy(x => x.Descripcion))
                {
                    precios.Filas.Add(new List<Xlsx.Celda>
                    {
                        C(p.Codigo), C(p.Descripcion + (p.Granel ? "" : " (pz)")), C(p.Precio, Xlsx.Estilo.DineroNegrita),
                        C(p.PrecioMedio > 0 ? Fmt.Dinero(p.PrecioMedio) + " (" + CantTexto(p.MedioDesde, p.Granel) + ")" : ""),
                        C(p.PrecioMayoreo > 0 ? Fmt.Dinero(p.PrecioMayoreo) + " (" + CantTexto(p.MayoreoDesde, p.Granel) + ")" : "")
                    });
                }

                Xlsx.Escribir(ruta, new List<Xlsx.HojaSalida> { costos, precios });
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo crear el archivo (¿está abierto en Excel?)"); return; }
            finally { Cursor.Current = Cursors.Default; }
            Abrir(ruta);
        }

        static void Abrir(string ruta)
        {
            if (Ui.Confirmar("Archivo guardado:\n" + ruta + "\n\n¿Abrirlo ahora?", "Excel"))
                try { Process.Start(ruta); } catch (Exception ex) { Ui.Error(ex, "Abrir archivo"); }
        }

        // ======================================================================
        //  Exportar cualquier reporte (DataTable) a Excel con el mismo estilo
        // ======================================================================
        static readonly string[] PalabrasDinero = { "TOTAL", "IMPORTE", "COSTO", "PRECIO", "GANANCIA", "VALOR", "FONDO", "MONTO", "EFECTIVO", "DECLARADO", "VENTA", "DESCUENTO", "UTILIDAD", "CAMBIO", "PAGO" };

        public static void ExportarTabla(DataTable dt, string nombre, string titulo = null)
        {
            if (dt == null || dt.Rows.Count == 0) { Ui.Aviso("No hay datos para exportar."); return; }
            string ruta;
            using (var d = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = nombre + " " + DateTime.Now.ToString("dd-MM-yyyy") + ".xlsx" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                ruta = d.FileName;
            }
            try
            {
                var h = new Xlsx.HojaSalida { Nombre = nombre.ToUpperInvariant(), FilasCongeladas = 3 };
                h.Filas.Add(new List<Xlsx.Celda> { C(Config.Actual.NegocioNombre + " — " + (titulo ?? nombre).ToUpperInvariant(), Xlsx.Estilo.Titulo) });
                h.Filas.Add(new List<Xlsx.Celda> { C("Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm")) });
                var enc = new List<Xlsx.Celda>();
                var dinero = new bool[dt.Columns.Count];
                var numerica = new bool[dt.Columns.Count];
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    var col = dt.Columns[i];
                    string n = col.ColumnName.ToUpperInvariant();
                    var tipo = col.DataType;
                    numerica[i] = tipo == typeof(double) || tipo == typeof(decimal) || tipo == typeof(long) || tipo == typeof(int) || tipo == typeof(float);
                    dinero[i] = numerica[i] && PalabrasDinero.Any(p => n.Contains(p));
                    enc.Add(C(col.ColumnName.ToUpperInvariant(), Xlsx.Estilo.Encabezado));
                    h.AnchosColumna.Add(Math.Max(10, Math.Min(40, col.ColumnName.Length + 4)));
                }
                h.Filas.Add(enc);
                var totales = new decimal[dt.Columns.Count];
                foreach (DataRow r in dt.Rows)
                {
                    var fila = new List<Xlsx.Celda>();
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        object v = r[i] == DBNull.Value ? null : r[i];
                        if (numerica[i] && v != null) totales[i] += Db.Dec(v);
                        if (v is string && ((string)v).Length + 2 > h.AnchosColumna[i]) h.AnchosColumna[i] = Math.Min(50, ((string)v).Length + 2);
                        fila.Add(C(v, dinero[i] ? Xlsx.Estilo.Dinero : numerica[i] ? Xlsx.Estilo.Numero : Xlsx.Estilo.Normal));
                    }
                    h.Filas.Add(fila);
                }
                // Renglón de totales para las columnas de dinero
                if (dinero.Any(x => x))
                {
                    var tot = new List<Xlsx.Celda>();
                    int primera = 4, ultima = h.Filas.Count;
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        string letra = Letra(i);
                        if (dinero[i]) tot.Add(C(totales[i], Xlsx.Estilo.DineroNegrita, "SUM(" + letra + primera + ":" + letra + ultima + ")"));
                        else tot.Add(C(i == 0 ? "TOTAL" : null, Xlsx.Estilo.Negrita));
                    }
                    h.Filas.Add(tot);
                }
                Xlsx.Escribir(ruta, new List<Xlsx.HojaSalida> { h });
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo crear el archivo (¿está abierto en Excel?)"); return; }
            Abrir(ruta);
        }

        static string Letra(int i)
        {
            string s = "";
            for (int n = i + 1; n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s;
            return s;
        }
    }

    /// <summary>Vista previa antes de importar la lista de costos.</summary>
    public class FormVistaImportacion : Dialogo
    {
        readonly RadioButton _existencia, _minimo;
        readonly CheckBox _actualizar;
        public bool StockEsExistencia { get { return _existencia.Checked; } }
        public bool ActualizarExistentes { get { return _actualizar.Checked; } }

        public FormVistaImportacion(List<ListaCostos.Renglon> datos, List<string> avisos) : base("Importar lista de costos", 980, 620)
        {
            var hojas = datos.GroupBy(d => d.Hoja).Select(h => h.Key + ": " + h.Count()).ToArray();
            int grupos = datos.Select(d => d.Grupo).Distinct().Count();
            Controls.Add(Ui.Etiqueta("Se encontraron " + datos.Count + " productos en " + grupos + " proveedores/grupos   (" + string.Join(",  ", hojas) + ")", 16, 12, Tema.Negrita, Tema.Azul));

            var g = new DataGridView { Left = 16, Top = 40, Width = 948, Height = 380 };
            Ui.EstiloGrid(g);
            g.Font = new Font("Segoe UI", 9f);
            foreach (var c in new[] { "Grupo", "Producto", "Stock", "Bulto", "Trae", "Costo", "Mayoreo", "Desde", "Med. may.", "Desde ", "Menudeo", "Venta" })
                g.Columns.Add(Ui.Columna(c, c, c == "Producto" ? 30 : c == "Grupo" ? 14 : 8, c != "Producto" && c != "Grupo"));
            foreach (var d in datos)
            {
                int i = g.Rows.Add(d.Grupo, d.Producto, F(d.Stock), D(d.Bulto), F(d.Trae), D(d.Costo), D(d.Mayoreo), F(d.MayoreoDesde), D(d.Medio), F(d.MedioDesde), D(d.Menudeo), d.PorPieza ? "pieza" : "kg");
                if (!(d.Menudeo ?? d.Medio ?? d.Mayoreo).HasValue) g.Rows[i].DefaultCellStyle.ForeColor = Tema.Humo;
            }
            Controls.Add(g);

            int y = 430;
            Controls.Add(Ui.Etiqueta("La columna STOCK del Excel es:", 16, y + 4, Tema.Negrita));
            _minimo = new RadioButton { Text = "Stock mínimo (avisar cuando quede poco)", Left = 260, Top = y, Width = 320, Checked = true };
            _existencia = new RadioButton { Text = "Existencia actual (lo que hay ahorita)", Left = 590, Top = y, Width = 330 };
            Controls.Add(_minimo); Controls.Add(_existencia);
            y += 32;
            _actualizar = new CheckBox { Text = "Si el producto ya existe (mismo nombre), actualizar su costo y precios", Left = 16, Top = y, Width = 900, Checked = true };
            Controls.Add(_actualizar);
            y += 30;
            Controls.Add(Ui.Etiqueta("Los renglones en gris no tienen precio y no se importarán. Cada proveedor/grupo del Excel se vuelve un grupo en VentaFácil.", 16, y, null, Tema.Humo));
            if (avisos.Count > 0) Controls.Add(Ui.Etiqueta(string.Join("  ", avisos), 16, y + 20, null, Tema.Rojo));

            var ok = Ui.Boton("Importar " + datos.Count + " productos", 620, ClientSize.Height - 56, 200, 42, (s, e) => { DialogResult = DialogResult.OK; }, true);
            var no = Ui.Boton("Cancelar (Esc)", 830, ClientSize.Height - 56, 134, 42, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            CancelButton = no;
        }

        static string D(decimal? v) { return v.HasValue ? Fmt.Dinero(v.Value) : ""; }
        static string F(decimal? v) { return v.HasValue ? Fmt.Dec(v.Value) : ""; }
    }
}
