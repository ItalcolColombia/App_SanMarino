/// Historial local: los registros que ya salieron del teléfono y el servidor
/// aceptó.
///
/// **Por qué existe.** `seguimientos_local` tenía una sola escritura
/// (`LocalDb.confirmarEnviado`) y cero lectores: en cuanto un registro se
/// sincronizaba desaparecía de la vista del usuario, porque la pantalla de cola
/// sólo muestra `pending_sync`. Sin señal, la única señal de que un día ya
/// estaba cargado era el rechazo al intentar cargarlo otra vez. El operario no
/// tenía cómo responder «¿ya cargué el lunes?»; ahora sí.
///
/// **Alcance honesto.** Acá se ve lo que salió de ESTE teléfono. Una tablet
/// nueva no conoce los días que subió otro equipo (pendiente conocido del
/// `CLAUDE.md`: `fechasRegistradas` está construido y sin cablear), y la
/// pantalla lo dice en vez de dejar que el usuario lo suponga.
///
/// Se agrupa por DÍA REGISTRADO, no por lote ni por fecha de envío: la pregunta
/// que trae al operario acá es siempre sobre un día del calendario.
library;

import 'package:flutter/material.dart';

import 'package:zootecnicoapp/core/db/local_db.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/design_system/components/app_widgets.dart';
import 'package:zootecnicoapp/design_system/motion/transiciones.dart';
import 'package:zootecnicoapp/design_system/tokens/app_colors.dart';
import 'package:zootecnicoapp/design_system/tokens/app_spacing.dart';
import 'package:zootecnicoapp/features/sync/widgets/sync_widgets.dart';
import 'package:zootecnicoapp/shared/formato.dart';

/// Días de la semana abreviados, a mano. La app no inicializa los datos de
/// locale, así que `DateFormat` con nombres en español fallaría en runtime
/// (mismo motivo por el que el perfil formatea la última sincronización a mano).
const List<String> _diasSemana = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

/// Emoji de una fila cuyo `tipo` no es uno de los 4 módulos (un movimiento, o
/// un módulo que esta versión todavía no conoce).
const String _emojiGenerico = '📋';

class HistorialPage extends StatefulWidget {
  const HistorialPage({super.key});

  @override
  State<HistorialPage> createState() => _HistorialPageState();
}

class _HistorialPageState extends State<HistorialPage> {
  late Future<_Historial> _futuro;

  @override
  void initState() {
    super.initState();
    _futuro = _leer();
  }

  Future<_Historial> _leer() async {
    final filas = await LocalDb.instance.historialLocal();
    // El historial no guarda el nombre del lote: `confirmarEnviado` copia de la
    // cola sólo el id. Se resuelve contra la caché de lotes, que es la misma
    // que el usuario ve en el resto de la app.
    final lotes = await LocalDb.instance.lotesCacheados();
    return _Historial.armar(filas, lotes);
  }

  /// Relectura manual (tirar hacia abajo). Es también la salida cuando la
  /// consulta falla: sin esto la pantalla quedaría trabada hasta cerrarla.
  Future<void> _recargar() async {
    final lectura = _leer();
    setState(() => _futuro = lectura);
    try {
      await lectura;
    } catch (_) {
      // El error se muestra en el cuerpo; acá sólo se cierra el indicador.
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.cream,
      appBar: AppBar(title: const Text('Historial')),
      body: FutureBuilder<_Historial>(
        future: _futuro,
        builder: (context, snap) {
          final datos = snap.data;
          // Mientras se lee no se afirma nada: una consulta sin terminar no es
          // un historial vacío (el mismo bug que ya tuvo la pantalla de cola).
          final cargando = datos == null && snap.connectionState == ConnectionState.waiting;

          return RefreshIndicator(
            onRefresh: _recargar,
            color: AppColors.brand500,
            backgroundColor: AppColors.surface,
            child: ListView(
              padding: const EdgeInsets.all(AppSpacing.s4),
              physics: const AlwaysScrollableScrollPhysics(),
              children: switch (true) {
                _ when cargando => _esqueleto(),
                _ when datos == null && snap.hasError => const <Widget>[_Falla()],
                _ when datos == null || datos.filas.isEmpty => const <Widget>[_Vacio()],
                _ => _contenido(datos),
              },
            ),
          );
        },
      ),
    );
  }

  List<Widget> _esqueleto() => <Widget>[
    for (var i = 0; i < 4; i++) ...[
      const EsqueletoFilaCola(),
      const SizedBox(height: AppSpacing.s2),
    ],
  ];

