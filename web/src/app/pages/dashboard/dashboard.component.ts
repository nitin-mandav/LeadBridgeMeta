import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LeadsService } from '../../core/services/leads.service';
import { LeadEvent, LeadEventStatusLabels } from '../../core/models/lead.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  readonly leads = signal<LeadEvent[]>([]);
  readonly isLoading = signal(true);
  readonly retryingId = signal<string | null>(null);
  readonly statusLabels = LeadEventStatusLabels;

  constructor(private leadsService: LeadsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.leadsService.getLeads().subscribe({
      next: (leads) => {
        this.leads.set(leads);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  retry(lead: LeadEvent): void {
    this.retryingId.set(lead.id);
    this.leadsService.retry(lead.id).subscribe({
      next: () => {
        this.retryingId.set(null);
        this.load();
      },
      error: () => this.retryingId.set(null),
    });
  }

  badgeClass(status: number): string {
    return `badge badge-${this.statusLabels[status as 0 | 1 | 2 | 3 | 4].toLowerCase()}`;
  }
}
