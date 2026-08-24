/// Widgets de sincronización. Traducción directa de los patrones UX del
/// design system (`components/sync-v2.jsx`).
///
/// ── Regla de color de este archivo (paleta de marca) ────────────────────────
///   naranja → cola y envío EN CURSO: son acciones, no éxitos.
///   verde   → SOLO el final feliz (`_RibbonExito`, punto `synced`).
///   neutro  → sin conexión. Es un modo de trabajo válido: nunca rojo, nunca
///             con iconografía de alarma.
/// El verde estaba usado para "detectando" y "sincronizando", que son estados
/// en progreso: al terminar bien, el usuario veía el mismo verde dos veces y el
/// cierre dejaba de significar algo.
library;

import 'package:flutter/material.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';

/// Ocre oscuro para texto sobre [AppColors.warningBg]. Se deriva de los tokens
/// (el ámbar de `warning` cortado con tinta) en vez de sumar un hex suelto: el
/// `warning` puro sobre su propio fondo no llega a contraste legible bajo sol.
Color get _ocreTexto =>
    Color.alphaBlend(AppColors.ink900.withValues(alpha: 0.55), AppColors.warning);

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
  late final AnimationController _c =
      AnimationController(vsync: this, duration: AppMotion.shimmer);

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // El pulso es decorativo: con "Reducir movimiento" el punto se queda quieto
    // en vez de latir. Va acá y no en initState porque necesita el MediaQuery.
    if (AppMotion.reducido(context)) {
      _c.stop();
      _c.value = 0;
    } else if (!_c.isAnimating) {
      _c.repeat(reverse: true);
    }
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    switch (widget.state) {
      case SyncDotState.synced:
        // Único verde de la fila: llegó al servidor.
        return Container(
          width: widget.size,
          height: widget.size,
          decoration: BoxDecoration(
            color: AppColors.green500.withValues(alpha: 0.4),
            shape: BoxShape.circle,
          ),
        );
      case SyncDotState.syncing:
        // Subiendo AHORA: sigue siendo naranja de acción, igual que el ribbon.
        return SizedBox(
          width: widget.size + 4,
          height: widget.size + 4,
          child: const CircularProgressIndicator(strokeWidth: 2, color: AppColors.brand500),
        );
      case SyncDotState.pending:
        // Naranja con halo pulsante suave — presente sin gritar.
        return AnimatedBuilder(
          animation: _c,
          builder: (_, _) => Container(
            width: widget.size,
            height: widget.size,
            decoration: BoxDecoration(
              color: AppColors.brand500,
              shape: BoxShape.circle,
              boxShadow: [
                BoxShadow(
                  color: AppColors.brand500.withValues(alpha: 0.18 - (_c.value * 0.10)),
                  blurRadius: 0,
                  spreadRadius: 4 + (_c.value * 2),
                ),
              ],
            ),
          ),
        );
    }
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Esqueletos de carga
// ═══════════════════════════════════════════════════════════════════════════

/// Bloque con barrido de luz, para ocupar el lugar de un dato que todavía no se
/// sabe. Existe porque la pantalla de sincronización afirmaba "todo
/// sincronizado" mientras aún leía la cola local: un esqueleto no afirma nada.
class EsqueletoBloque extends StatefulWidget {
  const EsqueletoBloque({
    super.key,
    this.width = double.infinity,
    required this.height,
    this.radius = AppRadius.xs,
  });

  final double width;
  final double height;
  final double radius;

  @override
  State<EsqueletoBloque> createState() => _EsqueletoBloqueState();
}

class _EsqueletoBloqueState extends State<EsqueletoBloque>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c =
      AnimationController(vsync: this, duration: AppMotion.shimmer);

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Con "Reducir movimiento" el barrido queda fuera del bloque (t = 0) y el
    // esqueleto se ve como un rectángulo plano, que es lo correcto.
    if (AppMotion.reducido(context)) {
      _c.stop();
      _c.value = 0;
    } else if (!_c.isAnimating) {
      _c.repeat();
    }
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _c,
      builder: (_, _) {
        final t = _c.value;
        return Container(
          width: widget.width,
          height: widget.height,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(widget.radius),
            // La banda de luz se mueve corriendo el gradiente, no el widget: así
            // no hay Transform ni repintado de layout en cada frame.
            gradient: LinearGradient(
              begin: Alignment(-3 + 4 * t, 0),
              end: Alignment(-1 + 4 * t, 0),
              colors: const [AppColors.ink100, AppColors.cream, AppColors.ink100],
            ),
          ),
        );
      },
    );
  }
}

