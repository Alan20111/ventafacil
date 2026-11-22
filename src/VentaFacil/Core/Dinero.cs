using System;

namespace VentaFacil.Core
{
    /// <summary>
    /// Lectura segura de importes y cantidades guardados como REAL en SQLite.
    /// Un REAL es un double y no puede representar 84.38 exacto (queda 84.37999999999999545...).
    /// Al convertirlo a decimal, versiones distintas de .NET dan resultados distintos; aquí se redondea
    /// siempre a 6 decimales (los importes usan 2 y las cantidades 3), así 84.38 siempre sale 84.38 y
    /// las comparaciones exactas (por ejemplo, "la caja cuadra") no fallan por ruido de decimales.
    /// Este archivo no depende de Windows ni de SQLite: lo usa también herramientas/pruebas-dinero.
    /// </summary>
    public static class Dinero
    {
        public const int Decimales = 6;

        public static decimal Leer(object v)
        {
            if (v == null || v is DBNull) return 0m;
            if (v is double)
            {
                double d = (double)v;
                if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > 7.9e27) return 0m;
                return Math.Round(Convert.ToDecimal(d), Decimales, MidpointRounding.AwayFromZero);
            }
            if (v is float) return Leer((double)(float)v);
            return Math.Round(Convert.ToDecimal(v), Decimales, MidpointRounding.AwayFromZero);
        }

        /// <summary>Centavos: para mostrar y guardar importes de dinero.</summary>
        public static decimal R2(decimal v) { return Math.Round(v, 2, MidpointRounding.AwayFromZero); }
    }
}
