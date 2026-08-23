/// Cola de sincronización: qué quedó pendiente de subir y en qué estado está.
///
/// Dos reglas de esta pantalla, cada una nacida de un bug medido:
///
///  1. **Mientras se lee la cola no se afirma nada.** El `FutureBuilder` miraba
///     `snap.data ?? []` sin mirar `connectionState`, así que el usuario que
///     abría la pantalla PORQUE tenía 12 pendientes veía primero el check verde
///     y "Todo sincronizado". Ahora `waiting` tiene su propio estado (esqueleto)
///     y el éxito solo se pinta cuando la consulta terminó y volvió vacía.
///
///  2. **La pantalla escucha al [SyncService].** Se abre con `Navigator.push`,
///     así que el `setState` del shell no la alcanza: sin escuchar, tocar
///     "Sincronizar ahora" no movía el contador, ni los puntos, ni el estado de
///     conexión (`enLinea` se leía una sola vez). El future además se recrea
///     cuando el service avisa, para que las filas desaparezcan al subir.
///
///  3. **Una fila agotada tiene que poder tocarse.** Pasados los 5 intentos la
///     fila sale de `porEnviar()` y la cola deja de reintentarla sola (I17),
///     pero seguía pintada como una pendiente más: punto naranja eterno, sin
///     decir qué pasó y sin nada que hacer. Ahora tienen su propia sección,
///     muestran el motivo que dio el servidor y se devuelven a la cola con
///     [SyncService.reintentar].
///
///  4. **Lo que el service ya sabe se dice.** `duplicados` y `rechazados` se
///     contaban en cada pasada y morían en memoria. Un día duplicado NO se
///     perdió; uno rechazado sí liberó la fecha y hay que volver a cargarlo —
///     es lo único de los dos que exige trabajo del operario.
library;

import 'dart:async';

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/sync/sync_service.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/motion/app_motion.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/sync/widgets/sync_widgets.dart';

/// Alto de un [AppButton] de tamaño `md`. El esqueleto lo replica para que la
/// tarjeta no dé un salto cuando termina de cargar.
const double _altoBoton = 48;

/// Qué está pasando, resuelto una sola vez por frame para que la tarjeta, la
/// lista y el botón no puedan contradecirse entre sí.
enum _Estado { cargando, falla, alDia, sinConexion, pendientes }

/// Lo que la pantalla necesita de SQLite, leído de una sola pasada.
///
/// `todas` es la cola entera —incluidas las agotadas—, que es lo que cuenta la
/// tarjeta de arriba: son todos registros que todavía no llegaron al servidor.
/// `agotadas` es el subconjunto que la cola ya no reintenta sola.
///
/// Van juntas a propósito: si cada sección leyera por su cuenta, entre una
/// lectura y la otra la misma fila podría aparecer en las dos a la vez.
typedef _LecturaCola = ({
  List<RegistroPendiente> todas,
  List<RegistroPendiente> agotadas,
});

class SyncPage extends StatefulWidget {
  const SyncPage({super.key, required this.sync});

  final SyncService sync;

  @override
  State<SyncPage> createState() => _SyncPageState();
}

class _SyncPageState extends State<SyncPage> {
  late Future<_LecturaCola> _cola;

  /// Última lectura buena de la cola. Al recrear el future, `FutureBuilder`
  /// vuelve a `waiting` **sin datos**: sin esta copia la lista parpadearía a
  /// esqueleto cada vez que sube una fila. El esqueleto queda para lo que de
  /// verdad no se sabe todavía — la primera lectura.
  _LecturaCola? _ultimaLectura;

  /// Lo que del service obliga a releer la base. Fuera de un envío en curso, el
  /// service avisa también por cosas que no tocan la cola; sin la firma cada
  /// aviso abriría una consulta a SQLite de más.
  late String _firma;

  @override
  void initState() {
    super.initState();
    _cola = _leerCola();
    _firma = _firmaActual();
    widget.sync.addListener(_alNotificarSync);
  }

  /// Lee la cola y se queda con el resultado. El `identical` descarta la lectura
  /// vieja que llegue tarde: durante un envío se dispara una por fila y no hay
  /// garantía de que resuelvan en orden.
  Future<_LecturaCola> _leerCola() {
    final lectura = _leerBase();
    lectura.then((datos) {
      if (!mounted || !identical(_cola, lectura)) return;
      setState(() => _ultimaLectura = datos);
    }).catchError((Object _) {
      // El fallo lo reporta el FutureBuilder sobre `lectura`; este `catchError`
      // solo evita que el derivado quede como error sin capturar.
    });
    return lectura;
  }

