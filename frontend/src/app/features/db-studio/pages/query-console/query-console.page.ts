// src/app/features/db-studio/pages/query-console/query-console.page.ts
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DbStudioService, QueryPageDto } from '../../data/db-studio.service';
import { FormsModule } from '@angular/forms';

@Component({
  standalone: true,
  selector: 'app-db-query-console',
  imports: [CommonModule, FormsModule],
  templateUrl: './query-console.page.html',
  styleUrls: ['./query-console.page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class QueryConsolePage {
  private api = inject(DbStudioService);

  sql = signal<string>('select now() as server_time');
  paramsText = signal<string>('{}'); // JSON
  limit = signal<number>(100);
  offset = signal<number>(0);

  loading = signal(false);
  error = signal<string | null>(null);
  page = signal<QueryPageDto | null>(null);

  run() {
    this.loading.set(true);
    this.error.set(null);

    let params: Record<string, any> = {};
    try {
      params = JSON.parse(this.paramsText());
    } catch {
      this.loading.set(false);
      this.error.set('Parámetros no son JSON válido.');
      return;
    }

    this.api.runSelect({
      sql: this.sql(),
      params,
      limit: this.limit(),
      offset: this.offset()
    }).subscribe({
      next: p => { this.page.set(p); this.loading.set(false); },
      error: err => { this.error.set(err?.error?.message ?? err.message ?? 'Error'); this.loading.set(false); }
    });
  }

  next() { this.offset.set(this.offset() + this.limit()); this.run(); }
  prev() { this.offset.set(Math.max(0, this.offset() - this.limit())); this.run(); }

  keys(row: Record<string, unknown> | null | undefined) { return row ? Object.keys(row) : []; }
}
