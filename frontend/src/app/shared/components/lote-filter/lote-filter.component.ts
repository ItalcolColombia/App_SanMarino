import { Component, OnInit, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoteService, LoteDto } from '../../../features/lote/services/lote.service';
import { FarmService, FarmDto } from '../../../features/farm/services/farm.service';
import { NucleoService, NucleoDto } from '../../../features/nucleo/services/nucleo.service';
import { GalponService } from '../../../features/galpon/services/galpon.service';
import { GalponDetailDto } from '../../../features/galpon/models/galpon.models';
import { CompanyService, Company } from '../../../core/services/company/company.service';

export interface LoteFilterCriteria {
  companyId?: number | null;
  farmId?: number | null;
  nucleoId?: string | null;
  galponId?: string | null;
  searchTerm?: string;
  selectedLoteId?: string | null;
}

@Component({
  selector: 'app-lote-filter',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lote-filter.component.html',
  styleUrls: ['./lote-filter.component.scss']
})
export class LoteFilterComponent implements OnInit {
  @Input() showLoteSelection = true;
  @Input() placeholder = 'Buscar lote...';
  @Input() filterLabel = 'Filtros de Lote';
  @Output() filterChange = new EventEmitter<LoteFilterCriteria>();
  @Output() loteSelected = new EventEmitter<LoteDto | null>();

  // Signals para estado reactivo
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  
  // Datos maestros
  companies = signal<Company[]>([]);
  farms = signal<FarmDto[]>([]);
  nucleos = signal<NucleoDto[]>([]);
  galpones = signal<GalponDetailDto[]>([]);
  lotes = signal<LoteDto[]>([]);
  
  // Filtros
  filters = signal<LoteFilterCriteria>({});
  
  // Mapas para búsqueda rápida
  farmById = computed(() => {
    const map: Record<number, FarmDto> = {};
    this.farms().forEach(f => map[f.id] = f);
    return map;
  });
  
  // Getters para filtros en cascada (igual que en lote-list.component.ts)
  get filteredFarms(): FarmDto[] {
    const companyId = this.filters().companyId;
    const allFarms = this.farms();
    if (companyId == null) return allFarms;
    return allFarms.filter(f => f.companyId === companyId);
  }
  
  get filteredNucleos(): NucleoDto[] {
    const farmId = this.filters().farmId;
    const companyId = this.filters().companyId;
    
    if (farmId != null) {
      return this.nucleos().filter(n => n.granjaId === farmId);
    }
    if (companyId != null) {
      const farmIds = new Set(this.filteredFarms.map(f => f.id));
      return this.nucleos().filter(n => farmIds.has(n.granjaId));
    }
    return this.nucleos();
  }
  
  get filteredGalpones(): GalponDetailDto[] {
    const farmId = this.filters().farmId;
    const companyId = this.filters().companyId;
    const nucleoId = this.filters().nucleoId;
    
    let arr = this.galpones();
    
    if (farmId != null) {
      arr = arr.filter(g => g.granjaId === farmId);
    } else if (companyId != null) {
      const farmIds = new Set(this.filteredFarms.map(f => f.id));
      arr = arr.filter(g => farmIds.has(g.granjaId));
    }
    
    if (nucleoId != null) {
      arr = arr.filter(g => g.nucleoId === nucleoId);
    }
    
    return arr;
  }
  
  filteredLotes = computed(() => {
    let result = [...this.lotes()];
    const f = this.filters();
    
    // Filtrar por compañía (a través de granja)
    if (f.companyId != null) {
      const farmIds = new Set(this.filteredFarms.map((farm: FarmDto) => farm.id));
      result = result.filter(l => farmIds.has(l.granjaId));
    }
    
    // Filtrar por granja
    if (f.farmId != null) {
      result = result.filter(l => l.granjaId === f.farmId);
    }
    
    // Filtrar por núcleo
    if (f.nucleoId != null) {
      result = result.filter(l => (l.nucleoId ?? null) === f.nucleoId);
    }
    
    // Filtrar por galpón
    if (f.galponId != null) {
      result = result.filter(l => (l.galponId ?? null) === f.galponId);
    }
    
    // Filtrar por término de búsqueda
    if (f.searchTerm) {
      const term = f.searchTerm.toLowerCase();
      result = result.filter(l => {
        const farmName = this.farmById()[l.granjaId]?.name || '';
        const nucleoName = this.nucleos().find(n => n.nucleoId === l.nucleoId)?.nucleoNombre || '';
        const galponName = this.galpones().find(g => g.galponId === l.galponId)?.galponNombre || '';
        
        const haystack = [
          l.loteId ?? '',
          l.loteNombre ?? '',
          farmName,
          nucleoName,
          galponName
        ].join(' ').toLowerCase();
        
        return haystack.includes(term);
      });
    }
    
    return result;
  });

  // Lote seleccionado
  selectedLote = signal<LoteDto | null>(null);

  constructor(
    private loteService: LoteService,
    private farmService: FarmService,
    private nucleoService: NucleoService,
    private galponService: GalponService,
    private companyService: CompanyService
  ) {}

  ngOnInit(): void {
    this.loadInitialData();
  }

