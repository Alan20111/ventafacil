using System;
using System.Collections.Generic;
using System.Linq;
using VentaFacil.Data;

namespace VentaFacil.Core
{
    /// <summary>
    /// Qué puede hacer cada usuario. El ADMIN puede todo (siempre: así nadie se queda sin entrar). El CAJERO tiene lo
    /// básico de fábrica y, si se le marcan casillas propias, mandan esas casillas.
    /// Se guarda en usuarios.permisos: "" = los de fábrica;  "=turno,cajon" = lista propia;  "=" = ninguno;
    /// "*" = todos los de la lista (Encargado), incluidos los que se agreguen en el futuro.
    /// El PERFIL que se ve en pantalla (Cajero, Encargado, Personalizado, Administrador) se deduce de esto: no se guarda aparte.
    /// Cobrar y sacar el corte X propio no se pueden quitar a nadie.
    /// </summary>
    public static class Permisos
    {
        public const string Turno = "turno", Movimientos = "movimientos", Cajon = "cajon", Descuentos = "descuentos", Cancelar = "cancelar",
            CorteCaja = "corte_caja", CorteZ = "corte_z", Envios = "envios", Catalogo = "catalogo", Almacen = "almacen", Resultados = "resultados", Ajustes = "ajustes",
            SoloAdmin = "solo_admin";   // usuarios y respaldos: no se puede dar con casillas (se daría permisos a sí mismo)

        const string Encargado = "*";
        public const string PerfilCajero = "Cajero", PerfilEncargado = "Encargado", PerfilPersonalizado = "Personalizado", PerfilAdmin = "Administrador";

        public class Info { public string Clave, Nombre; public bool Cajero; }

        public static readonly Info[] Todos =
        {
            new Info { Clave = Turno, Nombre = "Módulo Turno", Cajero = true },
            new Info { Clave = Catalogo, Nombre = "Módulo Catálogo" },
            new Info { Clave = Almacen, Nombre = "Módulo Almacén" },
            new Info { Clave = Resultados, Nombre = "Módulo Resultados" },
            new Info { Clave = Ajustes, Nombre = "Módulo Ajustes" },
            new Info { Clave = Movimientos, Nombre = "Entradas y salidas", Cajero = true },
            new Info { Clave = Cajon, Nombre = "Abrir el cajón", Cajero = true },
            new Info { Clave = Descuentos, Nombre = "Descuentos y precio" },
            new Info { Clave = Cancelar, Nombre = "Cancelar tickets" },
            new Info { Clave = CorteCaja, Nombre = "Corte X de caja y otros" },
            new Info { Clave = CorteZ, Nombre = "Corte Z (cierre)" },
            new Info { Clave = Envios, Nombre = "Envíos y reporte", Cajero = true }
        };

        /// <summary>Los de un cajero sin casillas propias. El ajuste general «los cajeros pueden cancelar…» suma esos dos.</summary>
        static bool DeFabrica(string clave, bool cajeroPuedeCancelar)
        {
            if ((clave == Descuentos || clave == Cancelar) && cajeroPuedeCancelar) return true;
            return Todos.Any(p => p.Clave == clave && p.Cajero);
        }

        public static bool Tiene(Usuario u, string clave) { return Tiene(u, clave, Config.Actual != null && Config.Actual.CajeroPuedeCancelar); }

        public static bool Tiene(Usuario u, string clave, bool cajeroPuedeCancelar)
        {
            if (u == null || !u.Activo) return false;
            if (u.EsAdmin) return true;
            if (!Todos.Any(p => p.Clave == clave)) return false;    // SoloAdmin, o una clave desconocida
            string g = u.Permisos ?? "";
            if (g == Encargado) return true;
            if (!g.StartsWith("=", StringComparison.Ordinal)) return DeFabrica(clave, cajeroPuedeCancelar);
            return g.Substring(1).Split(',').Contains(clave);
        }

        /// <summary>Las casillas que trae un perfil. null si el perfil no trae ninguna fija (Personalizado).</summary>
        public static List<string> DePerfil(string perfil, bool cajeroPuedeCancelar)
        {
            if (perfil == PerfilCajero) return Todos.Where(p => DeFabrica(p.Clave, cajeroPuedeCancelar)).Select(p => p.Clave).ToList();
            if (perfil == PerfilEncargado || perfil == PerfilAdmin) return Todos.Select(p => p.Clave).ToList();
            return null;
        }

        /// <summary>El perfil de unas casillas (nunca Administrador: ese no depende de casillas).</summary>
        public static string PerfilDe(ICollection<string> marcados, bool cajeroPuedeCancelar)
        {
            if (Todos.All(p => marcados.Contains(p.Clave))) return PerfilEncargado;
            return Todos.All(p => marcados.Contains(p.Clave) == DeFabrica(p.Clave, cajeroPuedeCancelar)) ? PerfilCajero : PerfilPersonalizado;
        }

        public static string PerfilDe(Usuario u, bool cajeroPuedeCancelar)
        {
            if (u.EsAdmin) return PerfilAdmin;
            var copia = new Usuario { Rol = u.Rol, Permisos = u.Permisos };   // activo: el perfil no depende de si está desactivado
            return PerfilDe(Todos.Where(p => Tiene(copia, p.Clave, cajeroPuedeCancelar)).Select(p => p.Clave).ToList(), cajeroPuedeCancelar);
        }

        /// <summary>El texto que se guarda. Si lo marcado es justo lo de fábrica, se guarda "" (así sigue al ajuste general).</summary>
        public static string Guardar(ICollection<string> marcados, bool cajeroPuedeCancelar)
        {
            if (Todos.All(p => marcados.Contains(p.Clave))) return Encargado;
            if (Todos.All(p => marcados.Contains(p.Clave) == DeFabrica(p.Clave, cajeroPuedeCancelar))) return "";
            return "=" + string.Join(",", Todos.Where(p => marcados.Contains(p.Clave)).Select(p => p.Clave).ToArray());
        }
    }
}
