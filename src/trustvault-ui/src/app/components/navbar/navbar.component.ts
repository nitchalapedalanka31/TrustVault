import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { UserRole } from '../../core/models/models';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <header class="navbar">
      <div class="nav-container">
        <div class="nav-brand" routerLink="/dashboard">
          <svg class="brand-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
            <path d="M9 12l2 2 4-4"/>
          </svg>
          <span class="brand-title">TrustVault</span>
          <span class="brand-badge">Zero-Trust</span>
        </div>

        @if (authService.isAuthenticated()) {
          <nav class="nav-links">
            <a routerLink="/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{exact: true}">Dashboard</a>
            @if (authService.isOwner() || authService.isAdmin()) {
              <a routerLink="/my-documents" routerLinkActive="active">My Documents</a>
            }
            <a routerLink="/shared-with-me" routerLinkActive="active">Shared With Me</a>
            @if (authService.isAdmin()) {
              <a routerLink="/admin" routerLinkActive="active" class="admin-link">Security Center</a>
            }
          </nav>

          <div class="user-menu">
            <div class="user-info">
              <span class="user-name">{{ authService.currentUser()?.fullName }}</span>
              <span class="badge" [ngClass]="getRoleBadgeClass(authService.currentUser()?.role)">
                {{ getRoleName(authService.currentUser()?.role) }}
              </span>
            </div>
            <button class="btn btn-secondary btn-sm" (click)="onLogout()">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/>
                <polyline points="16 17 21 12 16 7"/>
                <line x1="21" y1="12" x2="9" y2="12"/>
              </svg>
              Logout
            </button>
          </div>
        }
      </div>
    </header>
  `,
  styles: [`
    .navbar {
      background-color: var(--bg-card);
      border-bottom: 1px solid var(--border-color);
      height: 64px;
      display: flex;
      align-items: center;
      position: sticky;
      top: 0;
      z-index: 100;
    }
    .nav-container {
      width: 100%;
      max-width: 1300px;
      margin: 0 auto;
      padding: 0 1.5rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .nav-brand {
      display: flex;
      align-items: center;
      gap: 0.625rem;
      cursor: pointer;
      text-decoration: none;
    }
    .brand-icon {
      width: 26px;
      height: 26px;
      color: var(--primary);
    }
    .brand-title {
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--text-main);
      letter-spacing: -0.025em;
    }
    .brand-badge {
      font-size: 0.6875rem;
      font-weight: 700;
      background-color: var(--primary-light);
      color: #818cf8;
      padding: 0.125rem 0.5rem;
      border-radius: 9999px;
      text-transform: uppercase;
    }
    .nav-links {
      display: flex;
      gap: 1.5rem;
    }
    .nav-links a {
      color: var(--text-muted);
      font-size: 0.875rem;
      font-weight: 500;
      padding: 0.5rem 0;
      position: relative;
    }
    .nav-links a:hover, .nav-links a.active {
      color: var(--text-main);
      text-decoration: none;
    }
    .nav-links a.active::after {
      content: '';
      position: absolute;
      bottom: -13px;
      left: 0;
      right: 0;
      height: 2px;
      background-color: var(--primary);
      border-radius: 2px;
    }
    .admin-link {
      color: #f59e0b !important;
    }
    .user-menu {
      display: flex;
      align-items: center;
      gap: 1.25rem;
    }
    .user-info {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .user-name {
      font-size: 0.875rem;
      font-weight: 600;
      color: var(--text-main);
    }
  `]
})
export class NavbarComponent {
  constructor(public authService: AuthService, private router: Router) {}

  public getRoleName(role?: UserRole): string {
    switch (role) {
      case UserRole.Admin: return 'Admin';
      case UserRole.Owner: return 'Owner';
      case UserRole.Viewer: return 'Viewer';
      default: return '';
    }
  }

  public getRoleBadgeClass(role?: UserRole): string {
    switch (role) {
      case UserRole.Admin: return 'badge-warning';
      case UserRole.Owner: return 'badge-primary';
      case UserRole.Viewer: return 'badge-neutral';
      default: return 'badge-neutral';
    }
  }

  public onLogout(): void {
    this.authService.logout().subscribe(() => {
      this.router.navigate(['/login']);
    });
  }
}
