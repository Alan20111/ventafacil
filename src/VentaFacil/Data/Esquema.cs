using System.Data.SQLite;

namespace VentaFacil.Data
{
    static class Esquema
    {
        public static void Crear(SQLiteConnection cn)
        {
            Db.Exec(cn, null, @"
CREATE TABLE IF NOT EXISTS config (
    clave TEXT PRIMARY KEY,
    valor TEXT
);
CREATE TABLE IF NOT EXISTS usuarios (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    usuario TEXT NOT NULL UNIQUE COLLATE NOCASE,
    nombre TEXT NOT NULL,
    clave_hash TEXT NOT NULL,
    rol TEXT NOT NULL DEFAULT 'CAJERO',      -- ADMIN | CAJERO
    activo INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS departamentos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre TEXT NOT NULL UNIQUE COLLATE NOCASE
);
CREATE TABLE IF NOT EXISTS productos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    codigo TEXT NOT NULL UNIQUE COLLATE NOCASE,
    codigo2 TEXT COLLATE NOCASE,
    descripcion TEXT NOT NULL,
    departamento_id INTEGER REFERENCES departamentos(id),
    precio REAL NOT NULL DEFAULT 0,
    costo REAL NOT NULL DEFAULT 0,
    precio_mayoreo REAL NOT NULL DEFAULT 0,
    mayoreo_desde REAL NOT NULL DEFAULT 0,
    granel INTEGER NOT NULL DEFAULT 0,        -- 1 = se vende por peso (kg)
    usa_inventario INTEGER NOT NULL DEFAULT 1,
    existencia REAL NOT NULL DEFAULT 0,
    minimo REAL NOT NULL DEFAULT 0,
    activo INTEGER NOT NULL DEFAULT 1,
    actualizado TEXT
);
CREATE INDEX IF NOT EXISTS ix_productos_codigo2 ON productos(codigo2);
CREATE INDEX IF NOT EXISTS ix_productos_desc ON productos(descripcion);

CREATE TABLE IF NOT EXISTS turnos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    caja INTEGER NOT NULL DEFAULT 1,
    apertura TEXT NOT NULL,
    abierto_por INTEGER REFERENCES usuarios(id),
    fondo REAL NOT NULL DEFAULT 0,
    cierre TEXT,
    cerrado_por INTEGER REFERENCES usuarios(id),
    efectivo_declarado REAL,
    estado TEXT NOT NULL DEFAULT 'ABIERTO'    -- ABIERTO | CERRADO
);
CREATE TABLE IF NOT EXISTS ventas (
    id INTEGER PRIMARY KEY AUTOINCREMENT,     -- es el número de ticket
    turno_id INTEGER REFERENCES turnos(id),
    caja INTEGER NOT NULL DEFAULT 1,
    usuario_id INTEGER REFERENCES usuarios(id),
    fecha TEXT NOT NULL,
    articulos REAL NOT NULL DEFAULT 0,
    subtotal REAL NOT NULL DEFAULT 0,
    descuento REAL NOT NULL DEFAULT 0,
    total REAL NOT NULL DEFAULT 0,
    forma_pago TEXT NOT NULL DEFAULT 'EFECTIVO',
    pago_con REAL NOT NULL DEFAULT 0,
    cambio REAL NOT NULL DEFAULT 0,
    referencia TEXT,
    estado TEXT NOT NULL DEFAULT 'PAGADA',    -- PAGADA | CANCELADA
    cancelada_por INTEGER REFERENCES usuarios(id),
    cancelada_fecha TEXT,
    cancelada_turno INTEGER
);
CREATE INDEX IF NOT EXISTS ix_ventas_fecha ON ventas(fecha);
CREATE INDEX IF NOT EXISTS ix_ventas_turno ON ventas(turno_id);

CREATE TABLE IF NOT EXISTS venta_detalle (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    venta_id INTEGER NOT NULL REFERENCES ventas(id),
    producto_id INTEGER REFERENCES productos(id),
    codigo TEXT,
    descripcion TEXT,
    cantidad REAL NOT NULL,
    precio REAL NOT NULL,
    descuento REAL NOT NULL DEFAULT 0,
    importe REAL NOT NULL,
    costo REAL NOT NULL DEFAULT 0,
    granel INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_detalle_venta ON venta_detalle(venta_id);
CREATE INDEX IF NOT EXISTS ix_detalle_producto ON venta_detalle(producto_id);

CREATE TABLE IF NOT EXISTS movimientos_caja (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    turno_id INTEGER REFERENCES turnos(id),
    usuario_id INTEGER REFERENCES usuarios(id),
    fecha TEXT NOT NULL,
    tipo TEXT NOT NULL,                       -- ENTRADA | SALIDA
    monto REAL NOT NULL,
    concepto TEXT
);
CREATE TABLE IF NOT EXISTS movimientos_inv (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    producto_id INTEGER NOT NULL REFERENCES productos(id),
    fecha TEXT NOT NULL,
    tipo TEXT NOT NULL,                       -- VENTA | CANCELACION | ENTRADA | AJUSTE | SALIDA
    cantidad REAL NOT NULL,
    existencia REAL NOT NULL,
    referencia TEXT,
    usuario_id INTEGER
);
CREATE INDEX IF NOT EXISTS ix_movinv_prod ON movimientos_inv(producto_id);

CREATE TABLE IF NOT EXISTS suspendidas (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    fecha TEXT NOT NULL,
    usuario_id INTEGER,
    nota TEXT,
    total REAL NOT NULL DEFAULT 0,
    datos TEXT NOT NULL
);
");
        }

