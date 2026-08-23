/// Qué lotes puede trabajar el operario.
///
/// Un lote cerrado **no admite registros nuevos**: el backend los rechaza. Los
/// tres módulos lo dicen con nombres distintos y el mapeo ya los unifica en
/// `Lote.cerrado` (`lotes_api.dart`):
///
///   engorde       `estadoOperativoLote == 'Cerrado'`
///   reproductora  `estado == 'Cerrado'`  — ya se vendieron todas las aves
///   levante       `estadoCierre == 'Cerrado'` — ya pasó a producción
///
/// Antes se mostraban igual y el choque aparecía al final, cuando el operario
/// ya había elegido el lote: «El lote X está cerrado: no admite registros
/// nuevos». Ofrecer algo que no se puede hacer y avisarlo recién al tocarlo es
/// trabajo perdido, y en una lista larga son muchos toques perdidos.
///
/// ⚠️ Esto filtra lo que se **ofrece**, no lo que se **guarda**: la caché de
/// `lotes_cache` sigue teniendo todos. Es a propósito — el historial resuelve el
/// nombre del lote contra esa caché, y si un lote se cierra después de haber
/// registrado días, esos días tienen que seguir mostrando su nombre y no un
/// «Lote 187» pelado.
library;

import 'package:zootecnicoapp/core/models/models.dart';

/// Los que admiten registro. Preserva el orden de entrada.
List<Lote> lotesActivos(List<Lote> todos) =>
    todos.where((l) => !l.cerrado).toList();

/// Cuántos quedaron afuera. Sirve para poder decirlo en pantalla en vez de que
/// el operario note que "faltan" lotes y no sepa por qué.
int lotesCerrados(List<Lote> todos) => todos.where((l) => l.cerrado).length;
