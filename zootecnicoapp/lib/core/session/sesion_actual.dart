/// Lo mínimo que el cliente HTTP necesita saber de la sesión para armar los
/// headers de cada petición.
///
/// Existe como interfaz para que [ApiClient] no dependa de SQLite: el
/// [SessionStore] real la implementa sobre la base local, y el smoke de
/// `tool/smoke_backend.dart` la implementa en memoria, porque fuera de Flutter
/// no hay plugins de sqflite ni de SharedPreferences.
library;

import 'package:zootecnicoapp/core/models/models.dart';

/// Sesión vacía: sin usuario y con un id de equipo genérico.
///
/// Es el default de [ApiClient] para que el cliente no tenga que importar el
/// [SessionStore] real, que arrastra sqflite y SharedPreferences. Gracias a eso
/// toda la capa de red se puede correr con `dart run` (ver `tool/smoke_backend.dart`)
/// y montar en un test de widgets sin plugins.
class SinSesion implements SesionActual {
  const SinSesion();

  @override
  Usuario? get usuario => null;

  @override
  String get deviceId => 'sin-sesion';
}

abstract interface class SesionActual {
  /// Null mientras no haya login. Sin usuario no van ni el token ni la empresa.
  Usuario? get usuario;

  /// Identificador estable del equipo. No es una credencial: el backend jamás
  /// autoriza con esto.
  String get deviceId;
}
