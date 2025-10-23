// src/app/features/lote/components/tabla-registro-list/tabla-registro-list.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faEye } from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil } from 'rxjs';

import { LoteService, LoteDto, LoteMortalidadResumenDto } from '../../services/lote.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';

export interface TablaLotesEvent {
  type: 'edit' | 'delete' | 'view';
  lote: LoteDto;
}

@Component({
  selector: 'app-tabla-registro-list',
  standalone: true,
  imports: [CommonModule, FontAwesomeModule],
  templateUrl: './tabla-registro-list.component.html',
  styleUrls: ['./tabla-registro-list.component.scss']
})
export class TablaRegistroListComponent implements OnInit, OnDestroy {
  @Input() lotes: LoteDto[] = [];
  @Input() loading: boolean = false;
  @Input() selectedFarmId: number | null = null;
  @Input() selectedFarmName: string = '';

  @Output() loteAction = new EventEmitter<TablaLotesEvent>();
  @Output() createNew = new EventEmitter<void>();

  // Iconos
  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faEye = faEye;

  // Datos maestros para mapeo
  farms: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: GalponDetailDto[] = [];

  // Mapas para búsqueda rápida
  farmMap: Record<number, string> = {};
  nucleoMap: Record<string, string> = {};
  galponMap: Record<string, string> = {};

  // Resúmenes de mortalidad
  resumenMap: Record<number, LoteMortalidadResumenDto> = {};

  private destroy$ = new Subject<void>();

  constructor(
    private loteSvc: LoteService,
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private galponSvc: GalponService
  ) {}

  ngOnInit(): void {
    this.loadMasterData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngOnChanges(): void {
    if (this.lotes.length > 0) {
      this.loadResumenMortalidad();
    }
  }

  private loadMasterData(): void {
    // Cargar datos maestros para mapeo
    this.farmSvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(farms => {
      this.farms = farms;
      this.farmMap = {};
      farms.forEach(f => this.farmMap[f.id] = f.name);
    });

    this.nucleoSvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(nucleos => {
      this.nucleos = nucleos;
      this.nucleoMap = {};
      nucleos.forEach(n => this.nucleoMap[n.nucleoId] = n.nucleoNombre);
    });

    this.galponSvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(galpones => {
      this.galpones = galpones;
      this.galponMap = {};
      galpones.forEach(g => this.galponMap[g.galponId] = g.galponNombre);
    });
  }

  private loadResumenMortalidad(): void {
    const calls = this.lotes.map(l => this.loteSvc.getResumenMortalidad(l.loteId));
    if (calls.length > 0) {
      Promise.all(calls.map(call => call.toPromise())).then(resumenes => {
        resumenes.forEach(r => {
          if (r) {
            this.resumenMap[r.loteId] = r;
          }
        });
      }).catch(error => {
        console.error('Error cargando resúmenes de mortalidad:', error);
      });
    }
  }

  onEditLote(lote: LoteDto): void {
    this.loteAction.emit({ type: 'edit', lote });
  }

  onDeleteLote(lote: LoteDto): void {
    this.loteAction.emit({ type: 'delete', lote });
  }

  onViewLote(lote: LoteDto): void {
    this.loteAction.emit({ type: 'view', lote });
  }

  onCreateNew(): void {
    this.createNew.emit();
  }

  // Métodos de cálculo
  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 0;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    const msDia = 1000 * 60 * 60 * 24;
    return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
  }

  calcularFase(fechaEncaset?: string | Date | null): string {
    const edad = this.calcularEdadDias(fechaEncaset);
    if (edad <= 7) return 'Inicio';
    if (edad <= 21) return 'Crecimiento';
    if (edad <= 42) return 'Engorde';
    return 'Finalización';
  }

  vivasH(lote: LoteDto): number {
    const resumen = this.resumenMap[lote.loteId];
    if (!resumen) return lote.hembrasL || 0;
    return resumen.saldoHembras;
  }

  vivasM(lote: LoteDto): number {
    const resumen = this.resumenMap[lote.loteId];
    if (!resumen) return lote.machosL || 0;
    return resumen.saldoMachos;
  }

  vivasTotales(lote: LoteDto): number {
    return this.vivasH(lote) + this.vivasM(lote);
  }

  formatNumber(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat('es-CO').format(value);
  }

  formatOrDash(value: number | null | undefined): string {
    if (value === null || value === undefined || value === 0) return '—';
    return this.formatNumber(value);
  }

  trackByLote(index: number, lote: LoteDto): number {
    return lote.loteId;
  }

  // Getters para el template
  get hasLotes(): boolean {
    return this.lotes.length > 0;
  }

  get showTable(): boolean {
    return this.selectedFarmId !== null;
  }

  get farmDisplayName(): string {
    return this.selectedFarmName || 'Granja seleccionada';
  }
}
