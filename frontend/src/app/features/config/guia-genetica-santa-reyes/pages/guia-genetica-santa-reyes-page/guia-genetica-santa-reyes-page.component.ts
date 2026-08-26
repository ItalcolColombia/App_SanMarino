// src/app/features/config/guia-genetica-santa-reyes/pages/guia-genetica-santa-reyes-page/guia-genetica-santa-reyes-page.component.ts
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, finalize, takeUntil } from 'rxjs';
import { FaIconLibrary, FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faCircleInfo,
  faDna,
  faDownload,
  faFileExcel,
  faPen,
  faPlus,
  faSearch,
  faSpinner,
  faTimes,
  faTrash,
  faUpload
} from '@fortawesome/free-solid-svg-icons';

import { ToastService } from '../../../../../shared/services/toast.service';
import { ConfirmDialogService } from '../../../../../shared/services/confirm-dialog.service';
import { ActiveCompanyConfigService } from '../../../../../core/services/company-config/active-company-config.service';

import { GuiaGeneticaSantaReyesService } from '../../guia-genetica-santa-reyes.service';
import {
  ColumnaOrdenGuia,
  FilaGuiaGeneticaSantaReyes,
  FormularioGuiaGeneticaSantaReyes,
  GuiaGeneticaSantaReyesDto,
  NOTA_COBERTURA_GUIA,
  MAX_LARGO_ANIO_GUIA,
  MAX_LARGO_RAZA
} from '../../models/guia-genetica-santa-reyes.model';
import { construirFilasTablaGuia } from '../../funciones/construir-filas-tabla.funcion';
import {
  FiltrosCrudosGuia,
  describirFiltrosGuia,
  hayFiltrosActivos,
  normalizarFiltrosGuia
} from '../../funciones/normalizar-filtros.funcion';
import { exportarGuiaExcel } from '../../funciones/exportar-guia-excel.funcion';
import {
  construirCreateDtoGuia,
  construirUpdateDtoGuia,
  formularioDesdeDto,
  formularioGuiaVacio,
  validarFormularioGuia
} from '../../funciones/validar-formulario-guia.funcion';
import {
  EXTENSIONES_IMPORT_GUIA,
  ResumenImportGuia,
  resumirImportGuia,
  validarArchivoImportGuia
} from '../../funciones/resumir-import.funcion';

/**
 * Tope de filas que el backend devuelve en una página (`PaginacionCalculos.MaximoCatalogoMaestro`).
 * Es lo que se pide para exportar: la guía completa son ~615 filas por empresa, así que entra de una.
 */
const TOPE_PAGINA_BACKEND = 2000;

/**
 * Guía Genética **Santa Reyes** (`guia_genetica_santa_reyes`): grid + alta + edición + baja +
 * import y export de Excel.
 *
 * <p>
 * 🔓 Esta pantalla es la **puerta de escritura que la tabla nunca tuvo**: nació *seed-only* (615
 * filas por migración) y hasta acá no había forma de cargarle una línea desde la aplicación.
 * </p>
 *
 * <p>
 * <b>Orquestador delgado</b>: junta estado, dispara el HTTP y muestra el resultado. Todo lo que
 * calcula —filas del grid, filtros, validación del formulario, DTOs, resumen del import y el
 * `.xlsx`— vive en <code>../../funciones/</code> como funciones puras.
 * </p>
 *
 * <p>
 * 🔴 <b><code>changeDetection: Eager</code> es obligatorio acá.</b> En Angular 22 omitir la
 * propiedad significa <code>OnPush</code>, y este componente escribe su estado desde callbacks de
 * <code>HttpClient</code> (<code>this.loading = false</code>, <code>this.filas = …</code>): con
 * OnPush la vista nunca se marca sucia y el modal se queda en «Cargando…» con el 200 ya en Network.
 * </p>
 */
