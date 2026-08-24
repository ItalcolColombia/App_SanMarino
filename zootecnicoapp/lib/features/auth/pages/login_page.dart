/// Ingreso a la app.
///
/// Réplica móvil del login web (`frontend/src/app/features/auth/login/`), que es
/// la referencia de marca validada: el logo-stack con su divisor y tagline, una
/// tarjeta con la cinta de identidad Italcol → SanMarino arriba, inputs con
/// icono a la izquierda que se enciende con el foco, y el CTA naranja.
///
/// Acá viven además las piezas compartidas con la pantalla de recuperación
/// ([FondoAuth], [TarjetaAuth], [AvisoAuth], [CampoAuth]). Esta es la pantalla
/// canónica del par, así que la recuperación las importa desde acá en vez de
/// duplicarlas.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/components/marca.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/auth/pages/recovery_page.dart';

/// Ancho máximo de la tarjeta. El web usa 400 px; en móvil la tarjeta ocupa todo
/// el ancho disponible salvo en tablets, donde este tope evita la línea larga.
const double _anchoTarjeta = 420;

class LoginPage extends StatefulWidget {
  const LoginPage({
    super.key,
    required this.onLogin,
    required this.auth,
    this.mensajeInicial,
  });

  /// Recibe el usuario ya autenticado. El shell se encarga de bajar módulos y lotes.
  final ValueChanged<Usuario> onLogin;
  final AuthApi auth;

  /// Motivo por el que se volvió a esta pantalla (p. ej. el token venció con cola
  /// pendiente). Se muestra arriba para que el usuario sepa qué pasó.
  final String? mensajeInicial;

  @override
  State<LoginPage> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginPage> {
  final _email = TextEditingController();
  final _pass = TextEditingController();
  bool _verPass = false;
  bool _recordarme = true;
  bool _cargando = false;
  String? _error;

  /// Cómo se pinta el aviso. Existe porque no todos los fallos son un error:
  /// quedarse sin red es un modo válido de la app y no puede leerse como alarma,
  /// mientras que una credencial inválida sí. El texto lo sigue decidiendo la
  /// lógica de [_entrar]; esto solo elige color, icono y título.
  TonoAviso _tono = TonoAviso.atencion;

  @override
  void initState() {
    super.initState();
    _error = widget.mensajeInicial;
  }

  @override
  void dispose() { _email.dispose(); _pass.dispose(); super.dispose(); }

  Future<void> _entrar() async {
    // Guarda de doble envío: el botón se deshabilita mientras carga, pero un
    // doble toque rápido alcanzaba a disparar dos logins antes del repintado.
    if (_cargando) return;

    if (_email.text.isEmpty || _pass.text.isEmpty) {
      setState(() {
        _error = 'Ingresa tu correo y contraseña.';
        _tono = TonoAviso.atencion;
      });
      return;
    }
    setState(() { _cargando = true; _error = null; });

    try {
      final usuario = await widget.auth.login(email: _email.text, password: _pass.text);
      if (!mounted) return;

      // Sin empresa-país resuelta, el backend no le da scope a ninguna consulta:
      // dejarlo entrar sería mostrarle una app vacía sin explicarle por qué.
      if (!usuario.puedeRegistrar) {
        setState(() {
          _cargando = false;
          _error = 'Tu usuario no tiene una empresa asignada. Pedí que te la '
              'asignen desde la web para poder registrar.';
          _tono = TonoAviso.atencion;
        });
        return;
      }

      widget.onLogin(usuario);
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _cargando = false;
        _error = switch (e.tipo) {
          // Es el único endpoint sin token: acá un 401 son credenciales, no sesión.
          TipoFallo.sesionVencida => 'Correo o contraseña incorrectos.',
          TipoFallo.sinRed =>
            'Sin conexión con el servidor. La primera vez necesitás red para entrar.',
          _ => e.mensaje,
        };
        _tono = switch (e.tipo) {
          TipoFallo.sesionVencida => TonoAviso.credenciales,
          TipoFallo.sinRed => TonoAviso.sinConexion,
          _ => TonoAviso.problema,
        };
      });
    } catch (_) {
      if (!mounted) return;
      // Un fallo al descifrar significa llaves mal configuradas en el build.
      setState(() {
        _cargando = false;
        _error = 'No se pudo leer la respuesta del servidor. '
            'Revisá la configuración de la app.';
        _tono = TonoAviso.problema;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.cream,
      body: FondoAuth(
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.s5,
                vertical: AppSpacing.s7,
              ),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: _anchoTarjeta),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TarjetaAuth(
                      child: Column(
                        children: [
                          // Los índices dejan un escalonado corto: primero entra
                          // la marca y ~60 ms después el formulario.
                          const EntradaEscalonada(indice: 0, child: LogoMarca()),
                          const SizedBox(height: AppSpacing.s6),
                          EntradaEscalonada(indice: 2, child: _formulario()),
                        ],
                      ),
                    ),
                    const SizedBox(height: AppSpacing.s5),
                    EntradaEscalonada(indice: 4, child: _pie()),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _formulario() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const EncabezadoAuth(titulo: 'Iniciar sesión'),
      const SizedBox(height: AppSpacing.s5),

      // El aviso entra y sale con el mismo fade+slide del resto de la app en vez
      // de aparecer de golpe y empujar el formulario.
      CambioSuave(
        claveDeEstado: _error,
        child: _error == null
            ? const SizedBox(width: double.infinity)
            : Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.s4),
                child: AvisoAuth(mensaje: _error!, tono: _tono),
              ),
      ),

