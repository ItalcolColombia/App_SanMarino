/// La pantalla de login se construye y valida antes de tocar la red.
///
/// Se monta [LoginScreen] sola y no [SanMarinoApp]: el shell abre SQLite y
/// escucha la conectividad en su `initState`, y ninguno de los dos plugins
/// existe en un test unitario. Probar la app entera acá exigiría `sqflite_ffi` y
/// un mock de conectividad para verificar algo que ya cubren los otros tests.
library;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/core/models.dart';
import 'package:zootecnicoapp/screens/login_screen.dart';

void main() {
  // No se le pega a ningún servidor: los casos de abajo cortan antes de la red.
  final auth = AuthApi(ApiClient());

  Widget montar({ValueChanged<Usuario>? onLogin, String? mensaje}) => MaterialApp(
        home: LoginScreen(
          onLogin: onLogin ?? (_) {},
          auth: auth,
          mensajeInicial: mensaje,
        ),
      );

  testWidgets('muestra los campos de acceso', (tester) async {
    await tester.pumpWidget(montar());

    expect(find.text('Correo electrónico'), findsOneWidget);
    expect(find.text('Contraseña'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
  });

  testWidgets('arranca con el correo vacío: no hay usuario de demo', (tester) async {
    await tester.pumpWidget(montar());

    final campo = tester.widget<TextField>(find.byType(TextField).first);
    expect(campo.controller?.text, isEmpty);
  });

  testWidgets('con los campos vacíos avisa y no llama al backend', (tester) async {
    var llamado = false;
    await tester.pumpWidget(montar(onLogin: (_) => llamado = true));

    // La pantalla scrollea: sin esto el botón puede quedar fuera del viewport
    // del test y el tap fallar por una razón que no tiene que ver con el login.
    await tester.ensureVisible(find.text('Entrar'));
    await tester.tap(find.text('Entrar'));
    await tester.pump();

    expect(find.text('Ingresa tu correo y contraseña.'), findsOneWidget);
    expect(llamado, isFalse);
  });

  testWidgets('muestra el motivo por el que se volvió al login', (tester) async {
    await tester.pumpWidget(montar(mensaje: 'Tu sesión venció. Ingresá de nuevo.'));

    expect(find.text('Tu sesión venció. Ingresá de nuevo.'), findsOneWidget);
  });
}
