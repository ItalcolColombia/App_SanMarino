/// `SyncService` — la máquina de estados que decide qué pasa con el trabajo que
/// el operario ya anotó.
///
/// Era la pieza con más riesgo offline de la app y la única sin una sola prueba.
/// Lo que se cubre acá no es "que el código corra": es que **ningún camino
/// pierda un registro de campo**. Cada fallo del backend tiene una respuesta
/// distinta y varias son contraintuitivas — un duplicado NO es un error, un
/// rechazo de plataforma NO cierra sesión, un 401 NO borra la cola. Si alguna de
/// esas se invierte, el síntoma aparece días después y en el galpón de otro.
///
/// Se corre contra **SQLite de verdad** (`sqflite_ffi`, en memoria), igual que
/// `cola_sync_test.dart`: el estado de la cola vive en SQL, y un doble de la BD
/// probaría el doble, no la app.
///
/// La conectividad se fuerza con `simularConexion` y **nunca** se llama a
/// `init()`: eso tocaría el plugin `connectivity_plus`, que fuera de un
/// dispositivo no existe. La traducción de interfaz→calidad se prueba aparte, en
/// `conectividad_test.dart`.
library;

import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/session/sesion_actual.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';

/// Backend de mentira: registra qué se le pidió y responde lo que le digan.
///
/// Hereda de [SeguimientosApi] y pisa `enviar`, así que el [ApiClient] que
/// recibe nunca se usa — no hay red en estos tests.
class _ApiFalso extends SeguimientosApi {
  _ApiFalso() : super(ApiClient(sesion: const SinSesion()));

  /// Todo lo que se posteó, en orden. Es lo que permite afirmar "se envió UNA
  /// vez" en vez de sólo mirar el estado final.
  final List<({String endpoint, Map<String, dynamic> payload})> llamadas = [];

  /// Respuesta por llamada, en orden. Un `ApiError` se lanza; un `int` es el
  /// id remoto. Cuando se agota, se usa [porDefecto].
  final List<Object?> guion = [];
  Object? porDefecto = 1;

  /// Para poder solapar dos corridas y probar la guarda de reentrada.
  Duration demora = Duration.zero;

  /// Se ejecuta al entrar a `enviar`, antes de responder. Sirve para provocar
  /// una condición de carrera desde adentro de la corrida.
  Future<void> Function()? alEnviar;

  /// Lo que el servidor dice tener ya registrado para el lote.
  Set<DateTime> fechasDelServidor = const {};
  int llamadasFechas = 0;
  ApiError? fallaFechas;

  @override
  Future<Set<DateTime>> fechasRegistradas(Lote lote) async {
    llamadasFechas++;
    if (fallaFechas != null) throw fallaFechas!;
    return fechasDelServidor;
  }

