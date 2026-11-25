using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VentaFacil.Data;
using VentaFacil.Forms.Admin;

namespace VentaFacil.Forms.Pos
{
    /// <summary>Lo que se encontró en una USB y vale la pena avisar.</summary>
    class HallazgoUsb { public string Ruta, Texto; public bool EsAcuse; }

    /// <summary>
    /// Busca en las USB, en segundo plano (la caja no se detiene), un envío que esta tienda no tenga
    /// —o, en la oficina, un acuse nuevo—. Solo lee: nunca aplica nada.
    /// </summary>
    static class VigiaUsb
    {
        const int WM_DEVICECHANGE = 0x0219, Llego = 0x8000, SeFue = 0x8004;
        static int _buscando;

        /// <summary>Solo unidades de disco (tipo 2): una impresora o un lector que se conecta no cuenta.</summary>
        static bool EsUnidad(ref Message m, int evento)
        {
            return m.Msg == WM_DEVICECHANGE && m.WParam.ToInt64() == evento && m.LParam != IntPtr.Zero && Marshal.ReadInt32(m.LParam, 4) == 2;
        }
        public static bool Conectaron(ref Message m) { return EsUnidad(ref m, Llego); }
        public static bool Quitaron(ref Message m) { return EsUnidad(ref m, SeFue); }

        /// <summary>Espera a que Windows termine de montar la memoria, busca y avisa en la ventana (o no avisa si no hay nada).</summary>
        public static void Buscar(Control ventana, int esperaMs, Action<HallazgoUsb> avisar)
        {
            if (Interlocked.Exchange(ref _buscando, 1) == 1) return;
            var hilo = new Thread(() =>
            {
                HallazgoUsb h = null;
                try
                {
                    Thread.Sleep(esperaMs);
                    h = Mirar();
                }
                catch (Exception ex) { Log.Error(ex, "Buscar envíos en la USB"); }
                finally { Interlocked.Exchange(ref _buscando, 0); }
                if (h == null) return;
                try { if (ventana.IsHandleCreated && !ventana.IsDisposed) ventana.BeginInvoke((Action)(() => avisar(h))); }
                catch (InvalidOperationException) { /* se cerró la ventana entretanto */ }
            }) { IsBackground = true, Name = "VentaFacil-usb" };
            hilo.Start();
        }

        static HallazgoUsb Mirar()
        {
            if (Envios.EsTienda)
            {
                // Puede haber varios; interesa el que llega más lejos (trae todo lo anterior)
                NovedadEnvio mejor = null; string ruta = null;
                foreach (var f in EnvioUtil.BuscarEnUsb("*.vfenvio").Take(20))
                {
                    var n = Envios.Novedad(f);
                    if (n != null && (mejor == null || n.Hasta > mejor.Hasta)) { mejor = n; ruta = f; }
                }
                return mejor == null ? null : new HallazgoUsb { Ruta = ruta, Texto = "La USB trae el envío " + mejor.Num + " de la oficina (" + mejor.Cambios + (mejor.Cambios == 1 ? " cambio)" : " cambios)") };
            }
            if (Envios.EsOficina)
                foreach (var f in EnvioUtil.BuscarEnUsb("*.vfacuse").Take(20))
                    if (Envios.AcuseNuevo(f)) return new HallazgoUsb { Ruta = f, EsAcuse = true, Texto = "La USB trae un reporte nuevo de la tienda" };
            return null;
        }
    }

    /// <summary>
    /// Al abrir un envío con doble clic y VentaFácil ya está abierto, la segunda copia le pasa el archivo a la
    /// primera y termina. Solo se pasa la ruta: la ventana muestra el resumen y pregunta, como siempre.
    /// </summary>
    static class AbrirArchivo
    {
        static readonly string Canal = "VentaFacil_Abrir_" + Environment.UserName;
        static readonly object _cerrojo = new object();
        static string _pendiente;
        static Action<string> _alRecibir;

        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int proceso);

        /// <summary>El primer argumento que sea un archivo de VentaFácil, o null.</summary>
        public static string De(IEnumerable<string> args)
        {
            return args.FirstOrDefault(Es);
        }
        static bool Es(string a)
        {
            return a != null && (a.EndsWith(".vfenvio", StringComparison.OrdinalIgnoreCase) || a.EndsWith(".vfacuse", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Lo pasa a la copia que ya está abierta. false si no contestó.</summary>
        public static bool Mandar(string ruta)
        {
            try
            {
                AllowSetForegroundWindow(-1);   // deja que la otra copia se ponga al frente
                using (var p = new NamedPipeClientStream(".", Canal, PipeDirection.Out))
                {
                    p.Connect(3000);
                    var b = Encoding.UTF8.GetBytes(ruta);
                    p.Write(b, 0, b.Length);
                    p.Flush();
                }
                return true;
            }
            catch (Exception ex) { Log.Error(ex, "Pasar el archivo a VentaFácil abierto"); return false; }
        }

        /// <summary>La primera copia se queda escuchando.</summary>
        public static void Escuchar()
        {
            var hilo = new Thread(() =>
            {
                int fallas = 0;
                while (fallas < 20)
                {
                    try
                    {
                        using (var p = new NamedPipeServerStream(Canal, PipeDirection.In))
                        {
                            p.WaitForConnection();
                            using (var ms = new MemoryStream())
                            {
                                var buf = new byte[1024]; int n;
                                while ((n = p.Read(buf, 0, buf.Length)) > 0 && ms.Length < 8192) ms.Write(buf, 0, n);
                                Llego(Encoding.UTF8.GetString(ms.ToArray()));
                            }
                        }
                        fallas = 0;
                    }
                    catch (Exception ex) { fallas++; if (fallas == 1) Log.Error(ex, "Escuchar archivos"); Thread.Sleep(1000); }
                }
            }) { IsBackground = true, Name = "VentaFacil-abrir" };
            hilo.Start();
        }

        /// <summary>Un archivo por abrir: si la pantalla de venta todavía no está lista, se guarda para cuando lo esté.</summary>
        public static void Llego(string ruta)
        {
            if (!Es(ruta)) return;
            Action<string> a;
            lock (_cerrojo) { a = _alRecibir; if (a == null) { _pendiente = ruta; return; } }
            a(ruta);
        }

        /// <summary>La pantalla de venta ya puede recibir. Devuelve lo que estaba esperando.</summary>
        public static string Listo(Action<string> alRecibir)
        {
            lock (_cerrojo) { _alRecibir = alRecibir; var p = _pendiente; _pendiente = null; return p; }
        }
    }
}
