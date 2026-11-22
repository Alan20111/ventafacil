using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>
    /// Ciclo de inventario con Excel:
    ///   1) se exporta la hoja de conteo (lo que dice el sistema, con una columna CONTADO vacía),
    ///   2) se cuenta en el negocio y se escribe lo contado,
    ///   3) se importa: se ven las diferencias (faltantes y sobrantes, con su valor) y solo entonces se aplican.
    /// </summary>
    public static class ConteoFisico
    {
        public class Linea
        {
            public Producto P;
            public decimal Sistema;
            public decimal Contado;
            public decimal Dif { get { return Contado - Sistema; } }
            public decimal Valor { get { return Dinero.R2(Dif * P.Costo); } }
        }

        public static string Grupo(Producto p)
        {
            return p.Proveedor != "" ? p.Proveedor : (p.Departamento != "" ? p.Departamento : "SIN PROVEEDOR");
        }

        public static List<Producto> ProductosConteo()
        {
            return Db.Lista("SELECT p.*, IFNULL(d.nombre,'') AS departamento FROM productos p LEFT JOIN departamentos d ON d.id=p.departamento_id WHERE p.activo=1 AND p.usa_inventario=1 ORDER BY p.orden, p.id", Producto.De);
        }

        // ======================================================================
        //  1) Exportar la hoja de conteo
        // ======================================================================
        public static void Exportar(IWin32Window dueno, string grupo)
        {
            var productos = ProductosConteo();
            if (!string.IsNullOrEmpty(grupo)) productos = productos.Where(p => Grupo(p) == grupo).ToList();
            if (productos.Count == 0)
            {
                Ui.Aviso("No hay productos con control de existencia" + (string.IsNullOrEmpty(grupo) ? "." : " en \"" + grupo + "\".") +
                    "\nActive \"Controlar existencia\" en los productos que quiera contar.");
                return;
            }
            string ruta;
            using (var d = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "CONTEO " + Config.Actual.NegocioNombre + " " + DateTime.Now.ToString("dd-MM-yyyy") + ".xlsx" })
            {
                if (d.ShowDialog(dueno) != DialogResult.OK) return;
                ruta = d.FileName;
            }
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                var filas = productos.OrderBy(x => Grupo(x)).ThenBy(x => x.Orden).ThenBy(x => x.Descripcion)
                    .Select(p => new FilaConteo { Codigo = p.Codigo, Producto = p.Descripcion, Grupo = Grupo(p), EnSistema = p.Existencia });
                var h = ConteoExcel.ArmarHoja(Config.Actual.NegocioNombre + " — HOJA DE CONTEO  " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), filas);
                Xlsx.Escribir(ruta, new List<Xlsx.HojaSalida> { h });
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo crear el archivo (¿está abierto en Excel?)"); return; }
            finally { Cursor.Current = Cursors.Default; }
            if (Ui.Confirmar("Hoja de conteo guardada con " + productos.Count + " productos:\n" + ruta + "\n\n¿Abrirla ahora?", "Conteo"))
                try { Process.Start(ruta); } catch (Exception ex) { Ui.Error(ex, "Abrir archivo"); }
        }

        // ======================================================================
        //  2) Importar lo contado
        // ======================================================================
        public static void Importar(IWin32Window dueno)
        {
            string ruta;
            using (var d = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Elija la hoja de conteo con la columna CONTADO llena" })
            {
                if (d.ShowDialog(dueno) != DialogResult.OK) return;
                ruta = d.FileName;
            }
            List<Xlsx.Hoja> hojas;
            try { hojas = Xlsx.Leer(ruta); }
            catch (Exception ex) { Ui.Error(ex, "No se pudo leer el Excel (¿es un archivo .xlsx?)"); return; }

            var leido = ConteoExcel.Leer(hojas);
            if (!leido.HalloColumnas) { Ui.Aviso("No encontré las columnas CÓDIGO y CONTADO.\nUse la hoja que genera \"Exportar hoja de conteo\"."); return; }

            var lineas = new List<Linea>();
            var noEncontrados = new List<string>();
            int sinControl = 0;
            foreach (var kv in leido.Contados)
            {
                var p = ProductoRepo.PorCodigo(kv.Key);
                if (p == null) { noEncontrados.Add(kv.Key); continue; }
                if (!p.UsaInventario) { sinControl++; continue; }
                lineas.Add(new Linea { P = p, Sistema = p.Existencia, Contado = kv.Value });
            }
            var invalidos = leido.Invalidos;
            int sinContar = leido.SinContar, repetidos = leido.Repetidos;
            if (lineas.Count == 0 && noEncontrados.Count == 0 && invalidos.Count == 0)
            {
                Ui.Aviso("No hay nada contado todavía. Escriba las cantidades en la columna CONTADO y vuelva a importar.");
                return;
            }

            using (var f = new FormVistaConteo(lineas, sinContar, noEncontrados, invalidos, sinControl, repetidos))
            {
                if (f.ShowDialog(dueno) != DialogResult.OK) return;
                var cambios = lineas.Where(l => l.Dif != 0).ToList();
                if (cambios.Count == 0) { Ui.Info("Todo coincide con el sistema. No hay nada que ajustar."); return; }
                try
                {
                    Cursor.Current = Cursors.WaitCursor;
                    int n = ProductoRepo.AjustarVarios(cambios.Select(l => new KeyValuePair<long, decimal>(l.P.Id, l.Contado)).ToList(),
                        "Conteo físico " + DateTime.Now.ToString("dd/MM/yyyy"), Sesion.Usuario.Id);
                    Cursor.Current = Cursors.Default;
                    Ui.Info("Conteo aplicado: " + n + " productos ajustados.\nQuedó registrado en el kardex de cada producto.", "Conteo");
                }
                catch (Exception ex) { Cursor.Current = Cursors.Default; Ui.Error(ex, "No se pudo aplicar el conteo. No se cambió nada."); }
            }
        }

        public static DataTable TablaDiferencias(IEnumerable<Linea> lineas)
        {
            var dt = new DataTable();
            dt.Columns.Add("Codigo", typeof(string)); dt.Columns.Add("Producto", typeof(string)); dt.Columns.Add("Grupo", typeof(string));
            dt.Columns.Add("EnSistema", typeof(decimal)); dt.Columns.Add("Contado", typeof(decimal)); dt.Columns.Add("Diferencia", typeof(decimal));
            dt.Columns.Add("ValorCosto", typeof(decimal));
            foreach (var l in lineas.OrderBy(x => x.Valor))
                dt.Rows.Add(l.P.Codigo, l.P.Descripcion, Grupo(l.P), l.Sistema, l.Contado, l.Dif, l.Valor);
            return dt;
        }
    }

    /// <summary>Vista previa del conteo: solo lo que tiene diferencia, con su valor. Nada se aplica hasta pulsar "Aplicar".</summary>
    public class FormVistaConteo : Dialogo
    {
        public FormVistaConteo(List<ConteoFisico.Linea> lineas, int sinContar, List<string> noEncontrados, List<string> invalidos, int sinControl, int repetidos)
            : base("Conteo físico — diferencias", 1000, 640)
        {
            var dif = lineas.Where(l => l.Dif != 0).OrderBy(l => l.Valor).ToList();
            decimal falta = dif.Where(l => l.Dif < 0).Sum(l => -l.Valor);
            decimal sobra = dif.Where(l => l.Dif > 0).Sum(l => l.Valor);

            Controls.Add(Ui.Etiqueta(lineas.Count + " productos contados:  " + (lineas.Count - dif.Count) + " coinciden,  " + dif.Count + " con diferencia", 16, 12, Tema.Mediana, Tema.Azul));
            Controls.Add(Ui.Etiqueta("Faltante (a costo): " + Fmt.Dinero(falta) + "      Sobrante (a costo): " + Fmt.Dinero(sobra), 16, 46, Tema.Negrita, falta > 0 ? Tema.Rojo : Tema.Texto));

            var g = new DataGridView { Left = 16, Top = 80, Width = 968, Height = 400 };
            Ui.EstiloGrid(g);
            g.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            g.Columns.Add(Ui.Columna("c", "Código", 14));
            g.Columns.Add(Ui.Columna("p", "Producto", 38));
            g.Columns.Add(Ui.Columna("s", "En sistema", 11, true));
            g.Columns.Add(Ui.Columna("n", "Contado", 11, true));
            g.Columns.Add(Ui.Columna("d", "Diferencia", 11, true));
            g.Columns.Add(Ui.Columna("v", "Valor (costo)", 13, true));
            foreach (var l in dif)
            {
                int i = g.Rows.Add(l.P.Codigo, l.P.Descripcion, Fmt.Dec(l.Sistema), Fmt.Dec(l.Contado), (l.Dif > 0 ? "+" : "") + Fmt.Dec(l.Dif), Fmt.Dinero(l.Valor));
                g.Rows[i].DefaultCellStyle.ForeColor = l.Dif < 0 ? Tema.Rojo : Tema.Azul;
            }
            Controls.Add(g);

            var avisos = new StringBuilder();
            if (sinContar > 0) avisos.Append(sinContar + " renglones sin contar (se dejan igual).   ");
            if (sinControl > 0) avisos.Append(sinControl + " sin control de existencia (se ignoran).   ");
            if (repetidos > 0) avisos.Append(repetidos + " códigos repetidos (se usó el primero).   ");
            if (noEncontrados.Count > 0) avisos.Append("Códigos que no existen: " + string.Join(", ", noEncontrados.Take(5).ToArray()) + (noEncontrados.Count > 5 ? "…" : "") + ".   ");
            if (invalidos.Count > 0) avisos.Append("Cantidades no válidas en: " + string.Join(", ", invalidos.Take(5).ToArray()) + (invalidos.Count > 5 ? "…" : "") + ".");
            Controls.Add(new Label { Text = avisos.ToString(), Left = 16, Top = 488, Width = 968, Height = 44, Font = Tema.Normal, ForeColor = Tema.Rojo, BackColor = Color.Transparent });
            Controls.Add(Ui.Etiqueta("Al aplicar, la existencia de cada producto queda igual a lo contado. Si hubo ventas después de contar, cuéntelo de nuevo o ajuste a mano.", 16, 534, null, Tema.Humo));

            var ok = Ui.Boton("Aplicar " + dif.Count + " ajustes", 440, 580, 220, 44, (s, e) => { DialogResult = DialogResult.OK; });
            Tema.Pastilla(ok, true);
            ok.Enabled = dif.Count > 0;
            Controls.Add(ok);
            Controls.Add(Ui.Boton("Exportar diferencias", 672, 580, 190, 44, (s, e) => ExcelSemillera.ExportarTabla(ConteoFisico.TablaDiferencias(dif), "diferencias_conteo", "Diferencias del conteo físico")));
            var no = Ui.Boton("Cancelar", 874, 580, 110, 44, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(no);
            CancelButton = no;
        }
    }
}