  List<Widget> _contenido(_Historial h) {
    final hijos = <Widget>[
      EntradaEscalonada(indice: 0, child: _Resumen(historial: h)),
      const SizedBox(height: AppSpacing.s5),
    ];

    // Un solo índice corrido para todo el cuerpo: el escalonado tiene que leerse
    // como una sola lista bajando, no reiniciarse en cada día.
    var indice = 1;
    for (final grupo in h.grupos) {
      hijos.add(EntradaEscalonada(indice: indice++, child: _CabeceraDia(grupo: grupo)));
      hijos.add(const SizedBox(height: AppSpacing.s2));
      for (final fila in grupo.filas) {
        hijos.add(EntradaEscalonada(
          indice: indice++,
          child: _FilaHistorial(registro: fila, nombreLote: h.nombreDeLote(fila)),
        ));
        hijos.add(const SizedBox(height: AppSpacing.s2));
      }
      hijos.add(const SizedBox(height: AppSpacing.s4));
    }
    return hijos;
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Datos de la pantalla
// ═══════════════════════════════════════════════════════════════════════════

/// Lo leído, ya agrupado y con los nombres de lote resueltos.
class _Historial {
  const _Historial._(this.filas, this.grupos, this._nombres);

  final List<SeguimientoLocal> filas;
  final List<_GrupoDia> grupos;
  final _NombresDeLote _nombres;

  int get total => filas.length;

  /// Hay al menos una fila que el servidor aceptó sin devolver número. Sólo
  /// entonces se explica la diferencia entre las dos etiquetas: si todas son
  /// iguales, la aclaración es ruido.
  bool get haySinNumero => filas.any((f) => !f.confirmadoConId);

  String nombreDeLote(SeguimientoLocal f) =>
      _nombres.resolver(f.tipo, f.loteId) ?? 'Lote ${f.loteId}';

  /// Las filas ya vienen ordenadas por fecha DESC, así que agrupar es recorrer
  /// una vez y cortar cuando cambia el día.
  factory _Historial.armar(List<SeguimientoLocal> filas, List<Lote> cache) {
    final grupos = <_GrupoDia>[];
    for (final f in filas) {
      final clave = _claveDia(f);
      if (grupos.isEmpty || grupos.last.clave != clave) {
        grupos.add(_GrupoDia(clave: clave, dia: f.fecha?.toLocal(), filas: [f]));
      } else {
        grupos.last.filas.add(f);
      }
    }
    return _Historial._(filas, grupos, _NombresDeLote(cache));
  }

  /// Qué cuenta como "el mismo día". Con la fecha ilegible se agrupa por su
  /// texto crudo: sigue juntando lo que es igual, sin inventar una fecha.
  static String _claveDia(SeguimientoLocal f) {
    final d = f.fecha?.toLocal();
    if (d == null) return f.fechaTexto;
    return '${d.year.toString().padLeft(4, '0')}-'
        '${d.month.toString().padLeft(2, '0')}-'
        '${d.day.toString().padLeft(2, '0')}';
  }
}

class _GrupoDia {
  _GrupoDia({required this.clave, required this.dia, required this.filas});

  final String clave;

  /// `null` cuando la fecha guardada no se pudo parsear.
  final DateTime? dia;
  final List<SeguimientoLocal> filas;
}

/// Resuelve el nombre del lote a partir del `tipo` y el id guardados.
///
/// Engorde y reproductora numeran por separado (invariante I11: la caché tiene
/// PK `(modulo, id)`), así que el id solo **no** identifica un lote. Se busca
/// primero la coincidencia exacta módulo+id; el atajo por id solo se usa cuando
/// hay un único lote con ese número en toda la caché. Ante ambigüedad se
/// devuelve `null` y la fila muestra el id: mostrar el nombre de OTRO lote sería
/// peor que no mostrar ninguno.
class _NombresDeLote {
  _NombresDeLote(List<Lote> cache) {
    for (final l in cache) {
      _porModuloEId['${l.modulo.id}#${l.id}'] = l.nombre;
      if (!_porId.containsKey(l.id)) {
        _porId[l.id] = l.nombre;
      } else if (_porId[l.id] != l.nombre) {
        _porId[l.id] = null; // ambiguo: dos módulos con el mismo número
      }
    }
  }

  final Map<String, String> _porModuloEId = {};
  final Map<int, String?> _porId = {};

  String? resolver(String tipo, int loteId) =>
      _porModuloEId['$tipo#$loteId'] ?? _porId[loteId];
}

// ═══════════════════════════════════════════════════════════════════════════
// Resumen
// ═══════════════════════════════════════════════════════════════════════════

class _Resumen extends StatelessWidget {
  const _Resumen({required this.historial});

  final _Historial historial;

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Text(
            fmtMiles(historial.total),
            style: const TextStyle(
              fontFamily: 'PlusJakartaSans',
              fontSize: AppFontSize.xxl,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.6,
              height: 1,
              color: AppColors.ink900,
              fontFeatures: [FontFeature.tabularFigures()],
            ),
          ),
          const SizedBox(width: AppSpacing.s3),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(
                historial.total == 1 ? 'registro enviado' : 'registros enviados',
                style: _Tipo.titulo,
              ),
              const SizedBox(height: AppSpacing.s1),
              // El alcance va en la tarjeta, no en una nota al pie: es lo que
              // evita que el operario lea esta pantalla como "todo lo cargado".
              const Text('Los que salieron de este teléfono.', style: _Tipo.secundario),
            ]),
          ),
        ]),
        if (historial.haySinNumero) ...[
          const SizedBox(height: AppSpacing.s3),
          const AppInfoBox(
            text: 'Confirmado = el servidor devolvió su número. Enviado = lo aceptó '
                'sin número, normalmente porque ya tenía ese día. En los dos casos '
                'el registro está guardado del lado del servidor.',
          ),
        ],
      ]),
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Día y fila
// ═══════════════════════════════════════════════════════════════════════════

