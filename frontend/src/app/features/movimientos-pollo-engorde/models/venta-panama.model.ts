/**
 * Tipos de la venta Panamá (despacho por galpón con asignación H/M sobre las mixtas).
 * El registro guarda el split en hembras/machos; el inventario sale de las mixtas del lote.
 */

/** Línea enviada al backend (una por lote del galpón). */
export interface VentaPanamaLineaDto {
  loteAveEngordeOrigenId: number;
  granjaOrigenId?: number | null;
  nucleoOrigenId?: string | null;
  galponOrigenId?: string | null;
  cantidadHembras: number;
  cantidadMachos: number;
}

/** Cabecera de despacho + líneas. */
export interface CreateVentaPanamaDespachoDto {
  fechaMovimiento: string;
  tipoMovimiento: string;
  granjaOrigenId?: number | null;
  usuarioMovimientoId: number;
  motivoMovimiento?: string | null;
  observaciones?: string | null;
  numeroDespacho?: string | null;
  edadAves?: number | null;
  totalPollosGalpon?: number | null;
  raza?: string | null;
  placa?: string | null;
  horaSalida?: string | null;
  guiaAgrocalidad?: string | null;
  sellos?: string | null;
  ayuno?: string | null;
  conductor?: string | null;
  pesoBruto?: number | null;
  pesoTara?: number | null;
  lineas: VentaPanamaLineaDto[];
}

/**
 * Fila por lote en el modal Panamá. El usuario asigna H/M sobre las `mixtasDisponibles`;
 * `hStr`/`mStr` son el texto del input (solo dígitos) para no pisar el cursor al escribir.
 */
export interface VentaPanamaLineaUI {
  loteId: number;
  loteNombre: string;
  galponId: string;
  galponLabel: string;
  mixtasDisponibles: number;
  h: number;
  m: number;
  hStr: string;
  mStr: string;
  flashExceso?: boolean;
}
