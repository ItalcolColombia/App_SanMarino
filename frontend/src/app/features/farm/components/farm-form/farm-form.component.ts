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

@Component({
  selector: 'app-farm-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    FontAwesomeModule
  ],
  templateUrl: './farm-form.component.html',
  styleUrls: ['./farm-form.component.scss']
})
export class FarmFormComponent implements OnInit {
  form!: FormGroup;
  loading = false;
  isEdit = false;
  id!: number;

  constructor(
    private fb: FormBuilder,
    private svc: FarmService,
    private route: ActivatedRoute,
    private router: Router,
    library: FaIconLibrary
  ) {
    library.addIcons(faSave, faTimes);
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      name:       ['', Validators.required],
      companyId:  [null, Validators.required],
      regionalId: [null, Validators.required],
      zoneId:     [null, Validators.required],
      status:     ['', Validators.required]
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

  save(): void {
    if (this.form.invalid) return;
    this.loading = true;
    const v = this.form.value;

    const call$ = this.isEdit
      ? this.svc.update({ id: this.id, ...v } as UpdateFarmDto)
      : this.svc.create(v as CreateFarmDto);

    call$.subscribe(() => {
      this.loading = false;
      this.router.navigate(['config','farm-management']);
    });
  }

  cancel(): void {
    this.router.navigate(['config','farm-management']);
  }
}