  /// La cola entera y, encadenada, la lista de agotadas. En serie y en este
  /// orden: si entre las dos consultas alguien devolviera una fila a la cola,
  /// el resultado sería mostrarla como pendiente (que es lo correcto), nunca
  /// duplicarla en las dos secciones.
  Future<_LecturaCola> _leerBase() async {
    final todas = await LocalDb.instance.pendientes();
    final agotadas = await widget.sync.agotadas();
    return (todas: todas, agotadas: agotadas);
  }

  /// Devuelve una fila agotada a la cola.
  ///
  /// Después se relee a mano **a propósito**: el service notifica, pero sin red
  /// no cambia ni la fase ni el contador de pendientes (una fila en `error`
  /// también cuenta), así que la firma queda igual y `_alNotificarSync`
  /// descarta el aviso. Sin esta relectura la fila seguiría en "Necesitan tu
  /// atención" aunque ya hubiera vuelto a la cola.
  Future<void> _reintentar(String id) async {
    await widget.sync.reintentar(id);
    if (!mounted) return;
    await _recargar();
  }

  /// Abre el detalle de una fila agotada. La hoja no toca el service: devuelve
  /// `true` si el usuario pidió reintentar y la decisión se ejecuta acá, con el
  /// `mounted` de la pantalla ya verificado.
  Future<void> _abrirAgotada(RegistroPendiente registro) async {
    final reintentar = await showModalBottomSheet<bool>(
      context: context,
      backgroundColor: AppColors.cream,
      isScrollControlled: true,
      builder: (_) => _HojaAgotada(registro: registro),
    );
    if (reintentar != true || !mounted) return;
    await _reintentar(registro.id);
  }

