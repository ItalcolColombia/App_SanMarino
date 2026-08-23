/// Widgets de sincronización. Traducción directa de los patrones UX del
/// design system (`components/sync-v2.jsx`).
library;

import 'package:flutter/material.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';

// ═══════════════════════════════════════════════════════════════════════════
// SyncDot — indicador inline por registro
// ═══════════════════════════════════════════════════════════════════════════

enum SyncDotState { pending, syncing, synced }

class SyncDot extends StatefulWidget {
  const SyncDot({super.key, required this.state, this.size = 8});

  final SyncDotState state;
  final double size;

  @override
  State<SyncDot> createState() => _SyncDotState();
}

class _SyncDotState extends State<SyncDot> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this, duration: const Duration(milliseconds: 1800),
  )..repeat(reverse: true);

  @override
  void dispose() { _c.dispose(); super.dispose(); }

  @override
  Widget build(BuildContext context) {
    switch (widget.state) {
      case SyncDotState.synced:
        return Container(
          width: widget.size, height: widget.size,
          decoration: BoxDecoration(
            color: AppColors.green500.withValues(alpha: 0.4),
            shape: BoxShape.circle,
          ),
        );
      case SyncDotState.syncing:
        return SizedBox(
          width: widget.size + 4, height: widget.size + 4,
          child: const CircularProgressIndicator(strokeWidth: 2, color: AppColors.info),
        );
      case SyncDotState.pending:
        // Naranja con halo pulsante suave — presente sin gritar.
        return AnimatedBuilder(
          animation: _c,
          builder: (_, __) => Container(
            width: widget.size, height: widget.size,
            decoration: BoxDecoration(
              color: AppColors.brand500,
              shape: BoxShape.circle,
              boxShadow: [BoxShadow(
                color: AppColors.brand500.withValues(alpha: 0.18 - (_c.value * 0.10)),
                blurRadius: 0,
                spreadRadius: 4 + (_c.value * 2),
              )],
            ),
          ),
        );
    }
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// AmbientDot — punto sobre el avatar. Invisible cuando todo está al día.
// ═══════════════════════════════════════════════════════════════════════════

class AmbientDot extends StatelessWidget {
  const AmbientDot({super.key, required this.sync});

  final SyncService sync;

  @override
  Widget build(BuildContext context) {
    if (sync.todoAlDia) return const SizedBox.shrink();
    final offline = !sync.enLinea;
    return Container(
      width: 10, height: 10,
      decoration: BoxDecoration(
        color: offline ? AppColors.ink700 : AppColors.brand500,
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white, width: 2),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// ConnectionChip — solo aparece cuando hay algo que decir
// ═══════════════════════════════════════════════════════════════════════════

class ConnectionChip extends StatelessWidget {
  const ConnectionChip({super.key, required this.sync, this.onTap});

  final SyncService sync;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    if (sync.todoAlDia) return const SizedBox.shrink();

    final (bg, fg, label) = switch (sync.calidad) {
      CalidadConexion.offline => (AppColors.ink900, AppColors.cream, 'Sin conexión'),
      CalidadConexion.wifiDebil => (AppColors.warningBg, const Color(0xFF9A7626), '${sync.pendientes} pendientes · Wi-Fi débil'),
      _ => (AppColors.brand50, AppColors.brand700, '${sync.pendientes} pendientes'),
    };

    if (sync.fase == FaseRibbon.sincronizando) {
      return _Chip(
        bg: AppColors.green50, fg: AppColors.green600,
        label: 'Sincronizando…', onTap: onTap,
        leading: const SizedBox(
          width: 10, height: 10,
          child: CircularProgressIndicator(strokeWidth: 2, color: AppColors.green600),
        ),
      );
    }

    return _Chip(bg: bg, fg: fg, label: label, onTap: onTap,
      leading: Container(width: 8, height: 8, decoration: BoxDecoration(color: fg, shape: BoxShape.circle)),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.bg, required this.fg, required this.label, this.leading, this.onTap});

  final Color bg, fg;
  final String label;
  final Widget? leading;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(AppRadius.pill),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.pill),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            if (leading != null) ...[leading!, const SizedBox(width: 6)],
            Text(label, style: TextStyle(
              fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600, color: fg,
            )),
          ]),
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// SyncRibbon — reconexión progresiva. Nunca dura más de ~5 s ni bloquea.
// ═══════════════════════════════════════════════════════════════════════════

class SyncRibbon extends StatelessWidget {
  const SyncRibbon({super.key, required this.sync});

  final SyncService sync;

  @override
  Widget build(BuildContext context) {
    if (sync.fase == FaseRibbon.oculto) return const SizedBox.shrink();

    return Positioned(
      top: AppSpacing.s3, left: AppSpacing.s3, right: AppSpacing.s3,
      child: switch (sync.fase) {
        FaseRibbon.detectando    => const _RibbonDetectando(),
        FaseRibbon.sincronizando => _RibbonSincronizando(sync: sync),
        FaseRibbon.exito         => const _RibbonExito(),
        FaseRibbon.oculto        => const SizedBox.shrink(),
      },
    );
  }
}

class _RibbonDetectando extends StatelessWidget {
  const _RibbonDetectando();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        gradient: const LinearGradient(colors: [Color(0xFF1F3A29), AppColors.green700]),
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      child: const Row(children: [
        Icon(Icons.wifi_rounded, size: 16, color: Colors.white),
        SizedBox(width: AppSpacing.s2),
        Text('Conexión detectada · verificando…', style: TextStyle(
          fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600, color: Colors.white,
        )),
      ]),
    );
  }
}

