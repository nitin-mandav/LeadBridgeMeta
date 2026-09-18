import { Component, OnInit, computed, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LeadsService } from '../../core/services/leads.service';
import { LeadEvent, LeadEventStatusLabels } from '../../core/models/lead.models';

export interface ParsedContactField {
  label: string;
  value: string;
  rawKey: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  readonly leads = signal<LeadEvent[]>([]);
  readonly isLoading = signal(true);
  readonly retryingId = signal<string | null>(null);
  readonly statusLabels = LeadEventStatusLabels;

  // Search & Filter
  readonly searchQuery = signal<string>('');
  readonly statusFilter = signal<string | number>('all');

  // Sorting
  readonly sortColumn = signal<string>('receivedAtUtc');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');

  // Pagination
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(10);
  readonly pageSizeOptions = [10, 25, 50, 100];

  // Contact Details Modal State
  readonly selectedLead = signal<LeadEvent | null>(null);
  readonly isModalOpen = signal<boolean>(false);
  readonly copiedKey = signal<string | null>(null);
  readonly showRawJson = signal<boolean>(false);

  readonly selectedContact = computed(() => this.getPrimaryContact(this.selectedLead()));
  readonly selectedFields = computed(() => this.getParsedFields(this.selectedLead()));
  readonly selectedFormattedJson = computed(() => this.getFormattedRawJson(this.selectedLead()));

  // Counts
  readonly totalCount = computed(() => this.leads().length);
  readonly sentCount = computed(() => this.leads().filter((l) => l.status === 2).length);
  readonly sentBothCount = computed(() => this.leads().filter((l) => l.status === 2 && !!l.ghlContactId && !!l.shopifyCustomerId).length);
  readonly sentGhlCount = computed(() => this.leads().filter((l) => l.status === 2 && !!l.ghlContactId).length);
  readonly sentShopifyCount = computed(() => this.leads().filter((l) => l.status === 2 && !!l.shopifyCustomerId).length);
  readonly skippedCount = computed(() => this.leads().filter((l) => l.status === 4).length);
  readonly failedCount = computed(() => this.leads().filter((l) => l.status === 3).length);

  readonly filteredAndSortedLeads = computed(() => {
    let list = this.leads();
    const query = this.searchQuery().trim().toLowerCase();
    const status = this.statusFilter();

    if (status !== 'all') {
      if (status === 'both') {
        list = list.filter((l) => l.status === 2 && !!l.ghlContactId && !!l.shopifyCustomerId);
      } else if (status === 'ghl') {
        list = list.filter((l) => l.status === 2 && !!l.ghlContactId);
      } else if (status === 'shopify') {
        list = list.filter((l) => l.status === 2 && !!l.shopifyCustomerId);
      } else if (status === 'sent') {
        list = list.filter((l) => l.status === 2);
      } else {
        list = list.filter((l) => l.status === Number(status));
      }
    }

    if (query) {
      list = list.filter((l) => {
        const form = (l.formName || '').toLowerCase();
        const contact = (l.ghlContactId || '').toLowerCase();
        const shopifyId = (l.shopifyCustomerId || '').toLowerCase();
        const leadgen = (l.leadgenId || '').toLowerCase();
        const error = (l.errorMessage || '').toLowerCase();
        const statusObj = this.getLeadStatus(l);
        const statusText = statusObj.label.toLowerCase();
        const date = new Date(l.receivedAtUtc).toLocaleString().toLowerCase();
        const hasGhl = !!(l.ghlContactId || (l as any).GhlContactId);
        const hasShopify = !!(l.shopifyCustomerId || (l as any).ShopifyCustomerId);

        return (
          form.includes(query) ||
          contact.includes(query) ||
          shopifyId.includes(query) ||
          leadgen.includes(query) ||
          error.includes(query) ||
          statusText.includes(query) ||
          (query === 'ghl' && hasGhl) ||
          ((query === 'shopify' || query.includes('spotify')) && hasShopify) ||
          date.includes(query)
        );
      });
    }

    const col = this.sortColumn();
    const dir = this.sortDirection() === 'asc' ? 1 : -1;

    return [...list].sort((a, b) => {
      let valA: any;
      let valB: any;

      switch (col) {
        case 'receivedAtUtc':
          valA = new Date(a.receivedAtUtc).getTime();
          valB = new Date(b.receivedAtUtc).getTime();
          break;
        case 'formName':
          valA = (a.formName || '').toLowerCase();
          valB = (b.formName || '').toLowerCase();
          break;
        case 'status':
          valA = this.getLeadStatus(a).label;
          valB = this.getLeadStatus(b).label;
          break;
        case 'destinations':
        case 'ghlContactId':
          valA = (a.ghlContactId || a.shopifyCustomerId || '').toLowerCase();
          valB = (b.ghlContactId || b.shopifyCustomerId || '').toLowerCase();
          break;
        case 'errorMessage':
          valA = (a.errorMessage || '').toLowerCase();
          valB = (b.errorMessage || '').toLowerCase();
          break;
        default:
          return 0;
      }

      if (valA < valB) return -1 * dir;
      if (valA > valB) return 1 * dir;
      return 0;
    });
  });