  @override
  void didUpdateWidget(covariant SyncPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!identical(oldWidget.sync, widget.sync)) {
      oldWidget.sync.removeListener(_alNotificarSync);
      widget.sync.addListener(_alNotificarSync);
    }
  }

  @override
  void dispose() {
    widget.sync.removeListener(_alNotificarSync);
    super.dispose();
  }

  String _firmaActual() =>
      '${widget.sync.pendientes}|${widget.sync.fase.name}|${widget.sync.enLinea}';

  void _alNotificarSync() {
    if (!mounted) return;
    final firma = _firmaActual();
    // Mientras sube, cada aviso puede haber movido una fila (pending → syncing →
    // fuera de la cola) sin cambiar la firma, así que se relee siempre: es la
    // única forma de que el avance se vea fila a fila. El resto del tiempo
    // alcanza con la firma.
    final enVivo = widget.sync.fase == FaseRibbon.sincronizando;
    if (firma == _firma && !enVivo) return;
    setState(() {
      _firma = firma;
      _cola = _leerCola();
    });
  }

  /// Relectura manual (tirar hacia abajo). Es la salida cuando la consulta
  /// falló: sin esto la pantalla quedaba trabada hasta cerrarla y volver.
  Future<void> _recargar() async {
    final lectura = _leerCola();
    setState(() {
      _cola = lectura;
      _firma = _firmaActual();
    });
    try {
      await lectura;
    } catch (_) {
      // El error se muestra en el cuerpo; acá solo se cierra el indicador.
    }
  }

  @override
  Widget build(BuildContext context) {
    final sync = widget.sync;

    return Scaffold(
      backgroundColor: AppColors.cream,
      appBar: AppBar(title: const Text('Sincronización')),
      // Sin esto la pantalla queda congelada: se abrió con push, así que nadie
      // más la reconstruye cuando la cola avanza.
      body: ListenableBuilder(
        listenable: sync,
        builder: (context, _) => FutureBuilder<_LecturaCola>(
          future: _cola,
          builder: (context, snap) {
            // BUG 1: acá estaba `snap.data ?? []` sin mirar el connectionState,
            // y una cola todavía sin leer se veía igual que una cola vacía.
            final datos = snap.data ?? _ultimaLectura;
            final cola = datos?.todas ?? const <RegistroPendiente>[];
            final agotadas = datos?.agotadas ?? const <RegistroPendiente>[];
            final estado = switch (true) {
              // Solo es "cargando" cuando no se sabe nada: releer con datos ya
              // en pantalla no vuelve a poner esqueletos.
              _ when datos == null && snap.connectionState == ConnectionState.waiting =>
                _Estado.cargando,
              _ when datos == null && snap.hasError => _Estado.falla,
              // Sin nada en la cola no importa si hay señal: no hay nada que enviar.
              _ when cola.isEmpty => _Estado.alDia,
              _ when !sync.enLinea => _Estado.sinConexion,
              _ => _Estado.pendientes,
            };

            return RefreshIndicator(
              onRefresh: _recargar,
              color: AppColors.brand500,
              backgroundColor: AppColors.surface,
              child: ListView(
                padding: const EdgeInsets.all(AppSpacing.s4),
                physics: const AlwaysScrollableScrollPhysics(),
                children: [
                  _TarjetaEstado(estado: estado, sync: sync, cantidad: cola.length),
                  ..._motivos(sync),
                  const SizedBox(height: AppSpacing.s5),
                  ..._cuerpo(estado, cola, agotadas),
                  const SizedBox(height: AppSpacing.s5),
                  const _Rotulo('CONFIGURACIÓN'),
                  const SizedBox(height: AppSpacing.s2),
                  _AutoSync(sync: sync),
                ],
              ),
            );
          },
        ),
      ),
    );
  }

  /// Por qué la cola podría no estar avanzando. Son datos que el service ya
  /// tiene y que antes solo existían en la memoria del programa.
  List<Widget> _motivos(SyncService sync) {
    final avisos = <Widget>[];

    if (sync.requiereRelogin) {
      avisos.add(const AppInfoBox(
        text: 'Tu sesión venció mientras se subía la cola. Volvé a entrar y sigue '
            'donde quedó: no se perdió ningún registro.',
        tone: InfoTone.warn,
      ));
    }
    final plataforma = sync.avisoPlataforma;
    if (plataforma != null) {
      // La cola quedó DETENIDA por configuración del servidor: sin este aviso
      // el usuario solo ve que los pendientes no bajan y vuelve a tocar el
      // botón. No es culpa suya ni es peligro → advertencia, nunca rojo.
      avisos.add(AppInfoBox(
        text: 'El servidor no reconoció esta app: $plataforma. Es configuración '
            'del servidor, no tuya — tus registros siguen guardados acá y se '
            'van a enviar cuando lo resuelvan.',
        tone: InfoTone.warn,
      ));
    }

    final resumen = _resumenPasada(sync);
    if (resumen != null) avisos.add(resumen);

    return [
      for (final a in avisos) ...[const SizedBox(height: AppSpacing.s3), a],
    ];
  }

  /// Qué dejó la última sincronización, cuando dejó algo que el usuario no
  /// puede deducir mirando la cola.
  ///
  /// Se pinta solo si hubo **duplicados o rechazados**: si todo entró limpio,
  /// la cola vacía y "Al día" ya lo dicen, y un cartel permanente con lo que
  /// pasó hace tres horas es ruido. Los subidos se nombran acá únicamente para
  /// dar la proporción ("4 subidos · 1 hay que recargarlo").
  ///
  /// Mientras sube no aparece: los contadores están a mitad de camino y quien
  /// manda en pantalla es la barra de progreso.
  Widget? _resumenPasada(SyncService sync) {
    if (sync.fase == FaseRibbon.sincronizando) return null;

    final dup = sync.duplicados;
    final rech = sync.rechazados;
    if (dup == 0 && rech == 0) return null;

    final partes = <String>[
      if (sync.enviados > 0) '${sync.enviados} subido${sync.enviados == 1 ? '' : 's'}',
      if (dup > 0) '$dup ya ${dup == 1 ? 'estaba' : 'estaban'}',
      if (rech > 0) '$rech hay que ${rech == 1 ? 'recargarlo' : 'recargarlos'}',
    ];

    // El rechazo es el único que deja trabajo pendiente: el servidor no aceptó
    // el contenido y `datosInvalidos` soltó la marca del día (I9), así que la
    // fecha volvió a quedar libre y nadie se entera si no se dice acá.
    final detalle = rech > 0
        ? 'El servidor no aceptó ${rech == 1 ? 'ese registro' : 'esos registros'} '
            'y ${rech == 1 ? 'el día quedó' : 'los días quedaron'} libres otra vez: '
            'hay que volver a cargarlos.'
        : 'Los días que ya estaban en el servidor no se perdieron: quedaron '
            'guardados de antes.';

    return AppInfoBox(
      text: 'Última sincronización: ${partes.join(' · ')}. $detalle',
      tone: rech > 0 ? InfoTone.warn : InfoTone.info,
    );
  }

  List<Widget> _cuerpo(
    _Estado estado,
    List<RegistroPendiente> cola,
    List<RegistroPendiente> agotadas,
  ) {
    switch (estado) {
      case _Estado.cargando:
        return [
          const _Rotulo('LEYENDO LA COLA'),
          const SizedBox(height: AppSpacing.s2),
          for (var i = 0; i < 3; i++) ...[
            const EsqueletoFilaCola(),
            const SizedBox(height: AppSpacing.s2),
          ],
        ];

      case _Estado.falla:
        return const [
          AppInfoBox(
            text: 'No se pudo leer la cola guardada en el teléfono. Nada se perdió: '
                'deslizá hacia abajo para volver a intentarlo.',
            tone: InfoTone.warn,
          ),
        ];

      // Al día: la ausencia es el mensaje. Lo dice la tarjeta de arriba y basta.
      case _Estado.alDia:
        return const [];

      case _Estado.sinConexion:
      case _Estado.pendientes:
        // Las agotadas salen de la cola normal: siguen contando en la tarjeta
        // (son registros que no llegaron) pero no son "en camino" — nadie las
        // va a mandar hasta que alguien las toque.
        final idsAgotadas = {for (final r in agotadas) r.id};
        final enCola = [
          for (final r in cola)
            if (!idsAgotadas.contains(r.id)) r,
        ];

        return [
          ..._seccionAtencion(agotadas),
          if (enCola.isNotEmpty) ...[
            if (agotadas.isNotEmpty) const SizedBox(height: AppSpacing.s5),
            const _Rotulo('EN COLA'),
            const SizedBox(height: AppSpacing.s2),
            for (var i = 0; i < enCola.length; i++) ...[
              EntradaEscalonada(indice: i, child: _ColaItem(registro: enCola[i])),
              const SizedBox(height: AppSpacing.s2),
            ],
          ],
        ];
    }
  }

  /// Las filas que la cola ya no reintenta sola. Van **arriba** de la cola
  /// normal: son las únicas que piden algo del usuario. Si no hay, no se pinta
  /// nada — una sección vacía sugeriría que hay un problema donde no lo hay.
  List<Widget> _seccionAtencion(List<RegistroPendiente> agotadas) {
    if (agotadas.isEmpty) return const [];

    return [
      const _Rotulo('NECESITAN TU ATENCIÓN'),
      const SizedBox(height: 2),
      Text(
        agotadas.length == 1
            ? 'Dejamos de reintentarlo solo. Tocalo para ver qué dijo el '
                'servidor y volver a intentarlo.'
            : 'Dejamos de reintentarlos solos. Tocá cada uno para ver qué dijo '
                'el servidor y volver a intentarlo.',
        style: _Tipo.secundario,
      ),
      const SizedBox(height: AppSpacing.s2),
      for (var i = 0; i < agotadas.length; i++) ...[
        EntradaEscalonada(
          indice: i,
          child: _FilaAgotada(
            registro: agotadas[i],
            onTap: () => unawaited(_abrirAgotada(agotadas[i])),
          ),
        ),
        const SizedBox(height: AppSpacing.s2),
      ],
    ];
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Tarjeta de estado — la única que afirma algo sobre la cola
// ═══════════════════════════════════════════════════════════════════════════

class _TarjetaEstado extends StatelessWidget {
  const _TarjetaEstado({required this.estado, required this.sync, required this.cantidad});

  final _Estado estado;
  final SyncService sync;
  final int cantidad;

  @override
  Widget build(BuildContext context) {
    return switch (estado) {
      _Estado.cargando => const _TarjetaCargando(),
      _Estado.falla => const _TarjetaFalla(),
      _Estado.alDia => _TarjetaAlDia(enLinea: sync.enLinea),
      _Estado.sinConexion => _TarjetaCola(sync: sync, cantidad: cantidad, offline: true),
      _Estado.pendientes => _TarjetaCola(sync: sync, cantidad: cantidad, offline: false),
    };
  }
}

/// Envoltura neutra de las tarjetas que no son de cola.
class _Panel extends StatelessWidget {
  const _Panel({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.s4),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.xl),
        border: Border.all(color: AppColors.line),
        boxShadow: AppColors.shadowSm,
      ),
      child: child,
    );
  }
}

