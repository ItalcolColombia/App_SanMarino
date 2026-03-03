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
import { faPlus, faPen, faTrash, faEye, faFilter } from '@fortawesome/free-solid-svg-icons';

import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { Validators } from '@angular/forms';

import {
  LoteReproductoraService,
  FarmDto, NucleoDto, LoteFilterItemDto, GalponFilterItemDto,
  CreateLoteReproductoraDto, LoteReproductoraDto,
  AvesDisponiblesDto
} from '../../services/lote-reproductora.service';
import { LoteService } from '../../../lote/services/lote.service';
import type { LoteDto } from '../../../lote/services/lote.service';

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
  imports: [CommonModule, FormsModule, ReactiveFormsModule, HttpClientModule, FontAwesomeModule, ConfirmationModalComponent]
})
export class LoteReproductoraListComponent implements OnInit {
  // Constantes de límites
  readonly MAX_HEMBRAS_POR_LOTE = 500;
  readonly MAX_MACHOS_POR_LOTE = 30;

  // Icons
  faPlus = faPlus; faPen = faPen; faTrash = faTrash; faEye = faEye; faFilter = faFilter;

  // Data (cargada una vez por getFilterData())
  readonly granjas: FarmDto[] = [];
  private allNucleos: NucleoDto[] = [];
  private allGalpones: GalponFilterItemDto[] = [];
  private allLotes: LoteFilterItemDto[] = [];
  nucleos: NucleoDto[] = [];
  galpones: GalponFilterItemDto[] = [];
  lotes: LoteFilterItemDto[] = [];
  registros: LoteReproductoraDto[] = [];

  // Filtros
  selectedGranjaId: number | null = null;
  selectedNucleoId: string | null = null;
  selectedGalponId: string | null = null;
  selectedLoteId: string | number | null = null; // number desde filter-data, se convierte a string al usar

  // Resumen
  loteSeleccionado: LoteDtoExtendido | null = null;
  avesDisponibles: AvesDisponiblesDto | null = null;

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

  // Confirmation modal (eliminación)
  confirmModalOpen = false;
  confirmModalData: ConfirmationModalData = {
    title: 'Confirmación',
    message: '¿Estás seguro?',
    type: 'warning',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar'
  };
  private pendingDeleteAction: { loteId: string; repId: string } | null = null;

  // Confirmación al cancelar el modal crear/editar
  confirmCancelModalOpen = false;
  confirmCancelModalData: ConfirmationModalData = {
    title: 'Descartar cambios',
    message: '¿Salir sin guardar? Los datos ingresados se perderán.',
    type: 'warning',
    confirmText: 'Sí, salir',
    cancelText: 'Seguir editando'
  };

  // anti-race (solo registros y aves son async; filtros se resuelven en cliente)
  private registrosReq = 0;

  constructor(
    private fb: FormBuilder,
    private svc: LoteReproductoraService,
    private loteService: LoteService,
    private toastService: ToastService
  ) {}

  /** Validador que acepta vacío (null/undefined/'') o número >= min. Permite lotes solo con hembras (0 machos). */
  private static optionalMin(min: number) {
    return (control: { value: unknown }) => {
      const v = control.value;
      if (v === null || v === undefined || v === '') return null;
      const n = Number(v);
      if (Number.isNaN(n)) return null;
      return n >= min ? null : { min: { min, actual: n } };
    };
  }

  /** Validador que acepta vacío o número en [min, max]. */
  private static optionalMinMax(min: number, max: number) {
    return (control: { value: unknown }) => {
      const v = control.value;
      if (v === null || v === undefined || v === '') return null;
      const n = Number(v);
      if (Number.isNaN(n)) return null;
      if (n < min) return { min: { min, actual: n } };
      if (n > max) return { max: { max, actual: n } };
      return null;
    };
  }

