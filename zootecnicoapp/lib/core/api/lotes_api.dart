/// Descarga de los lotes sobre los que el usuario puede registrar.
///
/// El backend ya filtra por empresa activa y por las granjas asignadas al
/// usuario: la app no vuelve a filtrar por su cuenta, sólo traduce.
///
/// **Los dos módulos viven en tablas distintas.** El `loteId` de un seguimiento
/// de engorde es `lote_ave_engorde.lote_ave_engorde_id`; el de reproductora es
/// `lote_reproductora_ave_engorde.id`. Se parecen, no son el mismo número, y
/// cruzarlos escribiría el registro en el lote equivocado sin ningún error visible.
library;

import 'dart:convert';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/api/api_client.dart';

class LotesApi {
  LotesApi(this._api);

  final ApiClient _api;

  /// Todos los lotes que el usuario puede ver, de los módulos que tenga habilitados.
  ///
  /// Los lotes de engorde se descargan **siempre**, aunque el módulo no esté
  /// habilitado: son la única fuente de la granja y el galpón de un lote
  /// reproductora, cuyo DTO no los trae. Sin ese cruce, la lista de reproductora
  /// saldría sin ubicación y el usuario no sabría cuál es cuál.
  Future<List<Lote>> descargar({required List<ModuloSeguimiento> modulos}) async {
    final quiereEngorde = modulos.contains(ModuloSeguimiento.engorde);
    final quiereReproductora = modulos.contains(ModuloSeguimiento.reproductora);
    final quiereLevante = modulos.contains(ModuloSeguimiento.levante);
    final quiereProduccion = modulos.contains(ModuloSeguimiento.produccion);
    if (!quiereEngorde && !quiereReproductora && !quiereLevante && !quiereProduccion) {
      return const [];
    }

    // Engorde sólo se baja si hace falta: para sus propios lotes, o como fuente
    // de la ubicación de los de reproductora.
    final deEngorde = (quiereEngorde || quiereReproductora) ? await engorde() : const <Lote>[];

    final lotes = <Lote>[
      if (quiereEngorde) ...deEngorde,
      if (quiereReproductora) ...await reproductora(padres: deEngorde),
      if (quiereLevante) ...await levante(),
      if (quiereProduccion) ...await produccion(),
    ];

    lotes.sort((a, b) => a.nombre.toLowerCase().compareTo(b.nombre.toLowerCase()));
    return lotes;
  }

  /// `GET /api/LoteAveEngorde` → `LoteAveEngordeDetailDto`.
  Future<List<Lote>> engorde() async {
    final filas = await _lista('/LoteAveEngorde');
    return filas.map(loteDeEngorde).toList();
  }

  /// `GET /api/LoteReproductoraAveEngorde` → `LoteReproductoraAveEngordeDto`.
  Future<List<Lote>> reproductora({List<Lote> padres = const []}) async {
    final filas = await _lista('/LoteReproductoraAveEngorde');
    final porId = {for (final p in padres) p.id: p};
    return filas.map((f) => loteDeReproductora(f, padres: porId)).toList();
  }


  /// `GET /api/LotePosturaLevante` → `LotePosturaLevanteDetailDto`.
  Future<List<Lote>> levante() async {
    final filas = await _lista('/LotePosturaLevante');
    return filas.map(loteDePostura).toList();
  }

  /// `GET /api/LotePosturaProduccion` → `LotePosturaProduccionDetailDto`.
  Future<List<Lote>> produccion() async {
    final filas = await _lista('/LotePosturaProduccion');
    return filas
        .map((f) => loteDePostura(f, modulo: ModuloSeguimiento.produccion))
        .toList();
  }

  Future<List<Map<String, dynamic>>> _lista(String path) async {
    final cuerpo = await _api.getRaw(path);
    if (cuerpo.trim().isEmpty) return const [];
    final json = jsonDecode(cuerpo);
    if (json is! List) return const [];
    return json.whereType<Map>().map((e) => e.cast<String, dynamic>()).toList();
  }

  // ── Traducción de los DTOs ──────────────────────────────────────────────────

  /// Las aves vivas son `hembrasL + machosL + mixtas`. **En engorde esos campos
  /// son el saldo** que el seguimiento diario y las ventas van descontando, no el
  /// encasetamiento — en postura significan lo contrario, por eso este mapeo no
  /// se comparte entre módulos. La base histórica es `avesEncasetadas`.
  static Lote loteDeEngorde(Map<String, dynamic> j) {
    final fecha = _fecha(j['fechaEncaset']);
    final vivas = _int(j['hembrasL']) + _int(j['machosL']) + _int(j['mixtas']);
    final encasetadas = _int(j['avesEncasetadas']);

    return Lote(
      id: _int(j['loteAveEngordeId']),
      nombre: _texto(j['loteNombre']) ?? 's/n',
      granja: _texto(j['farm'] is Map ? (j['farm'] as Map)['name'] : null) ?? '',
      galpon: _texto(j['galpon'] is Map ? (j['galpon'] as Map)['galponNombre'] : null) ??
          _texto(j['galponId']) ??
          '',
      modulo: ModuloSeguimiento.engorde,
      dia: _diasDesde(fecha),
      aves: vivas,
      // Sin la base del encasetamiento no hay viabilidad: un 100 % inventado
      // sería peor que no mostrar nada.
      viabilidad: encasetadas > 0 ? (vivas / encasetadas) * 100 : null,
      raza: _texto(j['raza']),
      anoTablaGenetica: j['anoTablaGenetica'] as int?,
      fechaEncaset: fecha,
      companyId: j['companyId'] as int?,
      cerrado: (_texto(j['estadoOperativoLote']) ?? '').toLowerCase() == 'cerrado',
      granjaId: j['granjaId'] as int?,
      nucleoId: _texto(j['nucleoId']),
      galponId: _texto(j['galponId']),
    );
  }

