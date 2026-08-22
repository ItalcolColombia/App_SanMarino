/// Envío de los seguimientos diarios y consulta de qué días ya están registrados.
///
/// ⚠️ **El controller de engorde se llama "Ecuador" pero atiende a los tres
/// países.** No hay un camino por país: el front web postea a
/// `SeguimientoAvesEngordeEcuador` para Ecuador, Panamá y Colombia, y los dos
/// services del backend escriben la misma tabla `seguimiento_diario_aves_engorde`
/// (la tabla `_ecuador` que sugiere el nombre no existe en la BD). La app hace lo
/// mismo que el web: el nombre miente por historia, no por diseño.
library;

import 'dart:convert';

import '../models.dart';
import 'api_client.dart';

/// A dónde se postea cada módulo. La ruta se guarda **con la fila encolada**, no
/// se deduce al enviar: si mañana cambia este mapeo, lo que el usuario ya
/// registró tiene que seguir yendo a donde iba cuando lo registró.
const Map<ModuloSeguimiento, String> endpointDeModulo = {
  ModuloSeguimiento.engorde: '/SeguimientoAvesEngordeEcuador',
  ModuloSeguimiento.reproductora: '/SeguimientoDiarioLoteReproductora',
};

class SeguimientosApi {
  SeguimientosApi(this._api);

  final ApiClient _api;

  /// Postea un registro ya armado. Devuelve el id que asignó el backend.
  ///
  /// No captura errores: el llamador ([SyncService]) necesita distinguir un
  /// duplicado (día ya registrado, se resuelve solo) de un fallo de red
  /// (se reintenta) de un token vencido (se para la cola).
  Future<int?> enviar({required String endpoint, required Map<String, dynamic> payload}) async {
    final cuerpo = await _api.postRaw(endpoint, payload);
    if (cuerpo.trim().isEmpty) return null;
    final json = jsonDecode(cuerpo);
    return json is Map ? json['id'] as int? : null;
  }

  /// Fechas que el servidor ya tiene registradas para un lote. Se guardan en la
  /// caché local para poder avisar *antes* de que el usuario llene el formulario
  /// completo y se coma un 400 al sincronizar tres horas después.
  Future<Set<DateTime>> fechasRegistradas(Lote lote) async {
    final path = switch (lote.modulo) {
      ModuloSeguimiento.engorde => '/SeguimientoAvesEngordeEcuador/por-lote/${lote.id}',
      ModuloSeguimiento.reproductora =>
        '/SeguimientoDiarioLoteReproductora/por-lote-reproductora/${lote.id}',
      _ => null,
    };
    if (path == null) return const {};

    final cuerpo = await _api.getRaw(path);
    if (cuerpo.trim().isEmpty) return const {};

    final json = jsonDecode(cuerpo);
    // Engorde responde un objeto con `registros`; reproductora, una lista pelada.
    final filas = switch (json) {
      List l => l,
      Map m => (m['registros'] ?? m['seguimientos'] ?? const []) as List,
      _ => const [],
    };

    return filas
        .whereType<Map>()
        .map((r) => r['fechaRegistro'])
        .whereType<String>()
        .map(DateTime.tryParse)
        .whereType<DateTime>()
        .map((f) => DateTime(f.year, f.month, f.day))
        .toSet();
  }
}

/// Construye el cuerpo del POST a partir de lo que llenó el usuario.
///
/// Vive aparte del cliente HTTP a propósito: es **lógica pura** y se testea sin
/// red, igual que los `Calculos/` del backend. Las diferencias por país no se
/// deciden acá con un `if` de empresa — llegan resueltas en [controlAgua] y
/// [quintales], que salen de `PerfilPais`.
class PayloadSeguimiento {
  const PayloadSeguimiento._();

