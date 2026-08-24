/// Smoke del contrato con el backend, usando el MISMO código que la app.
///
/// No es un test unitario: pega contra un backend vivo. Es la única prueba que
/// demuestra que el cifrado de Dart y el de .NET son compatibles de verdad —
/// los vectores de `test/crypto_service_test.dart` sólo cuidan que eso no se
/// rompa después.
///
/// ```bash
/// dart run tool/smoke_backend.dart admin.ecuador@italcol.com 123456789
/// dart run tool/smoke_backend.dart admin.panama@italcol.com 123456789 --modulo=reproductora
/// ```
///
/// Los pasos 6 y 7 ESCRIBEN en la base: crean un seguimiento real y lo borran al
/// terminar, informando el id tocado. Se saltean con `--solo-lectura`.
library;

import 'dart:io';

import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/core/api/lotes_api.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/core/config/api_config.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/reglas/perfil_pais.dart';
import 'package:zootecnicoapp/core/session/sesion_actual.dart';

int _fallos = 0;

/// Módulo forzado por `--modulo=<engorde|reproductora>`.
ModuloSeguimiento? moduloPedido;

void _ok(String paso, [String detalle = '']) =>
    stdout.writeln('  OK   $paso${detalle.isEmpty ? '' : '  →  $detalle'}');

void _mal(String paso, Object motivo) {
  _fallos++;
  stdout.writeln('  FALLA $paso  →  $motivo');
}

Future<void> main(List<String> args) async {
  final soloLectura = args.contains('--solo-lectura');
  final positional = args.where((a) => !a.startsWith('--')).toList();
  if (positional.length < 2) {
    stderr.writeln('uso: dart run tool/smoke_backend.dart <email> <password> [--solo-lectura]');
    exit(64);
  }
  final email = positional[0];
  final password = positional[1];

  final flagModulo = args.firstWhere((a) => a.startsWith('--modulo='), orElse: () => '');
  if (flagModulo.isNotEmpty) {
    moduloPedido = ModuloSeguimiento.fromId(flagModulo.split('=').last);
    if (moduloPedido == null) {
      stderr.writeln('módulo desconocido: ${flagModulo.split('=').last}');
      exit(64);
    }
  }

  stdout.writeln('Backend: ${ApiConfig.baseUrl}\n');

  // El SessionStore real usa SQLite y SharedPreferences, que no existen fuera de
  // Flutter: acá se usa uno en memoria con la misma interfaz que lee el cliente.
  final sesion = SesionDeSmoke();
  final api = ApiClient(sesion: sesion);
  final auth = AuthApi(api);

  // ── 1. Login ───────────────────────────────────────────────────────────────
  Usuario? usuario;
  try {
    usuario = await auth.login(email: email, password: password);
    sesion.usuario = usuario;
    _ok('1. login cifrado',
        '${usuario.nombre} · ${usuario.companyName} (${usuario.companyId}) · '
        '${usuario.paisNombre} (${usuario.paisId})');
  } catch (e) {
    _mal('1. login cifrado', e);
    exit(1);
  }

  // ── 2. Credenciales incorrectas ────────────────────────────────────────────
  try {
    await auth.login(email: email, password: 'clave-que-no-es-$password');
    _mal('2. clave incorrecta', 'el backend aceptó una contraseña inválida');
  } on ApiError catch (e) {
    e.tipo == TipoFallo.sesionVencida
        ? _ok('2. clave incorrecta rechazada', '401')
        : _mal('2. clave incorrecta', 'se esperaba 401, llegó ${e.tipo} (${e.status})');
  }

  // ── 3. Sin SECRET_UP ───────────────────────────────────────────────────────
  // Se usa un cliente con el secreto roto: el backend debe devolver 401 CON la
  // cabecera X-Auth-Failure, que es lo que evita que la app cierre la sesión.
  final apiSinSecreto = ApiClient(sesion: sesion, secretUpTextoPlano: 'no-es-el-secreto');
  try {
    await apiSinSecreto.getRaw('/LoteAveEngorde');
    _mal('3. gate de plataforma', 'pasó una petición con SECRET_UP inválido');
  } on ApiError catch (e) {
    e.tipo == TipoFallo.plataformaRechazada
        ? _ok('3. SECRET_UP inválido → 401 tipificado', 'la sesión NO se cierra')
        : _mal('3. gate de plataforma',
            'se esperaba plataformaRechazada, llegó ${e.tipo} (${e.status})');
  }

  // ── 4. Módulos del menú ────────────────────────────────────────────────────
  var modulos = <ModuloSeguimiento>[];
  try {
    modulos = await auth.modulos(companyId: usuario.companyId);
    sesion.usuario = usuario.copyWith(modulos: modulos);
    _ok('4. menú descifrado', modulos.map((m) => m.label).join(', '));
    // `--modulo=X` sobre un módulo que el menú no habilita sirve para probar el
    // contrato del endpoint aunque ese usuario no lo tenga en su rol.
    final forzado = moduloPedido;
    if (forzado != null && !modulos.contains(forzado)) {
      modulos = [...modulos, forzado];
      stdout.writeln('         (${forzado.label} agregado a mano: no está en el menú)');
    }
  } catch (e) {
    _mal('4. menú descifrado', e);
  }

  // ── 5. Lotes ───────────────────────────────────────────────────────────────
  var lotes = <Lote>[];
  try {
    lotes = await LotesApi(api).descargar(modulos: modulos);
    final abiertos = lotes.where((l) => !l.cerrado).length;
    _ok('5. lotes descargados', '${lotes.length} lotes, $abiertos abiertos');
    for (final m in modulos) {
      final n = lotes.where((l) => l.modulo == m).length;
      stdout.writeln('         ${m.label}: $n');
    }
  } catch (e) {
    _mal('5. lotes descargados', e);
  }

  // ── 6 y 7. Escritura ───────────────────────────────────────────────────────
  if (soloLectura) {
    stdout.writeln('\n  (pasos 6 y 7 omitidos: --solo-lectura)');
  } else {
    await _probarEscritura(api, sesion, lotes, usuario);
  }

  stdout.writeln(_fallos == 0
      ? '\nTodo el contrato responde como se esperaba.'
      : '\n$_fallos paso(s) fallaron.');
  exit(_fallos == 0 ? 0 : 1);
}

