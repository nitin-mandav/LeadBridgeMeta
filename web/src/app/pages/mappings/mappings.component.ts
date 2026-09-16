import { Component, OnInit, computed, signal, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MetaService } from '../../core/services/meta.service';
import { GhlService } from '../../core/services/ghl.service';
import { MappingsService, MetaFormField } from '../../core/services/mappings.service';
import { MetaConnection, MetaLeadForm } from '../../core/models/meta.models';
import { GhlConnection, GhlFieldOption } from '../../core/models/ghl.models';
import { FieldMapping, GhlTargetFieldType, GhlTargetFieldTypeLabels } from '../../core/models/mapping.models';

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
  readonly selectedFormId = signal<string | null>(null);
  readonly selectedGhlConnectionId = signal<string>('');
  readonly fieldMappings = signal<FieldMapping[]>([]);
  readonly isSavingLocation = signal(false);

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

  readonly selectedForm = computed(() => this.formOptions().find((f) => f.form.id === this.selectedFormId())?.form ?? null);

  readonly filteredMetaFields = computed(() => {
    const search = this.newFieldKey().trim().toLowerCase();
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

  readonly ghlTotalCount = computed(() => this.ghlLocationFields().length);
  readonly ghlContactCount = computed(() => this.ghlLocationFields().filter((f) => f.category === 'Contact').length);
  readonly ghlOpportunityCount = computed(() => this.ghlLocationFields().filter((f) => f.category === 'Opportunity').length);

  constructor(private metaService: MetaService, private ghlService: GhlService, private mappingsService: MappingsService) { }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.meta-field-autocomplete')) {
      this.isFieldDropdownOpen.set(false);
    }
    if (!target.closest('.ghl-field-autocomplete')) {
      this.isGhlDropdownOpen.set(false);
    }
  }

  ngOnInit(): void {
    this.ghlService.getConnections().subscribe((c) => this.ghlConnections.set(c));

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
    const ghlId = this.selectedForm()?.ghlConnectionId ?? '';
    this.selectedGhlConnectionId.set(ghlId);
    this.mappingsService.getFieldMappings(formId).subscribe((m) => this.fieldMappings.set(m));

    this.loadFormFields(formId);
    this.loadGhlFields(ghlId);
  }

  onGhlConnectionChange(connId: string): void {
    this.selectedGhlConnectionId.set(connId);
    this.loadGhlFields(connId);
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

  // Meta Field Methods
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
}