class _RibbonSincronizando extends StatelessWidget {
  const _RibbonSincronizando({required this.sync});

  final SyncService sync;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.green200),
        boxShadow: AppColors.shadowMd,
      ),
      child: Column(children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          child: Row(children: [
            const SizedBox(width: 14, height: 14,
              child: CircularProgressIndicator(strokeWidth: 2, color: AppColors.green600)),
            const SizedBox(width: AppSpacing.s2),
            const Expanded(child: Text('Sincronizando', style: TextStyle(
              fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.green700,
            ))),
            Text('${sync.enviados} / ${sync.totalLote}', style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 11, fontWeight: FontWeight.w700,
              color: AppColors.green600, fontFeatures: [FontFeature.tabularFigures()],
            )),
          ]),
        ),
        ClipRRect(
          borderRadius: const BorderRadius.vertical(bottom: Radius.circular(AppRadius.md)),
          child: _BarraProgreso(valor: sync.progreso, duration: AppDuration.slow),
        ),
      ]),
    );
  }
}

/// Alias local para no depender de la versión de Flutter.
class _BarraProgreso extends ImplicitlyAnimatedWidget {
  const _BarraProgreso({required this.valor, required super.duration});

  final double valor;

  @override
  ImplicitlyAnimatedWidgetState<_BarraProgreso> createState() => _BarraProgresoState();
}

class _BarraProgresoState extends AnimatedWidgetBaseState<_BarraProgreso> {
  Tween<double>? _t;

  @override
  void forEachTween(TweenVisitor<dynamic> visitor) {
    _t = visitor(_t, widget.valor, (v) => Tween<double>(begin: v as double)) as Tween<double>?;
  }

  @override
  Widget build(BuildContext context) => LinearProgressIndicator(
    value: _t?.evaluate(animation) ?? 0,
    minHeight: 3,
    backgroundColor: AppColors.green50,
    color: AppColors.green500,
  );
}

class _RibbonExito extends StatelessWidget {
  const _RibbonExito();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          begin: Alignment.topLeft, end: Alignment.bottomRight,
          colors: [AppColors.green500, AppColors.green600],
        ),
        borderRadius: BorderRadius.circular(AppRadius.md),
        boxShadow: [BoxShadow(
          color: AppColors.green600.withValues(alpha: 0.25),
          blurRadius: 20, offset: const Offset(0, 8),
        )],
      ),
      child: Row(children: [
        Container(
          width: 24, height: 24,
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.2), shape: BoxShape.circle,
          ),
          child: const Icon(Icons.check_rounded, size: 14, color: Colors.white),
        ),
        const SizedBox(width: AppSpacing.s2),
        const Expanded(child: Text('Al día', style: TextStyle(
          fontFamily: 'PlusJakartaSans', fontSize: 13, fontWeight: FontWeight.w700, color: Colors.white,
        ))),
        Text('todo sincronizado', style: TextStyle(
          fontFamily: 'Inter', fontSize: 11, color: Colors.white.withValues(alpha: 0.9),
        )),
      ]),
    );
  }
}
