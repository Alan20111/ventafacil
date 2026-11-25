using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Printing;
using VentaFacil.UI;

namespace VentaFacil.Forms.Pos
{
    /// <summary>
    /// Cortes.
    ///  - Corte X de cajero: parcial, solo lo de un cajero. No cierra nada.
    ///  - Corte X de caja: parcial de toda la caja. No cierra nada.
    ///  - Corte Z: cierre del turno/día. Pide el efectivo contado y calcula sobrante/faltante.
    /// </summary>
    public class FormCortes : Dialogo
    {
        readonly ComboBox _cajeros;
        readonly TextBox _vista;
        Ticket _ticket;
        public bool TurnoCerrado { get; private set; }

        public FormCortes() : base("Cortes de caja", 760, 600)
        {
            var t = Sesion.Turno;
            Controls.Add(Ui.Etiqueta("Turno " + t.Id + "  ·  abierto el " + t.Apertura.ToString("dd/MM/yyyy HH:mm") + "  ·  fondo " + Fmt.Dinero(t.Fondo), 16, 12, Tema.Negrita, Tema.Azul));

            Controls.Add(Ui.Etiqueta("Cajero:", 16, 48, Tema.Negrita));
            _cajeros = new ComboBox { Left = 90, Top = 44, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            if (Permisos.Tiene(Sesion.Usuario, Permisos.CorteCaja)) foreach (var u in TurnoRepo.CajerosDelTurno(t.Id)) _cajeros.Items.Add(new ItemUsuario(u));
            if (!_cajeros.Items.Cast<ItemUsuario>().Any(i => i.U.Id == Sesion.Usuario.Id)) _cajeros.Items.Add(new ItemUsuario(Sesion.Usuario));
            _cajeros.SelectedItem = _cajeros.Items.Cast<ItemUsuario>().First(i => i.U.Id == Sesion.Usuario.Id);
            Controls.Add(_cajeros);

            Controls.Add(Ui.Boton("F3 Corte cajero", 300, 38, 136, 42, (s, e) => CorteXCajero()));
            Controls.Add(Ui.Boton("F4 Corte de caja", 444, 38, 136, 42, (s, e) => CorteXCaja()));
            var z = Ui.Boton("F2 Corte Z (cierre)", 588, 38, 154, 42, (s, e) => CorteZ());
            Tema.BotonPeligro(z);
            Controls.Add(z);

            _vista = new TextBox { Left = 16, Top = 92, Width = 726, Height = 440, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Courier New", 10f), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(_vista);
            var imp = Ui.Boton("Imprimir (Ctrl+P)", 466, 546, 140, 38, (s, e) => Imprimir());
            Tema.Pastilla(imp, true);
            Controls.Add(imp);
            Controls.Add(Ui.Boton("Salir (Esc)", 614, 546, 128, 38, (s, e) => Close()));
            Controls.Add(Ui.Etiqueta("Los cortes X son informativos. El corte Z cierra el turno.", 16, 556, null, Tema.Humo));

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F3) { e.Handled = true; CorteXCajero(); }
                else if (e.KeyCode == Keys.F4) { e.Handled = true; CorteXCaja(); }
                else if (e.KeyCode == Keys.F2) { e.Handled = true; CorteZ(); }
                else if (e.KeyData == (Keys.Control | Keys.P)) { e.Handled = true; Imprimir(); }
            };
            Shown += (s, e) => { if (Permisos.Tiene(Sesion.Usuario, Permisos.CorteCaja)) CorteXCaja(); else CorteXCajero(); };
        }

        class ItemUsuario
        {
            public readonly Usuario U;
            public ItemUsuario(Usuario u) { U = u; }
            public override string ToString() { return U.Nombre; }
        }

        void Mostrar(Ticket t) { _ticket = t; _vista.Text = t.ComoTexto(); }

        void CorteXCajero()
        {
            var item = _cajeros.SelectedItem as ItemUsuario;
            if (item == null) return;
            if (item.U.Id != Sesion.Usuario.Id && Ui.Autorizar(Permisos.CorteCaja, "Corte de otro cajero: se requiere autorización") == null) return;
            try
            {
                var r = TurnoRepo.Resumen(Sesion.Turno.Id, item.U.Id);
                r.Titulo = "CORTE X DE CAJERO (PARCIAL)";
                r.Cajero = item.U.Nombre;
                Mostrar(Tickets.Corte(r, Config.Actual, null));
            }
            catch (Exception ex) { Ui.Error(ex, "Corte de cajero"); }
        }

        void CorteXCaja()
        {
            if (Ui.Autorizar(Permisos.CorteCaja, "Corte de caja: se requiere autorización") == null) return;
            try
            {
                var r = TurnoRepo.Resumen(Sesion.Turno.Id);
                r.Titulo = "CORTE X DE CAJA (PARCIAL)";
                Mostrar(Tickets.Corte(r, Config.Actual, null));
            }
            catch (Exception ex) { Ui.Error(ex, "Corte de caja"); }
        }

        void CorteZ()
        {
            var autoriza = Ui.Autorizar(Permisos.CorteZ, "Corte Z: se requiere autorización");
            if (autoriza == null) return;
            if (SuspendidaRepo.Todas().Count > 0 && !Ui.Confirmar("Hay ventas en espera sin cobrar. Seguirán guardadas para el siguiente turno.\n¿Continuar con el corte Z?")) return;
            try
            {
                var r = TurnoRepo.Resumen(Sesion.Turno.Id);
                decimal? declarado = null;
                if (Config.Actual.DeclararEfectivoEnCorte)
                {
                    declarado = Ui.PedirNumero("Corte Z - Declaración de efectivo", "Cuente el dinero del cajón. ¿Cuánto efectivo hay? (incluya el fondo)");
                    if (!declarado.HasValue) return;
                }
                if (!Ui.Confirmar("Se cerrará el turno " + Sesion.Turno.Id + ". Después se abrirá uno nuevo.\n¿Hacer el corte Z?")) return;

                TurnoRepo.Cerrar(Sesion.Turno.Id, autoriza.Id, declarado);
                r = TurnoRepo.Resumen(Sesion.Turno.Id);
                r.Titulo = "CORTE Z - CIERRE DE TURNO";
                var t = Tickets.Corte(r, Config.Actual, declarado);
                Mostrar(t);
                TurnoCerrado = true;
                try { Db.Respaldar(); } catch (Exception ex) { Log.Error(ex, "Respaldo tras corte Z"); }
                try { Impresora.Imprimir(t); } catch (Exception ex) { Ui.Error(ex, "El corte se hizo, pero no se pudo imprimir. Puede reimprimirlo desde Administración > Reportes"); }

                if (declarado.HasValue)
                {
                    decimal dif = declarado.Value - r.EfectivoEsperado;
                    string msg = dif == 0 ? "La caja cuadra exacto." : (dif > 0 ? "SOBRANTE de " : "FALTANTE de ") + Fmt.Dinero(Math.Abs(dif));
                    Ui.Info("Corte Z terminado.\n\nDebe haber: " + Fmt.Dinero(r.EfectivoEsperado) + "\nContado: " + Fmt.Dinero(declarado.Value) + "\n\n" + msg);
                }
                else Ui.Info("Corte Z terminado.");
                Close();
            }
            catch (Exception ex) { Ui.Error(ex, "Corte Z"); }
        }

        void Imprimir()
        {
            if (_ticket == null) return;
            try { Impresora.Imprimir(_ticket); }
            catch (Exception ex) { Ui.Error(ex, "Imprimir corte"); }
        }
    }
}
