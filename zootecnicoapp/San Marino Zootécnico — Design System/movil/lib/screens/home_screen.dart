/// Home — bienvenida con animación gallina+huevo, módulos por rol y lotes.
library;

import 'dart:math' as math;
import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import '../widgets/app_widgets.dart';
import '../widgets/sync_widgets.dart';
import '../core/models.dart';
import '../core/sync_service.dart';

// ═══════════════════════════════════════════════════════════════════════════
// Animación gallina + huevo — dibujada con CustomPainter, sin assets.
// ═══════════════════════════════════════════════════════════════════════════

class GallinaAnimacion extends StatefulWidget {
  const GallinaAnimacion({super.key, this.height = 120});

  final double height;

  @override
  State<GallinaAnimacion> createState() => _GallinaAnimacionState();
}

class _GallinaAnimacionState extends State<GallinaAnimacion> with TickerProviderStateMixin {
  late final AnimationController _bob = AnimationController(
    vsync: this, duration: const Duration(milliseconds: 2400),
  )..repeat();
  late final AnimationController _huevo = AnimationController(
    vsync: this, duration: const Duration(milliseconds: 4000),
  )..repeat();

  @override
  void dispose() { _bob.dispose(); _huevo.dispose(); super.dispose(); }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: widget.height,
      child: AnimatedBuilder(
        animation: Listenable.merge([_bob, _huevo]),
        builder: (_, __) => CustomPaint(
          painter: _GallinaPainter(bob: _bob.value, huevo: _huevo.value),
          size: Size.infinite,
        ),
      ),
    );
  }
}

class _GallinaPainter extends CustomPainter {
  _GallinaPainter({required this.bob, required this.huevo});

  final double bob;   // 0..1 ciclo de balanceo
  final double huevo; // 0..1 ciclo de aparición del huevo

  static const _plumaje = Color(0xFFF5EDD8);
  static const _plumajeSombra = Color(0xFFECE4CA);
  static const _plumajeCola = Color(0xFFF0E8D8);

  @override
  void paint(Canvas canvas, Size size) {
    final cx = size.width / 2;
    final baseY = size.height - 8;
    final t = math.sin(bob * math.pi * 2);
    final dy = -t * 4;

    final p = Paint()..style = PaintingStyle.fill;

    // Sombra en el suelo — se comprime al saltar.
    p.color = AppColors.ink900.withValues(alpha: 0.10 - (t.abs() * 0.04));
    canvas.drawOval(Rect.fromCenter(
      center: Offset(cx - 14, baseY + 2),
      width: 96 - (t.abs() * 18), height: 9,
    ), p);

    canvas.save();
    canvas.translate(0, dy);

    // ── Cola ──
    p.color = _plumajeCola;
    canvas.save();
    canvas.translate(cx + 34, baseY - 44);
    canvas.rotate(-0.44);
    canvas.drawOval(Rect.fromCenter(center: Offset.zero, width: 32, height: 20), p);
    canvas.restore();

    // ── Cuerpo ──
    p.color = _plumaje;
    canvas.drawOval(Rect.fromCenter(
      center: Offset(cx - 6, baseY - 32), width: 88, height: 64,
    ), p);

    // ── Ala ──
    p.color = _plumajeSombra;
    canvas.drawOval(Rect.fromCenter(
      center: Offset(cx - 6, baseY - 30), width: 52, height: 34,
    ), p);

    // ── Patas ──
    final pata = Paint()
      ..color = AppColors.orange500
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3.5
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(Offset(cx - 18, baseY - 2), Offset(cx - 24, baseY + 6), pata);
    canvas.drawLine(Offset(cx - 2, baseY - 2), Offset(cx + 4, baseY + 6), pata);
    pata.strokeWidth = 2;
    canvas.drawLine(Offset(cx - 24, baseY + 6), Offset(cx - 31, baseY + 4), pata);
    canvas.drawLine(Offset(cx + 4, baseY + 6), Offset(cx + 11, baseY + 4), pata);

    // ── Cabeza (rotación propia, más viva) ──
    final headAngle = math.sin(bob * math.pi * 2 + 0.6) * 0.09;
    canvas.save();
    canvas.translate(cx - 46, baseY - 66);
    canvas.rotate(headAngle);

    // Cresta
    p.color = AppColors.orange500;
    final cresta = Path()
      ..moveTo(-7, -20)
      ..quadraticBezierTo(-4, -31, -1, -20)
      ..quadraticBezierTo(2, -31, 5, -20)
      ..close();
    canvas.drawPath(cresta, p);

    // Cabeza
    p.color = _plumaje;
    canvas.drawCircle(Offset.zero, 22, p);

    // Barbilla
    p.color = AppColors.orange500.withValues(alpha: 0.75);
    canvas.drawOval(Rect.fromCenter(center: const Offset(-10, 13), width: 12, height: 16), p);

    // Pico
    p.color = AppColors.orange500;
    canvas.drawPath(Path()
      ..moveTo(-26, -1)
      ..lineTo(-11, -4)
      ..lineTo(-11, 4)
      ..close(), p);

    // Ojo
    p.color = Colors.white;
    canvas.drawCircle(const Offset(-6, -5), 6, p);
    p.color = AppColors.ink900;
    canvas.drawCircle(const Offset(-8, -5), 3.5, p);
    p.color = Colors.white;
    canvas.drawCircle(const Offset(-9, -6.5), 1.2, p);

    canvas.restore(); // cabeza
    canvas.restore(); // cuerpo

    // ── Huevo: aparece, se asienta y se desvanece ──
    _pintarHuevo(canvas, Offset(cx + 62, baseY - 16), huevo);
    _pintarHuevo(canvas, Offset(cx + 84, baseY - 14), (huevo + 0.5) % 1.0, escala: 0.85);
  }

