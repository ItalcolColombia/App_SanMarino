/// Tokens de color — San Marino Zootécnico (app móvil).
///
/// ÚNICA fuente de color de la app. Ninguna pantalla define `Color(0x…)` propio.
///
/// ── REGLA DE MARCA (vinculante, `CLAUDE.md` de la raíz) ──────────────────────
///   naranja  → ACCIONES (botones, FAB, nav activo, links)
///   verde    → SOLO éxito  (+ color categórico del módulo Levante)
///   rojo     → SOLO peligro/destructivo
///   rojo SanMarino → SOLO identidad de marca (logo, borde de marca). NUNCA acción.
///   dorado   → acentos, badges, highlights
///
/// Los hex de marca salen del front web, que es la referencia validada:
/// `frontend/src/app/features/auth/login/login.component.scss`.
/// (El `CLAUDE.md` de la raíz cita `#e85c25`; está desactualizado — el código
/// real del web y la memoria del proyecto usan `#F5821F`.)
///
/// Los NEUTROS son propios de la app, más cálidos y bajados en saturación para
/// lectura prolongada bajo sol. Es una decisión deliberada: no reemplazarlos por
/// los del web sin una razón medida.
library;

import 'package:flutter/material.dart';

class AppColors {
  AppColors._();

  // ══ MARCA ITALCOL — naranja = ACCIONES ═══════════════════════════════════
  static const Color brand50 = Color(0xFFFEF4E9);
  static const Color brand100 = Color(0xFFFDE5C9);
  static const Color brand200 = Color(0xFFFACB93);
  static const Color brand300 = Color(0xFFF8AF5C);
  static const Color brand400 = Color(0xFFF69835);
  static const Color brand500 = Color(0xFFF5821F); // CTA / acción primaria
  static const Color brand600 = Color(0xFFE86F12); // hover
  static const Color brand700 = Color(0xFFC85A0E); // pressed

  /// Tinte de acción: fondo de chips/estados seleccionados. `--brand-orange-light`.
  static Color get brandTint => brand500.withValues(alpha: 0.14);

  // ══ DORADO — acento del logo ═════════════════════════════════════════════
  static const Color gold400 = Color(0xFFFDB813);
  static const Color gold500 = Color(0xFFFBB040);
  static const Color gold600 = Color(0xFFE89A1E);

  // ══ ROJO SANMARINO — SOLO identidad de marca ═════════════════════════════
  // Nunca en botones, tabs, acciones ni como peligro. Va en el logo-stack y en
  // el borde/ribbon de marca. Ver memoria `paleta-marca-italcol`.
  static const Color sanMarinoRed = Color(0xFFD2181E);
  static const Color sanMarinoRedDeep = Color(0xFF991918);

  /// Gradiente de marca del logo: dorado → naranja. **Sin rojo.**
  static const List<Color> brandGradient = [gold400, brand500];

  /// Ribbon dual Italcol → SanMarino (identidad, no acción).
  static const List<Color> identityGradient = [brand500, sanMarinoRed];

  // ══ VERDE — SOLO éxito (+ categórico de Levante) ═════════════════════════
  // Dejó de ser `primary`: esa es la regla de marca. Se conservan los valores
  // cálidos de la app porque leen bien sobre crema.
  static const Color green50 = Color(0xFFF0F6F1);
  static const Color green100 = Color(0xFFDCE9DE);
  static const Color green200 = Color(0xFFB9D3BE);
  static const Color green300 = Color(0xFF8FB99A);
  static const Color green400 = Color(0xFF6CA17A);
  static const Color green500 = Color(0xFF4F8A60);
  static const Color green600 = Color(0xFF3F7350);
  static const Color green700 = Color(0xFF305B3E);

  // ══ NEUTROS CÁLIDOS — no se tocan (legibilidad en campo) ═════════════════
  static const Color cream = Color(0xFFFBF8F3); // fondo base de toda la app
  static const Color cream2 = Color(0xFFF4EFE6); // chips inactivos, superficie 2
  static const Color surface = Color(0xFFFFFFFF);
  static const Color ink900 = Color(0xFF1E2620); // texto principal
  static const Color ink700 = Color(0xFF3A4640);
  static const Color ink500 = Color(0xFF6B736F);
  static const Color ink300 = Color(0xFFA5ABA7);
  static const Color ink200 = Color(0xFFD6D9D7);
  static const Color ink100 = Color(0xFFECEEEC);

  /// Divisores muy sutiles. Nunca usar `Colors.grey`.
  static Color get line => ink900.withValues(alpha: 0.08);
  static Color get lineStrong => ink900.withValues(alpha: 0.14);

  // ══ SEMÁNTICOS ═══════════════════════════════════════════════════════════
  static const Color success = green500;
  static const Color successBg = Color(0xFFE7F0E9);
  static const Color warning = Color(0xFFD9A445);
  static const Color warningBg = Color(0xFFFAF1DC);

  /// Peligro/destructivo. Alineado al web (`--brand-red`). Nunca para marca.
  static const Color danger = Color(0xFFDC2626);
  static const Color dangerBg = Color(0xFFFBE9E9);
  static const Color info = Color(0xFF5A85A6);
  static const Color infoBg = Color(0xFFE5EEF6);

  // ══ COLOR CATEGÓRICO POR MÓDULO ══════════════════════════════════════════
  // Eje distinto del semántico: identifica el módulo, no un estado. Por eso
  // Levante puede ser verde sin violar la regla de marca.
  static const Color levante = green500;
  static const Color engorde = brand500;
  static const Color produccion = Color(0xFF5A85A6);
  static const Color reproductora = Color(0xFF9B6B9B);

  // ══ SEXO (pares ♀/♂) ═════════════════════════════════════════════════════
  static const Color hembra = Color(0xFFC66B3F);
  static const Color macho = Color(0xFF3F668A);

  // ══ ELEVACIÓN — sombras cálidas, nunca negro puro ════════════════════════
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

  /// Sombra teñida de marca — solo para el CTA principal y el FAB.
  static List<BoxShadow> get shadowBrand => [
    BoxShadow(color: brand500.withValues(alpha: 0.32), blurRadius: 16, offset: const Offset(0, 6)),
  ];
}
