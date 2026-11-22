using System;
using System.Globalization;

namespace VentaFacil.Core
{
    /// <summary>Formatos de dinero y cantidades. Siempre punto decimal, estilo México.</summary>
    public static class Fmt
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string Dinero(decimal v) { return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("#,##0.00", Inv); }
        public static string Dec(decimal v) { return v.ToString("0.###", Inv); }
        public static string Cant(decimal v, bool granel) { return granel ? v.ToString("0.000", Inv) : v.ToString("0.###", Inv); }

        public static decimal ParseDec(string s)
        {
            decimal d;
            return TryDec(s, out d) ? d : 0m;
        }

        public static bool TryDec(string s, out decimal d)
        {
            s = (s ?? "").Trim().Replace("$", "").Replace(" ", "");
            // acepta coma decimal si no hay punto
            if (s.IndexOf(',') >= 0 && s.IndexOf('.') < 0 && s.Split(',').Length == 2 && s.Split(',')[1].Length != 3) s = s.Replace(',', '.');
            s = s.Replace(",", "");
            return decimal.TryParse(s, NumberStyles.Number, Inv, out d);
        }

        public static decimal R2(decimal v) { return Math.Round(v, 2, MidpointRounding.AwayFromZero); }
    }
}
