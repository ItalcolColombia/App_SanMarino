/// Traslado de huevos: del galpón a la planta.
///
/// ── El contrato del destino, y por qué es este ───────────────────────────────
/// Los dos formularios del web se contradicen: el standalone exige
/// `granjaDestinoId` para un Traslado, y el modal manda granja y lote en `null`
/// y fija `tipoDestino: 'Planta'`.
///
/// Manda el **modal**, y no por gusto: el Reporte Diario de Costos de Postura
/// (`backend/sql/fn_reporte_diario_costos_postura.sql:425`) arma su columna
/// `huevo_traslado_planta` con
///
///     WHEN th.tipo_operacion = 'Traslado' AND th.tipo_destino = 'Planta'
///
/// y sólo sobre `estado = 'Completado'`. Un traslado con cualquier otro
/// `tipo_destino` **no aparece en el reporte contable**: los huevos se
/// descuentan del lote y no se ven en ningún lado.
///
/// ── Lo que esta app deliberadamente NO hace ──────────────────────────────────
/// **Traslado entre granjas.** El flujo del backend es unilateral:
/// `GranjaDestinoId`/`LoteDestinoId` son metadatos y **nada acredita los huevos
/// en el destino**. Mover huevos "a otra granja" los hace desaparecer del
/// sistema. Hasta que exista recepción, la app sólo ofrece galpón → planta, que
/// es el movimiento que el negocio hace de verdad y el único que el contable
/// cuenta.
library;

import 'dart:convert';

import 'package:zootecnicoapp/core/api/api_client.dart';

/// Ruta del POST. Viaja congelada con la fila de la cola (invariante I5).
const String endpointTrasladoHuevos = '/traslados/huevos';

/// Las 11 categorías, en el orden en que el operario las cuenta.
///
/// La clave es la del backend (`cantidadLimpio`…); la etiqueta, la que ve el
/// operario. Son las mismas que ya tipea en el seguimiento diario de producción
/// — no se le pide aprender un vocabulario nuevo para mover lo que acaba de
/// contar.
const List<({String clave, String etiqueta})> categoriasHuevo = [
  (clave: 'cantidadLimpio', etiqueta: 'Limpio'),
  (clave: 'cantidadTratado', etiqueta: 'Tratado'),
  (clave: 'cantidadSucio', etiqueta: 'Sucio'),
  (clave: 'cantidadDeforme', etiqueta: 'Deforme'),
  (clave: 'cantidadBlanco', etiqueta: 'Blanco'),
  (clave: 'cantidadDobleYema', etiqueta: 'Doble yema'),
  (clave: 'cantidadPiso', etiqueta: 'Piso'),
  (clave: 'cantidadPequeno', etiqueta: 'Pequeño'),
  (clave: 'cantidadRoto', etiqueta: 'Roto'),
  (clave: 'cantidadDesecho', etiqueta: 'Desecho'),
  (clave: 'cantidadOtro', etiqueta: 'Otro'),
];

/// Cuántos huevos hay disponibles por categoría para un lote de producción.
class DisponibilidadHuevos {
  const DisponibilidadHuevos(this.porCategoria);

  /// Clave del backend (`cantidadLimpio`…) → cantidad disponible.
  final Map<String, int> porCategoria;

  int de(String clave) => porCategoria[clave] ?? 0;

  int get total => porCategoria.values.fold(0, (a, b) => a + b);

  bool get vacio => total == 0;

  /// El backend nombra los disponibles por TIPO (`Limpio`, `DobleYema`…) y el
  /// payload de creación por CANTIDAD (`cantidadLimpio`…). Acá se traduce una
  /// vez, para que la pantalla hable un solo idioma.
  factory DisponibilidadHuevos.desdeJson(Map<String, dynamic> j) {
    // Tolerante a propósito: esto sólo alimenta un "disponible: N" en pantalla.
    // Un cuerpo con otra forma tiene que dejar la pantalla usable —el operario
    // sigue pudiendo cargar— y no tumbarla con un cast fallido.
    final candidato = j['disponiblePorTipo'] ?? j['huevosDisponibles'] ?? j;
    final crudo = candidato is Map ? candidato : const {};
    final porCategoria = <String, int>{};

    for (final c in categoriasHuevo) {
      // 'cantidadDobleYema' → 'DobleYema'
      final tipo = c.clave.substring('cantidad'.length);
      final v = crudo[tipo] ?? crudo[_minusculaInicial(tipo)] ?? crudo[c.clave];
      porCategoria[c.clave] = _entero(v);
    }
    return DisponibilidadHuevos(porCategoria);
  }

  static String _minusculaInicial(String s) =>
      s.isEmpty ? s : s[0].toLowerCase() + s.substring(1);

  static int _entero(Object? v) => switch (v) {
    int i => i,
    num n => n.round(),
    String s => int.tryParse(s) ?? 0,
    _ => 0,
  };
}

class TrasladosApi {
  TrasladosApi(this._api);

  final ApiClient _api;

  /// Qué hay disponible para mover en este lote de producción.
  ///
  /// Se consulta ANTES de capturar: que el operario no cargue 500 huevos que no
  /// existen para enterarse al sincronizar, tres horas después.
  Future<DisponibilidadHuevos> disponibilidad(int lotePosturaProduccionId) async {
    final cuerpo = await _api.getRaw('/traslados/lote-lpp/$lotePosturaProduccionId/disponibilidad');
    if (cuerpo.trim().isEmpty) return const DisponibilidadHuevos({});
    final json = jsonDecode(cuerpo);
    if (json is! Map<String, dynamic>) return const DisponibilidadHuevos({});
    return DisponibilidadHuevos.desdeJson(json);
  }

  /// El cuerpo del POST, armado aparte del envío para que la cola pueda
  /// guardarlo y reintentarlo tal cual.
  static Map<String, dynamic> payload({
    required int lotePosturaProduccionId,
    required DateTime fecha,
    required Map<String, int> cantidades,
    String? observaciones,
  }) {
    return {
      'lotePosturaProduccionId': lotePosturaProduccionId,
      // El backend resuelve el lote unificado desde el LPP; mandarlo vacío es lo
      // que hace el web en este mismo caso.
      'loteId': '',
      'fechaTraslado': _fechaIso(fecha),
      'tipoOperacion': 'Traslado',
      // Ver la nota del encabezado: 'Planta' es lo único que el contable cuenta.
      'tipoDestino': 'Planta',
      'granjaDestinoId': null,
      'loteDestinoId': null,
      for (final c in categoriasHuevo) c.clave: cantidades[c.clave] ?? 0,
      'totalHuevos': cantidades.values.fold(0, (a, b) => a + b),
      if (observaciones != null && observaciones.trim().isNotEmpty)
        'observaciones': observaciones.trim(),
    };
  }

  /// Mediodía sin zona, igual que el resto de la app: el backend fecha por día y
  /// mandar medianoche local corre el registro de día en husos negativos
  /// (invariante I15).
  static String _fechaIso(DateTime f) =>
      DateTime(f.year, f.month, f.day, 12).toIso8601String();
}
