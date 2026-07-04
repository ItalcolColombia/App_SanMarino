import { Component } from '@angular/core';

import { RouterModule } from '@angular/router'; // Asegúrate de importar esto

@Component({
  selector: 'app-config',
  standalone: true,
  imports: [RouterModule], // Añade RouterModule aquí
  templateUrl: './config.component.html',
  styleUrls: ['./config.component.scss']
})
export class ConfigComponent {}