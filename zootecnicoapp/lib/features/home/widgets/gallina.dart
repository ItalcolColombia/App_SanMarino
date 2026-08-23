/// Animación gallina + huevo del home — dibujada con CustomPainter, sin assets.
///
/// Es decorativa: si el sistema pide reducir movimiento, el controlador no se
/// anima (ver `AppMotion.reducido`).
library;

import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';


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
      ..color = AppColors.brand500
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
    p.color = AppColors.brand500;
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
    p.color = AppColors.brand500.withValues(alpha: 0.75);
    canvas.drawOval(Rect.fromCenter(center: const Offset(-10, 13), width: 12, height: 16), p);

    // Pico
    p.color = AppColors.brand500;
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

