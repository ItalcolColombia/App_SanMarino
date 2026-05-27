import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FontAwesomeModule, FaIconLibrary } from '@fortawesome/angular-fontawesome';
import {
  faSave,
  faTimes
} from '@fortawesome/free-solid-svg-icons';
import {
  FarmService,
  CreateFarmDto,
  UpdateFarmDto,
  FarmDto
} from '../../services/farm.service';
import { ClienteService } from '../../../clientes/services/cliente.service';
import { ClienteDto } from '../../../clientes/models/cliente.models';
import { ShowIfCountryDirective } from '../../../../core/directives/show-if-country.directive';

@Component({
  selector: 'app-farm-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    FontAwesomeModule,
    ShowIfCountryDirective
  ],
  templateUrl: './farm-form.component.html',
  styleUrls: ['./farm-form.component.scss']
})
export class FarmFormComponent implements OnInit {
  form!: FormGroup;
  loading = false;
  isEdit = false;
  id!: number;

  clientes: ClienteDto[] = [];

  constructor(
    private fb: FormBuilder,
    private svc: FarmService,
    private clienteService: ClienteService,
    private route: ActivatedRoute,
    private router: Router,
    library: FaIconLibrary
  ) {
    library.addIcons(faSave, faTimes);
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      name:           ['', Validators.required],
      companyId:      [null, Validators.required],
      regionalId:     [null, Validators.required],
      zoneId:         [null, Validators.required],
      status:         ['', Validators.required],
      // Campos Panamá
      clienteId:      [null],
      zona:           [{ value: '', disabled: true }],
      certificadoGab: [false],
      latitud:        [null],
      longitud:       [null]
    });

    // Cargar lista de clientes para el select Panamá
    this.clienteService.getAll().subscribe({
      next: (data) => { this.clientes = data ?? []; },
      error: (err) => { console.error('Error cargando clientes:', err); }
    });

    this.route.paramMap.subscribe(p => {
      const param = p.get('id');
      if (param && param !== 'new') {
        this.isEdit = true;
        this.id = +param;
        this.svc.getById(this.id).subscribe((farm: FarmDto) =>
          this.form.patchValue(farm)
        );
      }
    });
  }

  /** Autopobla 'zona' a partir del cliente seleccionado. */
  onClienteChange(clienteIdRaw: any): void {
    const clienteId = clienteIdRaw === null || clienteIdRaw === '' || clienteIdRaw === 'null'
      ? null
      : Number(clienteIdRaw);

    if (clienteId === null || isNaN(clienteId)) {
      this.form.get('zona')?.setValue('');
      this.form.get('clienteId')?.setValue(null);
      return;
    }

    const cliente = this.clientes.find(c => c.id === clienteId);
    this.form.get('zona')?.setValue(cliente?.zona ?? '');
  }

  /** Captura la ubicación geográfica actual del dispositivo. */
  capturarUbicacion(): void {
    if (!navigator.geolocation) {
      alert('Geolocalización no disponible');
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        this.form.patchValue({
          latitud: pos.coords.latitude,
          longitud: pos.coords.longitude
        });
      },
      (err) => alert('No se pudo obtener ubicación: ' + err.message),
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  save(): void {
    if (this.form.invalid) return;
    this.loading = true;

    // getRawValue() incluye los controles disabled (ej. 'zona')
    const v = this.form.getRawValue();

    const payload = {
      ...v,
      clienteId:      v.clienteId ?? null,
      zona:           v.zona ?? null,
      certificadoGab: v.certificadoGab ?? false,
      latitud:        v.latitud === '' || v.latitud === undefined ? null : v.latitud,
      longitud:       v.longitud === '' || v.longitud === undefined ? null : v.longitud
    };

    const call$ = this.isEdit
      ? this.svc.update({ id: this.id, ...payload } as UpdateFarmDto)
      : this.svc.create(payload as CreateFarmDto);

    call$.subscribe(() => {
      this.loading = false;
      this.router.navigate(['config','farm-management']);
    });
  }

  cancel(): void {
    this.router.navigate(['config','farm-management']);
  }
}
