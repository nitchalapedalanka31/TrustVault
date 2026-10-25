import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { DocumentService } from '../../core/services/document.service';
import { DocumentItem, DocumentPermission } from '../../core/models/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-container">
      <!-- Header -->
      <div class="page-header">
        <div>
          <h1 class="page-title">Enterprise Security Dashboard</h1>
          <p class="page-subtitle">Authoritative resource-level access enforcement and audit telemetry</p>
        </div>
        <div class="header-actions">
          @if (authService.isOwner() || authService.isAdmin()) {
            <button class="btn btn-primary" (click)="openUploadModal()">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
                <polyline points="17 8 12 3 7 8"/>
                <line x1="12" y1="3" x2="12" y2="15"/>
              </svg>
              Upload Document
            </button>
          }
        </div>
      </div>

      <!-- Metric Cards -->
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-icon primary">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
              <polyline points="14 2 14 8 20 8"/>
            </svg>
          </div>
          <div>
            <div class="stat-value">{{ totalDocuments }}</div>
            <div class="stat-label">Authorized Documents</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon success">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
            </svg>
          </div>
          <div>
            <div class="stat-value">Active</div>
            <div class="stat-label">Zero-Trust Enforcement</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon warning">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"/>
              <polyline points="12 6 12 12 16 14"/>
            </svg>
          </div>
          <div>
            <div class="stat-value">Instant</div>
            <div class="stat-label">Revocation Latency</div>
          </div>
        </div>

        <div class="stat-card">
          <div class="stat-icon info">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2"/>
              <path d="M7 11V7a5 5 0 0 1 10 0v4"/>
            </svg>
          </div>
          <div>
            <div class="stat-value">SHA-256</div>
            <div class="stat-label">Tamper-Evident Trail</div>
          </div>
        </div>
      </div>

      <!-- Search & Filters -->
      <div class="filter-bar card">
        <div class="search-input-group">
          <svg class="search-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="11" cy="11" r="8"/>
            <line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          <input 
            type="text" 
            class="form-input search-field" 
            placeholder="Search authorized titles, filenames, descriptions..." 
            [(ngModel)]="searchQuery"
            (keyup.enter)="loadDocuments()">
        </div>

        <select class="form-select category-select" [(ngModel)]="selectedCategory" (change)="loadDocuments()">
          <option value="">All Categories</option>
          <option value="General">General</option>
          <option value="Finance">Finance</option>
          <option value="Engineering">Engineering</option>
          <option value="Legal">Legal</option>
          <option value="Security">Security</option>
        </select>

        <button class="btn btn-secondary" (click)="loadDocuments()">Filter</button>
      </div>

      <!-- Documents Table -->
      <div class="card table-card">
        <div class="table-header-title">
          <h3>Authorized Resources</h3>
          <span class="count-badge">{{ documents.length }} items</span>
        </div>

        @if (loading) {
          <div class="state-container">
            <div class="spinner"></div>
            <p>Evaluating resource permissions...</p>
          </div>
        } @else if (documents.length === 0) {
          <div class="state-container empty">
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
              <polyline points="14 2 14 8 20 8"/>
            </svg>
            <h4>No Authorized Documents Found</h4>
            <p>You can discover only documents you own or have an active, explicit grant for.</p>
          </div>
        } @else {
          <div class="table-container">
            <table class="table">
              <thead>
                <tr>
                  <th>Title & Original Filename</th>
                  <th>Category</th>
                  <th>Version</th>
                  <th>Integrity (SHA-256)</th>
                  <th>Your Permission</th>
                  <th>Owner</th>
                  <th style="text-align: right;">Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (doc of documents; track doc.id) {
                  <tr>
                    <td>
                      <div class="doc-title-cell">
                        <span class="doc-title" [routerLink]="['/documents', doc.id]">{{ doc.title }}</span>
                        <span class="doc-filename">{{ doc.originalFileName }}</span>
                      </div>
                    </td>
                    <td>
                      <span class="category-pill">{{ doc.category }}</span>
                    </td>
                    <td>
                      <span class="version-pill">v{{ doc.currentVersionNumber }}</span>
                    </td>
                    <td>
                      <span class="hash-preview" [title]="doc.sha256Hash">
                        {{ doc.sha256Hash.substring(0, 10) }}...
                      </span>
                    </td>
                    <td>
                      @if (doc.isOwner) {
                        <span class="badge badge-primary">Owner (Full)</span>
                      } @else if (doc.permissionLevel === 2) {
                        <span class="badge badge-success">EDIT Grant</span>
                      } @else {
                        <span class="badge badge-neutral">READ Grant</span>
                      }
                    </td>
                    <td>
                      <span class="owner-name">{{ doc.ownerName }}</span>
                    </td>
                    <td style="text-align: right;">
                      <div class="row-actions">
                        <button class="btn btn-secondary btn-sm" [routerLink]="['/documents', doc.id]">
                          Details
                        </button>
                        <button class="btn btn-primary btn-sm" (click)="download(doc)">
                          Download
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>

      <!-- Upload Modal -->
      @if (showUploadModal) {
        <div class="modal-overlay">
          <div class="modal-content">
            <div class="modal-header">
              <h3>Upload Secure Document</h3>
              <button class="modal-close" (click)="closeUploadModal()">×</button>
            </div>

            @if (uploadError) {
              <div class="alert alert-danger">{{ uploadError }}</div>
            }

            <form (ngSubmit)="submitUpload()">
              <div class="form-group">
                <label class="form-label">Document Title</label>
                <input type="text" class="form-input" [(ngModel)]="uploadTitle" name="title" placeholder="Confidential Q3 Report" required>
              </div>

              <div class="form-group">
                <label class="form-label">Category</label>
                <select class="form-select" [(ngModel)]="uploadCategory" name="category">
                  <option value="General">General</option>
                  <option value="Finance">Finance</option>
                  <option value="Engineering">Engineering</option>
                  <option value="Legal">Legal</option>
                  <option value="Security">Security</option>
                </select>
              </div>

              <div class="form-group">
                <label class="form-label">Tags (comma-separated)</label>
                <input type="text" class="form-input" [(ngModel)]="uploadTags" name="tags" placeholder="audit, finance, 2026">
              </div>

              <div class="form-group">
                <label class="form-label">Description (Optional)</label>
                <textarea class="form-textarea" [(ngModel)]="uploadDescription" name="description"></textarea>
              </div>

              <div class="form-group">
                <label class="form-label">File (Max 25MB - Valid PDF, PNG, JPG, DOCX, XLSX, TXT)</label>
                <input type="file" class="form-input" (change)="onFileSelected($event)" required>
                <small class="form-hint">Disguised polyglots or active executables (.exe, .sh, .html, .svg) are strictly blocked.</small>
              </div>

              <div class="modal-actions">
                <button type="button" class="btn btn-secondary" (click)="closeUploadModal()">Cancel</button>
                <button type="submit" class="btn btn-primary" [disabled]="uploading || !selectedFile">
                  {{ uploading ? 'Validating & Storing...' : 'Secure Upload' }}
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .page-container {
      max-width: 1300px;
      margin: 0 auto;
      padding: 2rem 1.5rem;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 2rem;
    }
    .page-title {
      font-size: 1.75rem;
      font-weight: 700;
      color: var(--text-main);
    }
    .page-subtitle {
      font-size: 0.875rem;
      color: var(--text-muted);
      margin-top: 0.25rem;
    }
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 1.25rem;
      margin-bottom: 2rem;
    }
    .stat-card {
      background-color: var(--bg-card);
      border: 1px solid var(--border-color);
      border-radius: var(--radius);
      padding: 1.25rem;
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .stat-icon {
      width: 44px;
      height: 44px;
      border-radius: var(--radius);
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .stat-icon svg { width: 22px; height: 22px; }
    .stat-icon.primary { background: var(--primary-light); color: var(--primary); }
    .stat-icon.success { background: var(--success-light); color: var(--success); }
    .stat-icon.warning { background: var(--warning-light); color: var(--warning); }
    .stat-icon.info { background: rgba(56, 189, 248, 0.15); color: #38bdf8; }
    .stat-value { font-size: 1.5rem; font-weight: 700; color: var(--text-main); }
    .stat-label { font-size: 0.75rem; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.05em; }

    .filter-bar {
      display: flex;
      gap: 1rem;
      align-items: center;
      margin-bottom: 1.5rem;
      padding: 1rem 1.25rem;
    }
    .search-input-group {
      flex: 1;
      position: relative;
      display: flex;
      align-items: center;
    }
    .search-icon {
      position: absolute;
      left: 12px;
      color: var(--text-dim);
    }
    .search-field {
      padding-left: 2.5rem;
    }
    .category-select {
      width: 180px;
    }

    .table-card { padding: 0; overflow: hidden; }
    .table-header-title {
      padding: 1.25rem 1.5rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid var(--border-color);
    }
    .count-badge {
      font-size: 0.75rem;
      background: var(--bg-input);
      color: var(--text-muted);
      padding: 0.25rem 0.625rem;
      border-radius: 9999px;
    }
    .doc-title-cell { display: flex; flex-direction: column; gap: 0.125rem; }
    .doc-title { font-weight: 600; color: var(--text-main); cursor: pointer; }
    .doc-title:hover { color: var(--primary); }
    .doc-filename { font-size: 0.75rem; color: var(--text-dim); }
    .category-pill {
      font-size: 0.75rem;
      background: var(--bg-input);
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      color: var(--text-muted);
    }
    .version-pill {
      font-size: 0.75rem;
      font-weight: 700;
      color: #38bdf8;
    }
    .hash-preview {
      font-family: monospace;
      font-size: 0.75rem;
      color: var(--text-dim);
      background: rgba(0, 0, 0, 0.25);
      padding: 0.2rem 0.4rem;
      border-radius: 4px;
    }
    .owner-name { font-size: 0.8125rem; color: var(--text-muted); }
    .row-actions { display: flex; gap: 0.5rem; justify-content: flex-end; }
    .state-container {
      padding: 4rem 2rem;
      text-align: center;
      color: var(--text-muted);
    }
    .state-container.empty svg {
      color: var(--text-dim);
      margin-bottom: 1rem;
    }
    .form-hint { font-size: 0.75rem; color: var(--text-dim); margin-top: 0.25rem; }
    .modal-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 1.5rem;
    }
  `]
})
export class DashboardComponent implements OnInit {
  public documents: DocumentItem[] = [];
  public totalDocuments = 0;
  public loading = true;
  public searchQuery = '';
  public selectedCategory = '';

  // Upload modal state
  public showUploadModal = false;
  public uploadTitle = '';
  public uploadCategory = 'General';
  public uploadTags = '';
  public uploadDescription = '';
  public selectedFile: File | null = null;
  public uploading = false;
  public uploadError = '';

  constructor(
    public authService: AuthService,
    private documentService: DocumentService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadDocuments();
  }

  public loadDocuments(): void {
    this.loading = true;
    this.documentService.getDocuments({
      query: this.searchQuery,
      category: this.selectedCategory
    }).subscribe({
      next: (res) => {
        this.documents = res.items;
        this.totalDocuments = res.totalCount;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  public openUploadModal(): void {
    this.showUploadModal = true;
    this.uploadError = '';
  }

  public closeUploadModal(): void {
    this.showUploadModal = false;
    this.selectedFile = null;
    this.uploadTitle = '';
  }

  public onFileSelected(event: any): void {
    if (event.target.files && event.target.files.length > 0) {
      this.selectedFile = event.target.files[0];
      if (!this.uploadTitle && this.selectedFile) {
        this.uploadTitle = this.selectedFile.name.replace(/\.[^/.]+$/, '');
      }
    }
  }

  public submitUpload(): void {
    if (!this.selectedFile) return;

    this.uploading = true;
    this.uploadError = '';

    const formData = new FormData();
    formData.append('title', this.uploadTitle);
    formData.append('category', this.uploadCategory);
    formData.append('tags', this.uploadTags);
    if (this.uploadDescription) {
      formData.append('description', this.uploadDescription);
    }
    formData.append('file', this.selectedFile, this.selectedFile.name);

    this.documentService.uploadDocument(formData).subscribe({
      next: () => {
        this.uploading = false;
        this.closeUploadModal();
        this.loadDocuments();
      },
      error: (err) => {
        this.uploading = false;
        this.uploadError = err.error?.message || 'File upload or validation failed.';
      }
    });
  }

  public download(doc: DocumentItem): void {
    this.documentService.downloadDocument(doc.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = doc.originalFileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        alert('Download failed: ' + (err.error?.message || 'Access Denied'));
      }
    });
  }
}
