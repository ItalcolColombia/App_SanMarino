import { Component, Input, OnChanges, signal, ChangeDetectionStrategy } from '@angular/core';

import {
  ReporteTecnicoProduccionTabsDto,
  ReporteDiarioGalponDto,
  ReporteSemanalGalponDto,
  ReporteTecnicoProduccionLoteInfoDto
} from '../../services/reporte-tecnico.service';
import { ReporteDiarioGalponComponent }   from '../reporte-diario-galpon/reporte-diario-galpon.component';
import { ReporteSemanalGalponComponent }  from '../reporte-semanal-galpon/reporte-semanal-galpon.component';
import { ReporteGeneralDiarioComponent }  from '../reporte-general-diario/reporte-general-diario.component';
import { ReporteGeneralSemanalComponent } from '../reporte-general-semanal/reporte-general-semanal.component';
import { GuiaMetricasDisponibles, GUIA_TODAS_DISPONIBLES } from '../../models/reporte-tecnico-guia.model';
import { normalizarGuiaDisponibilidad } from '../../funciones/normalizar-guia-disponibilidad.funcion';

type TabPrincipal = 'sublote' | 'consolidado';
type TabPeriodo   = 'diario'  | 'semanal';

@Component({
  selector: 'app-reportes-tabs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReporteDiarioGalponComponent,
    ReporteSemanalGalponComponent,
    ReporteGeneralDiarioComponent,
    ReporteGeneralSemanalComponent
],
  templateUrl: './reportes-tabs.component.html',
  styleUrls: ['./reportes-tabs.component.scss']
})
export class ReportesTabsComponent implements OnChanges {
  @Input({ required: true }) reporte!: ReporteTecnicoProduccionTabsDto;

  /**
   * Qué columnas de guía tienen dato, resuelto UNA vez por cambio de reporte. Campo y no getter a
   * propósito: `normalizarGuiaDisponibilidad` devuelve un objeto nuevo, y un getter lo recrearía
   * en cada ciclo de detección de cambios (CLAUDE.md §🧩).
   */
  guiaDisponible: GuiaMetricasDisponibles = GUIA_TODAS_DISPONIBLES;

  ngOnChanges(): void {
    this.guiaDisponible = normalizarGuiaDisponibilidad(this.reporte?.loteInfo?.guiaMetricasDisponibles);
  }

  /** `companies.clasificacion_huevo_por_items` de la empresa del reporte, informado por el backend. */
  get clasificacionHuevoPorItems(): boolean {
    return this.reporte?.loteInfo?.clasificacionHuevoPorItems === true;
  }

  tabPrincipal  = signal<TabPrincipal>('sublote');
  tabPeriodo    = signal<TabPeriodo>('diario');
  subloteActivo = signal<string>('');

  /** Lista de sublotes únicos ordenados por nombre */
  get sublotes(): string[] {
    const nombres = new Set([
      ...this.reporte.diariosGalpon.map(r => r.loteNombre),
      ...this.reporte.semanalesGalpon.map(r => r.loteNombre)
    ]);
    return [...nombres].sort();
  }

  get subloteSeleccionado(): string {
    return this.subloteActivo() || this.sublotes[0] || '';
  }

  get datosDiario(): ReporteDiarioGalponDto[] {
    return this.reporte.diariosGalpon.filter(r => r.loteNombre === this.subloteSeleccionado);
  }

  get datosSemanal(): ReporteSemanalGalponDto[] {
    return this.reporte.semanalesGalpon.filter(r => r.loteNombre === this.subloteSeleccionado);
  }

  get loteInfo(): ReporteTecnicoProduccionLoteInfoDto {
    return this.reporte.loteInfo;
  }

  setPrincipal(tab: TabPrincipal): void {
    this.tabPrincipal.set(tab);
  }

  setPeriodo(tab: TabPeriodo): void {
    this.tabPeriodo.set(tab);
  }

  setSublote(s: string): void {
    this.subloteActivo.set(s);
  }
}
