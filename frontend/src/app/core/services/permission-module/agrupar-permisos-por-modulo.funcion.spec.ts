import {
  agruparPermisosPorModulo,
  ModuloParaAgrupar,
  NOMBRE_SIN_CLASIFICAR
} from './agrupar-permisos-por-modulo.funcion';

interface Item { key: string; modulos: string[] }

describe('agruparPermisosPorModulo', () => {
  const modulos: ModuloParaAgrupar[] = [
    { key: 'pollo_engorde', nombre: 'Pollo Engorde', orden: 20 },
    { key: 'postura', nombre: 'Postura', orden: 10 },
    { key: 'inventario', nombre: 'Gestión de Inventario', orden: 40 }
  ];
  const modulosDe = (i: Item) => i.modulos;

  it('agrupa por módulo respetando el orden del catálogo', () => {
    const r = agruparPermisosPorModulo<Item>(
      [
        { key: 'abrir_lote', modulos: ['pollo_engorde'] },
        { key: 'carga_masiva_postura', modulos: ['postura'] }
      ],
      modulosDe,
      modulos
    );

    expect(r.map(g => g.nombre)).toEqual(['Postura', 'Pollo Engorde']);
    expect(r[0].items.map(i => i.key)).toEqual(['carga_masiva_postura']);
  });

  it('un permiso compartido aparece una sola vez, bajo su primer módulo por orden', () => {
    const r = agruparPermisosPorModulo<Item>(
      [{ key: 'lote.corregir_aves', modulos: ['pollo_engorde', 'postura'] }],
      modulosDe,
      modulos
    );

    expect(r.length).toBe(1);
    expect(r[0].moduloKey).toBe('postura');
  });

  it('lo que no tiene módulo conocido va al final en «Sin clasificar»', () => {
    const r = agruparPermisosPorModulo<Item>(
      [
        { key: 'tickets.crear', modulos: [] },
        { key: 'x', modulos: ['modulo_inexistente'] },
        { key: 'editar_registro', modulos: ['INVENTARIO'] }
      ],
      modulosDe,
      modulos
    );

    expect(r.map(g => g.nombre)).toEqual(['Gestión de Inventario', NOMBRE_SIN_CLASIFICAR]);
    expect(r[1].moduloKey).toBeNull();
    expect(r[1].items.map(i => i.key)).toEqual(['tickets.crear', 'x']);
  });

  it('no devuelve grupos vacíos y tolera entradas nulas', () => {
    expect(agruparPermisosPorModulo<Item>([], modulosDe, modulos)).toEqual([]);
    expect(agruparPermisosPorModulo<Item>(null as unknown as Item[], () => null, null as unknown as ModuloParaAgrupar[])).toEqual([]);
  });
});
