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

  static const int _version = 1;
  Database? _db;

  Future<Database> get db async => _db ??= await _open();

  Future<Database> _open() async {
    final dir = await getDatabasesPath();
    return openDatabase(
      p.join(dir, 'sanmarino.db'),
      version: _version,
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
            ultimo_error TEXT
          )
        ''');
        await d.execute('CREATE INDEX idx_pending_estado ON pending_sync(estado)');

        // Cache de lotes asignados — permite trabajar sin red desde el arranque.
        await d.execute('''
          CREATE TABLE lotes_cache (
            id                 INTEGER PRIMARY KEY,
            nombre             TEXT NOT NULL,
            granja             TEXT,
            galpon             TEXT,
            modulo             TEXT NOT NULL,
            dia                INTEGER,
            aves               INTEGER,
            viabilidad         REAL,
            raza               TEXT,
            ano_tabla_genetica INTEGER,
            updated_at         TEXT NOT NULL
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
      },
    );
  }

  // ── Cola de sincronización ─────────────────────────────────────────────────

  /// Encola un registro. Se llama justo después de que el usuario toca Guardar.
  Future<String> encolar({
    required String tipo,
    required int loteId,
    required String loteNombre,
    required DateTime fecha,
    required Map<String, dynamic> payload,
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
    });
    return id;
  }

  Future<List<RegistroPendiente>> pendientes() async {
    final d = await db;
    final rows = await d.query('pending_sync', orderBy: 'created_at DESC');
    return rows.map(_mapPendiente).toList();
  }

  Future<int> contarPendientes() async {
    final d = await db;
    final r = await d.rawQuery(
      "SELECT COUNT(*) c FROM pending_sync WHERE estado IN ('pending','syncing','error')",
    );
    return (r.first['c'] as int?) ?? 0;
  }

  Future<void> marcarEstado(String id, EstadoSync estado, {String? error}) async {
    final d = await db;
    await d.update(
      'pending_sync',
      {
        'estado': estado.name,
        if (error != null) 'ultimo_error': error,
        if (estado == EstadoSync.error) 'intentos': 1,
      },
      where: 'id = ?',
      whereArgs: [id],
    );
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
          'ano_tabla_genetica': l.anoTablaGenetica, 'updated_at': now,
        });
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
    )).toList();
  }
}
