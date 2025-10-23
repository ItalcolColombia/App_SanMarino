// src/app/shared/components/company-selector/company-selector.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { ActiveCompanyService } from '../../../core/auth/active-company.service';

@Component({
  selector: 'app-company-selector',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './company-selector.component.html',
  styleUrls: ['./company-selector.component.scss']
})
export class CompanySelectorComponent implements OnInit, OnDestroy {
  @Input() showLabel: boolean = true;
  @Input() size: 'sm' | 'md' | 'lg' = 'md';
  @Input() variant: 'default' | 'minimal' = 'default';
  
  @Output() companyChanged = new EventEmitter<string>();

  availableCompanies: string[] = [];
  activeCompany: string | null = null;
  private destroy$ = new Subject<void>();

  constructor(private activeCompanyService: ActiveCompanyService) {}

  ngOnInit(): void {
    // Suscribirse a los cambios de empresa activa
    this.activeCompanyService.activeCompany$
      .pipe(takeUntil(this.destroy$))
      .subscribe(company => {
        this.activeCompany = company;
      });

    // Suscribirse a las empresas disponibles
    this.activeCompanyService.availableCompanies$
      .pipe(takeUntil(this.destroy$))
      .subscribe(companies => {
        this.availableCompanies = companies;
        
        // Si no hay empresa activa pero hay empresas disponibles, seleccionar la primera
        if (!this.activeCompany && companies.length > 0) {
          this.selectCompany(companies[0]);
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onCompanyChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    const selectedCompany = target.value;
    
    if (selectedCompany && selectedCompany !== this.activeCompany) {
      this.selectCompany(selectedCompany);
    }
  }

  private selectCompany(companyName: string): void {
    this.activeCompanyService.setActiveCompany(companyName);
    this.companyChanged.emit(companyName);
  }

  get hasMultipleCompanies(): boolean {
    return this.availableCompanies.length > 1;
  }

  get shouldShowSelector(): boolean {
    return this.hasMultipleCompanies;
  }
}
