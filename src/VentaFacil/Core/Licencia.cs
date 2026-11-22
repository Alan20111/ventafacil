using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using VentaFacil.Data;

namespace VentaFacil.Core
{
    public enum EstadoLicencia { Activa, Prueba, Vencida }

    public class InfoLicencia
    {
        public EstadoLicencia Estado;
        public int DiasRestantes;      // en prueba
        public DateTime? Vence;        // licencia con vencimiento
        public string Motivo = "";
    }

    /// <summary>
    /// Licencia de VentaFácil: 30 días de prueba y después clave de activación, atada al equipo.
    /// La clave es un código de 16 caracteres calculado a partir del código del equipo (ver LicenciaNucleo).
    /// Se guarda fuera de la base de datos (licencia.dat y el registro de Windows) para que copiar
    /// o restaurar la base no copie ni borre la licencia.
    /// Limite asumido: es un freno contra copiar el programa a otra PC, no contra quien parchee el .exe.
    /// </summary>
    public static class Licencia
    {
        public const int DiasPrueba = 30;
        const string ClaveRegistro = @"Software\VentaFacil";
        const string Formato = "yyyyMMdd";

        static string _codigo;

        /// <summary>Código que se muestra al cliente. Estable mientras no se reinstale Windows.</summary>
        public static string CodigoInstalacion()
        {
            if (_codigo != null) return _codigo;
            string semilla = LeerMachineGuid();
            if (string.IsNullOrEmpty(semilla)) semilla = Environment.MachineName; // respaldo débil
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes("VentaFacil::maquina::" + semilla));
            var hex = new StringBuilder();
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            _codigo = LicenciaNucleo.CodigoInstalacion(hex.ToString());
            return _codigo;
        }

        static string LeerMachineGuid()
        {
            foreach (var vista in new[] { RegistryView.Registry64, RegistryView.Default })
            {
                try
                {
                    using (var bas = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, vista))
                    using (var k = bas.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    {
                        string v = k == null ? "" : Convert.ToString(k.GetValue("MachineGuid"));
                        if (!string.IsNullOrWhiteSpace(v)) return v;
                    }
                }
                catch (Exception) { }
            }
            return "";
        }

        // ---------------- lo guardado ----------------
        class Guardado { public DateTime? Inicio; public DateTime? Ultimo; public string Clave = ""; }

        static string RutaArchivo { get { return Path.Combine(Db.Carpeta, "licencia.dat"); } }

