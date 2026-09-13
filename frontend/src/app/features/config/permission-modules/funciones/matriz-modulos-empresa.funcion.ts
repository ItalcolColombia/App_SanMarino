import type { CompanyPermissionModuleItem } from '../../../../core/services/permission-module/permission-module.service';

/** Módulo mínimo para la matriz (lo que trae `PermissionModule`). */
export interface ModuloMatriz {
  id: number;
  key: string;
  nombre: string;
  descripcion?: string | null;
  orden: number;
}

/** Una celda empresa × módulo. */
export interface CeldaMatriz {
  companyId: number;
  moduleId: number;
  prendido: boolean;
  /** De los permisos del módulo, cuántos tiene prendidos la empresa. */
  habilitados: number;
  total: number;
  /** `false` si no se pudo cargar el estado de esa empresa: la celda NO se puede tocar. */
  cargada: boolean;
}

export interface FilaMatrizModulo {
  modulo: ModuloMatriz;
  celdas: CeldaMatriz[];
}

/**
 * Arma la matriz módulo (filas, por `orden`) × empresa (columnas, en el orden recibido).
 *
 * Una empresa ausente de `estadoPorEmpresa` sale con celdas `cargada: false`: guardar sobre un estado
 * que no se leyó mandaría una lista de módulos incompleta y apagaría los demás.
 *
 * Función PURA: el componente guarda el resultado en un campo.
 */
export function construirMatrizModulos(
  modulos: readonly ModuloMatriz[],
  empresaIds: readonly number[],
  estadoPorEmpresa: ReadonlyMap<number, readonly CompanyPermissionModuleItem[]>
): FilaMatrizModulo[] {
  return [...(modulos ?? [])]
    .sort((a, b) => a.orden - b.orden || a.nombre.localeCompare(b.nombre))
    .map(modulo => ({
      modulo,
      celdas: (empresaIds ?? []).map(companyId => {
        const estado = estadoPorEmpresa.get(companyId);
        const item = estado?.find(i => i.moduleId === modulo.id);
        return {
          companyId,
          moduleId: modulo.id,
          prendido: !!item?.isEnabled,
          habilitados: item?.permisosHabilitados ?? 0,
          total: item?.totalPermisos ?? 0,
          cargada: estado !== undefined
        };
      })
    }));
}

/**
 * Ids de módulo a enviar al prender/apagar UNA celda: los prendidos actuales de la empresa, con ese
 * módulo agregado o quitado. `null` si el estado de la empresa no está cargado (no se debe guardar).
 */
export function modulosTrasCambiar(
  estadoEmpresa: readonly CompanyPermissionModuleItem[] | undefined,
  moduleId: number,
  prender: boolean
): number[] | null {
  if (!estadoEmpresa) return null;
  const ids = estadoEmpresa.filter(m => m.isEnabled && m.moduleId !== moduleId).map(m => m.moduleId);
  return prender ? [...ids, moduleId] : ids;
}
