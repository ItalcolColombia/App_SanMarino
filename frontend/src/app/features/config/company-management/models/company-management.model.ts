import { PaisDto } from '../../../../core/services/country/country.service';
import { DepartamentoDto } from '../../../../core/services/department/department.service';
import { CityDto } from '../../../../core/services/city/city.service';

export interface GeoMaps {
  countryMapById: Record<number, string>;
  deptMapById: Record<number, string>;
  cityMapById: Record<number, string>;
  deptByCountryId: Map<number, DepartamentoDto[]>;
  cityByDeptId: Map<number, CityDto[]>;
}

export interface GeoSelects {
  countries: { code: string; name: string }[];
  states: { code: string; name: string }[];
  cities: string[];
}

export interface LogoValidationResult {
  dataUrl: string | null;
  error: string | null;
}

export interface PaisesDiff {
  toAdd: { companyId: number; paisId: number }[];
  toRemove: { companyId: number; paisId: number }[];
}
