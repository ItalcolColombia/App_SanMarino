/// Ítems de inventario y sus existencias, tal como los ve la app.
///
/// Estos dos modelos son la razón de ser del descuento de stock: sin un ítem
/// con su id, el backend guarda el seguimiento y **no toca el inventario** —
/// hace falta un array de ítems con `id > 0` para que el descuento se ejecute.
library;

/// Un ítem del catálogo que el operario puede elegir.
///
/// **Los dos ids no son intercambiables y el que vale depende del país.** En
/// Ecuador y Panamá el backend usa `itemInventarioEcuadorId` tal cual, sin
/// traducir; en Colombia recibe `catalogItemId` y lo traduce por código contra
/// el catálogo de la empresa. Mandar el equivocado no da un error limpio: puede
/// descontar **otro producto**, porque los ids colisionan entre las dos tablas
/// (227 colisiones medidas en la base local — el id 89 es un alimento en una y
/// un líquido en la otra).
class ItemInventario {
  const ItemInventario({
    required this.id,
    required this.codigo,
    required this.nombre,
    required this.tipo,
    required this.unidad,
    this.concepto,
  });

  /// `item_inventario_ecuador.id`. Es el que viaja como `itemInventarioEcuadorId`.
  final int id;

  /// Código del producto. Es la llave con la que Colombia cruza las dos tablas.
  final String codigo;

  final String nombre;

  /// Texto libre del catálogo: llega como `Alimento`, `alimento`, `ALIMENTO`…
  final String tipo;

  /// Unidad declarada en el catálogo. Es una **etiqueta**: nada convierte con
  /// ella. Lo único que el backend traduce es gramos → kilos.
  final String unidad;

  /// Clasificación alternativa; cuando viene, manda sobre [tipo] (es lo que
  /// hace el web).
  final String? concepto;

  /// Si es alimento. Se normaliza acá porque el filtro `?tipoItem=` del backend
  /// compara **exacto y sensible a mayúsculas**: pedir `alimento` devolvería 1
  /// de los 8 alimentos de Ecuador, que están cargados como `Alimento`. Por eso
  /// la app baja el catálogo entero y clasifica localmente.
  bool get esAlimento => (concepto ?? tipo).trim().toLowerCase() == 'alimento';

  factory ItemInventario.fromJson(Map<String, dynamic> j) => ItemInventario(
        id: (j['id'] as num?)?.toInt() ?? 0,
        codigo: (j['codigo'] ?? j['itemCodigo'] ?? '') as String,
        nombre: (j['nombre'] ?? j['itemNombre'] ?? '') as String,
        tipo: (j['tipoItem'] ?? j['itemType'] ?? '') as String,
        unidad: (j['unidad'] ?? j['unit'] ?? 'kg') as String,
        concepto: j['concepto'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'id': id, 'codigo': codigo, 'nombre': nombre,
        'tipo': tipo, 'unidad': unidad, 'concepto': concepto,
      };
}

/// La existencia de un ítem **en un lugar concreto**.
///
/// La clave del stock es `(granja, núcleo, galpón, silo, ítem)`, no el ítem
/// solo. Guardar un saldo por ítem mostraría el total de la granja y dejaría
/// pasar un consumo contra un galpón vacío.
class ExistenciaInventario {
  const ExistenciaInventario({
    required this.itemId,
    required this.farmId,
    required this.cantidad,
    required this.reservado,
    required this.unidad,
    this.nucleoId,
    this.galponId,
    this.siloId,
    this.itemNombre = '',
  });

  final int itemId;
  final int farmId;
  final String? nucleoId;
  final String? galponId;
  final int? siloId;
  final String itemNombre;

  /// El saldo físico. **Es contra este número que el servidor valida**, no
  /// contra [disponible].
  final double cantidad;

  /// Kilos ya comprometidos por seguimientos sin validar (doble validación).
  final double reservado;

  final String unidad;

  /// Lo que realmente se puede comprometer. Es lo que se le muestra al
  /// operario: [cantidad] a secas le prometería alimento ya apartado.
  ///
  /// ⚠️ Pero **el servidor no valida contra esto**: sus dos guardianes comparan
  /// contra [cantidad] física. Un consumo que supera `disponible` pero no
  /// `cantidad` se acepta al sincronizar. Es convención de pantalla, no garantía.
  double get disponible => cantidad - reservado;

  /// La clave natural, con la misma normalización que usa el índice de la BD
  /// (`COALESCE` de núcleo, galpón y silo).
  String get clave => claveDe(
      farmId: farmId, itemId: itemId, nucleoId: nucleoId,
      galponId: galponId, siloId: siloId);

  static String claveDe({
    required int farmId,
    required int itemId,
    String? nucleoId,
    String? galponId,
    int? siloId,
  }) =>
      '$farmId|$itemId|${nucleoId?.trim() ?? ''}|${galponId?.trim() ?? ''}|${siloId ?? 0}';

  factory ExistenciaInventario.fromJson(Map<String, dynamic> j) => ExistenciaInventario(
        itemId: (j['itemInventarioEcuadorId'] as num?)?.toInt() ?? 0,
        farmId: (j['farmId'] as num?)?.toInt() ?? 0,
        nucleoId: j['nucleoId'] as String?,
        galponId: j['galponId'] as String?,
        siloId: (j['siloId'] as num?)?.toInt(),
        itemNombre: (j['itemNombre'] ?? '') as String,
        cantidad: (j['quantity'] as num?)?.toDouble() ?? 0,
        reservado: (j['reservadoKg'] as num?)?.toDouble() ?? 0,
        unidad: (j['unit'] ?? 'kg') as String,
      );

  Map<String, dynamic> toJson() => {
        'itemInventarioEcuadorId': itemId, 'farmId': farmId,
        'nucleoId': nucleoId, 'galponId': galponId, 'siloId': siloId,
        'itemNombre': itemNombre, 'quantity': cantidad,
        'reservadoKg': reservado, 'unit': unidad,
      };
}

/// Una línea de consumo que el operario cargó en el formulario.
///
/// Es lo que se convierte en el array `itemsHembras` / `itemsMachos` del
/// payload — el array cuya sola presencia dispara el descuento.
class LineaConsumo {
  LineaConsumo({
    required this.item,
    this.cantidad = '',
    this.siloId,
  });

  final ItemInventario item;

  /// Texto crudo del campo: se parsea al armar el payload, no antes, para no
  /// pelearse con el usuario mientras escribe.
  String cantidad;

  /// Sólo cuando la empresa maneja inventario por silo. Si no, la clave se
  /// **omite** del JSON — mandarla en null no es lo mismo que no mandarla.
  int? siloId;

  double get cantidadKg {
    final t = cantidad.trim().replaceAll(',', '.');
    return double.tryParse(t) ?? 0;
  }

  bool get valida => item.id > 0 && cantidadKg > 0;
}