/// Esqueleto con la misma geometría que una fila de la cola, para que la lista
/// no salte cuando llegan los datos reales.
class EsqueletoFilaCola extends StatelessWidget {
  const EsqueletoFilaCola({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.s4,
        vertical: AppSpacing.s3,
      ),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: AppColors.line),
      ),
      child: Row(
        children: [
          const EsqueletoBloque(width: AppSpacing.s2, height: AppSpacing.s2, radius: AppRadius.pill),
          const SizedBox(width: AppSpacing.s3),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                FractionallySizedBox(
                  alignment: Alignment.centerLeft,
                  widthFactor: 0.72,
                  child: EsqueletoBloque(height: AppSpacing.s3),
                ),
                SizedBox(height: AppSpacing.s2),
                FractionallySizedBox(
                  alignment: Alignment.centerLeft,
                  widthFactor: 0.34,
                  child: EsqueletoBloque(height: AppSpacing.s2),
                ),
              ],
            ),
          ),
        ],
      ),
    );
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
      width: 10,
      height: 10,
      decoration: BoxDecoration(
        // Sin señal el punto es tinta, no rojo: hay cola, no hay falla.
        color: offline ? AppColors.ink700 : AppColors.brand500,
        shape: BoxShape.circle,
        border: Border.all(color: AppColors.surface, width: 2),
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

    if (sync.fase == FaseRibbon.sincronizando) {
      return _Chip(
        bg: AppColors.brand50,
        fg: AppColors.brand700,
        label: 'Sincronizando…',
        onTap: onTap,
        leading: const SizedBox(
          width: 10,
          height: 10,
          child: CircularProgressIndicator(strokeWidth: 2, color: AppColors.brand700),
        ),
      );
    }

    final (bg, fg, label) = switch (sync.calidad) {
      // Tinta sobre crema: alto contraste para leerlo al sol, sin semántica de error.
      CalidadConexion.offline => (AppColors.ink900, AppColors.cream, 'Sin conexión'),
      CalidadConexion.wifiDebil => (
        AppColors.warningBg,
        _ocreTexto,
        '${sync.pendientes} pendientes · Wi-Fi débil',
      ),
      _ => (AppColors.brand50, AppColors.brand700, '${sync.pendientes} pendientes'),
    };

    return _Chip(
      bg: bg,
      fg: fg,
      label: label,
      onTap: onTap,
      leading: Container(
        width: 8,
        height: 8,
        decoration: BoxDecoration(color: fg, shape: BoxShape.circle),
      ),
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
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s3, vertical: 6),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (leading != null) ...[leading!, const SizedBox(width: 6)],
              Text(
                label,
                style: TextStyle(
                  fontFamily: 'Inter',
                  fontSize: AppFontSize.xs,
                  fontWeight: FontWeight.w600,
                  color: fg,
                ),
              ),
            ],
          ),
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
      top: AppSpacing.s3,
      left: AppSpacing.s3,
      right: AppSpacing.s3,
      child: switch (sync.fase) {
        FaseRibbon.detectando => const _RibbonDetectando(),
        FaseRibbon.sincronizando => _RibbonSincronizando(sync: sync),
        FaseRibbon.exito => const _RibbonExito(),
        FaseRibbon.oculto => const SizedBox.shrink(),
      },
    );
  }
}

