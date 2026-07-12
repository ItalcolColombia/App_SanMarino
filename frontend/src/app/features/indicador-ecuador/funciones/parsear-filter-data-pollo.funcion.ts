/**
 * Normalización de la respuesta `LoteReproductoraAveEngorde/filter-data` — función PURA.
 *
 * El endpoint puede devolver claves en camelCase o PascalCase; esta función tolera ambas y devuelve
 * las listas ya tipadas y saneadas (trim de ids, Number() de numéricos). El componente solo asigna
 * el resultado a su estado. Lógica calcada del `subscribe.next` original (sin cambios de salida).
 */
import {
  GalponOption,
  GranjaOption,
  NucleoOption,
  PeLoteAveEngordeItem
} from '../models/indicador-filtros.model';

export interface FilterDataPolloParsed {
  farms: GranjaOption[];
  nucleos: NucleoOption[];
  galpones: GalponOption[];
  lotesAveEngorde: PeLoteAveEngordeItem[];
}

/** Parsea la respuesta cruda (camel/Pascal) a listas tipadas. */
export function parsearFilterDataPollo(raw: unknown): FilterDataPolloParsed {
  const data = (raw ?? {}) as Record<string, unknown>;
  const farms = (data['farms'] ?? data['Farms'] ?? []) as Array<{ id?: number; Id?: number; name?: string; Name?: string }>;
  const nucleos = (data['nucleos'] ?? data['Nucleos'] ?? []) as Array<{ nucleoId?: string; NucleoId?: string; nucleoNombre?: string; NucleoNombre?: string; granjaId?: number; GranjaId?: number }>;
  const galpones = (data['galpones'] ?? data['Galpones'] ?? []) as Array<{ galponId?: string; GalponId?: string; galponNombre?: string; GalponNombre?: string; nucleoId?: string; NucleoId?: string; granjaId?: number; GranjaId?: number }>;
  const lotesRaw = (data['lotesAveEngorde'] ?? data['LotesAveEngorde'] ?? []) as unknown[];

  return {
    farms: Array.isArray(farms)
      ? farms.map((f) => ({ id: Number(f.id ?? f.Id ?? 0), name: String(f.name ?? f.Name ?? '') }))
      : [],
    nucleos: Array.isArray(nucleos)
      ? nucleos.map((n) => ({
          nucleoId: String(n.nucleoId ?? n.NucleoId ?? '').trim(),
          nucleoNombre: n.nucleoNombre ?? n.NucleoNombre,
          granjaId: Number(n.granjaId ?? n.GranjaId ?? 0)
        }))
      : [],
    galpones: Array.isArray(galpones)
      ? galpones.map((g) => ({
          galponId: String(g.galponId ?? g.GalponId ?? '').trim(),
          galponNombre: g.galponNombre ?? g.GalponNombre,
          nucleoId: String(g.nucleoId ?? g.NucleoId ?? '').trim(),
          granjaId: Number(g.granjaId ?? g.GranjaId ?? 0)
        }))
      : [],
    lotesAveEngorde: Array.isArray(lotesRaw)
      ? lotesRaw.map((x: any) => ({
          loteAveEngordeId: Number(x.loteAveEngordeId ?? x.LoteAveEngordeId ?? 0),
          loteNombre: String(x.loteNombre ?? x.LoteNombre ?? ''),
          granjaId: Number(x.granjaId ?? x.GranjaId ?? 0),
          nucleoId: x.nucleoId ?? x.NucleoId ?? null,
          galponId: x.galponId ?? x.GalponId ?? null,
          linea: x.linea ?? x.Linea ?? null,
          fechaEncaset: x.fechaEncaset ?? x.FechaEncaset ?? null
        }))
      : []
  };
}
