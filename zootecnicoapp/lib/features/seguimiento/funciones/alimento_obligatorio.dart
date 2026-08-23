/// Alimento obligatorio en el seguimiento diario.
///
/// Espejo en Dart de `Application/Calculos/AlimentoObligatorioCalculos.cs`
/// (pedido del 14-ago-2026): no se puede registrar un día sin indicar el tipo de
/// alimento y la cantidad consumida.
///
/// **Por qué se valida también acá.** El backend la exige igual, pero en una app
/// offline el rechazo llegaría horas después: el usuario guarda en el galpón, se
/// va tranquilo, y el registro rebota cuando vuelve la señal. La regla se aplica
/// en el momento de guardar, con el mismo texto que da el servidor, para que el
/// usuario lo arregle mientras todavía tiene el lote enfrente.
///
/// El servidor sigue siendo el dueño de la regla: esto es una copia adelantada,
/// no una autorización.
library;

import 'package:zootecnicoapp/core/models/models.dart';

class AlimentoObligatorio {
  const AlimentoObligatorio._();

  /// Motivo del rechazo, o `null` si el registro cumple.
  ///
  /// [kgHembras] y [kgMachos] son los kilos de los bloques por sexo — los únicos
  /// que cuentan. En engorde de Panamá el bloque Mixto vuelca sobre hembras, así
  /// que entra por la misma puerta.
  static String? motivo({
    required ModuloSeguimiento modulo,
    required double? kgHembras,
    required double? kgMachos,
    required String? tipoAlimento,
  }) {
    final cuentan = (kgHembras ?? 0) + (kgMachos ?? 0);
    if (cuentan <= 0) return 'Sin alimento: ${_bloqueExigido(modulo)}';

    // El backend arma el `tipoAlimento` desde los ítems de inventario cuando el
    // texto viene vacío; desde el móvil no hay ítems, así que sin el nombre el
    // registro llegaría con el tipo en blanco.
    if ((tipoAlimento ?? '').trim().isEmpty) {
      return 'Falta el tipo de alimento: indicá cuál se consumió.';
    }
    return null;
  }

  /// Qué bloque exige cada módulo. Nombra el campo tal como aparece en pantalla,
  /// igual que el mensaje del backend.
  static String _bloqueExigido(ModuloSeguimiento modulo) => switch (modulo) {
        ModuloSeguimiento.engorde =>
          'hay que indicar el tipo de alimento y la cantidad de consumo en Hembras o en Machos.',
        ModuloSeguimiento.levante || ModuloSeguimiento.produccion =>
          'hay que indicar el tipo de alimento y la cantidad de consumo en Hembras, en Machos o en ambos.',
        ModuloSeguimiento.reproductora =>
          'hay que indicar el tipo de alimento y la cantidad de consumo del lote.',
      };
}
