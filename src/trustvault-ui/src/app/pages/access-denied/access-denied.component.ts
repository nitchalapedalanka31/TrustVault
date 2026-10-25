import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [RouterModule],
  template: `
    <div class="error-container">
      <div class="card error-card">
        <div class="icon-danger">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/>
            <line x1="12" y1="8" x2="12" y2="12"/>
            <line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
        </div>
        <h1>403 &bull; Authorization Boundary Exceeded</h1>
        <p>You do not have the required permissions or active grant to perform this action or view this resource.</p>
        <p class="audit-note">This access attempt has been logged in the tamper-evident audit ledger.</p>
        <button class="btn btn-primary mt-3" routerLink="/dashboard">Return to Dashboard</button>
      </div>
    </div>
  `,
  styles: [`
    .error-container { min-height: calc(100vh - 120px); display: flex; align-items: center; justify-content: center; padding: 2rem; }
    .error-card { max-width: 520px; text-align: center; padding: 3rem 2rem; }
    .icon-danger { color: #f87171; margin-bottom: 1.25rem; }
    h1 { font-size: 1.375rem; margin-bottom: 0.75rem; color: var(--text-main); }
    p { font-size: 0.875rem; color: var(--text-muted); }
    .audit-note { font-size: 0.75rem; color: var(--text-dim); margin-top: 0.5rem; font-style: italic; }
    .mt-3 { margin-top: 1.5rem; }
  `]
})
export class AccessDeniedComponent {}
