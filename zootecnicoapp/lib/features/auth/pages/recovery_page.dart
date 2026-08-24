/// Recuperación de contraseña: el backend envía una nueva por correo.
///
/// Comparte el fondo, la tarjeta, el aviso y el input con el login: son las
/// mismas piezas que replican el login web, y viven en `login_page.dart` para no
/// tener dos copias que se desincronicen.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/components/marca.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/auth/pages/login_page.dart'
    show AvisoAuth, CampoAuth, EncabezadoAuth, FondoAuth, TarjetaAuth, TonoAviso;

/// Mismo tope que el login: la tarjeta no se estira en tablet.
const double _anchoTarjeta = 420;

class RecoveryPage extends StatefulWidget {
  const RecoveryPage({super.key});

  @override
  State<RecoveryPage> createState() => _RecoveryScreenState();
}

class _RecoveryScreenState extends State<RecoveryPage> {
  final _email = TextEditingController();
  bool _cargando = false;
  bool _enviado = false;
  String? _error;

  @override
  void dispose() { _email.dispose(); super.dispose(); }

  Future<void> _enviar() async {
    // Guarda de doble envío: sin esto, dos toques rápidos encolaban dos pedidos.
    if (_cargando) return;

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
      body: FondoAuth(
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.s5,
                vertical: AppSpacing.s7,
              ),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: _anchoTarjeta),
                child: TarjetaAuth(
                  child: Column(
                    children: [
                      // El tagline se omite acá: la tarjeta ya trae un título
                      // propio y repetirlo alarga la pantalla sin aportar.
                      const EntradaEscalonada(
                        indice: 0,
                        child: LogoMarca(mostrarTagline: false),
                      ),
                      const SizedBox(height: AppSpacing.s6),
                      EntradaEscalonada(
                        indice: 2,
                        child: CambioSuave(
                          claveDeEstado: _enviado,
                          child: _enviado ? _exito() : _formulario(),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _formulario() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const EncabezadoAuth(titulo: 'Recuperar contraseña'),
      const SizedBox(height: AppSpacing.s5),

      const Center(child: _Emblema(
        icono: Icons.vpn_key_outlined,
        fondo: AppColors.brand50,
        color: AppColors.brand500,
      )),
      const SizedBox(height: AppSpacing.s4),

      const Text(
        'Ingresa tu correo y te enviaremos una nueva contraseña.',
        textAlign: TextAlign.center,
        style: TextStyle(
          fontFamily: 'Inter',
          fontSize: AppFontSize.sm,
          height: 1.5,
          color: AppColors.ink500,
        ),
      ),
      const SizedBox(height: AppSpacing.s5),

      if (_error != null) ...[
        // Un correo mal escrito se corrige solo: es un aviso, no un fallo grave.
        AvisoAuth(mensaje: _error!, tono: TonoAviso.atencion),
        const SizedBox(height: AppSpacing.s4),
      ],

      CampoAuth(
        etiqueta: 'Correo electrónico',
        controller: _email,
        icono: Icons.mail_outline_rounded,
        placeholder: 'usuario@empresa.com',
        keyboardType: TextInputType.emailAddress,
        textInputAction: TextInputAction.done,
        autofillHints: const [AutofillHints.username, AutofillHints.email],
      ),
      const SizedBox(height: AppSpacing.s5),

      AppButton(
        // Naranja de marca: la variante `primary` de la primitiva sigue siendo
        // verde y el verde está reservado para el éxito.
        variant: AppButtonVariant.primary,
        label: _cargando ? 'Enviando…' : 'Enviar nueva contraseña',
        size: AppButtonSize.lg,
        full: true,
        loading: _cargando,
        onPressed: _cargando ? null : _enviar,
      ),
      const SizedBox(height: AppSpacing.s2),

      _VolverAlLogin(onPressed: _cargando ? null : () => Navigator.of(context).pop()),
    ],
  );

  Widget _exito() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      // Verde: acá sí es éxito, que es lo único para lo que la regla de marca
      // reserva ese color.
      const Center(child: _Emblema(
        icono: Icons.mark_email_read_outlined,
        fondo: AppColors.successBg,
        color: AppColors.green500,
      )),
      const SizedBox(height: AppSpacing.s4),

      const Text(
        '¡Revisa tu correo!',
        textAlign: TextAlign.center,
        style: TextStyle(
          fontFamily: 'PlusJakartaSans',
          fontSize: AppFontSize.lg,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.4,
          color: AppColors.ink900,
        ),
      ),
      const SizedBox(height: AppSpacing.s2),

      Text.rich(
        textAlign: TextAlign.center,
        TextSpan(
          style: const TextStyle(
            fontFamily: 'Inter',
            fontSize: AppFontSize.sm,
            height: 1.6,
            color: AppColors.ink500,
          ),
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

      AppButton(
        variant: AppButtonVariant.primary,
        label: 'Ir al login',
        size: AppButtonSize.lg,
        full: true,
        onPressed: () => Navigator.of(context).pop(),
      ),
      const SizedBox(height: AppSpacing.s2),

      TextButton(
        onPressed: () => setState(() => _enviado = false),
        style: TextButton.styleFrom(foregroundColor: AppColors.ink500),
        child: const Text('Intentar con otro correo'),
      ),
    ],
  );
}

/// Círculo con el icono de la pantalla. Es el mismo bloque en el formulario y en
/// el éxito, solo cambia el par icono/color.
class _Emblema extends StatelessWidget {
  const _Emblema({required this.icono, required this.fondo, required this.color});

  final IconData icono;
  final Color fondo;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: AppSpacing.s10,
      height: AppSpacing.s10,
      decoration: BoxDecoration(color: fondo, shape: BoxShape.circle),
      child: Icon(icono, size: AppSpacing.s7, color: color),
    );
  }
}

/// Vuelta al login. Iba en verde, que la regla de marca reserva para el éxito;
/// ahora toma el naranja de acción del tema.
class _VolverAlLogin extends StatelessWidget {
  const _VolverAlLogin({this.onPressed});

  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return TextButton.icon(
      onPressed: onPressed,
      icon: const Icon(Icons.arrow_back_rounded, size: 16),
      label: const Text('Volver al login'),
    );
  }
}