class _CabeceraDia extends StatelessWidget {
  const _CabeceraDia({required this.grupo});

  final _GrupoDia grupo;

  @override
  Widget build(BuildContext context) {
    final n = grupo.filas.length;
    return Row(children: [
      Expanded(
        child: Text(
          _etiqueta(),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(
            fontFamily: 'Inter',
            fontSize: AppFontSize.xs,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.8,
            color: AppColors.ink500,
          ),
        ),
      ),
      const SizedBox(width: AppSpacing.s2),
      Text('$n ${n == 1 ? 'registro' : 'registros'}', style: _Tipo.secundario),
    ]);
  }

  /// "HOY · 23/08", "AYER · 22/08" o "SÁB 16/08/2026". El nombre del día está
  /// porque la pregunta del operario viene en esa forma: "¿cargué el lunes?".
  String _etiqueta() {
    final d = grupo.dia;
    if (d == null) return grupo.clave.toUpperCase();

    final dd = d.day.toString().padLeft(2, '0');
    final mm = d.month.toString().padLeft(2, '0');

    final hoy = DateTime.now();
    final dias = DateTime(hoy.year, hoy.month, hoy.day)
        .difference(DateTime(d.year, d.month, d.day))
        .inDays;
    if (dias == 0) return 'HOY · $dd/$mm';
    if (dias == 1) return 'AYER · $dd/$mm';

    return '${_diasSemana[d.weekday - 1].toUpperCase()} $dd/$mm/${d.year}';
  }
}

class _FilaHistorial extends StatelessWidget {
  const _FilaHistorial({required this.registro, required this.nombreLote});

  final SeguimientoLocal registro;
  final String nombreLote;