        public static void DatosIniciales(SQLiteConnection cn)
        {
            Db.Exec(cn, null, "INSERT OR IGNORE INTO usuarios(usuario,nombre,clave_hash,rol) VALUES(@p0,@p1,@p2,'ADMIN')",
                "admin", "Administrador", Core.Seguridad.Hash("admin"));
            Db.Exec(cn, null, "INSERT OR IGNORE INTO departamentos(nombre) VALUES('General')");
        }

        /// <summary>Lugar para cambios de esquema en versiones futuras.</summary>
        public static void Migrar(SQLiteConnection cn)
        {
            var ver = Db.Scalar(cn, null, "PRAGMA user_version");
            long v = ver == null ? 0 : System.Convert.ToInt64(ver);
            if (v < 1) Db.Exec(cn, null, "PRAGMA user_version = 1");
            if (v < 2)
            {
                // v2: estilo semillera — proveedor, costo por bulto, medio mayoreo y orden de la lista
                foreach (var col in new[] {
                    "proveedor TEXT NOT NULL DEFAULT ''", "costo_bulto REAL NOT NULL DEFAULT 0", "cant_bulto REAL NOT NULL DEFAULT 0",
                    "precio_medio REAL NOT NULL DEFAULT 0", "medio_desde REAL NOT NULL DEFAULT 0", "orden INTEGER NOT NULL DEFAULT 0" })
                {
                    try { Db.Exec(cn, null, "ALTER TABLE productos ADD COLUMN " + col); }
                    catch (System.Data.SQLite.SQLiteException) { /* ya existe */ }
                }
                Db.Exec(cn, null, "PRAGMA user_version = 2");
            }
            if (v < 3)
            {
                // v3: productos que NO llevan código de barras (granel, verdura): se cobran con su código corto o por nombre
                try { Db.Exec(cn, null, "ALTER TABLE productos ADD COLUMN sin_barras INTEGER NOT NULL DEFAULT 0"); }
                catch (System.Data.SQLite.SQLiteException) { /* ya existe */ }
                Db.Exec(cn, null, "PRAGMA user_version = 3");
            }
            if (v < 4)
            {
                // v4: permisos por usuario (ver Core.Permisos). Vacío = los de su tipo: nadie gana ni pierde nada al actualizar
                if (System.Convert.ToInt64(Db.Scalar(cn, null, "SELECT COUNT(*) FROM pragma_table_info('usuarios') WHERE name='permisos'")) == 0)
                    Db.Exec(cn, null, "ALTER TABLE usuarios ADD COLUMN permisos TEXT NOT NULL DEFAULT ''");
                Db.Exec(cn, null, "PRAGMA user_version = 4");
            }
        }
    }
}
