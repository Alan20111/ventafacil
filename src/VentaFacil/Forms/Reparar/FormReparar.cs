using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Reparar
{
    /// <summary>
    /// "Reparar VentaFácil". Revisa el programa, el motor de base de datos, la base, los respaldos
    /// y los accesos del escritorio. No borra datos. Restaura la base solo si el usuario lo confirma,
    /// y guarda la base dañada aparte.
    /// Se abre con el acceso del escritorio (--reparar) o al arrancar si la base no se puede leer.
    /// </summary>
    public class FormReparar : Dialogo
    {
        readonly TextBox _salida;
        readonly Button _btnRestaurar;
        bool _basePudoLeerse;
        string _mejorRespaldo;

        FormReparar() : base("Reparar VentaFácil", 720, 560)
        {
            Controls.Add(Ui.Etiqueta("Revisión y reparación", 16, 12, Tema.Grande, Tema.Azul));
            Controls.Add(Ui.Etiqueta("Esta revisión no borra sus ventas ni sus productos.", 16, 56, null, Tema.Humo));
            _salida = new TextBox { Left = 16, Top = 90, Width = 688, Height = 370, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10f), BackColor = Tema.Crema };
            Controls.Add(_salida);
            Controls.Add(Ui.Boton("Revisar otra vez", 16, 474, 180, 44, (s, e) => Revisar()));
            _btnRestaurar = Ui.Boton("Restaurar último respaldo bueno", 208, 474, 300, 44, (s, e) => Restaurar());
            _btnRestaurar.Enabled = false;
            Controls.Add(_btnRestaurar);
            Controls.Add(Ui.Boton("Cerrar", 556, 474, 148, 44, (s, e) => Close()));
            Shown += (s, e) => Revisar();
        }

        /// <summary>Entrada de "--reparar". Si VentaFácil está abierto, no se toca la base.</summary>
        public static void Ejecutar()
        {
            bool unica;
            using (var mutex = new Mutex(true, "VentaFacil_PDV_Unico", out unica))
            {
                if (!unica)
                {
                    Ui.Aviso("VentaFácil está abierto. Ciérrelo y vuelva a usar 'Reparar VentaFácil'.");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Db.PrepararRutas();
                using (var f = new FormReparar()) f.ShowDialog();
            }
        }

        /// <summary>
        /// Al arrancar, si la base no se puede abrir: ofrece restaurar el último respaldo bueno.
        /// Devuelve true si quedó restaurada y se puede seguir.
        /// </summary>
        public static bool RecuperarBase(string motivo)
        {
            var buenos = Salud.RespaldosBuenos();
            if (buenos.Count == 0)
            {
                Ui.Error("La base de datos no se puede abrir y no hay respaldos buenos.\n\nAbra 'Reparar VentaFácil' en el escritorio para revisar.");
                return false;
            }
            string fecha = File.GetLastWriteTime(buenos[0]).ToString("dd/MM/yyyy HH:mm");
            if (!Ui.Confirmar("La base de datos no se puede leer.\n\n" + motivo +
                "\n\n¿Restaurar el último respaldo bueno (" + fecha + ")?\n" +
                "Lo que se hizo después de esa fecha no se recupera. La base dañada se guarda aparte."))
                return false;
            try
            {
                Salud.Restaurar(buenos[0]);
                return true;
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "No se pudo restaurar. Abra 'Reparar VentaFácil' en el escritorio.");
                return false;
            }
        }

        void Agregar(string linea) { _salida.AppendText(linea + Environment.NewLine); }

        void Revisar()
        {
            _salida.Text = "";
            _basePudoLeerse = false;
            _mejorRespaldo = null;
            Seccion("1. Programa", () =>
            {
                Agregar("   " + Instalacion.Reinstalar());
            });
            Seccion("2. Motor de base de datos", () =>
            {
                Db.Scalar("SELECT 1");
                Agregar("   [OK] Funciona correctamente.");
            });
            Seccion("3. Carpeta de datos", () =>
            {
                Agregar("   " + Db.Carpeta);
                string prueba = Path.Combine(Db.Carpeta, ".prueba");
                Directory.CreateDirectory(Db.Carpeta);
                File.WriteAllText(prueba, "ok");
                File.Delete(prueba);
                Agregar("   [OK] Se puede escribir.");
            });
            Seccion("4. Base de datos", () =>
            {
                if (!File.Exists(Db.Archivo))
                {
                    Agregar("   [AVISO] Todavía no existe. Se creará al abrir VentaFácil.");
                    return;
                }
                string r = Salud.Revisar(Db.Archivo);
                if (r == "ok")
                {
                    _basePudoLeerse = true;
                    try { Db.Exec("REINDEX"); } catch (Exception ex) { Agregar("   [AVISO] No se reconstruyeron los índices: " + ex.Message); }
                    Agregar("   [OK] La base se lee completa.");
                    Agregar("   Ventas: " + Db.Scalar("SELECT COUNT(*) FROM ventas") + "   Productos: " + Db.Scalar("SELECT COUNT(*) FROM productos"));
                }
                else Agregar("   [DAÑADA] " + r);
            });
            Seccion("5. Respaldos", () =>
            {
                var buenos = Salud.RespaldosBuenos();
                if (buenos.Count == 0) Agregar("   [AVISO] No hay respaldos buenos todavía.");
                else
                {
                    _mejorRespaldo = buenos[0];
                    Agregar("   [OK] " + buenos.Count + " respaldo(s) bueno(s). El más reciente: " + File.GetLastWriteTime(buenos[0]).ToString("dd/MM/yyyy HH:mm"));
                }
            });
            Seccion("6. Accesos del escritorio", () =>
            {
                Instalacion.CrearAccesos();
                Agregar("   [OK] Creados o actualizados.");
            });

            _btnRestaurar.Enabled = !_basePudoLeerse && _mejorRespaldo != null;
            Agregar("");
            Agregar(_basePudoLeerse ? "Revisión terminada. No hay nada que restaurar." : "Hay un problema con la base. Puede restaurar el último respaldo bueno.");
        }

        /// <summary>Corre una revisión; si falla, lo muestra y sigue con la siguiente.</summary>
        void Seccion(string titulo, Action revisar)
        {
            Agregar(titulo);
            try { revisar(); }
            catch (Exception ex)
            {
                Agregar("   [ERROR] " + ex.Message);
                Log.Error(ex, "Reparar: " + titulo);
            }
        }

        void Restaurar()
        {
            if (_mejorRespaldo == null) return;
            string fecha = File.GetLastWriteTime(_mejorRespaldo).ToString("dd/MM/yyyy HH:mm");
            if (!Ui.Confirmar("Se reemplazará la base dañada con el respaldo del " + fecha + ".\n" +
                "Lo que se hizo después de esa fecha no se recupera. La base dañada se guarda aparte.\n\n¿Restaurar?")) return;
            try
            {
                string guardada = Salud.Restaurar(_mejorRespaldo);
                Revisar();
                Agregar("");
                Agregar("[OK] Base restaurada. La dañada se guardó en: " + guardada);
            }
            catch (Exception ex) { Ui.Error(ex, "Restaurar"); }
        }
    }
}
