/// Ingreso a la app.
///
/// El bloque de marca sale de `design_system/components/marca.dart`, que replica
/// el logo-stack del login web. Antes había un `_LogoBlock` local que usaba un
/// logo que la web no usa; se eliminó.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/components/marca.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/auth/pages/recovery_page.dart';


class LoginPage extends StatefulWidget {
  const LoginPage({
    super.key,
    required this.onLogin,
    required this.auth,
    this.mensajeInicial,
  });

  /// Recibe el usuario ya autenticado. El shell se encarga de bajar módulos y lotes.
  final ValueChanged<Usuario> onLogin;
  final AuthApi auth;

  /// Motivo por el que se volvió a esta pantalla (p. ej. el token venció con cola
  /// pendiente). Se muestra arriba para que el usuario sepa qué pasó.
  final String? mensajeInicial;

  @override
  State<LoginPage> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginPage> {
  final _email = TextEditingController();
  final _pass = TextEditingController();
  bool _verPass = false;
  bool _recordarme = true;
  bool _cargando = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _error = widget.mensajeInicial;
  }

  @override
  void dispose() { _email.dispose(); _pass.dispose(); super.dispose(); }

  Future<void> _entrar() async {
    if (_email.text.isEmpty || _pass.text.isEmpty) {
      setState(() => _error = 'Ingresa tu correo y contraseña.');
      return;
    }
    setState(() { _cargando = true; _error = null; });

    try {
      final usuario = await widget.auth.login(email: _email.text, password: _pass.text);
      if (!mounted) return;

      // Sin empresa-país resuelta, el backend no le da scope a ninguna consulta:
      // dejarlo entrar sería mostrarle una app vacía sin explicarle por qué.
      if (!usuario.puedeRegistrar) {
        setState(() {
          _cargando = false;
          _error = 'Tu usuario no tiene una empresa asignada. Pedí que te la '
              'asignen desde la web para poder registrar.';
        });
        return;
      }

      widget.onLogin(usuario);
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _cargando = false;
        _error = switch (e.tipo) {
          // Es el único endpoint sin token: acá un 401 son credenciales, no sesión.
          TipoFallo.sesionVencida => 'Correo o contraseña incorrectos.',
          TipoFallo.sinRed =>
            'Sin conexión con el servidor. La primera vez necesitás red para entrar.',
          _ => e.mensaje,
        };
      });
    } catch (_) {
      if (!mounted) return;
      // Un fallo al descifrar significa llaves mal configuradas en el build.
      setState(() {
        _cargando = false;
        _error = 'No se pudo leer la respuesta del servidor. '
            'Revisá la configuración de la app.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.cream,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s6, vertical: AppSpacing.s7),
            child: Column(children: [
              const LogoMarca(),
              const SizedBox(height: AppSpacing.s7),

              if (_error != null) ...[
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                  decoration: BoxDecoration(
                    color: AppColors.dangerBg, borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                  child: Text(_error!, style: const TextStyle(
                    fontFamily: 'Inter', fontSize: 13, color: Color(0xFF9A4035),
                  )),
                ),
                const SizedBox(height: AppSpacing.s3),
              ],

              AppField(label: 'Correo electrónico', controller: _email, required: true,
                placeholder: 'usuario@empresa.com', keyboardType: TextInputType.emailAddress),
              const SizedBox(height: AppSpacing.s3),

              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const Row(children: [
                  Text('Contraseña', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.ink700,
                  )),
                  Text(' *', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.brand500,
                  )),
                ]),
                const SizedBox(height: 6),
                TextField(
                  controller: _pass,
                  obscureText: !_verPass,
                  style: const TextStyle(fontFamily: 'Inter', fontSize: 15, color: AppColors.ink900),
                  decoration: InputDecoration(
                    hintText: '••••••••',
                    suffixIcon: IconButton(
                      onPressed: () => setState(() => _verPass = !_verPass),
                      icon: Icon(_verPass ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                        size: 19, color: AppColors.ink500),
                    ),
                  ),
                ),
              ]),
              const SizedBox(height: AppSpacing.s3),

              Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                Row(children: [
                  SizedBox(width: 22, height: 22, child: Checkbox(
                    value: _recordarme,
                    onChanged: (v) => setState(() => _recordarme = v ?? false),
                  )),
                  const SizedBox(width: AppSpacing.s2),
                  const Text('Recordarme', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 13, color: AppColors.ink700,
                  )),
                ]),
                TextButton(
                  onPressed: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const RecoveryPage()),
                  ),
                  child: const Text('¿Olvidaste tu contraseña?'),
                ),
              ]),
              const SizedBox(height: AppSpacing.s4),

              AppButton(label: 'Entrar', size: AppButtonSize.lg, full: true,
                loading: _cargando, onPressed: _cargando ? null : _entrar),
              const SizedBox(height: AppSpacing.s4),

              const Text('¿Necesitas acceso? Contacta a tu administrador',
                textAlign: TextAlign.center,
                style: TextStyle(fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)),

              const SizedBox(height: AppSpacing.s8),
              Opacity(opacity: 0.6, child: Image.asset(
                'assets/images/brand/italcol-naranja.png', height: 26)),
            ]),
          ),
        ),
      ),
    );
  }
}

