/// Tokens de movimiento — duraciones y curvas.
///
/// ÚNICA fuente de animación de la app: ninguna pantalla define un
/// `Duration(milliseconds: …)` suelto ni una curva propia.
///
/// Criterio: el movimiento **orienta**, no decora. La app se usa parada en un
/// galpón, con guantes y a veces con una sola mano — una transición larga es
/// tiempo perdido. Nada supera los 320 ms.
///
/// Accesibilidad: `AppMotion.duracion(context, …)` devuelve `Duration.zero`
/// cuando el sistema pide reducir animaciones (`MediaQuery.disableAnimations`,
/// que en iOS/Android refleja "Reducir movimiento"). Usalo siempre que la
/// animación no sea imprescindible para entender la pantalla.
library;

import 'package:flutter/material.dart';

class AppMotion {
  AppMotion._();

  // ══ DURACIONES ═══════════════════════════════════════════════════════════
  /// Realimentación inmediata: presionar un botón, un check.
  static const Duration instant = Duration(milliseconds: 120);

  /// Cambios de estado dentro de una pantalla: chips, switches, tabs.
  static const Duration fast = Duration(milliseconds: 200);

  /// Entradas y salidas: listas, secciones, ribbons.
  static const Duration base = Duration(milliseconds: 240);

  /// Navegación entre pantallas.
  static const Duration page = Duration(milliseconds: 260);

  /// Movimientos amplios: hojas modales, expandir formularios largos.
  static const Duration slow = Duration(milliseconds: 320);

  /// Ciclo del shimmer de carga.
  static const Duration shimmer = Duration(milliseconds: 1200);

  /// Retardo entre ítems de una lista escalonada.
  static const Duration stagger = Duration(milliseconds: 30);

  /// Tope de ítems que se escalonan. Más allá el usuario ya no percibe el
  /// escalonado y solo siente que la lista tarda.
  static const int staggerMax = 6;

  // ══ CURVAS ═══════════════════════════════════════════════════════════════
  /// Entradas. Arranca rápido y frena — se siente ágil.
  static const Curve entrada = Curves.easeOutCubic;

  /// Salidas. Arranca suave y acelera al irse.
  static const Curve salida = Curves.easeInCubic;

  /// Cambios simétricos: expandir/colapsar.
  static const Curve simetrica = Curves.easeInOut;

  /// Realimentación táctil.
  static const Curve tactil = Curves.easeOut;

  /// Rebote sutil para confirmaciones (guardado OK). Sin exagerar.
  static const Curve confirmacion = Curves.easeOutBack;

  // ══ DESPLAZAMIENTOS ══════════════════════════════════════════════════════
  /// Cuánto sube una pantalla nueva al entrar.
  static const double offsetPagina = 12;

  /// Cuánto se desplaza el contenido al cambiar de tab.
  static const double offsetTab = 8;

  /// Escala del botón presionado.
  static const double escalaPresionado = 0.97;

  // ══ ACCESIBILIDAD ════════════════════════════════════════════════════════
  /// La duración a usar, respetando "Reducir movimiento" del sistema.
  ///
  /// ```dart
  /// AnimatedOpacity(duration: AppMotion.duracion(context, AppMotion.fast), …)
  /// ```
  static Duration duracion(BuildContext context, Duration deseada) =>
      MediaQuery.maybeDisableAnimationsOf(context) ?? false ? Duration.zero : deseada;

  /// `true` si el sistema pide reducir movimiento. Para saltear animaciones
  /// decorativas enteras (la gallina del home, el shimmer).
  static bool reducido(BuildContext context) =>
      MediaQuery.maybeDisableAnimationsOf(context) ?? false;
}
