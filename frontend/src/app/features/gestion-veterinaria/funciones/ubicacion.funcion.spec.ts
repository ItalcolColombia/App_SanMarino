import { etiquetaUbicacion, lotesDeUbicacion } from './ubicacion.funcion';
import { VeterinariaGranjaDto } from '../models/gestion-veterinaria.models';

describe('ubicación de gestión veterinaria', () => {
  const granjas: VeterinariaGranjaDto[] = [{
    id: 7, nombre: 'La Esperanza', latitud: null, longitud: null, totalGalpones: 1, totalLotes: 2,
    lotesSinUbicacion: [{ id: 91, nombre: 'Lote libre', fase: null }],
    nucleos: [{ id: 'N1', nombre: 'Norte', lotesSinGalpon: [], galpones: [{
      id: 'G1', nombre: 'Galpón 1', lotes: [{ id: 92, nombre: 'Lote 92', fase: 'Levante' }],
    }] }],
  }];

  it('limita los lotes al galpón seleccionado', () => {
    expect(lotesDeUbicacion(granjas, { farmId: 7, nucleoId: 'N1', galponId: 'G1', loteId: null }))
      .toEqual([{ id: 92, nombre: 'Lote 92', fase: 'Levante' }]);
  });

  it('construye una etiqueta jerárquica sin niveles vacíos', () => {
    expect(etiquetaUbicacion({ farmNombre: 'La Esperanza', nucleoNombre: 'Norte', galponNombre: null, loteNombre: 'Lote 92' }))
      .toBe('La Esperanza · Norte · Lote 92');
  });
});
