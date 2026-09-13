import type { Permission } from '../../../../core/services/permission/permission.service';
import type { PermissionModule } from '../../../../core/services/permission-module/permission-module.service';
import {
  describirCambioPermisos,
  filtrarPermisosCatalogo,
  idsDePermisosDelModulo,
  otrosModulosPorPermiso
} from './catalogo-modulos.funcion';

describe('catalogo-modulos.funcion', () => {
  const permisos: Permission[] = [
    { id: 1, key: 'abrir_lote', description: 'Reabrir un lote liquidado' },
    { id: 2, key: 'lote.corregir_aves', description: 'Corregir encasetamiento' },
    { id: 3, key: 'tickets.crear' }
  ];
  const modulos: PermissionModule[] = [
    { id: 10, key: 'postura', nombre: 'Postura', orden: 10, permissionKeys: ['lote.corregir_aves'], empresasConModulo: 3 },
    { id: 20, key: 'pollo_engorde', nombre: 'Pollo Engorde', orden: 20, permissionKeys: ['ABRIR_LOTE', 'lote.corregir_aves'], empresasConModulo: 2 }
  ];

  it('filtra por key o descripción sin distinguir mayúsculas', () => {
    expect(filtrarPermisosCatalogo(permisos, 'LIQUIDADO').map(p => p.id)).toEqual([1]);
    expect(filtrarPermisosCatalogo(permisos, 'lote').map(p => p.id)).toEqual([1, 2]);
    expect(filtrarPermisosCatalogo(permisos, '  ').length).toBe(3);
  });

  it('otros módulos excluye el que se está editando', () => {
    const r = otrosModulosPorPermiso(modulos, 10);
    expect(r.get('lote.corregir_aves')).toEqual(['Pollo Engorde']);
    expect(r.get('abrir_lote')).toEqual(['Pollo Engorde']);
  });

  it('ids del módulo comparan keys sin mayúsculas', () => {
    expect([...idsDePermisosDelModulo(permisos, modulos[1])]).toEqual([1, 2]);
    expect(idsDePermisosDelModulo(permisos, null).size).toBe(0);
  });

  it('describe el cambio', () => {
    expect(describirCambioPermisos({ empresasAfectadas: 0, permisosPrendidos: 0, permisosApagados: 0 }))
      .toContain('No cambió');
    expect(describirCambioPermisos({ empresasAfectadas: 3, permisosPrendidos: 2, permisosApagados: 5 }))
      .toBe('Guardado: 2 permiso(s) prendido(s) y 5 apagado(s) en 3 empresas.');
  });
});
