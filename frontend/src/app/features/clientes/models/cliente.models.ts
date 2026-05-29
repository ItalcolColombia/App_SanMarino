export interface ClienteDto {
  id: number;
  tipoDocumento: string;
  numeroIdentificacion: string;
  nombre: string;
  correo:   string | null;
  telefono: string | null;
  tipoCliente: string | null;
  pais:       string | null;
  provincia:  string | null;
  distrito:   string | null;
  planta:     string | null;
  zona:       string | null;
  status: string;
  companyId: number;
  createdByUserId: number;
  createdAt: string;
  updatedByUserId: number | null;
  updatedAt: string | null;
  deletedAt: string | null;
}

export interface CreateClienteRequest {
  tipoDocumento: string;
  numeroIdentificacion: string;
  nombre: string;
  correo:      string | null;
  telefono:    string | null;
  tipoCliente: string | null;
  pais:        string | null;
  provincia:   string | null;
  distrito:    string | null;
  planta:      string | null;
  zona:        string | null;
}

export interface UpdateClienteRequest extends CreateClienteRequest {
  status: string;
}

export interface ClienteSearchRequest {
  search?:        string;
  tipoCliente?:   string;
  pais?:          string;
  zona?:          string;
  tipoDocumento?: string;
  soloActivos?:   boolean;
  sortBy?:        string;
  sortDesc?:      boolean;
  page?:          number;
  pageSize?:      number;
}

export interface PagedResult<T> {
  page:     number;
  pageSize: number;
  total:    number;
  items:    T[];
}

export const TIPOS_DOCUMENTO = [
  'Cédula', 'Pasaporte', 'RUC', 'NIT', 'DNI', 'Otro'
] as const;

export const TIPOS_CLIENTE = [
  'Propietario', 'Arrendador','Otro'
] as const;

/**
 * @deprecated Fallback estático; los países ahora se cargan desde el endpoint /Pais
 * mediante PaisService. Mantener solo por compatibilidad si algún componente legacy lo usa.
 */
export const PAISES = [
   'Panamá'
] as const;

export const ZONAS_PANAMA = ['Zona 1', 'Zona 2'] as const;
