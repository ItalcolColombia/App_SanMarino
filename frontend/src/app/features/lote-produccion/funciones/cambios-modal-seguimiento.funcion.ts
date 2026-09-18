/**
 * Qué hay que hacer cuando cambia un `@Input()` del modal de seguimiento diario de PRODUCCIÓN.
 *
 * Función PURA (sin `this`, sin DI, sin estado): recibe los nombres de los inputs que cambiaron y el
 * contexto del modal, y devuelve las acciones. El componente solo las ejecuta.
 *
 * ## Por qué existe
 *
 * El `ngOnChanges` del modal llamaba a `resetForm()` ante **cualquier** cambio de input con el modal
 * abierto. Pero al abrir «Nuevo registro» todavía viajan datos del lote que llegan después (la
 * consulta pesada `informacion-lote` trae `fechaEncaset`; `GET /LotePosturaProduccion/{id}` trae el
 * lote base), y cada llegada vaciaba el formulario: los huevos ya tecleados se perdían sin aviso y el
 * registro se guardaba con mortalidad y consumo pero con huevos en 0. También borraba todo cada vez que
 * el padre cambiaba `loading`, es decir, al guardar y —peor— tras un guardado rechazado.
 *
 * Regla: **el formulario abierto no se toca por datos que llegan tarde**. Se reinicia solo al abrir o
 * cuando cambia el registro que se edita; cada dato tardío recarga únicamente lo suyo.
 */

export interface ContextoCambiosModal {
  /** Nombres de los `@Input()` que cambiaron (las claves de `SimpleChanges`). */
  cambiados: readonly string[];
  /** `isOpen` actual. */
  abierto: boolean;
  /** `isOpen` acaba de pasar a `true` (apertura). */
  abre: boolean;
  /** Hay un registro en edición (`editingSeguimiento`). */
  editando: boolean;
}

export interface AccionesPorCambioDelModal {
  /** Vaciar (alta) o poblar (edición) el formulario. Solo al abrir o al cambiar el registro en edición. */
  reiniciarFormulario: boolean;
  /** Dejar en el formulario los ids ocultos de lote (`produccionLoteId` / `lotePosturaProduccionId`). */
  sincronizarIds: boolean;
  /** Silos y tipos de huevo: dependen del lote (`loteId`). */
  recargarDatosDelLote: boolean;
  /** Catálogo y stock de inventario: dependen de granja / núcleo / galpón. */
  recargarInventario: boolean;
  /** Etapa del registro nuevo: depende de `fechaEncaset` / `raza`. En edición la etapa es la del registro. */
  recalcularEtapa: boolean;
  /** Vigencia de primera postura de las filas de huevo: depende de la semana de vida (`fechaEncaset`). */
  reconstruirFilasHuevo: boolean;
}

const SIN_ACCIONES: AccionesPorCambioDelModal = Object.freeze({
  reiniciarFormulario: false,
  sincronizarIds: false,
  recargarDatosDelLote: false,
  recargarInventario: false,
  recalcularEtapa: false,
  reconstruirFilasHuevo: false
});

export function resolverCambiosDelModal(ctx: ContextoCambiosModal): AccionesPorCambioDelModal {
  if (!ctx.abierto) return SIN_ACCIONES;

  // Apertura: se carga todo. El reinicio (alta) o la población (edición) ya calculan etapa y filas de huevo.
  if (ctx.abre) {
    return {
      reiniciarFormulario: true,
      sincronizarIds: true,
      recargarDatosDelLote: true,
      recargarInventario: true,
      recalcularEtapa: false,
      reconstruirFilasHuevo: false
    };
  }

  const cambio = (nombre: string): boolean => ctx.cambiados.includes(nombre);
  const cambioAlgunoDe = (...nombres: string[]): boolean => nombres.some(cambio);

  // Modal ya abierto: `loading` (y cualquier input no listado) no hace nada.
  return {
    reiniciarFormulario: cambio('editingSeguimiento'),
    sincronizarIds: cambioAlgunoDe('produccionLoteId', 'lotePosturaProduccionId'),
    recargarDatosDelLote: cambio('loteId'),
    recargarInventario: cambioAlgunoDe('granjaId', 'nucleoId', 'galponId'),
    recalcularEtapa: !ctx.editando && cambioAlgunoDe('fechaEncaset', 'raza'),
    reconstruirFilasHuevo: cambio('fechaEncaset')
  };
}