  /// `CreateSeguimientoLoteLevanteRequest` — el request que comparten levante y
  /// engorde. Los campos de huevos de levante no se envían nunca desde el móvil.
  static Map<String, dynamic> engorde({
    required int loteId,
    required DateTime fecha,
    required Map<String, String> campos,
    required bool controlAgua,
    required bool quintales,
    String? usuarioId,
  }) {
    final p = <String, dynamic>{
      'loteId': loteId,
      'fechaRegistro': _fechaIso(fecha),
      'ciclo': 'Normal',
      'mortalidadHembras': _entero(campos['mortalidadHembras']),
      'mortalidadMachos': _entero(campos['mortalidadMachos']),
      'selH': _entero(campos['selH']),
      'selM': _entero(campos['selM']),
      'errorSexajeHembras': _entero(campos['errorSexajeHembras']),
      'errorSexajeMachos': _entero(campos['errorSexajeMachos']),
      'tipoAlimento': campos['tipoAlimento']?.trim() ?? '',
      // El backend acepta el consumo directo en kg con estas claves; sin ítems de
      // inventario no descuenta stock, que es justo lo que esta fase quiere.
      'consumoKgHembras': _decimal(campos['consumoKgHembras']),
      'consumoKgMachos': _decimal(campos['consumoKgMachos']),
      'pesoPromH': _decimal(campos['pesoPromH']),
      'pesoPromM': _decimal(campos['pesoPromM']),
      'uniformidadH': _decimal(campos['uniformidadH']),
      'uniformidadM': _decimal(campos['uniformidadM']),
      'cvH': _decimal(campos['cvH']),
      'cvM': _decimal(campos['cvM']),
      'observaciones': _textoONulo(campos['observaciones']),
      if (usuarioId != null && usuarioId.isNotEmpty) 'createdByUserId': usuarioId,
    };

    if (controlAgua) p.addAll(_agua(campos));
    if (quintales) p.addAll(_quintales(campos));
    return p..removeWhere((_, v) => v == null);
  }

  /// `CreateSeguimientoDiarioLoteReproductoraRequest`. Difiere del de engorde en
  /// el consumo: acá va como escalar + unidad, no como `consumoKg*`.
  static Map<String, dynamic> reproductora({
    required int loteId,
    required DateTime fecha,
    required Map<String, String> campos,
    required bool controlAgua,
    required bool quintales,
    String? usuarioId,
  }) {
    final p = <String, dynamic>{
      'loteId': loteId,
      'fechaRegistro': _fechaIso(fecha),
      'ciclo': 'Normal',
      'mortalidadHembras': _entero(campos['mortalidadHembras']),
      'mortalidadMachos': _entero(campos['mortalidadMachos']),
      'selH': _entero(campos['selH']),
      'selM': _entero(campos['selM']),
      'errorSexajeHembras': _entero(campos['errorSexajeHembras']),
      'errorSexajeMachos': _entero(campos['errorSexajeMachos']),
      'tipoAlimento': campos['tipoAlimento']?.trim() ?? '',
      'consumoHembras': _decimal(campos['consumoKgHembras']),
      'unidadConsumoHembras': 'kg',
      'consumoMachos': _decimal(campos['consumoKgMachos']),
      'unidadConsumoMachos': 'kg',
      'pesoPromH': _decimal(campos['pesoPromH']),
      'pesoPromM': _decimal(campos['pesoPromM']),
      'uniformidadH': _decimal(campos['uniformidadH']),
      'uniformidadM': _decimal(campos['uniformidadM']),
      'cvH': _decimal(campos['cvH']),
      'cvM': _decimal(campos['cvM']),
      'observaciones': _textoONulo(campos['observaciones']),
      if (usuarioId != null && usuarioId.isNotEmpty) 'createdByUserId': usuarioId,
    };

    if (controlAgua) p.addAll(_agua(campos));
    if (quintales) p.addAll(_quintales(campos));
    return p..removeWhere((_, v) => v == null);
  }

  static Map<String, dynamic> _agua(Map<String, String> c) => {
        'consumoAguaDiario': _decimal(c['consumoAguaDiario']),
        'consumoAguaPh': _decimal(c['consumoAguaPh']),
        'consumoAguaOrp': _decimal(c['consumoAguaOrp']),
        'consumoAguaTemperatura': _decimal(c['consumoAguaTemperatura']),
      };

  static Map<String, dynamic> _quintales(Map<String, String> c) => {
        'qqMixtas': _decimal(c['qqMixtas']),
        'qqHembras': _decimal(c['qqHembras']),
        'qqMachos': _decimal(c['qqMachos']),
      };

  /// El backend recibe `DateTime`. Se manda la fecha a mediodía **sin zona**:
  /// un `2026-08-21T00:00:00Z` cae al día anterior en cualquier huso al oeste de
  /// Greenwich, que son todos los de la operación.
  static String _fechaIso(DateTime f) =>
      DateTime(f.year, f.month, f.day, 12).toIso8601String();

  /// Los enteros del formulario nunca son null: un campo vacío es cero
  /// mortalidad, no "dato faltante".
  static int _entero(String? v) {
    if (v == null) return 0;
    return int.tryParse(v.trim()) ?? 0;
  }

  /// Los decimales sí pueden ser null: un peso sin medir no es un peso de 0 kg.
  /// Acepta coma o punto — el teclado numérico de Android da coma en es-CO.
  static double? _decimal(String? v) {
    final t = v?.trim().replaceAll(',', '.');
    if (t == null || t.isEmpty) return null;
    return double.tryParse(t);
  }

  static String? _textoONulo(String? v) {
    final t = v?.trim();
    return (t == null || t.isEmpty) ? null : t;
  }
}
