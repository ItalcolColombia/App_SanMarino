// src/app/features/config/geography/department-detail/department-detail.component.ts
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators
} from '@angular/forms';
import { ActivatedRoute, Router, RouterModule }        from '@angular/router';
import { Department }                                  from '../models/department.model.model';
import { DepartmentService } from '../services/department/department.service';


@Component({
  selector: 'app-department-detail',
  standalone: true,
  imports: [ReactiveFormsModule, RouterModule],
  templateUrl: './department-detail.component.html',
  styleUrls: ['./department-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DepartmentDetailComponent implements OnInit {
  public id?: number;
  readonly form: FormGroup = this.fb.group({
    name:      ['', Validators.required],
    countryId: [null, Validators.required],
    active:    [true]
  });

  constructor(
    private fb: FormBuilder,
    private svc: DepartmentService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const pid = this.route.snapshot.paramMap.get('id');
    if (pid) {
      this.id = +pid;
      this.svc.getById(this.id)
        .subscribe((dpt: Department) => this.form.patchValue(dpt));
    }
  }

  save(): void {
    const payload: Department = { ...this.form.value, id: this.id };
    const op = this.id
      ? this.svc.update(payload)
      : this.svc.create(payload);

    op.subscribe(() => {
      this.router.navigate(['/config/geography/departments']);
    });
  }
}
