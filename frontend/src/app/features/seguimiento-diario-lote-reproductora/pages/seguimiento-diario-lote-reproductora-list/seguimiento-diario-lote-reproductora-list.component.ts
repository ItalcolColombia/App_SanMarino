// src/app/features/lote-reproductora/pages/seguimiento-diario-lote-reproductora-list/seguimiento-diario-lote-reproductora-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';

// Sidebar
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';

// Catálogos y reproductoras
import {
  LoteReproductoraService,
  FarmDto, NucleoDto, LoteDto, LoteReproductoraDto
} from '../../../lote-reproductora/services/lote-reproductora.service';

// Seguimiento diario
import {
  LoteSeguimientoService,
  LoteSeguimientoDto
} from '../../services/lote-seguimiento.service';

// Empresas (igual que en otros módulos)
import { Company, CompanyService } from '../../../../core/services/company/company.service';

interface UiSeguimiento {
  id: number;
  fechaRegistro: string;   // ISO
  loteId: string;
  reproductoraId: string;
  mortalidadHembras: number;
  mortalidadMachos: number;
  selH: number;
  selM: number;
  errorSexajeHembras: number;
  errorSexajeMachos: number;
  tipoAlimento: string;
  consumoKgHembras: number;
  observaciones?: string;
  ciclo: string; // 'Normal' | 'Reforzado'
}

@Component({
  selector: 'app-seguimiento-diario-lote-reproductora-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, SidebarComponent],
  templateUrl: './seguimiento-diario-lote-reproductora-list.component.html',
  styleUrls: ['./seguimiento-diario-lote-reproductora-list.component.scss']
})
export class SeguimientoDiarioLoteReproductoraListComponent implements OnInit {
  // Catálogos (opción A: arreglos mutables)
  companies: Company[] = [];
  granjas:   FarmDto[] = [];
  nucleos:   NucleoDto[] = [];
  lotes:     LoteDto[]  = [];
  repros:    LoteReproductoraDto[] = [];

  // Filtros
  selectedCompanyId: number | null = null;
  selectedGranjaId:  number | null = null;
  selectedNucleoId:  string | null = null;
  selectedLoteId:    string | null = null;
  selectedReproId:   string | null = null;

  // Datos
  seguimientos: UiSeguimiento[] = [];

  // UI
  loading = false;
  modalOpen = false;
  form!: FormGroup;
  editing: UiSeguimiento | null = null;

  // Anti-race
  private nucleosReq = 0;
  private lotesReq = 0;
  private reproReq = 0;

  constructor(
    private fb: FormBuilder,
    private companySvc: CompanyService,
    private reproSvc: LoteReproductoraService,
    private segApi: LoteSeguimientoService
  ) {}

