using Microsoft.Data.Sqlite;
using VentaFacil.Core;

// Pruebas de dinero: ¿se pierde algún centavo al guardar en SQLite (REAL) y leer con Dinero.Leer?
// Uso: dotnet run -c Release          (tarda unos segundos)
int fallas = 0;
void Revisar(string nombre, long malos, string detalle = "")
{
    Console.WriteLine((malos == 0 ? "[OK]    " : "[FALLA] ") + nombre + (detalle == "" ? "" : "  " + detalle));
    if (malos != 0) fallas++;
}

// 1) Cada importe de $0.00 a $1,000,000.00 sale idéntico después de pasar por double (REAL)
long malos = 0;
for (long c = 0; c <= 100_000_000; c++)
{
    decimal d = c / 100m;
    if (Dinero.Leer((double)d) != d) malos++;
}
Revisar("100,000,001 importes hasta $1,000,000.00 salen idénticos", malos);

// 2) Cantidades a granel con 3 decimales hasta 5,000 kg
malos = 0;
for (long m = 0; m <= 5_000_000; m++) { decimal d = m / 1000m; if (Dinero.Leer((double)d) != d) malos++; }
Revisar("5,000,001 cantidades con 3 decimales (kg) salen idénticas", malos);

// 3) Negativos (cancelaciones, salidas de caja, mermas)
malos = 0;
for (long c = 1; c <= 10_000_000; c++) { decimal d = -c / 100m; if (Dinero.Leer((double)d) != d) malos++; }
Revisar("10,000,000 importes negativos salen idénticos", malos);

// 4) Sumas como las de un corte o reporte: SUM() de SQLite contra la suma exacta
var rnd = new Random(42);
using var cn = new SqliteConnection("Data Source=:memory:"); cn.Open();
using (var c0 = new SqliteCommand("CREATE TABLE v(total REAL NOT NULL)", cn)) c0.ExecuteNonQuery();
decimal exacta = 0m; int n = 2_000_000;
using (var tx = cn.BeginTransaction())
{
    var cmd = new SqliteCommand("INSERT INTO v(total) VALUES(@t)", cn, tx); cmd.Parameters.Add("@t", SqliteType.Real);
    for (int i = 0; i < n; i++) { decimal t = rnd.Next(100, 150000) / 100m; exacta += t; cmd.Parameters["@t"].Value = (double)t; cmd.ExecuteNonQuery(); }
    tx.Commit();
}
object suma = new SqliteCommand("SELECT SUM(total) FROM v", cn).ExecuteScalar();
decimal leida = Dinero.Leer(suma);
Revisar($"suma de {n:N0} ventas coincide al centavo", leida == exacta ? 0 : 1, $"(exacta {exacta:N2} | leída {leida:N2})");

// 5) Sumas con ROUND(SUM(),2), como en el corte de caja, y comparación exacta "la caja cuadra"
object redondeada = new SqliteCommand("SELECT ROUND(SUM(total),2) FROM v", cn).ExecuteScalar();
Revisar("ROUND(SUM(),2) leído es exactamente igual al total (caja cuadra)", Dinero.Leer(redondeada) == exacta ? 0 : 1);

// 6) Ruido típico de double
Revisar("0.1 + 0.2 se lee como 0.3", Dinero.Leer(0.1 + 0.2) == 0.3m ? 0 : 1);
Revisar("renglón 0.750 kg x $112.50 = $84.38 (redondeo hacia arriba)", Dinero.R2(0.750m * 112.50m) == 84.38m ? 0 : 1);
Revisar("null y DBNull se leen como 0", Dinero.Leer(null) == 0m && Dinero.Leer(DBNull.Value) == 0m ? 0 : 1);

Console.WriteLine(fallas == 0 ? "\nTodas las pruebas de dinero pasaron." : $"\n{fallas} prueba(s) fallaron.");
return fallas == 0 ? 0 : 1;
