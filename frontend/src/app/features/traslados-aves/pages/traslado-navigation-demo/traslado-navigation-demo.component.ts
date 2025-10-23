// src/app/features/traslados-aves/pages/traslado-navigation-demo/traslado-navigation-demo.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TrasladoNavigationListComponent } from '../../components/traslado-navigation-list/traslado-navigation-list.component';

@Component({
  selector: 'app-traslado-navigation-demo',
  standalone: true,
  imports: [CommonModule, TrasladoNavigationListComponent],
  templateUrl: './traslado-navigation-demo.component.html',
  styleUrls: ['./traslado-navigation-demo.component.scss']
})
export class TrasladoNavigationDemoComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

}





