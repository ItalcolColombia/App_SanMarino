/// Autenticación contra `/api/Auth`.
///
/// El login es el único endpoint que viaja cifrado en las dos direcciones, y el
/// menú viene cifrado en la respuesta. El resto de la API es JSON plano.
library;

import '../config/api_config.dart';
import '../models.dart';
import '../modulos_del_menu.dart';
import '../perfil_pais.dart';
import 'api_client.dart';

class AuthApi {
  AuthApi(this._api);

  final ApiClient _api;

  /// `POST /api/Auth/login`.
  ///
  /// El cuerpo es `{ "encryptedData": base64 }` y la respuesta llega como
  /// `text/plain` con otro base64 — no como JSON. Un 401 acá significa
  /// credenciales malas, no sesión vencida: es el único endpoint sin token.
  Future<Usuario> login({required String email, required String password}) async {
    final cifrado = _api.crypto.cifrarJson(
      {'email': email.trim(), 'password': password},
      ApiConfig.encKeyFrontend,
    );

    final respuesta = await _api.postRaw('/Auth/login', {'encryptedData': cifrado});
    final auth = _api.crypto.descifrarJson(respuesta, ApiConfig.encKeyBackend);
    return usuarioDesdeRespuesta(auth);
  }

  /// `GET /api/Auth/menu` — **respuesta cifrada**, igual que el login.
  ///
  /// De acá sale qué módulos ve el usuario. Un menú que no se puede leer
  /// devuelve lista vacía en vez de reventar: la app queda sin módulos y lo
  /// dice, que es preferible a un crash en la granja.
  Future<List<ModuloSeguimiento>> modulos({int? companyId}) async {
    final respuesta = await _api.getRaw(
      '/Auth/menu',
      query: companyId == null ? null : {'companyId': companyId},
    );

    final json = _api.crypto.descifrarJson(respuesta, ApiConfig.encKeyBackend);
    final crudo = (json['menu'] ?? json['Menu']) as List? ?? const [];
    final nodos = crudo
        .whereType<Map>()
        .map((n) => MenuNodo.fromJson(n.cast<String, dynamic>()))
        .toList();

    return modulosDelMenu(nodos);
  }

  /// `GET /api/Auth/ping` — prueba de vida autenticada. La usa el arranque para
  /// decidir si el token guardado sigue sirviendo antes de bajar nada.
  Future<bool> tokenSigueValido() async {
    try {
      await _api.getRaw('/Auth/ping');
      return true;
    } on ApiError catch (e) {
      // Sin red no se puede afirmar que el token murió: se lo da por bueno y la
      // app sigue en modo offline.
      if (e.tipo == TipoFallo.sinRed) return true;
      return e.tipo != TipoFallo.sesionVencida;
    }
  }

  /// Traduce `AuthResponseDto` al modelo de la app.
  ///
  /// La empresa y el país salen de `companyPaises[0]`, que es la combinación que
  /// el backend le reconoce al usuario. `empresas` es la lista legacy de nombres
  /// y no trae el id, así que sólo sirve de respaldo para el nombre.
  static Usuario usuarioDesdeRespuesta(Map<String, dynamic> j) {
    final empresas = (j['companyPaises'] as List?) ?? const [];
    final principal = empresas.isEmpty ? const <String, dynamic>{} : (empresas.first as Map).cast<String, dynamic>();

    final paisNombre = (principal['paisNombre'] as String?) ?? '';
    final companyName = (principal['companyName'] as String?) ??
        ((j['empresas'] as List?)?.cast<String>().firstOrNull ?? '');

    final roles = ((j['roles'] as List?) ?? const []).cast<String>();
    final nombre = ((j['fullName'] as String?) ?? '').trim();

    return Usuario(
      id: (j['userId'] as String?) ?? '',
      // `fullName` llega con doble espacio cuando el apellido está vacío.
      nombre: nombre.isEmpty ? (j['username'] as String? ?? '') : nombre.replaceAll(RegExp(r'\s+'), ' '),
      email: (j['username'] as String?) ?? '',
      cargo: roles.isEmpty ? '' : roles.first,
      granja: '',
      paisId: (principal['paisId'] as int?) ?? PerfilPais.idDesdeNombre(paisNombre),
      paisNombre: paisNombre,
      companyId: principal['companyId'] as int?,
      companyName: companyName,
      token: (j['token'] as String?) ?? '',
      // Los módulos llegan aparte: el `menu` del login viene vacío en varios
      // roles, mientras que `/Auth/menu` sí lo resuelve.
      modulos: const [],
      // Fail-closed (F5.1): ausente o `false` ⇒ el formulario sigue mandando el
      // escalar de hoy. Sale de `companyPaises[0].descuentaInventarioDesdeMovil`,
      // NUNCA de un `if (empresa == 'X')` — es la empresa activa la que decide.
      descuentaInventarioDesdeMovil: (principal['descuentaInventarioDesdeMovil'] as bool?) ?? false,
    );
  }
}

/// Utilitario local: el `firstOrNull` de collection sin sumar la dependencia.
extension _PrimeroONulo<T> on List<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
