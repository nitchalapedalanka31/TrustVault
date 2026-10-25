import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-wrapper">
      <div class="login-card">
        <div class="login-header">
          <div class="brand-badge-large">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
              <path d="M9 12l2 2 4-4"/>
            </svg>
          </div>
          <h2>TrustVault</h2>
          <p class="subtitle">Zero-Trust Enterprise Document Sharing</p>
        </div>

        @if (errorMessage) {
          <div class="alert alert-danger">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"/>
              <line x1="12" y1="8" x2="12" y2="12"/>
              <line x1="12" y1="16" x2="12.01" y2="16"/>
            </svg>
            <span>{{ errorMessage }}</span>
          </div>
        }

        <form (ngSubmit)="onLogin()" class="login-form">
          <div class="form-group">
            <label class="form-label">Work Email</label>
            <input 
              type="email" 
              class="form-input" 
              [(ngModel)]="email" 
              name="email" 
              placeholder="user@trustvault.local" 
              required
              autocomplete="username">
          </div>

          <div class="form-group">
            <label class="form-label">Password</label>
            <input 
              type="password" 
              class="form-input" 
              [(ngModel)]="password" 
              name="password" 
              placeholder="••••••••••••" 
              required
              autocomplete="current-password">
          </div>

          <button type="submit" class="btn btn-primary btn-block" [disabled]="loading">
            @if (loading) {
              <span>Authenticating...</span>
            } @else {
              <span>Sign In to Vault</span>
            }
          </button>
        </form>

        <div class="demo-section">
          <p class="demo-title">Quick Demo Sign-In (Select Role)</p>
          <div class="demo-grid">
            <button class="demo-pill admin" (click)="setCredentials('admin@trustvault.local', 'Admin@TrustVault2026!')">
              <span class="role">Admin</span>
              <span class="name">System Admin</span>
            </button>
            <button class="demo-pill owner" (click)="setCredentials('alice@trustvault.local', 'Alice@TrustVault2026!')">
              <span class="role">Owner</span>
              <span class="name">Alice Morgan</span>
            </button>
            <button class="demo-pill viewer" (click)="setCredentials('bob@trustvault.local', 'Bob@TrustVault2026!')">
              <span class="role">Viewer</span>
              <span class="name">Bob Jenkins</span>
            </button>
            <button class="demo-pill viewer" (click)="setCredentials('charlie@trustvault.local', 'Charlie@TrustVault2026!')">
              <span class="role">Viewer</span>
              <span class="name">Charlie Davis</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-wrapper {
      min-height: calc(100vh - 64px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2rem 1rem;
    }
    .login-card {
      background-color: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: 12px;
      width: 100%;
      max-width: 440px;
      padding: 2.25rem;
      box-shadow: var(--shadow-lg);
    }
    .login-header {
      text-align: center;
      margin-bottom: 2rem;
    }
    .brand-badge-large {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 52px;
      height: 52px;
      background: var(--primary-light);
      color: var(--primary);
      border-radius: 12px;
      margin-bottom: 1rem;
    }
    .brand-badge-large svg {
      width: 30px;
      height: 30px;
    }
    .login-header h2 {
      font-size: 1.5rem;
      font-weight: 700;
      color: var(--text-main);
    }
    .subtitle {
      font-size: 0.875rem;
      color: var(--text-muted);
      margin-top: 0.25rem;
    }
    .btn-block {
      width: 100%;
      padding: 0.75rem;
      margin-top: 0.5rem;
    }
    .demo-section {
      margin-top: 2rem;
      padding-top: 1.5rem;
      border-top: 1px solid var(--border-subtle);
    }
    .demo-title {
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--text-dim);
      margin-bottom: 0.75rem;
      text-align: center;
    }
    .demo-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.625rem;
    }
    .demo-pill {
      background-color: var(--bg-input);
      border: 1px solid var(--border-color);
      border-radius: var(--radius);
      padding: 0.625rem;
      cursor: pointer;
      text-align: left;
      transition: all 0.15s ease;
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
    }
    .demo-pill:hover {
      border-color: var(--primary);
      background-color: var(--bg-card-hover);
    }
    .demo-pill .role {
      font-size: 0.6875rem;
      font-weight: 700;
      text-transform: uppercase;
    }
    .demo-pill .name {
      font-size: 0.8125rem;
      color: var(--text-main);
      font-weight: 500;
    }
    .demo-pill.admin .role { color: #fbbf24; }
    .demo-pill.owner .role { color: #818cf8; }
    .demo-pill.viewer .role { color: #94a3b8; }
  `]
})
export class LoginComponent {
  public email = '';
  public password = '';
  public loading = false;
  public errorMessage = '';

  constructor(private authService: AuthService, private router: Router) {}

  public setCredentials(email: string, pass: string): void {
    this.email = email;
    this.password = pass;
    this.onLogin();
  }

  public onLogin(): void {
    if (!this.email || !this.password) {
      this.errorMessage = 'Please enter email and password.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.message || 'Authentication failed. Please verify credentials.';
      }
    });
  }
}
