/// Base de datos local SQLite — la app es offline-first.
/// Todo registro se guarda aquí primero; la sincronización viene después.
library;

import 'dart:convert';
import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart' as p;
import 'models.dart';

class LocalDb {
  LocalDb._();
  static final LocalDb instance = LocalDb._();

  /// v2 (21ago26): sesión persistida, registros ya conocidos por el servidor y
  /// las columnas que la cola necesita para saber a dónde postear cada fila.
  /// v3 (22ago26): `lote_maestro_id`, que los módulos de postura necesitan para
  /// postear (el id de la etapa no alcanza).
  static const int _version = 3;
  Database? _db;

  Future<Database> get db async => _db ??= await _open();

  Future<Database> _open() async {
    final dir = await getDatabasesPath();
    return openDatabase(
      p.join(dir, 'sanmarino.db'),
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

    if (desde == 2) {
      // Sólo para quien ya estaba en v2: los que vienen de v1 recrearon la tabla
      // arriba y la columna ya viene incluida.
      try {
        await d.execute('ALTER TABLE lotes_cache ADD COLUMN lote_maestro_id INTEGER');
      } catch (_) {}
    }
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

  /// Las que faltan enviar, **en orden cronológico**: el backend valida contra el
  /// saldo del lote, así que mandar el martes antes que el lunes puede dar un
  /// rechazo que en orden no ocurriría.
  Future<List<RegistroPendiente>> porEnviar() async {
    final d = await db;
    final rows = await d.query(
      'pending_sync',
      where: "estado IN ('pending','syncing','error')",
      orderBy: 'created_at ASC',
    );
    return rows.map(_mapPendiente).toList();
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

  // ── Cache de lotes ─────────────────────────────────────────────────────────

  Future<void> guardarLotes(List<Lote> lotes) async {
    final d = await db;
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
          'updated_at': now,
        }, conflictAlgorithm: ConflictAlgorithm.replace);
      }
    });
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
    )).toList();
  }
}