      CampoAuth(
        etiqueta: 'Correo electrónico',
        controller: _email,
        icono: Icons.mail_outline_rounded,
        placeholder: 'usuario@empresa.com',
        keyboardType: TextInputType.emailAddress,
        textInputAction: TextInputAction.next,
        autofillHints: const [AutofillHints.username, AutofillHints.email],
      ),
      const SizedBox(height: AppSpacing.s4),

      CampoAuth(
        etiqueta: 'Contraseña',
        controller: _pass,
        icono: Icons.lock_outline_rounded,
        placeholder: '••••••••',
        obscure: !_verPass,
        autofillHints: const [AutofillHints.password],
        sufijo: IconButton(
          onPressed: () => setState(() => _verPass = !_verPass),
          tooltip: _verPass ? 'Ocultar contraseña' : 'Mostrar contraseña',
          icon: Icon(
            _verPass ? Icons.visibility_off_outlined : Icons.visibility_outlined,
            size: 19,
            color: AppColors.ink500,
          ),
        ),
      ),
      const SizedBox(height: AppSpacing.s3),

      // "Recordarme" solo en su fila y el enlace debajo del CTA. Compartir una
      // fila obligaba a truncar el enlace ("¿Olvidaste tu contrase…"): los dos
      // textos no entran juntos en el ancho de la tarjeta, ni en un teléfono.
      Align(alignment: Alignment.centerLeft, child: _recordarmeControl()),
      const SizedBox(height: AppSpacing.s4),

      AppButton(
        // Naranja Italcol: es la acción primaria de la pantalla.
        variant: AppButtonVariant.primary,
        label: _cargando ? 'Verificando…' : 'Entrar',
        size: AppButtonSize.lg,
        full: true,
        loading: _cargando,
        onPressed: _cargando ? null : _entrar,
      ),
      const SizedBox(height: AppSpacing.s2),

      Center(
        child: TextButton(
          onPressed: _cargando
              ? null
              : () => Navigator.of(context).push(
                    rutaApp((_) => const RecoveryPage(), nombre: 'recuperar-contrasena'),
                  ),
          child: const Text('¿Olvidaste tu contraseña?'),
        ),
      ),
      const SizedBox(height: AppSpacing.s2),

      const Text(
        '¿Necesitas acceso? Contacta a tu administrador',
        textAlign: TextAlign.center,
        style: TextStyle(
          fontFamily: 'Inter',
          fontSize: AppFontSize.xs,
          height: 1.5,
          color: AppColors.ink500,
        ),
      ),
    ],
  );

  /// "Recordarme" con toda la fila tocable: el supervisor lo marca con guantes y
  /// el cuadrito de 22 px solo no llega al mínimo táctil.
  Widget _recordarmeControl() => InkWell(
    onTap: () => setState(() => _recordarme = !_recordarme),
    borderRadius: BorderRadius.circular(AppRadius.xs),
    child: Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.s2),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: 22,
            height: 22,
            child: Checkbox(
              value: _recordarme,
              onChanged: (v) => setState(() => _recordarme = v ?? false),
            ),
          ),
          const SizedBox(width: AppSpacing.s2),
          const Text('Recordarme', style: TextStyle(
            fontFamily: 'Inter', fontSize: AppFontSize.sm, color: AppColors.ink700,
          )),
        ],
      ),
    ),
  );

  /// Pie del web: solo el copyright. El logo Italcol ya está arriba en el
  /// logo-stack, repetirlo abajo era ruido de marca.
  Widget _pie() => Text(
    '© ${DateTime.now().year} San Marino Zootécnico · Todos los derechos reservados',
    textAlign: TextAlign.center,
    style: const TextStyle(
      fontFamily: 'Inter',
      fontSize: AppFontSize.xs,
      height: 1.5,
      color: AppColors.ink300,
    ),
  );
}

