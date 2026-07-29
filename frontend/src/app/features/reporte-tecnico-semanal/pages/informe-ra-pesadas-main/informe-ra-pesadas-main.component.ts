// Shell del módulo «Informe RA Pesadas».
//
// Conviven DOS granularidades y por eso hay dos modos en vez de tabs planos:
//   · Resumen  → TODOS los lotes en UNA semana calendario (hoja RESUMEN SEMANAL)
//   · Detalle  → UN lote base a lo largo de TODAS sus semanas (+ gráficas)
// Cada modo tiene su propia barra de filtros; mezclarlas en una sola confunde
// y obliga a recargar de más.
//
// El shell es deliberadamente delgado: no toca datos, solo elige qué página
// renderizar. La página del Detalle queda intacta (refactor ≠ cambio de
// comportamiento).
import { ChangeDetectionStrategy, Component } from '@angular/core';

import { ReporteTecnicoSemanalMainComponent } from '../reporte-tecnico-semanal-main/reporte-tecnico-semanal-main.component';
import { ResumenSemanalMainComponent } from '../resumen-semanal-main/resumen-semanal-main.component';

type ModoInforme = 'resumen' | 'detalle';

@Component({
  selector: 'app-informe-ra-pesadas-main',
  standalone: true,
  imports: [ResumenSemanalMainComponent, ReporteTecnicoSemanalMainComponent],
  templateUrl: './informe-ra-pesadas-main.component.html',
  styleUrls: ['./informe-ra-pesadas-main.component.scss'],
  // Estado mutable desde el template ⇒ Eager (en Angular 22 omitirlo = OnPush).
  changeDetection: ChangeDetectionStrategy.Eager
})
export class InformeRaPesadasMainComponent {
  modo: ModoInforme = 'resumen';

  cambiarModo(modo: ModoInforme): void {
    this.modo = modo;
  }
}
