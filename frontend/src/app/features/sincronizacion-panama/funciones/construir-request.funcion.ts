// features/sincronizacion-panama/funciones/construir-request.funcion.ts
/**
 * Arma el `PanamaConexion` y el `SincronizarPanamaRequest` a partir de los valores del formulario.
 * PURAS (sin `this`, sin DI): reciben los valores y devuelven el payload listo para el servicio.
 */
import { PanamaConexion, SincronizarPanamaRequest } from '../models/sincronizacion-panama.model';
import { ymdToIsoUtcNoon } from '../../../shared/utils/format';

/** Valores crudos de conexión (tal como salen de los inputs). */
export interface ConexionFormValues {
  baseUrl: string;
  email: string;
  password: string;
}

/** Valores crudos de los filtros del formulario. */
export interface FiltrosFormValues {
  anio: number | null;
  clienteIdOrigen: number | null;
  granjaIdOrigen: number | null;
  /** `yyyy-mm-dd` de un `<input type="date">` o cadena vacía. */
  fechaHasta: string;
  geneticaRaza: string;
  geneticaAnio: number | null;
  importarGuiaGenetica: boolean;
  /** Crear guía de PRUEBA (FAKE) si falta la real (red de seguridad para no dejar lotes pendientes). */
  crearGuiaFakeSiFalta: boolean;
}

/**
 * Construye el objeto `origen`. Los campos vacíos NO se envían (se omiten) para que el backend
 * caiga a su configuración por defecto, como aclara el hint de la pantalla.
 */
export function construirConexion(v: ConexionFormValues): PanamaConexion {
  const baseUrl = (v.baseUrl ?? '').trim();
  const email = (v.email ?? '').trim();
  const password = v.password ?? '';
  const c: PanamaConexion = {};
  if (baseUrl) c.baseUrl = baseUrl;
  if (email) c.email = email;
  if (password) c.password = password;
  return c;
}

/** Construye el request completo de previsualización/sincronización. */
export function construirRequest(
  conexion: ConexionFormValues,
  filtros: FiltrosFormValues,
  dryRun: boolean
): SincronizarPanamaRequest {
  const raza = (filtros.geneticaRaza ?? '').trim();
  return {
    anio: filtros.anio ?? null,
    clienteIdOrigen: filtros.clienteIdOrigen ?? null,
    granjaIdOrigen: filtros.granjaIdOrigen ?? null,
    fechaHasta: ymdToIsoUtcNoon(filtros.fechaHasta) ?? null,
    dryRun,
    geneticaRaza: raza || null,
    geneticaAnio: filtros.geneticaAnio ?? null,
    importarGuiaGenetica: !!filtros.importarGuiaGenetica,
    crearGuiaFakeSiFalta: !!filtros.crearGuiaFakeSiFalta,
    origen: construirConexion(conexion)
  };
}
