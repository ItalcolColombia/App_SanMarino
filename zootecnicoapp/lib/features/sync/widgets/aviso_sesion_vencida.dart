/// Aviso de sesión vencida — modo «sólo captura».
///
/// Aparece cuando el token venció pero la sesión **no** se destruyó: el operario
/// puede seguir registrando y viendo su cola, y lo único suspendido es subir.
///
/// No es un error ni un peligro: no lleva rojo. Es un estado de trabajo, igual
/// que estar sin señal. Lo que sí tiene que hacer es ser **imposible de pasar
/// por alto** y ofrecer la salida en el mismo lugar, porque mientras esté puesto
/// nada de lo que anote el operario llega al servidor.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';

class AvisoSesionVencida extends StatelessWidget {
  const AvisoSesionVencida({super.key, required this.onReingresar});

  final VoidCallback onReingresar;

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: AppMotion.duracion(context, AppMotion.base),
      curve: AppMotion.entrada,
      builder: (context, t, hijo) => Opacity(
        opacity: t,
        child: Transform.translate(offset: Offset(0, (1 - t) * -8), child: hijo),
      ),
      child: Container(
        margin: const EdgeInsets.fromLTRB(
            AppSpacing.s4, AppSpacing.s2, AppSpacing.s4, 0),
        padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.s3, vertical: AppSpacing.s3),
        decoration: BoxDecoration(
          color: AppColors.warningBg,
          borderRadius: BorderRadius.circular(AppRadius.md),
          border: Border.all(color: AppColors.warning.withValues(alpha: 0.35)),
        ),
        child: Row(
          children: [
            const Icon(Icons.lock_clock, size: AppSpacing.s5, color: AppColors.warning),
            const SizedBox(width: AppSpacing.s3),
            const Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Tu sesión venció',
                    style: TextStyle(
                      fontFamily: 'PlusJakartaSans',
                      fontSize: AppFontSize.sm,
                      fontWeight: FontWeight.w700,
                      color: AppColors.ink900,
                    ),
                  ),
                  SizedBox(height: 2),
                  Text(
                    'Podés seguir registrando: se guarda en el equipo. '
                    'Para subirlo hay que ingresar de nuevo.',
                    style: TextStyle(
                      fontFamily: 'Inter',
                      fontSize: AppFontSize.xs,
                      height: 1.4,
                      color: AppColors.ink700,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: AppSpacing.s2),
            // Objetivo táctil completo: se toca con guantes.
            TextButton(
              onPressed: onReingresar,
              style: TextButton.styleFrom(
                minimumSize: const Size(0, AppTouch.min),
                padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3),
              ),
              child: const Text('Ingresar'),
            ),
          ],
        ),
      ),
    );
  }
}