  /// El DTO de reproductora **no trae granja ni galpón**: cuelga del lote de
  /// engorde padre, de donde se copian. Sí trae ya calculados `avesActuales`,
  /// `saldoApertura` y `edadDias`, así que esos no se recalculan acá.
  static Lote loteDeReproductora(
    Map<String, dynamic> j, {
    Map<int, Lote> padres = const {},
  }) {
    final padreId = j['loteAveEngordeId'] as int?;
    final padre = padreId == null ? null : padres[padreId];
    final fecha = _fecha(j['fechaEncasetamiento']);
    final vivas = _int(j['avesActuales']);
    final apertura = _int(j['saldoApertura']);

    return Lote(
      id: _int(j['id']),
      nombre: _texto(j['nombreLote']) ??
          _texto(j['codigoReproductora']) ??
          _texto(j['reproductoraId']) ??
          's/n',
      granja: padre?.granja ?? '',
      galpon: padre?.galpon ?? '',
      modulo: ModuloSeguimiento.reproductora,
      dia: _int(j['edadDias']) > 0 ? _int(j['edadDias']) : _diasDesde(fecha),
      aves: vivas,
      viabilidad: apertura > 0 ? (vivas / apertura) * 100 : null,
      raza: padre?.raza,
      anoTablaGenetica: padre?.anoTablaGenetica,
      fechaEncaset: fecha,
      companyId: padre?.companyId,
      // "Cerrado" = ya se vendieron todas las aves iniciales; "Vigente" = abierto.
      cerrado: (_texto(j['estado']) ?? '').toLowerCase() == 'cerrado',
      loteAveEngordeId: padreId,
      granjaId: padre?.granjaId,
      nucleoId: padre?.nucleoId,
      galponId: padre?.galponId,
    );
  }


  /// Los dos DTOs de postura comparten forma, así que comparten mapeo. **Lo que
  /// NO se puede compartir con engorde es el significado de `hembrasL`:** en
  /// postura es la BASE del encasetamiento, no el saldo — el saldo vivo son
  /// `avesHActual` / `avesMActual`. Usar `hembrasL` acá mostraría el lote lleno
  /// aunque hubiera muerto la mitad.
  static Lote loteDePostura(
    Map<String, dynamic> j, {
    ModuloSeguimiento modulo = ModuloSeguimiento.levante,
  }) {
    final fecha = _fecha(j['fechaEncaset']);
    final vivas = _int(j['avesHActual']) + _int(j['avesMActual']);
    final inicial = _int(j['avesHInicial']) + _int(j['avesMInicial']);
    final esProduccion = modulo == ModuloSeguimiento.produccion;

    return Lote(
      id: _int(j[esProduccion ? 'lotePosturaProduccionId' : 'lotePosturaLevanteId']),
      nombre: _texto(j['loteNombre']) ?? 's/n',
      granja: _texto(j['farm'] is Map ? (j['farm'] as Map)['name'] : null) ??
          _texto(j['granjaNombre']) ??
          '',
      galpon: _texto(j['galpon'] is Map ? (j['galpon'] as Map)['galponNombre'] : null) ??
          _texto(j['galponId']) ??
          '',
      modulo: modulo,
      // `edad` viene en SEMANAS en estos DTOs; la app muestra días.
      dia: _diasDesde(fecha),
      aves: vivas,
      viabilidad: inicial > 0 ? (vivas / inicial) * 100 : null,
      raza: _texto(j['raza']),
      anoTablaGenetica: j['anoTablaGenetica'] as int?,
      fechaEncaset: fecha,
      companyId: j['companyId'] as int?,
      // `estadoCierre` es "Abierto | Cerrado": un levante cerrado ya pasó a
      // producción y no admite registros nuevos en esta etapa.
      cerrado: (_texto(j['estadoCierre']) ?? '').toLowerCase() == 'cerrado',
      loteMaestroId: j['loteId'] as int?,
      granjaId: j['granjaId'] as int?,
      nucleoId: _texto(j['nucleoId']),
      galponId: _texto(j['galponId']),
    );
  }

  static String? _texto(Object? v) {
    if (v is! String) return null;
    final t = v.trim();
    return t.isEmpty ? null : t;
  }

  static int _int(Object? v) => v is num ? v.toInt() : 0;

  static DateTime? _fecha(Object? v) =>
      v is String && v.isNotEmpty ? DateTime.tryParse(v) : null;

  static int _diasDesde(DateTime? f) {
    if (f == null) return 0;
    final hoy = DateTime.now();
    final d = DateTime(hoy.year, hoy.month, hoy.day)
        .difference(DateTime(f.year, f.month, f.day))
        .inDays;
    return d < 0 ? 0 : d;
  }
}
