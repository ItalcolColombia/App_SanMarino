// features/sincronizacion-panama/models/sincronizacion-panama.model.ts
// Tipos compartidos del módulo de Sincronización / Integración Panamá (espejo de los DTOs del
// backend `PuentePanamaController` → base `api/sincronizacion-panama`). Todo el wire es camelCase.

/** Credenciales/origen del sistema externo (ZooPanamaPollo). Campos vacíos → el backend usa su config. */
export interface PanamaConexion {
  baseUrl?: string | null;
  email?: string | null;
  password?: string | null;
}

/** Respuesta de `POST /probar-conexion`. */
export interface ProbarConexionResult {
  ok: boolean;
  mensaje?: string | null;
  /** Fecha/hora de expiración del token del origen (ISO), si el login fue exitoso. */
  expiracion?: string | null;
}

/** Cliente del origen (`POST /clientes`). */
export interface PanamaCliente {
  id: number;
  nombre: string;
}

/** Granja del origen (`POST /granjas`), dependiente del cliente. */
export interface PanamaGranja {
  id: number;
  nombre: string;
  latitud?: number | null;
  longitud?: number | null;
  certificadoGab?: string | null;
  idCliente: number;
}

/** Cuerpo de `POST /previsualizar` y `POST /sincronizar` (SincronizarPanamaRequest). */
export interface SincronizarPanamaRequest {
  anio?: number | null;
  clienteIdOrigen?: number | null;
  granjaIdOrigen?: number | null;
  /** Fecha límite (ISO) para el seguimiento traído; opcional. */
  fechaHasta?: string | null;
  dryRun?: boolean | null;
  geneticaRaza?: string | null;
  geneticaAnio?: number | null;
  importarGuiaGenetica?: boolean | null;
  /**
   * Red de seguridad: si no hay guía para (raza, año) y la del origen no está disponible o viene vacía,
   * crea una guía de PRUEBA claramente marcada como FAKE para no dejar lotes pendientes.
   */
  crearGuiaFakeSiFalta?: boolean | null;
  origen?: PanamaConexion | null;
}

/** Estado por lote en el resultado de la corrida. 'Pendiente' = sin guía genética (se carga luego). */
export type EstadoLote = 'Nuevo' | 'YaExiste' | 'Pendiente' | 'Error' | (string & {});

/** Fila por lote del resultado (`ResultadoSincronizacionDto.lotes[]`). */
export interface LoteSincronizacionDto {
  idOrigen: number;
  lote: string;
  granja: string;
  galpon: string;
  fechaInicio?: string | null;
  avesEncasetadas: number;
  /** Raza/línea genética con la que se crea el lote (línea del origen o el override global). */
  raza?: string | null;
  seguimientos: number;
  reproductoras: number;
  seguimientosReproductora: number;
  /** Lesiones de reproductora nuevas que trae/traería el lote. */
  lesiones: number;
  estado: EstadoLote;
  mensaje?: string | null;
}

/** Resultado de una corrida de previsualización (dryRun=true) o sincronización real. */
export interface ResultadoSincronizacionDto {
  dryRun: boolean;
  anio?: number | null;
  companyId: number;

  guiaGeneticaImportada: boolean;
  guiaGeneticaFilas: number;
  guiaGeneticaRazaAnio?: string | null;
  /** True si se creó (o se crearía) una guía de PRUEBA (FAKE): cargar la guía real, los indicadores no valen. */
  guiaGeneticaFakeCreada: boolean;

  granjasVistas: number;
  granjasNuevas: number;
  galponesVistos: number;
  galponesNuevos: number;

  lotesEnAnio: number;
  lotesNuevos: number;
  lotesOmitidos: number;
  lotesConError: number;
  /** Lotes pendientes por no tener guía genética (no se crean; se listan para cargar luego). */
  lotesPendientes: number;

  seguimientosNuevos: number;
  seguimientosOmitidos: number;

  reproductorasNuevas: number;
  reproductorasOmitidas: number;

  seguimientosReproNuevos: number;
  seguimientosReproOmitidos: number;

  lesionesNuevas: number;
  lesionesOmitidas: number;

  duracionMs: number;
  estado: string;

  /** Id del registro de auditoría (migracion_masiva) — "corrida #id". */
  auditoriaId?: number | null;

  lotes: LoteSincronizacionDto[];
  mensajes: string[];
}

// ── Historial de corridas (GET /historial y /historial/{id}) ──

/** Fila del historial (metadatos + contadores; sin el detalle pesado). */
export interface SincronizacionHistorialItemDto {
  id: number;
  fechaProceso: string;
  fueDryRun: boolean;
  estado: string;
  duracionMs?: number | null;
  nombreArchivo: string;
  lotesTotales: number;
  lotesNuevos: number;
  lotesOmitidos: number;
  lotesConError: number;
  lotesPendientes: number;
  /** True si la corrida tiene el detalle completo persistido (corridas nuevas). */
  tieneDetalle: boolean;
}

export interface SincronizacionHistorialPagedDto {
  page: number;
  pageSize: number;
  total: number;
  items: SincronizacionHistorialItemDto[];
}

/** Detalle de una corrida: metadatos + el resultado completo persistido (null en corridas viejas). */
export interface SincronizacionHistorialDetalleDto {
  id: number;
  fechaProceso: string;
  fueDryRun: boolean;
  estado: string;
  duracionMs?: number | null;
  nombreArchivo: string;
  resultado?: ResultadoSincronizacionDto | null;
}

// ── Tipos de presentación (compartidos entre las funciones puras y los componentes) ──

/** Tono visual reutilizado por badges y tarjetas de resumen. */
export type Tono = 'neutro' | 'ok' | 'alerta' | 'peligro';

/** Tarjeta de contador del resumen de resultado. */
export interface TarjetaResumen {
  etiqueta: string;
  valor: string;
  tono: Tono;
}

/** Badge de estado (por lote o del resultado completo). */
export interface BadgeEstado {
  etiqueta: string;
  tono: Tono;
}
