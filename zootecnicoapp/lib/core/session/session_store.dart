/// Sesión del usuario, persistida en SQLite.
///
/// Es lo que permite abrir la app en un galpón sin señal: si hubo un login
/// exitoso alguna vez, el usuario entra igual y trabaja contra la caché local.
/// El token guardado puede estar vencido — eso sólo impide sincronizar, nunca
/// registrar.
///
/// Se mantiene en memoria además de en disco porque el interceptor de HTTP la
/// lee en cada petición y no puede ser asíncrono.
library;

import 'package:shared_preferences/shared_preferences.dart';

import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/session/sesion_actual.dart';

class SessionStore implements SesionActual {
  SessionStore._();
  static final SessionStore instance = SessionStore._();

  Usuario? _usuario;
  DateTime? _ultimaSync;
  String _deviceId = '';

  @override
  Usuario? get usuario => _usuario;
  bool get haySesion => _usuario != null;

  /// Última vez que la app bajó lotes y catálogo del servidor.
  DateTime? get ultimaSync => _ultimaSync;

  /// Identificador estable del equipo. No es una credencial: el backend jamás
  /// autoriza con esto. Sirve para dos cosas que sí importan — saber qué
  /// dispositivo abrió cada sesión (para poder revocar la tablet perdida) y que
  /// el rate limit de `/api/sync/*` cuente por equipo y no por IP, así dos
  /// tablets tras el mismo NAT no se bloquean entre sí.
  @override
  String get deviceId => _deviceId;

  /// La sincronización diaria que pide la operación. No bloquea el registro:
  /// dejar a alguien sin poder anotar la mortalidad porque no hay señal sería
  /// peor que el dato desactualizado. La home lo muestra como aviso.
  bool get sincronizadoHoy {
    final u = _ultimaSync;
    if (u == null) return false;
    final hoy = DateTime.now();
    return u.year == hoy.year && u.month == hoy.month && u.day == hoy.day;
  }

  /// Carga la sesión del disco. Se llama una vez al arrancar, antes de la UI.
  Future<void> cargar() async {
    _deviceId = await _resolverDeviceId();
    final guardada = await LocalDb.instance.leerSesion();
    if (guardada == null) return;

    _usuario = Usuario.fromJson(guardada);
    final iso = guardada['ultimaSync'] as String?;
    _ultimaSync = iso == null ? null : DateTime.tryParse(iso);
  }

  Future<void> guardar(Usuario u) async {
    _usuario = u;
    await LocalDb.instance.guardarSesion({
      ...u.toJson(),
      'ultimaSync': _ultimaSync?.toIso8601String(),
    });
  }

  Future<void> marcarSincronizado([DateTime? cuando]) async {
    _ultimaSync = cuando ?? DateTime.now();
    final u = _usuario;
    if (u != null) await guardar(u);
  }

  /// Cierra sesión. **No toca `pending_sync`**: los registros que el usuario ya
  /// anotó son suyos y tienen que sobrevivir a un cierre de sesión, sea
  /// voluntario o por token vencido.
  Future<void> cerrar() async {
    _usuario = null;
    _ultimaSync = null;
    await LocalDb.instance.borrarSesion();
  }

  /// El id del equipo vive en `SharedPreferences` y no en la BD: así sobrevive a
  /// un `onUpgrade` fallido de SQLite y sigue identificando al mismo teléfono.
  Future<String> _resolverDeviceId() async {
    final prefs = await SharedPreferences.getInstance();
    final guardado = prefs.getString('device_id');
    if (guardado != null && guardado.isNotEmpty) return guardado;

    final nuevo = 'mob-${DateTime.now().microsecondsSinceEpoch.toRadixString(36)}';
    await prefs.setString('device_id', nuevo);
    return nuevo;
  }
}
