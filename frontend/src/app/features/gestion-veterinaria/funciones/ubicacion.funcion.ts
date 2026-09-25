import {
  TareaCampoDto,
  VeterinariaGalponDto,
  VeterinariaGranjaDto,
  VeterinariaLoteDto,
  VeterinariaNucleoDto,
} from '../models/gestion-veterinaria.models';

export interface SeleccionUbicacion {
  farmId: number | null;
  nucleoId: string | null;
  galponId: string | null;
  loteId: number | null;
}

export function granjaSeleccionada(
  granjas: VeterinariaGranjaDto[], farmId: number | null
): VeterinariaGranjaDto | null {
  return granjas.find((granja) => granja.id === farmId) ?? null;
}

export function nucleosDeGranja(
  granjas: VeterinariaGranjaDto[], farmId: number | null
): VeterinariaNucleoDto[] {
  return granjaSeleccionada(granjas, farmId)?.nucleos ?? [];
}

export function galponesDeNucleo(
  granjas: VeterinariaGranjaDto[], farmId: number | null, nucleoId: string | null
): VeterinariaGalponDto[] {
  return nucleosDeGranja(granjas, farmId).find((nucleo) => nucleo.id === nucleoId)?.galpones ?? [];
}

export function lotesDeUbicacion(
  granjas: VeterinariaGranjaDto[], seleccion: SeleccionUbicacion
): VeterinariaLoteDto[] {
  const granja = granjaSeleccionada(granjas, seleccion.farmId);
  if (!granja) return [];
  if (seleccion.galponId) {
    return granja.nucleos.flatMap((nucleo) => nucleo.galpones)
      .find((galpon) => galpon.id === seleccion.galponId)?.lotes ?? [];
  }
  if (seleccion.nucleoId) {
    const nucleo = granja.nucleos.find((item) => item.id === seleccion.nucleoId);
    return nucleo ? [...nucleo.lotesSinGalpon, ...nucleo.galpones.flatMap((galpon) => galpon.lotes)] : [];
  }
  return [
    ...granja.lotesSinUbicacion,
    ...granja.nucleos.flatMap((nucleo) => [
      ...nucleo.lotesSinGalpon,
      ...nucleo.galpones.flatMap((galpon) => galpon.lotes),
    ]),
  ];
}

export function etiquetaUbicacion(tarea: Pick<TareaCampoDto,
  'farmNombre' | 'nucleoNombre' | 'galponNombre' | 'loteNombre'>): string {
  return [tarea.farmNombre, tarea.nucleoNombre, tarea.galponNombre, tarea.loteNombre]
    .filter((value): value is string => !!value?.trim())
    .join(' · ');
}

export function mensajeErrorHttp(error: any, fallback: string): string {
  return error?.error?.error || error?.error?.message || error?.message || fallback;
}
