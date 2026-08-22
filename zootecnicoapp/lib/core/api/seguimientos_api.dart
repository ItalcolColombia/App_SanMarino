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
import '../postura_calculos.dart';
import 'api_client.dart';

/// A dónde se postea cada módulo. La ruta se guarda **con la fila encolada**, no
/// se deduce al enviar: si mañana cambia este mapeo, lo que el usuario ya
/// registró tiene que seguir yendo a donde iba cuando lo registró.
const Map<ModuloSeguimiento, String> endpointDeModulo = {
  ModuloSeguimiento.levante: '/SeguimientoLoteLevante',
  ModuloSeguimiento.engorde: '/SeguimientoAvesEngordeEcuador',
  ModuloSeguimiento.produccion: '/Produccion/seguimiento',
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

    // Producción devuelve el id **pelado** (`ActionResult<int>`); los otros tres
    // módulos devuelven el registro completo. Sin leer las dos formas, la fila de
    // la cola queda sin `remote_id` y después no se puede ni editar ni borrar.
    return switch (json) {
      int i => i,
      Map m => m['id'] as int?,
      _ => null,
    };
  }

  /// Fechas que el servidor ya tiene registradas para un lote. Se guardan en la
  /// caché local para poder avisar *antes* de que el usuario llene el formulario
  /// completo y se coma un 400 al sincronizar tres horas después.
  Future<Set<DateTime>> fechasRegistradas(Lote lote) async {
    // Los dos módulos de postura filtran por query, no por segmento de ruta.
    final query = switch (lote.modulo) {
      // Levante filtra por el lote MAESTRO, no por el id de lote_postura_levante.
      ModuloSeguimiento.levante => {'loteId': lote.loteMaestroId ?? lote.id},
      // `size: 0` = traer todos (el backend trata 100 como el default viejo).
      ModuloSeguimiento.produccion => {'lotePosturaProduccionId': lote.id, 'size': 0},
      _ => null,
    };

    final path = switch (lote.modulo) {
      ModuloSeguimiento.engorde => '/SeguimientoAvesEngordeEcuador/por-lote/${lote.id}',
      ModuloSeguimiento.reproductora =>
        '/SeguimientoDiarioLoteReproductora/por-lote-reproductora/${lote.id}',
      ModuloSeguimiento.levante => '/SeguimientoLoteLevante/filtro',
      ModuloSeguimiento.produccion => '/Produccion/seguimiento',
    };
    final cuerpo = await _api.getRaw(path, query: query);
    if (cuerpo.trim().isEmpty) return const {};

    final json = jsonDecode(cuerpo);
    // Cada módulo envuelve su listado distinto: reproductora y levante devuelven
    // la lista pelada, engorde la mete en `registros` y producción en `items`
    // (junto al total de la paginación).
    final filas = switch (json) {
      List l => l,
      Map m => (m['registros'] ?? m['seguimientos'] ?? m['items'] ?? const []) as List,
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

  /// Levante usa el **mismo** `CreateSeguimientoLoteLevanteRequest` que engorde,
  /// más `lotePosturaLevanteId`. Manda los DOS ids porque el backend los usa para
  /// cosas distintas: `loteId` es el lote maestro (la fila de `lotes`, la única
  /// que existe en las dos etapas) y `lotePosturaLevanteId` es el registro de la
  /// etapa. El web manda ambos y acá se hace igual.
  ///
  /// Los 11 campos de huevos de levante (semana 14+, flag `captura_huevos_en_levante`)
  /// NO se envían: el móvil no tiene ese tab todavía y el request los deja en null.
  static Map<String, dynamic> levante({
    required int loteId,
    required int? lotePosturaLevanteId,
    required DateTime fecha,
    required Map<String, String> campos,
    required bool controlAgua,
    required bool quintales,
    String? usuarioId,
  }) {
    final p = engorde(
      loteId: loteId,
      fecha: fecha,
      campos: campos,
      controlAgua: controlAgua,
      quintales: quintales,
      usuarioId: usuarioId,
    );
    if (lotePosturaLevanteId != null) p['lotePosturaLevanteId'] = lotePosturaLevanteId;
    return p;
  }

  /// `CrearSeguimientoRequest` de producción — el contrato más distinto de los
  /// cuatro. Cambian hasta los nombres de la mortalidad (`mortalidadH`, no
  /// `mortalidadHembras`) y el consumo vuelve a ser escalar + unidad.
  ///
  /// `huevosTotales` y `huevosIncubables` **no los escribe el usuario**: los
  /// calcula la clasificadora ([PosturaCalculos.totalesClasificadora]) y el
  /// backend los persiste tal cual llegan. Y `etapa` es obligatoria.
  static Map<String, dynamic> produccion({
    required int lotePosturaProduccionId,
    required DateTime fecha,
    required Map<String, String> campos,
    required bool controlAgua,
    DateTime? fechaEncaset,
    String? usuarioId,
  }) {
    final totales = PosturaCalculos.totalesClasificadora(campos);

    final p = <String, dynamic>{
      'lotePosturaProduccionId': lotePosturaProduccionId,
      'fechaRegistro': _fechaIso(fecha),
      'ciclo': 'Normal',
      'tipoSeguimiento': 'produccion',
      // Producción nombra la mortalidad distinto que los otros tres módulos.
      'mortalidadH': _entero(campos['mortalidadHembras']),
      'mortalidadM': _entero(campos['mortalidadMachos']),
      'selH': _entero(campos['selH']),
      'selM': _entero(campos['selM']),
      'errorSexajeHembras': _entero(campos['errorSexajeHembras']),
      'errorSexajeMachos': _entero(campos['errorSexajeMachos']),
      'tipoAlimento': campos['tipoAlimento']?.trim() ?? '',
      'consumoH': _decimal(campos['consumoKgHembras']),
      'unidadConsumoH': 'kg',
      'consumoM': _decimal(campos['consumoKgMachos']),
      'unidadConsumoM': 'kg',
      // Calculados, no capturados. El formulario los muestra en vivo y el backend
      // los recibe hechos: si el móvil los mandara distinto, el reporte semanal
      // no cuadraría con el web para el mismo día.
      'huevosTotales': totales.total,
      'huevosIncubables': totales.incubables,
      for (final k in [...huevosIncubables, ...huevosNoIncubables])
        k: _entero(campos[k]),
      'pesoHuevo': _decimal(campos['pesoHuevo']) ?? 0,
      'etapa': PosturaCalculos.etapa(fechaEncaset: fechaEncaset, fechaRegistro: fecha),
      // Pesaje semanal: se registra una vez por semana, así que casi siempre va null.
      'pesoH': _decimal(campos['pesoH']),
      'pesoM': _decimal(campos['pesoM']),
      // Uniformidad y CV viajan en dos juegos: el global del pesaje semanal y el
      // desglose por sexo. El request acepta los dos y el móvil captura ambos.
      'uniformidad': _decimal(campos['uniformidad']),
      'coeficienteVariacion': _decimal(campos['coeficienteVariacion']),
      'uniformidadHembras': _decimal(campos['uniformidadHembras']),
      'uniformidadMachos': _decimal(campos['uniformidadMachos']),
      'cvHembras': _decimal(campos['cvHembras']),
      'cvMachos': _decimal(campos['cvMachos']),
      'observaciones': _textoONulo(campos['observaciones']),
      if (usuarioId != null && usuarioId.isNotEmpty) 'createdByUserId': usuarioId,
    };

    if (controlAgua) p.addAll(_agua(campos));
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
