// features/config/company-management/funciones/permisos-empresa.funcion.ts
import { CompanyPermissionItem } from '../../../../core/services/company-permission/company-permission.service';

/**
 * Filtra el catálogo de permisos de la empresa por texto libre (key o descripción).
 *
 * Función PURA: recibe la lista y el término, devuelve una lista nueva. El componente guarda el
 * resultado en un campo — NO se llama desde el template, porque un getter que aloca un array por
 * ciclo rompe el change detection (NG0103).
 */
export function filtrarPermisosEmpresa(
  items: readonly CompanyPermissionItem[],
  termino: string
): CompanyPermissionItem[] {
  const t = (termino || '').trim().toLowerCase();
  if (!t) return [...items];
  return items.filter(
    p =>
      (p.key || '').toLowerCase().includes(t) ||
      (p.description || '').toLowerCase().includes(t)
  );
}

/**
 * Permisos que la empresa tiene habilitados pero que ningún rol suyo usa todavía, y al revés: los
 * que se están por apagar aunque haya roles usándolos.
 *
 * Se usa para el aviso del modal: apagar un permiso EN USO no borra la asignación del rol — la deja
 * huérfana (no se ofrece más y no viaja en el login), así que el admin tiene que verlo antes de
 * guardar.
 */
export function contarPermisosEnUsoQueSeApagan(
  items: readonly CompanyPermissionItem[]
): number {
  return items.filter(p => !p.isEnabled && p.enUsoPorRoles > 0).length;
}
