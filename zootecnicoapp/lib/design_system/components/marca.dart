/// Identidad de marca — el bloque de logos.
///
/// Réplica del `logo-stack` del login web
/// (`frontend/src/app/features/auth/login/login.component.html`), que es la
/// referencia de marca validada: logo Italcol arriba, logo SanMarino debajo,
/// un divisor con el degradado de marca y el tagline.
///
/// ── Por qué existe este archivo ──────────────────────────────────────────────
/// La app venía mostrando `logo-italfoods-zootecnico.png`, un logo que **el web
/// no usa en ninguna parte** (0 referencias medidas) y que se confundía con la
/// marca real. Se eliminó. Los dos assets que quedan son byte-idénticos a los
/// del web (mismo sha256), solo cambia el nombre del archivo:
///
///   app `italcol-naranja.png`  ==  web `italcol-naraanja.png`
///   app `logo-sanmarino.png`   ==  web `Logo-sanmarino-innovacion.png`
///
/// El rojo SanMarino del divisor es **identidad**, nunca acción: no reutilizar
/// ese color en botones (ver la regla de marca en `tokens/app_colors.dart`).
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';

/// Tagline de producto. Igual que el del web (`environment.appTagline`), sin la
/// mención a Italfoods que traía la app.
const String kTaglineApp = 'Gestión de granjas avícolas · Italcol';

/// Bloque de marca del login y de la cabecera de perfil.
class LogoMarca extends StatelessWidget {
  const LogoMarca({
    super.key,
    this.alturaItalcol = 34,
    this.alturaSanMarino = 44,
    this.mostrarTagline = true,
  });

  /// Alto del logo Italcol (primario).
  final double alturaItalcol;

  /// Alto del logo SanMarino (secundario). Va un poco más grande porque el
  /// isotipo tiene más aire vertical que el wordmark de Italcol.
  final double alturaSanMarino;

  final bool mostrarTagline;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Image.asset(
          'assets/images/brand/italcol-naranja.png',
          height: alturaItalcol,
          fit: BoxFit.contain,
          filterQuality: FilterQuality.high,
          semanticLabel: 'Italcol',
        ),
        const SizedBox(height: AppSpacing.s3),
        Image.asset(
          'assets/images/brand/logo-sanmarino.png',
          height: alturaSanMarino,
          fit: BoxFit.contain,
          filterQuality: FilterQuality.high,
          semanticLabel: 'San Marino',
        ),
        if (mostrarTagline) ...[
          const SizedBox(height: AppSpacing.s4),
          const DivisorMarca(),
          const SizedBox(height: AppSpacing.s3),
          Text(
            kTaglineApp,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontFamily: 'Inter',
              fontSize: AppFontSize.sm,
              height: 1.35,
              color: AppColors.ink500,
            ),
          ),
        ],
      ],
    );
  }
}

/// Divisor con el degradado de identidad Italcol → SanMarino.
///
/// Es el único lugar de la app donde aparece el rojo SanMarino junto al naranja,
/// igual que el borde superior dual del login web.
class DivisorMarca extends StatelessWidget {
  const DivisorMarca({super.key, this.ancho = 72, this.grosor = 3});

  final double ancho;
  final double grosor;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: ancho,
      height: grosor,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        gradient: const LinearGradient(colors: AppColors.identityGradient),
      ),
    );
  }
}

/// Marca de agua del isotipo, muy tenue, para el fondo del login.
///
/// El web usa `--wm-opacity: 0.07`; se replica ese valor.
class MarcaDeAgua extends StatelessWidget {
  const MarcaDeAgua({super.key, this.tamano = 320});

  final double tamano;

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: Opacity(
        opacity: 0.07,
        child: Image.asset(
          'assets/images/brand/v-logo.png',
          width: tamano,
          fit: BoxFit.contain,
          excludeFromSemantics: true,
        ),
      ),
    );
  }
}
