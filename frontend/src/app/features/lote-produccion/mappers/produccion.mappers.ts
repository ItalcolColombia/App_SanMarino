// src/app/features/lote-produccion/mappers/produccion.mappers.ts
import { FiltrosVM, TablaItemVM, IndicadoresVM, SerieVM, LoteVM, ProduccionQuery } from '../services/ui.contracts';

// Convert DD/MM/YYYY to ISO date
export function convertDMYToISO(dateStr: string): string | null {
  if (!dateStr) return null;
  
  const parts = dateStr.split('/');
  if (parts.length !== 3) return null;
  
  const day = parseInt(parts[0], 10);
  const month = parseInt(parts[1], 10);
  const year = parseInt(parts[2], 10);
  
  if (isNaN(day) || isNaN(month) || isNaN(year)) return null;
  
  const date = new Date(year, month - 1, day);
  return date.toISOString().split('T')[0];
}

// Convert ISO date to DD/MM/YYYY
export function convertISOToDMY(isoDate: string): string {
  if (!isoDate) return '';
  
  const date = new Date(isoDate);
  const day = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const year = date.getFullYear();
  
  return `${day}/${month}/${year}`;
}

export function mapFiltrosVMToProduccionQuery(filtros: FiltrosVM): any {
  return {
    loteId: filtros.loteId || 0,
    page: 1,
    size: 10
  };
}

export function mapProduccionToTablaVM(items: any[]): TablaItemVM[] {
  return items.map(item => ({
    id: item.id,
    fechaRegistro: convertISOToDMY(item.fechaRegistro),
    mortalidadH: item.mortalidadH || 0,
    mortalidadM: item.mortalidadM || 0,
    consumoKg: item.consumoKg || 0,
    huevosTotales: item.huevosTotales || 0,
    huevosIncubables: item.huevosIncubables || 0,
    pesoHuevo: item.pesoHuevo || 0,
    observaciones: item.observaciones
  }));
}

export function mapProduccionToIndicadoresVM(items: any[]): IndicadoresVM {
  if (!items || items.length === 0) {
    return {
      totalMortalidadH: 0,
      totalMortalidadM: 0,
      totalConsumoKg: 0,
      totalHuevosTotales: 0,
      totalHuevosIncubables: 0,
      promedioPesoHuevo: 0,
      promedioConsumoDiario: 0,
      promedioHuevosDiarios: 0
    };
  }

  const totalMortalidadH = items.reduce((sum, item) => sum + (item.mortalidadH || 0), 0);
  const totalMortalidadM = items.reduce((sum, item) => sum + (item.mortalidadM || 0), 0);
  const totalConsumoKg = items.reduce((sum, item) => sum + (item.consumoKg || 0), 0);
  const totalHuevosTotales = items.reduce((sum, item) => sum + (item.huevosTotales || 0), 0);
  const totalHuevosIncubables = items.reduce((sum, item) => sum + (item.huevosIncubables || 0), 0);
  
  const promedioPesoHuevo = items.length > 0 
    ? items.reduce((sum, item) => sum + (item.pesoHuevo || 0), 0) / items.length 
    : 0;
  
  const promedioConsumoDiario = items.length > 0 ? totalConsumoKg / items.length : 0;
  const promedioHuevosDiarios = items.length > 0 ? totalHuevosTotales / items.length : 0;

  return {
    totalMortalidadH,
    totalMortalidadM,
    totalConsumoKg,
    totalHuevosTotales,
    totalHuevosIncubables,
    promedioPesoHuevo: Math.round(promedioPesoHuevo * 100) / 100,
    promedioConsumoDiario: Math.round(promedioConsumoDiario * 100) / 100,
    promedioHuevosDiarios: Math.round(promedioHuevosDiarios * 100) / 100
  };
}

export function mapProduccionToSeriesVM(items: any[]): SerieVM[] {
  if (!items || items.length === 0) {
    return [];
  }

  const fechas = items.map(item => convertISOToDMY(item.fechaRegistro));
  const mortalidadH = items.map(item => item.mortalidadH || 0);
  const mortalidadM = items.map(item => item.mortalidadM || 0);
  const consumoKg = items.map(item => item.consumoKg || 0);
  const huevosTotales = items.map(item => item.huevosTotales || 0);
  const pesoHuevo = items.map(item => item.pesoHuevo || 0);

  return [
    {
      name: 'Mortalidad Hembras',
      data: mortalidadH,
      type: 'line',
      color: '#e74c3c'
    },
    {
      name: 'Mortalidad Machos',
      data: mortalidadM,
      type: 'line',
      color: '#c0392b'
    },
    {
      name: 'Consumo (Kg)',
      data: consumoKg,
      type: 'bar',
      color: '#3498db'
    },
    {
      name: 'Huevos Totales',
      data: huevosTotales,
      type: 'bar',
      color: '#f39c12'
    },
    {
      name: 'Peso Huevo (g)',
      data: pesoHuevo,
      type: 'line',
      color: '#9b59b6'
    }
  ];
}

export function mapProduccionLoteToVM(produccionLote: any): LoteVM {
  return {
    loteId: produccionLote.loteId,
    loteNombre: produccionLote.loteNombre || `Lote ${produccionLote.loteId}`,
    fechaInicio: convertISOToDMY(produccionLote.fechaInicio),
    avesInicialesH: produccionLote.avesInicialesH || 0,
    avesInicialesM: produccionLote.avesInicialesM || 0,
    observaciones: produccionLote.observaciones
  };
}

export function mapFormToCreateDto(formData: any): any {
  return {
    loteId: formData.loteId,
    fechaInicio: convertDMYToISO(formData.fechaInicio),
    avesInicialesH: formData.avesInicialesH || 0,
    avesInicialesM: formData.avesInicialesM || 0,
    observaciones: formData.observaciones
  };
}

export function mapFormToSeguimientoDto(formData: any, produccionLoteId: number): any {
  return {
    produccionLoteId,
    fechaRegistro: convertDMYToISO(formData.fechaRegistro),
    mortalidadH: formData.mortalidadH || 0,
    mortalidadM: formData.mortalidadM || 0,
    consumoKg: formData.consumoKg || 0,
    huevosTotales: formData.huevosTotales || 0,
    huevosIncubables: formData.huevosIncubables || 0,
    pesoHuevo: formData.pesoHuevo || 0,
    observaciones: formData.observaciones
  };
}

export function validateRegistroData(data: any): string[] {
  const errors: string[] = [];
  
  if (!data.fechaRegistro) {
    errors.push('La fecha de registro es requerida');
  }
  
  if (data.mortalidadH < 0) {
    errors.push('La mortalidad de hembras no puede ser negativa');
  }
  
  if (data.mortalidadM < 0) {
    errors.push('La mortalidad de machos no puede ser negativa');
  }
  
  if (data.consumoKg < 0) {
    errors.push('El consumo no puede ser negativo');
  }
  
  if (data.huevosTotales < 0) {
    errors.push('Los huevos totales no pueden ser negativos');
  }
  
  if (data.huevosIncubables < 0) {
    errors.push('Los huevos incubables no pueden ser negativos');
  }
  
  if (data.huevosIncubables > data.huevosTotales) {
    errors.push('Los huevos incubables no pueden ser más que los huevos totales');
  }
  
  if (data.pesoHuevo < 0) {
    errors.push('El peso del huevo no puede ser negativo');
  }
  
  return errors;
}