  async loadInitialData(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      // Cargar datos en paralelo
      const [companies, farms, nucleos, galpones, lotes] = await Promise.all([
        this.companyService.getAll().toPromise(),
        this.farmService.getAll().toPromise(),
        this.nucleoService.getAll().toPromise(),
        this.galponService.getAll().toPromise(),
        this.loteService.getAll().toPromise()
      ]);

      this.companies.set(companies || []);
      this.farms.set(farms || []);
      this.nucleos.set(nucleos || []);
      this.galpones.set(galpones || []);
      this.lotes.set(lotes || []);
    } catch (error) {
      console.error('Error loading filter data:', error);
      this.error.set('Error al cargar los datos de filtros');
    } finally {
      this.loading.set(false);
    }
  }

  onCompanyChange(companyId: number | null): void {
    const currentFilters = this.filters();
    const newFilters: LoteFilterCriteria = {
      ...currentFilters,
      companyId,
      selectedLoteId: null
    };
    
    // Validar y resetear filtros dependientes si ya no son válidos
    if (newFilters.farmId != null && !this.filteredFarms.some(f => f.id === newFilters.farmId)) {
      newFilters.farmId = null;
    }
    if (newFilters.nucleoId != null && !this.filteredNucleos.some(n => n.nucleoId === newFilters.nucleoId)) {
      newFilters.nucleoId = null;
    }
    if (newFilters.galponId != null && !this.filteredGalpones.some(g => g.galponId === newFilters.galponId)) {
      newFilters.galponId = null;
    }
    
    this.filters.set(newFilters);
    this.selectedLote.set(null);
    this.emitChanges();
  }

  onFarmChange(farmId: number | null): void {
    const currentFilters = this.filters();
    const newFilters: LoteFilterCriteria = {
      ...currentFilters,
      farmId,
      selectedLoteId: null
    };
    
    // Validar y resetear filtros dependientes si ya no son válidos
    if (newFilters.nucleoId != null && !this.filteredNucleos.some(n => n.nucleoId === newFilters.nucleoId)) {
      newFilters.nucleoId = null;
    }
    if (newFilters.galponId != null && !this.filteredGalpones.some(g => g.galponId === newFilters.galponId)) {
      newFilters.galponId = null;
    }
    
    this.filters.set(newFilters);
    this.selectedLote.set(null);
    this.emitChanges();
  }

  onNucleoChange(nucleoId: string | null): void {
    const currentFilters = this.filters();
    const newFilters: LoteFilterCriteria = {
      ...currentFilters,
      nucleoId,
      selectedLoteId: null
    };
    
    // Validar y resetear filtros dependientes si ya no son válidos
    if (newFilters.galponId != null && !this.filteredGalpones.some(g => g.galponId === newFilters.galponId)) {
      newFilters.galponId = null;
    }
    
    this.filters.set(newFilters);
    this.selectedLote.set(null);
    this.emitChanges();
  }

  onGalponChange(galponId: string | null): void {
    const currentFilters = this.filters();
    const newFilters: LoteFilterCriteria = {
      ...currentFilters,
      galponId,
      selectedLoteId: null
    };
    
    this.filters.set(newFilters);
    this.selectedLote.set(null);
    this.emitChanges();
  }

  onSearchChange(searchTerm: string): void {
    const currentFilters = this.filters();
    const newFilters: LoteFilterCriteria = {
      ...currentFilters,
      searchTerm,
      selectedLoteId: null
    };
    
    this.filters.set(newFilters);
    this.selectedLote.set(null);
    this.emitChanges();
  }

  onLoteSelect(loteId: string | null): void {
    const lote = loteId ? this.lotes().find(l => l.loteId === Number(loteId)) || null : null;  // Convert string to number for comparison
    const currentFilters = this.filters();
    const newFilters: LoteFilterCriteria = {
      ...currentFilters,
      selectedLoteId: loteId
    };
    
    this.filters.set(newFilters);
    this.selectedLote.set(lote);
    this.loteSelected.emit(lote);
    this.emitChanges();
  }

  clearFilters(): void {
    const newFilters: LoteFilterCriteria = {
      companyId: null,
      farmId: null,
      nucleoId: null,
      galponId: null,
      searchTerm: '',
      selectedLoteId: null
    };
    this.filters.set(newFilters);
    this.selectedLote.set(null);
    this.loteSelected.emit(null);
    this.emitChanges();
  }

  private emitChanges(): void {
    this.filterChange.emit(this.filters());
  }

  // Utility methods
  getCompanyName(companyId: number): string {
    return this.companies().find(c => c.id === companyId)?.name || '';
  }

  getFarmName(farmId: number): string {
    return this.farms().find(f => f.id === farmId)?.name || '';
  }

  getNucleoName(nucleoId: string): string {
    return this.nucleos().find(n => n.nucleoId === nucleoId)?.nucleoNombre || '';
  }

  getGalponName(galponId: string): string {
    return this.galpones().find(g => g.galponId === galponId)?.galponNombre || '';
  }

  calcularEdadDias(fechaEncaset?: string | Date | null): number {
    if (!fechaEncaset) return 0;
    const inicio = new Date(fechaEncaset);
    const hoy = new Date();
    const msDia = 1000 * 60 * 60 * 24;
    return Math.floor((hoy.getTime() - inicio.getTime()) / msDia) + 1;
  }
}
