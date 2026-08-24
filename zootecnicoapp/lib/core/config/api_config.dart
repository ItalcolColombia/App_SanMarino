/// Configuración de la conexión con el backend.
///
/// Nada de esto se hardcodea en el binario de producción: todos los valores llegan
/// por `--dart-define` y los defaults son los del **backend local de desarrollo**
/// (`appsettings.Development.json`). Un build de release sin defines apunta a
/// localhost y falla el gate de plataforma — que es exactamente lo que queremos:
/// un APK mal construido no habla con producción por accidente.
///
/// ```bash
/// flutter run \
///   --dart-define=API_BASE_URL=https://api.italgranja.com/api \
///   --dart-define=ENC_KEY_FRONTEND=... \
///   --dart-define=ENC_KEY_BACKEND=... \
///   --dart-define=SECRET_UP=... \
///   --dart-define=SECRET_UP_KEY=...
/// ```
library;

class ApiConfig {
  const ApiConfig._();

  /// En emulador Android `localhost` es el propio emulador: el host se alcanza
  /// por `10.0.2.2`. En iOS/desktop/dispositivo físico en la misma red, poné la IP.
  static const String baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5002/api',
  );

  /// Llave con la que el cliente CIFRA lo que le manda al backend
  /// (`Encryption:RemitenteFrontend`).
  static const String encKeyFrontend = String.fromEnvironment(
    'ENC_KEY_FRONTEND',
    defaultValue: r'pR7@xW2!dN#9mZ$eH8&',
  );

  /// Llave con la que el cliente DESCIFRA lo que le responde el backend
  /// (`Encryption:RemitenteBackend`).
  static const String encKeyBackend = String.fromEnvironment(
    'ENC_KEY_BACKEND',
    defaultValue: r'Q5#vF1@pG*0bT$yK9!r',
  );

  /// Firma de plataforma que exige `PlatformSecretMiddleware` en toda ruta que no
  /// sea login.
  ///
  /// **Es la de la APP, no la del web** (`PlatformSecret:SecretUpMovil`). Hasta
  /// ago-2026 las dos compartían el mismo valor: el backend no podía distinguir
  /// de dónde venía la petición, y rotarla dejaba sin servicio al web y a la app
  /// a la vez. Con firma propia se puede revocar la app sola.
  ///
  /// El default es el de DESARROLLO. En producción se pasa por
  /// `--dart-define=SECRET_UP=…` con el valor que el backend tenga en
  /// `PlatformSecret__SecretUpMovil`. Si ese ambiente todavía no lo configuró, el
  /// middleware sigue aceptando la firma del front y nada se rompe.
  ///
  /// Cuando el backend la rechaza responde `X-Auth-Failure: platform-secret`, que
  /// la app trata como `plataformaRechazada`: avisa y **no** cierra sesión ni
  /// borra la cola (invariante I7).
  static const String secretUp = String.fromEnvironment(
    'SECRET_UP',
    defaultValue: r'Mov!ZhaerqG45JRiMCu7QBIC',
  );

  /// Llave con la que se cifra el secreto anterior (`PlatformSecret:EncryptionKey`).
  static const String secretUpKey = String.fromEnvironment(
    'SECRET_UP_KEY',
    defaultValue: r'EncKey#SANMARINO2024!xZ9',
  );

  /// Salt fijo de la derivación PBKDF2. Literal compartido con el backend
  /// (`EncryptionService.DeriveKey`): cambiarlo rompe la comunicación en ambos sentidos.
  static const String salt = 'sanmarino-salt';

  /// Iteraciones de PBKDF2 — también fijadas por el backend.
  static const int pbkdf2Iterations = 10000;

  static const Duration connectTimeout = Duration(seconds: 15);
  static const Duration receiveTimeout = Duration(seconds: 30);
}
