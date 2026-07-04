// src/app/features/config/geography/city-detail/city-detail.component.ts
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';
import { ActivatedRoute, Router, RouterModule }        from '@angular/router';
import { City }                                        from '../models/city.model.model';
import { CityService }                                 from '../services/city/city.service';

@Component({
  selector: 'app-city-detail',
  standalone: true,
  imports: [ReactiveFormsModule, RouterModule],
  templateUrl: './city-detail.component.html',
  styleUrls: ['./city-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CityDetailComponent implements OnInit {
  public id?: number;
  readonly form: FormGroup = this.fb.group({
    name:         ['', Validators.required],
    departmentId: [null, Validators.required],
    active:       [true]
  });

  constructor(
    private fb: FormBuilder,
    private svc: CityService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const pid = this.route.snapshot.paramMap.get('id');
    if (pid) {
      this.id = +pid;
      this.svc.getById(this.id)
        .subscribe((city: City) => this.form.patchValue(city));
    }
  }

  save(): void {
    const payload: City = { ...this.form.value, id: this.id };
    const operation = this.id
      ? this.svc.update(payload)
      : this.svc.create(payload);

    operation.subscribe(() => {
      this.router.navigate(['/config/geography/cities']);
    });
  }
}