class _TarjetaCargando extends StatelessWidget {
  const _TarjetaCargando();

  @override
  Widget build(BuildContext context) {
    return const _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          FractionallySizedBox(
            alignment: Alignment.centerLeft,
            widthFactor: 0.42,
            child: EsqueletoBloque(height: AppSpacing.s3),
          ),
          SizedBox(height: AppSpacing.s3),
          FractionallySizedBox(
            alignment: Alignment.centerLeft,
            widthFactor: 0.24,
            child: EsqueletoBloque(height: AppSpacing.s7),
          ),
          SizedBox(height: AppSpacing.s4),
          EsqueletoBloque(height: _altoBoton, radius: AppRadius.md),
        ],
      ),
    );
  }
}

class _TarjetaFalla extends StatelessWidget {
  const _TarjetaFalla();

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Row(children: [
        _Icono(fondo: AppColors.infoBg, icono: Icons.help_outline_rounded, color: AppColors.info),
        const SizedBox(width: AppSpacing.s3),
        const Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('Cola no disponible', style: _Tipo.titulo),
            SizedBox(height: 2),
            Text('No pudimos leer los registros guardados.', style: _Tipo.secundario),
          ]),
        ),
      ]),
    );
  }
}

class _TarjetaAlDia extends StatelessWidget {
  const _TarjetaAlDia({required this.enLinea});

