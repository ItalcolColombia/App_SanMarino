/// Cómo se arma el array de ítems que dispara el descuento de inventario.
///
/// Lógica **pura y testeada**, aparte del cliente HTTP, porque acá se decide un
/// número que mueve el stock de un galpón. Es el equivalente en Dart de lo que
/// en el backend vive en `Application/Calculos/`.
///
/// ## Lo que hay que acertar, y por qué duele equivocarse
///
/// **1. Qué id mandar depende del PAÍS, no del módulo.** En Ecuador y Panamá el
/// backend usa `itemInventarioEcuadorId` tal cual; en Colombia recibe
/// `catalogItemId` y lo traduce por código contra el catálogo de la empresa.
/// Mandar sólo `catalogItemId` en Ecuador/Panamá no da un error limpio: el
/// parser cae al `catalogItemId` y lo usa **como si fuera** un id de
/// `item_inventario_ecuador`. Hay 227 ids que colisionan entre las dos tablas,
/// así que el descuento puede caer sobre otro producto sin que nadie se entere.
///
/// **2. `tipoItem` NO filtra el descuento.** El parser del backend no lo mira
/// nunca: una vacuna metida en el array descuenta stock igual que el alimento.
/// Sólo decide si la cantidad suma además a la columna de consumo.
///
/// **3. La unidad sólo traduce gramos.** `'l'`, `'lb'`, `'unidades'` y `'qq'`
/// se restan **como si fueran kilos**, sin error ni log. Por eso la app manda
/// siempre kg ya convertidos.
///
/// **4. El `siloId` se OMITE, no se manda en null.** Con el flag de silo
/// apagado, mandarlo con valor es un 400 en Colombia.
library;

import 'models.dart';
import 'models_inventario.dart';
import 'perfil_pais.dart';

class ItemsConsumo {
  const ItemsConsumo._();

  /// Convierte las líneas del formulario en el array que espera el backend.
  ///
  /// Descarta las líneas incompletas en silencio —una fila a medio llenar no es
  /// un consumo— pero **nunca** inventa una: si el operario no cargó nada,
  /// devuelve lista vacía y el registro va sin descuento, como hasta ahora.
  static List<Map<String, dynamic>> armar({
    required List<LineaConsumo> lineas,
    required int? paisId,
    required bool manejaSilos,
  }) {
    final usaIdDeInventario = PerfilPais.usaItemInventarioEcuador(paisId);

    return lineas.where((l) => l.valida).map((l) {
      final item = <String, dynamic>{
        // No filtra el descuento, pero sí decide si la cantidad suma a la
        // columna de consumo del seguimiento. Para alimento va 'alimento'.
        'tipoItem': _tipoDe(l),
        'nombre': l.item.nombre,
        // Siempre en kg: el backend sólo sabe convertir gramos.
        'cantidad': l.cantidadKg,
        'unidad': 'kg',
      };

      // Colombia traduce por código a partir de `catalogItemId`; Ecuador y
      // Panamá usan el id de inventario directo. El web manda el mismo valor en
      // las dos claves cuando usa el id de inventario, y se copia ese patrón:
      // así el registro es legible en los dos caminos.
      item['catalogItemId'] = l.item.id;
      if (usaIdDeInventario) item['itemInventarioEcuadorId'] = l.item.id;

      // Se OMITE la clave si no hay silo. Mandarla en null no es lo mismo que
      // no mandarla, y con el flag apagado un silo con valor es un 400.
      if (manejaSilos && (l.siloId ?? 0) > 0) item['siloId'] = l.siloId;

      return item;
    }).toList();
  }

  /// El backend NO filtra el descuento por este campo, pero sí decide con él si
  /// la cantidad suma además a la columna de consumo del seguimiento.
  static String _tipoDe(LineaConsumo l) => l.item.esAlimento ? 'alimento' : 'otro';

  /// Los kilos que suman a la columna de consumo del seguimiento, para que el
  /// formulario muestre el total sin esperar al servidor.
  ///
  /// Sólo cuenta el **alimento**: es la misma regla que aplica el guard de
  /// alimento obligatorio del backend, donde los "otros ítems" no satisfacen
  /// la exigencia por sí solos.
  static double kgDeAlimento(List<LineaConsumo> lineas) => lineas
      .where((l) => l.valida && l.item.esAlimento)
      .fold(0.0, (s, l) => s + l.cantidadKg);

  /// Avisa **antes de guardar** si lo cargado supera la existencia conocida.
  ///
  /// Es un aviso, no una garantía: la foto del stock puede tener horas y el
  /// servidor valida contra el saldo del momento del sync. Sirve para que el
  /// operario lo vea mientras todavía está frente al galpón, en vez de
  /// descubrirlo cuando vuelve la señal.
  static List<String> avisosDeStock({
    required List<LineaConsumo> lineas,
    required Map<String, ExistenciaInventario> existenciasPorClave,
    required int farmId,
    String? nucleoId,
    String? galponId,
  }) {
    final avisos = <String>[];
    for (final l in lineas.where((x) => x.valida)) {
      final clave = ExistenciaInventario.claveDe(
        farmId: farmId, itemId: l.item.id,
        nucleoId: nucleoId, galponId: galponId, siloId: l.siloId,
      );
      final e = existenciasPorClave[clave];
      if (e == null) {
        avisos.add('${l.item.nombre}: no hay existencia registrada en este galpón.');
      } else if (l.cantidadKg > e.disponible) {
        avisos.add(
          '${l.item.nombre}: hay ${_kg(e.disponible)} disponibles y estás '
          'registrando ${_kg(l.cantidadKg)}.',
        );
      }
    }
    return avisos;
  }

  static String _kg(double v) =>
      '${v.toStringAsFixed(v.truncateToDouble() == v ? 0 : 1)} kg';

  /// Dónde va el array dentro del payload, que **no es igual en los cuatro
  /// módulos**: producción lee los ítems del request, y los otros tres del
  /// `metadata` que arma el backend a partir de esas mismas claves.
  ///
  /// Para el cliente la diferencia no se nota —las claves se llaman igual— pero
  /// está documentada acá porque copiar el patrón de levante para producción
  /// asumiendo que todo sale del metadata hace que **no descuente y no dé error**.
  static void aplicarEn(
    Map<String, dynamic> payload, {
    required List<Map<String, dynamic>> itemsHembras,
    required List<Map<String, dynamic>> itemsMachos,
    List<Map<String, dynamic>> itemsGenerales = const [],
    required ModuloSeguimiento modulo,
  }) {
    if (itemsHembras.isNotEmpty) payload['itemsHembras'] = itemsHembras;
    if (itemsMachos.isNotEmpty) payload['itemsMachos'] = itemsMachos;

    // `itemsGenerales` sólo existe en el request de levante/engorde. En
    // reproductora el backend ni siquiera lo proyecta al metadata, y en
    // producción no está en el contrato: mandarlo se pierde en silencio.
    final aceptaGenerales = modulo == ModuloSeguimiento.levante ||
        modulo == ModuloSeguimiento.engorde;
    if (aceptaGenerales && itemsGenerales.isNotEmpty) {
      payload['itemsGenerales'] = itemsGenerales;
    }
  }
}
