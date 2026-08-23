/// Cliente HTTP contra el backend de ItalGranja.
///
/// Se ocupa de las tres cosas que el backend exige y que son fáciles de olvidar:
///  1. el header `X-Secret-Up` cifrado en **toda** ruta que no sea el login;
///  2. los headers de empresa/país activos, que el backend valida contra
///     `user_companies` antes de darle scope a la consulta;
///  3. distinguir los dos 401 que existen (ver [ApiError]).
library;

import 'dart:convert';

import 'package:dio/dio.dart';

import 'package:zootecnicoapp/core/config/api_config.dart';
import 'package:zootecnicoapp/core/crypto/crypto_service.dart';
import 'package:zootecnicoapp/core/session/sesion_actual.dart';

/// Por qué falló una petición. La app reacciona distinto a cada caso.
enum TipoFallo {
  /// No hubo respuesta: sin red, DNS caído, servidor apagado. Se reintenta.
  sinRed,

  /// El token venció o no sirve. Hay que volver a autenticarse — pero la cola
  /// de sincronización **no se borra**.
  sesionVencida,

  /// `PlatformSecretMiddleware` no reconoció el origen (`X-Auth-Failure:
  /// platform-secret`). El usuario y su token están perfectos: nunca se cierra
  /// la sesión por esto. Si se cerrara, rotar el secreto en el servidor borraría
  /// la cola de todos los dispositivos a la vez.
  plataformaRechazada,

  /// El backend rechazó el contenido (400/422). No se reintenta solo.
  datosInvalidos,

  /// Ya existe un registro para ese lote en esa fecha (índice único, 23505).
  duplicado,

  /// 403, 404, 5xx y demás.
  servidor,
}

class ApiError implements Exception {
  const ApiError(this.tipo, this.mensaje, {this.status});

  final TipoFallo tipo;
  final String mensaje;
  final int? status;

  /// Sólo estos dos justifican reintentar más tarde sin tocar nada.
  bool get esReintentable => tipo == TipoFallo.sinRed || tipo == TipoFallo.servidor;

  @override
  String toString() => mensaje;
}

class ApiClient {
  ApiClient({
    Dio? dio,
    CryptoService? crypto,
    SesionActual? sesion,
    String? secretUpTextoPlano,
  })  : _crypto = crypto ?? CryptoService(),
        _sesion = sesion ?? const SinSesion(),
        _secretUpTextoPlano = secretUpTextoPlano ?? ApiConfig.secretUp,
        _dio = dio ??
            Dio(BaseOptions(
              baseUrl: ApiConfig.baseUrl,
              connectTimeout: ApiConfig.connectTimeout,
              receiveTimeout: ApiConfig.receiveTimeout,
              // El backend responde el login como text/plain y los errores como
              // JSON: se procesa el cuerpo a mano según el caso.
              responseType: ResponseType.plain,
              // Los status de error los traduce [_traducir], no una excepción de Dio.
              validateStatus: (_) => true,
            )) {
    _dio.interceptors.add(InterceptorsWrapper(onRequest: (options, handler) {
      options.headers.addAll(_headers(esLogin: options.path.contains('/Auth/login')));
      handler.next(options);
    }));
  }

  final Dio _dio;
  final CryptoService _crypto;
  final SesionActual _sesion;

  /// El secreto de plataforma en claro, antes de cifrarlo. Sólo el smoke lo
  /// cambia, para comprobar que un secreto inválido devuelve 401 **con** la
  /// cabecera `X-Auth-Failure`: sin esa distinción, la app cerraría la sesión
  /// del usuario ante un problema de configuración del servidor y se llevaría
  /// por delante su cola pendiente.
  final String _secretUpTextoPlano;

  CryptoService get crypto => _crypto;

  /// El SECRET_UP cifrado cambia de IV en cada llamada, pero el texto plano es
  /// constante: se cifra una vez por proceso para no pagar PBKDF2 en cada request.
  String? _secretUpCache;
  String get _secretUp =>
      _secretUpCache ??= _crypto.cifrar(_secretUpTextoPlano, ApiConfig.secretUpKey);

  Map<String, String> _headers({bool esLogin = false}) {
    final h = <String, String>{
      'Content-Type': 'application/json',
      'X-Device-Id': _sesion.deviceId,
    };

    // El login está exento del gate de plataforma; el resto no.
    if (!esLogin) h['X-Secret-Up'] = _secretUp;

    final u = _sesion.usuario;
    if (u == null) return h;

    if (u.token.isNotEmpty) h['Authorization'] = 'Bearer ${u.token}';
    h['X-Active-Company'] = u.companyName;
    if (u.companyId != null) h['X-Active-Company-Id'] = '${u.companyId}';
    if (u.paisId != null) h['X-Active-Pais'] = '${u.paisId}';
    if (u.paisNombre.isNotEmpty) h['X-Active-Pais-Nombre'] = u.paisNombre;
    return h;
  }

