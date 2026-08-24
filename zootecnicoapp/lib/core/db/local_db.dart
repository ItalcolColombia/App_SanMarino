/// Base de datos local SQLite — la app es offline-first.
/// Todo registro se guarda aquí primero; la sincronización viene después.
library;

import 'dart:convert';
import 'package:flutter/foundation.dart' show visibleForTesting;
import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart' as p;
import 'package:zootecnicoapp/core/api/inventario_api.dart' show SiloDelLote;
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/models/models_inventario.dart';

class LocalDb {
  LocalDb._([this._rutaOverride]);
  static final LocalDb instance = LocalDb._();

  /// Instancia aislada para tests: recibe la ruta del archivo (o
  /// `inMemoryDatabasePath`) en vez de usar la del dispositivo. Cada test abre la
  /// suya, así no comparten estado ni pisan la base real.
  ///
  /// Existe porque la cola de sincronización es la pieza donde un bug cuesta el
  /// trabajo de campo de alguien, y sin esto no había forma de probarla.
  @visibleForTesting
  factory LocalDb.paraPruebas(String ruta) => LocalDb._(ruta);

  final String? _rutaOverride;

  /// v2 (21ago26): sesión persistida, registros ya conocidos por el servidor y
  /// las columnas que la cola necesita para saber a dónde postear cada fila.
  /// v3 (22ago26): `lote_maestro_id`, que los módulos de postura necesitan para
  /// postear (el id de la etapa no alcanza).
  /// v4 (22ago26): catálogo de ítems y existencias, para poder elegir alimento
  /// sin señal — es lo que permite que el consumo descuente stock.
  /// v5 (22ago26): `granja_id`/`nucleo_id`/`galpon_id` en `lotes_cache` — la
  /// ubicación REAL del lote (no el texto de pantalla), para cruzar contra
  /// `existencias_inventario` y avisar disponible antes de elegir el ítem.
  static const int _version = 5;
  Database? _db;

  Future<Database> get db async => _db ??= await _open();

  /// Cierra el handle. Sólo lo usan los tests, para no dejar bases abiertas entre
  /// casos; la app mantiene la suya viva mientras corre.
  @visibleForTesting
  Future<void> cerrar() async {
    await _db?.close();
    _db = null;
  }

  Future<Database> _open() async {
    final ruta = _rutaOverride ?? p.join(await getDatabasesPath(), 'sanmarino.db');
    return openDatabase(
      ruta,
      version: _version,
      onUpgrade: _migrar,
      onCreate: (d, v) async {
        // Cola de sincronización — la tabla crítica de la app.
        await d.execute('''
          CREATE TABLE pending_sync (
            id           TEXT PRIMARY KEY,
            tipo         TEXT NOT NULL,
            lote_id      INTEGER NOT NULL,
            lote_nombre  TEXT NOT NULL,
            fecha        TEXT NOT NULL,
            payload      TEXT NOT NULL,
            estado       TEXT NOT NULL DEFAULT 'pending',
            created_at   TEXT NOT NULL,
            intentos     INTEGER NOT NULL DEFAULT 0,
            ultimo_error TEXT,
            endpoint     TEXT,
            remote_id    INTEGER
          )
        ''');
        await d.execute('CREATE INDEX idx_pending_estado ON pending_sync(estado)');

        // Cache de lotes asignados — permite trabajar sin red desde el arranque.
        await d.execute('''
          CREATE TABLE lotes_cache (
            id                 INTEGER NOT NULL,
            nombre             TEXT NOT NULL,
            granja             TEXT,
            galpon             TEXT,
            modulo             TEXT NOT NULL,
            dia                INTEGER,
            aves               INTEGER,
            viabilidad         REAL,
            raza               TEXT,
            ano_tabla_genetica INTEGER,
            fecha_encaset      TEXT,
            company_id         INTEGER,
            cerrado            INTEGER NOT NULL DEFAULT 0,
            lote_ave_engorde_id INTEGER,
            lote_maestro_id    INTEGER,
            granja_id          INTEGER,
            nucleo_id          TEXT,
            galpon_id          TEXT,
            updated_at         TEXT NOT NULL,
            -- Engorde y reproductora numeran por separado: el id 12 existe en los
            -- dos módulos y son lotes distintos. La clave es (modulo, id).
            PRIMARY KEY (modulo, id)
          )
        ''');

        // Catálogo de ítems (alimentos, medicamentos) con existencias.
        await d.execute('''
          CREATE TABLE catalogo_cache (
            id         INTEGER PRIMARY KEY,
            nombre     TEXT NOT NULL,
            tipo       TEXT NOT NULL,
            disponible REAL,
            unidad     TEXT,
            updated_at TEXT NOT NULL
          )
        ''');

        // Historial local de lo ya enviado — para ver registros sin red.
        await d.execute('''
          CREATE TABLE seguimientos_local (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            remote_id  INTEGER,
            tipo       TEXT NOT NULL,
            lote_id    INTEGER NOT NULL,
            fecha      TEXT NOT NULL,
            payload    TEXT NOT NULL,
            created_at TEXT NOT NULL
          )
        ''');
        await d.execute('CREATE INDEX idx_seg_lote ON seguimientos_local(lote_id, fecha)');

        await _crearTablasV2(d);
        await _crearTablasV4(d);
      },
    );
  }


