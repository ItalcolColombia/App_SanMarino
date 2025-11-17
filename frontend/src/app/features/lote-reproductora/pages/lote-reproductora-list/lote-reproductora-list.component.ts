// app/features/lote-reproductora/pages/lote-reproductora-list/lote-reproductora-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder, FormGroup, ReactiveFormsModule, FormArray
} from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';

import { finalize } from 'rxjs/operators';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faEye } from '@fortawesome/free-solid-svg-icons';

import {
  LoteReproductoraService,
  FarmDto, NucleoDto, LoteDto,
  CreateLoteReproductoraDto, LoteReproductoraDto
} from '../../services/lote-reproductora.service';

interface LoteDtoExtendido {
  loteId: string; loteNombre: string; granjaId: number;
  nucleoId?: number; galponId?: number; regional?: string; fechaEncaset?: string;
  hembrasL?: number; machosL?: number; mixtas?: number; avesEncasetadas?: number;
  pesoInicialM?: number; pesoInicialH?: number; pesoMixto?: number;
}

@Component({
  selector: 'app-lote-reproductora-list',
  standalone: true,
  templateUrl: './lote-reproductora-list.component.html',
  styleUrls: ['./lote-reproductora-list.component.scss'],
  imports: [CommonModule, FormsModule, ReactiveFormsModule, HttpClientModule, FontAwesomeModule]
})
export class LoteReproductoraListComponent implements OnInit {
  // Icons
  faPlus = faPlus; faPen = faPen; faTrash = faTrash; faEye = faEye;

  // Data
  readonly granjas: FarmDto[] = [];
  nucleos: NucleoDto[] = [];
  lotes: LoteDto[] = [];
  registros: LoteReproductoraDto[] = [];

  // Filtros
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedLoteId: string | null = null;

  // Resumen
  loteSeleccionado: LoteDtoExtendido | null = null;

  // UI state
  loading = false;
  form!: FormGroup;
  modalOpen = false;
  editing: LoteReproductoraDto | null = null;
  detalleOpen = false;
  detalleData: LoteReproductoraDto | null = null;

  // Bulk mode
  bulkMode = false;
  bulkCount = 1;

  // anti-race
  private nucleosReq = 0;
  private lotesReq = 0;
  private registrosReq = 0;

  constructor(private fb: FormBuilder, private svc: LoteReproductoraService) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      // single (para edición o crear 1)
      loteId: [''],
      nombreLote: [''],
      reproductoraId: [''],
      fechaEncasetamiento: [''],
      m: [null], h: [null], mixtas: [null],
      mortCajaH: [null], mortCajaM: [null],
      unifH: [null], unifM: [null],
      pesoInicialM: [null], pesoInicialH: [null],

