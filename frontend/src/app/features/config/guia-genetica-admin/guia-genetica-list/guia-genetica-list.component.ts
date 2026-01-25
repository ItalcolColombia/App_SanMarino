import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { finalize, Subject, takeUntil } from 'rxjs';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { FaIconLibrary } from '@fortawesome/angular-fontawesome';
import { faList, faPlus, faEye, faPen, faTrash, faUpload, faDownload, faSpinner, faSearch, faTimes } from '@fortawesome/free-solid-svg-icons';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { ExcelImportResultDto, GuiaGeneticaAdminService, ProduccionAvicolaRawDto, ProduccionAvicolaRawFilterOptionsDto } from '../guia-genetica-admin.service';

@Component({
  selector: 'app-guia-genetica-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, FontAwesomeModule, SidebarComponent],
  templateUrl: './guia-genetica-list.component.html',
  styleUrls: ['./guia-genetica-list.component.scss']
})
export class GuiaGeneticaListComponent implements OnInit, OnDestroy {
  faList = faList;
  faPlus = faPlus;
  faEye = faEye;
  faPen = faPen;
  faTrash = faTrash;
  faUpload = faUpload;
  faDownload = faDownload;
  faSpinner = faSpinner;
  faSearch = faSearch;
  faTimes = faTimes;

  loading = false;
  error: string | null = null;

  // Filtros
  filtroAnioGuia = '';
  filtroRaza = '';
  filtroEdad = '';

  // Opciones de filtros (desde datos cargados)
  anioGuias: string[] = [];
  razas: string[] = [];

  // Paginación
  page = 1;
  pageSize = 15;
  total = 0;

  items: ProduccionAvicolaRawDto[] = [];

  // Modal carga masiva
  uploadModalOpen = false;
  uploadMode: 'validate' | 'import' = 'import';
  uploadFile: File | null = null;
  uploadLoading = false;
  uploadResult: ExcelImportResultDto | null = null;
  uploadPreview: ProduccionAvicolaRawDto[] | null = null;
  uploadErrors: string[] = [];

  private destroy$ = new Subject<void>();

  constructor(
    private svc: GuiaGeneticaAdminService,
    private router: Router,
    library: FaIconLibrary
  ) {
    library.addIcons(faList, faPlus, faEye, faPen, faTrash, faUpload, faDownload, faSpinner, faSearch, faTimes);
  }

  ngOnInit(): void {
    this.cargarFiltros();
    this.buscar();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  buscar(): void {
    this.loading = true;
    this.error = null;

    this.svc.search({
      anioGuia: this.filtroAnioGuia?.trim() || undefined,
      raza: this.filtroRaza?.trim() || undefined,
      edad: this.filtroEdad?.trim() || undefined,
      page: this.page,
      pageSize: this.pageSize,
      sortBy: 'id',
      sortDesc: true
    })
      .pipe(
        finalize(() => (this.loading = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (res) => {
          this.items = res.items ?? [];
          this.total = res.total ?? 0;
        },
        error: (err) => {
          console.error(err);
          this.error = 'No se pudo cargar la guía genética.';
          this.items = [];
          this.total = 0;
        }
      });
  }

  resetFiltros(): void {
    this.filtroAnioGuia = '';
    this.filtroRaza = '';
    this.filtroEdad = '';
    this.page = 1;
    this.buscar();
  }

  cargarFiltros(): void {
    this.svc.getFilters()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res: ProduccionAvicolaRawFilterOptionsDto) => {
          this.anioGuias = Array.isArray(res?.anioGuias) ? res.anioGuias : [];
          this.razas = Array.isArray(res?.razas) ? res.razas : [];
        },
        error: (err) => {
          console.error(err);
          this.anioGuias = [];
          this.razas = [];
        }
      });
  }

  goNuevo(): void {
    this.router.navigate(['/config/guia-genetica/new']);
  }

  goDetalle(id: number): void {
    this.router.navigate(['/config/guia-genetica', id]);
  }

  goEditar(id: number): void {
    this.router.navigate(['/config/guia-genetica', id, 'edit']);
  }

  eliminar(id: number): void {
    if (!confirm('¿Eliminar este registro de guía genética?')) return;
    this.loading = true;
    this.svc.delete(id)
      .pipe(finalize(() => (this.loading = false)), takeUntil(this.destroy$))
      .subscribe({
        next: () => this.buscar(),
        error: (err) => {
          console.error(err);
          alert('No se pudo eliminar el registro.');
        }
      });
  }

  // ====== Plantilla ======
  descargarPlantilla(): void {
    this.svc.downloadTemplate()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (blob) => {
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `plantilla_guia_genetica_${new Date().toISOString().split('T')[0]}.xlsx`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
        },
        error: (err) => {
          console.error(err);
          alert('No se pudo descargar la plantilla.');
        }
      });
  }

  // ====== Modal carga masiva ======
  openUploadModal(): void {
    this.uploadModalOpen = true;
    this.uploadMode = 'import';
    this.uploadFile = null;
    this.uploadLoading = false;
    this.uploadResult = null;
    this.uploadPreview = null;
    this.uploadErrors = [];
  }

  closeUploadModal(): void {
    this.uploadModalOpen = false;
  }

  onFileSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files && input.files.length ? input.files[0] : null;
    this.uploadFile = file;
    this.uploadResult = null;
    this.uploadPreview = null;
    this.uploadErrors = [];
  }

  runUpload(): void {
    if (!this.uploadFile) {
      this.uploadErrors = ['Debe seleccionar un archivo Excel (.xlsx/.xls).'];
      return;
    }

    this.uploadLoading = true;
    this.uploadResult = null;
    this.uploadPreview = null;
    this.uploadErrors = [];

    if (this.uploadMode === 'validate') {
      this.svc.validateExcel(this.uploadFile)
        .pipe(
          finalize(() => (this.uploadLoading = false)),
          takeUntil(this.destroy$)
        )
        .subscribe({
          next: (rows) => {
            this.uploadPreview = Array.isArray(rows) ? rows : [];
            if (!this.uploadPreview.length) {
              this.uploadErrors = ['No se encontraron filas válidas en el archivo.'];
            }
          },
          error: (err: unknown) => {
            console.error(err);
            this.uploadErrors = ['Error validando el archivo. Revise el formato de columnas y el tamaño (máx 10MB).'];
          }
        });
      return;
    }

    this.svc.importExcel(this.uploadFile)
      .pipe(
        finalize(() => (this.uploadLoading = false)),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (result) => {
          this.uploadResult = result;
          if (this.uploadResult?.errors?.length) {
            this.uploadErrors = this.uploadResult.errors;
          }
          // refrescar listado
          this.buscar();
        },
        error: (err: unknown) => {
          console.error(err);
          this.uploadErrors = ['Error importando el archivo. Revise el formato de columnas y el tamaño (máx 10MB).'];
        }
      });
  }

  // ====== Paginación ======
  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  prevPage(): void {
    if (this.page <= 1) return;
    this.page--;
    this.buscar();
  }

  nextPage(): void {
    if (this.page >= this.totalPages) return;
    this.page++;
    this.buscar();
  }
}

