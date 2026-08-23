/// Perfil del usuario: datos de cuenta, módulos asignados y cierre de sesión.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/components/marca.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';


class PerfilPage extends StatelessWidget {
  const PerfilPage({super.key, required this.usuario, required this.onLogout});

  final Usuario usuario;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.only(bottom: 96),
      children: [
        Container(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter, end: Alignment.bottomCenter,
              colors: [AppColors.green50, AppColors.cream],
            ),
          ),
          padding: const EdgeInsets.fromLTRB(AppSpacing.s5, AppSpacing.s7, AppSpacing.s5, AppSpacing.s5),
          child: Column(children: [
            Container(
              width: 80, height: 80,
              decoration: BoxDecoration(
                color: AppColors.green500,
                borderRadius: BorderRadius.circular(AppRadius.xl),
                boxShadow: AppColors.shadowMd,
              ),
              alignment: Alignment.center,
              child: Text(usuario.iniciales, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 26, fontWeight: FontWeight.w800, color: Colors.white,
              )),
            ),
            const SizedBox(height: AppSpacing.s4),
            Text(usuario.nombre, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 20, fontWeight: FontWeight.w800,
              letterSpacing: -0.4, color: AppColors.ink900,
            )),
            const SizedBox(height: 4),
            Text('${usuario.cargo} · ${usuario.granja}', style: const TextStyle(
              fontFamily: 'Inter', fontSize: 13, color: AppColors.ink500,
            )),
            const SizedBox(height: AppSpacing.s3),
            Row(mainAxisAlignment: MainAxisAlignment.center, children: [
              const AppBadge(label: 'Activo', tone: BadgeTone.success, dot: true),
              const SizedBox(width: 6),
              AppBadge(label: usuario.pais, tone: usuario.tieneControlAgua ? BadgeTone.info : BadgeTone.neutral),
            ]),
          ]),
        ),

        // Módulos habilitados
        Padding(
          padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, AppSpacing.s2),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('MÓDULOS ASIGNADOS', style: TextStyle(
              fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
              letterSpacing: 0.8, color: AppColors.ink500,
            )),
            const SizedBox(height: AppSpacing.s2),
            Wrap(spacing: 6, runSpacing: 6, children: [
              for (final m in usuario.modulos)
                AppBadge(label: '${m.emoji} ${m.label}', tone: BadgeTone.neutral),
            ]),
            if (usuario.tieneControlAgua) ...[
              const SizedBox(height: AppSpacing.s2),
              const AppBadge(label: 'Control de agua habilitado', tone: BadgeTone.info),
            ],
          ]),
        ),

        _seccion('CUENTA', [
          _fila(Icons.mail_outline_rounded, 'Correo', usuario.email),
          _fila(Icons.lock_outline_rounded, 'Cambiar contraseña', null, onTap: () {}),
          _fila(Icons.notifications_outlined, 'Notificaciones', 'Activadas', onTap: () {}),
        ]),

        _seccion('DATOS', [
          _fila(Icons.sync_rounded, 'Sincronizar ahora', null, onTap: () {}),
          _fila(Icons.download_outlined, 'Descargar guías genéticas', null, onTap: () {}),
        ]),

        _seccion('SESIÓN', [
          _fila(Icons.logout_rounded, 'Cerrar sesión', null, onTap: onLogout, danger: true),
        ]),

        Padding(
          padding: const EdgeInsets.all(AppSpacing.s6),
          child: Opacity(
            opacity: 0.5,
            child: Column(children: [
              Image.asset('assets/images/brand/logo-italfoods-zootecnico.png', height: 42),
              const SizedBox(height: 6),
              const Text('© 2026 Italfoods · v2.1.0', style: TextStyle(
                fontFamily: 'Inter', fontSize: 10, color: AppColors.ink500,
              )),
            ]),
          ),
        ),
      ],
    );
  }

  Widget _seccion(String titulo, List<Widget> filas) => Padding(
    padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s4, AppSpacing.s4, 0),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(titulo, style: const TextStyle(
        fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
        letterSpacing: 0.8, color: AppColors.ink500,
      )),
      const SizedBox(height: AppSpacing.s2),
      Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.lg),
          border: Border.all(color: AppColors.line),
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(children: [
          for (int i = 0; i < filas.length; i++) ...[
            if (i > 0) Divider(height: 1, color: AppColors.line),
            filas[i],
          ],
        ]),
      ),
    ]),
  );

  Widget _fila(IconData icon, String label, String? value, {VoidCallback? onTap, bool danger = false}) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: 13),
        child: Row(children: [
          Container(
            width: 32, height: 32,
            decoration: BoxDecoration(
              color: danger ? AppColors.dangerBg : AppColors.cream2,
              borderRadius: BorderRadius.circular(AppRadius.sm),
            ),
            child: Icon(icon, size: 15, color: danger ? const Color(0xFF9A4035) : AppColors.ink700),
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(child: Text(label, style: TextStyle(
            fontFamily: 'Inter', fontSize: 13, fontWeight: FontWeight.w500,
            color: danger ? const Color(0xFF9A4035) : AppColors.ink900,
          ))),
          if (value != null) Text(value, style: const TextStyle(
            fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500,
          )),
          if (onTap != null && !danger) ...[
            const SizedBox(width: AppSpacing.s2),
            const Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.ink200),
          ],
        ]),
      ),
    );
  }
}

