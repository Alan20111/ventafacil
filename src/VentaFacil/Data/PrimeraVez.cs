using System;
using System.Collections.Generic;
using System.IO;
using VentaFacil.Core;

namespace VentaFacil.Data
{
    /// <summary>Cuándo sale el asistente de configuración y qué falta por configurar. Sin pantallas.</summary>
    public static class PrimeraVez
    {
        public class Punto { public bool Bien; public string Texto; }

        static long N(string sql) { return Convert.ToInt64(Db.Scalar(sql)); }

        /// <summary>Base recién creada: solo la cuenta de fábrica y nada más. (Una base con datos nunca cuenta como nueva.)</summary>
        public static bool EsInstalacionNueva()
        {
            return Config.Actual.VersionVista == "" && N("SELECT COUNT(*) FROM ventas") == 0 && N("SELECT COUNT(*) FROM productos") == 0
                && N("SELECT COUNT(*) FROM usuarios") == 1 && ClaveDeFabrica() && !Envios.EsOficina && !Envios.EsTienda;
        }

        public static bool ClaveDeFabrica() { return UsuarioRepo.Validar("admin", "admin") != null; }

        /// <summary>Se actualizó (o nunca se ha preguntado): a un administrador se le ofrece configurar o no.</summary>
        public static bool HayQuePreguntar(string version, Usuario u) { return u != null && u.EsAdmin && u.Activo && Config.Actual.VersionVista != version; }

        public static void MarcarVista(string version)
        {
            var c = Config.Actual.Copia();
            c.VersionVista = version;
            c.Guardar();
        }

        /// <summary>Qué está listo y qué falta.</summary>
        public static List<Punto> Revision(bool comoOficina = false)
        {
            var c = Config.Actual;
            bool oficina = comoOficina || Envios.EsOficina;
            var l = new List<Punto>();
            Action<bool, string, string> p = (bien, si, no) => l.Add(new Punto { Bien = bien, Texto = bien ? si : no });
            p(!ClaveDeFabrica(), "La contraseña de fábrica ya no sirve", "admin / admin todavía sirve: cambie esa contraseña");
            p(c.NegocioNombre.Trim() != "" && c.NegocioNombre != "MI NEGOCIO", "Negocio: " + c.NegocioNombre, "Falta el nombre del negocio (sale en el ticket)");
            bool carpeta = false;
            try { carpeta = c.CarpetaRespaldo != "" && Directory.Exists(c.CarpetaRespaldo); } catch (Exception) { }
            p(carpeta, "Respaldos en: " + c.CarpetaRespaldo, "La carpeta de respaldos no existe o no está conectada");
            if (oficina) { p(Envios.EsOficina, "Este equipo es la oficina de " + Envios.Dato("negocio"), "Falta enlazar la oficina con la caja de la tienda"); return l; }
            long otros = N("SELECT COUNT(*) FROM usuarios WHERE activo=1 AND rol<>'ADMIN'");
            p(otros > 0, "Usuarios además del administrador: " + otros, "Falta crear el usuario de la cajera");
            p(c.ImpresoraRevisada, "Impresora: " + (c.Impresora == "" ? "la predeterminada de Windows" : c.Impresora), "Falta elegir y probar la impresora");
            long prod = N("SELECT COUNT(*) FROM productos WHERE activo=1");
            p(prod > 0, "Productos: " + prod, "Todavía no hay productos");
            return l;
        }

        /// <summary>null si la cuenta de administrador se puede guardar así; si no, qué corregir.</summary>
        public static string RevisarCuenta(string login, string nombre, string clave, string repetida)
        {
            if ((login ?? "").Trim() == "") return "Escriba el usuario con el que va a entrar.";
            if ((nombre ?? "").Trim() == "") return "Escriba su nombre.";
            if ((clave ?? "").Length < 4) return "La contraseña debe tener al menos 4 caracteres.";
            if (string.Equals(clave, "admin", StringComparison.OrdinalIgnoreCase) || string.Equals(clave, login.Trim(), StringComparison.OrdinalIgnoreCase)) return "Elija una contraseña distinta del usuario y de la de fábrica.";
            return clave != repetida ? "Las contraseñas no coinciden." : null;
        }
    }
}