  ngOnInit(): void {
    const optionalMin0 = LoteReproductoraListComponent.optionalMin(0);
    const optionalMinMax0_100 = LoteReproductoraListComponent.optionalMinMax(0, 100);

    this.form = this.fb.group({
      // single (para edición o crear 1). Solo obligatorios: incubadora y al menos una cantidad (validado en save).
      loteId: [''],
      nombreLote: ['', [Validators.maxLength(200)]],
      reproductoraId: ['', [Validators.required, Validators.maxLength(100)]],
      fechaEncasetamiento: [''],
      m: [0, [optionalMin0]],
      h: [0, [optionalMin0]],
      mixtas: [0, [optionalMin0]],
      mortCajaH: [0, [optionalMin0]],
      mortCajaM: [0, [optionalMin0]],
      unifH: [null, [optionalMinMax0_100]],
      unifM: [null, [optionalMinMax0_100]],
      pesoInicialM: [0, [optionalMin0]],
      pesoInicialH: [0, [optionalMin0]],

      // multiple
      incubadoras: this.fb.array([] as FormGroup[])
    });

    this.loading = true;
    this.svc.getFilterData().subscribe({
      next: (data) => {
        (this.granjas as FarmDto[]).push(...(data.farms ?? []));
        this.allNucleos = [...(data.nucleos ?? [])];
        this.allGalpones = [...(data.galpones ?? [])];
        this.allLotes = [...(data.lotes ?? [])];
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Error al cargar datos de filtros:', err);
        this.toastService.error('No se pudieron cargar los datos de filtros', 'Error');
      }
    });
  }

  // ---- helpers bulk ----
  get incubadoras(): FormArray<FormGroup> {
    return this.form.get('incubadoras') as FormArray<FormGroup>;
  }

  private buildIncubadoraGroup(prefill?: Partial<CreateLoteReproductoraDto>): FormGroup {
    return this.fb.group({
      // lote se setea con selectedLoteId al armar el DTO
      nombreLote: [prefill?.nombreLote ?? this.loteSeleccionado?.loteNombre ?? '', [Validators.required, Validators.maxLength(200)]],
      reproductoraId: [prefill?.reproductoraId ?? '', [Validators.required, Validators.maxLength(100)]],
      fechaEncasetamiento: [
        prefill?.fechaEncasetamiento ? String(prefill.fechaEncasetamiento).slice(0, 10) : '',
        Validators.required
      ],
      m: [prefill?.m ?? null, [Validators.min(0)]],
      h: [prefill?.h ?? null, [Validators.min(0)]],
      mixtas: [prefill?.mixtas ?? null, [Validators.min(0)]],
      mortCajaH: [prefill?.mortCajaH ?? null, [Validators.min(0)]],
      mortCajaM: [prefill?.mortCajaM ?? null, [Validators.min(0)]],
      unifH: [prefill?.unifH ?? null, [Validators.min(0), Validators.max(100)]],
      unifM: [prefill?.unifM ?? null, [Validators.min(0), Validators.max(100)]],
      pesoInicialM: [prefill?.pesoInicialM ?? null, [Validators.min(0)]],
      pesoInicialH: [prefill?.pesoInicialH ?? null, [Validators.min(0)]],
    });
  }

  regenerateIncubadoras(n: number) {
    const count = Math.max(1, Math.min(50, Number(n) || 1)); // límite sano
    
    // Verificar si se pueden crear más lotes
    if (!this.canCreateMoreLotes()) {
      // Si no hay aves disponibles, no permitir crear ningún lote
      if (count > 0) {
        this.toastService.error(
          'El lote ya no tiene aves disponibles para crear más lotes reproductoras. Todas las aves han sido asignadas o han fallecido.',
          'Sin Aves Disponibles',
          6000
        );
        // Mantener solo 1 incubadora vacía para mostrar el mensaje, pero no permitir guardar
        if (this.incubadoras.length === 0) {
          this.incubadoras.push(this.buildIncubadoraGroup());
        }
        this.bulkCount = this.incubadoras.length;
        return;
      }
    }
    
    // Si hay aves disponibles, permitir crear lotes
    if (!this.canCreateMoreLotes() && count > this.incubadoras.length) {
      this.toastService.warning(
        'El lote ya no tiene aves disponibles para crear más lotes reproductoras.',
        'Sin Aves Disponibles',
        5000
      );
      return;
    }

    while (this.incubadoras.length < count) this.incubadoras.push(this.buildIncubadoraGroup());
    while (this.incubadoras.length > count) this.incubadoras.removeAt(this.incubadoras.length - 1);
    
    // Actualizar validadores dinámicamente después de cambiar la cantidad
    this.updateValidatorsForBulkMode();
  }