      // multiple
      incubadoras: this.fb.array([] as FormGroup[])
    });

    this.svc.getGranjas().subscribe(r => {
      (this.granjas as FarmDto[]).push(...r);
    });
  }

  // ---- helpers bulk ----
  get incubadoras(): FormArray<FormGroup> {
    return this.form.get('incubadoras') as FormArray<FormGroup>;
  }

  private buildIncubadoraGroup(prefill?: Partial<CreateLoteReproductoraDto>): FormGroup {
    return this.fb.group({
      // lote se setea con selectedLoteId al armar el DTO
      nombreLote: [prefill?.nombreLote ?? this.loteSeleccionado?.loteNombre ?? ''],
      reproductoraId: [prefill?.reproductoraId ?? ''],
      fechaEncasetamiento: [
        prefill?.fechaEncasetamiento ? String(prefill.fechaEncasetamiento).slice(0, 10) : ''
      ],
      m: [prefill?.m ?? null],
      h: [prefill?.h ?? null],
      mixtas: [prefill?.mixtas ?? null],
      mortCajaH: [prefill?.mortCajaH ?? null],
      mortCajaM: [prefill?.mortCajaM ?? null],
      unifH: [prefill?.unifH ?? null],
      unifM: [prefill?.unifM ?? null],
      pesoInicialM: [prefill?.pesoInicialM ?? null],
      pesoInicialH: [prefill?.pesoInicialH ?? null],
    });
  }

  regenerateIncubadoras(n: number) {
    const count = Math.max(1, Math.min(50, Number(n) || 1)); // límite sano
    while (this.incubadoras.length < count) this.incubadoras.push(this.buildIncubadoraGroup());
    while (this.incubadoras.length > count) this.incubadoras.removeAt(this.incubadoras.length - 1);
  }

  addIncubadora() {
    this.incubadoras.push(this.buildIncubadoraGroup());
    this.bulkCount = this.incubadoras.length;
  }
  removeIncubadora(i: number) {
    if (this.incubadoras.length <= 1) return;
    this.incubadoras.removeAt(i);
    this.bulkCount = this.incubadoras.length;
  }

  private toDtoFromGroup(g: FormGroup): CreateLoteReproductoraDto {
    const v = g.value;
    // el service convierte 'yyyy-MM-dd' a ISO, pero aquí lo dejamos en ISO igual
    const fecha = v.fechaEncasetamiento ? new Date(v.fechaEncasetamiento as string).toISOString() : null;
    return {
      loteId: this.selectedLoteId!, // bajo el flow, todos van al lote seleccionado
      reproductoraId: v.reproductoraId?.trim() || 'Sanmarino',
      nombreLote: v.nombreLote?.trim() || '',
      fechaEncasetamiento: fecha,
      m: v.m ?? null,
      h: v.h ?? null,
      mixtas: v.mixtas ?? null,
      mortCajaH: v.mortCajaH ?? null,
      mortCajaM: v.mortCajaM ?? null,
      unifH: v.unifH ?? null,
      unifM: v.unifM ?? null,
      pesoInicialM: v.pesoInicialM ?? null,
      pesoInicialH: v.pesoInicialH ?? null,
      // pesoMixto: (si lo agregas en UI)
    } as CreateLoteReproductoraDto;
  }

  // ---------- Detalle ----------
  view(r: LoteReproductoraDto) { this.detalleData = r; this.detalleOpen = true; }
  closeDetalle() { this.detalleOpen = false; this.detalleData = null; }

  // ---------- Filtros en cascada ----------
  private resetState(level: 'granja' | 'nucleo' | 'lote'): void {
    if (level === 'granja') {
      this.selectedNucleoId = null; this.selectedLoteId = null;
      this.nucleos = []; this.lotes = []; this.registros = [];
      this.loteSeleccionado = null; this.closeDetalle();
    } else if (level === 'nucleo') {
      this.selectedLoteId = null; this.lotes = []; this.registros = [];
      this.loteSeleccionado = null; this.closeDetalle();
    } else {
      this.registros = []; this.loteSeleccionado = null; this.closeDetalle();
    }
  }

  onGranjaChange(): void {
    this.resetState('granja');
    if (!this.selectedGranjaId) return;

    const reqId = ++this.nucleosReq;
    this.svc.getNucleosPorGranja(this.selectedGranjaId).subscribe({
      next: (n) => { if (reqId !== this.nucleosReq) return; this.nucleos = [...n]; },
      error: () => { if (reqId !== this.nucleosReq) return; this.nucleos = []; }
    });
  }

  onNucleoChange(): void {
    this.resetState('nucleo');
    if (!this.selectedGranjaId || !this.selectedNucleoId) return;

    const reqId = ++this.lotesReq;
    this.svc.getLotes().subscribe({
      next: (l) => {
        if (reqId !== this.lotesReq) return;
        const gid = String(this.selectedGranjaId), nid = String(this.selectedNucleoId);
        this.lotes = l.filter(x => String(x.granjaId) === gid && String(x.nucleoId) === nid);
      },
      error: () => { if (reqId !== this.lotesReq) return; this.lotes = []; }
    });
  }

  onLoteChange(): void {
    this.resetState('lote');
    if (!this.selectedLoteId) return;

    const lote = this.lotes.find((l) => l.loteId === this.selectedLoteId);
    if (lote) {
      this.loteSeleccionado = {
        ...lote,
        nucleoId: lote.nucleoId ? +lote.nucleoId : undefined,
        galponId: lote.galponId ? +lote.galponId : undefined
      };
    }

    const reqId = ++this.registrosReq;
    this.loading = true;

    // ✅ pedir directamente por lote (evita traer todo)
    this.svc.getByLoteId(this.selectedLoteId)
      .pipe(finalize(() => { if (reqId === this.registrosReq) this.loading = false; }))
      .subscribe({
        next: (r) => { if (reqId !== this.registrosReq) return; this.registros = [...(r ?? [])]; },
        error: () => { if (reqId !== this.registrosReq) return; this.registros = []; }
      });
  }

  // ---------- CRUD ----------
  openNew(): void {
    if (!this.selectedLoteId) return;

    this.editing = null;
    this.bulkMode = false; // inicia en 1 por defecto
    this.bulkCount = 1;
    this.incubadoras.clear();

    const lote = this.lotes.find((l) => l.loteId === this.selectedLoteId);
    this.form.reset({
      loteId: this.selectedLoteId,
      nombreLote: lote?.loteNombre || '',
      reproductoraId: '',
      fechaEncasetamiento: '',
      m: null, h: null, mixtas: null,
      mortCajaH: null, mortCajaM: null,
      unifH: null, unifM: null,
      pesoInicialM: null, pesoInicialH: null
    });

    // prepara 1 formulario en el array por si cambian a múltiple
    this.regenerateIncubadoras(1);
    this.modalOpen = true;
  }

  edit(r: LoteReproductoraDto): void {
    this.editing = r;
    this.bulkMode = false; // edición siempre single
    this.incubadoras.clear();

    this.form.patchValue({
      loteId: r.loteId,
      nombreLote: r.nombreLote ?? '',
      reproductoraId: r.reproductoraId ?? '',
      // tolera null
      fechaEncasetamiento: r.fechaEncasetamiento ? r.fechaEncasetamiento.slice(0,10) : '',
      m: r.m ?? null, h: r.h ?? null, mixtas: r.mixtas ?? null,
      mortCajaH: r.mortCajaH ?? null, mortCajaM: r.mortCajaM ?? null,
      unifH: r.unifH ?? null, unifM: r.unifM ?? null,
      pesoInicialM: r.pesoInicialM ?? null, pesoInicialH: r.pesoInicialH ?? null,
    });
    this.modalOpen = true;
  }

  delete(loteId: string, repId: string): void {
    if (!confirm('¿Deseas eliminar este lote reproductora?')) return;
    this.svc.delete(loteId, repId).subscribe({
      next: () => this.onLoteChange(),
      error: () => this.onLoteChange()
    });
  }

  save(): void {
    if (this.editing) {
      // UPDATE (single)
      const v = this.form.value;
      const dto: CreateLoteReproductoraDto = {
        loteId: this.editing.loteId,
        reproductoraId: this.editing.reproductoraId,
        nombreLote: (v.nombreLote ?? '').trim(),
        fechaEncasetamiento: v.fechaEncasetamiento ? new Date(v.fechaEncasetamiento as string).toISOString() : null,
        m: v.m ?? null, h: v.h ?? null, mixtas: v.mixtas ?? null,
        mortCajaH: v.mortCajaH ?? null, mortCajaM: v.mortCajaM ?? null,
        unifH: v.unifH ?? null, unifM: v.unifM ?? null,
        pesoInicialM: v.pesoInicialM ?? null, pesoInicialH: v.pesoInicialH ?? null,
      };

      this.svc.update(dto).subscribe(() => { this.modalOpen = false; this.onLoteChange(); });
      return;
    }

    // CREATE
    if (!this.bulkMode) {
      // single
      const v = this.form.value;
      const dto: CreateLoteReproductoraDto = {
        loteId: this.selectedLoteId!,
        reproductoraId: (v.reproductoraId || 'Sanmarino').trim(),
        nombreLote: (v.nombreLote || '').trim(),
        fechaEncasetamiento: v.fechaEncasetamiento ? new Date(v.fechaEncasetamiento as string).toISOString() : null,
        m: v.m ?? null, h: v.h ?? null, mixtas: v.mixtas ?? null,
        mortCajaH: v.mortCajaH ?? null, mortCajaM: v.mortCajaM ?? null,
        unifH: v.unifH ?? null, unifM: v.unifM ?? null,
        pesoInicialM: v.pesoInicialM ?? null, pesoInicialH: v.pesoInicialH ?? null,
      };
      this.svc.create(dto).subscribe(() => { this.modalOpen = false; this.onLoteChange(); });
      return;
    }

    // bulk
    if (this.incubadoras.length === 0) this.regenerateIncubadoras(1);
    const dtos = this.incubadoras.controls.map(g => this.toDtoFromGroup(g as FormGroup));
    this.svc.createMany(dtos).subscribe(() => {
      this.modalOpen = false;
      this.onLoteChange();
    });
  }

  cancel(): void { this.modalOpen = false; }

  // ---------- trackBy para listas grandes ----------
  trackByRegistro = (_: number, r: LoteReproductoraDto) => `${r.loteId}::${r.reproductoraId}`;
  trackByIdx = (i: number) => i;
}
