import type { CompanyPermissionModuleItem } from '../../../../core/services/permission-module/permission-module.service';
import { construirMatrizModulos, modulosTrasCambiar, ModuloMatriz } from './matriz-modulos-empresa.funcion';

describe('construirMatrizModulos', () => {
  const modulos: ModuloMatriz[] = [
    { id: 2, key: 'pollo_engorde', nombre: 'Pollo Engorde', orden: 20 },
    { id: 1, key: 'postura', nombre: 'Postura', orden: 10 }
  ];

  const item = (moduleId: number, isEnabled: boolean, hab = 0, total = 0): CompanyPermissionModuleItem => ({
    moduleId, key: '', nombre: '', orden: 0, isEnabled, permisosHabilitados: hab, totalPermisos: total
  });

  it('ordena las filas por orden y respeta el orden de las empresas', () => {
    const estado = new Map([
      [6, [item(1, true, 5, 9), item(2, false)]],
      [5, [item(1, false), item(2, true, 20, 27)]]
    ]);

    const m = construirMatrizModulos(modulos, [6, 5], estado);

    expect(m.map(f => f.modulo.key)).toEqual(['postura', 'pollo_engorde']);
    expect(m[0].celdas.map(c => [c.companyId, c.prendido, c.habilitados, c.total])).toEqual([
      [6, true, 5, 9],
      [5, false, 0, 0]
    ]);
    expect(m[1].celdas[1].prendido).toBeTrue();
  });

  it('empresa sin estado cargado ⇒ celdas no cargadas y apagadas', () => {
    const m = construirMatrizModulos(modulos, [99], new Map());
    expect(m[0].celdas[0]).toEqual(jasmine.objectContaining({ cargada: false, prendido: false }));
  });
});

describe('modulosTrasCambiar', () => {
  const estado: CompanyPermissionModuleItem[] = [
    { moduleId: 1, key: 'postura', nombre: 'Postura', orden: 10, isEnabled: true, totalPermisos: 9, permisosHabilitados: 5 },
    { moduleId: 2, key: 'pollo_engorde', nombre: 'Pollo Engorde', orden: 20, isEnabled: false, totalPermisos: 27, permisosHabilitados: 0 },
    { moduleId: 3, key: 'tickets', nombre: 'Tickets', orden: 60, isEnabled: true, totalPermisos: 4, permisosHabilitados: 4 }
  ];

  it('prender agrega el módulo a los prendidos', () => {
    expect(modulosTrasCambiar(estado, 2, true)).toEqual([1, 3, 2]);
  });

  it('apagar lo quita y conserva los demás', () => {
    expect(modulosTrasCambiar(estado, 1, false)).toEqual([3]);
  });

  it('sin estado cargado devuelve null (no se debe guardar)', () => {
    expect(modulosTrasCambiar(undefined, 1, true)).toBeNull();
  });
});