  final bool enLinea;

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Row(children: [
        // Verde = éxito. Es el único de la pantalla y por eso significa algo.
        _Icono(fondo: AppColors.successBg, icono: Icons.check_rounded, color: AppColors.green600),
        const SizedBox(width: AppSpacing.s3),
        const Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('Al día', style: _Tipo.titulo),
            SizedBox(height: 2),
            Text('No hay registros pendientes de enviar.', style: _Tipo.secundario),
          ]),
        ),
        // Sin señal pero sin nada que mandar: se informa, no se alarma.
        if (!enLinea) const AppBadge(label: 'Sin conexión', tone: BadgeTone.neutral),
      ]),
    );
  }
}

/// Tarjeta con la cuenta de la cola. Naranja cuando hay red (es una acción
/// disponible) y tinta cuando no la hay: sin conexión es un modo de trabajo
/// válido, no una falla, así que nunca se pinta de rojo.
class _TarjetaCola extends StatelessWidget {
  const _TarjetaCola({required this.sync, required this.cantidad, required this.offline});

  final SyncService sync;
  final int cantidad;
  final bool offline;

  @override
  Widget build(BuildContext context) {
    final sincronizando = sync.fase == FaseRibbon.sincronizando;
    final blanco = Colors.white;
    final blancoSuave = blanco.withValues(alpha: 0.88);

    return Column(children: [
      Container(
        padding: const EdgeInsets.all(AppSpacing.s4),
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: offline
                ? const [AppColors.ink700, AppColors.ink900]
                : const [AppColors.brand500, AppColors.brand600],
          ),
          borderRadius: BorderRadius.circular(AppRadius.xl),
          boxShadow: offline ? AppColors.shadowMd : AppColors.shadowBrand,
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Icon(
              // "Esperando señal", no "falla de red": nada de iconos de alarma.
              offline ? Icons.schedule_rounded : Icons.cloud_upload_rounded,
              size: 16,
              color: blancoSuave,
            ),
            const SizedBox(width: AppSpacing.s2),
            Expanded(
              child: Text(
                offline ? 'Sin conexión · acumulados' : 'Pendientes de enviar',
                style: TextStyle(
                  fontFamily: 'Inter',
                  fontSize: AppFontSize.xs,
                  fontWeight: FontWeight.w600,
                  color: blancoSuave,
                ),
              ),
            ),
          ]),
          const SizedBox(height: AppSpacing.s2),
          Text(
            '$cantidad',
            style: TextStyle(
              fontFamily: 'PlusJakartaSans',
              fontSize: AppFontSize.xxl,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.6,
              height: 1,
              color: blanco,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
          const SizedBox(height: 2),
          Text(
            cantidad == 1 ? 'registro' : 'registros',
            style: TextStyle(
              fontFamily: 'Inter',
              fontSize: AppFontSize.xs,
              color: blancoSuave,
            ),
          ),
          if (sincronizando) ...[
            const SizedBox(height: AppSpacing.s3),
            _Progreso(sync: sync, sobreOscuro: offline),
          ],
        ]),
      ),
      const SizedBox(height: AppSpacing.s3),
      _BotonSincronizar(sync: sync, offline: offline, sincronizando: sincronizando),
    ]);
  }
}

/// Progreso del envío en curso. Es la prueba visible de que el botón hizo algo:
/// antes se tocaba "Sincronizar ahora" y la pantalla no se movía.
class _Progreso extends StatelessWidget {
  const _Progreso({required this.sync, required this.sobreOscuro});

  final SyncService sync;
  final bool sobreOscuro;

