/// Qué campos ve cada país. Es la regla que evita el `if (empresa == '...')`:
/// se decide por `paisId`, que es un dato de la BD.
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/perfil_pais.dart';

void main() {
  group('control de agua (pH, ORP, temperatura)', () {
    test('Ecuador y Panamá lo capturan', () {
      expect(PerfilPais.controlAgua(PaisId.ecuador), isTrue);
      expect(PerfilPais.controlAgua(PaisId.panama), isTrue);
    });

    test('Colombia no', () {
      expect(PerfilPais.controlAgua(PaisId.colombia), isFalse);
    });

    test('sin país resuelto se apaga: fail-closed', () {
      expect(PerfilPais.controlAgua(null), isFalse);
      expect(PerfilPais.controlAgua(99), isFalse);
    });
  });

  group('alimento en quintales', () {
    test('sólo Panamá', () {
      expect(PerfilPais.quintales(PaisId.panama), isTrue);
      expect(PerfilPais.quintales(PaisId.ecuador), isFalse);
      expect(PerfilPais.quintales(PaisId.colombia), isFalse);
    });

    test('sin país resuelto se apaga', () {
      expect(PerfilPais.quintales(null), isFalse);
    });
  });

  group('ids: son los de paises.pais_id, no un enum inventado', () {
    test('coinciden con la BD', () {
      expect(PaisId.colombia, 1);
      expect(PaisId.ecuador, 2);
      expect(PaisId.panama, 3);
    });
  });

  group('resolver el id desde el nombre que manda el backend', () {
    test('"Panama" viene sin tilde en la BD, pero se acepta con y sin', () {
      expect(PerfilPais.idDesdeNombre('Panama'), PaisId.panama);
      expect(PerfilPais.idDesdeNombre('Panamá'), PaisId.panama);
      expect(PerfilPais.idDesdeNombre('PANAMA'), PaisId.panama);
    });

    test('Ecuador y Colombia', () {
      expect(PerfilPais.idDesdeNombre('Ecuador'), PaisId.ecuador);
      expect(PerfilPais.idDesdeNombre('  colombia '), PaisId.colombia);
    });

    test('vacío o desconocido devuelve null, no un país por defecto', () {
      expect(PerfilPais.idDesdeNombre(null), isNull);
      expect(PerfilPais.idDesdeNombre(''), isNull);
      expect(PerfilPais.idDesdeNombre('Perú'), isNull);
    });
  });

  test('el nombre legible no inventa un país cuando no lo hay', () {
    expect(PerfilPais.nombre(PaisId.panama), 'Panamá');
    expect(PerfilPais.nombre(null), 'Sin país');
  });
}