@Component({
  selector: 'app-guia-genetica-santa-reyes-page',
  standalone: true,
  imports: [FormsModule, FontAwesomeModule],
  templateUrl: './guia-genetica-santa-reyes-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./guia-genetica-santa-reyes-page.component.scss']
})
export class GuiaGeneticaSantaReyesPageComponent implements OnInit, OnDestroy {
  private readonly svc = inject(GuiaGeneticaSantaReyesService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly companyConfig = inject(ActiveCompanyConfigService);

  // ── Iconos ────────────────────────────────────────────────────────────────
  faDna = faDna;
  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faUpload = faUpload;
  faDownload = faDownload;
  faFileExcel = faFileExcel;
  faSpinner = faSpinner;
  faSearch = faSearch;
  faTimes = faTimes;
  faCircleInfo = faCircleInfo;

  // ── Constantes que el template necesita ───────────────────────────────────
  readonly notaCobertura = NOTA_COBERTURA_GUIA;
  readonly maxLargoRaza = MAX_LARGO_RAZA;
  readonly maxLargoAnio = MAX_LARGO_ANIO_GUIA;
  readonly extensionesImport = EXTENSIONES_IMPORT_GUIA.join(',');

  // ── Listado ───────────────────────────────────────────────────────────────
  loading = false;
  error: string | null = null;

  /** Filas ya formateadas. Campo, NO getter: un getter devolvería un array nuevo por ciclo. */
  filas: FilaGuiaGeneticaSantaReyes[] = [];

  /** DTOs crudos de la página actual (los usa el modal de edición sin volver a pedirlos). */
  private items: GuiaGeneticaSantaReyesDto[] = [];

  page = 1;
  pageSize = 50;
  pageSizeOptions = [20, 50, 100, 500];
  total = 0;

  sortBy: ColumnaOrdenGuia | null = null;
  sortDesc = false;

  // ── Filtros ───────────────────────────────────────────────────────────────
  filtros: FiltrosCrudosGuia = { raza: '', anioGuia: '', edadDesde: '', edadHasta: '' };

  /**
   * ¿La empresa activa administra esta guía? Sale de `companies.guia_genetica_perfil`, nunca del
   * nombre de la empresa. **Fail-closed**: error, campo ausente o perfil desconocido ⇒ `false` ⇒ la
   * pantalla queda en solo lectura, que es exactamente lo que el backend haría (403) si se
   * intentara escribir igual.
   */
  puedeEscribir = false;

  // ── Modal de alta / edición ───────────────────────────────────────────────
  formOpen = false;
  form: FormularioGuiaGeneticaSantaReyes = formularioGuiaVacio();
  formErrores: string[] = [];
  formAdvertencias: string[] = [];
  guardando = false;

  // ── Modal de import ───────────────────────────────────────────────────────
  importOpen = false;
  importFile: File | null = null;
  importBusy = false;
  importResumen: ResumenImportGuia | null = null;
  importErrores: { fila: number; motivo: string }[] = [];

  // ── Export ────────────────────────────────────────────────────────────────
  exportando = false;

  private readonly destroy$ = new Subject<void>();

  constructor(library: FaIconLibrary) {
    library.addIcons(
      faDna, faPlus, faPen, faTrash, faUpload, faDownload,
      faFileExcel, faSpinner, faSearch, faTimes, faCircleInfo
    );
  }

  ngOnInit(): void {
    this.companyConfig.usaGuiaGeneticaReducida()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: reducida => (this.puedeEscribir = reducida),
        // El servicio ya es fail-closed (devuelve los flags apagados ante error); esto sólo cubre
        // el caso de que alguien le saque el catchError más adelante.
        error: () => (this.puedeEscribir = false)
      });

    this.buscar();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ══════════════════════════════════════════════════════════════════════════
  // Listado
  // ══════════════════════════════════════════════════════════════════════════

