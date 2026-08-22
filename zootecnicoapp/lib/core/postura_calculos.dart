/// Aritmética de los módulos de postura: clasificadora de huevos y etapa del ciclo.
///
/// Es lógica **pura** y con tests, por el mismo motivo que en el backend: son dos
/// números que se calculan en más de un lugar, y cuando divergen nadie se entera
/// hasta que alguien mira el reporte semanal.
///
/// Espeja `modal-seguimiento-diario.component.ts` del web
/// (`totalesClasificadoraFija` y `calcularEtapa`), que es la fuente de verdad:
/// esos totales viajan en el payload, el backend los persiste tal cual.
library;

/// Las 11 categorías de la clasificadora fija, en el orden del formulario.
///
/// **Incubables** son las dos primeras. Las otras nueve suman al total pero no
/// son incubables — esa separación es toda la regla.
const List<String> huevosIncubables = ['huevoLimpio', 'huevoTratado'];

const List<String> huevosNoIncubables = [
  'huevoSucio',
  'huevoDeforme',
  'huevoBlanco',
  'huevoDobleYema',
  'huevoPiso',
  'huevoPequeno',
  'huevoRoto',
  'huevoDesecho',
  'huevoOtro',
];

/// Totales de la clasificadora fija de 11 columnas.
class TotalesHuevos {
  const TotalesHuevos({required this.incubables, required this.total});

  final int incubables;
  final int total;

  /// Porcentaje de incubabilidad. Null cuando no hay huevos: un 0 % con total
  /// cero no significa "malo", significa "no se recogió nada".
  double? get porcentajeIncubables =>
      total == 0 ? null : (incubables / total) * 100;

  static const TotalesHuevos cero = TotalesHuevos(incubables: 0, total: 0);
}

class PosturaCalculos {
  const PosturaCalculos._();

  /// `incubables = limpio + tratado` · `total = incubables + las 9 no incubables`.
  ///
  /// El usuario no escribe estos dos números: el formulario los calcula y el
  /// backend los recibe ya hechos. Por eso la aritmética tiene que ser la misma
  /// que la del web, no una aproximación razonable.
  static TotalesHuevos totalesClasificadora(Map<String, String> campos) {
    int n(String k) => int.tryParse((campos[k] ?? '').trim()) ?? 0;

    final incubables = huevosIncubables.fold(0, (s, k) => s + n(k));
    final noIncubables = huevosNoIncubables.fold(0, (s, k) => s + n(k));

    return TotalesHuevos(
      incubables: incubables,
      total: incubables + noIncubables,
    );
  }

  /// Etapa del ciclo de postura: **1** semana 26-33 · **2** semana 34-50 · **3** >50.
  ///
  /// El rango arranca en 26, no en 25: el comentario del DTO del backend dice
  /// 25-33, pero la implementación que produce el dato hace `max(26, …)` y ese es
  /// el número que termina persistido. Se copia el que manda.
  ///
  /// ⚠️ **No cubre el ciclo por raza de Santa Reyes**
  /// (`companies.semanas_ciclo_postura_por_raza`): eso necesita la guía genética,
  /// que la app todavía no descarga. Para esas empresas el número puede diferir
  /// del que calcularía el web; hasta que el móvil traiga la guía, ese módulo se
  /// registra desde la web.
  static int etapa({DateTime? fechaEncaset, required DateTime fechaRegistro}) {
    if (fechaEncaset == null) return 1;

    final desde = DateTime(fechaEncaset.year, fechaEncaset.month, fechaEncaset.day);
    final hasta = DateTime(fechaRegistro.year, fechaRegistro.month, fechaRegistro.day);
    final dias = hasta.difference(desde).inDays;

    // `ceil` sobre los días, igual que el web: el día 1 ya cuenta como semana 1.
    final semana = _max(26, (dias / 7).ceil());

    if (semana <= 33) return 1;
    if (semana <= 50) return 2;
    return 3;
  }

  static int _max(int a, int b) => a > b ? a : b;
}
