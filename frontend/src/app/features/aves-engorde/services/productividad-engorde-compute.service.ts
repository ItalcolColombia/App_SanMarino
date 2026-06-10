import { Injectable } from '@angular/core';
import { SeguimientoDiarioTablaFilaDto } from './seguimiento-aves-engorde.service';
import {
  ProductividadDiariaFila,
  ProductividadSemanalFila,
  ProductividadEngordeResult
} from './productividad-engorde.models';

/** Factor de conversión kilogramos → libras. */
const LB_POR_KG = 2.20462;

/**
 * Cálculos puros (sin HTTP) para las gráficas de productividad de pollo engorde (Panamá).
 * Se alimenta de `tablaFilas` (precalculado por fn_seguimiento_diario_engorde), ya cargado
 * en el componente de seguimiento. Solo datos reales del seguimiento (sin guía / estándar).
 */
@Injectable({ providedIn: 'root' })
export class ProductividadEngordeComputeService {
  compute(tablaFilas: SeguimientoDiarioTablaFilaDto[]): ProductividadEngordeResult {
    const filas = this.ordenar(tablaFilas);
    if (!filas.length) {
      return { diaria: [], semanal: [] };
    }

    const diaria = this.computeDiaria(filas);
    const semanal = this.computeSemanal(filas);
    return { diaria, semanal };
  }

  // ─── Diaria ────────────────────────────────────────────────────────────────

  private computeDiaria(filas: SeguimientoDiarioTablaFilaDto[]): ProductividadDiariaFila[] {
    const out: ProductividadDiariaFila[] = [];
    let mortAcum = 0;
    let selAcum = 0;

    for (const f of filas) {
      const mortDia = this.mortDia(f);
      const selDia = this.selDia(f);
      mortAcum += mortDia;
      selAcum += selDia;

      const saldoInicio = this.saldoInicioDia(f);
      const pesoKg = this.pesoMixtoKg(f);

      out.push({
        edadDia: f.edadDia,
        gramos: pesoKg > 0 ? pesoKg * 1000 : 0,
        qq: pesoKg > 0 ? (f.saldoAves * pesoKg * LB_POR_KG) / 100 : 0,
        pctMortalidad: saldoInicio > 0 ? (mortDia / saldoInicio) * 100 : 0,
        pctSeleccion: saldoInicio > 0 ? (selDia / saldoInicio) * 100 : 0,
        pctMortSel: saldoInicio > 0 ? ((mortDia + selDia) / saldoInicio) * 100 : 0,
        mortalidadTotalAcum: mortAcum,
        seleccionTotalAcum: selAcum
      });
    }
    return out;
  }

  // ─── Semanal ───────────────────────────────────────────────────────────────

  private computeSemanal(filas: SeguimientoDiarioTablaFilaDto[]): ProductividadSemanalFila[] {
    // Agrupar por semana preservando el orden cronológico.
    const grupos = new Map<number, SeguimientoDiarioTablaFilaDto[]>();
    for (const f of filas) {
      const arr = grupos.get(f.semana);
      if (arr) arr.push(f);
      else grupos.set(f.semana, [f]);
    }

    const out: ProductividadSemanalFila[] = [];
    let mortAcum = 0;
    let selAcum = 0;

    const semanas = [...grupos.keys()].sort((a, b) => a - b);
    for (const semana of semanas) {
      const dias = grupos.get(semana)!;
      const primero = dias[0];
      const ultimo = dias[dias.length - 1];

      const mortSemana = dias.reduce((s, f) => s + this.mortDia(f), 0);
      const selSemana = dias.reduce((s, f) => s + this.selDia(f), 0);
      mortAcum += mortSemana;
      selAcum += selSemana;

      const saldoInicioSemana = this.saldoInicioDia(primero);

      // Peso representativo: último día de la semana con peso > 0.
      const pesoKgRep = this.pesoRepresentativo(dias);
      const grs = pesoKgRep > 0 ? pesoKgRep * 1000 : 0;
      const qq = pesoKgRep > 0 ? (ultimo.saldoAves * pesoKgRep * LB_POR_KG) / 100 : 0;

      // CA acumulada = consumo acumulado (kg) / peso vivo total (kg) al fin de semana.
      const pesoVivoTotalKg = ultimo.saldoAves * pesoKgRep;
      const ca = pesoVivoTotalKg > 0 ? ultimo.acumConsumoKg / pesoVivoTotalKg : 0;

      out.push({
        semana,
        grs,
        qq,
        ca,
        pctMortalidad: saldoInicioSemana > 0 ? (mortSemana / saldoInicioSemana) * 100 : 0,
        pctSeleccion: saldoInicioSemana > 0 ? (selSemana / saldoInicioSemana) * 100 : 0,
        pctMortSel: saldoInicioSemana > 0 ? ((mortSemana + selSemana) / saldoInicioSemana) * 100 : 0,
        mortalidadTotalAcum: mortAcum,
        seleccionTotalAcum: selAcum
      });
    }
    return out;
  }

  // ─── Helpers ─────────────────────────────────────────────────────────────────

  private ordenar(filas: SeguimientoDiarioTablaFilaDto[]): SeguimientoDiarioTablaFilaDto[] {
    return [...(filas ?? [])]
      .filter(f => f != null)
      .sort((a, b) => {
        const fa = (a.fecha ?? '').toString();
        const fb = (b.fecha ?? '').toString();
        if (fa !== fb) return fa.localeCompare(fb);
        return (a.edadDia ?? 0) - (b.edadDia ?? 0);
      });
  }

  private mortDia(f: SeguimientoDiarioTablaFilaDto): number {
    return (f.mortalidadHembras ?? 0) + (f.mortalidadMachos ?? 0);
  }

  private selDia(f: SeguimientoDiarioTablaFilaDto): number {
    return (f.selH ?? 0) + (f.selM ?? 0);
  }

  /** Saldo de aves al inicio del día = saldo final + bajas y despachos del día. */
  private saldoInicioDia(f: SeguimientoDiarioTablaFilaDto): number {
    const bajas =
      this.mortDia(f) +
      this.selDia(f) +
      (f.errorSexajeHembras ?? 0) +
      (f.errorSexajeMachos ?? 0);
    const despachos =
      (f.despachoHembras ?? 0) + (f.despachoMachos ?? 0) + (f.despachoMixtas ?? 0);
    return (f.saldoAves ?? 0) + bajas + despachos;
  }

  /** Peso promedio mixto del día (kg): promedio de pesos H/M disponibles. */
  private pesoMixtoKg(f: SeguimientoDiarioTablaFilaDto): number {
    const h = f.pesoPromHembras ?? 0;
    const m = f.pesoPromMachos ?? 0;
    if (h > 0 && m > 0) return (h + m) / 2;
    return h > 0 ? h : m;
  }

  /** Último día de la semana con peso > 0 (representativo de la semana). */
  private pesoRepresentativo(dias: SeguimientoDiarioTablaFilaDto[]): number {
    for (let i = dias.length - 1; i >= 0; i--) {
      const p = this.pesoMixtoKg(dias[i]);
      if (p > 0) return p;
    }
    return 0;
  }
}
