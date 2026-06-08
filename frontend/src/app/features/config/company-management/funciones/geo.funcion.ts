import { PaisDto } from '../../../../core/services/country/country.service';
import { DepartamentoDto } from '../../../../core/services/department/department.service';
import { CityDto } from '../../../../core/services/city/city.service';
import { GeoMaps, GeoSelects } from '../models/company-management.model';

export function buildGeoMaps(
  paises: PaisDto[],
  departamentos: DepartamentoDto[],
  municipios: CityDto[]
): GeoMaps {
  const countryMapById: Record<number, string> = {};
  const deptMapById: Record<number, string> = {};
  const cityMapById: Record<number, string> = {};
  const deptByCountryId = new Map<number, DepartamentoDto[]>();
  const cityByDeptId = new Map<number, CityDto[]>();

  for (const p of paises) {
    countryMapById[p.paisId] = p.paisNombre;
  }
  for (const d of departamentos) {
    deptMapById[d.departamentoId] = d.departamentoNombre;
    const arr = deptByCountryId.get(d.paisId) ?? [];
    arr.push(d);
    deptByCountryId.set(d.paisId, arr);
  }
  for (const m of municipios) {
    cityMapById[m.municipioId] = m.municipioNombre;
    const arr = cityByDeptId.get(m.departamentoId) ?? [];
    arr.push(m);
    cityByDeptId.set(m.departamentoId, arr);
  }

  return { countryMapById, deptMapById, cityMapById, deptByCountryId, cityByDeptId };
}

export function getStatesForCountry(
  countryId: number | null,
  maps: GeoMaps
): { code: string; name: string }[] {
  if (!countryId) return [];
  return (maps.deptByCountryId.get(countryId) ?? []).map(d => ({
    code: String(d.departamentoId),
    name: d.departamentoNombre
  }));
}

export function getCitiesForDept(depId: number | null, maps: GeoMaps): string[] {
  if (!depId) return [];
  return (maps.cityByDeptId.get(depId) ?? [])
    .map(m => m.municipioNombre)
    .sort((a, b) => a.localeCompare(b));
}

export function findCityIdByName(depId: number | null, name: string, maps: GeoMaps): number | null {
  if (!depId || !name) return null;
  const list = maps.cityByDeptId.get(depId) ?? [];
  return list.find(x => x.municipioNombre.toLowerCase() === name.toLowerCase())?.municipioId ?? null;
}

export function resolveCountryCode(countryId: number | undefined, country: string | undefined): string {
  if (countryId != null) return String(countryId);
  const n = toNumOrNull(country);
  return n != null ? String(n) : '';
}

export function resolveDeptCode(departamentoId: number | undefined, state: string | undefined): string {
  if (departamentoId != null) return String(departamentoId);
  const n = toNumOrNull(state);
  return n != null ? String(n) : '';
}

export function resolveCityName(
  municipioId: number | undefined,
  city: string | undefined,
  maps: GeoMaps
): string {
  if (municipioId != null && maps.cityMapById[municipioId]) return maps.cityMapById[municipioId];
  return city ?? '';
}

export function toNumOrNull(v: unknown): number | null {
  if (v === null || v === undefined) return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}
