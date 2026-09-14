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
  readonly notice = signal<{ kind: 'success' | 'error'; text: string } | null>(null);

  constructor(private metaService: MetaService, private ghlService: GhlService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    if (params.get('meta_connected')) this.notice.set({ kind: 'success', text: 'Meta account connected.' });
    if (params.get('ghl_connected')) this.notice.set({ kind: 'success', text: 'GoHighLevel location connected.' });
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
    this.metaService.getConnectUrl().subscribe((res) => (window.location.href = res.url));
  }

  connectGhl(): void {
    this.ghlService.getConnectUrl().subscribe((res) => (window.location.href = res.url));
  }

  subscribePage(pageId: string): void {
    this.subscribingPageId.set(pageId);
    this.metaService.subscribePage(pageId).subscribe({
      next: () => {
        this.subscribingPageId.set(null);
        this.load();
      },
      error: () => this.subscribingPageId.set(null),
    });
  }
}