  /**
   * Actualiza los validadores de los campos de aves en modo bulk
   * para reflejar los límites calculados dinámicamente
   */
  private updateValidatorsForBulkMode(): void {
    if (!this.bulkMode || !this.avesDisponibles) return;

    const maxHembrasPorLote = this.getMaxHembrasPorLote();
    const maxMachosPorLote = this.getMaxMachosPorLote();

    this.incubadoras.controls.forEach(g => {
      const hControl = g.get('h');
      const mControl = g.get('m');
      
      if (hControl) {
        // El límite real es solo la disponibilidad dividida por lotes
        hControl.setValidators([
          Validators.min(0),
          Validators.max(maxHembrasPorLote)
        ]);
        hControl.updateValueAndValidity({ emitEvent: false });
      }
      
      if (mControl) {
        // El límite real es solo la disponibilidad dividida por lotes
        mControl.setValidators([
          Validators.min(0),
          Validators.max(maxMachosPorLote)
        ]);
        mControl.updateValueAndValidity({ emitEvent: false });
      }
    });
  }

  onBulkModeChange(): void {
    if (this.bulkMode) {
      this.regenerateIncubadoras(this.bulkCount);
    } else {
      this.incubadoras.clear();
    }
  }

  addIncubadora() {
    // Verificar si se pueden crear más lotes
    if (!this.canCreateMoreLotes()) {
      this.toastService.warning(
        'El lote ya no tiene aves disponibles para crear más lotes reproductoras.',
        'Sin Aves Disponibles',
        5000
      );
      return;
    }

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
    // Asegurar que loteId sea string
    const loteIdStr = this.selectedLoteId ? String(this.selectedLoteId) : '';
    return {
      loteId: loteIdStr, // Asegurar que sea string
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

  // ---------- Filtros en cascada (Granja → Núcleo → Galpón → Lote) ----------
  private resetState(level: 'granja' | 'nucleo' | 'galpon' | 'lote'): void {
    if (level === 'granja') {
      this.selectedNucleoId = null; this.selectedGalponId = null; this.selectedLoteId = null;
      this.nucleos = []; this.galpones = []; this.lotes = []; this.registros = [];
      this.loteSeleccionado = null; this.avesDisponibles = null; this.closeDetalle();
    } else if (level === 'nucleo') {
      this.selectedGalponId = null; this.selectedLoteId = null;
      this.galpones = []; this.lotes = []; this.registros = [];
      this.loteSeleccionado = null; this.avesDisponibles = null; this.closeDetalle();
    } else if (level === 'galpon') {
      this.selectedLoteId = null; this.lotes = []; this.registros = [];
      this.loteSeleccionado = null; this.avesDisponibles = null; this.closeDetalle();
    } else {
      this.registros = []; this.loteSeleccionado = null; this.avesDisponibles = null; this.closeDetalle();
    }
  }

  onGranjaChange(): void {
    this.resetState('granja');
    if (!this.selectedGranjaId) return;
    const gid = Number(this.selectedGranjaId);
    this.nucleos = this.allNucleos.filter(n => n.granjaId === gid);
  }

  onNucleoChange(): void {
    this.resetState('nucleo');
    if (!this.selectedGranjaId || !this.selectedNucleoId) return;
    const gid = Number(this.selectedGranjaId);
    const nid = String(this.selectedNucleoId);
    this.galpones = this.allGalpones.filter(g => g.granjaId === gid && g.nucleoId === nid);
  }

  onGalponChange(): void {
    this.resetState('galpon');
    if (!this.selectedGranjaId || !this.selectedNucleoId || !this.selectedGalponId) return;
    const gid = Number(this.selectedGranjaId);
    const nid = String(this.selectedNucleoId);
    const galId = String(this.selectedGalponId);
    this.lotes = this.allLotes.filter(
      x => x.granjaId === gid && String(x.nucleoId ?? '') === nid && String(x.galponId ?? '') === galId
    );
  }

  onLoteChange(): void {
    this.resetState('lote');
    if (this.selectedLoteId === null || this.selectedLoteId === undefined) return;
    const idStr = String(this.selectedLoteId);
    const loteIdNum = Number(this.selectedLoteId);
    const lote = this.lotes.find((l) => String(l.loteId) === idStr);

    // Valores mínimos desde el filtro (por si falla getById)
    if (lote) {
      this.loteSeleccionado = {
        loteId: String(lote.loteId),
        loteNombre: lote.loteNombre,
        granjaId: lote.granjaId,
        nucleoId: lote.nucleoId ? +lote.nucleoId : undefined,
        galponId: lote.galponId ? +lote.galponId : undefined,
        hembrasL: 0,
        machosL: 0,
        mixtas: 0,
        avesEncasetadas: 0,
        pesoInicialH: 0,
        pesoInicialM: 0,
        pesoMixto: 0
      };
    }

    const reqId = ++this.registrosReq;
    this.loading = true;

    const loteIdStr = String(this.selectedLoteId);
    this.svc.getByLoteId(loteIdStr)
      .pipe(finalize(() => { if (reqId === this.registrosReq) this.loading = false; }))
      .subscribe({
        next: (r) => { if (reqId !== this.registrosReq) return; this.registros = [...(r ?? [])]; },
        error: () => { if (reqId !== this.registrosReq) return; this.registros = []; }
      });

    // Obtener detalle del lote (hembras, machos, pesos, aves encasetadas) para "Datos del lote seleccionado"
    if (!Number.isNaN(loteIdNum)) {
      this.loteService.getById(loteIdNum).subscribe({
        next: (detail: LoteDto) => {
          if (this.loteSeleccionado) {
            this.loteSeleccionado = {
              ...this.loteSeleccionado,
              loteId: String(detail.loteId),
              loteNombre: detail.loteNombre ?? this.loteSeleccionado.loteNombre,
              granjaId: detail.granjaId ?? this.loteSeleccionado.granjaId,
              nucleoId: detail.nucleoId != null ? parseInt(String(detail.nucleoId), 10) : this.loteSeleccionado.nucleoId,
              hembrasL: detail.hembrasL ?? 0,
              machosL: detail.machosL ?? 0,
              mixtas: detail.mixtas ?? 0,
              avesEncasetadas: detail.avesEncasetadas ?? 0,
              pesoInicialH: detail.pesoInicialH ?? 0,
              pesoInicialM: detail.pesoInicialM ?? 0,
              pesoMixto: detail.pesoMixto ?? 0,
              fechaEncaset: detail.fechaEncaset,
              regional: detail.regional
            };
          }
        },
        error: () => { /* mantener datos mínimos del filtro */ }
      });
    }

    // Obtener aves disponibles
    if (loteIdStr) {
      this.svc.getAvesDisponibles(loteIdStr).subscribe({
        next: (aves) => { 
          this.avesDisponibles = aves;
          if (this.bulkMode) {
            this.updateValidatorsForBulkMode();
          }
        },
        error: () => { this.avesDisponibles = null; }
      });
    }
  }

  // ---------- CRUD ----------
  openNew(): void {
    // Convertir selectedLoteId a string si es número
    if (this.selectedLoteId !== null && typeof this.selectedLoteId === 'number') {
      this.selectedLoteId = String(this.selectedLoteId);
    }
    if (!this.selectedLoteId) return;

    // Verificar si hay aves disponibles
    if (!this.canCreateMoreLotes()) {
      this.toastService.error(
        'El lote ya no tiene aves disponibles para distribuir en nuevos lotes reproductoras. Todas las aves han sido asignadas o han fallecido.',
        'Sin Aves Disponibles',
        6000
      );
      return;
    }

    this.editing = null;
    this.bulkMode = false;
    this.bulkCount = 1;
    this.incubadoras.clear(); // En modo "Uno" no añadimos incubadoras para no invalidar el form

    const loteIdStr = String(this.selectedLoteId);
    const lote = this.lotes.find((l) => String(l.loteId) === loteIdStr);
    this.form.reset({
      loteId: loteIdStr,
      nombreLote: lote?.loteNombre || '',
      reproductoraId: '',
      fechaEncasetamiento: '',
      m: 0, h: 0, mixtas: 0,
      mortCajaH: 0, mortCajaM: 0,
      unifH: null, unifM: null,
      pesoInicialM: 0, pesoInicialH: 0
    });
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
      fechaEncasetamiento: r.fechaEncasetamiento ? r.fechaEncasetamiento.slice(0,10) : '',
      m: r.m ?? 0, h: r.h ?? 0, mixtas: r.mixtas ?? 0,
      mortCajaH: r.mortCajaH ?? 0, mortCajaM: r.mortCajaM ?? 0,
      unifH: r.unifH ?? null, unifM: r.unifM ?? null,
      pesoInicialM: r.pesoInicialM ?? 0, pesoInicialH: r.pesoInicialH ?? 0,
    });
    this.modalOpen = true;
  }

