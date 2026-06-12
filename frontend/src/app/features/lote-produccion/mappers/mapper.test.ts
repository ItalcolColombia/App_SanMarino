// src/app/features/lote-produccion/mappers/mapper.test.ts
// Pruebas de los mappers (para desarrollo y validación)

import { 
  mapFiltrosVMToProduccionQuery,
  mapProduccionToTablaVM
} from './produccion.mappers';
import { FiltrosVM } from '../services/ui.contracts';

// ==================== DATOS DE PRUEBA ====================

const mockRegistros: any[] = [
  {
    id: 1,
    fechaRegistro: '2024-01-15',
    mortalidadH: 2,
    mortalidadM: 1,
    consumoKg: 45.5,
    huevosTotales: 120,
    huevosIncubables: 115,
    pesoHuevo: 56.2
  }
];

const mockFiltros: FiltrosVM = {
  granjaId: 1,
  nucleoId: '1',
  galponId: '1',
  loteId: 1
};

// ==================== FUNCIONES DE PRUEBA ====================

export function testMappers(): void {
  
  
  // Prueba 1: Mapeo de filtros
  
  const query = mapFiltrosVMToProduccionQuery(mockFiltros);
  
  
  // Prueba 2: Mapeo de tabla
  
  const tablaVM = mapProduccionToTablaVM(mockRegistros);
  
  
  
}

// Función para ejecutar las pruebas desde la consola del navegador
if (typeof window !== 'undefined') {
  (window as any).testProduccionMappers = testMappers;
}