  // Getters para nombres y filtrados
  get granjasFiltradas(): FarmDto[] {
    if (!this.selectedCompanyId) return this.granjas;
    return this.granjas.filter(g => g.companyId === this.selectedCompanyId);
  }
  get selectedCompanyName(): string {
    return this.companies.find(c => c.id === this.selectedCompanyId)?.name ?? '';
  }
  get selectedGranjaName(): string {
    return this.granjas.find(g => g.id === this.selectedGranjaId)?.name ?? '';
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      fechaRegistro:      [new Date().toISOString().substring(0,10), Validators.required],
      loteId:             ['', Validators.required],
      reproductoraId:     ['', Validators.required],
      mortalidadHembras:  [0, [Validators.required, Validators.min(0)]],
      mortalidadMachos:   [0, [Validators.required, Validators.min(0)]],
      selH:               [0, [Validators.required, Validators.min(0)]],
      selM:               [0, [Validators.required, Validators.min(0)]],
      errorSexajeHembras: [0, [Validators.required, Validators.min(0)]],
      errorSexajeMachos:  [0, [Validators.required, Validators.min(0)]],
      tipoAlimento:       ['', Validators.required],
      consumoKgHembras:   [0, [Validators.required, Validators.min(0)]],
      observaciones:      [''],
      ciclo:              ['Normal', Validators.required]
    });

    // Cargar catálogos (copias mutables)
    this.companySvc.getAll().subscribe({
      next: (cs) => (this.companies = [...(cs ?? [])]),
      error: ()   => (this.companies = [])
    });
    this.reproSvc.getGranjas().subscribe({
      next: (fs) => (this.granjas = [...(fs ?? [])]),
      error: ()   => (this.granjas = [])
    });
  }

  // ===== Cascada =====
  onCompanyChange(): void {
    this.selectedGranjaId = null;
    this.selectedNucleoId = null;
    this.selectedLoteId   = null;
    this.selectedReproId  = null;

    this.nucleos = [];
    this.lotes   = [];
    this.repros  = [];
    this.seguimientos = [];
  }

  onGranjaChange(): void {
    this.selectedNucleoId = null;
    this.selectedLoteId   = null;
    this.selectedReproId  = null;

    this.nucleos = [];
    this.lotes   = [];
    this.repros  = [];
    this.seguimientos = [];

    if (!this.selectedGranjaId) return;

    const reqId = ++this.nucleosReq;
    this.reproSvc.getNucleosPorGranja(this.selectedGranjaId).subscribe({
      next: (n) => {
        if (reqId !== this.nucleosReq) return;
        const gid = Number(this.selectedGranjaId);
        // Doble garantía: si el back no filtró, filtramos aquí
        this.nucleos = [...(n ?? [])].filter(x => Number(x.granjaId) === gid);
      },
      error: () => { if (reqId !== this.nucleosReq) return; this.nucleos = []; }
    });
  }

  onNucleoChange(): void {
    this.selectedLoteId   = null;
    this.selectedReproId  = null;

    this.lotes   = [];
    this.repros  = [];
    this.seguimientos = [];

    if (!this.selectedGranjaId || !this.selectedNucleoId) return;

    const reqId = ++this.lotesReq;
    this.reproSvc.getLotes().subscribe({
      next: (l) => {
        if (reqId !== this.lotesReq) return;
        const gid = String(this.selectedGranjaId);
        const nid = String(this.selectedNucleoId);
        const src = [...(l ?? [])];
        this.lotes = src.filter(x => String(x.granjaId) === gid && String(x.nucleoId) === nid);
      },
      error: () => { if (reqId !== this.lotesReq) return; this.lotes = []; }
    });
  }

  onLoteChange(): void {
    this.selectedReproId = null;
    this.repros = [];
    this.seguimientos = [];

    if (!this.selectedLoteId) return;

    const reqId = ++this.reproReq;
    this.loading = true;
    this.reproSvc.getByLoteId(this.selectedLoteId)
      .pipe(finalize(() => { if (reqId === this.reproReq) this.loading = false; }))
      .subscribe({
        next: (rows) => { if (reqId !== this.reproReq) return; this.repros = [...(rows ?? [])]; },
        error: () =>   { if (reqId !== this.reproReq) return; this.repros = []; }
      });
  }

  onReproChange(): void {
    this.seguimientos = [];
    if (!this.selectedLoteId || !this.selectedReproId) return;

    this.loading = true;
    this.segApi
      .getByLoteYRepro(this.selectedLoteId, this.selectedReproId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (rows) => (this.seguimientos = (rows ?? []).map(r => this.apiToUi(r))),
        error: () => (this.seguimientos = [])
      });
  }

  // ===== CRUD =====
  create(): void {
    if (!this.selectedLoteId || !this.selectedReproId) return;

    this.editing = null;
    this.form.reset({
      fechaRegistro: new Date().toISOString().substring(0, 10),
      loteId: this.selectedLoteId,
      reproductoraId: this.selectedReproId,
      mortalidadHembras: 0, mortalidadMachos: 0,
      selH: 0, selM: 0,
      errorSexajeHembras: 0, errorSexajeMachos: 0,
      tipoAlimento: '',
      consumoKgHembras: 0,
      observaciones: '',
      ciclo: 'Normal'
    });
    this.modalOpen = true;
  }

  edit(row: UiSeguimiento): void {
    this.editing = row;
    this.form.patchValue({
      fechaRegistro: row.fechaRegistro?.substring(0, 10),
      loteId: row.loteId,
      reproductoraId: row.reproductoraId,
      mortalidadHembras: row.mortalidadHembras,
      mortalidadMachos: row.mortalidadMachos,
      selH: row.selH,
      selM: row.selM,
      errorSexajeHembras: row.errorSexajeHembras,
      errorSexajeMachos: row.errorSexajeMachos,
      tipoAlimento: row.tipoAlimento,
      consumoKgHembras: row.consumoKgHembras,
      observaciones: row.observaciones ?? '',
      ciclo: row.ciclo ?? 'Normal'
    });
    this.modalOpen = true;
  }

  cancel(): void {
    this.modalOpen = false;
    this.editing = null;
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    const raw = this.form.value;
    const payload = this.uiToApi(raw);

    this.loading = true;

    const obs$ = this.editing
      ? this.segApi.update({ id: this.editing.id, ...payload })
      : this.segApi.create(payload);

    obs$
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => {
          this.modalOpen = false;
          this.editing = null;
          this.onReproChange();
        },
        error: () => { /* opcional: toast */ }
      });
  }

  delete(id: number): void {
    if (!confirm('¿Eliminar este registro?')) return;
    this.loading = true;
    this.segApi.delete(id)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: () => this.onReproChange(),
        error: () => this.onReproChange()
      });
  }

  // ===== Mapas UI ↔ API =====
  private uiToApi(raw: any): Omit<LoteSeguimientoDto, 'id'> {
    const toIso = (d: string) => new Date(d).toISOString();
    return {
      fecha: toIso(raw.fechaRegistro),
      loteId: raw.loteId,
      reproductoraId: raw.reproductoraId,
      mortalidadH: raw.mortalidadHembras,
      mortalidadM: raw.mortalidadMachos,
      selH: raw.selH,
      selM: raw.selM,
      errorH: raw.errorSexajeHembras,
      errorM: raw.errorSexajeMachos,
      tipoAlimento: raw.tipoAlimento,
      consumoAlimento: raw.consumoKgHembras,
      observaciones: raw.observaciones ?? null,
      pesoInicial: null,
      pesoFinal: null
    };
  }

  private apiToUi(x: LoteSeguimientoDto): UiSeguimiento {
    return {
      id: x.id,
      fechaRegistro: x.fecha,
      loteId: x.loteId ?? '',
      reproductoraId: x.reproductoraId ?? '',
      mortalidadHembras: x.mortalidadH ?? 0,
      mortalidadMachos: x.mortalidadM ?? 0,
      selH: x.selH ?? 0,
      selM: x.selM ?? 0,
      errorSexajeHembras: x.errorH ?? 0,
      errorSexajeMachos: x.errorM ?? 0,
      tipoAlimento: x.tipoAlimento ?? '',
      consumoKgHembras: x.consumoAlimento ?? 0,
      observaciones: x.observaciones ?? '',
      ciclo: 'Normal'
    };
  }

  // Helpers
  trackById = (_: number, r: UiSeguimiento) => r.id;
  trackByIdx = (i: number) => i;
}
