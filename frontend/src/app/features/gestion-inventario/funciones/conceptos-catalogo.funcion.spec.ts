import { ItemInventarioDto } from '../services/gestion-inventario.service';
import { conceptoEfectivo, conceptosUnicos, normalizarConcepto } from './conceptos-catalogo.funcion';

/**
 * Contrato del desplegable «Concepto». Existe porque el catálogo de Ecuador tiene el mismo concepto
 * escrito de dos formas (`Otros insumos` 36 ítems / `Otros Insumos` 6) y la lista se armaba con un
 * `Set` sensible a mayúsculas: dos opciones distintas que devuelven exactamente las mismas filas.
 */
function item(over: Partial<ItemInventarioDto> = {}): ItemInventarioDto {
  return {
    id: 1,
    codigo: 'AV0374',
    nombre: 'AV. AMINAPOT 720 1LT 0%',
    tipoItem: 'insumo',
    concepto: 'Otros insumos',
    unidad: 'kg',
    activo: true,
    ...over
  };
}

/** N ítems con el mismo concepto (para expresar frecuencias como en el catálogo real). */
function items(concepto: string | null, cantidad: number, tipoItem = 'insumo'): ItemInventarioDto[] {
  return Array.from({ length: cantidad }, (_, i) => item({ id: i + 1, concepto, tipoItem }));
}

describe('conceptos-catalogo.funcion', () => {
  describe('normalizarConcepto', () => {
    it('usa la misma clave que los filtros (trim + minúsculas)', () => {
      expect(normalizarConcepto('Otros insumos')).toBe('otros insumos');
      expect(normalizarConcepto('Otros Insumos')).toBe('otros insumos');
      expect(normalizarConcepto('  ALIMENTO  ')).toBe('alimento');
      expect(normalizarConcepto(null)).toBe('');
      expect(normalizarConcepto(undefined)).toBe('');
    });
  });

  describe('conceptoEfectivo', () => {
    it('cae a tipoItem cuando el ítem no tiene concepto (167 ítems del catálogo)', () => {
      expect(conceptoEfectivo(item({ concepto: null, tipoItem: 'alimento' }))).toBe('alimento');
      expect(conceptoEfectivo(item({ concepto: undefined, tipoItem: 'alimento' }))).toBe('alimento');
    });

    it('prefiere el concepto cuando existe y lo devuelve trimeado', () => {
      expect(conceptoEfectivo(item({ concepto: '  Alimento ', tipoItem: 'alimento' }))).toBe('Alimento');
    });

    it('sin concepto ni tipoItem devuelve cadena vacía', () => {
      expect(conceptoEfectivo({ concepto: null, tipoItem: '' })).toBe('');
    });
  });

  describe('conceptosUnicos', () => {
    it('sin variantes devuelve todos los conceptos ordenados (comportamiento previo)', () => {
      const lista = conceptosUnicos([
        ...items('Vacuna', 21),
        ...items('Alimento', 8),
        ...items('Medicamento', 31),
        ...items('Gas', 2)
      ]);

      expect(lista).toEqual(['Alimento', 'Gas', 'Medicamento', 'Vacuna']);
    });

    it('catálogo vacío devuelve lista vacía', () => {
      expect(conceptosUnicos([])).toEqual([]);
    });

    it('EL CASO DEL TICKET: «Otros insumos» y «Otros Insumos» son UNA sola opción', () => {
      const lista = conceptosUnicos([
        ...items('Otros insumos', 36),
        ...items('Otros Insumos', 6),
        ...items('Desinfectante', 36)
      ]);

      expect(lista).toEqual(['Desinfectante', 'Otros insumos']);
    });

    it('gana la variante más usada, llegue primero o última', () => {
      expect(conceptosUnicos([...items('Otros Insumos', 6), ...items('Otros insumos', 36)]))
        .toEqual(['Otros insumos']);
      expect(conceptosUnicos([...items('Otros insumos', 6), ...items('Otros Insumos', 36)]))
        .toEqual(['Otros Insumos']);
    });

    it('empate de frecuencia: gana la menor en orden ordinal (determinista)', () => {
      expect(conceptosUnicos([...items('Otros insumos', 6), ...items('Otros Insumos', 6)]))
        .toEqual(['Otros Insumos']);
      expect(conceptosUnicos([...items('Otros Insumos', 6), ...items('Otros insumos', 6)]))
        .toEqual(['Otros Insumos']);
    });

    it('el concepto «Alimento» agrupa con el «alimento» que viene del fallback de tipoItem', () => {
      const lista = conceptosUnicos([
        ...items('Alimento', 8, 'alimento'),
        ...items(null, 61, 'alimento') // sin concepto ⇒ caen a tipoItem
      ]);

      expect(lista).toEqual(['alimento']); // 61 usos > 8
    });

    it('descarta ítems sin concepto ni tipoItem', () => {
      const lista = conceptosUnicos([
        item({ id: 1, concepto: null, tipoItem: '' }),
        item({ id: 2, concepto: '   ', tipoItem: '  ' }),
        ...items('Gas', 2)
      ]);

      expect(lista).toEqual(['Gas']);
    });

    it('los espacios alrededor agrupan con la variante limpia', () => {
      const lista = conceptosUnicos([...items('  Otros insumos ', 2), ...items('Otros insumos', 36)]);

      expect(lista).toEqual(['Otros insumos']);
    });

    it('tres variantes del mismo concepto colapsan a una', () => {
      const lista = conceptosUnicos([
        ...items('alimento', 5, 'alimento'),
        ...items('Alimento', 17, 'alimento'),
        ...items('ALIMENTO', 1, 'alimento')
      ]);

      expect(lista).toEqual(['Alimento']);
    });

    it('la lista queda ordenada con localeCompare, sin importar el orden de entrada', () => {
      const lista = conceptosUnicos([
        ...items('Vacuna', 1),
        ...items('Alimento', 1),
        ...items('Semoviente', 1),
        ...items('Empaques', 1)
      ]);

      expect(lista).toEqual(['Alimento', 'Empaques', 'Semoviente', 'Vacuna']);
    });
  });
});
