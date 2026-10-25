import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DocumentService } from '../../core/services/document.service';
import { DocumentItem } from '../../core/models/models';

@Component({
  selector: 'app-shared-with-me',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1 class="page-title">Shared With Me</h1>
          <p class="page-subtitle">Confidential documents explicitly shared with your identity via verified grants</p>
        </div>
      </div>

      <div class="card table-card">
        @if (loading) {
          <div class="state-container"><div class="spinner"></div><p>Checking active grants...</p></div>
        } @else if (sharedDocs.length === 0) {
          <div class="state-container">
            <h4>No Shared Documents</h4>
            <p>You will only see documents here when an owner explicitly grants READ or EDIT access to you.</p>
          </div>
        } @else {
          <div class="table-container">
            <table class="table">
              <thead>
                <tr>
                  <th>Document Title</th>
                  <th>Owner</th>
                  <th>Permission</th>
                  <th>Category</th>
                  <th>Version</th>
                  <th style="text-align: right;">Action</th>
                </tr>
              </thead>
              <tbody>
                @for (doc of sharedDocs; track doc.id) {
                  <tr>
                    <td>
                      <div class="doc-title" [routerLink]="['/documents', doc.id]">{{ doc.title }}</div>
                      <div class="doc-filename">{{ doc.originalFileName }}</div>
                    </td>
                    <td>{{ doc.ownerName }}</td>
                    <td>
                      <span class="badge" [class.badge-success]="doc.permissionLevel === 2" [class.badge-neutral]="doc.permissionLevel === 1">
                        {{ doc.permissionLevel === 2 ? 'EDIT' : 'READ' }}
                      </span>
                    </td>
                    <td><span class="category-pill">{{ doc.category }}</span></td>
                    <td><span class="version-pill">v{{ doc.currentVersionNumber }}</span></td>
                    <td style="text-align: right;">
                      <button class="btn btn-primary btn-sm" (click)="download(doc)">Download</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .page-container { max-width: 1300px; margin: 0 auto; padding: 2rem 1.5rem; }
    .page-header { margin-bottom: 1.5rem; }
    .page-title { font-size: 1.75rem; font-weight: 700; color: var(--text-main); }
    .page-subtitle { font-size: 0.875rem; color: var(--text-muted); margin-top: 0.25rem; }
    .table-card { padding: 0; overflow: hidden; }
    .doc-title { font-weight: 600; color: var(--text-main); cursor: pointer; }
    .doc-title:hover { color: var(--primary); }
    .doc-filename { font-size: 0.75rem; color: var(--text-dim); }
    .category-pill { background: var(--bg-input); padding: 0.2rem 0.5rem; border-radius: 4px; font-size: 0.75rem; color: var(--text-muted); }
    .version-pill { color: #38bdf8; font-weight: 700; font-size: 0.75rem; }
    .state-container { padding: 4rem; text-align: center; color: var(--text-muted); }
  `]
})
export class SharedWithMeComponent implements OnInit {
  public sharedDocs: DocumentItem[] = [];
  public loading = true;

  constructor(private documentService: DocumentService) {}

  ngOnInit(): void {
    this.documentService.getDocuments().subscribe({
      next: (res) => {
        // Filter to only documents where isOwner is false
        this.sharedDocs = res.items.filter(d => !d.isOwner);
        this.loading = false;
      },
      error: () => this.loading = false
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
      error: () => alert('Download access denied.')
    });
  }
}
