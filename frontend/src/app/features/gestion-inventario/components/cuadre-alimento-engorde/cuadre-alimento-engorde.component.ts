// src/app/features/gestion-inventario/components/cuadre-alimento-engorde/cuadre-alimento-engorde.component.ts
//
// Pone a la vista los dos detectores del alimento de engorde, que hasta hoy solo existían en el
// backend:
//   1. el CUADRE por galpón — «saldo del ciclo activo == stock físico − movimientos posteriores».
//      El descuadre que originó el trabajo de jul-2026 lo encontró un humano de operación semanas
//      después; el endpoint existe desde entonces y NINGUNA pantalla lo leía;
//   2. la anomalía R2 — lotes que se liquidaron dejando alimento en el galpón. La regla operativa es
//      que al liquidar el galpón queda en cero y el sobrante se traslada. Esto no bloquea nada:
//      señala lo que quedó.
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { inject } from '@angular/core';

import {
  AnomaliaAlimentoLiquidadoDto,
  AnomaliaAlimentoLiquidadoFilaDto,
  CuadrarGalponAlimentoResultDto,
  CuadreAlimentoEngordeDto,
  CuadreAlimentoEngordeFilaDto,
  CuadreAlimentoEngordeService,
  EstadoAlimentoLiquidado,
  EstadoCuadreAlimento
} from '../../services/cuadre-alimento-engorde.service';
import { fechaCorta } from '../../../../shared/utils/format';
import { ModalCuadrarGalponComponent } from './modal-cuadrar-galpon/modal-cuadrar-galpon.component';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { ToastService } from '../../../../shared/services/toast.service';

@Component({
  selector: 'app-cuadre-alimento-engorde',
  standalone: true,
  imports: [CommonModule, FormsModule, ModalCuadrarGalponComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './cuadre-alimento-engorde.component.html',
  styleUrls: ['./cuadre-alimento-engorde.component.scss']
})
export class CuadreAlimentoEngordeComponent implements OnInit {
  private readonly svc = inject(CuadreAlimentoEngordeService);
  private readonly permSvc = inject(UserPermissionService);
  private readonly toast = inject(ToastService);

  /**
   * Permiso de ESCRITURA de la pestana. Se reusa la key que ya existia para el mismo concepto en
   * vez de inventar una segunda llave para la misma puerta. Campo y no getter: los permisos solo
   * cambian con la sesion.
   */
  readonly permCuadrar = 'cuadrar_ingresos_traslados_seguimiento';
  puedeCuadrar = false;

  // ── Modal de cuadre
  cuadrarAbierto = false;
  filaACuadrar: CuadreAlimentoEngordeFilaDto | null = null;

  // ── Cuadre por galpón
  cuadre: CuadreAlimentoEngordeDto | null = null;
  cargandoCuadre = false;
  errorCuadre: string | null = null;
  soloConProblemas = false;

  // ── Anomalía R2: liquidados con alimento
  liquidados: AnomaliaAlimentoLiquidadoDto | null = null;
  cargandoLiquidados = false;
  errorLiquidados: string | null = null;
  soloAnomalias = false;

  readonly EstadoCuadre = EstadoCuadreAlimento;
  readonly EstadoLiquidado = EstadoAlimentoLiquidado;

  ngOnInit(): void {
    this.puedeCuadrar = this.permSvc.has(this.permCuadrar);
    this.cargarTodo();
  }

  /** ¿Esta fila tiene algo que cerrar? Mismo criterio que el badge de estado. */
  requiereAtencion(fila: CuadreAlimentoEngordeFilaDto): boolean {
    return fila.estado !== EstadoCuadreAlimento.Ok;
  }

  abrirCuadrar(fila: CuadreAlimentoEngordeFilaDto): void {
    this.filaACuadrar = fila;
    this.cuadrarAbierto = true;
  }

  cerrarCuadrar(): void {
    this.cuadrarAbierto = false;
    this.filaACuadrar = null;
  }

  onCuadrado(r: CuadrarGalponAlimentoResultDto): void {
    this.cerrarCuadrar();
    this.toast.success(`${r.granja} / ${r.galponId}: ${r.resumen}`);
    // Se recarga desde el backend en vez de retocar la fila en memoria: el numero que importa es el
    // que devuelve la fn, no el que la pantalla creia tener.
    this.cargarCuadre();
  }

  cargarTodo(): void {
    this.cargarCuadre();
    this.cargarLiquidados();
  }

  cargarCuadre(): void {
    this.cargandoCuadre = true;
    this.errorCuadre = null;
    this.svc
      .obtenerCuadre(this.soloConProblemas)
      .pipe(finalize(() => (this.cargandoCuadre = false)))
      .subscribe({
        next: r => (this.cuadre = r),
        error: err => {
          this.cuadre = null;
          this.errorCuadre = err?.error?.message ?? err?.message ?? 'No se pudo cargar el cuadre de alimento.';
        }
      });
  }

  cargarLiquidados(): void {
    this.cargandoLiquidados = true;
    this.errorLiquidados = null;
    this.svc
      .obtenerLiquidadosConAlimento(this.soloAnomalias)
      .pipe(finalize(() => (this.cargandoLiquidados = false)))
      .subscribe({
        next: r => (this.liquidados = r),
        error: err => {
          this.liquidados = null;
          this.errorLiquidados =
            err?.error?.message ?? err?.message ?? 'No se pudo cargar el alimento de los lotes liquidados.';
        }
      });
  }

  // ── Etiquetas (el color y el texto salen del estado que ya decidió el backend) ──

  claseEstadoCuadre(fila: CuadreAlimentoEngordeFilaDto): string {
    switch (fila.estado) {
      case EstadoCuadreAlimento.Descuadrado:  return 'badge badge--danger';
      case EstadoCuadreAlimento.SaldoNegativo: return 'badge badge--warn';
      default:                                 return 'badge badge--ok';
    }
  }

  textoEstadoCuadre(fila: CuadreAlimentoEngordeFilaDto): string {
    switch (fila.estado) {
      case EstadoCuadreAlimento.Descuadrado:   return 'No cuadra';
      case EstadoCuadreAlimento.SaldoNegativo: return 'Días en negativo';
      default:                                 return 'Cuadra';
    }
  }

  claseEstadoLiquidado(fila: AnomaliaAlimentoLiquidadoFilaDto): string {
    switch (fila.estado) {
      case EstadoAlimentoLiquidado.SinRespaldoFisico:  return 'badge badge--danger';
      case EstadoAlimentoLiquidado.PendienteEnGalpon:  return 'badge badge--warn';
      default:                                         return 'badge badge--ok';
    }
  }

  textoEstadoLiquidado(fila: AnomaliaAlimentoLiquidadoFilaDto): string {
    switch (fila.estado) {
      case EstadoAlimentoLiquidado.SinRespaldoFisico: return 'Sin respaldo físico';
      case EstadoAlimentoLiquidado.PendienteEnGalpon: return 'Pendiente en el galpón';
      default:                                        return 'Trasladado';
    }
  }

  /** Fecha corta para la vista; delega en el helper compartido. */
  fecha(iso: string | null | undefined): string {
    return fechaCorta(iso);
  }
}