  @override
  Future<int?> enviar({
    required String endpoint,
    required Map<String, dynamic> payload,
  }) async {
    llamadas.add((endpoint: endpoint, payload: payload));
    if (alEnviar != null) await alEnviar!();
    if (demora > Duration.zero) await Future<void>.delayed(demora);

    final r = guion.isNotEmpty ? guion.removeAt(0) : porDefecto;
    if (r is ApiError) throw r;
    return r as int?;
  }
}

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  late LocalDb db;
  late _ApiFalso api;
  late SyncService sync;

  setUp(() {
    db = LocalDb.paraPruebas(inMemoryDatabasePath);
    api = _ApiFalso();
    // Las demoras del ribbon son tiempo de UX: en el test valen cero para que la
    // suite no espere 3,9 s por caso. El default de producción no se toca.
    sync = SyncService(
      db: db,
      api: api,
      demoraDeteccion: Duration.zero,
      demoraExito: Duration.zero,
    );
  });

  tearDown(() async {
    // Las subidas disparadas sin await pueden seguir en vuelo al terminar el
    // test; disponer el service mientras corren tira "used after disposed".
    await Future<void>.delayed(const Duration(milliseconds: 20));
    sync.dispose();
    await db.cerrar();
  });

  final fecha = DateTime.utc(2026, 8, 20);

  /// Encola **por la BD**, no por el service: así `_pendientes` sigue en 0 y
  /// `simularConexion` no dispara una sincronización que el test no pidió.
  Future<String> sembrar({
    String tipo = 'engorde',
    int loteId = 1,
    DateTime? cuando,
    String? endpoint = '/SeguimientoAvesEngordeEcuador',
  }) =>
      db.encolar(
        tipo: tipo,
        loteId: loteId,
        loteNombre: 'L$loteId',
        fecha: cuando ?? fecha,
        payload: {'mortalidadHembras': 1, 'lote': loteId},
        endpoint: endpoint,
      );

  void enLinea() => sync.simularConexion(CalidadConexion.wifiFuerte);

  ApiError error(TipoFallo tipo, [String mensaje = 'falló']) =>
      ApiError(tipo, mensaje);

  /// Espera a que se cumpla [condicion], o falla el test.
  ///
  /// Hace falta porque `encolar`, `reintentar` y la reconexión disparan la
  /// subida **sin esperarla**: el usuario ya vio su confirmación y la red no
  /// puede bloquear la captura. Es comportamiento buscado, así que el test se
  /// adapta a él en vez de dormir un rato fijo y cruzar los dedos.
  Future<void> esperarA(Future<bool> Function() condicion, String que) async {
    for (var i = 0; i < 200; i++) {
      if (await condicion()) return;
      await Future<void>.delayed(const Duration(milliseconds: 5));
    }
    fail('se agotó la espera: $que');
  }

  Future<void> colaVacia() =>
      esperarA(() async => await db.contarPendientes() == 0, 'la cola nunca se vació');

  // ══════════════════════════════════════════════════════════════════════════
  group('sin a dónde subir, la cola se queda quieta', () {
    test('sin conexión no se postea nada y la fila no se toca', () async {
      await sembrar();

      await sync.sincronizar();

      expect(api.llamadas, isEmpty);
      expect((await db.porEnviar()).single.estado, EstadoSync.pending,
          reason: 'sin red la fila espera; no es un error');
    });

    test('sin sesión (api null) la cola NO se altera — invariante I14', () async {
      final id = await sembrar();
      sync.api = null;
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas, isEmpty);
      final fila = (await db.porEnviar()).single;
      expect(fila.id, id);
      expect(fila.estado, EstadoSync.pending,
          reason: 'sin token la cola espera; perderla sería perder el trabajo');
      expect(sync.fase, FaseRibbon.oculto);
    });

    test('con la cola vacía no se postea ni se muestra ribbon', () async {
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas, isEmpty);
      expect(sync.fase, FaseRibbon.oculto);
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('camino feliz', () {
    test('sube la fila, la saca de la cola y anota el día como del servidor',
        () async {
      await sembrar();
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.single.endpoint, '/SeguimientoAvesEngordeEcuador');
      expect(await db.contarPendientes(), 0);
      expect(sync.enviados, 1);
      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: fecha),
        isTrue,
        reason: 'el día quedó tomado: no se puede volver a cargar',
      );
      expect(sync.fase, FaseRibbon.oculto, reason: 'el ribbon colapsa al final');
    });

    test('sube varias en el orden en que se anotaron — invariante I4', () async {
      // El backend valida contra el saldo del lote: mandarlas al revés produce
      // rechazos que no tienen nada que ver con lo que cargó el operario.
      // Se separan a proposito: created_at tiene precision de milisegundo y
      // dos filas creadas en el mismo ms empatan, dejando el orden indefinido.
      for (final id in [1, 2, 3]) {
        await sembrar(loteId: id, cuando: DateTime.utc(2026, 8, 17 + id));
        await Future<void>.delayed(const Duration(milliseconds: 5));
      }
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.map((l) => l.payload['lote']).toList(), [1, 2, 3],
          reason: 'se suben en el orden en que el operario los anoto');
      expect(sync.enviados, 3);
      expect(sync.totalLote, 3);
      expect(sync.progreso, 1.0);
      expect(await db.contarPendientes(), 0);
    });

    test('usa el endpoint congelado con la fila, no el mapa de hoy — I5',
        () async {
      // Si mañana cambia el mapeo de módulos, lo que el usuario ya registró
      // tiene que seguir yendo a donde iba cuando lo registró.
      await sembrar(tipo: 'engorde', endpoint: '/RutaVieja');
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.single.endpoint, '/RutaVieja');
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('los seis tipos de fallo tienen respuestas distintas', () {
    test('duplicado NO es error: sale de la cola como resuelto — I6', () async {
      // El día ya estaba en el servidor. El registro no se perdió: si esto se
      // tratara como error, la fila se reintentaría para siempre.
      await sembrar();
      api.guion.add(error(TipoFallo.duplicado, 'ya existe'));
      enLinea();

      await sync.sincronizar();

      expect(await db.contarPendientes(), 0, reason: 'sale de la cola');
      expect(sync.duplicados, 1);
      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: fecha),
        isTrue,
        reason: 'el día queda anotado: no se vuelve a ofrecer',
      );
    });

    test('duplicado no frena las filas que siguen', () async {
      await sembrar(loteId: 1);
      await sembrar(loteId: 2);
      api.guion.add(error(TipoFallo.duplicado));
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.length, 2, reason: 'siguió con la segunda');
      expect(sync.duplicados, 1);
      expect(await db.contarPendientes(), 0);
    });

    test('plataformaRechazada NO cierra la sesión — invariante I7', () async {
      // Rotar el secreto en el servidor borraría la cola de TODOS los equipos si
      // esto se tratara como sesión vencida.
      await sembrar();
      api.guion.add(error(TipoFallo.plataformaRechazada, 'origen no reconocido'));
      enLinea();

      await sync.sincronizar();

      expect(sync.requiereRelogin, isFalse,
          reason: 'el usuario y su token están bien: es el servidor');
      expect(sync.avisoPlataforma, 'origen no reconocido');
      expect((await db.porEnviar()).single.estado, EstadoSync.pending,
          reason: 'la fila vuelve a pendiente, lista para cuando se arregle');
    });

    test('sesión vencida para la cola SIN borrarla — invariante I8', () async {
      await sembrar();
      api.guion.add(error(TipoFallo.sesionVencida, 'token vencido'));
      enLinea();

      await sync.sincronizar();

      expect(sync.requiereRelogin, isTrue);
      expect(await db.contarPendientes(), 1,
          reason: 'la cola espera a que el usuario vuelva a entrar');
      expect((await db.porEnviar()).single.estado, EstadoSync.pending);
    });

    test('sin red deja la fila pendiente y para', () async {
      await sembrar();
      api.guion.add(error(TipoFallo.sinRed, 'sin respuesta'));
      enLinea();

      await sync.sincronizar();

      expect((await db.porEnviar()).single.estado, EstadoSync.pending);
      expect(sync.requiereRelogin, isFalse);
    });

    test('datosInvalidos deja el día LIBRE para volver a cargarlo — I9',
        () async {
      // El backend rechazó el contenido. Si el día siguiera marcado, el operario
      // vería "ya registrado" un día que el servidor nunca aceptó.
      await sembrar();
      await db.marcarRegistrado(modulo: 'engorde', loteId: 1, fecha: fecha);
      api.guion.add(error(TipoFallo.datosInvalidos, 'mortalidad negativa'));
      enLinea();

      await sync.sincronizar();

      expect(sync.rechazados, 1);
      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: fecha),
        isFalse,
        reason: 'se soltó la marca: el día se puede volver a cargar',
      );
      final fila = (await db.porEnviar()).single;
      expect(fila.estado, EstadoSync.error);
      expect(fila.ultimoError, 'mortalidad negativa',
          reason: 'el motivo del servidor tiene que quedar a la vista');
      expect(fila.intentos, 1);
    });

    test('datosInvalidos NO suelta una marca confirmada por el servidor — I9',
        () async {
      // La marca 'servidor' es verdad confirmada: soltarla dejaría que se
      // cargue de nuevo un día que el backend SÍ tiene.
      await sembrar();
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: fecha, origen: 'servidor');
      api.guion.add(error(TipoFallo.datosInvalidos));
      enLinea();

      await sync.sincronizar();

      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: fecha),
        isTrue,
      );
    });

    test('datosInvalidos no frena las filas que siguen', () async {
      await sembrar(loteId: 1);
      await sembrar(loteId: 2);
      api.guion.add(error(TipoFallo.datosInvalidos));
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.length, 2);
      expect(sync.rechazados, 1);
      expect(sync.enviados, 1);
    });

    test('error de servidor suma intento y para la cola', () async {
      await sembrar(loteId: 1);
      await sembrar(loteId: 2);
      api.guion.add(error(TipoFallo.servidor, 'error 500'));
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.length, 1,
          reason: 'para en seco: veinte errores idénticos gastan batería');
      // Por id de lote y no por posicion: las dos filas se crean en el mismo
      // milisegundo, asi que el orden entre ellas no esta garantizado.
      final fallada = (await db.porEnviar()).firstWhere((r) => r.loteId == 1);
      expect(fallada.estado, EstadoSync.error);
      expect(fallada.intentos, 1);
    });

    test('los intentos se acumulan entre corridas', () async {
      // Antes el contador se fijaba en 1 y una fila que fallaba veinte veces
      // seguía figurando con un intento.
      await sembrar();
      enLinea();

      for (var i = 0; i < 3; i++) {
        api.guion.add(error(TipoFallo.servidor));
        await sync.sincronizar();
      }

      expect((await db.porEnviar()).single.intentos, 3);
    });

    test('un fallo inesperado (no ApiError) no tumba la corrida', () async {
      await sembrar(loteId: 1);
      await sembrar(loteId: 2);
      api.guion.add(StateError('algo raro'));
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.length, 2, reason: 'siguió con la segunda');
      final fallada = (await db.porEnviar()).single;
      expect(fallada.estado, EstadoSync.error);
      expect(fallada.intentos, 1);
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('un módulo que esta versión no sabe enviar', () {
    test('queda marcado y visible, y la cola sigue', () async {
      // Una fila de un módulo futuro no puede desaparecer en silencio: el
      // operario tiene que poder verla en la pantalla de sincronización.
      await sembrar(tipo: 'movimiento-huevos', endpoint: null);
      await sembrar(loteId: 2);
      enLinea();

      await sync.sincronizar();

      expect(api.llamadas.length, 1, reason: 'sólo se envió la que sí sabe');
      final huerfana = (await db.porEnviar())
          .firstWhere((r) => r.tipo == 'movimiento-huevos');
      expect(huerfana.estado, EstadoSync.error);
      expect(huerfana.ultimoError, contains('movimiento-huevos'));
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('la guarda de reentrada', () {
    test('dos disparos a la vez postean la fila UNA sola vez', () async {
      // Sin la guarda, los dos toman el mismo snapshot de la cola y postean lo
      // mismo dos veces. Hoy lo salva el índice único del backend, no el cliente.
      await sembrar();
      enLinea();
      api.demora = const Duration(milliseconds: 30);

      await Future.wait([sync.sincronizar(), sync.sincronizar()]);

      expect(api.llamadas.length, 1);
      expect(await db.contarPendientes(), 0);
    });

    test('el pedido que llegó tarde no se pierde: se repite al terminar',
        () async {
      // El segundo disparo no se descarta, se anota. Lo que se encoló mientras
      // la primera corrida estaba en vuelo tiene que subir igual.
      await sembrar(loteId: 1);
      enLinea();

      var sembrada = false;
      api.alEnviar = () async {
        if (sembrada) return;
        sembrada = true;
        // Llega trabajo nuevo con la corrida en curso, y alguien pide sincronizar.
        await sembrar(loteId: 2);
        unawaited(sync.sincronizar());
      };

      await sync.sincronizar();

      expect(api.llamadas.length, 2,
          reason: 'la segunda corrida levantó la fila que entró tarde');
      expect(await db.contarPendientes(), 0);
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('encolar', () {
    test('marca el día en el mismo paso — invariante I10', () async {
      // Sin red, esta marca es lo ÚNICO que impide cargar el mismo día dos
      // veces. Todo encolador nuevo tiene que pasar por acá.
      await sync.encolar(
        tipo: 'engorde',
        loteId: 7,
        loteNombre: 'L7',
        fecha: fecha,
        payload: const {'x': 1},
        endpoint: '/SeguimientoAvesEngordeEcuador',
      );

      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 7, fecha: fecha),
        isTrue,
      );
      expect(sync.pendientes, 1);
    });

    test('sin red encola y NO intenta subir', () async {
      await sync.encolar(
        tipo: 'engorde',
        loteId: 7,
        loteNombre: 'L7',
        fecha: fecha,
        payload: const {'x': 1},
      );

      expect(api.llamadas, isEmpty);
      expect(sync.pendientes, 1, reason: 'la cola guarda el trabajo igual');
    });

    test('con autoSync apagado encola y espera', () async {
      enLinea();
      sync.autoSync = false;

      await sync.encolar(
        tipo: 'engorde',
        loteId: 7,
        loteNombre: 'L7',
        fecha: fecha,
        payload: const {'x': 1},
        endpoint: '/SeguimientoAvesEngordeEcuador',
      );

      expect(api.llamadas, isEmpty);
      expect(sync.pendientes, 1);
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('volver a entrar limpia el motivo de la parada', () {
    test('reanudar borra relogin y aviso de plataforma', () async {
      await sembrar();
      api.guion.add(error(TipoFallo.sesionVencida));
      enLinea();
      await sync.sincronizar();
      expect(sync.requiereRelogin, isTrue);

      sync.reanudar();

      expect(sync.requiereRelogin, isFalse);
      expect(sync.avisoPlataforma, isNull);
      expect(await db.contarPendientes(), 1,
          reason: 'reanudar no toca la cola, sólo el motivo');
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('filas agotadas', () {
    /// Deja una fila con `intentos >= maxIntentos`: la cola deja de reintentarla
    /// sola, pero **no** la borra (invariante I17).
    Future<String> agotarUna() async {
      final id = await sembrar();
      enLinea();
      for (var i = 0; i < 5; i++) {
        api.guion.add(error(TipoFallo.servidor));
        await sync.sincronizar();
      }
      return id;
    }

    test('a los 5 intentos sale de la cola pero NO se borra — I17', () async {
      await agotarUna();

      expect(await db.porEnviar(), isEmpty, reason: 'la cola ya no la reintenta');
      expect((await sync.agotadas()).length, 1, reason: 'pero sigue existiendo');
      expect(await db.contarPendientes(), 1,
          reason: 'el usuario la sigue viendo como pendiente de resolver');
    });

    test('reintentar la devuelve a la cola y la sube', () async {
      final id = await agotarUna();
      api.llamadas.clear();

      await sync.reintentar(id);
      await colaVacia();

      expect(await sync.agotadas(), isEmpty);
      expect(api.llamadas.length, 1, reason: 'reintentar dispara la subida');
      expect(await db.contarPendientes(), 0);
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('reconexión', () {
    test('volver la red con cola pendiente dispara la subida', () async {
      // Es el caso real: el operario registra en el galpón y vuelve a la oficina.
      await sync.encolar(
        tipo: 'engorde',
        loteId: 1,
        loteNombre: 'L1',
        fecha: fecha,
        payload: const {'x': 1},
        endpoint: '/SeguimientoAvesEngordeEcuador',
      );
      expect(api.llamadas, isEmpty, reason: 'todavía sin red');

      sync.simularConexion(CalidadConexion.wifiFuerte);
      await colaVacia();

      expect(api.llamadas.length, 1);
      expect(await db.contarPendientes(), 0);
    });

    test('volver la red sin cola no dispara nada', () async {
      sync.simularConexion(CalidadConexion.wifiFuerte);
      await Future<void>.delayed(const Duration(milliseconds: 60));

      expect(api.llamadas, isEmpty);
      expect(sync.fase, FaseRibbon.oculto);
    });

    test('con autoSync apagado la reconexión no sube nada', () async {
      await sync.encolar(
        tipo: 'engorde',
        loteId: 1,
        loteNombre: 'L1',
        fecha: fecha,
        payload: const {'x': 1},
        endpoint: '/SeguimientoAvesEngordeEcuador',
      );
      sync.autoSync = false;

      sync.simularConexion(CalidadConexion.wifiFuerte);
      await Future<void>.delayed(const Duration(milliseconds: 60));

      expect(api.llamadas, isEmpty);
      expect(sync.fase, FaseRibbon.oculto);
      expect(await db.contarPendientes(), 1);
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('los días que el servidor ya tiene', () {
    // Sin esto, `registros_conocidos` sólo sabe lo que registró ESTE equipo:
    // una tablet nueva deja llenar veinte campos para que el backend lo rechace
    // horas después.
    final lote = Lote(
      id: 1,
      nombre: 'L1',
      granja: 'G',
      galpon: 'g1',
      modulo: ModuloSeguimiento.engorde,
      dia: 10,
      aves: 100,
    );

    test('los baja y quedan en la caché local', () async {
      api.fechasDelServidor = {DateTime.utc(2026, 8, 19), DateTime.utc(2026, 8, 20)};
      enLinea();

      await sync.refrescarDiasDelServidor(lote);

      expect(api.llamadasFechas, 1);
      for (final d in [DateTime.utc(2026, 8, 19), DateTime.utc(2026, 8, 20)]) {
        expect(
          await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: d),
          isTrue,
          reason: 'el $d lo tiene el servidor: no se puede volver a cargar',
        );
      }
    });

    test('reemplaza lo que había: un día que el servidor ya no tiene se suelta',
        () async {
      api.fechasDelServidor = {DateTime.utc(2026, 8, 19)};
      enLinea();
      await sync.refrescarDiasDelServidor(lote);

      api.fechasDelServidor = {DateTime.utc(2026, 8, 20)};
      await sync.refrescarDiasDelServidor(lote);

      expect(
        await db.yaHayRegistro(
            modulo: 'engorde', loteId: 1, fecha: DateTime.utc(2026, 8, 19)),
        isFalse,
        reason: 'el servidor dejó de tenerlo: el día vuelve a estar libre',
      );
      expect(
        await db.yaHayRegistro(
            modulo: 'engorde', loteId: 1, fecha: DateTime.utc(2026, 8, 20)),
        isTrue,
      );
    });

    test('sin red no consulta y no rompe nada', () async {
      await sync.refrescarDiasDelServidor(lote);

      expect(api.llamadasFechas, 0, reason: 'no hay a quién preguntarle');
    });

    test('sin sesión no consulta', () async {
      sync.api = null;
      enLinea();

      await sync.refrescarDiasDelServidor(lote);

      expect(api.llamadasFechas, 0);
    });

    test('si el servidor falla, la caché queda intacta y no se propaga el error',
        () async {
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: fecha, origen: 'servidor');
      api.fallaFechas = error(TipoFallo.servidor, 'error 500');
      enLinea();

      // No debe lanzar: perder esta consulta no puede impedir registrar el día.
      await sync.refrescarDiasDelServidor(lote);

      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: fecha),
        isTrue,
        reason: 'lo que ya se sabía sigue estando',
      );
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  group('lo que ve el usuario', () {
    test('todoAlDia sólo cuando hay red, cola vacía y nada que mostrar',
        () async {
      expect(sync.todoAlDia, isFalse, reason: 'sin red no está "al día"');

      enLinea();
      expect(sync.todoAlDia, isTrue);

      await sync.encolar(
        tipo: 'engorde',
        loteId: 1,
        loteNombre: 'L1',
        fecha: fecha,
        payload: const {'x': 1},
        endpoint: '/SeguimientoAvesEngordeEcuador',
      );
      // La subida automática arranca sola; cuando termina vuelve a estar al día.
      await esperarA(() async => sync.pendientes == 0, 'la cola nunca se vació');
      expect(sync.todoAlDia, isTrue);
    });

    test('avisa a quien lo escucha cada vez que cambia algo', () async {
      var avisos = 0;
      sync.addListener(() => avisos++);

      await sembrar();
      enLinea();
      await sync.sincronizar();

      expect(avisos, greaterThan(0),
          reason: 'la UI se entera sola: es un ChangeNotifier');
    });
  });
}