class _RibbonDetectando extends StatelessWidget {
  const _RibbonDetectando();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
      decoration: BoxDecoration(
        // Azul informativo: todavía no hay nada que celebrar, solo se está
        // verificando la red. El tono oscuro se deriva del token, no es un hex nuevo.
        gradient: LinearGradient(colors: [
          AppColors.info,
          Color.alphaBlend(AppColors.ink900.withValues(alpha: 0.38), AppColors.info),
        ]),
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      child: const Row(children: [
        Icon(Icons.wifi_rounded, size: 16, color: Colors.white),
        SizedBox(width: AppSpacing.s2),
        Text(
          'Conexión detectada · verificando…',
          style: TextStyle(
            fontFamily: 'Inter',
            fontSize: AppFontSize.xs,
            fontWeight: FontWeight.w600,
            color: Colors.white,
          ),
        ),
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
        border: Border.all(color: AppColors.brand200),
        boxShadow: AppColors.shadowMd,
      ),
      child: Column(children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
          child: Row(children: [
            const SizedBox(
              width: 14,
              height: 14,
              child: CircularProgressIndicator(strokeWidth: 2, color: AppColors.brand500),
            ),
            const SizedBox(width: AppSpacing.s2),
            const Expanded(
              child: Text(
                'Sincronizando',
                style: TextStyle(
                  fontFamily: 'Inter',
                  fontSize: AppFontSize.xs,
                  fontWeight: FontWeight.w600,
                  color: AppColors.brand700,
                ),
              ),
            ),
            Text(
              '${sync.enviados} / ${sync.totalLote}',
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans',
                fontSize: AppFontSize.xs,
                fontWeight: FontWeight.w700,
                color: AppColors.brand700,
                fontFeatures: [FontFeature.tabularFigures()],
              ),
            ),
          ]),
        ),
        ClipRRect(
          borderRadius: const BorderRadius.vertical(bottom: Radius.circular(AppRadius.md)),
          child: BarraProgresoSync(
            valor: sync.progreso,
            duration: AppMotion.duracion(context, AppMotion.slow),
          ),
        ),
      ]),
    );
  }
}

/// Barra de progreso que interpola el valor en vez de saltar. Es un
/// `ImplicitlyAnimatedWidget` propio para no depender de la versión de Flutter.
class BarraProgresoSync extends ImplicitlyAnimatedWidget {
  const BarraProgresoSync({
    super.key,
    required this.valor,
    required super.duration,
    this.color = AppColors.brand500,
    this.fondo = AppColors.brand50,
    this.alto = 3,
  });

  final double valor;
  final Color color;
  final Color fondo;
  final double alto;

  @override
  ImplicitlyAnimatedWidgetState<BarraProgresoSync> createState() => _BarraProgresoSyncState();
}

class _BarraProgresoSyncState extends AnimatedWidgetBaseState<BarraProgresoSync> {
  Tween<double>? _t;

  @override
  void forEachTween(TweenVisitor<dynamic> visitor) {
    _t = visitor(_t, widget.valor, (v) => Tween<double>(begin: v as double)) as Tween<double>?;
  }

  @override
  Widget build(BuildContext context) => LinearProgressIndicator(
        value: _t?.evaluate(animation) ?? 0,
        minHeight: widget.alto,
        backgroundColor: widget.fondo,
        color: widget.color,
      );
}

class _RibbonExito extends StatelessWidget {
  const _RibbonExito();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
      decoration: BoxDecoration(
        // El único verde del flujo: terminó bien.
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [AppColors.green500, AppColors.green600],
        ),
        borderRadius: BorderRadius.circular(AppRadius.md),
        boxShadow: [
          BoxShadow(
            color: AppColors.green600.withValues(alpha: 0.25),
            blurRadius: 20,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Row(children: [
        Container(
          width: AppSpacing.s6,
          height: AppSpacing.s6,
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.2),
            shape: BoxShape.circle,
          ),
          child: const Icon(Icons.check_rounded, size: 14, color: Colors.white),
        ),
        const SizedBox(width: AppSpacing.s2),
        const Expanded(
          child: Text(
            'Al día',
            style: TextStyle(
              fontFamily: 'PlusJakartaSans',
              fontSize: AppFontSize.sm,
              fontWeight: FontWeight.w700,
              color: Colors.white,
            ),
          ),
        ),
        Text(
          'todo sincronizado',
          style: TextStyle(
            fontFamily: 'Inter',
            fontSize: AppFontSize.xs,
            color: Colors.white.withValues(alpha: 0.9),
          ),
        ),
      ]),
    );
  }
}
