/// Paleta suavizada San Marino Zootécnico — app móvil
/// Derivada del logo Italfoods (naranja + verde + crema), bajada en saturación
/// para lectura prolongada en campo. NO usar los hex del web (#e85c25 / #2d7a3e).
import 'package:flutter/material.dart';

class AppColors {
  AppColors._();

  // ── Naranja acento (duraznillo) ──────────────────────────────────────────
  static const Color orange50  = Color(0xFFFDF4EE);
  static const Color orange100 = Color(0xFFF9E4D4);
  static const Color orange200 = Color(0xFFF2C8A7);
  static const Color orange300 = Color(0xFFEBA97C);
  static const Color orange400 = Color(0xFFE59059);
  static const Color orange500 = Color(0xFFE48254); // primario acento
  static const Color orange600 = Color(0xFFC66B3F);
  static const Color orange700 = Color(0xFF9E5331);

  // ── Verde campo claro ────────────────────────────────────────────────────
  static const Color green50  = Color(0xFFF0F6F1);
  static const Color green100 = Color(0xFFDCE9DE);
  static const Color green200 = Color(0xFFB9D3BE);
  static const Color green300 = Color(0xFF8FB99A);
  static const Color green400 = Color(0xFF6CA17A);
  static const Color green500 = Color(0xFF4F8A60); // primario
  static const Color green600 = Color(0xFF3F7350);
  static const Color green700 = Color(0xFF305B3E);

  // ── Neutros cálidos (crema, no gris frío) ────────────────────────────────
  static const Color cream   = Color(0xFFFBF8F3); // fondo base de toda la app
  static const Color cream2  = Color(0xFFF4EFE6); // chips inactivos, superficies 2
  static const Color surface = Color(0xFFFFFFFF);
  static const Color ink900  = Color(0xFF1E2620); // texto principal
  static const Color ink700  = Color(0xFF3A4640);
  static const Color ink500  = Color(0xFF6B736F);
  static const Color ink300  = Color(0xFFA5ABA7);
  static const Color ink200  = Color(0xFFD6D9D7);
  static const Color ink100  = Color(0xFFECEEEC);

  /// Divisores muy sutiles. Nunca usar Colors.grey.
  static Color get line       => ink900.withValues(alpha: 0.08);
  static Color get lineStrong => ink900.withValues(alpha: 0.14);

  // ── Semánticos suaves ────────────────────────────────────────────────────
  static const Color success   = Color(0xFF4F8A60);
  static const Color successBg = Color(0xFFE7F0E9);
  static const Color warning   = Color(0xFFD9A445);
  static const Color warningBg = Color(0xFFFAF1DC);
  static const Color danger    = Color(0xFFC25B4E);
  static const Color dangerBg  = Color(0xFFF8E3DF);
  static const Color info      = Color(0xFF5A85A6);
  static const Color infoBg    = Color(0xFFE5EEF6);

  // ── Color por módulo de seguimiento ──────────────────────────────────────
  static const Color levante      = green500;
  static const Color engorde      = orange500;
  static const Color produccion   = Color(0xFF5A85A6);
  static const Color reproductora = Color(0xFF9B6B9B);

  // ── Sexo (usado en los pares ♀/♂) ────────────────────────────────────────
  static const Color hembra = Color(0xFFC66B3F);
  static const Color macho  = Color(0xFF3F668A);

  // ── Sombras cálidas (nunca negro puro) ───────────────────────────────────
  static List<BoxShadow> get shadowSm => [
    BoxShadow(color: ink900.withValues(alpha: 0.05), blurRadius: 2, offset: const Offset(0, 1)),
  ];
  static List<BoxShadow> get shadowMd => [
    BoxShadow(color: ink900.withValues(alpha: 0.06), blurRadius: 12, offset: const Offset(0, 4)),
    BoxShadow(color: ink900.withValues(alpha: 0.04), blurRadius: 3, offset: const Offset(0, 1)),
  ];
  static List<BoxShadow> get shadowLg => [
    BoxShadow(color: ink900.withValues(alpha: 0.08), blurRadius: 28, offset: const Offset(0, 12)),
    BoxShadow(color: ink900.withValues(alpha: 0.04), blurRadius: 10, offset: const Offset(0, 4)),
  ];
}
