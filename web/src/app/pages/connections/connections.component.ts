import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MetaService } from '../../core/services/meta.service';
import { GhlService } from '../../core/services/ghl.service';
import { MetaConnection } from '../../core/models/meta.models';
import { GhlConnection } from '../../core/models/ghl.models';

@Component({
  selector: 'app-connections',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './connections.component.html',
  styleUrl: './connections.component.scss',
})
export class ConnectionsComponent implements OnInit {
  readonly metaConnections = signal<MetaConnection[]>([]);
  readonly ghlConnections = signal<GhlConnection[]>([]);
  readonly isLoading = signal(true);
  readonly subscribingPageId = signal<string | null>(null);
  readonly unsubscribingPageId = signal<string | null>(null);
  readonly syncingPageId = signal<string | null>(null);
  readonly notice = signal<{ kind: 'success' | 'error'; text: string } | null>(null);

  readonly isConnectingGhl = signal(false);
  readonly isConnectingMeta = signal(false);
  readonly disconnectingGhlId = signal<string | null>(null);
  readonly disconnectingMetaId = signal<string | null>(null);

  constructor(private metaService: MetaService, private ghlService: GhlService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    if (params.get('meta_connected')) this.notice.set({ kind: 'success', text: 'Meta account connected successfully.' });
    if (params.get('ghl_connected')) this.notice.set({ kind: 'success', text: 'GoHighLevel location connected successfully.' });
    if (params.get('meta_error')) this.notice.set({ kind: 'error', text: params.get('meta_error')! });
    if (params.get('ghl_error')) this.notice.set({ kind: 'error', text: params.get('ghl_error')! });

    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.metaService.getConnections().subscribe((c) => this.metaConnections.set(c));
    this.ghlService.getConnections().subscribe({
      next: (c) => {
        this.ghlConnections.set(c);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  connectMeta(): void {
    this.isConnectingMeta.set(true);
    this.metaService.getConnectUrl().subscribe({
      next: (res) => (window.location.href = res.url),
      error: (err) => {
        this.isConnectingMeta.set(false);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to initiate Meta connection.' });
      }
    });
  }

  disconnectMeta(connectionId: string): void {
    if (!confirm('Are you sure you want to disconnect this Meta account? Subscribed pages and forms will be disconnected.')) {
      return;
    }
    this.disconnectingMetaId.set(connectionId);
    this.metaService.disconnect(connectionId).subscribe({
      next: () => {
        this.disconnectingMetaId.set(null);
        this.notice.set({ kind: 'success', text: 'Meta account disconnected successfully.' });
        this.load();
      },
      error: (err) => {
        this.disconnectingMetaId.set(null);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to disconnect Meta account.' });
      }
    });
  }

  connectGhl(): void {
    this.isConnectingGhl.set(true);
    this.ghlService.getConnectUrl().subscribe({
      next: (res) => (window.location.href = res.url),
      error: (err) => {
        this.isConnectingGhl.set(false);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to initiate GoHighLevel connection.' });
      }
    });
  }

  disconnectGhl(connectionId: string): void {
    if (!confirm('Are you sure you want to disconnect and logout from this GoHighLevel location?')) {
      return;
    }
    this.disconnectingGhlId.set(connectionId);
    this.ghlService.disconnect(connectionId).subscribe({
      next: () => {
        this.disconnectingGhlId.set(null);
        this.notice.set({ kind: 'success', text: 'GoHighLevel location disconnected successfully.' });
        this.load();
      },
      error: (err) => {
        this.disconnectingGhlId.set(null);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to disconnect GoHighLevel location.' });
      }
    });
  }

  subscribePage(pageId: string): void {
    this.subscribingPageId.set(pageId);
    this.metaService.subscribePage(pageId).subscribe({
      next: () => {
        this.subscribingPageId.set(null);
        this.notice.set({ kind: 'success', text: 'Subscribed to lead webhooks successfully.' });
        this.load();
      },
      error: (err) => {
        this.subscribingPageId.set(null);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to subscribe page to leads.' });
      },
    });
  }

  unsubscribePage(pageId: string, pageName: string): void {
    if (!confirm(`Are you sure you want to unsubscribe "${pageName}" from lead webhooks?`)) {
      return;
    }
    this.unsubscribingPageId.set(pageId);
    this.metaService.unsubscribePage(pageId).subscribe({
      next: () => {
        this.unsubscribingPageId.set(null);
        this.notice.set({ kind: 'success', text: `Unsubscribed "${pageName}" from lead webhooks.` });
        this.load();
      },
      error: (err) => {
        this.unsubscribingPageId.set(null);
        const errMsg = err?.error?.error || err?.error?.message || (err?.status ? `Server returned HTTP ${err.status} (${err.statusText || 'Not Found - please restart .NET API'})` : `Failed to unsubscribe "${pageName}".`);
        this.notice.set({ kind: 'error', text: errMsg });
      },
    });
  }

  syncPage(pageId: string, pageName: string): void {
    this.syncingPageId.set(pageId);
    this.metaService.syncPage(pageId).subscribe({
      next: () => {
        this.syncingPageId.set(null);
        this.notice.set({ kind: 'success', text: `Successfully synced lead forms for "${pageName}".` });
        this.load();
      },
      error: (err) => {
        this.syncingPageId.set(null);
        const errMsg = err?.error?.error || err?.error?.message || (err?.status ? `Server returned HTTP ${err.status} (${err.statusText || 'Not Found - please restart .NET API'})` : `Failed to sync forms for "${pageName}".`);
        this.notice.set({ kind: 'error', text: errMsg });
      },
    });
  }
}
