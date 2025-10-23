// src/app/features/galpon/models/galpon.models.ts

export interface FarmLiteDto     { id: number; name: string; regionalId: number; zoneId: number; }
export interface NucleoLiteDto   { nucleoId: string; nucleoNombre: string; granjaId: number; }
export interface CompanyLiteDto  { id: number; name: string; identifier: string; }

export interface GalponDetailDto {
  galponId: string;
  galponNombre: string;
  nucleoId: string;
  granjaId: number;
  ancho?: string | null;
  largo?: string | null;
  tipoGalpon?: string | null;

  companyId: number;
  createdByUserId?: number | null;
  createdAt?: string | null;       // ISO
  updatedByUserId?: number | null;
  updatedAt?: string | null;

  farm:    FarmLiteDto;
  nucleo:  NucleoLiteDto;
  company: CompanyLiteDto;
}

// Payloads para crear/actualizar (lo que espera el backend)
export interface CreateGalponDto {
  galponId: string;
  galponNombre: string;
  nucleoId: string;
  granjaId: number;
  ancho?: string | null;
  largo?: string | null;
  tipoGalpon?: string | null;
}

export interface UpdateGalponDto extends CreateGalponDto {}
