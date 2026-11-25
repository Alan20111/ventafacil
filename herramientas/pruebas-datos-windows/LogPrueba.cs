using System;
using System.Collections.Generic;
namespace VentaFacil
{
    /// <summary>En el programa, Log vive en Program.cs (que usa pantallas). Aquí solo se anota.</summary>
    public static class Log
    {
        public static List<string> Anotados = new List<string>();
        public static void Error(Exception ex, string contexto) { Anotados.Add(contexto + ": " + (ex == null ? "" : ex.Message)); }
    }
}
