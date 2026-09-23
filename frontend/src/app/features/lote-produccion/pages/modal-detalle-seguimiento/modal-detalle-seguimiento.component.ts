import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HuevoItemSeguimiento, ProduccionService, SeguimientoItemDto, leerHuevoItemsDeMetadata } from '../../services/produccion.service';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';

type PestanaDetalle = 'general' | 'aves' | 'huevos' | 'pesaje' | 'agua';

@Component({
  selector: 'app-modal-detalle-seguimiento',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './modal-detalle-seguimiento.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./modal-detalle-seguimiento.component.scss']
})
export class ModalDetalleSeguimientoComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() seguimientoId: number | null = null;
  @Output() close = new EventEmitter<void>();

  loading: boolean = false;
  seguimiento: SeguimientoItemDto | null = null;
  pestanaActiva: PestanaDetalle = 'general';
  isEcuadorOrPanama: boolean = false;
  /** Santa Reyes: los huevos se clasifican por ítem del catálogo (Primera/Pnc) en vez de las 11 columnas fijas. */
  clasificacionHuevoPorItems: boolean = false;
  /** Empresas sin machos en postura: se retira la pestaña Machos entera (SR-DEF-1). */
  ocultaMachosEnPostura = false;
  /** Referencia estable: evita parsear `metadata` y crear un arreglo nuevo en cada ciclo de CD. */
  huevoItemsGuardados: HuevoItemSeguimiento[] = [];

  constructor(
    private produccionService: ProduccionService,
    private countryFilter: CountryFilterService,
    private companyConfig: ActiveCompanyConfigService
  ) {}

  ngOnInit(): void {
    this.isEcuadorOrPanama = this.countryFilter.isEcuadorOrPanama();
    this.companyConfig.getFlags().subscribe(flags => {
      this.clasificacionHuevoPorItems = flags.clasificacionHuevoPorItems;
      this.ocultaMachosEnPostura = flags.ocultaMachosEnPostura;
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen']?.currentValue && this.seguimientoId) {
      this.pestanaActiva = 'general';
      this.cargarDetalle();
    }
  }

  cargarDetalle(): void {
    if (!this.seguimientoId) return;

    this.loading = true;
    this.seguimiento = null;
    this.huevoItemsGuardados = [];
    this.produccionService.obtenerSeguimientoPorId(this.seguimientoId).subscribe({
      next: (data) => {
        this.seguimiento = data;
        this.huevoItemsGuardados = leerHuevoItemsDeMetadata(data.metadata);
        this.loading = false;
      },
      error: (err) => {
        console.error('Error al cargar detalle:', err);
        this.huevoItemsGuardados = [];
        this.loading = false;
      }
    });
  }

  seleccionarPestana(pestana: PestanaDetalle): void {
    if (pestana === 'agua' && !this.isEcuadorOrPanama) return;
    this.pestanaActiva = pestana;
  }

  onClose(): void {
    this.close.emit();
  }

  /** REQ-012c: rango alineado a la hoja de fórmulas (26-33 / 34-50 / >50, no 25-33). */
  getEtapaLabel(etapa: number): string {
    switch (etapa) {
      case 1: return 'Etapa 1 (Semana 26-33)';
      case 2: return 'Etapa 2 (Semana 34-50)';
      case 3: return 'Etapa 3 (Semana >50)';
      default: return `Etapa ${etapa}`;
    }
  }

  tieneDatosClasificadora(): boolean {
    if (!this.seguimiento) return false;
    return !!(
      this.seguimiento.huevoLimpio || this.seguimiento.huevoTratado ||
      this.seguimiento.huevoSucio || this.seguimiento.huevoDeforme ||
      this.seguimiento.huevoBlanco || this.seguimiento.huevoDobleYema ||
      this.seguimiento.huevoPiso || this.seguimiento.huevoPequeno ||
      this.seguimiento.huevoRoto || this.seguimiento.huevoDesecho ||
      this.seguimiento.huevoOtro
    );
  }

  tieneDatosPesaje(): boolean {
    if (!this.seguimiento) return false;
    return !!(
      this.seguimiento.pesoH || this.seguimiento.pesoM ||
      this.seguimiento.uniformidad || this.seguimiento.coeficienteVariacion ||
      this.seguimiento.observacionesPesaje
    );
  }
}




