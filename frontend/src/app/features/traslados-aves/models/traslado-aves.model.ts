// src/app/features/traslados-aves/models/traslado-aves.model.ts
// Traslados y ventas de aves y huevos: disponibilidad, payloads y respuestas.
//
// Extraido de `services/traslados-aves.service.ts` (3-sep-2026): el service tenia 29 interfaces
// de 4 dominios distintos en la cabecera. El service los RE-EXPORTA, asi que los imports que ya
// existian siguen funcionando sin tocarse.

// Disponibilidad de Lote
export interface DisponibilidadLoteDto {
  loteId: number;
  loteNombre: string;
  tipoLote: string; // "Levante" o "Produccion"
  /**
   * Espejo de produccion del lote. El backend lo envia desde siempre
   * (`DisponibilidadLoteDto.LotePosturaProduccionId`, seteado tambien en la respuesta POR LOTE),
   * pero esta interfaz no lo declaraba, asi que el dato llegaba y se descartaba. Hace falta para
   * el traslado de huevos POR ITEMS, que exige LPP en el backend.
   */
  lotePosturaProduccionId?: number;
  aves?: AvesDisponiblesDto;
  huevos?: HuevosDisponiblesDto;
  granjaId: number;
  granjaNombre: string;
  nucleoId?: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
}

export interface AvesDisponiblesDto {
  hembrasVivas: number;
  machosVivos: number;
  totalAves: number;
  hembrasIniciales: number;
  machosIniciales: number;
  mortalidadAcumuladaHembras: number;
  mortalidadAcumuladaMachos: number;
  retirosAcumuladosHembras: number;
  retirosAcumuladosMachos: number;
}

export interface HuevosDisponiblesDto {
  totalHuevos: number;
  totalHuevosIncubables: number;
  limpio: number;
  tratado: number;
  sucio: number;
  deforme: number;
  blanco: number;
  dobleYema: number;
  piso: number;
  pequeno: number;
  roto: number;
  desecho: number;
  otro: number;
  fechaUltimoRegistro?: Date;
  diasEnProduccion: number;
}

// Traslado de Aves
export interface CrearTrasladoAvesDto {
  loteId: string;
  fechaTraslado: Date;
  tipoOperacion: string; // "Venta" o "Traslado"
  cantidadHembras: number;
  cantidadMachos: number;
  granjaDestinoId?: number;
  loteDestinoId?: string;
  tipoDestino?: string; // "Granja" o "Planta"
  motivo?: string;
  descripcion?: string;
  observaciones?: string;
}

// Traslado de Huevos
export interface CrearTrasladoHuevosDto {
  loteId: string;
  fechaTraslado: Date;
  tipoOperacion: string; // "Venta" o "Traslado"
  cantidadLimpio: number;
  cantidadTratado: number;
  cantidadSucio: number;
  cantidadDeforme: number;
  cantidadBlanco: number;
  cantidadDobleYema: number;
  cantidadPiso: number;
  cantidadPequeno: number;
  cantidadRoto: number;
  cantidadDesecho: number;
  cantidadOtro: number;
  granjaDestinoId?: number;
  loteDestinoId?: string;
  tipoDestino?: string; // "Granja" o "Planta"
  motivo?: string;
  descripcion?: string;
  observaciones?: string;
}

export interface TrasladoHuevosDto {
  id: number;
  numeroTraslado: string;
  fechaTraslado: Date;
  tipoOperacion: string;
  loteId: string;
  loteNombre: string;
  granjaOrigenId: number;
  granjaOrigenNombre: string;
  granjaDestinoId?: number;
  granjaDestinoNombre?: string;
  loteDestinoId?: string;
  tipoDestino?: string;
  motivo?: string;
  descripcion?: string;
  cantidadLimpio: number;
  cantidadTratado: number;
  cantidadSucio: number;
  cantidadDeforme: number;
  cantidadBlanco: number;
  cantidadDobleYema: number;
  cantidadPiso: number;
  cantidadPequeno: number;
  cantidadRoto: number;
  cantidadDesecho: number;
  cantidadOtro: number;
  totalHuevos: number;
  estado: string;
  usuarioTrasladoId: number;
  usuarioNombre?: string;
  fechaProcesamiento?: Date;
  fechaCancelacion?: Date;
  observaciones?: string;
  createdAt: Date;
  updatedAt?: Date;
}

// Disponibilidad aves para traslado desde seguimiento diario (R3)
export interface DisponibilidadAvesSegDto {
  loteId: number;
  loteNombre: string;
  tipoLote: string;
  avesHActual: number;
  avesMActual: number;
  granjaId?: number | null;
  granjaNombre?: string | null;
  galponId?: string | null;
  galponNombre?: string | null;
}

export interface TrasladoAvesDesdeSegDiarioDto {
  loteOrigenId: number;
  tipoOrigen: string;            // "Levante" | "Produccion"
  fechaSeguimiento: string;      // ISO date
  trasladoHembras: number;
  trasladoMachos: number;
  loteDestinoId: number;
  tipoDestino: string;           // "Levante" | "Produccion"
  granjaDestinoId?: number | null;
  observaciones?: string | null;
  /** Placa del vehículo de transporte. Opcional (postura, Santa Reyes). */
  placa?: string | null;
  /** Nombre del conductor. Opcional (postura, Santa Reyes). */
  conductor?: string | null;
  /** Precinto/sellos de seguridad del transporte. Opcional (postura, Santa Reyes). */
  sellos?: string | null;
}

export interface TrasladoAvesResultSegDto {
  exitoso: boolean;
  mensaje: string;
  movimientoAvesId?: number | null;
  avesHActualOrigen: number;
  avesMActualOrigen: number;
}

// Traslado de Lote
export interface TrasladoLoteRequest {
  loteId: number;
  granjaDestinoId: number;
  nucleoDestinoId?: string | null;
  galponDestinoId?: string | null;
  observaciones?: string | null;
  /**
   * Dia real en que el lote se movio (`yyyy-MM-dd`), que no es el instante en que se registra. Si
   * se omite, el backend usa hoy. Misma semantica que en `lote.service.ts`.
   */
  fechaTraslado?: string | null;
}

export interface TrasladoLoteResponse {
  success: boolean;
  message: string;
  loteOriginalId?: number;
  loteNuevoId?: number;
  loteNombre?: string;
  granjaOrigen?: string;
  granjaDestino?: string;
}

export interface HistorialTrasladoLoteDto {
  id: number;
  loteOriginalId: number;
  loteNuevoId: number;
  granjaOrigenId: number;
  granjaOrigenNombre?: string;
  granjaDestinoId: number;
  granjaDestinoNombre?: string;
  nucleoDestinoId?: string | null;
  nucleoDestinoNombre?: string | null;
  galponDestinoId?: string | null;
  galponDestinoNombre?: string | null;
  observaciones?: string | null;
  createdByUserId: number;
  /** `firstName + surName` de quien lo registro; null si su id no corresponde a ninguna cedula. */
  createdByUserName?: string | null;
  /** Instante en que se DIGITO el registro (ISO con hora). No es el dia del traslado. */
  createdAt: string;
  /**
   * Dia REAL en que el lote se movio (`yyyy-MM-dd`, `DateOnly` del backend), que es el que eligio
   * quien registro en el modal. Null solo en filas previas a la migracion que agrego la columna.
   */
  fechaTraslado?: string | null;
}

// Resultado paginado
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