  @override
  Widget build(BuildContext context) {
    final modulo = ModuloSeguimiento.fromId(registro.tipo);
    final color = _colorModulo(modulo);
    final resumen = _resumenPayload(registro.payload);
    final conNumero = registro.confirmadoConId;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.s3,
        vertical: AppSpacing.s3,
      ),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.md),
        // Borde de color uniforme: un `Border` con lados de distinto color más
        // `borderRadius` revienta al pintar, no al compilar (trampa conocida).
        border: Border.all(color: AppColors.line),
      ),
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        // El módulo se distingue con su emoji, no con un `IconData` elegido en
        // runtime: ese se pinta en BLANCO en release porque el tree-shaking de
        // íconos sólo conserva los que resuelve estáticamente. El emoji es texto
        // y no pasa por la fuente de íconos.
        Container(
          width: AppTouch.min,
          height: AppTouch.min,
          decoration: BoxDecoration(
            color: color.withValues(alpha: 0.10),
            borderRadius: BorderRadius.circular(AppRadius.sm),
          ),
          alignment: Alignment.center,
          child: Text(
            modulo?.emoji ?? _emojiGenerico,
            style: const TextStyle(fontSize: AppFontSize.md),
          ),
        ),
        const SizedBox(width: AppSpacing.s3),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(
              nombreLote,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: _Tipo.titulo,
            ),
            Text(
              _meta(modulo),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: _Tipo.secundario,
            ),
            if (resumen != null)
              Text(
                resumen,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: _Tipo.secundario,
              ),
          ]),
        ),
        const SizedBox(width: AppSpacing.s2),
        // Verde sólo acá y sólo cuando de verdad hay confirmación con número:
        // es lo único de esta pantalla que significa éxito.
        AppBadge(
          label: conNumero ? 'Confirmado' : 'Enviado',
          tone: conNumero ? BadgeTone.success : BadgeTone.neutral,
          dot: conNumero,
        ),
      ]),
    );
  }

  /// Módulo y cuándo se envió. El día registrado ya lo dice la cabecera del
  /// grupo, así que repetirlo acá sería ruido.
  String _meta(ModuloSeguimiento? modulo) {
    final nombre = modulo?.label ?? registro.tipo;
    final cuando = registro.createdAt;
    if (cuando == null) return nombre;
    return '$nombre · enviado ${_hace(cuando)}';
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Estados sin datos
// ═══════════════════════════════════════════════════════════════════════════

class _Vacio extends StatelessWidget {
  const _Vacio();

  @override
  Widget build(BuildContext context) {
    // Vacío no es error: nadie hizo nada mal, todavía no hay nada que mostrar.
    // Por eso tinta neutra y ni un gramo de rojo.
    return _Panel(
      child: Column(children: [
        Container(
          width: AppSpacing.s9,
          height: AppSpacing.s9,
          decoration: BoxDecoration(
            color: AppColors.cream2,
            borderRadius: BorderRadius.circular(AppRadius.lg),
          ),
          alignment: Alignment.center,
          child: const Icon(
            Icons.history_rounded,
            size: AppSpacing.s6,
            color: AppColors.ink500,
          ),
        ),
        const SizedBox(height: AppSpacing.s4),
        const Text('Todavía no hay historial', style: _Tipo.titulo),
        const SizedBox(height: AppSpacing.s2),
        const Text(
          'Acá van a aparecer los registros que el servidor ya recibió. '
          'Los que están esperando señal se ven en Sincronización.',
          textAlign: TextAlign.center,
          style: _Tipo.secundario,
        ),
      ]),
    );
  }
}

class _Falla extends StatelessWidget {
  const _Falla();

  @override
  Widget build(BuildContext context) {
    return const AppInfoBox(
      text: 'No se pudo leer el historial guardado en el teléfono. Nada se perdió: '
          'deslizá hacia abajo para volver a intentarlo.',
      tone: InfoTone.warn,
    );
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Piezas comunes
// ═══════════════════════════════════════════════════════════════════════════

class _Panel extends StatelessWidget {
  const _Panel({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
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

/// Color categórico del módulo (eje distinto del semántico: identifica el
/// módulo, no un estado — por eso Levante puede ser verde).
Color _colorModulo(ModuloSeguimiento? m) => switch (m) {
  ModuloSeguimiento.levante => AppColors.levante,
  ModuloSeguimiento.engorde => AppColors.engorde,
  ModuloSeguimiento.produccion => AppColors.produccion,
  ModuloSeguimiento.reproductora => AppColors.reproductora,
  null => AppColors.ink500,
};

/// Antigüedad en palabras. Igual criterio que la pantalla de cola.
String _hace(DateTime d) {
  final min = DateTime.now().difference(d).inMinutes;
  if (min < 1) return 'recién';
  if (min < 60) return 'hace $min min';
  final h = min ~/ 60;
  if (h < 24) return 'hace $h h';
  return 'hace ${h ~/ 24} d';
}

/// Dos datos del payload, y sólo si están. El JSON crudo cambia por módulo,
/// país y empresa, así que acá no se renderiza: se buscan un par de claves
/// conocidas y lo que no aparece simplemente no se muestra.
///
/// Los nombres difieren entre módulos (postura manda `mortalidadH`/`mortalidadM`
/// donde levante y engorde mandan `mortalidadHembras`/`mortalidadMachos`), por
/// eso cada dato se busca en sus dos formas.
String? _resumenPayload(Map<String, dynamic> p) {
  final partes = <String>[];

  final mortalidad = (_numero(p['mortalidadHembras']) ?? _numero(p['mortalidadH']) ?? 0) +
      (_numero(p['mortalidadMachos']) ?? _numero(p['mortalidadM']) ?? 0);
  if (mortalidad > 0) partes.add('Mortalidad ${fmtMiles(mortalidad.round())}');

  final huevos = _numero(p['huevosTotales']) ?? 0;
  if (huevos > 0) partes.add('Huevos ${fmtMiles(huevos.round())}');

  return partes.isEmpty ? null : partes.join(' · ');
}

/// Tolerante a que el valor venga como número, como texto o como nada.
double? _numero(Object? v) => switch (v) {
  num n => n.toDouble(),
  String s => double.tryParse(s),
  _ => null,
};

/// Estilos repetidos de la pantalla, en un solo lugar.
class _Tipo {
  _Tipo._();

  static const TextStyle titulo = TextStyle(
    fontFamily: 'PlusJakartaSans',
    fontSize: AppFontSize.sm,
    fontWeight: FontWeight.w700,
    color: AppColors.ink900,
  );

  static const TextStyle secundario = TextStyle(
    fontFamily: 'Inter',
    fontSize: AppFontSize.xs,
    height: 1.35,
    color: AppColors.ink500,
  );
}
