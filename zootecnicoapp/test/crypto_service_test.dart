/// El cifrado tiene que ser byte a byte el del backend .NET.
///
/// Los vectores de abajo se produjeron con la receta declarada en
/// `EncryptionService.cs` (AES-256-CBC · PBKDF2-HMAC-SHA256 · 10.000 iteraciones
/// · salt `sanmarino-salt` · IV de 16 bytes antepuesto · base64) usando un IV de
/// ceros, que no es seguro pero sí reproducible: un IV aleatorio daría un
/// ciphertext distinto en cada corrida y el test no podría comparar nada.
///
/// La compatibilidad real con el backend la prueba el smoke de
/// `tool/smoke_backend.dart`, que cifra con ESTA clase y pega contra el servidor
/// vivo. Estos tests son la red que atrapa una regresión sin necesitar backend.
library;

import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/config/api_config.dart';
import 'package:zootecnicoapp/core/crypto/crypto_service.dart';

/// Random determinista: devuelve siempre 0, de modo que el IV sea 16 ceros.
class _RandomCero implements Random {
  const _RandomCero();
  @override
  bool nextBool() => false;
  @override
  double nextDouble() => 0;
  @override
  int nextInt(int max) => 0;
}

void main() {
  final crypto = CryptoService(random: const _RandomCero());

  group('derivación de llave (PBKDF2-HMAC-SHA256, 10000, sanmarino-salt)', () {
    test('la llave del frontend coincide con la del backend', () {
      expect(
        base64.encode(crypto.derivarLlave(ApiConfig.encKeyFrontend)),
        'rc1bWeiAdso/Zd5crzF59kQ3dkHZwdryOP8XuAbiP9w=',
      );
    });

    test('la llave del backend coincide', () {
      expect(
        base64.encode(crypto.derivarLlave(ApiConfig.encKeyBackend)),
        'ECKImQFCEXkBHSrGunarFupqkni8cJc7eDGPAfxRA5A=',
      );
    });

    test('la llave del SECRET_UP coincide', () {
      expect(
        base64.encode(crypto.derivarLlave(ApiConfig.secretUpKey)),
        '+eCXh2uA+6mPHiKC5Y0aAiT1rDLCPQDQZ9A8PiN3uB0=',
      );
    });

    test('siempre son 32 bytes: AES-256 no acepta otra cosa', () {
      expect(crypto.derivarLlave('cualquier cosa').length, 32);
    });
  });

  group('cifrado: lo que produce Dart es lo que espera .NET', () {
    test('el payload del login', () {
      const plano = '{"email":"admin.ecuador@italcol.com","password":"123456789"}';
      expect(
        crypto.cifrar(plano, ApiConfig.encKeyFrontend),
        'AAAAAAAAAAAAAAAAAAAAAAQ0VGIpOVE9u9plpMxJVFc+YAvqe61I3OxS1MNl5N2OJ9YheXEa3LYgKnbCX7VlXuZyvF14len3f39nRVfXTws=',
      );
    });

    test('el SECRET_UP que exige el middleware de plataforma', () {
      expect(
        crypto.cifrar(ApiConfig.secretUp, ApiConfig.secretUpKey),
        'AAAAAAAAAAAAAAAAAAAAALtrEQRSR4WkFTY3DEbpyAlWvpsF4J+5x4kHfcX4etq3',
      );
    });

    test('los acentos van en UTF-8, no en la codificación del sistema', () {
      expect(
        crypto.cifrar('Panamá · galpón ñ', ApiConfig.encKeyFrontend),
        'AAAAAAAAAAAAAAAAAAAAADRenFnOurpyscPJux+9nZ4dmf2Mra1gxZw33R8avwPR',
      );
    });
  });

  group('descifrado: lo que manda .NET se lee en Dart', () {
    test('una respuesta cifrada con la llave del backend', () {
      expect(
        crypto.descifrar(
          'AAAAAAAAAAAAAAAAAAAAAIB61FvBN1PLf/FC0mb2ZvPV6/hcYPhsfDMBrH6j9lFX1Z6w4S2elPleHxdsgw0QzQ==',
          ApiConfig.encKeyBackend,
        ),
        '{"userId":"35f70596","token":"abc"}',
      );
    });

    test('descifrarJson devuelve el mapa listo', () {
      final j = crypto.descifrarJson(
        'AAAAAAAAAAAAAAAAAAAAAIB61FvBN1PLf/FC0mb2ZvPV6/hcYPhsfDMBrH6j9lFX1Z6w4S2elPleHxdsgw0QzQ==',
        ApiConfig.encKeyBackend,
      );
      expect(j['userId'], '35f70596');
      expect(j['token'], 'abc');
    });
  });

  group('robustez', () {
    test('ida y vuelta con IV aleatorio real', () {
      final real = CryptoService();
      const texto = 'mortalidad 12 · consumo 340,5 kg · galpón G0040';
      expect(real.descifrar(real.cifrar(texto, 'k'), 'k'), texto);
    });

    test('dos cifrados del mismo texto NO son iguales: el IV es aleatorio', () {
      final real = CryptoService();
      expect(real.cifrar('igual', 'k'), isNot(real.cifrar('igual', 'k')));
    });

    test('un payload sin IV completo se rechaza en vez de devolver basura', () {
      expect(
        () => crypto.descifrar(base64.encode(Uint8List(10)), 'k'),
        throwsA(isA<FormatException>()),
      );
    });

    test('la llave equivocada falla, no devuelve texto silvestre', () {
      final real = CryptoService();
      final cifrado = real.cifrar('secreto', 'llave-correcta');
      expect(() => real.descifrar(cifrado, 'llave-incorrecta'), throwsA(anything));
    });
  });
}
