// src/app/features/lote-produccion/funciones/modal-seguimiento-diario-calculos.funcion.ts
// Cálculo y mapeo puro del modal de seguimiento diario — extraído de ModalSeguimientoDiarioComponent.
// Funciones PURAS: sin `this`, sin DI, sin estado del componente.

import { ItemInventarioDto } from '../../gestion-inventario/services/gestion-inventario.service';
import { CatalogItemExtended, MetadataSeguimientoNormalizada } from '../models/seguimiento-metadata.model';
import { HuevoFilaFija } from '../models/huevo-clasificacion.model';
import { esItemEnKilos } from './items-huevo-catalogo.funcion';
import {
  ETAPA_CICLO_POSTURA,
  ETAPA_CICLO_FUERA_DE_CICLO,
  EtapaCicloPostura
} from '../../../shared/utils/fecha/semanas-ciclo-postura.funcion';

export function itemEcuadorToExtended(i: ItemInventarioDto): CatalogItemExtended {
  return {
    id: i.id,
    codigo: i.codigo,
    nombre: i.nombre,
    tipoItem: (i.concepto ?? i.tipoItem ?? '').trim() || i.tipoItem,
    unidad: (i.unidad ?? 'kg').trim() || 'kg',
    activo: i.activo,
    metadata: { type_item: i.tipoItem, concepto: i.concepto }
  };
}

/** Convierte cantidad a kg según la unidad declarada (g/gramos → /1000; resto se asume kg). */
export function toKg(cantidad: number, unidad: string | null | undefined): number {
  const u = String(unidad || 'kg').trim().toLowerCase();
  if (u === 'g' || u === 'gramo' || u === 'gramos') return cantidad / 1000;
  return cantidad;
}

/** D2 — los ítems que se PESAN admiten decimales; los que se cuentan, no. */
export function pasoCantidadHuevo(fila: HuevoFilaFija): string {
  return esItemEnKilos(fila.um) ? '0.01' : '1';
}

export function snakeCase(key: string): string {
  return key.replace(/([A-Z])/g, '_$1').toLowerCase().replace(/^_/, '');
}

/** Colapsa la etapa por raza al mismo rango 1/2/3 que persiste `form.etapa` (dato exportable, no aritmético). */
export function etapaCicloANumero(etapa: EtapaCicloPostura): number {
  if (etapa === ETAPA_CICLO_POSTURA) return 2;
  if (etapa === ETAPA_CICLO_FUERA_DE_CICLO) return 3;
  return 1; // Alistamiento / Levante / LevanteEnProduccion — no debería verse en este modal, pero no revienta
}

export function getEtapaLabel(etapa: number): string {
  const labels: { [key: number]: string } = {
    1: 'Etapa 1 (Semana 26-33)',
    2: 'Etapa 2 (Semana 34-50)',
    3: 'Etapa 3 (Semana >50)'
  };
  return labels[etapa] || `Etapa ${etapa}`;
}

/** Hoy en formato YYYY-MM-DD (local, sin zona) para <input type="date"> */
export function todayYMD(): string {
  const d = new Date();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

/** Normaliza cadenas mm/dd/aaaa, dd/mm/aaaa, ISO o Date a YYYY-MM-DD (local) */
export function toYMD(input: string | Date | null | undefined): string | null {
  if (!input) return null;

  if (input instanceof Date && !isNaN(input.getTime())) {
    const y = input.getFullYear();
    const m = String(input.getMonth() + 1).padStart(2, '0');
    const d = String(input.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  const s = String(input).trim();

  // YYYY-MM-DD
  const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
  const m1 = s.match(ymd);
  if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;

  // mm/dd/aaaa o dd/mm/aaaa
  const sl = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
  const m2 = s.match(sl);
  if (m2) {
    let a = parseInt(m2[1], 10);
    let b = parseInt(m2[2], 10);
    const yyyy = parseInt(m2[3], 10);
    let mm = a, dd = b;
    if (a > 12 && b <= 12) { mm = b; dd = a; }
    const mmS = String(mm).padStart(2, '0');
    const ddS = String(dd).padStart(2, '0');
    return `${yyyy}-${mmS}-${ddS}`;
  }

  // ISO (con T). Sin zona → literal; con Z/offset → fecha UTC del instante.
  //
  // 🔴 Misma corrección que en levante y engorde: el regex de arriba está ANCLADO, así que un ISO
  // con 'T' caía a `new Date(s)` + getters LOCALES y restaba un día en UTC-5. Las filas cargadas por
  // migración masiva viven a 00:00 UTC —su convención legítima— y el modal las abría en el día
  // anterior, en desacuerdo con la grilla y provocando un rechazo del backend al guardar.
  if (/^\d{4}-\d{2}-\d{2}T/.test(s)) {
    if (!/(?:Z|[+-]\d{2}:?\d{2})$/.test(s)) return s.slice(0, 10);
    const dIso = new Date(s);
    if (!isNaN(dIso.getTime())) return dIso.toISOString().slice(0, 10);
  }

  // Otros formatos parseables → extracción LOCAL (comportamiento previo)
  const d = new Date(s);
  if (!isNaN(d.getTime())) {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  return null;
}

/** Convierte YYYY-MM-DD a ISO asegurando MEDIODÍA local → evita cruzar de día por zona horaria */
export function ymdToIsoAtNoon(ymd: string): string {
  const iso = new Date(`${ymd}T12:00:00`);
  return iso.toISOString();
}

export function emptyMetadata(): MetadataSeguimientoNormalizada {
  return {
    itemsHembras: [],
    itemsMachos: [],
    consumoOriginalHembras: undefined,
    unidadConsumoOriginalHembras: 'kg',
    consumoOriginalMachos: undefined,
    unidadConsumoOriginalMachos: 'kg',
    tipoItemHembras: null,
    tipoItemMachos: null,
    tipoAlimentoHembras: null,
    tipoAlimentoMachos: null
  };
}
