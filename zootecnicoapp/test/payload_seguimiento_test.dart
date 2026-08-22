/// El cuerpo que se le manda al backend. Lógica pura: sin red, sin SQLite.
///
/// Lo que se prueba acá es lo que en el web se rompe callado — un campo que se
/// llama distinto en el formulario y en el DTO llega como null y nadie se entera
/// hasta que alguien mira el reporte semanal.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/core/models.dart';

void main() {
  final fecha = DateTime(2026, 8, 21);

  Map<String, dynamic> engorde(Map<String, String> campos,
          {bool agua = false, bool qq = false}) =>
      PayloadSeguimiento.engorde(
        loteId: 12,
        fecha: fecha,
        campos: campos,
        controlAgua: agua,
        quintales: qq,
      );

  group('engorde', () {
    test('manda el lote y la fecha', () {
      final p = engorde({});
      expect(p['loteId'], 12);
      expect(p['fechaRegistro'], startsWith('2026-08-21T12:00'));
    });

    test('la fecha va a mediodía, no a medianoche', () {
      // Un `2026-08-21T00:00:00` interpretado en UTC cae al día anterior en
      // cualquier huso al oeste de Greenwich — que son todos los de la operación.
      expect(engorde({})['fechaRegistro'], contains('T12:00:00'));
    });

    test('un campo de mortalidad vacío es cero, no null', () {
      // Vacío significa "no hubo mortalidad", no "no se midió".
      final p = engorde({});
      expect(p['mortalidadHembras'], 0);
      expect(p['mortalidadMachos'], 0);
      expect(p['selH'], 0);
      expect(p['errorSexajeMachos'], 0);
    });

    test('un peso sin medir NO viaja como cero', () {
      // Un 0 kg entraría al promedio y arruinaría la curva de la semana.
      expect(engorde({}).containsKey('pesoPromH'), isFalse);
      expect(engorde({'pesoPromH': '2.4'})['pesoPromH'], 2.4);
    });

    test('acepta coma decimal: el teclado de Android da coma en es-CO', () {
      expect(engorde({'consumoKgHembras': '340,5'})['consumoKgHembras'], 340.5);
      expect(engorde({'consumoKgHembras': '340.5'})['consumoKgHembras'], 340.5);
    });

    test('un número inválido no se manda como 0', () {
      expect(engorde({'pesoPromH': 'abc'}).containsKey('pesoPromH'), isFalse);
    });

    test('las observaciones en blanco no viajan', () {
      expect(engorde({'observaciones': '   '}).containsKey('observaciones'), isFalse);
      expect(engorde({'observaciones': 'Se cayó el bebedero'})['observaciones'],
          'Se cayó el bebedero');
    });

    test('el usuario que registra viaja cuando se conoce', () {
      final p = PayloadSeguimiento.engorde(
        loteId: 1, fecha: fecha, campos: const {},
        controlAgua: false, quintales: false, usuarioId: 'guid-1');
      expect(p['createdByUserId'], 'guid-1');
    });
  });

  group('agua — sólo Ecuador y Panamá', () {
    test('con el control apagado no viaja ninguna clave de agua', () {
      final p = engorde({'consumoAguaDiario': '1500', 'consumoAguaPh': '7.2'});
      expect(p.keys.where((k) => k.startsWith('consumoAgua')), isEmpty);
    });

    test('con el control encendido viajan los cuatro campos medidos', () {
      final p = engorde(
        {'consumoAguaDiario': '1500.5', 'consumoAguaPh': '7.2', 'consumoAguaOrp': '650',
         'consumoAguaTemperatura': '25.5'},
        agua: true,
      );
      expect(p['consumoAguaDiario'], 1500.5);
      expect(p['consumoAguaPh'], 7.2);
      expect(p['consumoAguaOrp'], 650);
      expect(p['consumoAguaTemperatura'], 25.5);
    });

    test('encendido pero sin medir: no manda ceros', () {
      final p = engorde(const {}, agua: true);
      expect(p.containsKey('consumoAguaPh'), isFalse);
    });
  });

  group('quintales — sólo Panamá', () {
    test('apagado: no viajan', () {
      expect(engorde({'qqHembras': '10'}).containsKey('qqHembras'), isFalse);
    });

    test('encendido: viajan', () {
      final p = engorde({'qqMixtas': '5', 'qqHembras': '10.5', 'qqMachos': '2'}, qq: true);
      expect(p['qqMixtas'], 5);
      expect(p['qqHembras'], 10.5);
      expect(p['qqMachos'], 2);
    });
  });

  group('reproductora', () {
    test('el consumo va como escalar + unidad, no como consumoKg*', () {
      // El DTO de reproductora no tiene `consumoKgHembras`: mandarlo así
      // haría que el consumo se pierda sin ningún error visible.
      final p = PayloadSeguimiento.reproductora(
        loteId: 7, fecha: fecha,
        campos: const {'consumoKgHembras': '120', 'consumoKgMachos': '30'},
        controlAgua: false, quintales: false);

      expect(p['consumoHembras'], 120);
      expect(p['unidadConsumoHembras'], 'kg');
      expect(p['consumoMachos'], 30);
      expect(p['unidadConsumoMachos'], 'kg');
      expect(p.containsKey('consumoKgHembras'), isFalse);
    });

    test('usa el id del lote reproductora, no el del lote de engorde padre', () {
      final p = PayloadSeguimiento.reproductora(
        loteId: 7, fecha: fecha, campos: const {},
        controlAgua: false, quintales: false);
      expect(p['loteId'], 7);
    });

    test('comparte con engorde las claves de mortalidad', () {
      final p = PayloadSeguimiento.reproductora(
        loteId: 7, fecha: fecha,
        campos: const {'mortalidadHembras': '3', 'mortalidadMachos': '1'},
        controlAgua: false, quintales: false);
      expect(p['mortalidadHembras'], 3);
      expect(p['mortalidadMachos'], 1);
    });
  });

  group('mapeo módulo → endpoint', () {
    test('engorde postea al controller "Ecuador", que atiende a los 3 países', () {
      expect(endpointDeModulo[ModuloSeguimiento.engorde],
          '/SeguimientoAvesEngordeEcuador');
    });

    test('reproductora tiene su propio controller', () {
      expect(endpointDeModulo[ModuloSeguimiento.reproductora],
          '/SeguimientoDiarioLoteReproductora');
    });

    test('levante y producción todavía no se envían desde el móvil', () {
      expect(endpointDeModulo[ModuloSeguimiento.levante], isNull);
      expect(endpointDeModulo[ModuloSeguimiento.produccion], isNull);
    });
  });
}
