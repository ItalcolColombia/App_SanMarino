/// Modelos de dominio. Vocabulario en español — coincide con el backend.
library;

import 'perfil_pais.dart';

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

/// Usuario en sesión, tal como lo devuelve `POST /api/Auth/login`.
///
/// La empresa y el país salen de `companyPaises[0]` — nunca de un header ni de
/// una preferencia local: el backend valida la empresa contra `user_companies`
/// y descarta la que no corresponda. Los módulos salen del menú del usuario
/// (`GET /api/Auth/menu`), no del rol ni del país.
class Usuario {
  const Usuario({
    required this.id,
    required this.nombre,
    required this.email,
    required this.cargo,
    required this.granja,
    required this.paisId,
    required this.paisNombre,
    required this.companyId,
    required this.companyName,
    required this.token,
    required this.modulos,
  });

  /// `userId` del backend: es un GUID, no un entero.
  final String id;
  final String nombre;
  final String email;

  /// Rol principal — lo que se muestra bajo el nombre en el perfil.
  final String cargo;

  /// Granja de referencia del usuario. Vacía cuando tiene varias asignadas.
  final String granja;

  /// `paises.pais_id`: 1 Colombia · 2 Ecuador · 3 Panamá. Null si el usuario no
  /// tiene empresa-país resuelta, y entonces la app no ofrece registrar.
  final int? paisId;
  final String paisNombre;

  final int? companyId;
  final String companyName;

  /// JWT. Vive 60 min en producción; la cola offline le sobrevive.
  final String token;

  final List<ModuloSeguimiento> modulos;

  /// Etiqueta corta del país para el badge del perfil.
  String get pais => paisNombre.isEmpty ? PerfilPais.nombre(paisId) : paisNombre;

  /// El control de agua (pH, ORP, temperatura) solo aplica en Ecuador y Panamá.
  bool get tieneControlAgua => PerfilPais.controlAgua(paisId);

  /// El alimento en quintales solo se captura en Panamá.
  bool get capturaQuintales => PerfilPais.quintales(paisId);

  /// Sin empresa resuelta no se puede registrar nada: el backend descartaría el
  /// scope y el registro caería en la empresa equivocada o en ninguna.
  bool get puedeRegistrar => companyId != null && paisId != null;

  bool tieneModulo(ModuloSeguimiento m) => modulos.contains(m);

  String get iniciales {
    final partes = nombre.trim().split(RegExp(r'\s+'));
    return partes.take(2).map((p) => p.isEmpty ? '' : p[0].toUpperCase()).join();
  }

  Usuario copyWith({List<ModuloSeguimiento>? modulos, String? token, String? granja}) => Usuario(
    id: id, nombre: nombre, email: email, cargo: cargo,
    granja: granja ?? this.granja,
    paisId: paisId, paisNombre: paisNombre,
    companyId: companyId, companyName: companyName,
    token: token ?? this.token,
    modulos: modulos ?? this.modulos,
  );

  /// Reconstruye la sesión guardada en SQLite (ver `SessionStore`).
  factory Usuario.fromJson(Map<String, dynamic> j) => Usuario(
    id: j['id'] as String? ?? '',
    nombre: j['nombre'] as String? ?? '',
    email: j['email'] as String? ?? '',
    cargo: j['cargo'] as String? ?? '',
    granja: j['granja'] as String? ?? '',
    paisId: j['paisId'] as int?,
    paisNombre: j['paisNombre'] as String? ?? '',
    companyId: j['companyId'] as int?,
    companyName: j['companyName'] as String? ?? '',
    token: j['token'] as String? ?? '',
    modulos: ((j['modulos'] as List?) ?? const [])
        .map((m) => ModuloSeguimiento.fromId(m as String))
        .whereType<ModuloSeguimiento>()
        .toList(),
  );

  Map<String, dynamic> toJson() => {
    'id': id, 'nombre': nombre, 'email': email, 'cargo': cargo, 'granja': granja,
    'paisId': paisId, 'paisNombre': paisNombre,
    'companyId': companyId, 'companyName': companyName,
    'token': token,
    'modulos': modulos.map((m) => m.id).toList(),
  };
}

/// Lote sobre el que se registra. `id` es lo que viaja como `loteId` en el POST:
/// para **engorde** es `loteAveEngordeId`; para **reproductora** es el id de
/// `lote_reproductora_ave_engorde`, que es otra tabla — no son intercambiables.
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
    this.fechaEncaset,
    this.companyId,
    this.cerrado = false,
    this.loteAveEngordeId,
  });

  final int id;
  final String nombre;
  final String granja;
  final String galpon;
  final ModuloSeguimiento modulo;

  /// Edad en días desde el encasetamiento.
  final int dia;
  final int aves;
  final double? viabilidad;
  final String? raza;
  final int? anoTablaGenetica;
  final DateTime? fechaEncaset;
  final int? companyId;

  /// `estadoOperativoLote == 'Cerrado'`: el lote está liquidado y no admite
  /// registros nuevos. Se cachea igual, para poder consultarlo sin red.
  final bool cerrado;

  /// Sólo en reproductora: el lote de engorde del que cuelga. Informativo.
  final int? loteAveEngordeId;

  int get semana => (dia / 7).ceil();

  /// Días transcurridos hasta [hoy], recalculados desde la fecha de encasetamiento.
  /// El `dia` cacheado envejece: un lote guardado el lunes muestra la edad del lunes.
  int diaAl(DateTime hoy) {
    final f = fechaEncaset;
    if (f == null) return dia;
    final desde = DateTime(f.year, f.month, f.day);
    final hasta = DateTime(hoy.year, hoy.month, hoy.day);
    final d = hasta.difference(desde).inDays;
    return d < 0 ? 0 : d;
  }

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
    fechaEncaset: j['fechaEncaset'] == null ? null : DateTime.tryParse(j['fechaEncaset'] as String),
    companyId: j['companyId'] as int?,
    cerrado: (j['cerrado'] as bool?) ?? false,
    loteAveEngordeId: j['loteAveEngordeId'] as int?,
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

/// Un registro pendiente de sincronizar. Se persiste en SQLite y sobrevive al
/// cierre de la app, al token vencido y al reinicio del teléfono.
///
/// `duplicado` no es un fallo: el backend tiene un índice único por lote+día, así
/// que dos equipos sin red que registran el mismo día producen un 400 al segundo.
/// El día YA quedó guardado — se saca de la cola y se le informa al usuario, pero
/// no se reintenta ni se le muestra como error suyo.
enum EstadoSync { pending, syncing, synced, error, duplicado }

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
    this.endpoint,
    this.remoteId,
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

  /// Ruta relativa a la que se postea. Se guarda con la fila y no se deduce al
  /// enviar: si mañana cambia el mapeo módulo→endpoint, lo ya encolado tiene que
  /// seguir yendo a donde iba cuando el usuario lo registró.
  final String? endpoint;

  /// Id que devolvió el backend al aceptarlo.
  final int? remoteId;

  final int intentos;
  final String? ultimoError;
}
