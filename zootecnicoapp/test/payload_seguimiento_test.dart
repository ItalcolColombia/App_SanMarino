/// El cuerpo que se le manda al backend. Lógica pura: sin red, sin SQLite.
///
/// Lo que se prueba acá es lo que en el web se rompe callado — un campo que se
/// llama distinto en el formulario y en el DTO llega como null y nadie se entera
/// hasta que alguien mira el reporte semanal.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/reglas/postura_calculos.dart';

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


  group('levante', () {
    Map<String, dynamic> levante(Map<String, String> campos, {int? lpl = 55}) =>
        PayloadSeguimiento.levante(
          loteId: 9, lotePosturaLevanteId: lpl, fecha: fecha, campos: campos,
          controlAgua: false, quintales: false);

    test('manda los DOS ids: el lote maestro y el de la etapa', () {
      // El backend los usa para cosas distintas; el web manda ambos.
      final p = levante(const {});
      expect(p['loteId'], 9);
      expect(p['lotePosturaLevanteId'], 55);
    });

    test('sin id de etapa la clave no viaja, en vez de ir en null', () {
      expect(levante(const {}, lpl: null).containsKey('lotePosturaLevanteId'), isFalse);
    });

    test('comparte el resto del contrato con engorde', () {
      final p = levante(const {'mortalidadHembras': '4', 'consumoKgHembras': '120'});
      expect(p['mortalidadHembras'], 4);
      expect(p['consumoKgHembras'], 120);
    });

    test('nunca manda los 11 campos de huevos de levante', () {
      // Ese tab (semana 14+, flag captura_huevos_en_levante) no existe en el móvil.
      final p = levante(const {'huevoLimpio': '10'});
      expect(p.keys.where((k) => k.startsWith('huevo')), isEmpty);
    });
  });

  group('producción', () {
    Map<String, dynamic> prod(Map<String, String> campos,
            {bool agua = false, DateTime? encaset}) =>
        PayloadSeguimiento.produccion(
          lotePosturaProduccionId: 31, fecha: fecha, campos: campos,
          controlAgua: agua, fechaEncaset: encaset);

    test('usa lotePosturaProduccionId, no loteId', () {
      final p = prod(const {});
      expect(p['lotePosturaProduccionId'], 31);
      expect(p.containsKey('loteId'), isFalse);
    });

    test('la mortalidad se llama distinto que en los otros tres módulos', () {
      final p = prod(const {'mortalidadHembras': '7', 'mortalidadMachos': '2'});
      expect(p['mortalidadH'], 7);
      expect(p['mortalidadM'], 2);
      expect(p.containsKey('mortalidadHembras'), isFalse);
    });

    test('el consumo va como escalar + unidad', () {
      final p = prod(const {'consumoKgHembras': '250,5'});
      expect(p['consumoH'], 250.5);
      expect(p['unidadConsumoH'], 'kg');
      expect(p.containsKey('consumoKgHembras'), isFalse);
    });

    test('los totales de huevos los calcula la app, no el usuario', () {
      final p = prod(const {
        'huevoLimpio': '800', 'huevoTratado': '100',
        'huevoSucio': '50', 'huevoRoto': '50',
      });
      expect(p['huevosIncubables'], 900);
      expect(p['huevosTotales'], 1000);
      // Y las 11 categorías viajan igual, para el desglose.
      expect(p['huevoLimpio'], 800);
      expect(p['huevoRoto'], 50);
    });

    test('un día sin huevos manda ceros, no omite las claves', () {
      // El request las declara obligatorias: omitirlas sería un 400.
      final p = prod(const {});
      expect(p['huevosTotales'], 0);
      expect(p['huevosIncubables'], 0);
      expect(p['huevoLimpio'], 0);
    });

    test('la etapa sale del encasetamiento, no del formulario', () {
      // 2026-08-21 con encaset 2026-01-01 ≈ semana 34 → etapa 2.
      final p = prod(const {}, encaset: DateTime(2026, 1, 1));
      expect(p['etapa'], 2);
      // Y no se deja pisar por lo que venga en los campos.
      final q = PayloadSeguimiento.produccion(
        lotePosturaProduccionId: 1, fecha: fecha, campos: const {'etapa': '3'},
        controlAgua: false, fechaEncaset: DateTime(2026, 1, 1));
      expect(q['etapa'], 2);
    });

    test('pesoHuevo va en 0 si no se midió: el request lo exige', () {
      expect(prod(const {})['pesoHuevo'], 0);
      expect(prod(const {'pesoHuevo': '62.5'})['pesoHuevo'], 62.5);
    });

    test('la uniformidad viaja en los dos juegos: global y por sexo', () {
      final p = prod(const {
        'uniformidad': '88', 'coeficienteVariacion': '7',
        'uniformidadHembras': '90', 'cvMachos': '6',
      });
      expect(p['uniformidad'], 88);
      expect(p['coeficienteVariacion'], 7);
      expect(p['uniformidadHembras'], 90);
      expect(p['cvMachos'], 6);
    });

    test('sin pesaje semanal esas claves no viajan', () {
      final p = prod(const {});
      expect(p.containsKey('pesoH'), isFalse);
      expect(p.containsKey('uniformidad'), isFalse);
    });

    test('producción no captura quintales ni en Panamá', () {
      // El request de producción no los declara; sólo engorde y reproductora.
      final p = prod(const {'qqHembras': '10'}, agua: true);
      expect(p.keys.where((k) => k.startsWith('qq')), isEmpty);
    });
  });

  group('las claves de huevo son las mismas en el cálculo y en la pantalla', () {
    test('son 11: 2 incubables + 9 no incubables', () {
      expect(huevosIncubables.length + huevosNoIncubables.length, 11);
    });

    test('ninguna se repite entre los dos grupos', () {
      final todas = {...huevosIncubables, ...huevosNoIncubables};
      expect(todas.length, 11);
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

    test('levante y producción tienen los suyos', () {
      expect(endpointDeModulo[ModuloSeguimiento.levante], '/SeguimientoLoteLevante');
      expect(endpointDeModulo[ModuloSeguimiento.produccion], '/Produccion/seguimiento');
    });

    test('los cuatro módulos tienen endpoint', () {
      for (final m in ModuloSeguimiento.values) {
        expect(endpointDeModulo[m], isNotNull, reason: 'falta ${m.label}');
      }
    });
  });

  group('el ciclo que elige el operario VIAJA', () {
    // El formulario pintaba el campo y el payload mandaba 'Normal' fijo: lo que
    // el operario elegia se descartaba en silencio. Ofrecer un campo que no
    // viaja es peor que no ofrecerlo.
    final fecha = DateTime(2026, 8, 20);

    test('levante manda lo tipeado', () {
      final p = PayloadSeguimiento.levante(
        loteId: 9, lotePosturaLevanteId: 55, fecha: fecha,
        campos: const {'ciclo': 'Segundo'},
        controlAgua: false, quintales: false);

      expect(p['ciclo'], 'Segundo');
    });

    test('produccion manda lo tipeado', () {
      final p = PayloadSeguimiento.produccion(
        lotePosturaProduccionId: 3, fecha: fecha,
        campos: const {'ciclo': 'Segundo'},
        controlAgua: false);

      expect(p['ciclo'], 'Segundo');
    });

    test('vacio cae a Normal, que es el default del negocio', () {
      final p = PayloadSeguimiento.levante(
        loteId: 9, lotePosturaLevanteId: 55, fecha: fecha,
        campos: const {'ciclo': '   '},
        controlAgua: false, quintales: false);

      expect(p['ciclo'], 'Normal');
    });

    test('ausente cae a Normal', () {
      final p = PayloadSeguimiento.levante(
        loteId: 9, lotePosturaLevanteId: 55, fecha: fecha,
        campos: const {},
        controlAgua: false, quintales: false);

      expect(p['ciclo'], 'Normal');
    });
  });
}
