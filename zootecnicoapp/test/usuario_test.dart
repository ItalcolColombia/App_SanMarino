/// `Usuario.descuentaInventarioDesdeMovil` (F5.1): el kill switch de F5 tiene
/// que sobrevivir el viaje a SQLite (`SessionStore` lo persiste vía
/// `toJson`/`fromJson`) y quedar en `false` cuando falta — una sesión guardada
/// ANTES de este campo no lo trae, y eso NO puede leerse como "encendido".
library;

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/core/models.dart';

Usuario _usuario({bool descuentaInventarioDesdeMovil = false}) => Usuario(
      id: 'u1', nombre: 'Prueba', email: 'p@x.com', cargo: 'Admin', granja: '',
      paisId: 2, paisNombre: 'Ecuador', companyId: 3, companyName: 'ItalcolEcuador',
      token: 'tok', modulos: const [ModuloSeguimiento.engorde],
      descuentaInventarioDesdeMovil: descuentaInventarioDesdeMovil,
    );

void main() {
  group('descuentaInventarioDesdeMovil — round trip a JSON (sesión en SQLite)', () {
    test('true sobrevive el viaje', () {
      final j = _usuario(descuentaInventarioDesdeMovil: true).toJson();
      expect(Usuario.fromJson(j).descuentaInventarioDesdeMovil, isTrue);
    });

    test('false sobrevive el viaje', () {
      final j = _usuario(descuentaInventarioDesdeMovil: false).toJson();
      expect(Usuario.fromJson(j).descuentaInventarioDesdeMovil, isFalse);
    });

    test('fail-closed: una sesión guardada ANTES de este campo lee false, no revienta', () {
      final j = _usuario().toJson()..remove('descuentaInventarioDesdeMovil');
      expect(Usuario.fromJson(j).descuentaInventarioDesdeMovil, isFalse);
    });

    test('default del constructor es false', () {
      expect(_usuario().descuentaInventarioDesdeMovil, isFalse);
    });

    test('copyWith conserva el flag: refrescar módulos no lo apaga de rebote', () {
      final u = _usuario(descuentaInventarioDesdeMovil: true);
      final refrescado = u.copyWith(modulos: const [ModuloSeguimiento.reproductora]);
      expect(refrescado.descuentaInventarioDesdeMovil, isTrue);
    });
  });

  group('descuentaInventarioDesdeMovil — lo que manda el login (AuthResponseDto)', () {
    test('true en companyPaises[0] se lee', () {
      final u = AuthApi.usuarioDesdeRespuesta({
        'userId': 'u1', 'username': 'p@x.com', 'fullName': 'Prueba', 'token': 'tok',
        'companyPaises': [
          {'companyId': 3, 'companyName': 'ItalcolEcuador', 'paisId': 2, 'paisNombre': 'Ecuador',
            'descuentaInventarioDesdeMovil': true},
        ],
      });
      expect(u.descuentaInventarioDesdeMovil, isTrue);
    });

    test('fail-closed: ausente en companyPaises[0] es false, no revienta', () {
      final u = AuthApi.usuarioDesdeRespuesta({
        'userId': 'u1', 'username': 'p@x.com', 'fullName': 'Prueba', 'token': 'tok',
        'companyPaises': [
          {'companyId': 1, 'companyName': 'Sanmarino', 'paisId': 1, 'paisNombre': 'Colombia'},
        ],
      });
      expect(u.descuentaInventarioDesdeMovil, isFalse);
    });

    test('fail-closed: sin companyPaises es false, no revienta', () {
      final u = AuthApi.usuarioDesdeRespuesta({
        'userId': 'u1', 'username': 'p@x.com', 'fullName': 'Prueba', 'token': 'tok',
      });
      expect(u.descuentaInventarioDesdeMovil, isFalse);
    });
  });
}
