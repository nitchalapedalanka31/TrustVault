import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterModule],
  template: `
    <div class="error-container">
      <div class="card error-card">
        <h1>404 &bull; Resource Inaccessible or Not Found</h1>
        <p>The requested route or document could not be located. Under Zero-Trust security rules, unauthorized resources return 404 to conceal resource existence.</p>
        <button class="btn btn-primary mt-3" routerLink="/dashboard">Return to Dashboard</button>
      </div>
    </div>
  `,
  styles: [`
    .error-container { min-height: calc(100vh - 120px); display: flex; align-items: center; justify-content: center; padding: 2rem; }
    .error-card { max-width: 520px; text-align: center; padding: 3rem 2rem; }
    h1 { font-size: 1.375rem; margin-bottom: 0.75rem; color: var(--text-main); }
    p { font-size: 0.875rem; color: var(--text-muted); }
    .mt-3 { margin-top: 1.5rem; }
  `]
})
export class NotFoundComponent {}
