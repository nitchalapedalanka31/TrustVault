import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DocumentService } from '../../core/services/document.service';
import { DocumentItem } from '../../core/models/models';

@Component({
  selector: 'app-my-documents',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h1 class="page-title">My Documents</h1>
          <p class="page-subtitle">Resources owned by you with full grant and ownership control</p>
        </div>
      </div>

      <div class="card table-card">
        @if (loading) {
          <div class="state-container"><div class="spinner"></div><p>Loading owned documents...</p></div>
        } @else if (myDocs.length === 0) {
          <div class="state-container">
            <h4>No Owned Documents</h4>
            <p>You haven't uploaded any confidential documents yet.</p>
          </div>
        } @else {
          <div class="table-container">
            <table class="table">
              <thead>
                <tr>
                  <th>Title & Original Filename</th>
                  <th>Category</th>
                  <th>Version</th>
                  <th>Hash</th>
                  <th>Updated</th>
                  <th style="text-align: right;">Action</th>
                </tr>
              </thead>
              <tbody>
                @for (doc of myDocs; track doc.id) {
                  <tr>
                    <td>
                      <div class="doc-title" [routerLink]="['/documents', doc.id]">{{ doc.title }}</div>
                      <div class="doc-filename">{{ doc.originalFileName }}</div>
                    </td>
                    <td><span class="category-pill">{{ doc.category }}</span></td>
                    <td><span class="version-pill">v{{ doc.currentVersionNumber }}</span></td>
                    <td><code>{{ doc.sha256Hash.substring(0, 10) }}...</code></td>
                    <td>{{ doc.updatedAtUtc | date:'short' }}</td>
                    <td style="text-align: right;">
                      <button class="btn btn-secondary btn-sm" [routerLink]="['/documents', doc.id]">Manage & Grants</button>
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
export class MyDocumentsComponent implements OnInit {
  public myDocs: DocumentItem[] = [];
  public loading = true;

  constructor(private documentService: DocumentService) {}

  ngOnInit(): void {
    this.documentService.getDocuments().subscribe({
      next: (res) => {
        this.myDocs = res.items.filter(d => d.isOwner);
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }
}