        static DateTime? Fecha(string s)
        {
            DateTime d;
            return DateTime.TryParseExact((s ?? "").Trim(), Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? (DateTime?)d : null;
        }

        static Guardado LeerGuardado()
        {
            var g = new Guardado();
            try
            {
                if (File.Exists(RutaArchivo))
                    foreach (string linea in File.ReadAllLines(RutaArchivo))
                    {
                        int i = linea.IndexOf('=');
                        if (i < 0) continue;
                        string k = linea.Substring(0, i), v = linea.Substring(i + 1);
                        if (k == "inicio") g.Inicio = Fecha(v);
                        else if (k == "ultimo") g.Ultimo = Fecha(v);
                        else if (k == "clave") g.Clave = v.Trim();
                    }
            }
            catch (Exception) { }
            try
            {
                using (var r = Registry.CurrentUser.OpenSubKey(ClaveRegistro))
                {
                    if (r != null)
                    {
                        // Si falta un dato en el archivo o hay dos, se toma el más desfavorable al "reinicio" de la prueba
                        var ri = Fecha(Convert.ToString(r.GetValue("Inicio")));
                        var ru = Fecha(Convert.ToString(r.GetValue("Ultimo")));
                        if (ri.HasValue && (!g.Inicio.HasValue || ri.Value < g.Inicio.Value)) g.Inicio = ri;
                        if (ru.HasValue && (!g.Ultimo.HasValue || ru.Value > g.Ultimo.Value)) g.Ultimo = ru;
                        if (g.Clave == "") g.Clave = Convert.ToString(r.GetValue("Clave")) ?? "";
                    }
                }
            }
            catch (Exception) { }
            return g;
        }

        static void Guardar(Guardado g)
        {
            try
            {
                Directory.CreateDirectory(Db.Carpeta);
                File.WriteAllLines(RutaArchivo, new[] {
                    "inicio=" + (g.Inicio.HasValue ? g.Inicio.Value.ToString(Formato, CultureInfo.InvariantCulture) : ""),
                    "ultimo=" + (g.Ultimo.HasValue ? g.Ultimo.Value.ToString(Formato, CultureInfo.InvariantCulture) : ""),
                    "clave=" + g.Clave });
            }
            catch (Exception ex) { Log.Error(ex, "Guardar licencia (archivo)"); }
            try
            {
                using (var r = Registry.CurrentUser.CreateSubKey(ClaveRegistro))
                {
                    r.SetValue("Inicio", g.Inicio.HasValue ? g.Inicio.Value.ToString(Formato, CultureInfo.InvariantCulture) : "");
                    r.SetValue("Ultimo", g.Ultimo.HasValue ? g.Ultimo.Value.ToString(Formato, CultureInfo.InvariantCulture) : "");
                    r.SetValue("Clave", g.Clave);
                }
            }
            catch (Exception ex) { Log.Error(ex, "Guardar licencia (registro)"); }
        }

        // ---------------- estado ----------------
        /// <summary>Evalúa la licencia. Si algo inesperado falla, el negocio NO se queda sin poder cobrar.</summary>
        public static InfoLicencia Evaluar()
        {
            try { return EvaluarInterno(DateTime.Today); }
            catch (Exception ex)
            {
                Log.Error(ex, "Evaluar licencia");
                return new InfoLicencia { Estado = EstadoLicencia.Prueba, DiasRestantes = DiasPrueba };
            }
        }

        public static InfoLicencia EvaluarInterno(DateTime hoy)
        {
            var g = LeerGuardado();
            var info = new InfoLicencia();

            string motivoClave;
            if (g.Clave != "" && LicenciaNucleo.Verificar(CodigoInstalacion(), g.Clave, out motivoClave))
            {
                info.Estado = EstadoLicencia.Activa;
                return info;
            }
            // Clave guardada que ya no corresponde (otra PC): se cae a la prueba

            if (!g.Inicio.HasValue) g.Inicio = hoy;
            bool relojAtrasado = g.Ultimo.HasValue && hoy < g.Ultimo.Value.AddDays(-1);
            if (!relojAtrasado && (!g.Ultimo.HasValue || hoy > g.Ultimo.Value)) g.Ultimo = hoy;
            Guardar(g);

            int usados = (int)(hoy - g.Inicio.Value).TotalDays;
            info.DiasRestantes = Math.Max(0, DiasPrueba - usados);
            if (relojAtrasado)
            {
                info.Estado = EstadoLicencia.Vencida;
                info.Motivo = "La fecha de la computadora está atrasada. Corríjala para continuar.";
            }
            else if (info.DiasRestantes <= 0)
            {
                info.Estado = EstadoLicencia.Vencida;
                info.Motivo = "Terminó el período de prueba de " + DiasPrueba + " días. Active VentaFácil con la clave de su proveedor.";
            }
            else info.Estado = EstadoLicencia.Prueba;
            return info;
        }

        /// <summary>Valida y guarda la clave. Devuelve null si quedó activada, o el motivo si no.</summary>
        public static string Activar(string clave)
        {
            string motivo;
            if (string.IsNullOrWhiteSpace(clave)) return "Escriba la clave de activación que le dio su proveedor.";
            if (!LicenciaNucleo.Verificar(CodigoInstalacion(), clave, out motivo)) return motivo;
            var g = LeerGuardado();
            g.Clave = Base32.Agrupar(Base32.Normalizar(clave));
            Guardar(g);
            return null;
        }
    }
}
