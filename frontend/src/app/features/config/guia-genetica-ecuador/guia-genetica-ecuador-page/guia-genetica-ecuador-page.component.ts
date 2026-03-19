import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, finalize, takeUntil } from 'rxjs';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faDna,
  faUpload,
  faPlus,
  faSpinner,
  faTimes,
  faPen,
  faTrash,
  faChevronLeft,
  faChevronRight
} from '@fortawesome/free-solid-svg-icons';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import {
  GuiaGeneticaEcuadorDetalleDto,
  GuiaGeneticaEcuadorDetalleInputDto,
  GuiaGeneticaEcuadorImportResultDto,
  GuiaGeneticaEcuadorService
} from '../guia-genetica-ecuador.service';

type SexoTab = 'mixto' | 'hembra' | 'macho';

@Component({
  selector: 'app-guia-genetica-ecuador-page',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule, SidebarComponent],
  templateUrl: './guia-genetica-ecuador-page.component.html',
  styleUrls: ['./guia-genetica-ecuador-page.component.scss']
})
export class GuiaGeneticaEcuadorPageComponent implements OnInit, OnDestroy {
  faDna = faDna;
  faUpload = faUpload;
  faPlus = faPlus;
  faSpinner = faSpinner;
  faTimes = faTimes;
  faPen = faPen;
  faTrash = faTrash;
  faChevronLeft = faChevronLeft;
  faChevronRight = faChevronRight;

  loading = false;
  error: string | null = null;

  razas: string[] = [];
  anos: number[] = [];
  razaSel = '';
  anioSel: number | null = null;

  anosLoading = false;
  sexosDisponibles: SexoTab[] = ['mixto', 'hembra', 'macho'];

  tab: SexoTab = 'mixto';
  filas: GuiaGeneticaEcuadorDetalleDto[] = [];

  private razaChangeDebounce?: number;

