/// Pantallas de autenticación: login y recuperación de contraseña.
/// El backend envía por correo una nueva contraseña (igual que el web).
library;

import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import '../widgets/app_widgets.dart';
import '../core/api/api_client.dart';
import '../core/api/auth_api.dart';
import '../core/models.dart';

class _LogoBlock extends StatelessWidget {
  const _LogoBlock();

  @override
  Widget build(BuildContext context) {
    return Column(children: [
      Container(
        width: 80, height: 80,
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.xl),
          border: Border.all(color: AppColors.line),
          boxShadow: AppColors.shadowMd,
        ),
        padding: const EdgeInsets.all(10),
        child: Image.asset('assets/images/brand/icono-logo.png', fit: BoxFit.contain),
      ),
      const SizedBox(height: AppSpacing.s4),
      RichText(
        textAlign: TextAlign.center,
        text: const TextSpan(
          style: TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 24, fontWeight: FontWeight.w800,
            letterSpacing: -0.5, color: AppColors.ink900,
          ),
          children: [
            TextSpan(text: 'San Marino '),
            TextSpan(text: 'Zootécnico', style: TextStyle(color: AppColors.orange500)),
          ],
        ),
      ),
      const SizedBox(height: 4),
      const Text('Genética avícola · Italfoods', style: TextStyle(
        fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
      )),
    ]);
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Login
// ═══════════════════════════════════════════════════════════════════════════

class LoginScreen extends StatefulWidget {
  const LoginScreen({
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
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
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
              const _LogoBlock(),
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
                    fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.orange500,
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
                    MaterialPageRoute(builder: (_) => const RecoveryScreen()),
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

// ═══════════════════════════════════════════════════════════════════════════
// Recuperar contraseña
// ═══════════════════════════════════════════════════════════════════════════

class RecoveryScreen extends StatefulWidget {
  const RecoveryScreen({super.key});

  @override
  State<RecoveryScreen> createState() => _RecoveryScreenState();
}

class _RecoveryScreenState extends State<RecoveryScreen> {
  final _email = TextEditingController();
  bool _cargando = false;
  bool _enviado = false;
  String? _error;

  @override
  void dispose() { _email.dispose(); super.dispose(); }

  Future<void> _enviar() async {
    if (!_email.text.contains('@')) {
      setState(() => _error = 'Ingresa un correo válido.');
      return;
    }
    setState(() { _cargando = true; _error = null; });
    await Future.delayed(const Duration(milliseconds: 1200));
    // TODO: AuthService.recuperarPassword(_email.text)
    if (mounted) setState(() { _cargando = false; _enviado = true; });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.cream,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s6, vertical: AppSpacing.s7),
            child: _enviado ? _exito() : _formulario(),
          ),
        ),
      ),
    );
  }

  Widget _formulario() => Column(children: [
    const _LogoBlock(),
    const SizedBox(height: AppSpacing.s7),
    Container(
      width: 56, height: 56,
      decoration: BoxDecoration(
        color: AppColors.orange50, borderRadius: BorderRadius.circular(18),
      ),
      child: const Icon(Icons.vpn_key_outlined, size: 26, color: AppColors.orange500),
    ),
    const SizedBox(height: AppSpacing.s3),
    const Text('Recuperar contraseña', style: TextStyle(
      fontFamily: 'PlusJakartaSans', fontSize: 20, fontWeight: FontWeight.w800,
      letterSpacing: -0.4, color: AppColors.ink900,
    )),
    const SizedBox(height: 4),
    const Text('Ingresa tu correo y te enviaremos una nueva contraseña.',
      textAlign: TextAlign.center,
      style: TextStyle(fontFamily: 'Inter', fontSize: 13, height: 1.5, color: AppColors.ink500)),
    const SizedBox(height: AppSpacing.s5),

    if (_error != null) ...[
      Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        decoration: BoxDecoration(color: AppColors.dangerBg, borderRadius: BorderRadius.circular(AppRadius.sm)),
        child: Text(_error!, style: const TextStyle(
          fontFamily: 'Inter', fontSize: 13, color: Color(0xFF9A4035),
        )),
      ),
      const SizedBox(height: AppSpacing.s3),
    ],

    AppField(label: 'Correo electrónico', controller: _email, required: true,
      placeholder: 'usuario@empresa.com', keyboardType: TextInputType.emailAddress),
    const SizedBox(height: AppSpacing.s4),

    AppButton(label: 'Enviar nueva contraseña', size: AppButtonSize.lg, full: true,
      loading: _cargando, onPressed: _cargando ? null : _enviar),
    const SizedBox(height: AppSpacing.s3),

    TextButton(
      onPressed: () => Navigator.of(context).pop(),
      style: TextButton.styleFrom(foregroundColor: AppColors.green600),
      child: const Text('← Volver al login'),
    ),
  ]);

  Widget _exito() => Column(children: [
    const _LogoBlock(),
    const SizedBox(height: AppSpacing.s7),
    Container(
      width: 72, height: 72,
      decoration: BoxDecoration(
        color: AppColors.successBg, borderRadius: BorderRadius.circular(AppRadius.xl),
      ),
      child: const Icon(Icons.check_rounded, size: 34, color: AppColors.green500),
    ),
    const SizedBox(height: AppSpacing.s4),
    const Text('¡Revisa tu correo!', style: TextStyle(
      fontFamily: 'PlusJakartaSans', fontSize: 22, fontWeight: FontWeight.w800,
      letterSpacing: -0.4, color: AppColors.ink900,
    )),
    const SizedBox(height: AppSpacing.s2),
    Text.rich(
      textAlign: TextAlign.center,
      TextSpan(
        style: const TextStyle(fontFamily: 'Inter', fontSize: 14, height: 1.6, color: AppColors.ink500),
        children: [
          const TextSpan(text: 'Enviamos una nueva contraseña a '),
          TextSpan(text: _email.text, style: const TextStyle(
            fontWeight: FontWeight.w700, color: AppColors.ink900,
          )),
          const TextSpan(text: '.\nRevisa también la carpeta de spam.'),
        ],
      ),
    ),
    const SizedBox(height: AppSpacing.s5),
    AppButton(label: 'Ir al login', size: AppButtonSize.lg, full: true,
      onPressed: () => Navigator.of(context).pop()),
    const SizedBox(height: AppSpacing.s2),
    TextButton(
      onPressed: () => setState(() => _enviado = false),
      style: TextButton.styleFrom(foregroundColor: AppColors.ink500),
      child: const Text('Intentar con otro correo'),
    ),
  ]);
}