  delete(loteId: string, repId: string): void {
    this.pendingDeleteAction = { loteId, repId };
    this.confirmModalData = {
      title: 'Confirmar Eliminación',
      message: `¿Estás seguro de que deseas eliminar el lote reproductora "${repId}"? Esta acción no se puede deshacer.`,
      type: 'error',
      confirmText: 'Eliminar',
      cancelText: 'Cancelar'
    };
    this.confirmModalOpen = true;
  }

  onConfirmDelete(): void {
    if (!this.pendingDeleteAction) return;
    
    const { loteId, repId } = this.pendingDeleteAction;
    this.loading = true;
    
    this.svc.delete(loteId, repId).subscribe({
      next: () => {
        this.loading = false;
        this.toastService.success(
          `Lote reproductora "${repId}" eliminado correctamente`,
          'Eliminación Exitosa',
          4000
        );
        this.confirmModalOpen = false;
        this.pendingDeleteAction = null;
        this.onLoteChange();
      },
      error: (err) => {
        this.loading = false;
        const errorMsg = err?.error?.message || err?.message || 'Error al eliminar el lote reproductora';
        this.toastService.error(errorMsg, 'Error al Eliminar');
        this.confirmModalOpen = false;
        this.pendingDeleteAction = null;
      }
    });
  }