  // ── Migración de esquema ───────────────────────────────────────────────────

  /// v1 → v2. Se agregan columnas y tablas; **no se borra nada**: un equipo ya
  /// instalado puede tener registros pendientes de subir, y perderlos en una
  /// actualización sería perder trabajo de campo que nadie anotó en otro lado.
  static Future<void> _migrar(Database d, int desde, int hasta) async {
    if (desde < 2) {
      // `pending_sync` tiene trabajo del usuario: se le AGREGAN columnas, jamás
      // se recrea. SQLite no tiene ADD COLUMN IF NOT EXISTS, así que una columna
      // ya presente (instalación a medio migrar) se ignora en vez de tumbar el
      // arranque de la app en la granja.
      for (final sql in const [
        'ALTER TABLE pending_sync ADD COLUMN endpoint TEXT',
        'ALTER TABLE pending_sync ADD COLUMN remote_id INTEGER',
      ]) {
        try {
          await d.execute(sql);
        } catch (_) {}
      }

      // `lotes_cache` es caché regenerable, y v2 le cambia la clave primaria a
      // (modulo, id) — un ALTER no puede hacer eso. Se recrea: lo único que
      // cuesta es volver a descargarla en la próxima sincronización.
      await d.execute('DROP TABLE IF EXISTS lotes_cache');
      await d.execute(
        'CREATE TABLE lotes_cache ('
        '  id INTEGER NOT NULL,'
        '  nombre TEXT NOT NULL,'
        '  granja TEXT,'
        '  galpon TEXT,'
        '  modulo TEXT NOT NULL,'
        '  dia INTEGER,'
        '  aves INTEGER,'
        '  viabilidad REAL,'
        '  raza TEXT,'
        '  ano_tabla_genetica INTEGER,'
        '  fecha_encaset TEXT,'
        '  company_id INTEGER,'
        '  cerrado INTEGER NOT NULL DEFAULT 0,'
        '  lote_ave_engorde_id INTEGER,'
        '  lote_maestro_id INTEGER,'
        '  updated_at TEXT NOT NULL,'
        '  PRIMARY KEY (modulo, id)'
        ')',
      );

      await _crearTablasV2(d);
    }

    if (desde < 4) await _crearTablasV4(d);

    if (desde == 2) {
      // Sólo para quien ya estaba en v2: los que vienen de v1 recrearon la tabla
      // arriba y la columna ya viene incluida.
      try {
        await d.execute('ALTER TABLE lotes_cache ADD COLUMN lote_maestro_id INTEGER');
      } catch (_) {}
    }

    if (desde < 5) {
      // `lotes_cache` es caché regenerable (se borra y se rellena en cada
      // sincronización) — agregar columnas es seguro, la próxima sync las llena.
      for (final sql in const [
        'ALTER TABLE lotes_cache ADD COLUMN granja_id INTEGER',
        'ALTER TABLE lotes_cache ADD COLUMN nucleo_id TEXT',
        'ALTER TABLE lotes_cache ADD COLUMN galpon_id TEXT',
      ]) {
        try {
          await d.execute(sql);
        } catch (_) {}
      }
    }
  }

