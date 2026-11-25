using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Forms.Licenciamiento;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Después de una actualización: configurar o seguir igual.</summary>
    public class FormActualizado : Dialogo
    {
        public FormActualizado(string version) : base("VentaFácil " + version, 620, 300)
        {
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Controls.Add(Ui.Etiqueta("VentaFácil se actualizó a la versión " + version, 20, 18, Tema.Mediana, Tema.Azul));
            Controls.Add(new Label { Left = 20, Top = 58, Width = 580, Height = 50, Font = Tema.Normal, BackColor = Color.Transparent,
                Text = "Sus productos, ventas, usuarios y configuración se conservaron. Puede entrar así, o revisar la configuración." });
            var faltan = PrimeraVez.Revision().Where(p => !p.Bien).Select(p => "•  " + p.Texto).ToArray();
            if (faltan.Length > 0)
                Controls.Add(new Label { Left = 20, Top = 112, Width = 580, Height = 110, Font = Tema.Normal, ForeColor = Tema.Rojo, BackColor = Color.Transparent, Text = string.Join("\n", faltan.Take(5).ToArray()) });
            var no = Ui.Boton("No configurar, entrar", 20, 236, 280, 48, (s, e) => { DialogResult = DialogResult.No; }, true);
            var si = Ui.Boton("Configurar", 320, 236, 280, 48, (s, e) => { DialogResult = DialogResult.Yes; });
            Controls.Add(no); Controls.Add(si);
            AcceptButton = no;
        }
    }

    /// <summary>
    /// Asistente de configuración. Cada paso se guarda al pasar al siguiente, así que cerrarlo a la mitad no pierde
    /// lo hecho. En una instalación nueva pide crear la cuenta de administrador; en una que ya existe todo es opcional.
    /// </summary>
    public class FormAsistente : Dialogo
    {
        class Paso { public string Titulo; public Func<Control> Crear; public Func<bool> Guardar; public Func<bool> Visible; }

        readonly bool _nueva;
        readonly List<Paso> _pasos = new List<Paso>();
        readonly Panel _lista, _cuerpo;
        readonly Label _titulo;
        readonly Button _atras, _siguiente;
        int _actual = -1;
        bool _terminado, _oficina, _cuentaLista;
        public bool CuentaLista { get { return _cuentaLista; } }
        public bool Restaurado { get; private set; }
        public bool Terminado { get { return _terminado; } }

        public FormAsistente(bool nueva) : base("Configurar VentaFácil", 1000, 640)
        {
            _nueva = nueva;
            _oficina = Envios.EsOficina;
            _lista = new Panel { Left = 0, Top = 0, Width = 220, Height = 640, BackColor = Tema.AzulOscuro };
            Controls.Add(_lista);
            _titulo = Ui.Etiqueta("", 240, 16, Tema.Grande, Tema.Azul);
            Controls.Add(_titulo);
            _cuerpo = new Panel { Left = 240, Top = 70, Width = 744, Height = 496, AutoScroll = true, BackColor = Tema.Crema };
            Controls.Add(_cuerpo);
            _atras = Ui.Boton("Atrás", 240, 580, 130, 46, (s, e) => Ir(Vecino(-1), false));
            _siguiente = Ui.Boton("Siguiente", 784, 580, 200, 46, (s, e) => Siguiente(), true);
            Controls.Add(_atras); Controls.Add(_siguiente);
            Controls.Add(Ui.Boton("Cerrar, seguir después", 384, 580, 230, 46, (s, e) => Close()));

            Agregar("Inicio", PasoInicio, null);
            Agregar("Su cuenta", PasoCuenta, GuardarCuenta, () => _nueva);
            Agregar("Negocio", PasoNegocio, GuardarNegocio);
            Agregar("Usuarios", () => new SeccionUsuarios { Width = 720, Height = 480 }, null);
            Agregar("Respaldos", PasoRespaldos, GuardarRespaldos);
            Agregar("Impresora", PasoImpresora, null, () => !_oficina);
            Agregar("Forma de trabajar", PasoOperacion, GuardarOperacion, () => !_oficina);
            Agregar("Productos", PasoProductos, null, () => !_oficina);
            Agregar("Oficina", PasoOficina, null, () => _oficina);
            Agregar("Licencia", PasoLicencia, null);
            Agregar("Listo", PasoListo, null);
            FormClosing += (s, e) =>
            {
                if (_terminado || Restaurado) return;
                if (!Ui.Confirmar("¿Dejar la configuración para después?\n\nLo que ya pasó con «Siguiente» quedó guardado. El asistente está en F6 Ajustes → Negocio.")) { e.Cancel = true; DialogResult = DialogResult.None; }
            };
            Shown += (s, e) => Ir(0, false);
        }

        void Agregar(string titulo, Func<Control> crear, Func<bool> guardar, Func<bool> visible = null) { _pasos.Add(new Paso { Titulo = titulo, Crear = crear, Guardar = guardar, Visible = visible ?? (() => true) }); }

        int Vecino(int d) { int i = _actual + d; while (i >= 0 && i < _pasos.Count && !_pasos[i].Visible()) i += d; return i; }

        void Siguiente()
        {
            if (!GuardarActual()) return;
            if (_pasos[_actual].Titulo == "Listo")
            {
                _terminado = true;
                DialogResult = DialogResult.OK;
                return;
            }
            Ir(Vecino(1), false);
        }

        bool GuardarActual()
        {
            if (_actual < 0 || _pasos[_actual].Guardar == null) return true;
            try { return _pasos[_actual].Guardar(); }
            catch (Exception ex) { Ui.Error(ex, "Guardar " + _pasos[_actual].Titulo); return false; }
        }

        void Ir(int i, bool guardar)
        {
            if (i < 0 || i >= _pasos.Count) return;
            if (guardar && !GuardarActual()) return;
            _actual = i;
            foreach (Control c in _cuerpo.Controls.Cast<Control>().ToArray()) { _cuerpo.Controls.Remove(c); c.Dispose(); }
            _titulo.Text = _pasos[i].Titulo;
            try
            {
                var v = _pasos[i].Crear();
                v.Left = 0; v.Top = 0;
                _cuerpo.Controls.Add(v);
                v.Focus();
                v.SelectNextControl(v, true, true, true, true);
            }
            catch (Exception ex) { Ui.Error(ex, _pasos[i].Titulo); }
            _atras.Enabled = Vecino(-1) >= 0;
            _siguiente.Text = _pasos[i].Titulo == "Listo" ? "Terminar" : "Siguiente";
            PintarLista();
        }

        /// <summary>Los pasos a la izquierda. Si ya está instalado se puede brincar al que se quiera.</summary>
        void PintarLista()
        {
            foreach (Control c in _lista.Controls.Cast<Control>().ToArray()) { _lista.Controls.Remove(c); c.Dispose(); }
            int y = 20;
            for (int i = 0; i < _pasos.Count; i++)
            {
                if (!_pasos[i].Visible()) continue;
                int n = i;
                bool libre = !_nueva || _cuentaLista || n <= _actual;
                var l = new Label { Text = (n == _actual ? "▸  " : "    ") + _pasos[i].Titulo, Left = 8, Top = y, Width = 204, Height = 36, Font = Tema.Negrita, TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = n == _actual ? Color.White : Tema.Piedra, BackColor = n == _actual ? Tema.Azul : Color.Transparent, Cursor = libre ? Cursors.Hand : Cursors.Default };
                if (libre) l.Click += (s, e) => { if (n != _actual) BeginInvoke((Action)(() => Ir(n, true))); };
                _lista.Controls.Add(l);
                y += 40;
            }
        }

        static Panel Hoja() { return new Panel { Width = 720, Height = 480, BackColor = Tema.Crema }; }
        static Label Parrafo(string t, int y, int alto, Color? color = null) { return new Label { Text = t, Left = 8, Top = y, Width = 700, Height = alto, Font = Tema.Normal, ForeColor = color ?? Tema.Texto, BackColor = Color.Transparent }; }
        static TextBox Campo(Panel p, string etiqueta, string valor, ref int y, int ancho = 420, bool clave = false)
        {
            p.Controls.Add(Ui.Etiqueta(etiqueta, 8, y + 4));
            var t = Ui.Caja(230, y, ancho); t.Text = valor; t.UseSystemPasswordChar = clave;
            p.Controls.Add(t);
            y += 44;
            return t;
        }
        static CheckBox Casilla(Panel p, string texto, bool valor, ref int y)
        {
            var c = new CheckBox { Text = texto, Left = 8, Top = y, Width = 700, Height = 28, Font = Tema.Normal, Checked = valor };
            p.Controls.Add(c);
            y += 40;
            return c;
        }

        // ------------------------------------------------------------------ pasos
        Control PasoInicio()
        {
            var p = Hoja();
            if (_nueva)
            {
                p.Controls.Add(Parrafo("Vamos a dejar VentaFácil listo: su cuenta, el negocio, los usuarios, los respaldos, la impresora y los productos. Toma unos 10 minutos.", 8, 56));
                p.Controls.Add(Ui.Etiqueta("¿Qué computadora es esta?", 8, 84, Tema.Mediana, Tema.Azul));
                var caja = new RadioButton { Text = "La CAJA de la tienda (aquí se cobra)", Left = 8, Top = 122, Width = 600, Font = Tema.Negrita, Checked = !_oficina };
                var ofi = new RadioButton { Text = "La OFICINA (cambia precios y captura mercancía; no se cobra)", Left = 8, Top = 156, Width = 680, Font = Tema.Negrita, Checked = _oficina };
                ofi.CheckedChanged += (s, e) => { _oficina = ofi.Checked; PintarLista(); };
                p.Controls.Add(caja); p.Controls.Add(ofi);
                p.Controls.Add(Parrafo("¿Cambió de computadora? Puede traer todo desde un respaldo de VentaFácil en lugar de empezar de cero:", 214, 44));
                p.Controls.Add(Ui.Boton("Ya tengo un respaldo…", 8, 264, 230, 42, (s, e) => BeginInvoke((Action)(() => { if (SeccionRespaldos.Restaurar()) { Restaurado = true; Close(); } }))));
            }
            else
            {
                p.Controls.Add(Parrafo("Nada es obligatorio: use la lista de la izquierda para ir directo a lo que quiera cambiar, o pase con «Siguiente». Cada paso se guarda al salir de él.", 8, 56));
                p.Controls.Add(Ui.Etiqueta("Cómo está ahora:", 8, 84, Tema.Mediana, Tema.Azul));
                Revision(p, 122);
            }
            return p;
        }

        int Revision(Panel p, int y)
        {
            var l = PrimeraVez.Revision(_oficina);
            var lic = Licencia.Evaluar();
            l.Add(new PrimeraVez.Punto { Bien = lic.Estado == EstadoLicencia.Activa, Texto = lic.Estado == EstadoLicencia.Activa ? "Licencia activada" : lic.Estado == EstadoLicencia.Prueba ? "Licencia de prueba: quedan " + lic.DiasRestantes + " días" : "Licencia vencida" });
            foreach (var x in l)
            {
                p.Controls.Add(Ui.Etiqueta(x.Bien ? "Listo" : "Falta", 8, y, Tema.Negrita, x.Bien ? Tema.Verde : Tema.Rojo));
                p.Controls.Add(new Label { Text = x.Texto, Left = 80, Top = y, Width = 630, Height = 24, AutoEllipsis = true, Font = Tema.Normal, BackColor = Color.Transparent });
                y += 34;
            }
            return y;
        }

        TextBox _login, _nombre, _clave, _clave2;
        Control PasoCuenta()
        {
            var p = Hoja(); int y = 70;
            p.Controls.Add(Parrafo("Esta es la cuenta del ADMINISTRADOR: puede todo. Con ella se entra desde ahora, en lugar de admin / admin. Apunte la contraseña en un lugar seguro.", 8, 56));
            var u = Sesion.Usuario;
            _login = Campo(p, "Usuario (para entrar):", _cuentaLista ? u.Login : "", ref y);
            _nombre = Campo(p, "Su nombre:", _cuentaLista ? u.Nombre : "", ref y);
            _clave = Campo(p, "Contraseña:", "", ref y, 420, true);
            _clave2 = Campo(p, "Repetir contraseña:", "", ref y, 420, true);
            if (_cuentaLista) p.Controls.Add(Parrafo("Ya está guardada. Deje las contraseñas vacías para no cambiarla.", y + 8, 28, Tema.Humo));
            return p;
        }
        bool GuardarCuenta()
        {
            bool sinCambiar = _cuentaLista && _clave.Text == "" && _clave2.Text == "";
            string mal = sinCambiar ? (_login.Text.Trim() == "" || _nombre.Text.Trim() == "" ? "Escriba el usuario y el nombre." : null) : PrimeraVez.RevisarCuenta(_login.Text, _nombre.Text, _clave.Text, _clave2.Text);
            if (mal != null) { Ui.Aviso(mal); return false; }
            var u = UsuarioRepo.Obtener(Sesion.Usuario.Id);
            u.Login = _login.Text.Trim(); u.Nombre = _nombre.Text.Trim();
            UsuarioRepo.Guardar(u, sinCambiar ? null : _clave.Text);
            Sesion.Usuario = u;
            _cuentaLista = true;
            return true;
        }

        TextBox _neg, _dir, _tel, _rfc, _pie; NumericUpDown _caja;
        Control PasoNegocio()
        {
            var p = Hoja(); int y = 8; var c = Config.Actual;
            _neg = Campo(p, "Nombre del negocio:", c.NegocioNombre == "MI NEGOCIO" ? "" : c.NegocioNombre, ref y);
            _dir = Campo(p, "Dirección:", c.NegocioDireccion, ref y);
            _tel = Campo(p, "Teléfono:", c.NegocioTelefono, ref y);
            _rfc = Campo(p, "RFC (opcional):", c.NegocioRfc, ref y);
            _pie = Campo(p, "Mensaje al pie del ticket:", c.TicketPie, ref y);
            p.Controls.Add(Ui.Etiqueta("Número de caja:", 8, y + 4));
            _caja = AdminUtil.Numero(230, y, 90, 1, 99); _caja.Value = Math.Max(1, Math.Min(99, c.Caja));
            p.Controls.Add(_caja);
            return p;
        }
        bool GuardarNegocio()
        {
            if (_neg.Text.Trim() == "") { Ui.Aviso("Escriba el nombre del negocio."); _neg.Focus(); return false; }
            var c = Config.Actual.Copia();
            c.NegocioNombre = _neg.Text.Trim(); c.NegocioDireccion = _dir.Text.Trim(); c.NegocioTelefono = _tel.Text.Trim(); c.NegocioRfc = _rfc.Text.Trim(); c.TicketPie = _pie.Text.Trim();
            // Con un turno abierto no se cambia el número de caja: el turno se quedaría sin cerrar
            if (c.Caja != (int)_caja.Value && TurnoRepo.Abierto(c.Caja) != null) { Ui.Aviso("Hay un turno abierto en la caja " + c.Caja + ". Haga el corte Z antes de cambiar el número de caja."); _caja.Value = c.Caja; return false; }
            c.Caja = (int)_caja.Value;
            c.Guardar();
            return true;
        }

        TextBox _carpeta;
        Control PasoRespaldos()
        {
            var p = Hoja();
            p.Controls.Add(Parrafo("Después de cada venta o cambio se guarda una copia en esta carpeta. Lo más seguro es otra unidad o una USB que se quede conectada: si falla el disco, los datos siguen ahí.", 8, 60));
            p.Controls.Add(Ui.Etiqueta("Carpeta de respaldos:", 8, 84, Tema.Negrita));
            _carpeta = Ui.Caja(8, 112, 540); _carpeta.Text = Config.Actual.CarpetaRespaldo == "" ? PorDefecto : Config.Actual.CarpetaRespaldo; _carpeta.ReadOnly = true;
            p.Controls.Add(_carpeta);
            p.Controls.Add(Ui.Boton("Cambiar…", 560, 106, 130, 38, (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Elija dónde guardar los respaldos", SelectedPath = _carpeta.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) _carpeta.Text = d.SelectedPath;
            }));
            return p;
        }
        public static string PorDefecto { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VentaFacil_Respaldos"); } }
        bool GuardarRespaldos()
        {
            try
            {   // se comprueba escribiendo de verdad
                Directory.CreateDirectory(_carpeta.Text);
                string prueba = Path.Combine(_carpeta.Text, ".prueba_ventafacil");
                File.WriteAllText(prueba, "ok"); File.Delete(prueba);
            }
            catch (Exception ex) { Ui.Aviso("No se puede guardar en esa carpeta:\n" + _carpeta.Text + "\n\n" + ex.Message + "\n\nElija otra."); return false; }
            var c = Config.Actual.Copia();
            c.CarpetaRespaldo = _carpeta.Text; c.CarpetaRespaldoRevisada = true;
            c.Guardar();
            Respaldo.Pedir();
            return true;
        }

        Control PasoImpresora()
        {
            var p = Hoja();
            p.Controls.Add(Parrafo("Conecte y encienda la impresora de tickets. En la ventana que se abre puede detectarla, imprimir una prueba y probar el cajón.", 8, 56));
            var l = Ui.Etiqueta("", 8, 76, Tema.Mediana, Tema.Azul);
            Action pintar = () => { var c = Config.Actual; l.Text = !c.ImpresoraRevisada ? "Todavía no se ha elegido." : (c.Impresora == "" ? "La predeterminada de Windows" : c.Impresora) + "   ·   " + c.AnchoTicket + " mm"; };
            pintar();
            p.Controls.Add(l);
            p.Controls.Add(Ui.Boton("Elegir y probar impresora…", 8, 120, 300, 48, (s, e) => { try { FormImpresoras.Configurar(this, true); Config.Cargar(); pintar(); } catch (Exception ex) { Ui.Error(ex, "Impresora"); } }, true));
            p.Controls.Add(Parrafo("Si todavía no tiene impresora, siga adelante: se puede elegir después con Ctrl+I.", 186, 30, Tema.Humo));
            return p;
        }

        CheckBox _fondo, _declarar, _sinEx, _auto, _cancela;
        Control PasoOperacion()
        {
            var p = Hoja(); int y = 8; var c = Config.Actual;
            _fondo = Casilla(p, "Pedir fondo de caja al abrir el turno", c.PedirFondoAlAbrir, ref y);
            _declarar = Casilla(p, "Pedir el efectivo contado en el corte Z (dice si falta o sobra)", c.DeclararEfectivoEnCorte, ref y);
            _sinEx = Casilla(p, "Permitir vender productos sin existencia", c.PermitirVentaSinExistencia, ref y);
            _auto = Casilla(p, "Imprimir el ticket al cobrar", c.ImprimirAutomatico, ref y);
            _cancela = Casilla(p, "Los cajeros pueden cancelar y hacer descuentos sin clave", c.CajeroPuedeCancelar, ref y);
            p.Controls.Add(Parrafo("El cajón, la báscula y lo demás están en F6 Ajustes → Equipos.", y + 8, 30, Tema.Humo));
            return p;
        }
        bool GuardarOperacion()
        {
            var c = Config.Actual.Copia();
            c.PedirFondoAlAbrir = _fondo.Checked; c.DeclararEfectivoEnCorte = _declarar.Checked; c.PermitirVentaSinExistencia = _sinEx.Checked; c.ImprimirAutomatico = _auto.Checked; c.CajeroPuedeCancelar = _cancela.Checked;
            c.Guardar();
            return true;
        }

        Control PasoProductos()
        {
            var p = Hoja();
            var l = Ui.Etiqueta("", 8, 8, Tema.Mediana, Tema.Azul);
            Action pintar = () => l.Text = "Productos activos: " + Db.Scalar("SELECT COUNT(*) FROM productos WHERE activo=1");
            pintar();
            p.Controls.Add(l);
            p.Controls.Add(Ui.Boton("Importar Excel de la semillera…", 8, 52, 300, 46, (s, e) => { try { ExcelSemillera.Importar(this); pintar(); } catch (Exception ex) { Ui.Error(ex, "Importar"); } }));
            p.Controls.Add(Ui.Boton("Importar de Zafirah, Excel o CSV…", 8, 110, 300, 46, (s, e) => { try { using (var f = new FormImportarZafirah()) f.ShowDialog(this); pintar(); } catch (Exception ex) { Ui.Error(ex, "Importar"); } }));
            p.Controls.Add(Parrafo("También se pueden dar de alta después en F3 Catálogo.", 172, 30, Tema.Humo));
            return p;
        }

        Control PasoOficina()
        {
            var p = Hoja();
            if (Envios.EsOficina) { p.Controls.Add(Parrafo("Este equipo ya es la oficina de: " + Envios.Dato("negocio"), 8, 30)); return p; }
            p.Controls.Add(Parrafo("En la caja de la tienda: F6 Ajustes → Envíos → «Crear archivo de enlace», y guárdelo en una USB. Luego conecte aquí esa USB.", 8, 56));
            p.Controls.Add(Ui.Boton("Elegir archivo de enlace…", 8, 76, 280, 48, (s, e) => { if (SeccionEnlace.Convertir(this)) BeginInvoke((Action)(() => Ir(_actual, false))); }, true));
            p.Controls.Add(Parrafo("Si todavía no lo tiene, siga adelante y hágalo después en F6 Ajustes → Envíos.", 142, 30, Tema.Humo));
            return p;
        }

        Control PasoLicencia()
        {
            var p = Hoja();
            var l = Ui.Etiqueta("", 8, 8, Tema.Mediana, Tema.Azul);
            Action pintar = () => { var i = Licencia.Evaluar(); l.Text = i.Estado == EstadoLicencia.Activa ? "Activada en este equipo." : i.Estado == EstadoLicencia.Prueba ? "Período de prueba: quedan " + i.DiasRestantes + " días." : i.Motivo; };
            pintar();
            p.Controls.Add(l);
            p.Controls.Add(Parrafo("Mande a su proveedor el código de este equipo y escriba la clave de 16 caracteres que le dé. Cada computadora lleva la suya.", 46, 56));
            p.Controls.Add(Ui.Boton("Ver código / activar…", 8, 110, 260, 46, (s, e) => { FormLicencia.Mostrar(Licencia.Evaluar(), true); pintar(); }));
            return p;
        }

        Control PasoListo()
        {
            var p = Hoja();
            int y = Revision(p, 8);
            p.Controls.Add(Parrafo("Lo que falte se puede hacer después. Este asistente está en F6 Ajustes → Negocio → «Abrir asistente».", y + 14, 50, Tema.Humo));
            return p;
        }
    }
}