  onCancelDelete(): void {
    this.confirmModalOpen = false;
    this.pendingDeleteAction = null;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      const errRepro = this.form.get('reproductoraId')?.invalid;
      const msg = errRepro
        ? 'El campo Incubadora es obligatorio. Además, asigna al menos 1 hembra, 1 macho o 1 mixta.'
        : 'Asigna al menos 1 hembra, 1 macho o 1 mixta para poder guardar.';
      this.toastService.warning(msg, 'Formulario incompleto', 5000);
      return;
    }

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

      this.loading = true;
      this.svc.update(dto).pipe(
        finalize(() => this.loading = false)
      ).subscribe({
        next: () => {
          this.modalOpen = false;
          this.toastService.success(
            `Lote reproductora "${dto.nombreLote || dto.reproductoraId}" actualizado correctamente`,
            'Actualización Exitosa',
            4000
          );
          this.onLoteChange();
        },
        error: (err) => {
          const errorMsg = err?.error?.message || err?.message || 'Error al actualizar el lote reproductora';
          this.toastService.error(errorMsg, 'Error al Actualizar');
        }
      });
      return;
    }

    // CREATE
    if (!this.bulkMode) {
      // single
      const v = this.form.value;
      const h = Number(v.h) || 0;
      const m = Number(v.m) || 0;

      // Validar límites por lote basados en disponibilidad
      // El límite real es la cantidad disponible, no los límites fijos
      if (this.avesDisponibles) {
        const maxHembrasPermitidas = this.avesDisponibles.hembrasDisponibles;
        if (h > maxHembrasPermitidas) {
          this.toastService.error(
            `No se pueden asignar más de ${maxHembrasPermitidas} hembras. Solo hay ${maxHembrasPermitidas} hembras disponibles en el lote.`,
            'Límite Excedido'
          );
          return;
        }
        const maxMachosPermitidos = this.avesDisponibles.machosDisponibles;
        if (m > maxMachosPermitidos) {
          this.toastService.error(
            `No se pueden asignar más de ${maxMachosPermitidos} machos. Solo hay ${maxMachosPermitidos} machos disponibles en el lote.`,
            'Límite Excedido'
          );
          return;
        }
      }

      const mixtas = Number(this.form.get('mixtas')?.value) || 0;
      if (h === 0 && m === 0 && mixtas === 0) {
        this.toastService.warning(
          'Debe asignar al menos 1 hembra, 1 macho o 1 mixta.',
          'Cantidad inválida'
        );
        return;
      }

      // Validar que no se asignen más aves de las disponibles
      if (this.avesDisponibles) {
        if (h > this.avesDisponibles.hembrasDisponibles) {
          this.toastService.error(
            `No se pueden asignar ${h} hembras. Solo hay ${this.avesDisponibles.hembrasDisponibles} disponibles.`,
            'Validación de Aves'
          );
          return;
        }
        if (m > this.avesDisponibles.machosDisponibles) {
          this.toastService.error(
            `No se pueden asignar ${m} machos. Solo hay ${this.avesDisponibles.machosDisponibles} disponibles.`,
            'Validación de Aves'
          );
          return;
        }
      }

      const nombreLote = (v.nombreLote ?? '').trim() || this.loteSeleccionado?.loteNombre || 'Lote reproductora';
      const fechaEncasetamiento = v.fechaEncasetamiento
        ? new Date(v.fechaEncasetamiento as string).toISOString()
        : new Date().toISOString();

      const dto: CreateLoteReproductoraDto = {
        loteId: this.selectedLoteId ? String(this.selectedLoteId) : '',
        reproductoraId: (v.reproductoraId || 'Sanmarino').trim(),
        nombreLote,
        fechaEncasetamiento,
        m: v.m ?? null, h: v.h ?? null, mixtas: v.mixtas ?? null,
        mortCajaH: v.mortCajaH ?? null, mortCajaM: v.mortCajaM ?? null,
        unifH: v.unifH ?? null, unifM: v.unifM ?? null,
        pesoInicialM: v.pesoInicialM ?? null, pesoInicialH: v.pesoInicialH ?? null,
      };

      this.loading = true;
      this.svc.create(dto).pipe(
        finalize(() => this.loading = false)
      ).subscribe({
        next: () => {
          this.modalOpen = false;
          this.toastService.success(
            `Lote reproductora "${dto.nombreLote || dto.reproductoraId}" creado correctamente`,
            'Creación Exitosa',
            4000
          );
          this.onLoteChange();
        },
        error: (err) => {
          const errorMsg = err?.error?.message || err?.message || 'Error al crear el lote reproductora';
          this.toastService.error(errorMsg, 'Error al Crear');
        }
      });
      return;
    }

    // bulk
    if (this.incubadoras.length === 0) this.regenerateIncubadoras(1);
    
    // Validar que haya aves disponibles
    if (!this.canCreateMoreLotes()) {
      this.toastService.error(
        'El lote ya no tiene aves disponibles para distribuir en nuevos lotes reproductoras. Todas las aves han sido asignadas o han fallecido.',
        'Sin Aves Disponibles',
        6000
      );
      return;
    }

    // Validar límites individuales y distribución
    const maxHembrasPorLote = this.getMaxHembrasPorLote();
    const maxMachosPorLote = this.getMaxMachosPorLote();
    const totalH = this.getTotalHembras();
    const totalM = this.getTotalMachos();

    // Validar límites por lote individual basados en disponibilidad
    for (let i = 0; i < this.incubadoras.length; i++) {
      const g = this.incubadoras.at(i);
      const h = Number(g.get('h')?.value) || 0;
      const m = Number(g.get('m')?.value) || 0;

      // Validar distribución equitativa (el límite real es disponibilidad / cantidad de lotes)
      if (h > maxHembrasPorLote) {
        this.toastService.error(
          `La incubadora #${i + 1} no puede tener más de ${maxHembrasPorLote} hembras. Con ${this.incubadoras.length} lotes, el máximo por lote es ${maxHembrasPorLote} (${this.avesDisponibles?.hembrasDisponibles || 0} disponibles / ${this.incubadoras.length} lotes).`,
          'Distribución Excedida'
        );
        return;
      }
      if (m > maxMachosPorLote) {
        this.toastService.error(
          `La incubadora #${i + 1} no puede tener más de ${maxMachosPorLote} machos. Con ${this.incubadoras.length} lotes, el máximo por lote es ${maxMachosPorLote} (${this.avesDisponibles?.machosDisponibles || 0} disponibles / ${this.incubadoras.length} lotes).`,
          'Distribución Excedida'
        );
        return;
      }

      // Validar que al menos haya una cantidad válida (hembras o machos)
      if (h === 0 && m === 0) {
        this.toastService.warning(
          `La incubadora #${i + 1} debe tener al menos 1 hembra o 1 macho.`,
          'Cantidad Inválida'
        );
        return;
      }
    }
    
    // Validar totales disponibles
    if (this.avesDisponibles) {
      if (totalH > this.avesDisponibles.hembrasDisponibles) {
        this.toastService.error(
          `No se pueden asignar ${totalH} hembras en total. Solo hay ${this.avesDisponibles.hembrasDisponibles} disponibles.`,
          'Validación de Aves'
        );
        return;
      }
      if (totalM > this.avesDisponibles.machosDisponibles) {
        this.toastService.error(
          `No se pueden asignar ${totalM} machos en total. Solo hay ${this.avesDisponibles.machosDisponibles} disponibles.`,
          'Validación de Aves'
        );
        return;
      }
    }

    const dtos = this.incubadoras.controls.map(g => this.toDtoFromGroup(g as FormGroup));
    this.loading = true;
    this.svc.createMany(dtos).pipe(
      finalize(() => this.loading = false)
    ).subscribe({
      next: () => {
        this.modalOpen = false;
        this.toastService.success(
          `${dtos.length} lote(s) reproductora(s) creado(s) correctamente`,
          'Creación Múltiple Exitosa',
          4000
        );
        this.onLoteChange();
      },
      error: (err) => {
        const errorMsg = err?.error?.message || err?.message || 'Error al crear los lotes reproductora';
        this.toastService.error(errorMsg, 'Error al Crear');
      }
    });
  }

  requestCancel(): void {
    this.confirmCancelModalOpen = true;
  }

  doCancel(): void {
    this.confirmCancelModalOpen = false;
    this.modalOpen = false;
  }

  // ---------- Helpers para múltiples lotes ----------
  getTotalHembras(): number {
    return this.incubadoras.controls.reduce((sum, g) => sum + (Number(g.get('h')?.value) || 0), 0);
  }

  getTotalMachos(): number {
    return this.incubadoras.controls.reduce((sum, g) => sum + (Number(g.get('m')?.value) || 0), 0);
  }

  getIncubadoraPlaceholder(index: number): string {
    return `Ej: Sanmarino-${String.fromCharCode(65 + index)}`;
  }

  // ---------- Lógica de distribución de aves ----------
  /**
   * Calcula el máximo de hembras que se pueden asignar por lote reproductora
   * basado ÚNICAMENTE en las aves disponibles y la cantidad de lotes
   * El límite real es la cantidad disponible dividida por el número de lotes
   * NO aplica el límite fijo de 500, solo la disponibilidad
   */
  getMaxHembrasPorLote(): number {
    if (!this.avesDisponibles || this.incubadoras.length === 0) {
      // Si no hay información, retornar 0 para evitar asignaciones incorrectas
      return 0;
    }
    const disponible = this.avesDisponibles.hembrasDisponibles;
    const cantidadLotes = this.incubadoras.length;
    // El máximo es simplemente la cantidad disponible dividida por lotes
    // NO aplicamos el límite fijo de 500, solo la disponibilidad real
    return Math.floor(disponible / cantidadLotes);
  }

  /**
   * Calcula el máximo de machos que se pueden asignar por lote reproductora
   * basado ÚNICAMENTE en las aves disponibles y la cantidad de lotes
   * El límite real es la cantidad disponible dividida por el número de lotes
   * NO aplica el límite fijo de 30, solo la disponibilidad
   */
  getMaxMachosPorLote(): number {
    if (!this.avesDisponibles || this.incubadoras.length === 0) {
      // Si no hay información, retornar 0 para evitar asignaciones incorrectas
      return 0;
    }
    const disponible = this.avesDisponibles.machosDisponibles;
    const cantidadLotes = this.incubadoras.length;
    // El máximo es simplemente la cantidad disponible dividida por lotes
    // NO aplicamos el límite fijo de 30, solo la disponibilidad real
    return Math.floor(disponible / cantidadLotes);
  }

  /**
   * Obtiene el máximo de hembras por lote sin aplicar el límite fijo
   * (solo basado en disponibilidad / cantidad de lotes)
   */
  getMaxHembrasPorLoteSinLimite(): number {
    if (!this.avesDisponibles || this.incubadoras.length === 0) {
      return 0;
    }
    const disponible = this.avesDisponibles.hembrasDisponibles;
    const cantidadLotes = this.incubadoras.length;
    return Math.floor(disponible / cantidadLotes);
  }

  /**
   * Obtiene el máximo de machos por lote sin aplicar el límite fijo
   * (solo basado en disponibilidad / cantidad de lotes)
   */
  getMaxMachosPorLoteSinLimite(): number {
    if (!this.avesDisponibles || this.incubadoras.length === 0) {
      return 0;
    }
    const disponible = this.avesDisponibles.machosDisponibles;
    const cantidadLotes = this.incubadoras.length;
    return Math.floor(disponible / cantidadLotes);
  }

  /**
   * Verifica si se pueden crear más lotes reproductoras
   * Un lote reproductora puede tener:
   * - Máximo 500 hembras O
   * - Máximo 30 machos O
   * - Ambos, pero respetando los límites individuales
   * 
   * Se pueden crear más lotes si hay al menos:
   * - 1 hembra disponible (para crear un lote con hembras)
   * - O 1 macho disponible (para crear un lote con machos)
   */
  canCreateMoreLotes(): boolean {
    if (!this.avesDisponibles) return false;
    const hembrasDisponibles = this.avesDisponibles.hembrasDisponibles;
    const machosDisponibles = this.avesDisponibles.machosDisponibles;
    
    // Si no hay aves disponibles (ni hembras ni machos), no se pueden crear más lotes
    if (hembrasDisponibles === 0 && machosDisponibles === 0) {
      return false;
    }

    // Si hay al menos 1 hembra o 1 macho disponible, se puede crear un lote
    // (aunque sea solo con hembras o solo con machos)
    return hembrasDisponibles > 0 || machosDisponibles > 0;
  }

  /**
   * Obtiene el mensaje de disponibilidad de aves
   */
  getAvesDisponiblesMessage(): string {
    if (!this.avesDisponibles) {
      return 'No hay información de aves disponibles';
    }

    const hembras = this.avesDisponibles.hembrasDisponibles;
    const machos = this.avesDisponibles.machosDisponibles;

    if (hembras === 0 && machos === 0) {
      return '⚠️ El lote ya no tiene aves disponibles para distribuir en nuevos lotes reproductoras. Todas las aves han sido asignadas o han fallecido.';
    }

    const mensajes: string[] = [];
    if (hembras > 0) {
      const maxPorLote = this.bulkMode ? this.getMaxHembrasPorLote() : hembras;
      mensajes.push(`${hembras} hembras disponibles${this.bulkMode ? ` (máx. ${maxPorLote} por lote)` : ''}`);
    }
    if (machos > 0) {
      const maxPorLote = this.bulkMode ? this.getMaxMachosPorLote() : machos;
      mensajes.push(`${machos} machos disponibles${this.bulkMode ? ` (máx. ${maxPorLote} por lote)` : ''}`);
    }

    return mensajes.join(' | ');
  }

  get selectedGranjaName(): string {
    return this.granjas.find(g => g.id === this.selectedGranjaId)?.name ?? '';
  }
  get selectedNucleoNombre(): string {
    return this.nucleos.find(n => n.nucleoId === this.selectedNucleoId)?.nucleoNombre ?? '';
  }
  get selectedGalponNombre(): string {
    return this.galpones.find(g => g.galponId === this.selectedGalponId)?.galponNombre ?? '';
  }

  // ---------- trackBy para listas grandes ----------
  trackByRegistro = (_: number, r: LoteReproductoraDto) => `${r.loteId}::${r.reproductoraId}`;
  trackByIdx = (i: number) => i;
}
