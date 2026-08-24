/// Escalas de espaciado, radios y tipografía.
/// Espaciado en escala 4pt — no inventar valores intermedios.
class AppSpacing {
  AppSpacing._();

  static const double s0  = 0;
  static const double s1  = 4;
  static const double s2  = 8;
  static const double s3  = 12;
  static const double s4  = 16; // padding lateral por defecto de pantalla
  static const double s5  = 20;
  static const double s6  = 24;
  static const double s7  = 32;
  static const double s8  = 40;
  static const double s9  = 48;
  static const double s10 = 64;
}

class AppRadius {
  AppRadius._();

  static const double xs   = 6;
  static const double sm   = 10;
  static const double md   = 14; // inputs, botones
  static const double lg   = 18; // cards
  static const double xl   = 24; // hero, bottom nav
  static const double xxl  = 32;
  static const double pill = 9999;
}

class AppFontSize {
  AppFontSize._();

  static const double xs   = 11;
  static const double sm   = 13;
  static const double base = 15;
  static const double md   = 17;
  static const double lg   = 20;
  static const double xl   = 24;
  static const double xxl  = 30;
  static const double xxxl = 38;
}

class AppDuration {
  AppDuration._();

  static const Duration fast = Duration(milliseconds: 120);
  static const Duration base = Duration(milliseconds: 200);
  static const Duration slow = Duration(milliseconds: 320);
}

/// Touch target mínimo — el supervisor usa la app con guantes bajo sol fuerte.
class AppTouch {
  AppTouch._();
  static const double min = 44;
}
