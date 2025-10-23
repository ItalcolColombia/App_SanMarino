import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SeguimientoLoteLevanteDto, CreateSeguimientoLoteLevanteDto, UpdateSeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { CatalogoAlimentosService, CatalogItemDto, PagedResult } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { EMPTY } from 'rxjs';
import { expand, map, reduce, finalize } from 'rxjs/operators';

@Component({
  selector: 'app-modal-create-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './modal-create-edit.component.html',
  styleUrls: ['./modal-create-edit.component.scss']
})
export class ModalCreateEditComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() editing: SeguimientoLoteLevanteDto | null = null;
  @Input() lotes: LoteDto[] = [];
  @Input() selectedLoteId: number | null = null;
  @Input() loading: boolean = false;
  
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{ data: CreateSeguimientoLoteLevanteDto | UpdateSeguimientoLoteLevanteDto; isEdit: boolean }>();

  // Formulario
  form!: FormGroup;

  // Catálogo de alimentos
  alimentosCatalog: CatalogItemDto[] = [];
  private alimentosByCode = new Map<string, CatalogItemDto>();
  private alimentosById = new Map<number, CatalogItemDto>();
  private alimentosByName = new Map<string, CatalogItemDto>();

  constructor(
    private fb: FormBuilder,
    private catalogSvc: CatalogoAlimentosService
  ) { }

  ngOnInit(): void {
    this.initializeForm();
    this.loadAlimentosCatalog();
  }

  ngOnChanges(): void {
    if (this.isOpen && this.editing) {
      this.populateForm();
    } else if (this.isOpen && !this.editing) {
      this.resetForm();
    }
  }

  // ================== FORMULARIO ==================
  private initializeForm(): void {
    this.form = this.fb.group({
      fechaRegistro: [this.todayYMD(), Validators.required],
      loteId: ['', Validators.required],
      mortalidadHembras: [0, [Validators.required, Validators.min(0)]],
      mortalidadMachos: [0, [Validators.required, Validators.min(0)]],
      selH: [0, [Validators.required, Validators.min(0)]],
      selM: [0, [Validators.required, Validators.min(0)]],
      errorSexajeHembras: [0, [Validators.required, Validators.min(0)]],
      errorSexajeMachos: [0, [Validators.required, Validators.min(0)]],
      tipoAlimento: [''],
      consumoKgHembras: [0, [Validators.required, Validators.min(0)]],
      observaciones: [''],
      consumoKgMachos: [null, [Validators.min(0)]],
      pesoPromH: [null, [Validators.min(0)]],
      pesoPromM: [null, [Validators.min(0)]],
      uniformidadH: [null, [Validators.min(0), Validators.max(100)]],
      uniformidadM: [null, [Validators.min(0), Validators.max(100)]],
      cvH: [null, [Validators.min(0)]],
      cvM: [null, [Validators.min(0)]],
      tipoAlimentoHembras: [''],
      tipoAlimentoMachos: [''],
      consumoAlimentoHembras: [null],
      consumoAlimentoMachos: [null],
      ciclo: ['Normal'],
    });
  }

  private resetForm(): void {
    this.form.reset({
      fechaRegistro: this.todayYMD(),
      loteId: this.selectedLoteId,
      mortalidadHembras: 0,
      mortalidadMachos: 0,
      selH: 0,
      selM: 0,
      errorSexajeHembras: 0,
      errorSexajeMachos: 0,
      tipoAlimento: '',
      consumoKgHembras: 0,
      observaciones: '',
      ciclo: 'Normal',
      consumoKgMachos: null,
      pesoPromH: null,
      pesoPromM: null,
      uniformidadH: null,
      uniformidadM: null,
      cvH: null,
      cvM: null,
      tipoAlimentoHembras: '',
      tipoAlimentoMachos: '',
      consumoAlimentoHembras: null,
      consumoAlimentoMachos: null,
    });
  }

  private populateForm(): void {
    if (!this.editing) return;
    
    this.form.patchValue({
      fechaRegistro: this.toYMD(this.editing.fechaRegistro),
      loteId: this.editing.loteId,
      mortalidadHembras: this.editing.mortalidadHembras,
      mortalidadMachos: this.editing.mortalidadMachos,
      selH: this.editing.selH,
      selM: this.editing.selM,
      errorSexajeHembras: this.editing.errorSexajeHembras,
      errorSexajeMachos: this.editing.errorSexajeMachos,
      tipoAlimento: this.editing.tipoAlimento ?? '',
      consumoKgHembras: this.editing.consumoKgHembras,
      observaciones: this.editing.observaciones || '',
      ciclo: this.editing.ciclo || 'Normal',
      consumoKgMachos: this.editing.consumoKgMachos ?? null,
      pesoPromH: this.editing.pesoPromH ?? null,
      pesoPromM: this.editing.pesoPromM ?? null,
      uniformidadH: this.editing.uniformidadH ?? null,
      uniformidadM: this.editing.uniformidadM ?? null,
      cvH: this.editing.cvH ?? null,
      cvM: this.editing.cvM ?? null,
      tipoAlimentoHembras: this.editing.tipoAlimentoHembras ?? '',
      tipoAlimentoMachos: this.editing.tipoAlimentoMachos ?? '',
    });
  }

  // ================== CATALOGO ALIMENTOS ==================
  private loadAlimentosCatalog(): void {
    const firstPage = 1;
    const pageSize = 100;

    this.catalogSvc.list('', firstPage, pageSize).pipe(
      expand((res: PagedResult<CatalogItemDto>) => {
        const received = res.page * res.pageSize;
        const more = received < (res.total ?? 0);
        return more
          ? this.catalogSvc.list('', res.page + 1, res.pageSize)
          : EMPTY;
      }),
      reduce((acc: CatalogItemDto[], res: PagedResult<CatalogItemDto>) => {
        const items = Array.isArray(res.items) ? res.items : [];
        return acc.concat(items);
      }, []),
      map(all => all.sort((a, b) =>
        (a.nombre || '').localeCompare(b.nombre || '', 'es', { numeric: true, sensitivity: 'base' })
      ))
    ).subscribe(all => {
      this.alimentosCatalog = all;

      this.alimentosById.clear();
      this.alimentosByCode.clear();
      this.alimentosByName.clear();

      for (const it of all) {
        if (it.id != null) this.alimentosById.set(it.id, it);
        if (it.codigo)     this.alimentosByCode.set(String(it.codigo).trim(), it);
        if (it.nombre)     this.alimentosByName.set(it.nombre.trim().toLowerCase(), it);
      }
    });
  }

  // ================== EVENTOS ==================
  onClose(): void {
    this.close.emit();
  }

  onSave(): void {
    if (this.form.invalid) { 
      this.form.markAllAsTouched(); 
      return; 
    }

    const raw = this.form.value;
    const tipoAlH = (raw.tipoAlimentoHembras || '').toString().trim();
    const tipoAlM = (raw.tipoAlimentoMachos || '').toString().trim();
    const ymd = this.toYMD(raw.fechaRegistro)!;

    const baseDto = {
      fechaRegistro: this.ymdToIsoAtNoon(ymd),
      loteId: raw.loteId,
      mortalidadHembras: Number(raw.mortalidadHembras) || 0,
      mortalidadMachos: Number(raw.mortalidadMachos) || 0,
      selH: Number(raw.selH) || 0,
      selM: Number(raw.selM) || 0,
      errorSexajeHembras: Number(raw.errorSexajeHembras) || 0,
      errorSexajeMachos: Number(raw.errorSexajeMachos) || 0,
      tipoAlimento: raw.tipoAlimento || tipoAlH || tipoAlM || '',
      consumoKgHembras: Number(raw.consumoKgHembras) || 0,
      consumoKgMachos: this.toNumOrNull(raw.consumoKgMachos),
      pesoPromH: this.toNumOrNull(raw.pesoPromH),
      pesoPromM: this.toNumOrNull(raw.pesoPromM),
      uniformidadH: this.toNumOrNull(raw.uniformidadH),
      uniformidadM: this.toNumOrNull(raw.uniformidadM),
      cvH: this.toNumOrNull(raw.cvH),
      cvM: this.toNumOrNull(raw.cvM),
      observaciones: raw.observaciones,
      kcalAlH: null,
      protAlH: null,
      kcalAveH: null,
      protAveH: null,
      ciclo: raw.ciclo,
      tipoAlimentoHembras: tipoAlH || null,
      tipoAlimentoMachos: tipoAlM || null,
    };

    const isEdit = !!this.editing;
    const data = isEdit 
      ? { ...baseDto, id: this.editing!.id } as UpdateSeguimientoLoteLevanteDto
      : baseDto as CreateSeguimientoLoteLevanteDto;

    this.save.emit({ data, isEdit });
  }

  // ================== HELPERS ==================
  private toNumOrNull(v: any): number | null {
    if (v === null || v === undefined || v === '') return null;
    const n = typeof v === 'number' ? v : Number(v);
    return isNaN(n) ? null : n;
  }

  /** Hoy en formato YYYY-MM-DD (local, sin zona) para <input type="date"> */
  private todayYMD(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** Normaliza cadenas mm/dd/aaaa, dd/mm/aaaa, ISO o Date a YYYY-MM-DD (local) */
  private toYMD(input: string | Date | null | undefined): string | null {
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

    // ISO (con T). Extrae la fecha en LOCAL sin cambiar el día
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
  private ymdToIsoAtNoon(ymd: string): string {
    const iso = new Date(`${ymd}T12:00:00`);
    return iso.toISOString();
  }
}