// ═══════════════════════════════════════════════════════════════════════════
// PIEZAS COMPARTIDAS DE AUTENTICACIÓN (login + recuperación)
// ═══════════════════════════════════════════════════════════════════════════

/// Fondo de las pantallas de autenticación.
///
/// Réplica del `.login-shell` del web: crema con un halo naranja muy tenue
/// arriba y la marca de agua del isotipo detrás de la tarjeta.
class FondoAuth extends StatelessWidget {
  const FondoAuth({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        Positioned.fill(
          child: DecoratedBox(
            decoration: BoxDecoration(
              gradient: RadialGradient(
                center: const Alignment(0, -1),
                radius: 1.1,
                colors: [AppColors.brand500.withValues(alpha: 0.10), AppColors.cream],
                stops: const [0, 0.75],
              ),
            ),
          ),
        ),
        const Positioned.fill(child: Center(child: MarcaDeAgua())),
        child,
      ],
    );
  }
}

/// Tarjeta blanca con la cinta de identidad arriba.
///
/// La cinta es el borde superior de 4 px de la card del web: el único lugar
/// donde el rojo SanMarino convive con el naranja, y es identidad — nunca acción.
class TarjetaAuth extends StatelessWidget {
  const TarjetaAuth({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.line),
        boxShadow: AppColors.shadowLg,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              height: AppSpacing.s1,
              decoration: const BoxDecoration(
                gradient: LinearGradient(colors: AppColors.identityGradient),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(AppSpacing.s6),
              child: child,
            ),
          ],
        ),
      ),
    );
  }
}

/// Título de sección del formulario — el `.form-heading` del web: versalitas
/// centradas con una barrita de acento debajo.
class EncabezadoAuth extends StatelessWidget {
  const EncabezadoAuth({super.key, required this.titulo});

  final String titulo;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          titulo.toUpperCase(),
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontFamily: 'PlusJakartaSans',
            fontSize: AppFontSize.sm,
            fontWeight: FontWeight.w800,
            letterSpacing: 1.2,
            color: AppColors.ink500,
          ),
        ),
        const SizedBox(height: AppSpacing.s2),
        Container(
          width: AppSpacing.s6,
          height: 2,
          decoration: BoxDecoration(
            color: AppColors.brand500,
            borderRadius: BorderRadius.circular(AppRadius.pill),
          ),
        ),
      ],
    );
  }
}

/// Qué clase de aviso se le muestra al usuario.
///
/// El web separa el error por `errorType` justamente porque no todos pesan igual;
/// acá se replica esa separación con lo que la app puede distinguir de verdad.
enum TonoAviso {
  /// Sin red. **No es un error**: la app funciona offline y el aviso tiene que
  /// leerse como información, nunca como alarma. Por eso no lleva rojo.
  sinConexion,

  /// Correo o contraseña incorrectos. Esto sí es peligro: el usuario no entra.
  credenciales,

  /// Algo que el usuario tiene que corregir o pedir (campos vacíos, sin empresa
  /// asignada, sesión vencida).
  atencion,

  /// Falla del servidor o de la configuración de la app.
  problema,
}

/// Franja de aviso del formulario. Réplica del `.alert-error` del web: barra de
/// color a la izquierda, título y mensaje.
///
/// El color vive en el icono y en la barra; el texto va en tinta neutra para que
/// se lea igual de bien bajo sol en los cuatro tonos.
class AvisoAuth extends StatelessWidget {
  const AvisoAuth({super.key, required this.mensaje, this.tono = TonoAviso.atencion});

  final String mensaje;
  final TonoAviso tono;

