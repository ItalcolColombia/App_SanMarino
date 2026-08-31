// src/app/features/lote/funciones/agrupar-huevo-items.funcion.spec.ts
import { agruparHuevoItemsPorTipo, seleccionInicialHuevoItems } from './agrupar-huevo-items.funcion';
import { LoteHuevoItemDto } from '../services/lote-huevo-items.service';
import { SIN_CATEGORIA_HUEVO } from '../models/huevo-items.model';

function item(partial: Partial<LoteHuevoItemDto>): LoteHuevoItemDto {
  return {
    id: 0,
    loteId: 0,
    catalogItemId: 1,
    codigo: null,
    nombre: 'HUEVO',
    tipoHuevo: null,
    um: 'UND',
    primeraPostura: false,
    itemActivo: true,
    activo: false,
    ...partial
  };
}

describe('agruparHuevoItemsPorTipo', () => {
  it('respeta el orden en que llegan los ítems, sin reordenar', () => {
    // El backend ya ordena Primera → Pnc → resto (HuevoItemsCalculos.PesoTipoHuevo). Reordenar acá
    // sería una segunda regla para el mismo número: el catálogo saldría distinto según la pantalla.
    const grupos = agruparHuevoItemsPorTipo([
      item({ catalogItemId: 1, tipoHuevo: 'Primera', nombre: 'SIN CLASIFICAR ROJO' }),
      item({ catalogItemId: 2, tipoHuevo: 'Primera', nombre: 'SIN CLASIFICAR BLANCO' }),
      item({ catalogItemId: 3, tipoHuevo: 'Pnc', nombre: 'MANCHADO ROJO' })
    ]);

    expect(grupos.map(g => g.tipoHuevo)).toEqual(['Primera', 'Pnc']);
    expect(grupos[0].items.map(i => i.catalogItemId)).toEqual([1, 2]);
    expect(grupos[1].items.map(i => i.catalogItemId)).toEqual([3]);
  });

  it('agrupa bajo «Sin categoría» los ítems sin tipoHuevo en el metadata', () => {
    const grupos = agruparHuevoItemsPorTipo([
      item({ catalogItemId: 1, tipoHuevo: null }),
      item({ catalogItemId: 2, tipoHuevo: '   ' })
    ]);

    expect(grupos.length).toBe(1);
    expect(grupos[0].tipoHuevo).toBe(SIN_CATEGORIA_HUEVO);
    expect(grupos[0].items.length).toBe(2);
  });

  it('tolera lista vacía y nula sin romper la pantalla', () => {
    expect(agruparHuevoItemsPorTipo([])).toEqual([]);
    expect(agruparHuevoItemsPorTipo(null as unknown as LoteHuevoItemDto[])).toEqual([]);
  });
});

describe('seleccionInicialHuevoItems', () => {
  it('parte de los que el lote ya declaró (activo = true)', () => {
    const seleccion = seleccionInicialHuevoItems([
      item({ catalogItemId: 10, activo: true }),
      item({ catalogItemId: 11, activo: false }),
      item({ catalogItemId: 12, activo: true })
    ]);

    expect([...seleccion].sort()).toEqual([10, 12]);
  });

  it('arranca vacía en el ALTA, donde nada viene marcado', () => {
    // `GET /LoteHuevoItem/por-granja/{granjaId}/disponibles` devuelve todo con activo=false: el lote
    // todavía no existe, así que no hay declaración previa que marcar.
    const seleccion = seleccionInicialHuevoItems([
      item({ catalogItemId: 10 }),
      item({ catalogItemId: 11 })
    ]);

    expect(seleccion.size).toBe(0);
  });
});
