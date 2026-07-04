import { Component, ChangeDetectionStrategy } from '@angular/core';

import { RouterModule } from '@angular/router'; // Asegúrate de importar esto

@Component({
  selector: 'app-config',
  standalone: true,
  imports: [RouterModule], // Añade RouterModule aquí
  templateUrl: './config.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./config.component.scss']
})
export class ConfigComponent {}