  @override
  Widget build(BuildContext context) {
    final (fondo, acento, icono, titulo) = switch (tono) {
      TonoAviso.sinConexion => (
        AppColors.infoBg, AppColors.info, Icons.cloud_off_rounded, 'Sin conexión',
      ),
      TonoAviso.credenciales => (
        AppColors.dangerBg, AppColors.danger, Icons.lock_outline_rounded, 'Credenciales inválidas',
      ),
      TonoAviso.atencion => (
        AppColors.warningBg, AppColors.warning, Icons.info_outline_rounded, 'Revisá esto',
      ),
      TonoAviso.problema => (
        AppColors.dangerBg, AppColors.danger, Icons.error_outline_rounded, 'No se pudo entrar',
      ),
    };

    return SizedBox(
      width: double.infinity,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.sm),
        child: ColoredBox(
          color: fondo,
          // La barra va como hijo y no como `Border(left:)`: un borde no uniforme
          // con `borderRadius` revienta en tiempo de ejecución.
          child: IntrinsicHeight(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ColoredBox(color: acento, child: const SizedBox(width: AppSpacing.s1)),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.all(AppSpacing.s3),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(icono, size: 18, color: acento),
                        const SizedBox(width: AppSpacing.s2),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(titulo, style: const TextStyle(
                                fontFamily: 'PlusJakartaSans',
                                fontSize: AppFontSize.sm,
                                fontWeight: FontWeight.w700,
                                color: AppColors.ink900,
                              )),
                              const SizedBox(height: 2),
                              Text(mensaje, style: const TextStyle(
                                fontFamily: 'Inter',
                                fontSize: AppFontSize.sm,
                                height: 1.45,
                                color: AppColors.ink700,
                              )),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Input de autenticación: etiqueta arriba e icono a la izquierda que se pone
/// naranja al enfocar, igual que el `:focus-within .input-icon` del web.
///
/// No usa [AppField] porque la primitiva no acepta icono de prefijo y no es de
/// este alcance modificarla; el resto (etiqueta, asterisco naranja, borde y foco)
/// sale del mismo tema.
class CampoAuth extends StatefulWidget {
  const CampoAuth({
    super.key,
    required this.etiqueta,
    required this.controller,
    required this.icono,
    this.placeholder,
    this.keyboardType,
    this.textInputAction,
    this.autofillHints,
    this.obscure = false,
    this.sufijo,
  });

  final String etiqueta;
  final TextEditingController controller;
  final IconData icono;
  final String? placeholder;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final Iterable<String>? autofillHints;
  final bool obscure;
  final Widget? sufijo;

  @override
  State<CampoAuth> createState() => _CampoAuthState();
}

class _CampoAuthState extends State<CampoAuth> {
  final FocusNode _foco = FocusNode();

  @override
  void initState() {
    super.initState();
    _foco.addListener(_repintar);
  }

  void _repintar() => setState(() {});

  @override
  void dispose() {
    _foco.removeListener(_repintar);
    _foco.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(children: [
          Text(widget.etiqueta, style: const TextStyle(
            fontFamily: 'Inter',
            fontSize: 12,
            fontWeight: FontWeight.w600,
            letterSpacing: 0.3,
            color: AppColors.ink700,
          )),
          const Text(' *', style: TextStyle(
            fontFamily: 'Inter', fontSize: 12, fontWeight: FontWeight.w700, color: AppColors.brand500,
          )),
        ]),
        const SizedBox(height: 6),
        TextField(
          controller: widget.controller,
          focusNode: _foco,
          obscureText: widget.obscure,
          keyboardType: widget.keyboardType,
          textInputAction: widget.textInputAction,
          autofillHints: widget.autofillHints,
          style: const TextStyle(
            fontFamily: 'Inter',
            fontSize: AppFontSize.base,
            color: AppColors.ink900,
          ),
          decoration: InputDecoration(
            hintText: widget.placeholder,
            prefixIcon: Padding(
              padding: const EdgeInsets.only(left: 14, right: AppSpacing.s2),
              child: TweenAnimationBuilder<Color?>(
                tween: ColorTween(
                  end: _foco.hasFocus ? AppColors.brand500 : AppColors.ink300,
                ),
                duration: AppMotion.duracion(context, AppMotion.fast),
                curve: AppMotion.tactil,
                builder: (_, color, _) => Icon(widget.icono, size: 19, color: color),
              ),
            ),
            prefixIconConstraints: const BoxConstraints(minWidth: 0, minHeight: 0),
            suffixIcon: widget.sufijo,
          ),
        ),
      ],
    );
  }
}
