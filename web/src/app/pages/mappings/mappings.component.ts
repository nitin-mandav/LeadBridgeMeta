import { Component, OnInit, computed, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MetaService } from '../../core/services/meta.service';
import { GhlService } from '../../core/services/ghl.service';
import { ShopifyService } from '../../core/services/shopify.service';
import { MappingsService, MetaFormField } from '../../core/services/mappings.service';
import { MetaConnection, MetaLeadForm } from '../../core/models/meta.models';
import { GhlConnection, GhlFieldOption } from '../../core/models/ghl.models';
import { FieldMapping, GhlTargetFieldType, GhlTargetFieldTypeLabels } from '../../core/models/mapping.models';
import {
  ShopifyConnection,
  ShopifyFieldOption,
  ShopifyFieldMapping,
  ShopifyTargetFieldType,
  ShopifyTargetFieldTypeLabels,
} from '../../core/models/shopify.models';

interface FormOption {
  form: MetaLeadForm;
  pageName: string;
}

@Component({
  selector: 'app-mappings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './mappings.component.html',
  styleUrl: './mappings.component.scss',
})
export class MappingsComponent implements OnInit {
  readonly formOptions = signal<FormOption[]>([]);
  readonly ghlConnections = signal<GhlConnection[]>([]);
  readonly shopifyConnections = signal<ShopifyConnection[]>([]);
  readonly selectedFormId = signal<string | null>(null);

  // Active destination tab
  readonly activeDestinationTab = signal<'ghl' | 'shopify'>('ghl');

  // GHL state
  readonly selectedGhlConnectionId = signal<string>('');
  readonly fieldMappings = signal<FieldMapping[]>([]);
  readonly isSavingLocation = signal(false);

  // Shopify state
  readonly selectedShopifyConnectionId = signal<string>('');
  readonly shopifyFieldMappings = signal<ShopifyFieldMapping[]>([]);
  readonly isSavingShopifyConnection = signal(false);

  // Meta form fields for searchable autocomplete
  readonly metaFormFields = signal<MetaFormField[]>([]);
  readonly isLoadingFields = signal<boolean>(false);
  readonly isFieldDropdownOpen = signal<boolean>(false);
  readonly highlightedMetaIndex = signal<number>(-1);

  // GHL location fields for searchable autocomplete
  readonly ghlLocationFields = signal<GhlFieldOption[]>([]);
  readonly isLoadingGhlFields = signal<boolean>(false);
  readonly isGhlDropdownOpen = signal<boolean>(false);
  readonly highlightedGhlIndex = signal<number>(-1);
  readonly activeGhlCategory = signal<'All' | 'Contact' | 'Opportunity'>('All');

  readonly targetTypeLabels = GhlTargetFieldTypeLabels;
  readonly targetTypes: GhlTargetFieldType[] = [0, 1, 2];

  readonly newFieldKey = signal<string>('');
  newTargetType: GhlTargetFieldType = 0;
  readonly newGhlFieldKey = signal<string>('');

  // Shopify fields state
  readonly shopifyCustomerFields = signal<ShopifyFieldOption[]>([]);
  readonly isLoadingShopifyFields = signal<boolean>(false);
  readonly isShopifyDropdownOpen = signal<boolean>(false);
  readonly highlightedShopifyIndex = signal<number>(-1);
  readonly activeShopifyCategory = signal<'All' | 'Standard' | 'Custom'>('All');

  readonly shopifyTargetTypeLabels = ShopifyTargetFieldTypeLabels;
  readonly shopifyTargetTypes: ShopifyTargetFieldType[] = [0, 3, 1, 2];

  readonly newShopifyMetaFieldKey = signal<string>('');
  newShopifyTargetType: ShopifyTargetFieldType = 0;
  readonly newShopifyFieldKey = signal<string>('');
  readonly isShopifyMetaDropdownOpen = signal<boolean>(false);
  readonly highlightedShopifyMetaIndex = signal<number>(-1);

