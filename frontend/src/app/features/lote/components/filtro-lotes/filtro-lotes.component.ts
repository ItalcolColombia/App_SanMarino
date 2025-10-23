// src/app/features/lote/components/filtro-lotes/filtro-lotes.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faMagnifyingGlass } from '@fortawesome/free-solid-svg-icons';
import { Subject, takeUntil } from 'rxjs';

import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../nucleo/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';
import { Company, CompanyService } from '../../../../core/services/company/company.service';

export interface FiltroLotesEvent {
  companyId: number | null;
  farmId: number | null;
  nucleoId: string | null;
  galponId: string | null;
  searchTerm: string;
  sortKey: 'edad' | 'fecha';
  sortDir: 'asc' | 'desc';
}

@Component({
  selector: 'app-filtro-lotes',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './filtro-lotes.component.html',
  styleUrls: ['./filtro-lotes.component.scss']
})
export class FiltroLotesComponent implements OnInit, OnDestroy {
  @Input() selectedCompanyId: number | null = null;
  @Input() selectedFarmId: number | null = null;
  @Input() selectedNucleoId: string | null = null;
  @Input() selectedGalponId: string | null = null;
  @Input() filtro: string = '';
  @Input() sortKey: 'edad' | 'fecha' = 'edad';
  @Input() sortDir: 'asc' | 'desc' = 'desc';

  @Output() filtrosChanged = new EventEmitter<FiltroLotesEvent>();
  @Output() resetFiltros = new EventEmitter<void>();

  // Iconos
  faMagnifyingGlass = faMagnifyingGlass;

  // Datos maestros
  companies: Company[] = [];
  farms: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: GalponDetailDto[] = [];

  // Filtros en cascada
  farmsFiltered: FarmDto[] = [];
  nucleosFiltered: NucleoDto[] = [];
  galponesFiltered: GalponDetailDto[] = [];

  private destroy$ = new Subject<void>();

  constructor(
    private farmSvc: FarmService,
    private nucleoSvc: NucleoService,
    private galponSvc: GalponService,
    private companySvc: CompanyService
  ) {}

  ngOnInit(): void {
    this.loadMasterData();
    this.setupCascadingFilters();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadMasterData(): void {
    // Cargar datos maestros
    this.companySvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(companies => {
      this.companies = companies;
    });

    this.farmSvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(farms => {
      this.farms = farms;
      this.updateFarmsFiltered();
    });

    this.nucleoSvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(nucleos => {
      this.nucleos = nucleos;
      this.updateNucleosFiltered();
    });

    this.galponSvc.getAll().pipe(takeUntil(this.destroy$)).subscribe(galpones => {
      this.galpones = galpones;
      this.updateGalponesFiltered();
    });
  }

  private setupCascadingFilters(): void {
    // Los filtros en cascada se manejan a través de los métodos de cambio
  }

  private updateFarmsFiltered(): void {
    if (this.selectedCompanyId == null) {
      this.farmsFiltered = this.farms;
    } else {
      this.farmsFiltered = this.farms.filter(f => f.companyId === this.selectedCompanyId);
    }
  }

  private updateNucleosFiltered(): void {
    if (this.selectedFarmId != null) {
      this.nucleosFiltered = this.nucleos.filter(n => n.granjaId === this.selectedFarmId);
    } else if (this.selectedCompanyId != null) {
      const farmIds = new Set(this.farmsFiltered.map(f => f.id));
      this.nucleosFiltered = this.nucleos.filter(n => farmIds.has(n.granjaId));
    } else {
      this.nucleosFiltered = this.nucleos;
    }
  }

  private updateGalponesFiltered(): void {
    if (this.selectedNucleoId != null) {
      this.galponesFiltered = this.galpones.filter(g => g.nucleoId === this.selectedNucleoId);
    } else if (this.selectedFarmId != null) {
      this.galponesFiltered = this.galpones.filter(g => g.granjaId === this.selectedFarmId);
    } else {
      this.galponesFiltered = this.galpones;
    }
  }

  onCompanyChange(companyId: number | null): void {
    this.selectedCompanyId = companyId;
    this.updateFarmsFiltered();
    
    // Resetear filtros dependientes
    if (this.selectedFarmId != null && !this.farmsFiltered.some(f => f.id === this.selectedFarmId)) {
      this.selectedFarmId = null;
    }
    if (this.selectedNucleoId != null && !this.nucleosFiltered.some(n => n.nucleoId === this.selectedNucleoId)) {
      this.selectedNucleoId = null;
    }
    if (this.selectedGalponId != null && !this.galponesFiltered.some(g => g.galponId === this.selectedGalponId)) {
      this.selectedGalponId = null;
    }
    
    this.updateNucleosFiltered();
    this.updateGalponesFiltered();
    this.emitFiltrosChanged();
  }

  onFarmChange(farmId: number | null): void {
    this.selectedFarmId = farmId;
    this.updateNucleosFiltered();
    
    // Resetear filtros dependientes
    if (this.selectedNucleoId != null && !this.nucleosFiltered.some(n => n.nucleoId === this.selectedNucleoId)) {
      this.selectedNucleoId = null;
    }
    if (this.selectedGalponId != null && !this.galponesFiltered.some(g => g.galponId === this.selectedGalponId)) {
      this.selectedGalponId = null;
    }
    
    this.updateGalponesFiltered();
    this.emitFiltrosChanged();
  }

  onNucleoChange(nucleoId: string | null): void {
    this.selectedNucleoId = nucleoId;
    this.updateGalponesFiltered();
    
    // Resetear filtros dependientes
    if (this.selectedGalponId != null && !this.galponesFiltered.some(g => g.galponId === this.selectedGalponId)) {
      this.selectedGalponId = null;
    }
    
    this.emitFiltrosChanged();
  }

  onGalponChange(galponId: string | null): void {
    this.selectedGalponId = galponId;
    this.emitFiltrosChanged();
  }

  onSearchChange(searchTerm: string): void {
    this.filtro = searchTerm;
    this.emitFiltrosChanged();
  }

  onSortKeyChange(sortKey: 'edad' | 'fecha'): void {
    this.sortKey = sortKey;
    this.emitFiltrosChanged();
  }

  onSortDirChange(sortDir: 'asc' | 'desc'): void {
    this.sortDir = sortDir;
    this.emitFiltrosChanged();
  }

  onResetFiltros(): void {
    this.selectedCompanyId = null;
    this.selectedFarmId = null;
    this.selectedNucleoId = null;
    this.selectedGalponId = null;
    this.filtro = '';
    this.sortKey = 'edad';
    this.sortDir = 'desc';
    
    this.updateFarmsFiltered();
    this.updateNucleosFiltered();
    this.updateGalponesFiltered();
    
    this.resetFiltros.emit();
  }

  private emitFiltrosChanged(): void {
    const event: FiltroLotesEvent = {
      companyId: this.selectedCompanyId,
      farmId: this.selectedFarmId,
      nucleoId: this.selectedNucleoId,
      galponId: this.selectedGalponId,
      searchTerm: this.filtro,
      sortKey: this.sortKey,
      sortDir: this.sortDir
    };
    this.filtrosChanged.emit(event);
  }

  // Getters para el template
  get hasFarmSelected(): boolean {
    return this.selectedFarmId !== null;
  }

  get farmSelectionRequired(): boolean {
    return !this.hasFarmSelected;
  }
}