  /// Catálogo y existencias. Son **caché regenerable**: se borran y se vuelven a
  /// bajar en cada sincronización, así que nunca se migran, se recrean.
  static Future<void> _crearTablasV4(Database d) async {
    await d.execute(
      'CREATE TABLE IF NOT EXISTS items_inventario ('
      '  id INTEGER PRIMARY KEY,'
      '  codigo TEXT NOT NULL,'
      '  nombre TEXT NOT NULL,'
      '  tipo TEXT NOT NULL,'
      '  concepto TEXT,'
      '  unidad TEXT NOT NULL,'
      '  updated_at TEXT NOT NULL'
      ')',
    );

    // La PK es la clave natural del stock, la misma del índice del servidor:
    // un saldo por ítem mostraría el total de la granja y dejaría pasar un
    // consumo contra un galpón vacío.
    await d.execute(
      'CREATE TABLE IF NOT EXISTS existencias_inventario ('
      '  farm_id INTEGER NOT NULL,'
      '  item_id INTEGER NOT NULL,'
      "  nucleo_id TEXT NOT NULL DEFAULT '',"
      "  galpon_id TEXT NOT NULL DEFAULT '',"
      '  silo_id INTEGER NOT NULL DEFAULT 0,'
      '  item_nombre TEXT,'
      '  cantidad REAL NOT NULL DEFAULT 0,'
      '  reservado REAL NOT NULL DEFAULT 0,'
      '  unidad TEXT,'
      '  updated_at TEXT NOT NULL,'
      '  PRIMARY KEY (farm_id, item_id, nucleo_id, galpon_id, silo_id)'
      ')',
    );

    await d.execute(
      'CREATE TABLE IF NOT EXISTS silos_lote ('
      '  lote_maestro_id INTEGER NOT NULL,'
      '  silo_id INTEGER NOT NULL,'
      '  nombre TEXT,'
      '  PRIMARY KEY (lote_maestro_id, silo_id)'
      ')',
    );
  }

  static Future<void> _crearTablasV2(Database d) async {
    // Sesión del usuario. Una sola fila (`id = 1`). Es lo que permite abrir la
    // app sin red y seguir viendo los lotes cacheados.
    await d.execute(
      'CREATE TABLE IF NOT EXISTS sesion ('
      '  id INTEGER PRIMARY KEY CHECK (id = 1),'
      '  datos TEXT NOT NULL,'
      '  guardado_at TEXT NOT NULL'
      ')',
    );

    // Días que el servidor YA tiene registrados, por lote. Sirve para avisarle al
    // usuario antes de que llene el formulario, en vez de que se coma el 400 del
    // índice único tres horas después, cuando vuelva la señal.
    await d.execute(
      'CREATE TABLE IF NOT EXISTS registros_conocidos ('
      '  modulo TEXT NOT NULL,'
      '  lote_id INTEGER NOT NULL,'
      '  fecha TEXT NOT NULL,'
      '  origen TEXT NOT NULL,'
      '  PRIMARY KEY (modulo, lote_id, fecha)'
      ')',
    );
  }

  // ── Sesión ─────────────────────────────────────────────────────────────────

  Future<Map<String, dynamic>?> leerSesion() async {
    final d = await db;
    final rows = await d.query('sesion', where: 'id = 1', limit: 1);
    if (rows.isEmpty) return null;
    return jsonDecode(rows.first['datos'] as String) as Map<String, dynamic>;
  }

