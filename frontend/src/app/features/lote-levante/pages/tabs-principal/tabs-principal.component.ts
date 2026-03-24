import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto, LoteMortalidadResumenDto } from '../../../lote/services/lote.service';
import { LotePosturaLevanteDto } from '../../../lote/services/lote-postura-levante.service';
import { TablaListaIndicadoresComponent } from '../tabla-lista-indicadores/tabla-lista-indicadores.component';
import { GraficasPrincipalComponent } from '../graficas-principal/graficas-principal.component';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';


@Component({
  selector: 'app-tabs-principal',
  standalone: true,
  imports: [CommonModule, TablaListaIndicadoresComponent, GraficasPrincipalComponent],
  templateUrl: './tabs-principal.component.html',
  styleUrls: ['./tabs-principal.component.scss']
})
export class TabsPrincipalComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  /** LoteDto (aves-engorde) o LotePosturaLevanteDto (seguimiento levante). */
  @Input() selectedLote: LoteDto | LotePosturaLevanteDto | null = null;
  /** Resumen de descuentos (mortalidad, descarte, error sexaje) sobre el lote en Levante. */
  @Input() resumenLevante: LoteMortalidadResumenDto | null = null;
  @Input() loading: boolean = false;
  /** Si true, deshabilita botones Crear / Editar / Eliminar (ej. lote sin aves o cerrado). */
  @Input() disableCreateEditDelete: boolean = false;
  /** Si true, muestra aviso "Lote cerrado" en la información del lote. */
  @Input() isLoteCerrado: boolean = false;

  @Output() create = new EventEmitter<void>();
  @Output() edit = new EventEmitter<SeguimientoLoteLevanteDto>();
  @Output() delete = new EventEmitter<number>();
  @Output() viewDetail = new EventEmitter<SeguimientoLoteLevanteDto>();

  activeTab: 'general' | 'indicadores' | 'grafica' = 'general';

  // Verificar si el usuario es admin
  isAdmin: boolean = false;

  constructor(
    private storageService: TokenStorageService
  ) { }

  ngOnInit(): void {
    this.checkAdminRole();
  }

  // Verificar si el usuario tiene rol de Admin
  private checkAdminRole(): void {
    const session = this.storageService.get();
    if (session?.user?.roles) {
      this.isAdmin = session.user.roles.includes('Admin');
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
  }

  // ================== EVENTOS ==================
  onTabChange(tab: 'general' | 'indicadores' | 'grafica'): void {
    this.activeTab = tab;
  }

  onCreate(): void {
    this.create.emit();
  }

  onEdit(seg: SeguimientoLoteLevanteDto): void {
    this.edit.emit(seg);
  }

  onDelete(id: number): void {
    this.delete.emit(id);
  }

  onViewDetail(seg: SeguimientoLoteLevanteDto): void {
    this.viewDetail.emit(seg);
  }

  // ================== CALCULO DE EDAD ==================
  /**
   * Edad del lote en la fecha del registro (días de calendario desde encasetamiento).
   * El mismo día del encasetamiento = 0; no usa ceil ni zona UTC sobre la cadena ISO.
   */
  calcularEdadDias(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    const encYmd = this.toYMD(this.selectedLote.fechaEncaset);
    const regYmd = this.toYMD(fechaRegistro);
    if (!encYmd || !regYmd) return 0;
    const MS_DAY = 24 * 60 * 60 * 1000;
    const enc = this.ymdToLocalNoonDate(encYmd);
    const reg = this.ymdToLocalNoonDate(regYmd);
    if (!enc || !reg) return 0;
    const diff = Math.floor((reg.getTime() - enc.getTime()) / MS_DAY);
    return Math.max(0, diff);
  }

  private toYMD(input: string | Date | null | undefined): string | null {
    if (input == null || input === '') return null;
    if (input instanceof Date && !isNaN(input.getTime())) {
      return `${input.getFullYear()}-${String(input.getMonth() + 1).padStart(2, '0')}-${String(input.getDate()).padStart(2, '0')}`;
    }
    const s = String(input).trim();
    const head = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (head) return `${head[1]}-${head[2]}-${head[3]}`;
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }
    return null;
  }

  private ymdToLocalNoonDate(ymd: string | null): Date | null {
    if (!ymd) return null;
    const d = new Date(`${ymd}T12:00:00`);
    return isNaN(d.getTime()) ? null : d;
  }

  formatDMY(input: string | Date | null | undefined): string {
    const ymd = this.toYMD(input);
    if (!ymd) return '';
    const [y, m, d] = ymd.split('-');
    return `${d}/${m}/${y}`;
  }
}
