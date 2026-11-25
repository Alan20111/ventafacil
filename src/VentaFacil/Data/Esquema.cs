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
            if (v < 5)
            {
                // v5: envíos de la oficina a la tienda (ver Data.Envios). Cada producto lleva un identificador que nunca cambia
                if (System.Convert.ToInt64(Db.Scalar(cn, null, "SELECT COUNT(*) FROM pragma_table_info('productos') WHERE name='uid'")) == 0)
                    Db.Exec(cn, null, "ALTER TABLE productos ADD COLUMN uid TEXT NOT NULL DEFAULT ''");
                Db.Exec(cn, null, "UPDATE productos SET uid=lower(hex(randomblob(16))) WHERE uid=''");
                Db.Exec(cn, null, @"
CREATE UNIQUE INDEX IF NOT EXISTS ix_productos_uid ON productos(uid);
CREATE TABLE IF NOT EXISTS enlace (clave TEXT PRIMARY KEY, valor TEXT);      -- rol OFICINA | TIENDA, llaves y con quién está enlazada
-- En la OFICINA: cada cambio de producto, numerado. Se llena solo con los disparadores de abajo
CREATE TABLE IF NOT EXISTS cambios (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    uid TEXT NOT NULL DEFAULT (lower(hex(randomblob(16)))),
    fecha TEXT NOT NULL DEFAULT (datetime('now','localtime')),
    producto_uid TEXT NOT NULL,
    campo TEXT NOT NULL,                      -- alta | eliminado | existencia | el nombre de la columna
    antes, despues,
    cantidad REAL NOT NULL DEFAULT 0,         -- solo existencia: lo que se suma (o se resta)
    referencia TEXT
);
CREATE TABLE IF NOT EXISTS envios (           -- en la OFICINA: envíos creados
    id TEXT PRIMARY KEY, num INTEGER NOT NULL, fecha TEXT NOT NULL, usuario TEXT, desde INTEGER NOT NULL, hasta INTEGER NOT NULL,
    archivo TEXT, resumen TEXT, estado TEXT NOT NULL DEFAULT 'CREADO',       -- CREADO | APLICADO | DESHECHO
    aplicado TEXT, problemas TEXT
);
CREATE TABLE IF NOT EXISTS recibidos (seq INTEGER PRIMARY KEY, uid TEXT NOT NULL, envio_id TEXT NOT NULL);   -- en la TIENDA: cambios ya aplicados
CREATE TABLE IF NOT EXISTS envios_aplicados ( -- en la TIENDA
    orden INTEGER PRIMARY KEY AUTOINCREMENT,
    id TEXT NOT NULL, num INTEGER NOT NULL, oficina_id TEXT NOT NULL, fecha_envio TEXT, fecha TEXT NOT NULL, usuario_id INTEGER, usuario TEXT,
    resumen TEXT, estado TEXT NOT NULL DEFAULT 'APLICADO', respaldo TEXT     -- APLICADO | DESHECHO
);
CREATE TABLE IF NOT EXISTS envio_detalle (
    id INTEGER PRIMARY KEY AUTOINCREMENT, orden INTEGER NOT NULL REFERENCES envios_aplicados(orden), producto_id INTEGER,
    tipo TEXT NOT NULL, codigo TEXT, producto TEXT, campo TEXT, antes TEXT, despues TEXT, cantidad REAL NOT NULL DEFAULT 0, nota TEXT
);
CREATE INDEX IF NOT EXISTS ix_envdet ON envio_detalle(orden);");
                Db.Exec(cn, null, "PRAGMA user_version = 5");
            }
            Disparadores(cn);
        }

        /// <summary>
        /// Disparadores que anotan en «cambios» lo que se modifica en el catálogo, sin importar desde qué pantalla
        /// (solo si este equipo es la OFICINA). La existencia NO se anota aquí: viaja lo que se capture en «Llegó mercancía».
        /// </summary>
        static void Disparadores(SQLiteConnection cn)
        {
            const string Oficina = "(SELECT valor FROM enlace WHERE clave='rol')='OFICINA'";
            Db.Exec(cn, null, "CREATE TRIGGER IF NOT EXISTS tr_prod_alta AFTER INSERT ON productos BEGIN " +
                "UPDATE productos SET uid=lower(hex(randomblob(16))) WHERE id=NEW.id AND uid=''; " +
                "INSERT INTO cambios(producto_uid,campo,despues) SELECT uid,'alta',NEW.descripcion FROM productos WHERE id=NEW.id AND " + Oficina + "; " +
                "INSERT INTO cambios(producto_uid,campo,cantidad,referencia) SELECT uid,'existencia',NEW.existencia,'Alta de producto' FROM productos WHERE id=NEW.id AND NEW.existencia<>0 AND NEW.usa_inventario=1 AND " + Oficina + "; END");
            Db.Exec(cn, null, "CREATE TRIGGER IF NOT EXISTS tr_prod_baja AFTER DELETE ON productos WHEN " + Oficina + " BEGIN " +
                "INSERT INTO cambios(producto_uid,campo,antes,referencia) VALUES(OLD.uid,'eliminado',OLD.descripcion,OLD.codigo); END");
            foreach (var c in Envios.Campos)
            {
                string valor = c.Nombre == "departamento" ? "(SELECT nombre FROM departamentos WHERE id={0}.departamento_id)" : "{0}." + c.Columna;
                Db.Exec(cn, null, "CREATE TRIGGER IF NOT EXISTS tr_prod_" + c.Nombre + " AFTER UPDATE OF " + c.Columna + " ON productos WHEN OLD." + c.Columna + " IS NOT NEW." + c.Columna + " AND " + Oficina + " BEGIN " +
                    "INSERT INTO cambios(producto_uid,campo,antes,despues) VALUES(NEW.uid,'" + c.Nombre + "'," + string.Format(valor, "OLD") + "," + string.Format(valor, "NEW") + "); END");
            }
            Db.Exec(cn, null, "CREATE TRIGGER IF NOT EXISTS tr_depto_nombre AFTER UPDATE OF nombre ON departamentos WHEN OLD.nombre IS NOT NEW.nombre AND " + Oficina + " BEGIN " +
                "INSERT INTO cambios(producto_uid,campo,antes,despues) SELECT uid,'departamento',OLD.nombre,NEW.nombre FROM productos WHERE departamento_id=NEW.id; END");
        }
    }
}