  Future<void> guardarSesion(Map<String, dynamic> datos) async {
    final d = await db;
    await d.insert(
      'sesion',
      {'id': 1, 'datos': jsonEncode(datos), 'guardado_at': DateTime.now().toIso8601String()},
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<void> borrarSesion() async {
    final d = await db;
    await d.delete('sesion');
  }

  // ── Días ya registrados ────────────────────────────────────────────────────

  /// `origen`: 'servidor' (lo trajo una sincronización) o 'local' (lo acaba de
  /// anotar el usuario). Se guardan los dos porque, sin red, el único que existe
  /// es el local y también tiene que impedir un segundo registro del mismo día.
  Future<void> marcarRegistrado({
    required String modulo,
    required int loteId,
    required DateTime fecha,
    String origen = 'local',
  }) async {
    final d = await db;
    await d.insert(
      'registros_conocidos',
      {'modulo': modulo, 'lote_id': loteId, 'fecha': _soloFecha(fecha), 'origen': origen},
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<void> reemplazarRegistrosDelServidor({
    required String modulo,
    required int loteId,
    required Set<DateTime> fechas,
  }) async {
    final d = await db;
    await d.transaction((tx) async {
      await tx.delete('registros_conocidos',
          where: 'modulo = ? AND lote_id = ? AND origen = ?',
          whereArgs: [modulo, loteId, 'servidor']);
      for (final f in fechas) {
        await tx.insert(
          'registros_conocidos',
          {'modulo': modulo, 'lote_id': loteId, 'fecha': _soloFecha(f), 'origen': 'servidor'},
          conflictAlgorithm: ConflictAlgorithm.replace,
        );
      }
    });
  }

  /// Quita la marca de un día. Se llama cuando la fila que la puso deja de tener
  /// futuro (el servidor la rechazó y se agotaron los reintentos).
  ///
  /// **Por qué hace falta:** `encolar` marca el día apenas el usuario guarda —
  /// sin red, esa marca es lo único que impide cargarlo dos veces. Pero si el
  /// servidor termina rechazándolo, la marca quedaba igual: el operario veía el
  /// día como cargado, el servidor no lo tenía, y nadie se enteraba. Sólo se
  /// borran las de origen `local`; las que confirmó el servidor no se tocan.
  Future<void> desmarcarRegistroLocal({
    required String modulo,
    required int loteId,
    required DateTime fecha,
  }) async {
    final d = await db;
    await d.delete('registros_conocidos',
        where: 'modulo = ? AND lote_id = ? AND fecha = ? AND origen = ?',
        whereArgs: [modulo, loteId, _soloFecha(fecha), 'local']);
  }

  Future<bool> yaHayRegistro({
    required String modulo,
    required int loteId,
    required DateTime fecha,
  }) async {
    final d = await db;
    final r = await d.query('registros_conocidos',
        where: 'modulo = ? AND lote_id = ? AND fecha = ?',
        whereArgs: [modulo, loteId, _soloFecha(fecha)],
        limit: 1);
    return r.isNotEmpty;
  }

  static String _soloFecha(DateTime f) =>
      '${f.year.toString().padLeft(4, '0')}-${f.month.toString().padLeft(2, '0')}-${f.day.toString().padLeft(2, '0')}';


  // ── Catálogo de inventario ─────────────────────────────────────────────────

  /// Reemplaza el catálogo entero. Es caché: lo que ya no viene del servidor
  /// deja de existir, en vez de quedar como opción fantasma en el selector.
  Future<bool> guardarCatalogo(List<ItemInventario> items) async {
    final d = await db;
    if (await _vaciaConCache(d, items, 'items_inventario')) return false;
    final now = DateTime.now().toIso8601String();
    await d.transaction((tx) async {
      await tx.delete('items_inventario');
      for (final i in items) {
        await tx.insert('items_inventario', {
          'id': i.id, 'codigo': i.codigo, 'nombre': i.nombre,
          'tipo': i.tipo, 'concepto': i.concepto, 'unidad': i.unidad,
          'updated_at': now,
        }, conflictAlgorithm: ConflictAlgorithm.replace);
      }
    });
    return true;
  }

  Future<List<ItemInventario>> catalogoCacheado({bool soloAlimento = false}) async {
    final d = await db;
    final rows = await d.query('items_inventario', orderBy: 'nombre');
    final items = rows.map((r) => ItemInventario(
          id: r['id'] as int,
          codigo: r['codigo'] as String,
          nombre: r['nombre'] as String,
          tipo: r['tipo'] as String,
          unidad: r['unidad'] as String? ?? 'kg',
          concepto: r['concepto'] as String?,
        ));
    // El filtro por tipo se hace ACÁ y no en el SQL ni en el servidor: el texto
    // del catálogo viene con mayúsculas distintas según la empresa, y el filtro
    // del backend compara exacto.
    return (soloAlimento ? items.where((i) => i.esAlimento) : items).toList();
  }

  /// Reemplaza las existencias. Idem catálogo: es una foto, no un histórico.
  Future<bool> guardarExistencias(List<ExistenciaInventario> filas) async {
    final d = await db;
    if (await _vaciaConCache(d, filas, 'existencias_inventario')) return false;
    final now = DateTime.now().toIso8601String();
    await d.transaction((tx) async {
      await tx.delete('existencias_inventario');
      for (final e in filas) {
        await tx.insert('existencias_inventario', {
          'farm_id': e.farmId,
          'item_id': e.itemId,
          'nucleo_id': e.nucleoId?.trim() ?? '',
          'galpon_id': e.galponId?.trim() ?? '',
          'silo_id': e.siloId ?? 0,
          'item_nombre': e.itemNombre,
          'cantidad': e.cantidad,
          'reservado': e.reservado,
          'unidad': e.unidad,
          'updated_at': now,
        }, conflictAlgorithm: ConflictAlgorithm.replace);
      }
    });
    return true;
  }

  /// Las existencias indexadas por su clave natural, listas para consultar al
  /// armar un consumo.
  Future<Map<String, ExistenciaInventario>> existenciasCacheadas() async {
    final d = await db;
    final rows = await d.query('existencias_inventario');
    return {
      for (final r in rows)
        ExistenciaInventario.claveDe(
          farmId: r['farm_id'] as int,
          itemId: r['item_id'] as int,
          nucleoId: r['nucleo_id'] as String?,
          galponId: r['galpon_id'] as String?,
          siloId: r['silo_id'] as int?,
        ): ExistenciaInventario(
          itemId: r['item_id'] as int,
          farmId: r['farm_id'] as int,
          nucleoId: (r['nucleo_id'] as String?)?.isEmpty ?? true
              ? null : r['nucleo_id'] as String,
          galponId: (r['galpon_id'] as String?)?.isEmpty ?? true
              ? null : r['galpon_id'] as String,
          siloId: (r['silo_id'] as int?) == 0 ? null : r['silo_id'] as int?,
          itemNombre: r['item_nombre'] as String? ?? '',
          cantidad: (r['cantidad'] as num?)?.toDouble() ?? 0,
          reservado: (r['reservado'] as num?)?.toDouble() ?? 0,
          unidad: r['unidad'] as String? ?? 'kg',
        ),
    };
  }

  /// Cuándo se bajó la foto del stock. La pantalla la muestra junto al saldo:
  /// el operario tiene que saber que está mirando algo que puede tener horas.
  Future<DateTime?> existenciasActualizadasEn() async {
    final d = await db;
    final r = await d.rawQuery('SELECT MAX(updated_at) u FROM existencias_inventario');
    final iso = r.first['u'] as String?;
    return iso == null ? null : DateTime.tryParse(iso);
  }

  Future<void> guardarSilosDeLote(int loteMaestroId, List<SiloDelLote> silos) async {
    final d = await db;
    await d.transaction((tx) async {
      await tx.delete('silos_lote',
          where: 'lote_maestro_id = ?', whereArgs: [loteMaestroId]);
      for (final s in silos) {
        await tx.insert('silos_lote', {
          'lote_maestro_id': loteMaestroId,
          'silo_id': s.siloId,
          'nombre': s.nombre,
        }, conflictAlgorithm: ConflictAlgorithm.replace);
      }
    });
  }

  Future<List<SiloDelLote>> silosDeLote(int loteMaestroId) async {
    final d = await db;
    final rows = await d.query('silos_lote',
        where: 'lote_maestro_id = ?', whereArgs: [loteMaestroId], orderBy: 'nombre');
    return rows
        .map((r) => SiloDelLote(
            siloId: r['silo_id'] as int, nombre: r['nombre'] as String? ?? ''))
        .toList();
  }

  // ── Cola de sincronización ─────────────────────────────────────────────────

  /// Encola un registro. Se llama justo después de que el usuario toca Guardar.
  Future<String> encolar({
    required String tipo,
    required int loteId,
    required String loteNombre,
    required DateTime fecha,
    required Map<String, dynamic> payload,
    String? endpoint,
  }) async {
    final d = await db;
    final id = '${tipo}_${loteId}_${DateTime.now().millisecondsSinceEpoch}';
    await d.insert('pending_sync', {
      'id': id,
      'tipo': tipo,
      'lote_id': loteId,
      'lote_nombre': loteNombre,
      'fecha': fecha.toIso8601String(),
      'payload': jsonEncode(payload),
      'estado': 'pending',
      'created_at': DateTime.now().toIso8601String(),
      // Se guarda la ruta con la fila: si el mapeo módulo→endpoint cambia, lo ya
      // encolado sigue yendo a donde iba cuando el usuario lo registró.
      'endpoint': endpoint,
    });
    return id;
  }

  Future<List<RegistroPendiente>> pendientes() async {
    final d = await db;
    final rows = await d.query('pending_sync', orderBy: 'created_at DESC');
    return rows.map(_mapPendiente).toList();
  }

  /// Cuántas veces se reintenta una fila que el servidor rechazó por su
  /// contenido antes de dejar de insistir y pedirle al usuario que la mire.
  ///
  /// Sin techo, `estado='error'` volvía a la cola en cada pasada **para siempre**:
  /// un registro que el backend nunca va a aceptar (el lote se cerró, el alimento
  /// ya no existe) se reenviaba cada vez que había señal, gastando batería y
  /// datos, y tapando en la pantalla de sincronización a los que sí van a entrar.
  static const int maxIntentos = 5;

  /// Las que faltan enviar, **en orden cronológico**: el backend valida contra el
  /// saldo del lote, así que mandar el martes antes que el lunes puede dar un
  /// rechazo que en orden no ocurriría.
  ///
  /// Las agotadas (`intentos >= maxIntentos`) quedan fuera: siguen en la tabla y
  /// visibles para el usuario, pero la cola deja de reintentarlas sola.
  Future<List<RegistroPendiente>> porEnviar() async {
    final d = await db;
    final rows = await d.query(
      'pending_sync',
      where: "estado IN ('pending','syncing') "
          "OR (estado = 'error' AND intentos < ?)",
      whereArgs: [maxIntentos],
      orderBy: 'created_at ASC',
    );
    return rows.map(_mapPendiente).toList();
  }

  /// Las que la cola ya no reintenta sola y necesitan que alguien las mire.
  Future<List<RegistroPendiente>> agotadas() async {
    final d = await db;
    final rows = await d.query(
      'pending_sync',
      where: "estado = 'error' AND intentos >= ?",
      whereArgs: [maxIntentos],
      orderBy: 'created_at ASC',
    );
    return rows.map(_mapPendiente).toList();
  }

  /// Devuelve una fila agotada a la cola. La llama el usuario desde la pantalla
  /// de sincronización, después de corregir lo que estuviera mal.
  Future<void> reintentar(String id) async {
    final d = await db;
    await d.update('pending_sync', {'estado': 'pending', 'intentos': 0},
        where: 'id = ?', whereArgs: [id]);
  }

  Future<int> contarPendientes() async {
    final d = await db;
    final r = await d.rawQuery(
      "SELECT COUNT(*) c FROM pending_sync WHERE estado IN ('pending','syncing','error')",
    );
    return (r.first['c'] as int?) ?? 0;
  }

  /// [sumarIntento] incrementa el contador real (antes lo fijaba en 1, así que un
  /// registro que fallaba veinte veces seguía figurando con un intento).
  Future<void> marcarEstado(
    String id,
    EstadoSync estado, {
    String? error,
    bool sumarIntento = false,
  }) async {
    final d = await db;
    await d.update(
      'pending_sync',
      {
        'estado': estado.name,
        'ultimo_error': error,
      },
      where: 'id = ?',
      whereArgs: [id],
    );
    if (sumarIntento) {
      await d.rawUpdate('UPDATE pending_sync SET intentos = intentos + 1 WHERE id = ?', [id]);
    }
  }

  /// Se llama cuando el servidor confirma. Mueve el registro al historial.
  Future<void> confirmarEnviado(String id, {int? remoteId}) async {
    final d = await db;
    final rows = await d.query('pending_sync', where: 'id = ?', whereArgs: [id], limit: 1);
    if (rows.isEmpty) return;
    final r = rows.first;
    await d.transaction((tx) async {
      await tx.insert('seguimientos_local', {
        'remote_id': remoteId,
        'tipo': r['tipo'],
        'lote_id': r['lote_id'],
        'fecha': r['fecha'],
        'payload': r['payload'],
        'created_at': DateTime.now().toIso8601String(),
      });
      await tx.delete('pending_sync', where: 'id = ?', whereArgs: [id]);
    });
  }

  RegistroPendiente _mapPendiente(Map<String, Object?> r) => RegistroPendiente(
    id: r['id'] as String,
    tipo: r['tipo'] as String,
    loteId: r['lote_id'] as int,
    loteNombre: r['lote_nombre'] as String,
    fecha: DateTime.parse(r['fecha'] as String),
    payload: jsonDecode(r['payload'] as String) as Map<String, dynamic>,
    estado: EstadoSync.values.firstWhere((e) => e.name == r['estado'], orElse: () => EstadoSync.pending),
    createdAt: DateTime.parse(r['created_at'] as String),
    intentos: (r['intentos'] as int?) ?? 0,
    ultimoError: r['ultimo_error'] as String?,
    endpoint: r['endpoint'] as String?,
    remoteId: r['remote_id'] as int?,
  );

  // ── Historial local ────────────────────────────────────────────────────────

  /// Lo que ya se envió y el servidor aceptó, **más reciente primero**.
  ///
  /// `seguimientos_local` la escribe [confirmarEnviado] y hasta ahora no la leía
  /// nadie: apenas un registro se sincronizaba desaparecía de la vista (la
  /// pantalla de cola sólo muestra `pending_sync`), así que sin señal la única
  /// pista de que el día ya estaba cargado era el rechazo al intentar cargarlo
  /// de nuevo. Este es el lector que faltaba.
  ///
  /// [loteId] filtra por lote: es exactamente el prefijo del índice
  /// `idx_seg_lote (lote_id, fecha)`. [limite] existe porque esta tabla sólo
  /// crece — la pantalla muestra lo reciente, no el historial entero del equipo.
  ///
  /// Es una lectura pura: no borra, no marca y no toca la cola.
  Future<List<SeguimientoLocal>> historialLocal({int? loteId, int limite = 200}) async {
    final d = await db;
    final rows = await d.query(
      'seguimientos_local',
      where: loteId == null ? null : 'lote_id = ?',
      whereArgs: loteId == null ? null : [loteId],
      // `fecha` es texto ISO, que ordena bien lexicográficamente. El desempate
      // por `id` (AUTOINCREMENT) da orden estable entre dos registros del mismo
      // día: sin él, dos filas empatadas podían intercambiarse entre lecturas.
      orderBy: 'fecha DESC, id DESC',
      limit: limite,
    );
    return rows.map(SeguimientoLocal.desdeFila).toList();
  }

  // ── Guarda de caché ────────────────────────────────────────────────────────

  /// `true` si hay que SALTEAR el reemplazo: la respuesta vino vacía y en disco
  /// hay datos que perderíamos.
  ///
  /// El reemplazo total es intencional (invariante I16): un ítem que el servidor
  /// ya no manda no debe quedar de fantasma en el selector. Pero eso vale para
  /// una respuesta *con* datos. Una lista vacía casi nunca significa «este
  /// usuario se quedó sin lotes»: significa un glitch de rol/menú o un 200 con
  /// cuerpo raro. Y dejar al operario con CERO lotes en el galpón es más
  /// destructivo que un error de red, que sí se maneja bien (se sigue con caché).
  ///
  /// Por eso: lista vacía + caché con filas ⇒ no se toca nada.
  Future<bool> _vaciaConCache(Database d, List<Object?> entrantes, String tabla) async {
    if (entrantes.isNotEmpty) return false;
    final n = Sqflite.firstIntValue(await d.rawQuery('SELECT COUNT(*) FROM $tabla')) ?? 0;
    return n > 0;
  }

  // ── Cache de lotes ─────────────────────────────────────────────────────────

  /// Devuelve `false` si se salteó el reemplazo por la guarda de lista vacía.
  Future<bool> guardarLotes(List<Lote> lotes) async {
    final d = await db;
    if (await _vaciaConCache(d, lotes, 'lotes_cache')) return false;
    final now = DateTime.now().toIso8601String();
    await d.transaction((tx) async {
      await tx.delete('lotes_cache');
      for (final l in lotes) {
        await tx.insert('lotes_cache', {
          'id': l.id, 'nombre': l.nombre, 'granja': l.granja, 'galpon': l.galpon,
          'modulo': l.modulo.id, 'dia': l.dia, 'aves': l.aves,
          'viabilidad': l.viabilidad, 'raza': l.raza,
          'ano_tabla_genetica': l.anoTablaGenetica,
          'fecha_encaset': l.fechaEncaset?.toIso8601String(),
          'company_id': l.companyId,
          'cerrado': l.cerrado ? 1 : 0,
          'lote_ave_engorde_id': l.loteAveEngordeId,
          'lote_maestro_id': l.loteMaestroId,
          'granja_id': l.granjaId,
          'nucleo_id': l.nucleoId,
          'galpon_id': l.galponId,
          'updated_at': now,
        }, conflictAlgorithm: ConflictAlgorithm.replace);
      }
    });
    return true;
  }

  Future<List<Lote>> lotesCacheados() async {
    final d = await db;
    final rows = await d.query('lotes_cache', orderBy: 'nombre');
    return rows.map((r) => Lote(
      id: r['id'] as int,
      nombre: r['nombre'] as String,
      granja: r['granja'] as String? ?? '',
      galpon: r['galpon'] as String? ?? '',
      modulo: ModuloSeguimiento.fromId(r['modulo'] as String) ?? ModuloSeguimiento.levante,
      dia: (r['dia'] as int?) ?? 0,
      aves: (r['aves'] as int?) ?? 0,
      viabilidad: (r['viabilidad'] as num?)?.toDouble(),
      raza: r['raza'] as String?,
      anoTablaGenetica: r['ano_tabla_genetica'] as int?,
      fechaEncaset: r['fecha_encaset'] == null
          ? null
          : DateTime.tryParse(r['fecha_encaset'] as String),
      companyId: r['company_id'] as int?,
      cerrado: ((r['cerrado'] as int?) ?? 0) == 1,
      loteAveEngordeId: r['lote_ave_engorde_id'] as int?,
      loteMaestroId: r['lote_maestro_id'] as int?,
      granjaId: r['granja_id'] as int?,
      nucleoId: r['nucleo_id'] as String?,
      galponId: r['galpon_id'] as String?,
    )).toList();
  }
}

/// Una fila de `seguimientos_local`: un registro que el servidor **ya aceptó**.
///
/// Vive acá y no en `core/models/` a propósito: no es vocabulario de dominio ni
/// viaja al backend, es la forma de una tabla local y su único lector es la
/// pantalla de historial. Si algún día lo consume algo más, ahí sí sube.
///
/// Todo el parseo es tolerante. El historial es de **sólo lectura**: una fila
/// vieja o rara tiene que verse como se pueda, nunca tumbar la pantalla y
/// esconder las otras 199 que sí están bien.
class SeguimientoLocal {
  const SeguimientoLocal({
    required this.id,
    required this.tipo,
    required this.loteId,
    required this.fecha,
    required this.fechaTexto,
    required this.payload,
    this.createdAt,
    this.remoteId,
  });

  /// Autoincremental de la tabla local. **No** es el id del servidor.
  final int id;

  /// El mismo `tipo` con el que se encoló: 'levante' | 'engorde' | 'produccion'
  /// | 'reproductora' (y, si algún día se encolan, los movimientos).
  final String tipo;

  final int loteId;

  /// Día del registro. `null` si la columna trae un texto que no se puede
  /// parsear; en ese caso queda [fechaTexto], que es lo que se muestra.
  ///
  /// Ojo al leerla: la escribe `confirmarEnviado` copiando lo que guardó
  /// `encolar` (`DateTime.toIso8601String()` en hora **local**, sin zona), no el
  /// `_soloFecha` de `registros_conocidos`. Son dos formatos distintos.
  final DateTime? fecha;
  final String fechaTexto;

  /// El JSON tal como se envió. Se conserva entero, pero los campos cambian por
  /// módulo, país y empresa: la pantalla muestra lo identificatorio, no esto.
  final Map<String, dynamic> payload;

  /// Cuándo se confirmó el envío — no cuándo se capturó el día.
  final DateTime? createdAt;

  /// Id que devolvió el backend al aceptarlo. `null` cuando la confirmación no
  /// trajo número: pasa con el duplicado (`TipoFallo.duplicado`, el servidor ya
  /// lo tenía) y con las respuestas de cuerpo vacío. En los dos casos el
  /// registro está a salvo del lado del servidor.
  final int? remoteId;

  bool get confirmadoConId => remoteId != null;

  static SeguimientoLocal desdeFila(Map<String, Object?> r) {
    final fecha = r['fecha'] as String? ?? '';
    return SeguimientoLocal(
      id: (r['id'] as int?) ?? 0,
      tipo: r['tipo'] as String? ?? '',
      loteId: (r['lote_id'] as int?) ?? 0,
      fecha: DateTime.tryParse(fecha),
      fechaTexto: fecha,
      payload: _payloadOVacio(r['payload'] as String?),
      createdAt: DateTime.tryParse(r['created_at'] as String? ?? ''),
      remoteId: r['remote_id'] as int?,
    );
  }

  /// Un payload ilegible no puede hacer fallar la lectura: lo que identifica a
  /// la fila (módulo, lote, fecha) vive en las columnas, no en el JSON.
  static Map<String, dynamic> _payloadOVacio(String? crudo) {
    if (crudo == null || crudo.isEmpty) return const {};
    try {
      final v = jsonDecode(crudo);
      return v is Map<String, dynamic> ? v : const {};
    } catch (_) {
      return const {};
    }
  }
}