/// Crea un seguimiento, comprueba que el duplicado se rechaza como tal y borra
/// lo que creó. Deja dicho qué id tocó, por si el borrado no llegara a correr.
Future<void> _probarEscritura(
  ApiClient api,
  SesionDeSmoke sesion,
  List<Lote> lotes,
  Usuario usuario,
) async {
  final seg = SeguimientosApi(api);

  // `--modulo=reproductora` fuerza el módulo a escribir; sin él se toma el
  // primer lote abierto que haya.
  final pedido = moduloPedido;
  final candidatos = lotes.where((l) =>
      !l.cerrado && (pedido == null || l.modulo == pedido));
  final lote = candidatos.firstOrNull;
  if (lote == null) {
    stdout.writeln('\n  (pasos 6 y 7 omitidos: no hay ningún lote abierto)');
    return;
  }

  // Fechas DENTRO de la vida del lote y sin registro previo. El backend rechaza
  // cualquier fecha anterior al encasetamiento (y, con el flag de hora de
  // llegada, al día siguiente), así que se arranca dos días después del encaset.
  final candidatas = <DateTime>[];
  var ocupadasCount = 0;
  try {
    final ocupadas = await seg.fechasRegistradas(lote);
    ocupadasCount = ocupadas.length;
    final desde = (lote.fechaEncaset ?? DateTime.now().subtract(const Duration(days: 30)))
        .add(const Duration(days: 2));
    for (var f = DateTime(desde.year, desde.month, desde.day);
        f.isBefore(DateTime.now()) && candidatas.length < 40;
        f = f.add(const Duration(days: 1))) {
      if (!ocupadas.contains(f)) candidatas.add(f);
    }
  } catch (e) {
    _mal('6. buscar una fecha libre', e);
    return;
  }
  if (candidatas.isEmpty) {
    stdout.writeln('  (pasos 6 y 7 omitidos: no se encontró una fecha libre; '
        '$ocupadasCount días ya registrados)');
    return;
  }
  stdout.writeln('         días ya registrados en el servidor: $ocupadasCount');

  // El backend rechaza por motivos que dependen del día y no del contrato — el
  // más común: producción no admite una fecha que ya tenga registro de levante
  // (si no, el ciclo sumaría dos veces el mismo alimento). Se prueban varias
  // fechas libres antes de dar el contrato por roto.
  final campos = {
    'mortalidadHembras': '1',
    // El alimento es obligatorio en los cuatro módulos (regla del 14ago26):
    // sin esto el backend rechaza el registro con un 400.
    'tipoAlimento': 'SMOKE',
    'consumoKgHembras': '1',
    'observaciones': 'SMOKE — borrar',
  };
  // Cada módulo arma su propio cuerpo: usar el de engorde para todos mandaba el
  // id de la etapa como `loteId` y el backend contestaba «Lote no existe».
  final agua = PerfilPais.controlAgua(usuario.paisId);
  final qq = PerfilPais.quintales(usuario.paisId);
  Map<String, dynamic> cuerpoPara(DateTime libre) => switch (lote.modulo) {
    ModuloSeguimiento.reproductora => PayloadSeguimiento.reproductora(
        loteId: lote.id, fecha: libre, campos: campos,
        controlAgua: agua, quintales: qq),
    ModuloSeguimiento.levante => PayloadSeguimiento.levante(
        loteId: lote.loteMaestroId ?? lote.id, lotePosturaLevanteId: lote.id,
        fecha: libre, campos: campos, controlAgua: agua, quintales: qq),
    ModuloSeguimiento.produccion => PayloadSeguimiento.produccion(
        lotePosturaProduccionId: lote.id, fecha: libre, campos: campos,
        controlAgua: agua, fechaEncaset: lote.fechaEncaset),
    ModuloSeguimiento.engorde => PayloadSeguimiento.engorde(
        loteId: lote.id, fecha: libre, campos: campos,
        controlAgua: agua, quintales: qq),
  };

  final endpoint = endpointDeModulo[lote.modulo]!;

  int? id;
  DateTime? usada;
  String? ultimoMotivo;
  var descartadas = 0;

  for (final f in candidatas) {
    try {
      id = await seg.enviar(endpoint: endpoint, payload: cuerpoPara(f));
      usada = f;
      break;
    } on ApiError catch (e) {
      if (e.tipo != TipoFallo.datosInvalidos) {
        _mal('6. seguimiento creado', e);
        return;
      }
      ultimoMotivo = e.mensaje;
      descartadas++;
    }
  }

  if (usada == null) {
    _mal('6. seguimiento creado',
        'ninguna de las ${candidatas.length} fechas libres fue aceptada. Último motivo: $ultimoMotivo');
    return;
  }

  final fechaTxt = '${usada.year}-${usada.month}-${usada.day}';
  _ok('6. seguimiento creado',
      'id=$id · lote ${lote.nombre} (${lote.modulo.label}) · $fechaTxt'
      '${descartadas > 0 ? ' (${descartadas} fecha(s) descartadas por el backend)' : ''}');

  // El mismo día otra vez. Hay DOS formas legítimas de que el backend lo pare y
  // las dos son correctas:
  //  · sin doble validación → llega al índice único y responde "ya existe";
  //  · con `requiere_validacion_seguimiento_diario` (Panamá) → una guarda previa
  //    corta porque el registro del paso 6 quedó sin validar.
  // Lo que NO puede pasar es que lo acepte. En ambos casos la app deja de
  // reintentar, que es lo que se está comprobando.
  try {
    await seg.enviar(endpoint: endpoint, payload: cuerpoPara(usada));
    _mal('7. segundo registro del mismo día',
        'el backend aceptó dos registros del mismo lote y día');
  } on ApiError catch (e) {
    switch (e.tipo) {
      case TipoFallo.duplicado:
        _ok('7. duplicado detectado', e.mensaje);
      case TipoFallo.datosInvalidos:
        _ok('7. rechazado por la doble validación', e.mensaje);
      default:
        _mal('7. segundo registro del mismo día',
            'se esperaba un rechazo, llegó ${e.tipo} (${e.status}): ${e.mensaje}');
    }
  }

  if (id == null) return;
  try {
    await api.deleteRaw('$endpoint/$id');
    _ok('8. registro de prueba borrado', 'id=$id');
  } catch (e) {
    _mal('8. borrar el registro de prueba',
        '$e — QUEDÓ EN LA BASE: $endpoint id=$id, lote ${lote.id}, $fechaTxt');
  }
}

/// Sesión en memoria: fuera de Flutter no hay sqflite ni SharedPreferences, así
/// que el [SessionStore] real no se puede instanciar acá.
class SesionDeSmoke implements SesionActual {
  @override
  Usuario? usuario;

  @override
  final String deviceId = 'smoke-dart';
}

extension _Primero<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
