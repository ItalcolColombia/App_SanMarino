import { Component, OnInit, ChangeDetectionStrategy }                      from '@angular/core';

import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule }   from '@angular/router';
import { Country }                                from '../models/country.model.model';
import { CountryService }                         from '../services/country/country.service';

@Component({
  selector: 'app-country-detail',
  standalone: true,
  imports: [ReactiveFormsModule, RouterModule],
  templateUrl: './country-detail.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./country-detail.component.scss']
})
export class CountryDetailComponent implements OnInit {
  public id?: number;         // ← ahora público
  form: FormGroup;

  constructor(
    private fb: FormBuilder,
    private svc: CountryService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      name:    ['', Validators.required],
      isoCode:['', Validators.required],
      active: [true]
    });
  }

  ngOnInit() {
    const pid = this.route.snapshot.paramMap.get('id');
    if (pid) {
      this.id = +pid;
      this.svc.getById(this.id)
        .subscribe((country: Country) => this.form.patchValue(country));
    }
  }

  save() {
    const entity: Country = { ...this.form.value, id: this.id };
    const op = this.id
      ? this.svc.update(entity)
      : this.svc.create(entity);

    op.subscribe(() => this.router.navigate(['/config/geography/countries']));
  }
}
