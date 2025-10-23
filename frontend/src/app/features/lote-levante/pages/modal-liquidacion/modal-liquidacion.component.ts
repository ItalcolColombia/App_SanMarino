import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LiquidacionTecnicaComponent } from '../../components/liquidacion-tecnica/liquidacion-tecnica.component';
import { LiquidacionComparacionComponent } from '../../components/liquidacion-comparacion/liquidacion-comparacion.component';

@Component({
  selector: 'app-modal-liquidacion',
  standalone: true,
  imports: [CommonModule, LiquidacionTecnicaComponent, LiquidacionComparacionComponent],
  templateUrl: './modal-liquidacion.component.html',
  styleUrls: ['./modal-liquidacion.component.scss']
})
export class ModalLiquidacionComponent implements OnInit {
  @Input() isOpen: boolean = false;
  @Input() loteId: number | null = null;
  @Input() loteNombre: string = '';
  
  @Output() close = new EventEmitter<void>();

  constructor() { }

  ngOnInit(): void {
  }

  onClose(): void {
    this.close.emit();
  }
}
