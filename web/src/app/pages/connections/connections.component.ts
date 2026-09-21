import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MetaService } from '../../core/services/meta.service';
import { GhlService } from '../../core/services/ghl.service';
import { ShopifyService } from '../../core/services/shopify.service';
import { EmailService } from '../../core/services/email.service';
import { MetaConnection } from '../../core/models/meta.models';
import { GhlConnection } from '../../core/models/ghl.models';
import { ShopifyConnection } from '../../core/models/shopify.models';
import { EmailConnection } from '../../core/models/email.models';

@Component({
  selector: 'app-connections',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './connections.component.html',
  styleUrl: './connections.component.scss',
})
export class ConnectionsComponent implements OnInit {
  readonly metaConnections = signal<MetaConnection[]>([]);
  readonly ghlConnections = signal<GhlConnection[]>([]);
  readonly shopifyConnections = signal<ShopifyConnection[]>([]);
  readonly emailConnections = signal<EmailConnection[]>([]);
  readonly isLoading = signal(true);
  readonly subscribingPageId = signal<string | null>(null);
  readonly unsubscribingPageId = signal<string | null>(null);
  readonly syncingPageId = signal<string | null>(null);
  readonly notice = signal<{ kind: 'success' | 'error'; text: string } | null>(null);

  readonly isConnectingGhl = signal(false);
  readonly isConnectingMeta = signal(false);
  readonly isConnectingShopify = signal(false);
  readonly isConnectingEmail = signal(false);
  readonly disconnectingGhlId = signal<string | null>(null);
  readonly disconnectingMetaId = signal<string | null>(null);
  readonly disconnectingShopifyId = signal<string | null>(null);
  readonly disconnectingEmailId = signal<string | null>(null);
  readonly testingEmailId = signal<string | null>(null);
  readonly shopifyStoreDomain = signal<string>('');
  readonly newEmailInput = signal<string>('');

  constructor(
    private metaService: MetaService,
    private ghlService: GhlService,
    private shopifyService: ShopifyService,
    private emailService: EmailService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    if (params.get('meta_connected')) this.notice.set({ kind: 'success', text: 'Meta account connected successfully.' });
    if (params.get('ghl_connected')) this.notice.set({ kind: 'success', text: 'GoHighLevel location connected successfully.' });
    if (params.get('shopify_connected')) this.notice.set({ kind: 'success', text: 'Shopify store connected successfully.' });
    if (params.get('meta_error')) this.notice.set({ kind: 'error', text: params.get('meta_error')! });
    if (params.get('ghl_error')) this.notice.set({ kind: 'error', text: params.get('ghl_error')! });
    if (params.get('shopify_error')) this.notice.set({ kind: 'error', text: params.get('shopify_error')! });

    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.metaService.getConnections().subscribe((c) => this.metaConnections.set(c));
    this.shopifyService.getConnections().subscribe({
      next: (c) => this.shopifyConnections.set(c),
      error: () => {},
    });
    this.emailService.getConnections().subscribe({
      next: (c) => this.emailConnections.set(c),
      error: () => {},
    });
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

  connectShopify(): void {
    const shop = this.shopifyStoreDomain().trim();
    if (!shop) {
      this.notice.set({ kind: 'error', text: 'Please enter your Shopify store domain (e.g. your-store.myshopify.com).' });
      return;
    }
    this.isConnectingShopify.set(true);
    this.shopifyService.getConnectUrl(shop).subscribe({
      next: (res) => (window.location.href = res.url),
      error: (err) => {
        this.isConnectingShopify.set(false);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to initiate Shopify connection.' });
      }
    });
  }

  disconnectShopify(connectionId: string): void {
    if (!confirm('Are you sure you want to disconnect this Shopify store?')) {
      return;
    }
    this.disconnectingShopifyId.set(connectionId);
    this.shopifyService.disconnect(connectionId).subscribe({
      next: () => {
        this.disconnectingShopifyId.set(null);
        this.notice.set({ kind: 'success', text: 'Shopify store disconnected successfully.' });
        this.load();
      },
      error: (err) => {
        this.disconnectingShopifyId.set(null);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to disconnect Shopify store.' });
      }
    });
  }

  connectEmail(): void {
    const email = this.newEmailInput().trim();
    if (!email || !email.includes('@')) {
      this.notice.set({ kind: 'error', text: 'Please enter a valid email address (e.g. office@domain.com).' });
      return;
    }

    this.isConnectingEmail.set(true);
    this.emailService.addConnection(email).subscribe({
      next: (conn) => {
        this.isConnectingEmail.set(false);
        this.newEmailInput.set('');
        this.notice.set({ kind: 'success', text: `Email connection for "${conn.email}" added successfully.` });
        this.load();
      },
      error: (err) => {
        this.isConnectingEmail.set(false);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to add email connection.' });
      }
    });
  }

  disconnectEmail(connectionId: string, email: string): void {
    if (!confirm(`Are you sure you want to remove "${email}" from receiving lead notifications?`)) {
      return;
    }

    this.disconnectingEmailId.set(connectionId);
    this.emailService.disconnect(connectionId).subscribe({
      next: () => {
        this.disconnectingEmailId.set(null);
        this.notice.set({ kind: 'success', text: `Email "${email}" disconnected successfully.` });
        this.load();
      },
      error: (err) => {
        this.disconnectingEmailId.set(null);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to disconnect email.' });
      }
    });
  }

  sendTestEmail(email: string, connectionId: string): void {
    this.testingEmailId.set(connectionId);
    this.emailService.sendTestEmail(email).subscribe({
      next: (res) => {
        this.testingEmailId.set(null);
        this.notice.set({ kind: 'success', text: res.message || `Test lead email sent successfully to ${email}!` });
      },
      error: (err) => {
        this.testingEmailId.set(null);
        this.notice.set({ kind: 'error', text: err?.error?.error || 'Failed to send test lead email.' });
      }
    });
  }
}
