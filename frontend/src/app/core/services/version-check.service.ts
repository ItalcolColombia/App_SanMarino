import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { interval, Observable, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';

/**
 * Service to check for application updates and force reload when a new version is detected.
 * 
 * This service periodically checks if a new version of the application has been deployed
 * by fetching the index.html file with cache-busting. When a new version is detected,
 * it forces a page reload to ensure users are using the latest version.
 * 
 * This solves the problem where users with active sessions don't get updates after
 * a frontend deployment, causing communication issues with the backend.
 */
@Injectable({
  providedIn: 'root'
})
export class VersionCheckService {
  private readonly CHECK_INTERVAL = 5 * 60 * 1000; // Check every 5 minutes
  private readonly INDEX_HTML_PATH = '/index.html';
  private currentVersion: string | null = null;
  private checkInterval: any;

  constructor(private http: HttpClient) {
    // Get initial version from the current index.html
    this.getCurrentVersion().then(version => {
      this.currentVersion = version;
    });
  }

  /**
   * Starts periodic version checking
   */
  startVersionChecking(): void {
    // Check immediately on start
    this.checkVersion();

    // Then check periodically
    this.checkInterval = setInterval(() => {
      this.checkVersion();
    }, this.CHECK_INTERVAL);
  }

  /**
   * Stops periodic version checking
   */
  stopVersionChecking(): void {
    if (this.checkInterval) {
      clearInterval(this.checkInterval);
      this.checkInterval = null;
    }
  }

  /**
   * Checks if a new version is available by comparing the current index.html
   * with a fresh fetch (using cache-busting)
   */
  private checkVersion(): void {
    // Fetch index.html with cache-busting to get the latest version
    const cacheBuster = `?v=${Date.now()}`;
    
    this.http.get(this.INDEX_HTML_PATH + cacheBuster, { 
      responseType: 'text',
      headers: {
        'Cache-Control': 'no-cache',
        'Pragma': 'no-cache'
      }
    }).pipe(
      map(html => this.extractVersionFromHtml(html)),
      catchError(error => {
        console.warn('Version check failed:', error);
        return of(null);
      })
    ).subscribe(newVersion => {
      if (newVersion && this.currentVersion && newVersion !== this.currentVersion) {
        console.log('New version detected! Current:', this.currentVersion, 'New:', newVersion);
        this.handleNewVersion();
      } else if (newVersion && !this.currentVersion) {
        // First time, just store the version
        this.currentVersion = newVersion;
      }
    });
  }

  /**
   * Extracts version/build timestamp from HTML
   * Looks for a meta tag or script tag with version info
   */
  private extractVersionFromHtml(html: string): string {
    // Try to extract from meta tag first
    const metaMatch = html.match(/<meta\s+name="app-version"\s+content="([^"]+)"/i);
    if (metaMatch) {
      return metaMatch[1];
    }

    // Fallback: use a hash of the main script references as version identifier
    // This works because Angular generates hashed filenames for JS/CSS files
    const scriptMatches = html.match(/<script[^>]+src="([^"]+\.js[^"]*)"/gi);
    if (scriptMatches && scriptMatches.length > 0) {
      // Create a simple hash from script URLs
      const scriptUrls = scriptMatches.map(m => {
        const srcMatch = m.match(/src="([^"]+)"/);
        return srcMatch ? srcMatch[1] : '';
      }).join('|');
      return this.simpleHash(scriptUrls);
    }

    // Last resort: use a hash of the entire HTML (less efficient but works)
    return this.simpleHash(html.substring(0, 1000)); // First 1000 chars should be enough
  }

  /**
   * Simple hash function for version comparison
   */
  private simpleHash(str: string): string {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      const char = str.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash; // Convert to 32-bit integer
    }
    return hash.toString(36);
  }

  /**
   * Gets the current version from the loaded index.html
   */
  private async getCurrentVersion(): Promise<string> {
    try {
      const html = document.documentElement.outerHTML;
      return this.extractVersionFromHtml(html);
    } catch (error) {
      console.warn('Could not get current version:', error);
      return '';
    }
  }

  /**
   * Handles detection of a new version
   * Forces a full page reload to get the new version
   */
  private handleNewVersion(): void {
    // Stop checking to avoid multiple reloads
    this.stopVersionChecking();

    // Show a message to the user (optional - you can customize this)
    const message = 'Una nueva versión de la aplicación está disponible. La página se recargará automáticamente...';
    
    // Try to show a notification if you have a toast service
    // For now, we'll just log and reload
    
    // Give a brief moment for any cleanup, then reload
    setTimeout(() => {
      // Force a hard reload to bypass cache
      window.location.reload();
    }, 1000);
  }

  /**
   * Manually check for updates (useful for testing or manual refresh)
   */
  checkForUpdates(): Observable<boolean> {
    const cacheBuster = `?v=${Date.now()}`;
    
    return this.http.get(this.INDEX_HTML_PATH + cacheBuster, { 
      responseType: 'text',
      headers: {
        'Cache-Control': 'no-cache',
        'Pragma': 'no-cache'
      }
    }).pipe(
      map(html => {
        const newVersion = this.extractVersionFromHtml(html);
        if (newVersion && this.currentVersion && newVersion !== this.currentVersion) {
          this.handleNewVersion();
          return true;
        }
        return false;
      }),
      catchError(() => of(false))
    );
  }
}

