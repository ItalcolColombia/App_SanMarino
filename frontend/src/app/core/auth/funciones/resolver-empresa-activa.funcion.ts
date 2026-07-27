import { CompanyPais } from '../auth.models';

/**
 * Campos de la sesión que definen "en qué empresa estoy parado".
 * Cambian **siempre juntos**: dejarlos desincronizados es una fuga entre empresas.
 */
export interface EmpresaActiva {
  activeCompany: string;
  activeCompanyId?: number;
  activePaisId?: number;
  activePaisNombre?: string;
  activeCompanyLogoDataUrl?: string | null;
}

/**
 * Resuelve la empresa activa a partir del nombre elegido en el selector.
 *
 * ## El bug que arregla
 *
 * `TokenStorageService.setActiveCompany(name)` sólo escribía `activeCompany` (el **nombre**)
 * y nunca `activeCompanyId`. Pero el interceptor manda las dos cosas
 * (`X-Active-Company` y `X-Active-Company-Id`) y `ActiveCompanyMiddleware` **prefiere el id**.
 * Resultado: al cambiar de empresa en el selector, la UI mostraba la empresa nueva mientras el
 * backend seguía respondiendo por la empresa del login. Y del lado del cliente, todo lo que lee
 * `activeCompanyId` —flags por empresa (`ActiveCompanyConfigService`), listas maestras, menús de
 * rol, listado de granjas— seguía apuntando a la anterior.
 *
 * Con la PWA esto empeora: un snapshot offline se particiona por `{userId, companyId}`, así que
 * un id desincronizado escribiría capturas en la partición equivocada.
 *
 * ## Fail-closed
 *
 * Si el nombre no corresponde a ninguna combinación empresa-país disponible, devuelve `null` y
 * **no se cambia nada**. Es preferible no cambiar de empresa a quedar con id y nombre apuntando
 * a empresas distintas.
 *
 * Tolera `companyPaises` en camelCase o PascalCase porque el login guarda la respuesta del
 * backend tal cual viene y ese contrato no está normalizado.
 */
export function resolverEmpresaActiva(
  companyPaises: readonly CompanyPais[] | undefined,
  nombreElegido: string
): EmpresaActiva | null {
  const nombre = (nombreElegido ?? '').trim();
  if (!nombre) return null;

  const entradas = (companyPaises ?? []).map(normalizar).filter((e): e is EntradaNormalizada => e !== null);
  if (entradas.length === 0) return null;

  // Coincidencia exacta primero; si no, sin distinguir mayúsculas ni espacios de más.
  const elegida =
    entradas.find(e => e.companyName === nombre) ??
    entradas.find(e => e.companyName.trim().toLocaleLowerCase() === nombre.toLocaleLowerCase());

  if (!elegida) return null;

  return {
    activeCompany: elegida.companyName,
    activeCompanyId: elegida.companyId,
    activePaisId: elegida.paisId,
    activePaisNombre: elegida.paisNombre,
    activeCompanyLogoDataUrl: elegida.companyLogoDataUrl ?? null
  };
}

interface EntradaNormalizada {
  companyId: number;
  companyName: string;
  paisId?: number;
  paisNombre?: string;
  companyLogoDataUrl?: string | null;
}

/** Lee una entrada tolerando camelCase y PascalCase; descarta las que no traen id o nombre. */
function normalizar(cp: CompanyPais): EntradaNormalizada | null {
  const raw = cp as unknown as Record<string, unknown>;
  const companyId = numero(raw['companyId'] ?? raw['CompanyId']);
  const companyName = texto(raw['companyName'] ?? raw['CompanyName']);

  if (companyId === undefined || !companyName) return null;

  return {
    companyId,
    companyName,
    paisId: numero(raw['paisId'] ?? raw['PaisId']),
    paisNombre: texto(raw['paisNombre'] ?? raw['PaisNombre']),
    companyLogoDataUrl: (raw['companyLogoDataUrl'] ?? raw['CompanyLogoDataUrl'] ?? null) as string | null
  };
}

function numero(v: unknown): number | undefined {
  return typeof v === 'number' && Number.isFinite(v) && v !== 0 ? v : undefined;
}

function texto(v: unknown): string | undefined {
  return typeof v === 'string' && v.trim() !== '' ? v : undefined;
}
