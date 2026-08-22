/// La cola de sincronización — la pieza donde un bug cuesta trabajo de campo.
///
/// Un registro que el operario anotó en el galpón y que la app pierde, o que
/// marca como cargado sin que el servidor lo tenga, no se recupera de ningún
/// lado. Por eso estos tests existen sobre SQLite de verdad (`sqflite_ffi`, en
/// memoria) y no sobre un doble: lo que se está probando es el SQL, no el Dart.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:zootecnicoapp/core/local_db.dart';
import 'package:zootecnicoapp/core/models.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  late LocalDb db;

  setUp(() => db = LocalDb.paraPruebas(inMemoryDatabasePath));
  tearDown(() => db.cerrar());

  Future<String> encolar({
    String tipo = 'engorde',
    int loteId = 1,
    DateTime? fecha,
  }) =>
      db.encolar(
        tipo: tipo,
        loteId: loteId,
        loteNombre: 'L$loteId',
        fecha: fecha ?? DateTime(2026, 8, 20),
        payload: const {'mortalidadHembras': 1},
        endpoint: '/SeguimientoAvesEngordeEcuador',
      );

  group('la cola guarda lo que el usuario anotó', () {
    test('una fila encolada queda pendiente y cuenta', () async {
      await encolar();
      expect(await db.contarPendientes(), 1);
      expect((await db.porEnviar()).single.estado, EstadoSync.pending);
    });

    test('guarda el endpoint con la fila, no lo deduce al enviar', () async {
      // Si mañana cambia el mapeo módulo→endpoint, lo ya encolado tiene que
      // seguir yendo a donde iba cuando el usuario lo registró.
      await encolar();
      expect((await db.porEnviar()).single.endpoint, '/SeguimientoAvesEngordeEcuador');
    });

    test('se envían en orden cronológico', () async {
      // El backend valida contra el saldo del lote: mandar el martes antes que
      // el lunes puede dar un rechazo que en orden no ocurriría.
      await encolar(loteId: 1, fecha: DateTime(2026, 8, 20));
      await encolar(loteId: 2, fecha: DateTime(2026, 8, 18));
      final cola = await db.porEnviar();
      expect(cola.map((r) => r.loteId), [1, 2]); // orden de creación, no de fecha
    });
  });

  group('reintentos: la cola no insiste para siempre', () {
    test('una fila en error vuelve a la cola mientras le queden intentos', () async {
      final id = await encolar();
      await db.marcarEstado(id, EstadoSync.error, error: 'x', sumarIntento: true);
      expect(await db.porEnviar(), hasLength(1));
    });

    test('agotados los intentos, sale de la cola pero NO se borra', () async {
      // El registro es del usuario: deja de reintentarse solo, pero sigue visible.
      final id = await encolar();
      for (var i = 0; i < LocalDb.maxIntentos; i++) {
        await db.marcarEstado(id, EstadoSync.error, error: 'sin stock', sumarIntento: true);
      }
      expect(await db.porEnviar(), isEmpty);
      expect(await db.agotadas(), hasLength(1));
      expect((await db.agotadas()).single.ultimoError, 'sin stock');
    });

    test('el contador de intentos sube de a uno', () async {
      // Antes se fijaba en 1, así que una fila que fallaba veinte veces figuraba
      // con un intento y nunca se agotaba.
      final id = await encolar();
      await db.marcarEstado(id, EstadoSync.error, sumarIntento: true);
      await db.marcarEstado(id, EstadoSync.error, sumarIntento: true);
      expect((await db.porEnviar()).single.intentos, 2);
    });

    test('reintentar la devuelve a la cola con el contador en cero', () async {
      final id = await encolar();
      for (var i = 0; i < LocalDb.maxIntentos; i++) {
        await db.marcarEstado(id, EstadoSync.error, sumarIntento: true);
      }
      await db.reintentar(id);
      expect(await db.agotadas(), isEmpty);
      expect((await db.porEnviar()).single.intentos, 0);
    });
  });

  group('la marca del día: lo que impide cargarlo dos veces', () {
    test('encolar NO marca por su cuenta: la marca la pone SyncService', () async {
      // La responsabilidad vive arriba a propósito. `LocalDb.encolar` guarda la
      // fila; `SyncService.encolar` además marca el día. Separarlas es lo que
      // permite encolar sin marcar (una reposición, un reintento).
      await encolar(fecha: DateTime(2026, 8, 20));
      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20)),
        isFalse,
      );
    });

    test('marcar el día lo deja registrado', () async {
      // Sin red es el único dato que existe para evitar el duplicado.
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20));
      expect(
        await db.yaHayRegistro(modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20)),
        isTrue,
      );
    });

    test('la marca es por módulo, lote y día — no se contagia', () async {
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20));

      expect(await db.yaHayRegistro(
          modulo: 'levante', loteId: 1, fecha: DateTime(2026, 8, 20)), isFalse);
      expect(await db.yaHayRegistro(
          modulo: 'engorde', loteId: 2, fecha: DateTime(2026, 8, 20)), isFalse);
      expect(await db.yaHayRegistro(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 21)), isFalse);
    });

    test('la hora no cambia la marca: se guarda el día', () async {
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20, 6, 30));
      expect(await db.yaHayRegistro(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20, 23, 59)), isTrue);
    });

    test('soltar la marca local deja el día libre otra vez', () async {
      // Es lo que corrige el bug: si el servidor rechaza el registro, el operario
      // veía el día como cargado y el servidor no lo tenía.
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20));
      await db.desmarcarRegistroLocal(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20));
      expect(await db.yaHayRegistro(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20)), isFalse);
    });

    test('soltar la local NO borra lo que confirmó el servidor', () async {
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20), origen: 'servidor');
      await db.desmarcarRegistroLocal(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20));
      expect(await db.yaHayRegistro(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20)), isTrue);
    });

    test('refrescar desde el servidor no pisa las marcas locales', () async {
      // El día que el operario acaba de anotar sin red tiene que sobrevivir a una
      // sincronización que trae la lista del servidor.
      await db.marcarRegistrado(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20));
      await db.reemplazarRegistrosDelServidor(
        modulo: 'engorde',
        loteId: 1,
        fechas: {DateTime(2026, 8, 1), DateTime(2026, 8, 2)},
      );
      expect(await db.yaHayRegistro(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 20)), isTrue);
      expect(await db.yaHayRegistro(
          modulo: 'engorde', loteId: 1, fecha: DateTime(2026, 8, 1)), isTrue);
    });
  });

  group('confirmación del servidor', () {
    test('confirmar mueve la fila al historial y la saca de la cola', () async {
      final id = await encolar();
      await db.confirmarEnviado(id, remoteId: 999);
      expect(await db.contarPendientes(), 0);
      expect(await db.porEnviar(), isEmpty);
    });

    test('confirmar dos veces no rompe', () async {
      final id = await encolar();
      await db.confirmarEnviado(id, remoteId: 1);
      await db.confirmarEnviado(id, remoteId: 1); // la fila ya no está
      expect(await db.contarPendientes(), 0);
    });
  });

  group('caché de lotes', () {
    test('engorde y reproductora pueden compartir el id sin pisarse', () async {
      // La PK es (modulo, id): el lote 12 de engorde y el 12 de reproductora son
      // lotes distintos. Con la PK vieja, uno borraba al otro.
      await db.guardarLotes(const [
        Lote(id: 12, nombre: 'E12', granja: 'G', galpon: 'A',
            modulo: ModuloSeguimiento.engorde, dia: 10, aves: 100),
        Lote(id: 12, nombre: 'R12', granja: 'G', galpon: 'A',
            modulo: ModuloSeguimiento.reproductora, dia: 10, aves: 50),
      ]);
      final lotes = await db.lotesCacheados();
      expect(lotes, hasLength(2));
      expect(lotes.map((l) => l.nombre).toSet(), {'E12', 'R12'});
    });

    test('guardar reemplaza la caché, no la duplica', () async {
      const l = Lote(id: 1, nombre: 'A', granja: 'G', galpon: 'A',
          modulo: ModuloSeguimiento.engorde, dia: 1, aves: 10);
      await db.guardarLotes(const [l]);
      await db.guardarLotes(const [l]);
      expect(await db.lotesCacheados(), hasLength(1));
    });

    test('conserva el lote maestro, que postura necesita para postear', () async {
      await db.guardarLotes(const [
        Lote(id: 6, nombre: 'A374A', granja: 'G', galpon: 'A',
            modulo: ModuloSeguimiento.levante, dia: 200, aves: 8000,
            loteMaestroId: 114),
      ]);
      expect((await db.lotesCacheados()).single.loteMaestroId, 114);
    });
  });

  group('sesión', () {
    test('se guarda y se lee entera', () async {
      await db.guardarSesion({'id': 'guid-1', 'companyId': 3, 'token': 'abc'});
      final s = await db.leerSesion();
      expect(s?['id'], 'guid-1');
      expect(s?['companyId'], 3);
    });

    test('guardar de nuevo reemplaza, no acumula', () async {
      await db.guardarSesion({'id': 'a'});
      await db.guardarSesion({'id': 'b'});
      expect((await db.leerSesion())?['id'], 'b');
    });

    test('cerrar sesión NO toca la cola: es trabajo del usuario', () async {
      await encolar();
      await db.guardarSesion({'id': 'a'});
      await db.borrarSesion();
      expect(await db.leerSesion(), isNull);
      expect(await db.contarPendientes(), 1);
    });
  });
}