  void _pintarHuevo(Canvas canvas, Offset pos, double fase, {double escala = 1}) {
    // Curva de opacidad: 0 → aparece → visible → desvanece
    final double opacidad;
    final double scale;
    if (fase < 0.15) {
      final k = fase / 0.15;
      opacidad = k; scale = 0.7 + (k * 0.38);
    } else if (fase < 0.25) {
      final k = (fase - 0.15) / 0.10;
      opacidad = 1; scale = 1.08 - (k * 0.08);
    } else if (fase < 0.65) {
      opacidad = 1; scale = 1;
    } else if (fase < 0.9) {
      final k = (fase - 0.65) / 0.25;
      opacidad = 1 - (k * 0.7); scale = 1 - (k * 0.05);
    } else {
      final k = (fase - 0.9) / 0.10;
      opacidad = 0.3 - (k * 0.3); scale = 0.95 - (k * 0.15);
    }
    if (opacidad <= 0.01) return;

    final w = 26 * scale * escala;
    final h = 32 * scale * escala;

    final relleno = Paint()..color = Colors.white.withValues(alpha: opacidad);
    final borde = Paint()
      ..color = const Color(0xFFE8DFC8).withValues(alpha: opacidad)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.5;

    final r = Rect.fromCenter(center: pos, width: w, height: h);
    canvas.drawOval(r, relleno);
    canvas.drawOval(r, borde);
    // Brillo
    canvas.drawOval(
      Rect.fromCenter(center: pos.translate(-w * 0.16, -h * 0.2), width: w * 0.24, height: h * 0.14),
      Paint()..color = Colors.white.withValues(alpha: opacidad * 0.7),
    );
  }

  @override
  bool shouldRepaint(_GallinaPainter old) => old.bob != bob || old.huevo != huevo;
}

// ═══════════════════════════════════════════════════════════════════════════
// Home
// ═══════════════════════════════════════════════════════════════════════════

class HomeScreen extends StatelessWidget {
  const HomeScreen({
    super.key,
    required this.usuario,
    required this.lotes,
    required this.sync,
    required this.onNuevoSeguimiento,
    required this.onVerLotes,
    required this.onVerSync,
    required this.onPerfil,
  });

  final Usuario usuario;
  final List<Lote> lotes;
  final SyncService sync;
  final void Function(ModuloSeguimiento? modulo, Lote? lote) onNuevoSeguimiento;
  final VoidCallback onVerLotes;
  final VoidCallback onVerSync;
  final VoidCallback onPerfil;

  @override
  Widget build(BuildContext context) {
    return Stack(children: [
      ListView(
        padding: const EdgeInsets.only(bottom: 96),
        children: [
          _topBar(context),
          _bienvenida(context),
          if (usuario.modulos.isNotEmpty) _modulos(),
          if (lotes.isNotEmpty) _misLotes(),
          if (sync.pendientes > 0 || !sync.enLinea) _pendientes(),
        ],
      ),
      SyncRibbon(sync: sync),
    ]);
  }

