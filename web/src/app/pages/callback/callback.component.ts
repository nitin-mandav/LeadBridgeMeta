import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MetaService } from '../../core/services/meta.service';
import { GhlService } from '../../core/services/ghl.service';

type Status = 'processing' | 'success' | 'error';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './callback.component.html',
  styleUrl: './callback.component.scss',
})
export class CallbackComponent implements OnInit {
  readonly status = signal<Status>('processing');
  readonly statusMessage = signal<string>('Verifying authorization and establishing connection...');
  readonly errorMessage = signal<string | null>(null);
  readonly providerName = signal<string>('Meta');

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private metaService: MetaService,
    private ghlService: GhlService
  ) {}

  ngOnInit(): void {
    const url = this.router.url.toLowerCase();
    const queryParams = this.route.snapshot.queryParams;

    // Detect provider
    const isGhl =
      url.includes('ghl') ||
      queryParams['provider'] === 'ghl' ||
      (queryParams['state'] && queryParams['state'].includes('ghl'));

    const provider = isGhl ? 'GoHighLevel' : 'Meta';
    this.providerName.set(provider);

    // Check for error returned by OAuth provider
    const error = queryParams['error'] || queryParams['error_reason'] || queryParams['meta_error'] || queryParams['ghl_error'];
    const errorDescription = queryParams['error_description'];
    if (error) {
      this.status.set('error');
      this.errorMessage.set(
        errorDescription ? `${error}: ${errorDescription}` : error
      );
      return;
    }

    const code = queryParams['code'];
    const state = queryParams['state'];

    if (!code) {
      this.status.set('error');
      this.errorMessage.set('Missing authorization code in the callback URL.');
      return;
    }

    if (!state && !isGhl) {
      this.status.set('error');
      this.errorMessage.set('Missing state token in the callback URL.');
      return;
    }

    this.status.set('processing');
    this.statusMessage.set(`Connecting ${provider} account... Please wait.`);

    if (isGhl) {
      this.ghlService.handleCallback(code, state).subscribe({
        next: () => this.handleSuccess('ghl_connected'),
        error: (err) => this.handleFailure(err, 'GoHighLevel'),
      });
    } else {
      this.metaService.handleCallback(code, state).subscribe({
        next: (res) => {
          const userSuffix = res?.userName ? ` as ${res.userName}` : '';
          this.handleSuccess('meta_connected', userSuffix);
        },
        error: (err) => this.handleFailure(err, 'Meta'),
      });
    }
  }

  private handleSuccess(queryFlag: string, suffix = ''): void {
    this.status.set('success');
    this.statusMessage.set(`Successfully connected ${this.providerName()}${suffix}! Redirecting...`);
    setTimeout(() => {
      this.router.navigate(['/connections'], {
        queryParams: { [queryFlag]: '1' },
      });
    }, 1200);
  }

  private handleFailure(err: any, provider: string): void {
    this.status.set('error');
    const msg =
      err?.error?.error ||
      err?.error?.message ||
      err?.message ||
      `Failed to complete ${provider} authorization.`;
    this.errorMessage.set(msg);
  }

  goToConnections(): void {
    this.router.navigate(['/connections']);
  }
}