  @override
  Widget build(BuildContext context) {
    final blanco = Colors.white;
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(
        'Enviando ${sync.enviados} de ${sync.totalLote}',
        style: TextStyle(
          fontFamily: 'PlusJakartaSans',
          fontSize: AppFontSize.xs,
          fontWeight: FontWeight.w700,
          color: blanco.withValues(alpha: 0.92),
          fontFeatures: const [FontFeature.tabularFigures()],
        ),
      ),
      const SizedBox(height: 6),
      ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.pill),
        child: BarraProgresoSync(
          valor: sync.progreso,
          duration: AppMotion.duracion(context, AppMotion.slow),
          // Sobre el degradado de la tarjeta el color de marca no se vería:
          // acá el contraste lo da el blanco, no el naranja.
          color: blanco,
          fondo: blanco.withValues(alpha: sobreOscuro ? 0.22 : 0.30),
          alto: 4,
        ),
      ),
    ]);
  }
}

/// El CTA vive FUERA de la tarjeta: sobre el degradado naranja un botón naranja
/// no se lee, y pintarlo de verde rompería la regla de marca (verde = éxito).
class _BotonSincronizar extends StatelessWidget {
  const _BotonSincronizar({
    required this.sync,
    required this.offline,
    required this.sincronizando,
  });

  final SyncService sync;
  final bool offline;
  final bool sincronizando;

  @override
  Widget build(BuildContext context) {
    final habilitado = !offline && !sincronizando;

    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      AppButton(
        label: switch (true) {
          _ when sincronizando => 'Sincronizando…',
          _ when offline => 'Sin conexión',
          _ => 'Sincronizar ahora',
        },
        icon: offline ? Icons.schedule_rounded : Icons.sync_rounded,
        variant: AppButtonVariant.primary,
        size: AppButtonSize.md,
        full: true,
        loading: sincronizando,
        onPressed: habilitado ? sync.sincronizar : null,
      ),
      // Un botón apagado sin explicación se lee como app rota. El motivo va
      // siempre debajo, en tono neutro.
      if (offline) ...[
        const SizedBox(height: AppSpacing.s2),
        Text(
          sync.autoSync
              ? 'Se envían solos apenas vuelva la señal. Podés seguir registrando.'
              : 'El envío automático está apagado: tocá el botón cuando vuelva la señal.',
          style: _Tipo.secundario,
        ),
      ],
    ]);
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Fila de la cola
// ═══════════════════════════════════════════════════════════════════════════

class _ColaItem extends StatelessWidget {
  const _ColaItem({required this.registro});

  final RegistroPendiente registro;