  readonly selectedForm = computed(() => this.formOptions().find((f) => f.form.id === this.selectedFormId())?.form ?? null);

  readonly filteredMetaFields = computed(() => {
    const search = this.newFieldKey().trim().toLowerCase();
    const fields = this.metaFormFields();
    if (!search) return fields;
    return fields.filter(
      (f) => f.key.toLowerCase().includes(search) || f.label.toLowerCase().includes(search)
    );
  });

  readonly filteredShopifyMetaFields = computed(() => {
    const search = this.newShopifyMetaFieldKey().trim().toLowerCase();
    const fields = this.metaFormFields();
    if (!search) return fields;
    return fields.filter(
      (f) => f.key.toLowerCase().includes(search) || f.label.toLowerCase().includes(search)
    );
  });

  readonly filteredGhlFields = computed(() => {
    const search = this.newGhlFieldKey().trim().toLowerCase();
    const cat = this.activeGhlCategory();
    let fields = this.ghlLocationFields();

    if (cat !== 'All') {
      fields = fields.filter((f) => f.category === cat);
    }

    if (!search) return fields;
    return fields.filter(
      (f) =>
        f.key.toLowerCase().includes(search) ||
        f.label.toLowerCase().includes(search) ||
        (f.category && f.category.toLowerCase().includes(search)) ||
        (f.dataType && f.dataType.toLowerCase().includes(search))
    );
  });

  readonly allShopifyFields = computed(() => {
    const fromApi = this.shopifyCustomerFields();
    const seen = new Set<string>();
    const result: ShopifyFieldOption[] = [];

    // 1. Standard & customer metafield definitions fetched from Shopify store API
    for (const f of fromApi) {
      const lower = f.key.toLowerCase();
      if (!seen.has(lower)) {
        seen.add(lower);
        result.push({
          ...f,
          category: f.category || (f.isStandard ? 'Standard' : 'Custom Field'),
        });
      }
    }

    // 2. Any custom field keys already configured in saved shopifyFieldMappings
    for (const m of this.shopifyFieldMappings()) {
      if (m.shopifyFieldKey && !seen.has(m.shopifyFieldKey.toLowerCase())) {
        seen.add(m.shopifyFieldKey.toLowerCase());
        const clean = m.shopifyFieldKey.replace('custom.', '').replace(/_/g, ' ');
        const label = clean.charAt(0).toUpperCase() + clean.slice(1);
        result.push({
          key: m.shopifyFieldKey,
          label: `${label} (Mapped)`,
          type: 'Custom',
          isStandard: false,
          category: 'Custom Field',
        });
      }
    }

    return result;
  });

  readonly shopifyTotalCount = computed(() => this.allShopifyFields().length);
  readonly shopifyStandardCount = computed(() => this.allShopifyFields().filter((f) => f.isStandard).length);
  readonly shopifyCustomCount = computed(() => this.allShopifyFields().filter((f) => !f.isStandard).length);

  readonly hasExactShopifyMatch = computed(() => {
    const q = this.newShopifyFieldKey().trim().toLowerCase();
    if (!q) return true;
    return this.allShopifyFields().some((f) => f.key.toLowerCase() === q || f.label.toLowerCase() === q);
  });

  readonly filteredShopifyFields = computed(() => {
    const search = this.newShopifyFieldKey().trim().toLowerCase();
    const cat = this.activeShopifyCategory();
    let fields = this.allShopifyFields();

    if (cat === 'Standard') {
      fields = fields.filter((f) => f.isStandard);
    } else if (cat === 'Custom') {
      fields = fields.filter((f) => !f.isStandard);
    }

    if (!search) return fields;
    return fields.filter(
      (f) =>
        f.key.toLowerCase().includes(search) ||
        f.label.toLowerCase().includes(search) ||
        (f.type && f.type.toLowerCase().includes(search)) ||
        (f.category && f.category.toLowerCase().includes(search))
    );
  });

