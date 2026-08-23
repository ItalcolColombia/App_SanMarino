/// Punto único de entrada: `main.dart` llama a
/// `inicializarFactoryWebSiCorresponde()` sin preguntar la plataforma. El
/// import condicional resuelve a la versión que corresponde en tiempo de
/// compilación — en mobile/desktop es un no-op.
library;

export 'db_init_stub.dart' if (dart.library.js_interop) 'db_init_web.dart';
