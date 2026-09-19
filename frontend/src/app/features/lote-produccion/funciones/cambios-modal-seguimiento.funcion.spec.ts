import { resolverCambiosDelModal, type AccionesPorCambioDelModal, type ContextoCambiosModal } from './cambios-modal-seguimiento.funcion';

const NADA: AccionesPorCambioDelModal = {
  reiniciarFormulario: false,
  sincronizarIds: false,
  recargarDatosDelLote: false,
  recargarInventario: false,
  recalcularEtapa: false,
  reconstruirFilasHuevo: false
};

/** Contexto de un modal ya abierto en alta (el caso que rompía: datos que llegan tarde). */
const abierto = (cambiados: string[], extra: Partial<ContextoCambiosModal> = {}): ContextoCambiosModal =>
  ({ cambiados, abierto: true, abre: false, editando: false, ...extra });

describe('resolverCambiosDelModal', () => {
  it('modal cerrado: nunca hace nada, cambie lo que cambie', () => {
    const acciones = resolverCambiosDelModal({
      cambiados: ['isOpen', 'loteId', 'fechaEncaset', 'loading'],
      abierto: false, abre: false, editando: false
    });
    expect(acciones).toEqual(NADA);
  });

  it('apertura: reinicia el formulario y carga los datos del lote, el inventario y los ids', () => {
    const acciones = resolverCambiosDelModal({ cambiados: ['isOpen'], abierto: true, abre: true, editando: false });
    expect(acciones).toEqual({ ...NADA, reiniciarFormulario: true, sincronizarIds: true, recargarDatosDelLote: true, recargarInventario: true });
  });

  it('apertura para editar: también reinicia (poblar), y no recalcula la etapa aparte', () => {
    const acciones = resolverCambiosDelModal({ cambiados: ['isOpen', 'editingSeguimiento'], abierto: true, abre: true, editando: true });
    expect(acciones.reiniciarFormulario).toBeTrue();
    expect(acciones.recalcularEtapa).toBeFalse();
  });

  // ── El defecto: nada de lo que sigue puede vaciar lo que el operario ya tecleó ─────────────────

  it('fechaEncaset que llega tarde (informacion-lote): NO reinicia; recalcula etapa y filas de huevo', () => {
    const acciones = resolverCambiosDelModal(abierto(['fechaEncaset']));
    expect(acciones.reiniciarFormulario).toBeFalse();
    expect(acciones).toEqual({ ...NADA, recalcularEtapa: true, reconstruirFilasHuevo: true });
  });

  it('loteId que llega tarde (lote base del LPP): NO reinicia; recarga silos y tipos de huevo', () => {
    const acciones = resolverCambiosDelModal(abierto(['loteId']));
    expect(acciones).toEqual({ ...NADA, recargarDatosDelLote: true });
  });

  it('loading (guardar / guardado rechazado): no hace NADA', () => {
    expect(resolverCambiosDelModal(abierto(['loading']))).toEqual(NADA);
  });

  it('granja, núcleo o galpón tardíos: recargan solo el inventario', () => {
    for (const nombre of ['granjaId', 'nucleoId', 'galponId']) {
      expect(resolverCambiosDelModal(abierto([nombre]))).toEqual({ ...NADA, recargarInventario: true });
    }
  });

  it('raza tardía: recalcula la etapa pero no toca las filas de huevo (la vigencia sale de fechaEncaset)', () => {
    expect(resolverCambiosDelModal(abierto(['raza']))).toEqual({ ...NADA, recalcularEtapa: true });
  });

  it('ids ocultos del lote: se sincronizan sin reiniciar', () => {
    for (const nombre of ['produccionLoteId', 'lotePosturaProduccionId']) {
      expect(resolverCambiosDelModal(abierto([nombre]))).toEqual({ ...NADA, sincronizarIds: true });
    }
  });

  it('varios datos tardíos a la vez: se suman las recargas y nunca hay reinicio', () => {
    const acciones = resolverCambiosDelModal(abierto(['loading', 'loteId', 'fechaEncaset', 'granjaId']));
    expect(acciones).toEqual({
      ...NADA,
      recargarDatosDelLote: true,
      recargarInventario: true,
      recalcularEtapa: true,
      reconstruirFilasHuevo: true
    });
  });

  // ── Edición ────────────────────────────────────────────────────────────────────────────────────

  it('cambia el registro en edición con el modal abierto: reinicia (poblar de nuevo)', () => {
    const acciones = resolverCambiosDelModal(abierto(['editingSeguimiento'], { editando: true }));
    expect(acciones).toEqual({ ...NADA, reiniciarFormulario: true });
  });

  it('editando: un fechaEncaset tardío NO recalcula la etapa (es la del registro) pero sí las filas de huevo', () => {
    const acciones = resolverCambiosDelModal(abierto(['fechaEncaset'], { editando: true }));
    expect(acciones).toEqual({ ...NADA, reconstruirFilasHuevo: true });
  });

  it('editando: `loading` tampoco repuebla el formulario con los valores originales', () => {
    expect(resolverCambiosDelModal(abierto(['loading'], { editando: true }))).toEqual(NADA);
  });
});
