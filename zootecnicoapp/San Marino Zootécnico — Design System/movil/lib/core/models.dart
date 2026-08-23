/// Modelos de dominio. Vocabulario en español — coincide con el backend.
library;

/// Los 4 módulos de seguimiento diario. `id` coincide con `tipo_seguimiento`
/// de la tabla polimórfica `seguimiento_diario` del backend.
enum ModuloSeguimiento {
  levante('levante', 'Levante', '🌱'),
  engorde('engorde', 'Pollo Engorde', '🐔'),
  produccion('produccion', 'Producción', '🥚'),
  reproductora('reproductora', 'Reproductora', '🐣');

  const ModuloSeguimiento(this.id, this.label, this.emoji);
  final String id;
  final String label;
  final String emoji;

  static ModuloSeguimiento? fromId(String id) =>
      ModuloSeguimiento.values.where((m) => m.id == id).firstOrNull;
}

/// Usuario en sesión. `modulos` y `loteIds` llegan del backend al hacer login:
/// el rol define qué módulos ve y las granjas asignadas definen qué lotes.
class Usuario {
  const Usuario({
    required this.id,
    required this.nombre,
    required this.email,
    required this.cargo,
    required this.granja,
    required this.pais,
    required this.modulos,
    required this.loteIds,
  });

  final int id;
  final String nombre;
  final String email;
  final String cargo;
  final String granja;
  /// 'colombia' | 'ecuador' | 'panama'
  final String pais;
  final List<ModuloSeguimiento> modulos;
  final List<int> loteIds;

  /// El control de agua (pH, ORP, temperatura) solo aplica en Ecuador y Panamá.
  bool get tieneControlAgua => pais == 'ecuador' || pais == 'panama';

  bool tieneModulo(ModuloSeguimiento m) => modulos.contains(m);

  String get iniciales {
    final partes = nombre.trim().split(RegExp(r'\s+'));
    return partes.take(2).map((p) => p.isEmpty ? '' : p[0].toUpperCase()).join();
  }

  factory Usuario.fromJson(Map<String, dynamic> j) => Usuario(
    id: j['id'] as int,
    nombre: j['nombre'] as String,
    email: j['email'] as String,
    cargo: j['cargo'] as String? ?? '',
    granja: j['granja'] as String? ?? '',
    pais: (j['pais'] as String? ?? 'colombia').toLowerCase(),
    modulos: ((j['modulos'] as List?) ?? const [])
        .map((m) => ModuloSeguimiento.fromId(m as String))
        .whereType<ModuloSeguimiento>()
        .toList(),
    loteIds: ((j['loteIds'] as List?) ?? const []).cast<int>(),
  );
}

/// Lote asignado. `dia` es la edad en días desde el encasetamiento.
class Lote {
  const Lote({
    required this.id,
    required this.nombre,
    required this.granja,
    required this.galpon,
    required this.modulo,
    required this.dia,
    required this.aves,
    this.viabilidad,
    this.raza,
    this.anoTablaGenetica,
  });

  final int id;
  final String nombre;
  final String granja;
  final String galpon;
  final ModuloSeguimiento modulo;
  final int dia;
  final int aves;
  final double? viabilidad;
  final String? raza;
  final int? anoTablaGenetica;

  int get semana => (dia / 7).ceil();

  factory Lote.fromJson(Map<String, dynamic> j) => Lote(
    id: j['id'] as int,
    nombre: j['nombre'] as String,
    granja: j['granja'] as String? ?? '',
    galpon: j['galpon'] as String? ?? '',
    modulo: ModuloSeguimiento.fromId(j['modulo'] as String? ?? 'levante') ?? ModuloSeguimiento.levante,
    dia: j['dia'] as int? ?? 0,
    aves: j['aves'] as int? ?? 0,
    viabilidad: (j['viabilidad'] as num?)?.toDouble(),
    raza: j['raza'] as String?,
    anoTablaGenetica: j['anoTablaGenetica'] as int?,
  );
}

/// Ítem consumido en un seguimiento: alimento, medicamento, suplemento…
/// En el web es un FormArray; aquí una lista dinámica por sexo.
class ItemSeguimiento {
  ItemSeguimiento({this.tipo = '', this.catalogItemId, this.cantidad = '', this.unidad = 'kg'});

  String tipo;
  int? catalogItemId;
  String cantidad;
  String unidad;

  Map<String, dynamic> toJson() => {
    'tipoItem': tipo,
    'catalogItemId': catalogItemId,
    'cantidad': double.tryParse(cantidad.replaceAll(',', '.')) ?? 0,
    'unidad': unidad,
  };
}

/// Un registro pendiente de sincronizar. Se persiste en SQLite.
enum EstadoSync { pending, syncing, synced, error }

class RegistroPendiente {
  const RegistroPendiente({
    required this.id,
    required this.tipo,
    required this.loteId,
    required this.loteNombre,
    required this.fecha,
    required this.payload,
    required this.estado,
    required this.createdAt,
    this.intentos = 0,
    this.ultimoError,
  });

  final String id;
  /// 'levante' | 'engorde' | 'produccion' | 'reproductora' |
  /// 'venta-aves' | 'traslado-aves' | 'movimiento-huevos'
  final String tipo;
  final int loteId;
  final String loteNombre;
  final DateTime fecha;
  final Map<String, dynamic> payload;
  final EstadoSync estado;
  final DateTime createdAt;
  final int intentos;
  final String? ultimoError;
}
