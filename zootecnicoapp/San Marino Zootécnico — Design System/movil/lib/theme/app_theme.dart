import 'package:flutter/material.dart';
import 'app_colors.dart';
import 'app_spacing.dart';

/// Tema global. Display = Plus Jakarta Sans · Texto = Inter.
class AppTheme {
  AppTheme._();

  static const String fontDisplay = 'PlusJakartaSans';
  static const String fontText = 'Inter';

  static ThemeData get light => ThemeData(
    useMaterial3: true,
    scaffoldBackgroundColor: AppColors.cream,
    fontFamily: fontText,
    colorScheme: const ColorScheme.light(
      primary: AppColors.green500,
      onPrimary: Colors.white,
      secondary: AppColors.orange500,
      onSecondary: Colors.white,
      surface: AppColors.surface,
      onSurface: AppColors.ink900,
      error: AppColors.danger,
      onError: Colors.white,
    ),

    textTheme: const TextTheme(
      // Display — Plus Jakarta Sans
      displayLarge:   TextStyle(fontFamily: fontDisplay, fontSize: AppFontSize.xxxl, fontWeight: FontWeight.w800, height: 1.15, letterSpacing: -0.7, color: AppColors.ink900),
      displayMedium:  TextStyle(fontFamily: fontDisplay, fontSize: AppFontSize.xxl,  fontWeight: FontWeight.w800, height: 1.15, letterSpacing: -0.6, color: AppColors.ink900),
      displaySmall:   TextStyle(fontFamily: fontDisplay, fontSize: AppFontSize.xl,   fontWeight: FontWeight.w700, height: 1.3,  letterSpacing: -0.5, color: AppColors.ink900),
      headlineMedium: TextStyle(fontFamily: fontDisplay, fontSize: AppFontSize.lg,   fontWeight: FontWeight.w700, height: 1.3,  color: AppColors.ink900),
      headlineSmall:  TextStyle(fontFamily: fontDisplay, fontSize: AppFontSize.md,   fontWeight: FontWeight.w600, height: 1.3,  color: AppColors.ink900),
      titleMedium:    TextStyle(fontFamily: fontDisplay, fontSize: AppFontSize.base, fontWeight: FontWeight.w700, color: AppColors.ink900),
      // Texto — Inter
      bodyLarge:  TextStyle(fontFamily: fontText, fontSize: AppFontSize.base, height: 1.5, color: AppColors.ink700),
      bodyMedium: TextStyle(fontFamily: fontText, fontSize: AppFontSize.sm,   height: 1.5, color: AppColors.ink700),
      bodySmall:  TextStyle(fontFamily: fontText, fontSize: AppFontSize.xs,   height: 1.4, color: AppColors.ink500),
      labelLarge: TextStyle(fontFamily: fontText, fontSize: 14, fontWeight: FontWeight.w600, color: AppColors.ink900),
      labelMedium:TextStyle(fontFamily: fontText, fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.ink700),
      labelSmall: TextStyle(fontFamily: fontText, fontSize: 10, fontWeight: FontWeight.w600, letterSpacing: 0.8, color: AppColors.ink500),
    ),

    cardTheme: CardThemeData(
      color: AppColors.surface,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(color: AppColors.line),
      ),
    ),

    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: AppColors.surface,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
      hintStyle: const TextStyle(fontFamily: fontText, fontSize: 15, color: AppColors.ink300),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        borderSide: BorderSide(color: AppColors.lineStrong),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        borderSide: BorderSide(color: AppColors.lineStrong),
      ),
      // El foco siempre es naranja — el acento marca "aquí estás escribiendo".
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        borderSide: const BorderSide(color: AppColors.orange500, width: 1.6),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        borderSide: const BorderSide(color: AppColors.danger),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        borderSide: const BorderSide(color: AppColors.danger, width: 1.6),
      ),
    ),

    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: AppColors.green500,
        foregroundColor: Colors.white,
        disabledBackgroundColor: AppColors.ink200,
        disabledForegroundColor: Colors.white,
        minimumSize: const Size(double.infinity, 48),
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
        textStyle: const TextStyle(fontFamily: fontText, fontWeight: FontWeight.w700, fontSize: 14),
      ),
    ),

    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.green600,
        minimumSize: const Size(0, 48),
        side: const BorderSide(color: AppColors.green200, width: 1.5),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
        textStyle: const TextStyle(fontFamily: fontText, fontWeight: FontWeight.w700, fontSize: 14),
      ),
    ),

    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: AppColors.orange600,
        textStyle: const TextStyle(fontFamily: fontText, fontWeight: FontWeight.w600, fontSize: 13),
      ),
    ),

    dividerTheme: DividerThemeData(color: AppColors.line, thickness: 1, space: 1),

    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: AppColors.cream,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
    ),

    appBarTheme: const AppBarTheme(
      backgroundColor: AppColors.surface,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      scrolledUnderElevation: 0,
      titleTextStyle: TextStyle(fontFamily: fontDisplay, fontSize: 18, fontWeight: FontWeight.w800, letterSpacing: -0.4, color: AppColors.ink900),
      iconTheme: IconThemeData(color: AppColors.ink700),
    ),

    checkboxTheme: CheckboxThemeData(
      fillColor: WidgetStateProperty.resolveWith(
        (s) => s.contains(WidgetState.selected) ? AppColors.green500 : Colors.transparent,
      ),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
      side: BorderSide(color: AppColors.lineStrong, width: 1.5),
    ),

    switchTheme: SwitchThemeData(
      thumbColor: WidgetStateProperty.all(Colors.white),
      trackColor: WidgetStateProperty.resolveWith(
        (s) => s.contains(WidgetState.selected) ? AppColors.green500 : AppColors.ink200,
      ),
      trackOutlineColor: WidgetStateProperty.all(Colors.transparent),
    ),

    /// Sin bounces: la curva estándar es out-quart.
    pageTransitionsTheme: const PageTransitionsTheme(builders: {
      TargetPlatform.android: CupertinoPageTransitionsBuilder(),
      TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
    }),
  );

  /// Curva estándar del sistema — equivale a cubic-bezier(0.22, 1, 0.36, 1).
  static const Curve easeOut = Curves.easeOutQuart;
}
