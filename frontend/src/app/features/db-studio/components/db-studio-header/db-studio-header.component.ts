// src/app/features/db-studio/components/db-studio-header/db-studio-header.component.ts
import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { 
  faDatabase, faRefresh, faChartBar, faCog, faQuestionCircle,
  faDownload, faUpload, faTrash, faPlus, faSearch
} from '@fortawesome/free-solid-svg-icons';

export interface DbStudioHeaderEvent {
  type: 'refresh' | 'analyze' | 'export' | 'import' | 'settings' | 'help';
  data?: any;
}

@Component({
  selector: 'app-db-studio-header',
  standalone: true,
  imports: [CommonModule, FontAwesomeModule],
  templateUrl: './db-studio-header.component.html',
  styleUrls: ['./db-studio-header.component.scss']
})
export class DbStudioHeaderComponent {
  @Input() loading: boolean = false;
  @Input() error: string | null = null;
  @Input() databaseName: string = 'PostgreSQL';
  @Input() connectionStatus: 'connected' | 'disconnected' | 'connecting' = 'connected';

  @Output() action = new EventEmitter<DbStudioHeaderEvent>();

  // Iconos
  faDatabase = faDatabase;
  faRefresh = faRefresh;
  faChartBar = faChartBar;
  faCog = faCog;
  faQuestionCircle = faQuestionCircle;
  faDownload = faDownload;
  faUpload = faUpload;
  faTrash = faTrash;
  faPlus = faPlus;
  faSearch = faSearch;

  onRefresh(): void {
    this.action.emit({ type: 'refresh' });
  }

  onAnalyze(): void {
    this.action.emit({ type: 'analyze' });
  }

  onExport(): void {
    this.action.emit({ type: 'export' });
  }

  onImport(): void {
    this.action.emit({ type: 'import' });
  }

  onSettings(): void {
    this.action.emit({ type: 'settings' });
  }

  onHelp(): void {
    this.action.emit({ type: 'help' });
  }

  onClearError(): void {
    this.error = null;
  }

  get connectionStatusClass(): string {
    switch (this.connectionStatus) {
      case 'connected': return 'status-connected';
      case 'connecting': return 'status-connecting';
      case 'disconnected': return 'status-disconnected';
      default: return 'status-unknown';
    }
  }

  get connectionStatusText(): string {
    switch (this.connectionStatus) {
      case 'connected': return 'Conectado';
      case 'connecting': return 'Conectando...';
      case 'disconnected': return 'Desconectado';
      default: return 'Desconocido';
    }
  }
}