  /// GET que devuelve el cuerpo crudo. El llamador decide si es JSON o base64 cifrado.
  Future<String> getRaw(String path, {Map<String, dynamic>? query}) async {
    final r = await _enviar(() => _dio.get(path, queryParameters: query));
    return r.data as String? ?? '';
  }

  Future<String> postRaw(String path, Object? body) async {
    final r = await _enviar(() => _dio.post(path, data: body));
    return r.data as String? ?? '';
  }

  Future<String> deleteRaw(String path) async {
    final r = await _enviar(() => _dio.delete(path));
    return r.data as String? ?? '';
  }


  Future<Response<dynamic>> _enviar(Future<Response<dynamic>> Function() peticion) async {
    final Response<dynamic> r;
    try {
      r = await peticion();
    } on DioException catch (e) {
      throw ApiError(TipoFallo.sinRed, _mensajeDeRed(e));
    }

    final status = r.statusCode ?? 0;
    if (status >= 200 && status < 300) return r;
    throw _traducir(r, status);
  }

  ApiError _traducir(Response<dynamic> r, int status) {
    final cuerpo = (r.data as String?) ?? '';
    final mensaje = _mensajeDelBackend(cuerpo) ?? 'El servidor respondió $status';

    if (status == 401) {
      // El backend tipifica el rechazo de origen con esta cabecera; sin ella,
      // un 401 sí significa que la sesión ya no vale.
      final marca = r.headers.value('X-Auth-Failure');
      if (marca == 'platform-secret') {
        return ApiError(TipoFallo.plataformaRechazada, mensaje, status: status);
      }
      return ApiError(TipoFallo.sesionVencida, mensaje, status: status);
    }

    // El duplicado se detecta por CONTENIDO, no por status. Engorde lo traduce a
    // un 400 legible, pero el controller de reproductora deja escapar la
    // violación del índice único como 500 con el `23505` crudo de Postgres. Si
    // eso se tomara por un fallo de servidor, la cola reintentaría para siempre
    // un día que ya está guardado.
    if (_esDuplicado(cuerpo, mensaje)) {
      return ApiError(TipoFallo.duplicado, _mensajeDuplicado, status: status);
    }
    if (status == 400 || status == 422) {
      return ApiError(TipoFallo.datosInvalidos, mensaje, status: status);
    }
    return ApiError(TipoFallo.servidor, mensaje, status: status);
  }

  /// Texto único para el duplicado: el del 500 de reproductora es el error crudo
  /// de Postgres, ilegible para quien está en un galpón.
  static const String _mensajeDuplicado =
      'Ya existe un registro de este lote en esa fecha.';

  /// Violación de "un registro por lote y día". Cada módulo la reporta a su
  /// manera y hay que reconocer las tres, porque de eso depende que la cola deje
  /// de reintentar un día que ya está guardado:
  ///  · engorde y reproductora → «Ya existe un registro …» (400 redactado);
  ///  · levante → «Ya existe un seguimiento manual para ese lote en esa fecha»;
  ///  · reproductora sin traducir → el `23505` crudo dentro de un 500.
  static bool _esDuplicado(String cuerpo, String mensaje) {
    final t = '$mensaje $cuerpo'.toLowerCase();
    return t.contains('ya existe un registro') ||
        t.contains('ya existe un seguimiento') ||
        t.contains('solo puede haber un registro') ||
        t.contains('23505') ||
        t.contains('duplicate key value');
  }

  /// Los errores llegan de tres formas y hay que leer las tres, porque de esto
  /// depende que el usuario vea el motivo y no un «El servidor respondió 400»:
  ///  · `{ "message": "..." }` — la mayoría de los controllers;
  ///  · `ValidationProblemDetails` con `title`/`detail`/`errors`;
  ///  · un **string JSON pelado** — así responde `SeguimientoLoteLevante`.
  static String? _mensajeDelBackend(String cuerpo) {
    if (cuerpo.trim().isEmpty) return null;
    try {
      final j = jsonDecode(cuerpo);
      if (j is String) return j.trim().isEmpty ? null : j;
      if (j is! Map) return null;
      final m = j['message'] ?? j['Message'] ?? j['title'] ?? j['detail'];
      if (m is String && m.trim().isNotEmpty) return m;

      final errores = j['errors'];
      if (errores is Map && errores.isNotEmpty) {
        final primero = errores.values.first;
        if (primero is List && primero.isNotEmpty) return '${primero.first}';
      }
    } catch (_) {
      // No era JSON: no hay mensaje que rescatar.
    }
    return null;
  }

  static String _mensajeDeRed(DioException e) => switch (e.type) {
        DioExceptionType.connectionTimeout ||
        DioExceptionType.sendTimeout ||
        DioExceptionType.receiveTimeout =>
          'El servidor no respondió a tiempo',
        DioExceptionType.connectionError => 'Sin conexión con el servidor',
        _ => 'No se pudo contactar al servidor',
      };
}
