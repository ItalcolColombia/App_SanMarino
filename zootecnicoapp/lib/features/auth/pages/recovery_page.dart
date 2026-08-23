/// Recuperación de contraseña: el backend envía una nueva por correo.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/components/marca.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';


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
    const LogoMarca(),
    const SizedBox(height: AppSpacing.s7),
    Container(
      width: 56, height: 56,
      decoration: BoxDecoration(
        color: AppColors.brand50, borderRadius: BorderRadius.circular(18),
      ),
      child: const Icon(Icons.vpn_key_outlined, size: 26, color: AppColors.brand500),
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
    const LogoMarca(),
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