  readonly filteredCount = computed(() => this.filteredAndSortedLeads().length);

  readonly totalPages = computed(() => {
    const total = this.filteredCount();
    const size = this.pageSize();
    return Math.max(1, Math.ceil(total / size));
  });

  readonly paginatedLeads = computed(() => {
    const list = this.filteredAndSortedLeads();
    const page = Math.min(this.currentPage(), this.totalPages());
    const size = this.pageSize();
    const start = (page - 1) * size;
    return list.slice(start, start + size);
  });

  readonly pageStartRecord = computed(() => {
    const total = this.filteredCount();
    if (total === 0) return 0;
    const page = Math.min(this.currentPage(), this.totalPages());
    return (page - 1) * this.pageSize() + 1;
  });

  readonly pageEndRecord = computed(() => {
    const total = this.filteredCount();
    const page = Math.min(this.currentPage(), this.totalPages());
    return Math.min(total, page * this.pageSize());
  });

  readonly visiblePages = computed(() => {
    const total = this.totalPages();
    const current = Math.min(this.currentPage(), total);
    const pages: number[] = [];

    const start = Math.max(1, current - 2);
    const end = Math.min(total, current + 2);

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  });

  constructor(private leadsService: LeadsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.leadsService.getLeads().subscribe({
      next: (leads) => {
        const normalized = (leads || []).map((l: any) => ({
          ...l,
          shopifyCustomerId: l.shopifyCustomerId ?? l.ShopifyCustomerId ?? null,
          ghlContactId: l.ghlContactId ?? l.GhlContactId ?? null,
        }));
        this.leads.set(normalized);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  toggleSort(column: string): void {
    if (this.sortColumn() === column) {
      this.sortDirection.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortColumn.set(column);
      this.sortDirection.set(column === 'receivedAtUtc' ? 'desc' : 'asc');
    }
  }

  onSearchInput(val: string): void {
    this.searchQuery.set(val);
    this.currentPage.set(1);
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.currentPage.set(1);
  }

  setStatusFilter(status: string | number): void {
    this.statusFilter.set(status);
    this.currentPage.set(1);
  }

  onPageSizeChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.pageSize.set(Number(select.value));
    this.currentPage.set(1);
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update((p) => p - 1);
    }
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update((p) => p + 1);
    }
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

  getLeadStatus(lead: LeadEvent): { label: string; cssClass: string; key: string } {
    if (lead.status === 3) return { label: 'Failed', cssClass: 'badge badge-failed', key: 'failed' };
    if (lead.status === 4) return { label: 'Skipped (Unmapped)', cssClass: 'badge badge-skipped', key: 'skipped' };
    if (lead.status === 0) return { label: 'Received', cssClass: 'badge badge-received', key: 'received' };
    if (lead.status === 1) return { label: 'Fetched', cssClass: 'badge badge-fetched', key: 'fetched' };

    // Status === 2 (Sent)
    const hasGhl = !!lead.ghlContactId;
    const hasShopify = !!lead.shopifyCustomerId;

    if (hasGhl && hasShopify) {
      return { label: 'Sent to Both', cssClass: 'badge badge-both', key: 'both' };
    }
    if (hasGhl) {
      return { label: 'Sent to GHL', cssClass: 'badge badge-ghl', key: 'ghl' };
    }
    if (hasShopify) {
      return { label: 'Sent to Shopify', cssClass: 'badge badge-shopify', key: 'shopify' };
    }
    return { label: 'Sent', cssClass: 'badge badge-sent', key: 'sent' };
  }

  badgeClass(status: number, lead?: LeadEvent): string {
    if (lead) {
      return this.getLeadStatus(lead).cssClass;
    }
    const label = this.statusLabels[status as 0 | 1 | 2 | 3 | 4];
    return `badge badge-${(label || 'received').toLowerCase()}`;
  }

  readonly isFetchingDetails = signal<boolean>(false);
  readonly fetchError = signal<string | null>(null);

  getRawLeadData(lead: LeadEvent | null): string | null {
    if (!lead) return null;
    return (
      lead.rawLeadDataJson ??
      (lead as any).RawLeadDataJson ??
      (lead as any).raw_lead_data_json ??
      null
    );
  }

  // Contact Details Modal Handlers
  openContactDetails(lead: LeadEvent): void {
    this.selectedLead.set(lead);
    this.showRawJson.set(false);
    this.copiedKey.set(null);
    this.fetchError.set(null);
    this.isModalOpen.set(true);

    // If answers are missing, automatically fetch details from Meta via the backend
    if (!this.getRawLeadData(lead) || this.getParsedFields(lead).length === 0) {
      this.fetchLeadDetails(lead.id);
    }
  }

  fetchLeadDetails(id: string): void {
    this.isFetchingDetails.set(true);
    this.fetchError.set(null);
    this.leadsService.getLeadById(id).subscribe({
      next: (fullLead) => {
        this.isFetchingDetails.set(false);
        if (fullLead) {
          const norm = {
            ...fullLead,
            shopifyCustomerId: (fullLead as any).shopifyCustomerId ?? (fullLead as any).ShopifyCustomerId ?? null,
            ghlContactId: (fullLead as any).ghlContactId ?? (fullLead as any).GhlContactId ?? null,
          };
          this.leads.update((list) =>
            list.map((l) => (l.id === norm.id ? { ...l, ...norm } : l))
          );
          if (this.selectedLead()?.id === norm.id) {
            this.selectedLead.set({ ...this.selectedLead()!, ...norm });
          }
        }
      },
      error: (err) => {
        this.isFetchingDetails.set(false);
        this.fetchError.set(err?.error?.message || 'Could not fetch lead details from Meta Graph API.');
        console.warn('Could not fetch lead details from Meta:', err);
      },
    });
  }

  closeContactDetails(): void {
    this.isModalOpen.set(false);
    this.selectedLead.set(null);
    this.showRawJson.set(false);
    this.copiedKey.set(null);
    this.fetchError.set(null);
    this.isFetchingDetails.set(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isModalOpen()) {
      this.closeContactDetails();
    }
  }

  copyText(text: string | null | undefined, key: string): void {
    if (!text) return;
    navigator.clipboard.writeText(text);
    this.copiedKey.set(key);
    setTimeout(() => {
      if (this.copiedKey() === key) {
        this.copiedKey.set(null);
      }
    }, 2000);
  }

  toggleRawJson(): void {
    this.showRawJson.update((v) => !v);
  }

  getParsedFields(lead: LeadEvent | null): ParsedContactField[] {
    const raw = this.getRawLeadData(lead);
    if (!raw) return [];
    try {
      let data = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (typeof data === 'string') {
        try {
          data = JSON.parse(data);
        } catch {}
      }

      let fields: any[] = [];
      if (Array.isArray(data)) {
        fields = data;
      } else if (data && typeof data === 'object') {
        const candidate = data.FieldData || data.field_data || data.fieldData || data.fields;
        if (Array.isArray(candidate)) {
          fields = candidate;
        } else {
          // Flat key-value object
          return Object.entries(data)
            .filter(([k, v]) => k && v && typeof v !== 'object')
            .map(([k, v]) => ({
              label: this.formatFieldLabel(k),
              value: String(v),
              rawKey: k,
            }));
        }
      }

      return fields
        .map((f: any) => {
          const rawKey = f.Name || f.name || f.key || '';
          const label = this.formatFieldLabel(rawKey);
          const vals = f.Values ?? f.values ?? f.value ?? '';
          const value = Array.isArray(vals) ? vals.join(', ') : String(vals ?? '');
          return { label, value, rawKey };
        })
        .filter((f) => f.value && f.value.trim().length > 0);
    } catch (e) {
      console.warn('Failed parsing rawLeadDataJson:', e, raw);
      return [];
    }
  }

  getPrimaryContact(lead: LeadEvent | null): { name: string; email: string; phone: string; initials: string } {
    const fields = this.getParsedFields(lead);
    let name = '';
    let email = '';
    let phone = '';

    for (const f of fields) {
      const k = f.rawKey.toLowerCase();
      if (!name && (k.includes('full_name') || k === 'name' || k.includes('first_name'))) {
        name = f.value;
      }
      if (!email && (k.includes('email') || f.value.includes('@'))) {
        email = f.value;
      }
      if (!phone && (k.includes('phone') || k.includes('mobile') || k.includes('contact_number'))) {
        phone = f.value;
      }
    }

    const fallbackInitials = name
      ? name
          .split(' ')
          .filter(Boolean)
          .map((n) => n[0])
          .slice(0, 2)
          .join('')
          .toUpperCase()
      : 'LD';

    return { name: name || 'Lead Contact', email, phone, initials: fallbackInitials };
  }

  getFormattedRawJson(lead: LeadEvent | null): string {
    const raw = this.getRawLeadData(lead);
    if (!raw) return '';
    try {
      let parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (typeof parsed === 'string') {
        try {
          parsed = JSON.parse(parsed);
        } catch {}
      }
      return JSON.stringify(parsed, null, 2);
    } catch {
      return String(raw);
    }
  }

  private formatFieldLabel(key: string): string {
    if (!key) return 'Field';
    const standardLabels: Record<string, string> = {
      full_name: 'Full Name',
      first_name: 'First Name',
      last_name: 'Last Name',
      email: 'Email Address',
      phone_number: 'Phone Number',
      phone: 'Phone Number',
      city: 'City',
      state: 'State',
      street_address: 'Street Address',
      zip_code: 'Zip / Postal Code',
      postal_code: 'Postal Code',
      country: 'Country',
      company_name: 'Company Name',
      job_title: 'Job Title',
    };

    if (standardLabels[key.toLowerCase()]) {
      return standardLabels[key.toLowerCase()];
    }

    return key
      .replace(/[_-]+/g, ' ')
      .replace(/\b\w/g, (char) => char.toUpperCase());
  }
}