  // Paginación (en cliente, porque el endpoint devuelve todas las filas del sexo)
  page = 1;
  pageSize = 5;
  pageSizeOptions = [5, 10];
  get totalFilas(): number {
    return this.filas?.length ?? 0;
  }
  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalFilas / this.pageSize));
  }
  get paginatedFilas(): GuiaGeneticaEcuadorDetalleDto[] {
    const start = (this.page - 1) * this.pageSize;
    return (this.filas ?? []).slice(start, start + this.pageSize);
  }

  importOpen = false;
  importFile: File | null = null;
  importRaza = '';
  importAnio: number | null = null;
  importEstado: 'active' | 'inactive' = 'active';
  importBusy = false;
  importResult: GuiaGeneticaEcuadorImportResultDto | null = null;

  manualOpen = false;
  manualSexo: SexoTab = 'mixto';
  manualEstado: 'active' | 'inactive' = 'active';
  manualRows: GuiaGeneticaEcuadorDetalleInputDto[] = [];
  manualBusy = false;

  // Editar fila (sobre el sexo actual)
  editOpen = false;
  editBusy = false;
  editRowDia: number | null = null;
  editForm: GuiaGeneticaEcuadorDetalleInputDto = this.filaVacia();

  // Eliminar fila (confirmación)
  deleteOpen = false;
  deleteBusy = false;
  deleteDia: number | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private svc: GuiaGeneticaEcuadorService,
    library: FaIconLibrary
  ) {
    library.addIcons(
      faDna,
      faUpload,
      faPlus,
      faSpinner,
      faTimes,
      faPen,
      faTrash,
      faChevronLeft,
      faChevronRight
    );
  }

  ngOnInit(): void {
    this.svc
      .getFilters()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (f) => {
          this.razas = f.razas ?? [];
          // Los años dependen de la raza; se consultan cuando el usuario elige/edita la raza.
          this.anos = [];
          this.anioSel = null;
        },
        error: () => (this.error = 'No se pudieron cargar los filtros.')
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get puedeConsultar(): boolean {
    return !!this.razaSel?.trim() && this.anioSel != null && this.anioSel > 0;
  }

  onRazaChange(value: string): void {
    const raza = (value ?? '').trim();
    this.error = null;

    // Debounce para no hacer demasiadas llamadas mientras el usuario escribe.
    if (this.razaChangeDebounce != null) {
      window.clearTimeout(this.razaChangeDebounce);
    }

    this.razaChangeDebounce = window.setTimeout(() => {
      if (!raza) {
        this.anos = [];
        this.anioSel = null;
        this.filas = [];
        this.sexosDisponibles = ['mixto', 'hembra', 'macho'];
        return;
      }

      this.anosLoading = true;
      this.svc
        .getAnosPorRaza(raza)
        .pipe(
          finalize(() => (this.anosLoading = false)),
          takeUntil(this.destroy$)
        )
        .subscribe({
          next: (anos) => {
            this.anos = anos ?? [];
            this.anioSel = null;
            this.filas = [];

            if (this.anos.length === 1) {
              this.anioSel = this.anos[0];
              this.cargarDatosConSexos();
            }
          },
          error: () => (this.error = 'No se pudieron cargar los años para la raza seleccionada.')
        });
    }, 250);
  }

  onAnioChange(value: number | null): void {
    this.error = null;
    this.anioSel = value;
    this.filas = [];
    if (this.puedeConsultar) {
      this.cargarDatosConSexos();
    }
  }

  private cargarDatosConSexos(): void {
    if (!this.puedeConsultar) return;
    const raza = this.razaSel.trim();
    const anio = this.anioSel!;

    this.svc
      .getSexos(raza, anio)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (sexos) => {
          const disponibles = new Set((sexos ?? []).map((s) => (s ?? '').trim().toLowerCase()));
          this.sexosDisponibles = (['mixto', 'hembra', 'macho'] as SexoTab[]).filter((s) => disponibles.has(s));
          if (this.sexosDisponibles.length === 0) {
            this.sexosDisponibles = ['mixto', 'hembra', 'macho'];
          }

          if (!this.sexosDisponibles.includes(this.tab)) {
            this.tab = this.sexosDisponibles[0];
          }

          this.cargarDatos();
        },
        error: () => this.cargarDatos()
      });
  }

  setTab(t: SexoTab): void {
    this.tab = t;
    this.cargarDatos();
  }

  cargarDatos(): void {
    if (!this.puedeConsultar) {
      this.filas = [];
      return;
    }
    this.loading = true;
    this.error = null;
    this.svc
      .getDatos(this.razaSel.trim(), this.anioSel!, this.tab)
      .pipe(
        finalize(() => (this.loading = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (rows) => {
          this.filas = rows ?? [];
          this.page = 1;
        },
        error: (e) => (this.error = e?.error?.message ?? 'Error al cargar datos.')
      });
  }

  aplicarFiltro(): void {
    this.cargarDatos();
  }

  openImport(): void {
    this.importRaza = this.razaSel?.trim() ?? '';
    this.importAnio = this.anioSel;
    this.importEstado = 'active';
    this.importFile = null;
    this.importResult = null;
    this.importOpen = true;
  }

  closeImport(): void {
    this.importOpen = false;
  }

  onImportFile(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const f = input.files?.[0];
    this.importFile = f ?? null;
  }

  ejecutarImport(): void {
    if (!this.importFile || !this.importRaza?.trim() || !this.importAnio) return;
    this.importBusy = true;
    this.importResult = null;
    this.svc
      .importExcel(this.importFile, this.importRaza.trim(), this.importAnio, this.importEstado)
      .pipe(
        finalize(() => (this.importBusy = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (r) => {
          this.importResult = r;
          if (r.success) {
            this.razaSel = this.importRaza.trim();
            this.anioSel = this.importAnio!;
            // Actualizar lista de años para que el selector quede consistente.
            this.svc
              .getAnosPorRaza(this.razaSel)
              .pipe(takeUntil(this.destroy$))
              .subscribe((anos) => (this.anos = anos ?? []));
            this.cargarDatosConSexos();
          }
        },
        error: (e) =>
          (this.importResult = {
            success: false,
            totalFilasProcesadas: 0,
            totalDetallesInsertados: 0,
            errorFilas: 1,
            errors: [e?.error?.message ?? 'Error de red']
          })
      });
  }

  openManual(): void {
    if (!this.puedeConsultar) {
      this.error = 'Seleccione raza y año antes de dar de alta manual.';
      return;
    }
    this.manualSexo = this.tab;
    this.manualEstado = 'active';
    this.manualRows = [this.filaVacia()];
    this.manualOpen = true;
  }

  closeManual(): void {
    this.manualOpen = false;
  }

  filaVacia(): GuiaGeneticaEcuadorDetalleInputDto {
    return {
      dia: 1,
      pesoCorporalG: 0,
      gananciaDiariaG: 0,
      promedioGananciaDiariaG: 0,
      cantidadAlimentoDiarioG: 0,
      alimentoAcumuladoG: 0,
      ca: 0,
      mortalidadSeleccionDiaria: 0
    };
  }

  addManualRow(): void {
    this.manualRows.push(this.filaVacia());
  }

  removeManualRow(i: number): void {
    this.manualRows.splice(i, 1);
    if (this.manualRows.length === 0) this.manualRows.push(this.filaVacia());
  }

  guardarManual(): void {
    if (!this.puedeConsultar) return;
    this.manualBusy = true;
    this.svc
      .manual({
        raza: this.razaSel.trim(),
        anioGuia: this.anioSel!,
        sexo: this.manualSexo,
        estado: this.manualEstado,
        items: this.manualRows.map((r) => ({
          ...r,
          dia: Number(r.dia),
          pesoCorporalG: Number(r.pesoCorporalG),
          gananciaDiariaG: Number(r.gananciaDiariaG),
          promedioGananciaDiariaG: Number(r.promedioGananciaDiariaG),
          cantidadAlimentoDiarioG: Number(r.cantidadAlimentoDiarioG),
          alimentoAcumuladoG: Number(r.alimentoAcumuladoG),
          ca: Number(r.ca),
          mortalidadSeleccionDiaria: Number(r.mortalidadSeleccionDiaria)
        }))
      })
      .pipe(
        finalize(() => (this.manualBusy = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.closeManual();
          this.tab = this.manualSexo;
          this.cargarDatos();
        },
        error: (e) => (this.error = e?.error?.message ?? 'Error al guardar.')
      });
  }

  cambiarPage(p: number): void {
    if (p < 1 || p > this.totalPages) return;
    this.page = p;
  }

  onChangePageSize(size: number): void {
    this.pageSize = size;
    this.page = 1;
  }

  private mapDetalleToInput(r: GuiaGeneticaEcuadorDetalleDto): GuiaGeneticaEcuadorDetalleInputDto {
    return {
      dia: Number(r.dia),
      pesoCorporalG: Number(r.pesoCorporalG),
      gananciaDiariaG: Number(r.gananciaDiariaG),
      promedioGananciaDiariaG: Number(r.promedioGananciaDiariaG),
      cantidadAlimentoDiarioG: Number(r.cantidadAlimentoDiarioG),
      alimentoAcumuladoG: Number(r.alimentoAcumuladoG),
      ca: Number(r.ca),
      mortalidadSeleccionDiaria: Number(r.mortalidadSeleccionDiaria)
    };
  }

  openEditRow(r: GuiaGeneticaEcuadorDetalleDto): void {
    this.error = null;
    this.editRowDia = r.dia;
    this.editForm = this.mapDetalleToInput(r);
    this.editOpen = true;
  }

  closeEdit(): void {
    this.editOpen = false;
  }

  guardarEdit(): void {
    if (!this.puedeConsultar || this.editRowDia == null) return;

    this.editBusy = true;
    const estado: 'active' | 'inactive' = 'active';

    const items = (this.filas ?? []).map((x) => this.mapDetalleToInput(x));
    const idx = items.findIndex((x) => x.dia === this.editRowDia);
    if (idx < 0) {
      this.editBusy = false;
      this.error = 'No se pudo encontrar la fila a editar (cambió el día).';
      return;
    }

    // Dia se mantiene (no editable) para evitar inconsistencias.
    items[idx] = { ...this.editForm, dia: this.editRowDia };

    this.svc
      .manual({
        raza: this.razaSel.trim(),
        anioGuia: this.anioSel!,
        sexo: this.tab,
        estado,
        items
      })
      .pipe(
        finalize(() => (this.editBusy = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.closeEdit();
          this.cargarDatos();
        },
        error: (e) => (this.error = e?.error?.message ?? 'Error al editar.')
      });
  }

  openDeleteRow(r: GuiaGeneticaEcuadorDetalleDto): void {
    this.error = null;
    this.deleteDia = r.dia;
    this.deleteOpen = true;
  }

  closeDelete(): void {
    this.deleteOpen = false;
  }

  confirmarDelete(): void {
    if (!this.puedeConsultar || this.deleteDia == null) return;
    this.error = null;
    if ((this.filas?.length ?? 0) <= 1) {
      this.error = 'No se puede eliminar la última fila del sexo. Usa “Alta manual” para manejarlo.';
      return;
    }

    this.deleteBusy = true;
    const estado: 'active' | 'inactive' = 'active';

    const items = (this.filas ?? [])
      .filter((x) => x.dia !== this.deleteDia)
      .map((x) => this.mapDetalleToInput(x))
      .sort((a, b) => a.dia - b.dia);

    this.svc
      .manual({
        raza: this.razaSel.trim(),
        anioGuia: this.anioSel!,
        sexo: this.tab,
        estado,
        items
      })
      .pipe(
        finalize(() => (this.deleteBusy = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: () => {
          this.closeDelete();
          this.cargarDatos();
        },
        error: (e) => (this.error = e?.error?.message ?? 'Error al eliminar.')
      });
  }
}
