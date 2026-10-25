import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/services/admin.service';
import { AdminStats, AuditEventItem, AuditVerificationResult, UserProfile, UserRole } from '../../core/models/models';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1 class="page-title">Administrative Security Center</h1>
          <p class="page-subtitle">Zero-Trust Telemetry, Identity Management & Tamper-Evident Chain Integrity</p>
        </div>
      </div>

      <!-- Tab Navigation -->
      <div class="tabs-nav">
        <button class="tab-btn" [class.active]="activeTab === 'overview'" (click)="activeTab = 'overview'">
          Security Overview
        </button>
        <button class="tab-btn" [class.active]="activeTab === 'users'" (click)="activeTab = 'users'">
          User Management
        </button>
        <button class="tab-btn" [class.active]="activeTab === 'audit'" (click)="activeTab = 'audit'">
          Audit Explorer
        </button>
        <button class="tab-btn" [class.active]="activeTab === 'chain'" (click)="activeTab = 'chain'">
          Hash-Chain Verifier
        </button>
      </div>

      <!-- Tab 1: Overview -->
      @if (activeTab === 'overview') {
        @if (stats) {
          <div class="stats-grid">
            <div class="stat-card">
              <div class="stat-val">{{ stats.totalUsers }}</div>
              <div class="stat-lbl">Registered Identities</div>
            </div>
            <div class="stat-card">
              <div class="stat-val">{{ stats.totalDocuments }}</div>
              <div class="stat-lbl">Active Documents</div>
            </div>
            <div class="stat-card">
              <div class="stat-val">{{ stats.totalActiveGrants }}</div>
              <div class="stat-lbl">Active Permission Grants</div>
            </div>
            <div class="stat-card">
              <div class="stat-val">{{ stats.totalAuditEvents }}</div>
              <div class="stat-lbl">Committed Audit Events</div>
            </div>
            <div class="stat-card danger">
              <div class="stat-val" style="color: #f87171;">{{ stats.totalAccessDenials }}</div>
              <div class="stat-lbl">Access Denials / Probes</div>
            </div>
            <div class="stat-card" [class.success]="stats.chainIntegrityValid">
              <div class="stat-val" [style.color]="stats.chainIntegrityValid ? '#34d399' : '#f87171'">
                {{ stats.chainIntegrityValid ? 'VALID' : 'CORRUPTED' }}
              </div>
              <div class="stat-lbl">Cryptographic Chain State</div>
            </div>
          </div>
        }

        <div class="card mt-3">
          <h3>Zero-Trust Admin Boundaries & Retention Invariants</h3>
          <ul class="policy-list">
            <li><strong>Content Segregation:</strong> Administrators manage identity and inspect security logs, but cannot view or download tenant document contents without explicit owner grants.</li>
            <li><strong>Tamper-Evident Ledger:</strong> Every authentication, read, upload, grant, denial, and status modification is cryptographically hashed with SHA-256 and chained to the previous record.</li>
            <li><strong>Authoritative Revocation:</strong> Revoking access or deactivating accounts invalidates sessions instantly upon database transaction commit.</li>
          </ul>
        </div>
      }

      <!-- Tab 2: User Management -->
      @if (activeTab === 'users') {
        <div class="user-layout">
          <!-- Create User Card -->
          <div class="card create-user-card">
            <h3>Provision New Identity</h3>
            @if (userCreateMessage) { <div class="alert alert-success">{{ userCreateMessage }}</div> }
            @if (userCreateError) { <div class="alert alert-danger">{{ userCreateError }}</div> }

            <form (ngSubmit)="createUser()">
              <div class="form-group">
                <label class="form-label">Full Name</label>
                <input type="text" class="form-input" [(ngModel)]="newUserName" name="name" required>
              </div>
              <div class="form-group">
                <label class="form-label">Email Address</label>
                <input type="email" class="form-input" [(ngModel)]="newUserEmail" name="email" required>
              </div>
              <div class="form-group">
                <label class="form-label">Password (Min 8 Chars, Upper, Lower, Special)</label>
                <input type="password" class="form-input" [(ngModel)]="newUserPassword" name="password" required>
              </div>
              <div class="form-group">
                <label class="form-label">Application Role</label>
                <select class="form-select" [(ngModel)]="newUserRole" name="role">
                  <option [ngValue]="3">Viewer (Access Shared Only)</option>
                  <option [ngValue]="2">Owner (Upload & Manage Grants)</option>
                  <option [ngValue]="1">Admin (Security Center & Users)</option>
                </select>
              </div>
              <button type="submit" class="btn btn-primary btn-block" [disabled]="creatingUser">
                {{ creatingUser ? 'Provisioning...' : 'Provision User' }}
              </button>
            </form>
          </div>

          <!-- Users Table -->
          <div class="card users-table-card">
            <h3>Registered Identities</h3>
            <div class="table-container mt-2">
              <table class="table">
                <thead>
                  <tr>
                    <th>User</th>
                    <th>Role</th>
                    <th>Status</th>
                    <th>Created</th>
                    <th style="text-align: right;">Action</th>
                  </tr>
                </thead>
                <tbody>
                  @for (u of users; track u.id) {
                    <tr>
                      <td>
                        <strong>{{ u.fullName }}</strong>
                        <div class="meta-hint">{{ u.email }}</div>
                      </td>
                      <td>
                        <span class="badge" [ngClass]="getRoleBadgeClass(u.role)">{{ getRoleName(u.role) }}</span>
                      </td>
                      <td>
                        <span class="badge" [class.badge-success]="u.isActive" [class.badge-danger]="!u.isActive">
                          {{ u.isActive ? 'Active' : 'Deactivated' }}
                        </span>
                      </td>
                      <td>{{ u.createdAtUtc | date:'shortDate' }}</td>
                      <td style="text-align: right;">
                        <button 
                          class="btn btn-sm" 
                          [class.btn-danger]="u.isActive" 
                          [class.btn-secondary]="!u.isActive"
                          (click)="toggleUserStatus(u)">
                          {{ u.isActive ? 'Deactivate' : 'Activate' }}
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Tab 3: Audit Explorer -->
      @if (activeTab === 'audit') {
        <div class="card filter-bar-audit mb-3">
          <input type="text" class="form-input" placeholder="Filter by Actor..." [(ngModel)]="auditActor" (keyup.enter)="loadAuditLogs()">
          <input type="text" class="form-input" placeholder="Filter by Action..." [(ngModel)]="auditAction" (keyup.enter)="loadAuditLogs()">
          <select class="form-select" [(ngModel)]="auditResult" (change)="loadAuditLogs()">
            <option value="">All Outcomes</option>
            <option value="SUCCESS">SUCCESS</option>
            <option value="DENIED">DENIED</option>
            <option value="FAILED">FAILED</option>
          </select>
          <button class="btn btn-secondary" (click)="loadAuditLogs()">Filter</button>
        </div>

        <div class="card">
          <div class="table-container">
            <table class="table">
              <thead>
                <tr>
                  <th>Seq #</th>
                  <th>Timestamp (UTC)</th>
                  <th>Actor</th>
                  <th>Action</th>
                  <th>Resource</th>
                  <th>Outcome</th>
                  <th>Chain Hash (SHA-256)</th>
                </tr>
              </thead>
              <tbody>
                @for (ev of auditLogs; track ev.id) {
                  <tr>
                    <td><strong>#{{ ev.id }}</strong></td>
                    <td>{{ ev.timestampUtc | date:'MM/dd HH:mm:ss' }}</td>
                    <td><code>{{ ev.actorId.substring(0, 8) }}...</code></td>
                    <td><span class="action-tag">{{ ev.action }}</span></td>
                    <td><code class="meta-hint">{{ ev.resourceId ? ev.resourceId.substring(0, 8) + '...' : '-' }}</code></td>
                    <td>
                      <span class="badge" [class.badge-success]="ev.result === 'SUCCESS'" [class.badge-danger]="ev.result === 'DENIED' || ev.result === 'FAILED'">
                        {{ ev.result }}
                      </span>
                    </td>
                    <td>
                      <code class="hash-tag" [title]="'Current: ' + ev.currentHash + '\nPrevious: ' + ev.previousHash">
                        {{ ev.currentHash.substring(0, 10) }}...
                      </code>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Tab 4: Chain Verifier -->
      @if (activeTab === 'chain') {
        <div class="card text-center py-4">
          <h2>Cryptographic Hash-Chain Verification</h2>
          <p class="subtitle max-w-md">Traverse all sequential ledger entries, recalculate deterministic canonical hashes, and verify backward-pointer integrity.</p>

          <button class="btn btn-primary btn-lg mt-3" (click)="verifyChain()" [disabled]="verifyingChain">
            {{ verifyingChain ? 'Verifying Entire Cryptographic Chain...' : 'Run Audit Integrity Verification' }}
          </button>

          @if (verificationResult) {
            <div class="verification-report card mt-4" [class.valid]="verificationResult.isValid" [class.invalid]="!verificationResult.isValid">
              <div class="report-badge">
                {{ verificationResult.isValid ? 'CRYPTO INTEGRITY VERIFIED: OK' : 'INTEGRITY COMPROMISED: TAMPERING DETECTED' }}
              </div>
              <div class="report-details">
                <p><strong>Total Sequential Events Evaluated:</strong> {{ verificationResult.totalEventsChecked }}</p>
                <p><strong>Verification Timestamp:</strong> {{ verificationResult.checkedAtUtc | date:'medium' }}</p>
                @if (!verificationResult.isValid) {
                  <p class="text-danger"><strong>Corrupted Event ID:</strong> #{{ verificationResult.corruptedEventId }}</p>
                  <p class="text-danger"><strong>Diagnostic Error:</strong> {{ verificationResult.errorMessage }}</p>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .page-container { max-width: 1300px; margin: 0 auto; padding: 2rem 1.5rem; }
    .page-header { margin-bottom: 1.5rem; }
    .page-title { font-size: 1.75rem; font-weight: 700; color: var(--text-main); }
    .page-subtitle { font-size: 0.875rem; color: var(--text-muted); margin-top: 0.25rem; }
    
    .tabs-nav { display: flex; gap: 0.5rem; border-bottom: 1px solid var(--border-color); margin-bottom: 1.5rem; }
    .tab-btn { background: none; border: none; padding: 0.75rem 1.25rem; color: var(--text-muted); font-size: 0.9375rem; font-weight: 600; cursor: pointer; border-bottom: 2px solid transparent; }
    .tab-btn:hover { color: var(--text-main); }
    .tab-btn.active { color: var(--primary); border-bottom-color: var(--primary); }

    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; }
    .stat-card { background: var(--bg-card); border: 1px solid var(--border-color); border-radius: var(--radius); padding: 1.25rem; }
    .stat-val { font-size: 1.5rem; font-weight: 700; color: var(--text-main); }
    .stat-lbl { font-size: 0.75rem; color: var(--text-muted); text-transform: uppercase; margin-top: 0.25rem; }
    
    .policy-list { margin-top: 1rem; padding-left: 1.25rem; display: flex; flex-direction: column; gap: 0.5rem; color: var(--text-muted); font-size: 0.875rem; }
    .policy-list li strong { color: var(--text-main); }

    .user-layout { display: grid; grid-template-columns: 360px 1fr; gap: 1.5rem; }
    .btn-block { width: 100%; margin-top: 0.5rem; }
    .meta-hint { font-size: 0.75rem; color: var(--text-dim); }
    .filter-bar-audit { display: flex; gap: 0.75rem; }
    .action-tag { font-family: monospace; font-size: 0.8125rem; background: var(--bg-input); padding: 0.2rem 0.4rem; border-radius: 4px; }
    .hash-tag { font-family: monospace; font-size: 0.75rem; color: #34d399; cursor: pointer; }

    .text-center { text-align: center; }
    .py-4 { padding: 3rem 1.5rem; }
    .mt-3 { margin-top: 1rem; }
    .mt-4 { margin-top: 1.5rem; }
    .mb-3 { margin-bottom: 1rem; }
    .btn-lg { padding: 0.875rem 2rem; font-size: 1rem; }
    .max-w-md { max-width: 600px; margin: 0.5rem auto; }
    
    .verification-report { max-width: 700px; margin: 1.5rem auto 0; padding: 1.5rem; text-align: left; }
    .verification-report.valid { border-color: rgba(16, 185, 129, 0.4); background: rgba(16, 185, 129, 0.05); }
    .verification-report.invalid { border-color: rgba(239, 68, 68, 0.4); background: rgba(239, 68, 68, 0.05); }
    .report-badge { font-weight: 700; font-size: 1rem; margin-bottom: 1rem; color: #34d399; }
    .verification-report.invalid .report-badge { color: #f87171; }
    .report-details { display: flex; flex-direction: column; gap: 0.375rem; font-size: 0.875rem; }
    .text-danger { color: #f87171; }
  `]
})
export class AdminDashboardComponent implements OnInit {
  public activeTab: 'overview' | 'users' | 'audit' | 'chain' = 'overview';
  public stats: AdminStats | null = null;

  // Users
  public users: UserProfile[] = [];
  public newUserName = '';
  public newUserEmail = '';
  public newUserPassword = '';
  public newUserRole: UserRole = UserRole.Viewer;
  public creatingUser = false;
  public userCreateMessage = '';
  public userCreateError = '';

  // Audit Explorer
  public auditLogs: AuditEventItem[] = [];
  public auditActor = '';
  public auditAction = '';
  public auditResult = '';

  // Verifier
  public verifyingChain = false;
  public verificationResult: AuditVerificationResult | null = null;

  constructor(private adminService: AdminService) {}

  ngOnInit(): void {
    this.loadStats();
    this.loadUsers();
    this.loadAuditLogs();
  }

  public loadStats(): void {
    this.adminService.getStats().subscribe({
      next: (res) => this.stats = res
    });
  }

  public loadUsers(): void {
    this.adminService.getUsers().subscribe({
      next: (res) => this.users = res
    });
  }

  public createUser(): void {
    this.creatingUser = true;
    this.userCreateError = '';
    this.userCreateMessage = '';

    this.adminService.createUser({
      fullName: this.newUserName,
      email: this.newUserEmail,
      password: this.newUserPassword,
      role: this.newUserRole
    }).subscribe({
      next: () => {
        this.creatingUser = false;
        this.userCreateMessage = 'User provisioned successfully.';
        this.newUserName = '';
        this.newUserEmail = '';
        this.newUserPassword = '';
        this.loadUsers();
        this.loadStats();
      },
      error: (err) => {
        this.creatingUser = false;
        this.userCreateError = err.error?.message || 'Provisioning failed.';
      }
    });
  }

  public toggleUserStatus(user: UserProfile): void {
    const newStatus = !user.isActive;
    this.adminService.updateUserStatus(user.id, newStatus).subscribe({
      next: () => {
        user.isActive = newStatus;
        this.loadStats();
      },
      error: (err) => {
        alert(err.error?.message || 'Failed to update user status.');
      }
    });
  }

  public loadAuditLogs(): void {
    this.adminService.getAuditLogs({
      actorId: this.auditActor,
      action: this.auditAction,
      result: this.auditResult,
      pageSize: 50
    }).subscribe({
      next: (res) => this.auditLogs = res.items
    });
  }

  public verifyChain(): void {
    this.verifyingChain = true;
    this.adminService.verifyAuditIntegrity().subscribe({
      next: (res) => {
        this.verificationResult = res;
        this.verifyingChain = false;
        this.loadStats();
      },
      error: () => {
        this.verifyingChain = false;
      }
    });
  }

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
      default: return 'badge-neutral';
    }
  }
}