  Widget _topBar(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s5, AppSpacing.s5, AppSpacing.s5, AppSpacing.s3),
      child: Row(children: [
        GestureDetector(
          onTap: onPerfil,
          child: Stack(clipBehavior: Clip.none, children: [
            Container(
              width: 44, height: 44,
              decoration: BoxDecoration(
                color: AppColors.green500, borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              alignment: Alignment.center,
              child: Text(usuario.iniciales, style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 15, fontWeight: FontWeight.w800, color: Colors.white,
              )),
            ),
            Positioned(top: -2, right: -2, child: AmbientDot(sync: sync)),
          ]),
        ),
        const SizedBox(width: AppSpacing.s3),
        Expanded(child: GestureDetector(
          onTap: onPerfil,
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Buen día,', style: TextStyle(
              fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
            )),
            Text(usuario.nombre, maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 15, fontWeight: FontWeight.w700, color: AppColors.ink900,
              )),
            Text('${usuario.cargo} · ${usuario.granja}', maxLines: 1, overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontFamily: 'Inter', fontSize: 10, color: AppColors.ink500)),
          ]),
        )),
        ConnectionChip(sync: sync, onTap: onVerSync),
      ]),
    );
  }

  Widget _bienvenida(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s5),
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadius.xl),
          border: Border.all(color: AppColors.line),
          boxShadow: AppColors.shadowSm,
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(children: [
          Container(
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft, end: Alignment.bottomRight,
                colors: [AppColors.green50, Color(0xFFE0EDE0)],
              ),
            ),
            padding: const EdgeInsets.fromLTRB(AppSpacing.s4, AppSpacing.s5, AppSpacing.s4, AppSpacing.s2),
            child: const GallinaAnimacion(),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(18, 14, 18, 18),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('¡Listo para registrar hoy!', style: TextStyle(
                fontFamily: 'PlusJakartaSans', fontSize: 18, fontWeight: FontWeight.w800,
                letterSpacing: -0.4, color: AppColors.ink900,
              )),
              const SizedBox(height: 4),
              Text(_fechaLarga(DateTime.now()), style: const TextStyle(
                fontFamily: 'Inter', fontSize: 12, height: 1.5, color: AppColors.ink500,
              )),
            ]),
          ),
        ]),
      ),
    );
  }

  Widget _modulos() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s5),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          const Text('Seguimiento diario', style: TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.ink900,
          )),
          TextButton(
            onPressed: () => onNuevoSeguimiento(null, null),
            style: TextButton.styleFrom(padding: EdgeInsets.zero, minimumSize: Size.zero),
            child: const Text('+ Nuevo'),
          ),
        ]),
        const SizedBox(height: AppSpacing.s2),
        Row(children: [
          for (int i = 0; i < usuario.modulos.length; i++) ...[
            if (i > 0) const SizedBox(width: AppSpacing.s2),
            Expanded(child: _ModuloCard(
              modulo: usuario.modulos[i],
              lotes: lotes.where((l) => l.modulo == usuario.modulos[i]).length,
              onTap: () => onNuevoSeguimiento(usuario.modulos[i], null),
            )),
          ],
        ]),
      ]),
    );
  }

  Widget _misLotes() {
    final visibles = lotes.take(3).toList();
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s5),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          const Text('Mis lotes', style: TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.ink900,
          )),
          TextButton(
            onPressed: onVerLotes,
            style: TextButton.styleFrom(padding: EdgeInsets.zero, minimumSize: Size.zero),
            child: const Text('Ver todos'),
          ),
        ]),
        const SizedBox(height: AppSpacing.s2),
        Container(
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.lg),
            border: Border.all(color: AppColors.line),
          ),
          clipBehavior: Clip.antiAlias,
          child: Column(children: [
            for (int i = 0; i < visibles.length; i++) ...[
              if (i > 0) Divider(height: 1, color: AppColors.line),
              _LoteRow(lote: visibles[i], onTap: () => onNuevoSeguimiento(visibles[i].modulo, visibles[i])),
            ],
          ]),
        ),
      ]),
    );
  }

  Widget _pendientes() {
    final offline = !sync.enLinea;
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.s4, 0, AppSpacing.s4, AppSpacing.s4),
      child: InkWell(
        onTap: onVerSync,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.lg),
            border: Border(
              left: BorderSide(color: offline ? AppColors.ink700 : AppColors.orange500, width: 3),
              top: BorderSide(color: AppColors.line),
              right: BorderSide(color: AppColors.line),
              bottom: BorderSide(color: AppColors.line),
            ),
          ),
          child: Row(children: [
            Container(
              width: 32, height: 32,
              decoration: BoxDecoration(
                color: offline ? AppColors.ink100 : AppColors.orange50,
                borderRadius: BorderRadius.circular(AppRadius.sm),
              ),
              child: Icon(offline ? Icons.wifi_off_rounded : Icons.schedule_rounded,
                size: 16, color: offline ? AppColors.ink700 : AppColors.orange600),
            ),
            const SizedBox(width: AppSpacing.s3),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(
                sync.pendientes == 1
                  ? '1 registro guardado aquí'
                  : '${sync.pendientes} registros guardados aquí',
                style: const TextStyle(
                  fontFamily: 'PlusJakartaSans', fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.ink900,
                ),
              ),
              const SizedBox(height: 2),
              Text(offline ? 'Se enviarán cuando vuelva la red' : 'Toca para revisar la cola',
                style: const TextStyle(fontFamily: 'Inter', fontSize: 12, color: AppColors.ink500)),
            ])),
            if (!offline)
              AppButton(label: 'Sincronizar', size: AppButtonSize.sm,
                variant: AppButtonVariant.accent, onPressed: sync.sincronizar),
          ]),
        ),
      ),
    );
  }
}

