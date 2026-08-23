/// Cola de sincronización: qué quedó pendiente de subir y en qué estado está.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/sync/widgets/sync_widgets.dart';


class SyncPage extends StatelessWidget {
  const SyncPage({super.key, required this.sync});

  final SyncService sync;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.cream,
      appBar: AppBar(title: const Text('Sincronización')),
      body: FutureBuilder<List<RegistroPendiente>>(
        future: LocalDb.instance.pendientes(),
        builder: (context, snap) {
          final cola = snap.data ?? const <RegistroPendiente>[];
          return ListView(
            padding: const EdgeInsets.all(AppSpacing.s4),
            children: [
              _resumen(context, cola),
              const SizedBox(height: AppSpacing.s5),
              if (cola.isEmpty)
                Column(children: [
                  Container(
                    width: 60, height: 60,
                    decoration: BoxDecoration(
                      color: AppColors.successBg, borderRadius: BorderRadius.circular(AppRadius.xl),
                    ),
                    child: const Icon(Icons.check_rounded, size: 28, color: AppColors.green600),
                  ),
                  const SizedBox(height: AppSpacing.s3),
                  const Text('Todo sincronizado', style: TextStyle(
                    fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.ink900,
                  )),
                  const SizedBox(height: 4),
                  const Text('No hay registros pendientes de enviar.',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)),
                ])
              else ...[
                const Text('EN COLA', style: TextStyle(
                  fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w700,
                  letterSpacing: 0.8, color: AppColors.ink500,
                )),
                const SizedBox(height: AppSpacing.s2),
                for (final r in cola) ...[
                  _ColaItem(registro: r),
                  const SizedBox(height: AppSpacing.s2),
                ],
              ],
              const SizedBox(height: AppSpacing.s4),
              const Text('CONFIGURACIÓN', style: TextStyle(
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
                child: SwitchListTile(
                  value: sync.autoSync,
                  onChanged: (v) => sync.autoSync = v,
                  title: const Text('Sincronización automática', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 13, color: AppColors.ink900,
                  )),
                  subtitle: const Text('Al recuperar conexión', style: TextStyle(
                    fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
                  )),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _resumen(BuildContext context, List<RegistroPendiente> cola) {
    final offline = !sync.enLinea;
    return Container(
      padding: const EdgeInsets.all(AppSpacing.s4),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft, end: Alignment.bottomRight,
          colors: offline
            ? [AppColors.ink700, AppColors.ink900]
            : [AppColors.brand500, AppColors.brand600],
        ),
        borderRadius: BorderRadius.circular(AppRadius.xl),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(offline ? 'Sin conexión · acumulados' : 'Pendientes de enviar', style: TextStyle(
          fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600,
          color: Colors.white.withValues(alpha: 0.9),
        )),
        const SizedBox(height: 4),
        Text('${cola.length}', style: const TextStyle(
          fontFamily: 'PlusJakartaSans', fontSize: 30, fontWeight: FontWeight.w800,
          letterSpacing: -0.6, height: 1, color: Colors.white,
          fontFeatures: [FontFeature.tabularFigures()],
        )),
        const SizedBox(height: 2),
        Text(cola.length == 1 ? 'registro' : 'registros', style: TextStyle(
          fontFamily: 'Inter', fontSize: 12, color: Colors.white.withValues(alpha: 0.85),
        )),
        const SizedBox(height: AppSpacing.s4),
        AppButton(
          label: offline ? 'Sin conexión' : 'Sincronizar ahora',
          icon: Icons.sync_rounded, full: true, size: AppButtonSize.md,
          onPressed: offline || cola.isEmpty ? null : sync.sincronizar,
        ),
      ]),
    );
  }
}

class _ColaItem extends StatelessWidget {
  const _ColaItem({required this.registro});

  final RegistroPendiente registro;

  @override
  Widget build(BuildContext context) {
    final estado = switch (registro.estado) {
      EstadoSync.pending => SyncDotState.pending,
      EstadoSync.syncing => SyncDotState.syncing,
      EstadoSync.synced  => SyncDotState.synced,
      // El servidor ya tenía ese día: el registro NO se perdió, está guardado.
      EstadoSync.duplicado => SyncDotState.synced,
      EstadoSync.error   => SyncDotState.pending,
    };
    final modulo = ModuloSeguimiento.fromId(registro.tipo);
    final titulo = modulo != null
      ? 'Seguimiento · ${registro.loteNombre} · ${modulo.label}'
      : '${_tituloMovimiento(registro.tipo)} · ${registro.loteNombre}';

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.line),
      ),
      child: Row(children: [
        SyncDot(state: estado),
        const SizedBox(width: AppSpacing.s3),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(titulo, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 13, fontWeight: FontWeight.w600, color: AppColors.ink900,
          )),
          Text(_hace(registro.createdAt), style: const TextStyle(
            fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
          )),
        ])),
        if (registro.estado == EstadoSync.error)
          const AppBadge(label: 'Resolver', tone: BadgeTone.danger),
      ]),
    );
  }

  String _tituloMovimiento(String tipo) => switch (tipo) {
    'venta-aves' => 'Venta de aves',
    'traslado-aves' => 'Traslado de aves',
    'movimiento-huevos' => 'Movimiento de huevos',
    _ => 'Registro',
  };

  String _hace(DateTime d) {
    final min = DateTime.now().difference(d).inMinutes;
    if (min < 1) return 'Ahora';
    if (min < 60) return 'Hace $min min';
    final h = min ~/ 60;
    if (h < 24) return 'Hace $h h';
    return 'Hace ${h ~/ 24} d';
  }
}