  readonly ghlTotalCount = computed(() => this.ghlLocationFields().length);
  readonly ghlContactCount = computed(() => this.ghlLocationFields().filter((f) => f.category === 'Contact').length);
  readonly ghlOpportunityCount = computed(() => this.ghlLocationFields().filter((f) => f.category === 'Opportunity').length);

  constructor(
    private metaService: MetaService,
    private ghlService: GhlService,
    private shopifyService: ShopifyService,
    private mappingsService: MappingsService
  ) {}

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.meta-field-autocomplete')) {
      this.isFieldDropdownOpen.set(false);
    }
    if (!target.closest('.ghl-field-autocomplete')) {
      this.isGhlDropdownOpen.set(false);
    }
    if (!target.closest('.shopify-meta-autocomplete')) {
      this.isShopifyMetaDropdownOpen.set(false);
    }
    if (!target.closest('.shopify-field-autocomplete')) {
      this.isShopifyDropdownOpen.set(false);
    }
  }

  ngOnInit(): void {
    this.ghlService.getConnections().subscribe((c) => {
      this.ghlConnections.set(c);
      if (c.length > 0 && !this.selectedGhlConnectionId()) {
        this.selectedGhlConnectionId.set(c[0].id);
        this.loadGhlFields(c[0].id);
      }
    });

    this.shopifyService.getConnections().subscribe((c) => {
      this.shopifyConnections.set(c);
      if (c.length > 0 && !this.selectedShopifyConnectionId()) {
        this.selectedShopifyConnectionId.set(c[0].id);
        this.loadShopifyFields(c[0].id);
      }
    });

    this.loadShopifyFields();

    this.metaService.getConnections().subscribe((connections: MetaConnection[]) => {
      const options: FormOption[] = [];
      for (const conn of connections) {
        for (const page of conn.pages) {
          for (const form of page.forms) {
            options.push({ form, pageName: page.pageName });
          }
        }
      }
      this.formOptions.set(options);
      if (options.length > 0) this.selectForm(options[0].form.id);
    });
  }

  selectForm(formId: string): void {
    this.selectedFormId.set(formId);
    const form = this.selectedForm();

    // GHL Connection resolution
    let ghlId = form?.ghlConnectionId ?? '';
    if (!ghlId && this.ghlConnections().length > 0) {
      ghlId = this.ghlConnections()[0].id;
    }
    this.selectedGhlConnectionId.set(ghlId);
    this.mappingsService.getFieldMappings(formId).subscribe((m) => this.fieldMappings.set(m));

    // Shopify Connection resolution
    let shopifyId = form?.shopifyConnectionId ?? '';
    if (!shopifyId && this.shopifyConnections().length > 0) {
      shopifyId = this.shopifyConnections()[0].id;
    }
    this.selectedShopifyConnectionId.set(shopifyId);
    this.mappingsService.getShopifyFieldMappings(formId).subscribe((m) => this.shopifyFieldMappings.set(m));

    this.loadFormFields(formId);
    if (ghlId) {
      this.loadGhlFields(ghlId);
    }
    if (shopifyId) {
      this.loadShopifyFields(shopifyId);
    }
  }

  onGhlConnectionChange(connId: string): void {
    this.selectedGhlConnectionId.set(connId);
    this.loadGhlFields(connId);
  }

  onShopifyConnectionChange(connId: string): void {
    this.selectedShopifyConnectionId.set(connId);
    this.loadShopifyFields(connId);
  }

  loadShopifyFields(connectionId?: string): void {
    this.isLoadingShopifyFields.set(true);
    this.shopifyService.getFields(connectionId).subscribe({
      next: (fields) => {
        this.shopifyCustomerFields.set(fields);
        this.isLoadingShopifyFields.set(false);
      },
      error: () => {
        this.isLoadingShopifyFields.set(false);
      },
    });
  }

  loadFormFields(formId: string): void {
    this.isLoadingFields.set(true);
    this.mappingsService.getFormFields(formId).subscribe({
      next: (fields) => {
        this.metaFormFields.set(fields);
        this.isLoadingFields.set(false);
      },
      error: () => {
        this.metaFormFields.set([]);
        this.isLoadingFields.set(false);
      },
    });
  }

  loadGhlFields(connectionId: string): void {
    if (!connectionId) {
      this.ghlLocationFields.set([]);
      return;
    }
    this.isLoadingGhlFields.set(true);
    this.ghlService.getLocationFields(connectionId).subscribe({
      next: (fields) => {
        this.ghlLocationFields.set(fields);
        this.isLoadingGhlFields.set(false);
      },
      error: () => {
        this.ghlLocationFields.set([]);
        this.isLoadingGhlFields.set(false);
      },
    });
  }

  // Meta Field Methods (GHL tab)
  openFieldDropdown(): void {
    this.isFieldDropdownOpen.set(true);
    if (this.highlightedMetaIndex() < 0 && this.filteredMetaFields().length > 0) {
      this.highlightedMetaIndex.set(0);
    }
  }

  toggleFieldDropdown(): void {
    if (this.isFieldDropdownOpen()) {
      this.isFieldDropdownOpen.set(false);
    } else {
      this.openFieldDropdown();
    }
  }

  onMetaInput(val: string): void {
    this.newFieldKey.set(val);
    this.openFieldDropdown();
    this.highlightedMetaIndex.set(0);
  }

  clearMetaInput(): void {
    this.newFieldKey.set('');
    this.openFieldDropdown();
    this.highlightedMetaIndex.set(0);
  }

  selectMetaField(field: MetaFormField): void {
    this.newFieldKey.set(field.key);
    this.isFieldDropdownOpen.set(false);
    this.highlightedMetaIndex.set(-1);
  }

  onMetaKeyDown(event: KeyboardEvent): void {
    const list = this.filteredMetaFields();
    if (!this.isFieldDropdownOpen()) {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter') {
        this.openFieldDropdown();
        return;
      }
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (list.length === 0) return;
      const next = (this.highlightedMetaIndex() + 1) % list.length;
      this.highlightedMetaIndex.set(next);
      this.scrollToHighlighted('meta', next);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (list.length === 0) return;
      const prev = this.highlightedMetaIndex() <= 0 ? list.length - 1 : this.highlightedMetaIndex() - 1;
      this.highlightedMetaIndex.set(prev);
      this.scrollToHighlighted('meta', prev);
    } else if (event.key === 'Enter') {
      const idx = this.highlightedMetaIndex();
      if (idx >= 0 && idx < list.length) {
        event.preventDefault();
        this.selectMetaField(list[idx]);
      }
    } else if (event.key === 'Escape') {
      this.isFieldDropdownOpen.set(false);
    }
  }

  // GHL Field Methods
  openGhlDropdown(): void {
    this.isGhlDropdownOpen.set(true);
    if (this.highlightedGhlIndex() < 0 && this.filteredGhlFields().length > 0) {
      this.highlightedGhlIndex.set(0);
    }
  }

  toggleGhlDropdown(): void {
    if (this.isGhlDropdownOpen()) {
      this.isGhlDropdownOpen.set(false);
    } else {
      this.openGhlDropdown();
    }
  }

  onGhlInput(val: string): void {
    this.newGhlFieldKey.set(val);
    this.openGhlDropdown();
    this.highlightedGhlIndex.set(0);
  }

  clearGhlInput(): void {
    this.newGhlFieldKey.set('');
    this.openGhlDropdown();
    this.highlightedGhlIndex.set(0);
  }

  setGhlCategory(category: 'All' | 'Contact' | 'Opportunity'): void {
    this.activeGhlCategory.set(category);
    this.highlightedGhlIndex.set(0);
  }

  selectGhlField(field: GhlFieldOption): void {
    this.newGhlFieldKey.set(field.key);
    if (field.category === 'Tag' || field.key === 'tags') {
      this.newTargetType = 2; // Tag
    } else if (field.isStandard && field.category === 'Contact') {
      this.newTargetType = 0; // StandardContactField
    } else {
      this.newTargetType = 1; // CustomField
    }
    this.isGhlDropdownOpen.set(false);
    this.highlightedGhlIndex.set(-1);
  }

  onGhlKeyDown(event: KeyboardEvent): void {
    const list = this.filteredGhlFields();
    if (!this.isGhlDropdownOpen()) {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter') {
        this.openGhlDropdown();
        return;
      }
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (list.length === 0) return;
      const next = (this.highlightedGhlIndex() + 1) % list.length;
      this.highlightedGhlIndex.set(next);
      this.scrollToHighlighted('ghl', next);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (list.length === 0) return;
      const prev = this.highlightedGhlIndex() <= 0 ? list.length - 1 : this.highlightedGhlIndex() - 1;
      this.highlightedGhlIndex.set(prev);
      this.scrollToHighlighted('ghl', prev);
    } else if (event.key === 'Enter') {
      const idx = this.highlightedGhlIndex();
      if (idx >= 0 && idx < list.length) {
        event.preventDefault();
        this.selectGhlField(list[idx]);
      }
    } else if (event.key === 'Escape') {
      this.isGhlDropdownOpen.set(false);
    }
  }

  // Shopify Meta autocomplete methods
  openShopifyMetaDropdown(): void {
    this.isShopifyMetaDropdownOpen.set(true);
    if (this.highlightedShopifyMetaIndex() < 0 && this.filteredShopifyMetaFields().length > 0) {
      this.highlightedShopifyMetaIndex.set(0);
    }
  }

  toggleShopifyMetaDropdown(): void {
    if (this.isShopifyMetaDropdownOpen()) {
      this.isShopifyMetaDropdownOpen.set(false);
    } else {
      this.openShopifyMetaDropdown();
    }
  }

  onShopifyMetaInput(val: string): void {
    this.newShopifyMetaFieldKey.set(val);
    this.openShopifyMetaDropdown();
    this.highlightedShopifyMetaIndex.set(0);
  }

  clearShopifyMetaInput(): void {
    this.newShopifyMetaFieldKey.set('');
    this.openShopifyMetaDropdown();
    this.highlightedShopifyMetaIndex.set(0);
  }

  selectShopifyMetaField(field: MetaFormField): void {
    this.newShopifyMetaFieldKey.set(field.key);
    this.isShopifyMetaDropdownOpen.set(false);
    this.highlightedShopifyMetaIndex.set(-1);
  }

  // Shopify Customer fields autocomplete methods
  openShopifyDropdown(): void {
    this.isShopifyDropdownOpen.set(true);
    if (this.highlightedShopifyIndex() < 0 && this.filteredShopifyFields().length > 0) {
      this.highlightedShopifyIndex.set(0);
    }
  }

  toggleShopifyDropdown(): void {
    if (this.isShopifyDropdownOpen()) {
      this.isShopifyDropdownOpen.set(false);
    } else {
      this.openShopifyDropdown();
    }
  }

  onShopifyInput(val: string): void {
    this.newShopifyFieldKey.set(val);
    this.openShopifyDropdown();
    this.highlightedShopifyIndex.set(0);
  }

  clearShopifyInput(): void {
    this.newShopifyFieldKey.set('');
    this.openShopifyDropdown();
    this.highlightedShopifyIndex.set(0);
  }

  setShopifyCategory(cat: 'All' | 'Standard' | 'Custom'): void {
    this.activeShopifyCategory.set(cat);
    this.highlightedShopifyIndex.set(-1);
  }

  selectShopifyField(field: ShopifyFieldOption): void {
    this.newShopifyFieldKey.set(field.key);
    if (field.isStandard) {
      if (field.key === 'tags') {
        this.newShopifyTargetType = 1; // Tag
      } else if (field.key === 'note') {
        this.newShopifyTargetType = 2; // Note
      } else {
        this.newShopifyTargetType = 0; // StandardCustomerField
      }
    } else {
      this.newShopifyTargetType = 3; // CustomField
    }
    this.isShopifyDropdownOpen.set(false);
    this.highlightedShopifyIndex.set(-1);
  }

  selectCustomShopifyField(key: string): void {
    if (!key) return;
    this.newShopifyFieldKey.set(key);
    this.newShopifyTargetType = 3; // CustomField
    this.isShopifyDropdownOpen.set(false);
    this.highlightedShopifyIndex.set(-1);
  }

  private scrollToHighlighted(prefix: 'meta' | 'ghl', index: number): void {
    setTimeout(() => {
      const el = document.getElementById(`${prefix}-opt-${index}`);
      if (el) {
        el.scrollIntoView({ block: 'nearest' });
      }
    }, 0);
  }

  saveGhlConnection(): void {
    const formId = this.selectedFormId();
    const ghlId = this.selectedGhlConnectionId();
    if (!formId || !ghlId) return;

    this.isSavingLocation.set(true);
    this.mappingsService.setFormGhlConnection(formId, ghlId).subscribe({
      next: () => {
        this.isSavingLocation.set(false);
        const form = this.selectedForm();
        if (form) form.ghlConnectionId = ghlId;
        this.loadGhlFields(ghlId);
      },
      error: () => this.isSavingLocation.set(false),
    });
  }

  saveShopifyConnection(): void {
    const formId = this.selectedFormId();
    const shopifyId = this.selectedShopifyConnectionId();
    if (!formId || !shopifyId) return;

    this.isSavingShopifyConnection.set(true);
    this.mappingsService.setFormShopifyConnection(formId, shopifyId).subscribe({
      next: () => {
        this.isSavingShopifyConnection.set(false);
        const form = this.selectedForm();
        if (form) form.shopifyConnectionId = shopifyId;
      },
      error: () => this.isSavingShopifyConnection.set(false),
    });
  }

  addFieldMapping(): void {
    const formId = this.selectedFormId();
    const fieldKey = this.newFieldKey().trim();
    const ghlKey = this.newGhlFieldKey().trim();
    if (!formId || !fieldKey || !ghlKey) return;

    this.mappingsService
      .upsertFieldMapping({ metaLeadFormId: formId, metaFieldKey: fieldKey, targetType: this.newTargetType, ghlFieldKey: ghlKey })
      .subscribe((mapping) => {
        this.fieldMappings.update((list) => [...list.filter((m) => m.metaFieldKey !== mapping.metaFieldKey), mapping]);
        this.newFieldKey.set('');
        this.newGhlFieldKey.set('');
        this.newTargetType = 0;
        this.isFieldDropdownOpen.set(false);
        this.isGhlDropdownOpen.set(false);
      });
  }

  deleteFieldMapping(mapping: FieldMapping): void {
    this.mappingsService.deleteFieldMapping(mapping.id).subscribe(() => {
      this.fieldMappings.update((list) => list.filter((m) => m.id !== mapping.id));
    });
  }

  addShopifyFieldMapping(): void {
    const formId = this.selectedFormId();
    const fieldKey = this.newShopifyMetaFieldKey().trim();
    const shopifyKey = this.newShopifyFieldKey().trim();
    if (!formId || !fieldKey || !shopifyKey) return;

    this.mappingsService
      .upsertShopifyFieldMapping({
        metaLeadFormId: formId,
        metaFieldKey: fieldKey,
        targetType: this.newShopifyTargetType,
        shopifyFieldKey: shopifyKey,
      })
      .subscribe((mapping) => {
        this.shopifyFieldMappings.update((list) => [...list.filter((m) => m.metaFieldKey !== mapping.metaFieldKey), mapping]);
        this.newShopifyMetaFieldKey.set('');
        this.newShopifyFieldKey.set('');
        this.newShopifyTargetType = 0;
        this.isShopifyMetaDropdownOpen.set(false);
        this.isShopifyDropdownOpen.set(false);
      });
  }

  deleteShopifyFieldMapping(mapping: ShopifyFieldMapping): void {
    this.mappingsService.deleteShopifyFieldMapping(mapping.id).subscribe(() => {
      this.shopifyFieldMappings.update((list) => list.filter((m) => m.id !== mapping.id));
    });
  }
}
