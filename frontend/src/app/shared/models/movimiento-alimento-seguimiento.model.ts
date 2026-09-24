export interface MovimientoAlimentoSeguimientoDto {
  id: number;
  fecha: string;
  tipoMovimiento: 'INV_INGRESO' | 'INV_TRASLADO_ENTRADA' | 'INV_TRASLADO_SALIDA' | string;
  cantidadKg: number;
  alimento?: string | null;
  referencia?: string | null;
  numeroDocumento?: string | null;
}

export interface ResumenMovimientosAlimentoDia {
  fecha: string;
  ingresos: MovimientoAlimentoSeguimientoDto[];
  traslados: MovimientoAlimentoSeguimientoDto[];
  referencias: string[];
}