  @override
  Widget build(BuildContext context) {
    final estado = switch (registro.estado) {
      EstadoSync.pending => SyncDotState.pending,
      EstadoSync.syncing => SyncDotState.syncing,
      EstadoSync.synced => SyncDotState.synced,
      // El servidor ya tenía ese día: el registro NO se perdió, está guardado.
      EstadoSync.duplicado => SyncDotState.synced,
      EstadoSync.error => SyncDotState.pending,
    };
    final titulo = _tituloRegistro(registro);
    final falla = registro.estado == EstadoSync.error;
    final subiendo = registro.estado == EstadoSync.syncing;

    return AnimatedContainer(
      duration: AppMotion.duracion(context, AppMotion.fast),
      curve: AppMotion.simetrica,
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.s4,
        vertical: AppSpacing.s3,
      ),
      decoration: BoxDecoration(
        // La fila que está subiendo se tiñe de marca: el avance se ve fila a fila.
        color: subiendo ? AppColors.brand50 : AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        border: Border.all(color: subiendo ? AppColors.brand200 : AppColors.line),
      ),
      child: Row(children: [
        SyncDot(state: estado),
        const SizedBox(width: AppSpacing.s3),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(
              titulo,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontFamily: 'PlusJakartaSans',
                fontSize: AppFontSize.sm,
                fontWeight: FontWeight.w600,
                color: AppColors.ink900,
              ),
            ),
            Text(_meta(), style: _Tipo.secundario),
            // El motivo del rechazo, tal como lo devolvió el servidor: sin esto
            // el usuario ve "Resolver" y no tiene qué resolver.
            if (falla && registro.ultimoError != null)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(
                  registro.ultimoError!,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontFamily: 'Inter',
                    fontSize: AppFontSize.xs,
                    height: 1.35,
                    color: AppColors.danger,
                  ),
                ),
              ),
          ]),
        ),
        if (falla) ...[
          const SizedBox(width: AppSpacing.s2),
          const AppBadge(label: 'Resolver', tone: BadgeTone.danger),
        ],
      ]),
    );
  }

  /// Antigüedad y, si hubo reintentos, cuántos: dos filas iguales con distinta
  /// insistencia no son el mismo problema.
  String _meta() {
    final hace = _hace(registro.createdAt);
    if (registro.intentos <= 0) return hace;
    return '$hace · ${registro.intentos} '
        '${registro.intentos == 1 ? 'intento' : 'intentos'}';
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Necesitan tu atención — filas agotadas (intentos >= LocalDb.maxIntentos)
// ═══════════════════════════════════════════════════════════════════════════

/// Fila agotada: la cola dejó de insistir y quedó esperando a una persona.
///
/// Se pinta en tono de advertencia, **no en rojo**: el registro no se perdió,
/// sigue guardado en el teléfono. Lo que cambió es que ya no va a subir solo.
class _FilaAgotada extends StatelessWidget {
  const _FilaAgotada({required this.registro, required this.onTap});

  final RegistroPendiente registro;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return PresionHundida(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.s4,
          vertical: AppSpacing.s3,
        ),
        decoration: BoxDecoration(
          color: AppColors.warningBg,
          borderRadius: BorderRadius.circular(AppRadius.md),
          // Borde de color uniforme: con `borderRadius`, un borde de colores
          // distintos por lado revienta al pintar (trampa conocida de Flutter).
          border: Border.all(color: AppColors.warning),
        ),
        child: Row(children: [
          // Ícono FIJO: uno elegido en runtime se cae del subconjunto de la
          // fuente en release y sale un cuadro en blanco. El estado lo dice el
          // color, no el glifo.
          const Icon(
            Icons.error_outline_rounded,
            size: AppSpacing.s5,
            color: AppColors.warning,
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(
                _tituloRegistro(registro),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontFamily: 'PlusJakartaSans',
                  fontSize: AppFontSize.sm,
                  fontWeight: FontWeight.w600,
                  color: AppColors.ink900,
                ),
              ),
              Text(
                '${_hace(registro.createdAt)} · ${registro.intentos} '
                '${registro.intentos == 1 ? 'intento' : 'intentos'} · sin subir',
                style: _Tipo.secundario,
              ),
              // El motivo tal como lo devolvió el servidor. Sin esto la fila
              // dice "algo falló" y no hay nada que el operario pueda hacer.
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(
                  registro.ultimoError ?? 'El servidor no explicó el motivo.',
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontFamily: 'Inter',
                    fontSize: AppFontSize.xs,
                    height: 1.35,
                    color: AppColors.ink700,
                  ),
                ),
              ),
            ]),
          ),
          const SizedBox(width: AppSpacing.s2),
          const Icon(
            Icons.chevron_right_rounded,
            size: AppSpacing.s5,
            color: AppColors.ink500,
          ),
        ]),
      ),
    );
  }
}

/// Detalle de una fila agotada: el mensaje completo del servidor y la salida.
///
/// No conoce al [SyncService]: devuelve `true` con `pop` y quien la abrió
/// decide. Así el reintento corre con la pantalla viva y no dentro de una hoja
/// que ya se está cerrando.
class _HojaAgotada extends StatelessWidget {
  const _HojaAgotada({required this.registro});

  final RegistroPendiente registro;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(
          AppSpacing.s5,
          AppSpacing.s3,
          AppSpacing.s5,
          AppSpacing.s5,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(
              child: Container(
                width: AppSpacing.s8,
                height: AppSpacing.s1,
                decoration: BoxDecoration(
                  color: AppColors.ink200,
                  borderRadius: BorderRadius.circular(AppRadius.pill),
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.s4),
            Row(children: [
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  const Text('No se pudo enviar', style: _Tipo.titulo),
                  const SizedBox(height: 2),
                  Text(
                    _tituloRegistro(registro),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: _Tipo.secundario,
                  ),
                ]),
              ),
              const SizedBox(width: AppSpacing.s2),
              IconButton(
                onPressed: () => Navigator.of(context).pop(),
                icon: const Icon(Icons.close_rounded, size: AppSpacing.s5),
                style: IconButton.styleFrom(
                  backgroundColor: AppColors.cream2,
                  foregroundColor: AppColors.ink700,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                  ),
                ),
              ),
            ]),
            const SizedBox(height: AppSpacing.s4),
            const _Rotulo('LO QUE DIJO EL SERVIDOR'),
            const SizedBox(height: AppSpacing.s2),
            // Completo y sin recortar: acá es donde el operario (o quien lo
            // asista por teléfono) lee el motivo real. Un mensaje largo de
            // validación scrollea DENTRO del bloque, en vez de empujar el botón
            // "Reintentar" fuera de la pantalla.
            ConstrainedBox(
              constraints: BoxConstraints(
                maxHeight: MediaQuery.sizeOf(context).height * 0.3,
              ),
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(AppSpacing.s3),
                decoration: BoxDecoration(
                  color: AppColors.warningBg,
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                ),
                child: SingleChildScrollView(
                  child: Text(
                    registro.ultimoError ??
                        'No quedó un mensaje del servidor. Puede haber sido un '
                            'corte de red en el momento del envío.',
                    style: const TextStyle(
                      fontFamily: 'Inter',
                      fontSize: AppFontSize.sm,
                      height: 1.4,
                      color: AppColors.ink900,
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.s3),
            Text(
              'Se intentó ${registro.intentos} '
              '${registro.intentos == 1 ? 'vez' : 'veces'} y la app dejó de '
              'insistir sola. El registro sigue guardado en el teléfono: no se '
              'perdió nada.',
              style: _Tipo.secundario,
            ),
            const SizedBox(height: AppSpacing.s4),
            AppButton(
              label: 'Reintentar',
              icon: Icons.refresh_rounded,
              variant: AppButtonVariant.primary,
              size: AppButtonSize.md,
              full: true,
              onPressed: () => Navigator.of(context).pop(true),
            ),
            const SizedBox(height: AppSpacing.s2),
            const Text(
              'Vuelve a la cola y se envía en el próximo intento.',
              textAlign: TextAlign.center,
              style: _Tipo.secundario,
            ),
          ],
        ),
      ),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Formato compartido de las filas (cola y agotadas dicen lo mismo igual)
