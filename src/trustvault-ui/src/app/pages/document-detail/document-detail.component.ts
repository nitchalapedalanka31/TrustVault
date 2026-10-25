import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DocumentService } from '../../core/services/document.service';
import { GrantService } from '../../core/services/grant.service';
import { DocumentDetail, DocumentGrant, DocumentPermission, RecipientSuggestion } from '../../core/models/models';

@Component({
  selector: 'app-document-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-container">
      @if (loading) {
        <div class="state-container">
          <div class="spinner"></div>
          <p>Authorizing and loading document...</p>
        </div>
      } @else if (errorMessage) {
        <div class="alert alert-danger">{{ errorMessage }}</div>
        <button class="btn btn-secondary" routerLink="/dashboard">Return to Dashboard</button>
      } @else if (document) {
        <!-- Detail Header -->
        <div class="detail-header card">
          <div class="header-main">
            <div class="badge-row">
              <span class="category-pill">{{ document.category }}</span>
              <span class="version-pill">Version {{ document.currentVersionNumber }}</span>
              @if (document.isOwner) {
                <span class="badge badge-primary">Owner Controls Active</span>
              } @else if (document.permissionLevel === 2) {
                <span class="badge badge-success">EDIT Grant</span>
              } @else {
                <span class="badge badge-neutral">READ Grant</span>
              }
            </div>
            <h1 class="doc-title-large">{{ document.title }}</h1>
            <p class="doc-filename-meta">Original File: <strong>{{ document.originalFileName }}</strong> &bull; Size: {{ formatBytes(document.fileSizeBytes) }}</p>
          </div>

          <div class="header-action-buttons">
            <button class="btn btn-primary" (click)="download(document.currentVersionNumber)">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
                <polyline points="7 10 12 15 17 10"/>
                <line x1="12" y1="15" x2="12" y2="3"/>
              </svg>
              Download File
            </button>

            @if (document.isOwner || document.permissionLevel === 2) {
              <button class="btn btn-secondary" (click)="openNewVersionModal()">
                Upload New Version
              </button>
            }

            @if (document.isOwner) {
              <button class="btn btn-danger btn-sm" (click)="deleteDocument()">
                Delete
              </button>
            }
          </div>
        </div>

        <!-- Metadata & Integrity Hash -->
        <div class="meta-grid">
          <div class="card">
            <h3>Cryptographic Integrity (SHA-256)</h3>
            <div class="hash-box">
              <code>{{ document.sha256Hash }}</code>
            </div>
            <p class="hash-hint">Hash is calculated in-flight during upload and verified against tamper records.</p>
          </div>

          <div class="card">
            <h3>Document Details</h3>
            <div class="meta-list">
              <div class="meta-row">
                <span class="meta-key">Document ID:</span>
                <span class="meta-val monospace">{{ document.id }}</span>
              </div>
              <div class="meta-row">
                <span class="meta-key">Document Owner:</span>
                <span class="meta-val">{{ document.ownerName }}</span>
              </div>
              <div class="meta-row">
                <span class="meta-key">Created:</span>
                <span class="meta-val">{{ document.createdAtUtc | date:'medium' }}</span>
              </div>
              <div class="meta-row">
                <span class="meta-key">Last Modified:</span>
                <span class="meta-val">{{ document.updatedAtUtc | date:'medium' }}</span>
              </div>
              <div class="meta-row">
                <span class="meta-key">Tags:</span>
                <span class="meta-val">{{ document.tags || 'None' }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Versions Timeline -->
        <div class="card section-card">
          <div class="section-header">
            <h3>Immutable Version History</h3>
            <span class="count-badge">{{ document.versions.length }} versions</span>
          </div>

          <div class="table-container">
            <table class="table">
              <thead>
                <tr>
                  <th>Version</th>
                  <th>Uploaded By</th>
                  <th>Size</th>
                  <th>MIME Type</th>
                  <th>Hash</th>
                  <th>Timestamp</th>
                  <th style="text-align: right;">Action</th>
                </tr>
              </thead>
              <tbody>
                @for (v of document.versions; track v.id) {
                  <tr>
                    <td><strong>v{{ v.versionNumber }}</strong></td>
                    <td>{{ v.uploadedByName }}</td>
                    <td>{{ formatBytes(v.fileSizeBytes) }}</td>
                    <td><code>{{ v.contentType }}</code></td>
                    <td><code>{{ v.sha256Hash.substring(0, 12) }}...</code></td>
                    <td>{{ v.createdAtUtc | date:'short' }}</td>
                    <td style="text-align: right;">
                      <button class="btn btn-secondary btn-sm" (click)="download(v.versionNumber)">
                        Download v{{ v.versionNumber }}
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Permission Management (Document Owner Only) -->
        @if (document.isOwner) {
          <div class="card section-card">
            <div class="section-header">
              <div>
                <h3>Access Grants & Revocation Management</h3>
                <p class="section-subtitle">Explicit per-document grants. Revoking access immediately blocks subsequent downloads.</p>
              </div>
              <button class="btn btn-secondary btn-sm" (click)="loadSuggestions()">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"/>
                </svg>
                AI Recipient Suggestions
              </button>
            </div>

            <!-- Grant Creation Form -->
            <form (ngSubmit)="createGrant()" class="grant-form">
              <div class="grant-inputs">
                <input 
                  type="email" 
                  class="form-input" 
                  placeholder="Colleague Email (e.g. bob@trustvault.local)" 
                  [(ngModel)]="newGrantEmail" 
                  name="grantEmail" 
                  required>

                <select class="form-select permission-select" [(ngModel)]="newGrantPermission" name="grantPerm">
                  <option [ngValue]="1">READ (View & Download)</option>
                  <option [ngValue]="2">EDIT (Read + Edit & Versions)</option>
                </select>

                <button type="submit" class="btn btn-primary" [disabled]="grantingAccess || !newGrantEmail">
                  {{ grantingAccess ? 'Granting...' : 'Grant Access' }}
                </button>
              </div>
              @if (grantMessage) {
                <div class="alert alert-success mt-2">{{ grantMessage }}</div>
              }
              @if (grantError) {
                <div class="alert alert-danger mt-2">{{ grantError }}</div>
              }
            </form>

            <!-- Grants Table -->
            @if (document.grants.length === 0) {
              <div class="empty-grants">
                <p>No external grants active. Only you (the owner) can access this document.</p>
              </div>
            } @else {
              <div class="table-container">
                <table class="table">
                  <thead>
                    <tr>
                      <th>Recipient Name & Email</th>
                      <th>Permission</th>
                      <th>Granted Date</th>
                      <th>Status</th>
                      <th style="text-align: right;">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (grant of document.grants; track grant.id) {
                      <tr>
                        <td>
                          <strong>{{ grant.userFullName }}</strong>
                          <div class="meta-hint">{{ grant.userEmail }}</div>
                        </td>
                        <td>
                          @if (grant.permission === 2) {
                            <span class="badge badge-success">EDIT</span>
                          } @else {
                            <span class="badge badge-neutral">READ</span>
                          }
                        </td>
                        <td>{{ grant.grantedAtUtc | date:'short' }}</td>
                        <td>
                          <span class="badge badge-success">Active</span>
                        </td>
                        <td style="text-align: right;">
                          <button class="btn btn-danger btn-sm" (click)="revokeGrant(grant.id)">
                            Immediate Revoke
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>
        }

        <!-- AI Suggestions Modal -->
        @if (showSuggestionsModal) {
          <div class="modal-overlay">
            <div class="modal-content">
              <div class="modal-header">
                <h3>Contextual Recipient Suggestions</h3>
                <button class="modal-close" (click)="showSuggestionsModal = false">×</button>
              </div>

              <div class="alert alert-warning">
                <strong>Zero-Trust Rule:</strong> Recommendations never grant access automatically. An explicit human owner confirmation is required.
              </div>

              @if (suggestionsLoading) {
                <p>Analyzing collaboration context and category permissions...</p>
              } @else if (suggestions.length === 0) {
                <p>No additional candidate colleagues found for suggestion.</p>
              } @else {
                <div class="suggestions-list">
                  @for (sug of suggestions; track sug.userId) {
                    <div class="suggestion-item">
                      <div class="suggestion-main">
                        <div class="sug-name">{{ sug.fullName }} ({{ sug.email }})</div>
                        <div class="sug-reason">{{ sug.reason }}</div>
                        <div class="sug-meta">Confidence: {{ (sug.confidenceScore * 100).toFixed(0) }}% &bull; Engine: {{ sug.sourceModel }}</div>
                      </div>
                      <button class="btn btn-primary btn-sm" (click)="applySuggestion(sug)">
                        Grant READ Access
                      </button>
                    </div>
                  }
                </div>
              }
            </div>
          </div>
        }

        <!-- New Version Modal -->
        @if (showVersionModal) {
          <div class="modal-overlay">
            <div class="modal-content">
              <div class="modal-header">
                <h3>Upload New Document Version</h3>
                <button class="modal-close" (click)="showVersionModal = false">×</button>
              </div>

              @if (versionError) {
                <div class="alert alert-danger">{{ versionError }}</div>
              }

              <form (ngSubmit)="submitNewVersion()">
                <div class="form-group">
                  <label class="form-label">Replacement File</label>
                  <input type="file" class="form-input" (change)="onVersionFileSelected($event)" required>
                  <small class="form-hint">A new immutable version entry will be added to the document ledger.</small>
                </div>

                <div class="modal-actions">
                  <button type="button" class="btn btn-secondary" (click)="showVersionModal = false">Cancel</button>
                  <button type="submit" class="btn btn-primary" [disabled]="uploadingVersion || !selectedVersionFile">
                    {{ uploadingVersion ? 'Storing Version...' : 'Commit Version' }}
                  </button>
                </div>
              </form>
            </div>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .page-container { max-width: 1300px; margin: 0 auto; padding: 2rem 1.5rem; }
    .detail-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1.5rem;
      padding: 1.75rem;
    }
    .badge-row { display: flex; gap: 0.5rem; margin-bottom: 0.75rem; align-items: center; }
    .category-pill { background: var(--bg-input); padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.75rem; color: var(--text-muted); }
    .version-pill { color: #38bdf8; font-weight: 700; font-size: 0.75rem; }
    .doc-title-large { font-size: 1.75rem; font-weight: 700; color: var(--text-main); }
    .doc-filename-meta { font-size: 0.875rem; color: var(--text-muted); margin-top: 0.25rem; }
    .header-action-buttons { display: flex; gap: 0.75rem; align-items: center; }
    
    .meta-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.5rem;
      margin-bottom: 1.5rem;
    }
    .hash-box {
      background: #06090e;
      border: 1px solid var(--border-color);
      border-radius: var(--radius);
      padding: 0.875rem;
      margin: 0.75rem 0;
      word-break: break-all;
    }
    .hash-box code { font-family: monospace; color: #34d399; font-size: 0.875rem; }
    .hash-hint { font-size: 0.75rem; color: var(--text-dim); }
    
    .meta-list { display: flex; flex-direction: column; gap: 0.5rem; margin-top: 0.75rem; font-size: 0.875rem; }
    .meta-row { display: flex; justify-content: space-between; border-bottom: 1px solid var(--border-subtle); padding-bottom: 0.375rem; }
    .meta-key { color: var(--text-muted); }
    .meta-val { color: var(--text-main); font-weight: 500; }
    .monospace { font-family: monospace; font-size: 0.8125rem; }

    .section-card { margin-bottom: 1.5rem; padding: 1.5rem; }
    .section-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1.25rem; }
    .section-subtitle { font-size: 0.8125rem; color: var(--text-muted); margin-top: 0.25rem; }
    .count-badge { font-size: 0.75rem; background: var(--bg-input); padding: 0.2rem 0.5rem; border-radius: 9999px; }

    .grant-form { margin-bottom: 1.5rem; padding: 1.25rem; background: var(--bg-input); border-radius: var(--radius); }
    .grant-inputs { display: flex; gap: 0.75rem; }
    .permission-select { width: 260px; }
    .meta-hint { font-size: 0.75rem; color: var(--text-dim); }
    .empty-grants { text-align: center; padding: 2rem; color: var(--text-muted); font-size: 0.875rem; }
    .mt-2 { margin-top: 0.75rem; }

    .suggestions-list { display: flex; flex-direction: column; gap: 0.75rem; margin-top: 1rem; }
    .suggestion-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      background: var(--bg-input);
      border: 1px solid var(--border-color);
      border-radius: var(--radius);
      padding: 0.875rem 1rem;
    }
    .sug-name { font-weight: 600; font-size: 0.875rem; color: var(--text-main); }
    .sug-reason { font-size: 0.8125rem; color: var(--text-muted); margin-top: 0.125rem; }
    .sug-meta { font-size: 0.6875rem; color: var(--text-dim); margin-top: 0.25rem; }
    
    .modal-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.25rem; }
    .form-hint { font-size: 0.75rem; color: var(--text-dim); margin-top: 0.25rem; display: block; }
    .state-container { padding: 4rem; text-align: center; color: var(--text-muted); }
  `]
})
export class DocumentDetailComponent implements OnInit {
  public document: DocumentDetail | null = null;
  public loading = true;
  public errorMessage = '';

  // Grant creation
  public newGrantEmail = '';
  public newGrantPermission: DocumentPermission = DocumentPermission.Read;
  public grantingAccess = false;
  public grantMessage = '';
  public grantError = '';

  // AI Suggestions
  public showSuggestionsModal = false;
  public suggestions: RecipientSuggestion[] = [];
  public suggestionsLoading = false;

  // New Version
  public showVersionModal = false;
  public selectedVersionFile: File | null = null;
  public uploadingVersion = false;
  public versionError = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    public authService: AuthService,
    private documentService: DocumentService,
    private grantService: GrantService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadDocument(id);
    }
  }

  public loadDocument(id: string): void {
    this.loading = true;
    this.errorMessage = '';
    this.documentService.getDocument(id).subscribe({
      next: (doc) => {
        this.document = doc;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.status === 404
          ? 'Document not found or you lack authorized access.'
          : 'Failed to retrieve document metadata.';
      }
    });
  }

  public formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  public download(versionNumber?: number): void {
    if (!this.document) return;
    this.documentService.downloadDocument(this.document.id, versionNumber).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = this.document!.originalFileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        alert('Download denied. Your authorization token may be expired or revoked.');
      }
    });
  }

  public createGrant(): void {
    if (!this.document || !this.newGrantEmail) return;

    this.grantingAccess = true;
    this.grantError = '';
    this.grantMessage = '';

    this.grantService.createGrant(this.document.id, {
      userEmail: this.newGrantEmail,
      permission: this.newGrantPermission
    }).subscribe({
      next: () => {
        this.grantingAccess = false;
        this.grantMessage = `Access granted to ${this.newGrantEmail} successfully.`;
        this.newGrantEmail = '';
        this.loadDocument(this.document!.id);
      },
      error: (err) => {
        this.grantingAccess = false;
        this.grantError = err.error?.message || 'Failed to grant permission.';
      }
    });
  }

  public revokeGrant(grantId: string): void {
    if (!this.document) return;
    if (!confirm('Are you sure you want to revoke this user\'s access immediately?')) return;

    this.grantService.revokeGrant(this.document.id, grantId).subscribe({
      next: () => {
        this.loadDocument(this.document!.id);
      },
      error: (err) => {
        alert('Revocation failed: ' + (err.error?.message || 'Error'));
      }
    });
  }

  public loadSuggestions(): void {
    if (!this.document) return;
    this.showSuggestionsModal = true;
    this.suggestionsLoading = true;
    this.grantService.getSuggestions(this.document.id).subscribe({
      next: (res) => {
        this.suggestions = res;
        this.suggestionsLoading = false;
      },
      error: () => {
        this.suggestionsLoading = false;
      }
    });
  }

  public applySuggestion(sug: RecipientSuggestion): void {
    this.newGrantEmail = sug.email;
    this.newGrantPermission = DocumentPermission.Read;
    this.showSuggestionsModal = false;
    this.createGrant();
  }

  public openNewVersionModal(): void {
    this.showVersionModal = true;
    this.versionError = '';
    this.selectedVersionFile = null;
  }

  public onVersionFileSelected(event: any): void {
    if (event.target.files && event.target.files.length > 0) {
      this.selectedVersionFile = event.target.files[0];
    }
  }

  public submitNewVersion(): void {
    if (!this.document || !this.selectedVersionFile) return;

    this.uploadingVersion = true;
    this.versionError = '';

    this.documentService.uploadNewVersion(this.document.id, this.selectedVersionFile).subscribe({
      next: () => {
        this.uploadingVersion = false;
        this.showVersionModal = false;
        this.loadDocument(this.document!.id);
      },
      error: (err) => {
        this.uploadingVersion = false;
        this.versionError = err.error?.message || 'Failed to upload new version.';
      }
    });
  }

  public deleteDocument(): void {
    if (!this.document) return;
    if (!confirm(`Are you sure you want to delete "${this.document.title}"? This soft-deletes the file according to retention policies.`)) return;

    this.documentService.deleteDocument(this.document.id).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        alert('Deletion failed: ' + (err.error?.message || 'Access Denied'));
      }
    });
  }
}
