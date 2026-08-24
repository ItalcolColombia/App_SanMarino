/// Traslado de huevos: el contrato con el backend.
///
/// El campo que decide todo es `tipoDestino`. El Reporte Diario de Costos de
/// Postura (`fn_reporte_diario_costos_postura.sql:425`) arma su columna
/// `huevo_traslado_planta` con
///
///     WHEN th.tipo_operacion = 'Traslado' AND th.tipo_destino = 'Planta'
///
/// Si la app manda otra cosa, los huevos se descuentan del lote y **no aparecen
/// en el reporte contable**. No es un default estético: es la diferencia entre
/// que el movimiento exista para contabilidad o se pierda.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/api/traslados_api.dart';

void main() {
  final fecha = DateTime(2026, 8, 20);

  Map<String, dynamic> payload({Map<String, int>? cantidades, String? obs}) =>
      TrasladosApi.payload(
        lotePosturaProduccionId: 7,
        fecha: fecha,
        cantidades: cantidades ?? const {'cantidadLimpio': 100, 'cantidadSucio': 5},
        observaciones: obs,
      );

  group('el destino es lo que el contable cuenta', () {
    test("tipoDestino SIEMPRE es 'Planta'", () {
      expect(payload()['tipoDestino'], 'Planta');
    });

    test("tipoOperacion SIEMPRE es 'Traslado'", () {
      // 'Venta' es otro flujo, con motivo y descripción obligatorios.
      expect(payload()['tipoOperacion'], 'Traslado');
    });

    test('granja y lote destino van en null: la app no mueve entre granjas', () {
      // El flujo del backend es unilateral — nada acredita los huevos en el
      // destino. Mandar una granja destino los haría desaparecer del sistema.
      expect(payload()['granjaDestinoId'], isNull);
      expect(payload()['loteDestinoId'], isNull);
    });
  });

  group('las cantidades', () {
    test('manda las 11 categorías, con 0 en las que no se cargaron', () {
      final p = payload(cantidades: const {'cantidadLimpio': 10});

      for (final c in categoriasHuevo) {
        expect(p[c.clave], isNotNull, reason: '${c.clave} tiene que viajar');
      }
      expect(p['cantidadLimpio'], 10);
      expect(p['cantidadRoto'], 0);
    });

    test('el total es la suma de lo cargado', () {
      final p = payload(cantidades: const {
        'cantidadLimpio': 100,
        'cantidadTratado': 20,
        'cantidadRoto': 3,
      });

      expect(p['totalHuevos'], 123);
    });

    test('sin nada cargado el total es 0, no null', () {
      expect(payload(cantidades: const {})['totalHuevos'], 0);
    });
  });

  group('la fecha va a mediodía sin zona — invariante I15', () {
    test('mediodía, no medianoche', () {
      // Con medianoche local, en husos negativos el backend fecha el día
      // anterior y el traslado cae en otro día que el seguimiento.
      expect(payload()['fechaTraslado'], startsWith('2026-08-20T12:00'));
    });
  });

  group('observaciones', () {
    test('no viaja la clave si está vacía', () {
      expect(payload(obs: '   ').containsKey('observaciones'), isFalse);
      expect(payload().containsKey('observaciones'), isFalse);
    });

    test('viaja recortada si tiene contenido', () {
      expect(payload(obs: '  se rompió una bandeja  ')['observaciones'],
          'se rompió una bandeja');
    });
  });

  group('disponibilidad — traduce los nombres del backend', () {
    test('lee los tipos como los nombra el backend', () {
      // El backend nombra por TIPO (`Limpio`) y el payload por CANTIDAD
      // (`cantidadLimpio`): si la traducción falla, el operario ve 0 disponible
      // en todo y no puede mover nada.
      final d = DisponibilidadHuevos.desdeJson(const {
        'disponiblePorTipo': {'Limpio': 500, 'DobleYema': 12, 'Roto': 3},
      });

      expect(d.de('cantidadLimpio'), 500);
      expect(d.de('cantidadDobleYema'), 12);
      expect(d.de('cantidadRoto'), 3);
      expect(d.total, 515);
    });

    test('tolera la primera letra en minúscula', () {
      final d = DisponibilidadHuevos.desdeJson(const {
        'disponiblePorTipo': {'limpio': 40},
      });

      expect(d.de('cantidadLimpio'), 40);
    });

    test('una categoría ausente es 0, no revienta', () {
      final d = DisponibilidadHuevos.desdeJson(const {'disponiblePorTipo': {}});

      expect(d.de('cantidadLimpio'), 0);
      expect(d.vacio, isTrue);
    });

    test('un cuerpo inesperado no tumba la pantalla', () {
      final d = DisponibilidadHuevos.desdeJson(const {'otraCosa': 1});

      expect(d.total, 0);
    });
  });
}
