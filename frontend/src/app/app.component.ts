// src/app/app.component.ts
import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, Router } from '@angular/router';
import { VersionCheckService } from './core/services/version-check.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  router = inject(Router);
  private versionCheckService = inject(VersionCheckService);

  ngOnInit(): void {
    // Start checking for application updates
    // This will periodically check if a new version has been deployed
    // and force a reload if detected
    this.versionCheckService.startVersionChecking();
  }

  ngOnDestroy(): void {
    // Stop version checking when component is destroyed
    this.versionCheckService.stopVersionChecking();
  }
}
