import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize, Subject, takeUntil } from 'rxjs';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { FaIconLibrary } from '@fortawesome/angular-fontawesome';
import { faArrowLeft, faPen, faSpinner } from '@fortawesome/free-solid-svg-icons';

import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { GuiaGeneticaAdminService, ProduccionAvicolaRawDto } from '../guia-genetica-admin.service';

@Component({
  selector: 'app-guia-genetica-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FontAwesomeModule, SidebarComponent],
  templateUrl: './guia-genetica-detail.component.html',
  styleUrls: ['./guia-genetica-detail.component.scss']
})
export class GuiaGeneticaDetailComponent implements OnInit, OnDestroy {
  faBack = faArrowLeft;
  faPen = faPen;
  faSpinner = faSpinner;

  loading = false;
  error: string | null = null;
  item: ProduccionAvicolaRawDto | null = null;

  private destroy$ = new Subject<void>();

  constructor(
    private svc: GuiaGeneticaAdminService,
    private route: ActivatedRoute,
    private router: Router,
    library: FaIconLibrary
  ) {
    library.addIcons(faArrowLeft, faPen, faSpinner);
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading = true;
    this.svc.getById(id)
      .pipe(finalize(() => (this.loading = false)), takeUntil(this.destroy$))
      .subscribe({
        next: (dto) => (this.item = dto),
        error: (err) => {
          console.error(err);
          this.error = 'No se pudo cargar el detalle.';
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  back(): void {
    this.router.navigate(['/config/guia-genetica']);
  }

  edit(): void {
    if (!this.item) return;
    this.router.navigate(['/config/guia-genetica', this.item.id, 'edit']);
  }
}