// ═══════════════════════════════════════════════════════════════════════════

String _tituloRegistro(RegistroPendiente r) {
  final modulo = ModuloSeguimiento.fromId(r.tipo);
  return modulo != null
      ? 'Seguimiento · ${r.loteNombre} · ${modulo.label}'
      : '${_tituloMovimiento(r.tipo)} · ${r.loteNombre}';
}

String _tituloMovimiento(String tipo) => switch (tipo) {
      'venta-aves' => 'Venta de aves',
      'traslado-aves' => 'Traslado de aves',
      'movimiento-huevos' => 'Movimiento de huevos',
      _ => 'Registro',
    };

String _hace(DateTime d) {
  final min = DateTime.now().difference(d).inMinutes;
  if (min < 1) return 'Ahora';
  if (min < 60) return 'Hace $min min';
  final h = min ~/ 60;
  if (h < 24) return 'Hace $h h';
  return 'Hace ${h ~/ 24} d';
}

// ═══════════════════════════════════════════════════════════════════════════
// Piezas menores
// ═══════════════════════════════════════════════════════════════════════════

class _AutoSync extends StatelessWidget {
  const _AutoSync({required this.sync});

  final SyncService sync;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.lg),
        border: Border.all(color: AppColors.line),
      ),
      child: SwitchListTile(
        value: sync.autoSync,
        onChanged: (v) => sync.autoSync = v,
        activeThumbColor: AppColors.surface,
        activeTrackColor: AppColors.brand500,
        title: const Text('Sincronización automática', style: _Tipo.cuerpo),
        subtitle: const Text('Al recuperar conexión', style: _Tipo.secundario),
      ),
    );
  }
}

class _Rotulo extends StatelessWidget {
  const _Rotulo(this.texto);

  final String texto;

  @override
  Widget build(BuildContext context) {
    return Text(
      texto,
      style: const TextStyle(
        fontFamily: 'Inter',
        fontSize: AppFontSize.xs,
        fontWeight: FontWeight.w700,
        letterSpacing: 0.8,
        color: AppColors.ink500,
      ),
    );
  }
}

class _Icono extends StatelessWidget {
  const _Icono({required this.fondo, required this.icono, required this.color});

  final Color fondo;
  final IconData icono;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: AppTouch.min,
      height: AppTouch.min,
      decoration: BoxDecoration(
        color: fondo,
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      child: Icon(icono, size: AppSpacing.s6, color: color),
    );
  }
}

/// Estilos repetidos de la pantalla, en un solo lugar.
class _Tipo {
  _Tipo._();

  static const TextStyle titulo = TextStyle(
    fontFamily: 'PlusJakartaSans',
    fontSize: AppFontSize.md,
    fontWeight: FontWeight.w700,
    color: AppColors.ink900,
  );

  static const TextStyle cuerpo = TextStyle(
    fontFamily: 'Inter',
    fontSize: AppFontSize.sm,
    color: AppColors.ink900,
  );

  static const TextStyle secundario = TextStyle(
    fontFamily: 'Inter',
    fontSize: AppFontSize.xs,
    height: 1.35,
    color: AppColors.ink500,
  );
}
