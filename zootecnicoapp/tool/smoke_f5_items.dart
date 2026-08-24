/// Smoke de F5 (descuento de inventario desde el móvil) usando el MISMO
/// código que la app: `InventarioApi`, `ItemsConsumo` y `PayloadSeguimiento`.
///
/// A diferencia de `smoke_backend.dart` (que asume el flag apagado, como está
/// hoy en toda empresa real), este exige que la empresa del login tenga
/// `descuenta_inventario_desde_movil = true` — normalmente sólo cierto en una
/// base local de prueba, encendido a mano para este smoke.
///
/// ```bash
/// dart run tool/smoke_f5_items.dart admin.ecuador@italcol.com 123456789 <loteId> <itemId>
/// ```
///
/// Crea un seguimiento real con un ítem de inventario y lo borra al terminar.
library;

import 'dart:io';

import 'package:zootecnicoapp/core/api/api_client.dart';
import 'package:zootecnicoapp/core/api/auth_api.dart';
import 'package:zootecnicoapp/core/api/inventario_api.dart';
import 'package:zootecnicoapp/core/api/seguimientos_api.dart';
import 'package:zootecnicoapp/core/config/api_config.dart';
import 'package:zootecnicoapp/features/seguimiento/funciones/items_consumo.dart';
import 'package:zootecnicoapp/core/models/models.dart';
import 'package:zootecnicoapp/core/models/models_inventario.dart';
import 'package:zootecnicoapp/core/session/sesion_actual.dart';

int _fallos = 0;
void _ok(String paso, [String detalle = '']) =>
    stdout.writeln('  OK   $paso${detalle.isEmpty ? '' : '  →  $detalle'}');
void _mal(String paso, Object motivo) {
  _fallos++;
  stdout.writeln('  FALLA $paso  →  $motivo');
}

class SesionDeSmoke implements SesionActual {
  @override
  Usuario? usuario;
  @override
  final String deviceId = 'smoke-f5-dart';
}

Future<void> main(List<String> args) async {
  if (args.length < 4) {
    stderr.writeln('uso: dart run tool/smoke_f5_items.dart <email> <password> <loteId> <itemId>');
    exit(64);
  }
  final email = args[0], password = args[1];
  final loteId = int.parse(args[2]);
  final itemId = int.parse(args[3]);

  stdout.writeln('Backend: ${ApiConfig.baseUrl}\n');

  final sesion = SesionDeSmoke();
  final api = ApiClient(sesion: sesion);
  final auth = AuthApi(api);
  final inventario = InventarioApi(api);
  final seguimientos = SeguimientosApi(api);

  final usuario = await auth.login(email: email, password: password);
  sesion.usuario = usuario;
  _ok('1. login', '${usuario.nombre} · ${usuario.companyName} · flag=${usuario.descuentaInventarioDesdeMovil}');

  if (!usuario.descuentaInventarioDesdeMovil) {
    _mal('2. flag F5', 'la empresa "${usuario.companyName}" tiene el flag APAGADO — este smoke no prueba nada así');
    exit(1);
  }
  _ok('2. flag F5 encendido para esta empresa', '');

  final catalogo = await inventario.catalogo();
  final item = catalogo.where((i) => i.id == itemId).firstOrNull;
  if (item == null) {
    _mal('3. catálogo', 'el ítem $itemId no está en /api/inventario/items?activo=true');
    exit(1);
  }
  _ok('3. catálogo descargado', '${catalogo.length} ítems, elegido: ${item.nombre} (esAlimento=${item.esAlimento})');

  final linea = LineaConsumo(item: item, cantidad: '6.25');
  final itemsHembras = ItemsConsumo.armar(lineas: [linea], paisId: usuario.paisId, manejaSilos: false);
  if (itemsHembras.isEmpty) {
    _mal('4. ItemsConsumo.armar', 'devolvió vacío para una línea válida');
    exit(1);
  }
  final idQueViaja = itemsHembras.first['itemInventarioEcuadorId'];
  if (idQueViaja != itemId) {
    _mal('4. ItemsConsumo.armar', 'esperaba itemInventarioEcuadorId=$itemId, mandó $idQueViaja');
  } else {
    _ok('4. ItemsConsumo.armar manda itemInventarioEcuadorId', '$idQueViaja (F5.3)');
  }

  // Fecha libre para el lote (misma lógica que usa el operario: hoy, o el día
  // más reciente sin registro).
  final registrados = await seguimientos.fechasRegistradas(
    Lote(id: loteId, nombre: 's/n', granja: '', galpon: '', modulo: ModuloSeguimiento.engorde, dia: 0, aves: 0));
  DateTime? fechaLibre;
  for (var i = 0; i < 60; i++) {
    final f = DateTime.now().subtract(Duration(days: i));
    final solo = DateTime(f.year, f.month, f.day);
    if (!registrados.contains(solo)) { fechaLibre = solo; break; }
  }
  if (fechaLibre == null) {
    _mal('5. fecha libre', 'no se encontró ninguna en los últimos 60 días');
    exit(1);
  }

  final payload = <String, dynamic>{
    'loteId': loteId,
    'fechaRegistro': DateTime(fechaLibre.year, fechaLibre.month, fechaLibre.day, 12).toIso8601String(),
    'ciclo': 'Normal',
    'mortalidadHembras': 0, 'mortalidadMachos': 0, 'selH': 0, 'selM': 0,
    'errorSexajeHembras': 0, 'errorSexajeMachos': 0,
    'observaciones': 'SMOKE-F5 - borrar',
  };
  ItemsConsumo.aplicarEn(payload, itemsHembras: itemsHembras, itemsMachos: const [], modulo: ModuloSeguimiento.engorde);
  if (!payload.containsKey('itemsHembras')) {
    _mal('5. armado del payload', 'aplicarEn no agregó itemsHembras');
    exit(1);
  }
  _ok('5. payload armado', 'itemsHembras=${payload['itemsHembras']}');

  final id = await seguimientos.enviar(endpoint: '/SeguimientoAvesEngordeEcuador', payload: payload);
  if (id == null) {
    _mal('6. POST con ítems (descuento real)', 'no devolvió id');
    exit(1);
  }
  _ok('6. POST con ítems -> descuenta', 'seguimiento id=$id');

  final del = await api.deleteRaw('/SeguimientoAvesEngordeEcuador/$id');
  _ok('7. DELETE de limpieza', del);

  stdout.writeln(_fallos == 0 ? '\nTodo lo verificado responde como se esperaba.' : '\n$_fallos verificacion(es) fallaron.');
  exit(_fallos == 0 ? 0 : 1);
}

extension _PrimeroONulo<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