class _ModuloCard extends StatelessWidget {
  const _ModuloCard({required this.modulo, required this.lotes, required this.onTap});

  final ModuloSeguimiento modulo;
  final int lotes;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = switch (modulo) {
      ModuloSeguimiento.levante      => (AppColors.green50, AppColors.green600),
      ModuloSeguimiento.engorde      => (AppColors.orange50, AppColors.orange600),
      ModuloSeguimiento.produccion   => (AppColors.infoBg, const Color(0xFF3F668A)),
      ModuloSeguimiento.reproductora => (const Color(0xFFF3EAF3), const Color(0xFF7A4D7A)),
    };

    return Material(
      color: bg,
      borderRadius: BorderRadius.circular(AppRadius.lg),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 14),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(modulo.emoji, style: const TextStyle(fontSize: 22)),
            const SizedBox(height: 6),
            Text(modulo.label, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 13, fontWeight: FontWeight.w700,
              height: 1.2, color: AppColors.ink900,
            )),
            const SizedBox(height: 4),
            Text('$lotes lotes', style: TextStyle(
              fontFamily: 'Inter', fontSize: 11, fontWeight: FontWeight.w600, color: fg,
            )),
          ]),
        ),
      ),
    );
  }
}

class _LoteRow extends StatelessWidget {
  const _LoteRow({required this.lote, required this.onTap});

  final Lote lote;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = switch (lote.modulo) {
      ModuloSeguimiento.levante      => AppColors.levante,
      ModuloSeguimiento.engorde      => AppColors.engorde,
      ModuloSeguimiento.produccion   => AppColors.produccion,
      ModuloSeguimiento.reproductora => AppColors.reproductora,
    };

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.s4, vertical: AppSpacing.s3),
        child: Row(children: [
          Container(width: 8, height: 8, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
          const SizedBox(width: AppSpacing.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(lote.nombre, style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.ink900,
            )),
            Text('${lote.granja} · Día ${lote.dia}', style: const TextStyle(
              fontFamily: 'Inter', fontSize: 11, color: AppColors.ink500,
            )),
          ])),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Text(_fmtMiles(lote.aves), style: const TextStyle(
              fontFamily: 'PlusJakartaSans', fontSize: 13, fontWeight: FontWeight.w700,
              color: AppColors.ink900, fontFeatures: [FontFeature.tabularFigures()],
            )),
            Text(lote.modulo.label, style: TextStyle(
              fontFamily: 'Inter', fontSize: 10, fontWeight: FontWeight.w600, color: color,
            )),
          ]),
          const SizedBox(width: AppSpacing.s2),
          const Icon(Icons.chevron_right_rounded, size: 18, color: AppColors.ink200),
        ]),
      ),
    );
  }
}

String _fmtMiles(int n) => n.toString().replaceAllMapped(
  RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]}.');

String _fechaLarga(DateTime d) {
  const dias = ['lunes','martes','miércoles','jueves','viernes','sábado','domingo'];
  const meses = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];
  final dia = dias[d.weekday - 1];
  return '${dia[0].toUpperCase()}${dia.substring(1)} ${d.day} de ${meses[d.month - 1]}, ${d.year}';
}
