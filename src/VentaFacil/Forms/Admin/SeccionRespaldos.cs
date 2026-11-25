using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Copias de seguridad de la base de datos.</summary>
    public class SeccionRespaldos : UserControl
    {
        public SeccionRespaldos()
        {
            BackColor = Tema.Crema;
            var p = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            Controls.Add(p);
            Controls.Add(AdminUtil.TituloSeccion("Respaldos"));

            int y = 16;
            p.Controls.Add(Ui.Etiqueta("Carpeta de datos:", 16, y, Tema.Negrita));
            var ruta = Ui.Caja(16, y + 24, 620);
            ruta.ReadOnly = true;
            ruta.Text = Db.Carpeta;
            p.Controls.Add(ruta);
            y += 64;
            p.Controls.Add(Ui.Etiqueta("El sistema hace un respaldo automático al iniciar cada día y guarda los últimos 30 en la carpeta \"respaldos\".\n" +
                "Recomendación: una vez por semana respalde también a una memoria USB.", 16, y, null, Tema.Humo));
            y += 50;

            p.Controls.Add(Ui.Boton("Respaldar ahora", 16, y, 180, 42, (s, e) => Respaldar(null), true));
            p.Controls.Add(Ui.Boton("Respaldar a USB…", 206, y, 180, 42, (s, e) => RespaldarA()));
            p.Controls.Add(Ui.Boton("Carpeta de datos", 396, y, 170, 42, (s, e) => AbrirCarpeta()));
            y += 66;

            p.Controls.Add(Ui.Etiqueta("Restaurar", 16, y, Tema.Mediana, Tema.Rojo));
            y += 28;
            p.Controls.Add(Ui.Etiqueta("Reemplaza TODOS los datos actuales por los de un respaldo. Úselo sólo si se dañó la información o cambió de computadora.", 16, y, null, Tema.Humo));
            y += 26;
            var restaurar = Ui.Boton("Restaurar respaldo…", 16, y, 200, 42, (s, e) => { if (Restaurar()) Application.Exit(); });
            Tema.BotonPeligro(restaurar);
            p.Controls.Add(restaurar);
        }

        void Respaldar(string destino)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var archivo = Db.Respaldar(destino);
                Cursor = Cursors.Default;
                Ui.Info("Respaldo creado:\n" + archivo);
            }
            catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex, "Respaldar"); }
        }

        void RespaldarA()
        {
            using (var d = new FolderBrowserDialog { Description = "Elija la memoria USB o carpeta donde guardar el respaldo" })
                if (d.ShowDialog() == DialogResult.OK) Respaldar(d.SelectedPath);
        }

        void AbrirCarpeta()
        {
            try { Process.Start("explorer.exe", "\"" + Db.Carpeta + "\""); }
            catch (Exception ex) { Ui.Error(ex, "Abrir carpeta"); }
        }

        /// <summary>Reemplaza todo por un respaldo. true si se hizo: hay que cerrar el programa.</summary>
        public static bool Restaurar()
        {
            using (var d = new OpenFileDialog { Filter = "Respaldo VentaFácil|*.db", Title = "Elija el respaldo a restaurar" })
            {
                if (d.InitialDirectory == "") d.InitialDirectory = Path.Combine(Db.Carpeta, "respaldos");
                if (d.ShowDialog() != DialogResult.OK) return false;
                if (string.Equals(Path.GetFullPath(d.FileName), Path.GetFullPath(Db.Archivo), StringComparison.OrdinalIgnoreCase))
                { Ui.Aviso("Ese es el archivo de datos actual."); return false; }
                if (!Ui.Confirmar("ATENCIÓN: se reemplazarán TODOS los datos actuales (ventas, productos, usuarios) por los del respaldo:\n\n" +
                    d.FileName + "\n\nAntes se guardará una copia de los datos actuales.\n¿Continuar?")) return false;
                if (Ui.PedirTexto("Confirmar", "Escriba RESTAURAR para confirmar:") != "RESTAURAR") return false;
                try
                {
                    Db.Respaldar(); // copia de lo actual, por si acaso
                    System.Data.SQLite.SQLiteConnection.ClearAllPools();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    foreach (var extra in new[] { Db.Archivo + "-wal", Db.Archivo + "-shm" })
                        if (File.Exists(extra)) File.Delete(extra);
                    File.Copy(d.FileName, Db.Archivo, true);
                    Ui.Info("Respaldo restaurado.\n\nEl programa se cerrará. Vuelva a abrirlo.");
                    return true;
                }
                catch (Exception ex) { Ui.Error(ex, "No se pudo restaurar. Cierre y abra el programa e intente de nuevo"); return false; }
            }
        }
    }
}
