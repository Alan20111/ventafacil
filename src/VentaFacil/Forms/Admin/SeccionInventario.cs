using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Entradas de mercancía, ajustes por conteo físico, faltantes, kardex y valor del inventario.</summary>
    public class SeccionInventario : UserControl
    {
        public SeccionInventario()
        {
            BackColor = Color.White;
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = Tema.Negrita, Padding = new Point(14, 6) };
            tabs.TabPages.Add(Pagina("Entrada de mercancía", new PanelEntrada()));
            tabs.TabPages.Add(Pagina("Ajuste / conteo físico", new PanelAjuste()));
            tabs.TabPages.Add(Pagina("Bajos de existencia", new PanelReporte(() => AdminConsultas.BajosDeExistencia(), "faltantes")));
            tabs.TabPages.Add(Pagina("Kardex de un producto", new PanelKardex()));
            tabs.TabPages.Add(Pagina("Valor del inventario", new PanelReporte(() => AdminConsultas.ValorInventario(), "valor_inventario", "ValorCosto", "ValorVenta")));
            Controls.Add(tabs);
            Controls.Add(AdminUtil.TituloSeccion("Inventario"));
        }

        static TabPage Pagina(string titulo, Control c)
        {
            var p = new TabPage(titulo) { BackColor = Color.White };
            c.Dock = DockStyle.Fill;
            c.Font = Tema.Normal;
            p.Controls.Add(c);
            return p;
        }

        // ------------------------------------------------------------------
        class PanelEntrada : UserControl
        {
            class Renglon { public Producto P; public decimal Cantidad; public decimal? Costo; }
            readonly List<Renglon> _lista = new List<Renglon>();
            readonly TextBox _codigo, _cantidad, _costo, _referencia;
            readonly DataGridView _grid;
            readonly Label _total;

            public PanelEntrada()
            {
                var arriba = new Panel { Dock = DockStyle.Top, Height = 92 };
                arriba.Controls.Add(Ui.Etiqueta("Código:", 10, 10, Tema.Negrita));
                _codigo = Ui.Caja(10, 32, 220, Tema.Mediana); arriba.Controls.Add(_codigo);
                arriba.Controls.Add(Ui.Etiqueta("Cantidad:", 240, 10, Tema.Negrita));
                _cantidad = Ui.Caja(240, 32, 100, Tema.Mediana); _cantidad.TextAlign = HorizontalAlignment.Right; _cantidad.Text = "1"; arriba.Controls.Add(_cantidad);
                arriba.Controls.Add(Ui.Etiqueta("Costo nuevo (opcional):", 350, 10));
                _costo = Ui.Caja(350, 32, 120, Tema.Mediana); _costo.TextAlign = HorizontalAlignment.Right; arriba.Controls.Add(_costo);
                arriba.Controls.Add(Ui.Boton("Agregar (Enter)", 480, 30, 140, 34, (s, e) => Agregar()));
                arriba.Controls.Add(Ui.Boton("Buscar (F2)", 626, 30, 110, 34, (s, e) => Buscar()));
                arriba.Controls.Add(Ui.Etiqueta("Escanee el código, escriba la cantidad y presione Enter. Al terminar presione \"Aplicar entrada\".", 10, 70, null, Color.Gray));
                Controls.Add(arriba);

                _grid = AdminUtil.Grid();
                _grid.Columns.Add(Ui.Columna("codigo", "Código", 18));
                _grid.Columns.Add(Ui.Columna("descripcion", "Descripción", 40));
                _grid.Columns.Add(Ui.Columna("existencia", "Existencia actual", 14, true));
                _grid.Columns.Add(Ui.Columna("cantidad", "Entra", 12, true));
                _grid.Columns.Add(Ui.Columna("costo", "Costo", 12, true));
                _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) { Quitar(); e.Handled = true; } };

                var abajo = new Panel { Dock = DockStyle.Bottom, Height = 56 };
                abajo.Controls.Add(Ui.Etiqueta("Referencia (factura / proveedor):", 10, 18));
                _referencia = Ui.Caja(240, 14, 260); abajo.Controls.Add(_referencia);
                abajo.Controls.Add(Ui.Boton("Quitar renglón (Supr)", 510, 10, 170, 38, (s, e) => Quitar()));
                var aplicar = Ui.Boton("Aplicar entrada", 690, 10, 160, 38, (s, e) => Aplicar());
                aplicar.ForeColor = Tema.Verde;
                abajo.Controls.Add(aplicar);
                _total = Ui.Etiqueta("", 860, 18, Tema.Negrita);
                abajo.Controls.Add(_total);

                Controls.Add(_grid);
                Controls.Add(abajo);
                _grid.BringToFront();

                _codigo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _cantidad.Focus(); _cantidad.SelectAll(); } else if (e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; Buscar(); } };
                _cantidad.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Agregar(); } };
                _costo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Agregar(); } };
            }

            void Buscar()
            {
                var p = FormElegirProducto.Pedir(FindForm(), "");
                if (p != null) { _codigo.Text = p.Codigo; _cantidad.Focus(); _cantidad.SelectAll(); }
            }

            void Agregar()
            {
                if (_codigo.Text.Trim() == "") { _codigo.Focus(); return; }
                var p = FormElegirProducto.Pedir(FindForm(), _codigo.Text);
                if (p == null) { _codigo.Focus(); _codigo.SelectAll(); return; }
                if (!p.UsaInventario) { Ui.Aviso("\"" + p.Descripcion + "\" no controla existencia.\nActívelo en el producto si quiere llevar inventario."); return; }
                decimal cant, costo;
                if (!Fmt.TryDec(_cantidad.Text, out cant) || cant <= 0) { Ui.Aviso("Cantidad inválida."); _cantidad.Focus(); _cantidad.SelectAll(); return; }
                decimal? nuevoCosto = null;
                if (_costo.Text.Trim() != "")
                {
                    if (!Fmt.TryDec(_costo.Text, out costo) || costo < 0) { Ui.Aviso("Costo inválido."); _costo.Focus(); return; }
                    nuevoCosto = costo;
                }
                var ya = _lista.FirstOrDefault(r => r.P.Id == p.Id);
                if (ya != null) { ya.Cantidad += cant; if (nuevoCosto.HasValue) ya.Costo = nuevoCosto; }
                else _lista.Add(new Renglon { P = p, Cantidad = cant, Costo = nuevoCosto });
                Pintar();
                _codigo.Text = ""; _cantidad.Text = "1"; _costo.Text = "";
                _codigo.Focus();
            }

            void Quitar()
            {
                if (_grid.CurrentRow == null) return;
                _lista.Remove(_grid.CurrentRow.Tag as Renglon);
                Pintar();
            }

            void Pintar()
            {
                _grid.Rows.Clear();
                foreach (var r in _lista)
                {
                    int i = _grid.Rows.Add(r.P.Codigo, r.P.Descripcion, Fmt.Cant(r.P.Existencia, r.P.Granel), Fmt.Cant(r.Cantidad, r.P.Granel),
                        Fmt.Dinero(r.Costo ?? r.P.Costo));
                    _grid.Rows[i].Tag = r;
                }
                _total.Text = _lista.Count + " productos · " + Fmt.Dinero(_lista.Sum(r => r.Cantidad * (r.Costo ?? r.P.Costo)));
            }

            void Aplicar()
            {
                if (_lista.Count == 0) { Ui.Aviso("No hay productos en la entrada."); return; }
                if (!Ui.Confirmar("¿Aplicar la entrada de " + _lista.Count + " productos al inventario?")) return;
                string refe = _referencia.Text.Trim() == "" ? "Entrada de mercancía" : "Entrada: " + _referencia.Text.Trim();
                long uid = Sesion.Usuario != null ? Sesion.Usuario.Id : 0;
                try
                {
                    using (var cn = Db.Abrir())
                    using (var tx = cn.BeginTransaction())
                    {
                        foreach (var r in _lista)
                        {
                            ProductoRepo.MoverExistencia(cn, tx, r.P.Id, r.Cantidad, "ENTRADA", refe, uid);
                            if (r.Costo.HasValue)
                                Db.Exec(cn, tx, "UPDATE productos SET costo=@p0, actualizado=@p1 WHERE id=@p2", r.Costo.Value, Db.Ahora(), r.P.Id);
                        }
                        tx.Commit();
                    }
                    Ui.Info("Entrada aplicada.");
                    _lista.Clear(); _referencia.Text = "";
                    Pintar();
                    _codigo.Focus();
                }
                catch (Exception ex) { Ui.Error(ex, "Aplicar entrada (no se guardó nada)"); }
            }
        }

        // ------------------------------------------------------------------
        class PanelAjuste : UserControl
        {
            readonly TextBox _codigo, _contado, _motivo;
            readonly Label _info;
            Producto _p;

            public PanelAjuste()
            {
                Controls.Add(Ui.Etiqueta("1. Escanee o escriba el código (F2 para buscar):", 16, 16, Tema.Negrita));
                _codigo = Ui.Caja(16, 42, 260, Tema.Mediana); Controls.Add(_codigo);
                Controls.Add(Ui.Boton("Buscar (F2)", 286, 40, 110, 34, (s, e) => Elegir("")));
                _info = Ui.Etiqueta("", 16, 86, Tema.Mediana, Tema.Azul);
                Controls.Add(_info);
                Controls.Add(Ui.Etiqueta("2. Existencia contada físicamente:", 16, 150, Tema.Negrita));
                _contado = Ui.Caja(16, 176, 160, Tema.Mediana); _contado.TextAlign = HorizontalAlignment.Right; Controls.Add(_contado);
                Controls.Add(Ui.Etiqueta("Motivo (opcional):", 16, 220));
                _motivo = Ui.Caja(16, 244, 380); Controls.Add(_motivo);
                var b = Ui.Boton("Guardar ajuste", 16, 288, 180, 40, (s, e) => Guardar());
                b.ForeColor = Tema.Verde;
                Controls.Add(b);

                _codigo.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Elegir(_codigo.Text); }
                    else if (e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; Elegir(""); }
                };
                _contado.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _motivo.Focus(); } };
                _motivo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Guardar(); } };
            }

            void Elegir(string texto)
            {
                var p = FormElegirProducto.Pedir(FindForm(), texto);
                if (p == null) return;
                _p = p;
                _codigo.Text = p.Codigo;
                _info.Text = p.Descripcion + "\nExistencia en sistema: " + Fmt.Cant(p.Existencia, p.Granel) + (p.UsaInventario ? "" : "   (no controla inventario)");
                _contado.Text = "";
                _contado.Focus();
            }

            void Guardar()
            {
                if (_p == null) { Ui.Aviso("Primero elija el producto."); _codigo.Focus(); return; }
                decimal cont;
                if (!Fmt.TryDec(_contado.Text, out cont) || cont < 0) { Ui.Aviso("Escriba la cantidad contada."); _contado.Focus(); return; }
                if (!_p.UsaInventario) { Ui.Aviso("El producto no controla inventario."); return; }
                try
                {
                    ProductoRepo.AjustarExistencia(_p.Id, cont, _motivo.Text.Trim() == "" ? "Conteo físico" : _motivo.Text.Trim(), Sesion.Usuario != null ? Sesion.Usuario.Id : 0);
                    Ui.Info("Existencia ajustada: " + _p.Descripcion + "\nAntes: " + Fmt.Dec(_p.Existencia) + "   Ahora: " + Fmt.Dec(cont));
                    _p = null; _info.Text = ""; _codigo.Text = ""; _contado.Text = ""; _motivo.Text = "";
                    _codigo.Focus();
                }
                catch (Exception ex) { Ui.Error(ex, "Ajustar existencia"); }
            }
        }

        // ------------------------------------------------------------------
        class PanelKardex : UserControl
        {
            readonly DataGridView _grid;
            readonly Label _titulo;
            DataTable _dt;

            public PanelKardex()
            {
                _grid = AdminUtil.Grid();
                Controls.Add(_grid);
                var arriba = new Panel { Dock = DockStyle.Top, Height = 54 };
                arriba.Controls.Add(Ui.Boton("Elegir producto…", 10, 10, 160, 36, (s, e) => Elegir()));
                arriba.Controls.Add(Ui.Boton("Exportar CSV", 176, 10, 130, 36, (s, e) => AdminUtil.ExportarCsv(_dt, "kardex")));
                _titulo = Ui.Etiqueta("", 320, 18, Tema.Negrita, Tema.Azul);
                arriba.Controls.Add(_titulo);
                Controls.Add(arriba);
            }

            void Elegir()
            {
                var p = FormElegirProducto.Pedir(FindForm(), "");
                if (p == null) return;
                try
                {
                    _dt = AdminConsultas.Kardex(p.Id);
                    AdminUtil.Mostrar(_grid, _dt);
                    _titulo.Text = p.Codigo + "  " + p.Descripcion + "   ·   Existencia: " + Fmt.Cant(p.Existencia, p.Granel);
                }
                catch (Exception ex) { Ui.Error(ex, "Kardex"); }
            }
        }
    }

    /// <summary>Panel genérico: cuadrícula con botón actualizar y exportar.</summary>
    class PanelReporte : UserControl
    {
        readonly Func<DataTable> _consulta;
        readonly string _nombre;
        readonly string[] _dinero;
        readonly DataGridView _grid;
        readonly Label _resumen;
        DataTable _dt;

        public PanelReporte(Func<DataTable> consulta, string nombre, params string[] dinero)
        {
            _consulta = consulta; _nombre = nombre; _dinero = dinero;
            _grid = AdminUtil.Grid();
            Controls.Add(_grid);
            var arriba = new Panel { Dock = DockStyle.Top, Height = 54 };
            arriba.Controls.Add(Ui.Boton("Actualizar", 10, 10, 120, 36, (s, e) => Cargar()));
            arriba.Controls.Add(Ui.Boton("Exportar CSV", 136, 10, 130, 36, (s, e) => AdminUtil.ExportarCsv(_dt, _nombre)));
            _resumen = Ui.Etiqueta("", 280, 18, Tema.Negrita, Tema.Azul);
            arriba.Controls.Add(_resumen);
            Controls.Add(arriba);
            Load += (s, e) => Cargar();
        }

        void Cargar()
        {
            try
            {
                _dt = _consulta();
                AdminUtil.Mostrar(_grid, _dt, _dinero);
                string r = _dt.Rows.Count + " renglones";
                foreach (var col in _dinero)
                {
                    decimal suma = 0;
                    foreach (DataRow row in _dt.Rows) suma += AdminUtil.ValorDec(row[col]);
                    r += "   ·   " + _grid.Columns[col].HeaderText + ": " + Fmt.Dinero(suma);
                }
                _resumen.Text = r;
            }
            catch (Exception ex) { Ui.Error(ex, "Consulta"); }
        }
    }
}
