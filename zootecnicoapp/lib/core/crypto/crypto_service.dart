/// Cifrado compatible con el backend .NET.
///
/// Espejo exacto de `ZooSanMarino.Infrastructure.Services.EncryptionService`:
/// si algún parámetro se desvía, el backend responde 401/400 sin decir por qué.
///
/// | Parámetro | Valor |
/// |---|---|
/// | Cifrado | AES-256-CBC, PKCS7 |
/// | Derivación | PBKDF2-HMAC-SHA256, 10.000 iteraciones, 32 bytes |
/// | Salt | `sanmarino-salt` (fijo) |
/// | IV | 16 bytes aleatorios, **antepuestos** al ciphertext |
/// | Salida | Base64 de `IV ‖ ciphertext` |
///
/// El backend serializa su respuesta en **camelCase** y acepta el request en
/// camelCase (`PropertyNameCaseInsensitive = true`), así que el JSON viaja tal
/// cual lo produce Dart.
library;

import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

import 'package:zootecnicoapp/core/config/api_config.dart';

class CryptoService {
  CryptoService({Random? random}) : _random = random ?? Random.secure();

  final Random _random;

  /// Cache de llaves derivadas. PBKDF2 con 10.000 iteraciones cuesta decenas de
  /// milisegundos: repetirlo en cada request haría notoria la latencia en un
  /// teléfono de gama baja, que es el equipo que hay en la granja.
  static final Map<String, Uint8List> _llavesDerivadas = {};

  /// Cifra un objeto como JSON. Es lo que espera `encryptedData` del login.
  String cifrarJson(Object valor, String llave) => cifrar(jsonEncode(valor), llave);

  /// Descifra y parsea un JSON producido por el backend.
  Map<String, dynamic> descifrarJson(String base64Texto, String llave) =>
      jsonDecode(descifrar(base64Texto, llave)) as Map<String, dynamic>;

  String cifrar(String textoPlano, String llave) {
    final iv = _ivAleatorio();
    final cifrador = PaddedBlockCipher('AES/CBC/PKCS7')
      ..init(
        true,
        PaddedBlockCipherParameters<CipherParameters, CipherParameters>(
          ParametersWithIV(KeyParameter(derivarLlave(llave)), iv),
          null,
        ),
      );

    final cifrado = cifrador.process(Uint8List.fromList(utf8.encode(textoPlano)));
    return base64.encode(Uint8List.fromList([...iv, ...cifrado]));
  }

  String descifrar(String base64Texto, String llave) {
    final bytes = base64.decode(base64Texto.trim());
    if (bytes.length <= 16) {
      throw const FormatException('Payload cifrado demasiado corto: falta el IV o el contenido');
    }

    final iv = Uint8List.sublistView(bytes, 0, 16);
    final cuerpo = Uint8List.sublistView(bytes, 16);

    final cifrador = PaddedBlockCipher('AES/CBC/PKCS7')
      ..init(
        false,
        PaddedBlockCipherParameters<CipherParameters, CipherParameters>(
          ParametersWithIV(KeyParameter(derivarLlave(llave)), iv),
          null,
        ),
      );

    return utf8.decode(cifrador.process(cuerpo));
  }

  /// PBKDF2-HMAC-SHA256 sobre el salt fijo. Público porque los tests comparan
  /// la llave derivada contra la que produce .NET.
  Uint8List derivarLlave(String llave) => _llavesDerivadas.putIfAbsent(llave, () {
        final derivador = PBKDF2KeyDerivator(HMac(SHA256Digest(), 64))
          ..init(Pbkdf2Parameters(
            Uint8List.fromList(utf8.encode(ApiConfig.salt)),
            ApiConfig.pbkdf2Iterations,
            32,
          ));
        return derivador.process(Uint8List.fromList(utf8.encode(llave)));
      });

  Uint8List _ivAleatorio() =>
      Uint8List.fromList(List<int>.generate(16, (_) => _random.nextInt(256)));
}
