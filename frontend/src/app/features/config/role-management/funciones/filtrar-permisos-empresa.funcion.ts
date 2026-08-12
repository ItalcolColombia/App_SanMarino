// features/config/role-management/funciones/filtrar-permisos-empresa.funcion.ts
import { Permission } from '../../../../core/services/permission/permission.service';

/** Resultado de resolver qué permisos puede llevar un rol según sus empresas. */
export interface PermisosAsignablesResultado {
  /** Keys que la UI puede ofrecer (ya intersectadas entre todas las empresas del rol). */
  asignables: Permission[];
  /**
   * Keys que el rol YA tiene asignadas pero que alguna de sus empresas no habilita. No se pierden en
   * silencio: la UI las muestra para que el admin decida quitarlas.
   */
  huerfanas: string[];
  /** Empresas del rol sin ninguna configuración en `company_permissions` (aportan cero, fail-closed). */
  empresasSinConfigurar: number[];
}

/**
 * Espejo en el front de `CompanyPermissionCalculos.ResolverAsignables` (backend).
 *
 * Reglas: **fail-closed** (un permiso es asignable solo si la empresa lo habilita) e **intersección**
 * cuando el rol pertenece a varias empresas (solo lo que TODAS habilitan). Comparación de keys sin
 * distinguir mayúsculas.
 *
 * Función PURA: sin `this`, sin DI, sin HTTP.
 */
export function resolverPermisosAsignables(
  catalogo: readonly Permission[],
  habilitadasPorEmpresa: ReadonlyMap<number, readonly string[]>,
  empresasDelRol: readonly number[],
  yaAsignadas: readonly string[]
): PermisosAsignablesResultado {
  const empresas = Array.from(new Set(empresasDelRol ?? []));
  const empresasSinConfigurar = empresas.filter(id => !habilitadasPorEmpresa.has(id));

  // Sin empresas seleccionadas no hay contra qué contrastar: no se ofrece nada.
  let permitidas: Set<string> | null = null;
  for (const companyId of empresas) {
    const deEmpresa = new Set(
      (habilitadasPorEmpresa.get(companyId) ?? []).map(k => (k || '').toLowerCase())
    );
    if (permitidas === null) {
      permitidas = deEmpresa;
    } else {
      // Variable intermedia: `permitidas = new Set([...permitidas]...)` se infiere circular (never).
      const previa: ReadonlySet<string> = permitidas;
      permitidas = new Set<string>(Array.from(previa).filter(k => deEmpresa.has(k)));
    }
    if (permitidas.size === 0) break;
  }
  const permitidasSet = permitidas ?? new Set<string>();

  const asignables = (catalogo ?? []).filter(p =>
    permitidasSet.has((p.key || '').toLowerCase())
  );

  const asignablesKeys = new Set(asignables.map(p => (p.key || '').toLowerCase()));
  const huerfanas = Array.from(
    new Set((yaAsignadas ?? []).map(k => (k || '').toLowerCase()).filter(k => k))
  ).filter(k => !asignablesKeys.has(k));

  return { asignables, huerfanas, empresasSinConfigurar };
}