  buscar(): void {
    this.loading = true;
    this.error = null;

    const request = normalizarFiltrosGuia(
      this.filtros, this.page, this.pageSize, this.sortBy ?? undefined, this.sortDesc
    );

    this.svc.search(request)
      .pipe(finalize(() => (this.loading = false)), takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.items = res?.items ?? [];
          this.filas = construirFilasTablaGuia(this.items);
          this.total = res?.total ?? 0;
        },
        error: (err: unknown) => {
          console.error(err);
          this.items = [];
          this.filas = [];
          this.total = 0;
          this.error = this.mensajeDeError(err, 'No se pudo cargar la guía genética.');
        }
      });
  }

  aplicarFiltros(): void {
    this.page = 1;
    this.buscar();
  }

  limpiarFiltros(): void {
    this.filtros = { raza: '', anioGuia: '', edadDesde: '', edadHasta: '' };
    this.page = 1;
    this.buscar();
  }

  get hayFiltros(): boolean {
    return hayFiltrosActivos(this.filtros);
  }

  /** Click en un encabezado: primera vez ascendente, segunda descendente. */
  ordenarPor(columna: ColumnaOrdenGuia): void {
    if (this.sortBy === columna) {
      this.sortDesc = !this.sortDesc;
    } else {
      this.sortBy = columna;
      this.sortDesc = false;
    }
    this.page = 1;
    this.buscar();
  }

  /** Indicador del encabezado (`▲` / `▼` / vacío). */
  indicadorOrden(columna: ColumnaOrdenGuia): string {
    if (this.sortBy !== columna) return '';
    return this.sortDesc ? '▼' : '▲';
  }

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

  cambiarPageSize(valor: string | number): void {
    const n = Number(valor);
    if (!Number.isFinite(n) || n <= 0) return;
    this.pageSize = n;
    this.page = 1;
    this.buscar();
  }

  // ══════════════════════════════════════════════════════════════════════════
  // Alta / edición
  // ══════════════════════════════════════════════════════════════════════════

  get editando(): boolean {
    return this.form.id !== null;
  }

  abrirNuevo(): void {
    if (!this.puedeEscribir) return;
    this.form = formularioGuiaVacio();
    this.formErrores = [];
    this.formAdvertencias = [];
    this.guardando = false;
    this.formOpen = true;
  }

  abrirEditar(fila: FilaGuiaGeneticaSantaReyes): void {
    if (!this.puedeEscribir) return;
    this.form = formularioDesdeDto(fila.origen);
    this.formErrores = [];
    this.formAdvertencias = [];
    this.guardando = false;
    this.formOpen = true;
  }

  cerrarForm(): void {
    if (this.guardando) return;
    this.formOpen = false;
  }

  /** Revalida mientras se escribe, para que el aviso de cobertura aparezca al tipear la semana. */
  revalidarForm(): void {
    const resultado = validarFormularioGuia(this.form);
    this.formAdvertencias = resultado.advertencias;
    // Los errores sólo se muestran tras intentar guardar: marcarlos al primer caracter es ruido.
    if (this.formErrores.length) this.formErrores = resultado.errores;
  }

  guardar(): void {
    const resultado = validarFormularioGuia(this.form);
    this.formErrores = resultado.errores;
    this.formAdvertencias = resultado.advertencias;
    if (!resultado.valido) return;

    this.guardando = true;
    const id = this.form.id;

    const peticion$ = id === null
      ? this.svc.create(construirCreateDtoGuia(this.form))
      : this.svc.update(construirUpdateDtoGuia(this.form, id));

    peticion$
      .pipe(finalize(() => (this.guardando = false)), takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.formOpen = false;
          this.toast.success(
            id === null ? 'Línea de guía genética creada.' : 'Línea de guía genética actualizada.'
          );
          this.buscar();
        },
        error: (err: unknown) => {
          console.error(err);
          // El backend responde 403/400 CON cuerpo (`{ message, error }`): el código duplicado y
          // el rechazo por perfil/permiso llegan acá con su motivo, y se muestran en el modal.
          this.formErrores = [this.mensajeDeError(err, 'No se pudo guardar la línea de guía genética.')];
        }
      });
  }

  async eliminar(fila: FilaGuiaGeneticaSantaReyes): Promise<void> {
    if (!this.puedeEscribir) return;

    const confirmado = await this.confirmDialog.ask({
      title: 'Dar de baja la línea',
      message:
        `¿Dar de baja la línea «${fila.raza} · ${fila.anioGuia} · semana ${fila.edad}»?\n\n` +
        'Es una baja suave: la línea deja de listarse y de alimentar los indicadores, pero se ' +
        'conserva en la base y el mismo código se puede volver a crear.',
      type: 'warning',
      confirmText: 'Dar de baja'
    });
    if (!confirmado) return;

    this.loading = true;
    this.svc.delete(fila.id)
      .pipe(finalize(() => (this.loading = false)), takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toast.success('Línea dada de baja.');
          // Si era la única fila de la última página, retroceder para no quedar en una página vacía.
          if (this.filas.length === 1 && this.page > 1) this.page--;
          this.buscar();
        },
        error: (err: unknown) => {
          console.error(err);
          this.toast.error(this.mensajeDeError(err, 'No se pudo dar de baja la línea.'));
        }
      });
  }

  // ══════════════════════════════════════════════════════════════════════════
  // Import
  // ══════════════════════════════════════════════════════════════════════════

  abrirImport(): void {
    if (!this.puedeEscribir) return;
    // Estado limpio en CADA apertura: sin esto, el resultado del import anterior se ve al reabrir.
    this.importFile = null;
    this.importBusy = false;
    this.importResumen = null;
    this.importErrores = [];
    this.importOpen = true;
  }

  cerrarImport(): void {
    if (this.importBusy) return;
    this.importOpen = false;
  }

  onFileSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    this.importFile = input.files?.length ? input.files[0] : null;
    this.importResumen = null;
    this.importErrores = [];
  }

  ejecutarImport(): void {
    const motivo = validarArchivoImportGuia(this.importFile);
    if (motivo) {
      this.importResumen = { tono: 'error', mensaje: motivo, detalle: [], hayErrores: true };
      return;
    }

    this.importBusy = true;
    this.importResumen = null;
    this.importErrores = [];

    this.svc.importExcel(this.importFile!)
      .pipe(finalize(() => (this.importBusy = false)), takeUntil(this.destroy$))
      .subscribe({
        next: resultado => {
          this.importResumen = resumirImportGuia(resultado);
          this.importErrores = resultado?.errores ?? [];

          if (this.importResumen.tono === 'success') this.toast.success(this.importResumen.mensaje);
          else if (this.importResumen.tono === 'warning') this.toast.warning(this.importResumen.mensaje);
          else this.toast.error(this.importResumen.mensaje);

          // El modal queda abierto si hay filas rechazadas: son lo que el usuario tiene que ver.
          if (!this.importResumen.hayErrores) this.importOpen = false;

          this.page = 1;
          this.buscar();
        },
        error: (err: unknown) => {
          console.error(err);
          this.importResumen = {
            tono: 'error',
            mensaje: this.mensajeDeError(err, 'No se pudo importar el archivo.'),
            detalle: [],
            hayErrores: true
          };
        }
      });
  }

  descargarPlantilla(): void {
    this.svc.downloadTemplate()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: blob => this.descargarBlob(blob, `plantilla_guia_genetica_${this.selloFecha()}.xlsx`),
        error: (err: unknown) => {
          console.error(err);
          this.toast.error(this.mensajeDeError(err, 'No se pudo descargar la plantilla.'));
        }
      });
  }

  // ══════════════════════════════════════════════════════════════════════════
  // Export
  // ══════════════════════════════════════════════════════════════════════════

  /**
   * Exporta la guía **completa que cumple el filtro**, no sólo la página visible: se vuelve a pedir
   * con el tope del backend (2.000 filas; la guía entera son ~615). Exportar sólo lo que se ve
   * daría un archivo incompleto que, al reimportarlo, parecería correcto.
   */
  exportarExcel(): void {
    this.exportando = true;

    const request = normalizarFiltrosGuia(
      this.filtros, 1, TOPE_PAGINA_BACKEND, this.sortBy ?? undefined, this.sortDesc
    );

    this.svc.search(request)
      .pipe(finalize(() => (this.exportando = false)), takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          const items = res?.items ?? [];
          if (!items.length) {
            this.toast.info('No hay líneas para exportar con los filtros actuales.');
            return;
          }

          exportarGuiaExcel(items, this.hayFiltros ? 'filtrado' : '');

          const faltantes = (res?.total ?? 0) - items.length;
          if (faltantes > 0) {
            this.toast.warning(
              `Se exportaron ${items.length} línea(s); quedaron ${faltantes} afuera por el tope de ` +
              `${TOPE_PAGINA_BACKEND} filas. Acotá el filtro para bajar el resto.`
            );
          } else {
            this.toast.success(`Se exportaron ${items.length} línea(s). ${describirFiltrosGuia(request)}.`);
          }
        },
        error: (err: unknown) => {
          console.error(err);
          this.toast.error(this.mensajeDeError(err, 'No se pudo exportar la guía.'));
        }
      });
  }

  // ══════════════════════════════════════════════════════════════════════════
  // Auxiliares
  // ══════════════════════════════════════════════════════════════════════════

  /**
   * Mensaje del backend si lo mandó, o el de respaldo. El repo devuelve `403`/`400` con cuerpo
   * `{ message, error }` justamente para que acá se lea el motivo real (un `Forbid()` pelado
   * dejaría el toast en blanco).
   */
  private mensajeDeError(err: unknown, fallback: string): string {
    if (err instanceof HttpErrorResponse) {
      const cuerpo = err.error as { message?: string; error?: string } | string | null;
      if (typeof cuerpo === 'string') {
        if (cuerpo.trim()) return cuerpo.trim();
      } else {
        const mensaje = cuerpo?.message ?? cuerpo?.error;
        if (typeof mensaje === 'string' && mensaje.trim()) return mensaje.trim();
      }
    }
    return fallback;
  }

  private selloFecha(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}${mm}${dd}`;
  }

  private descargarBlob(blob: Blob, nombre: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = nombre;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
}
