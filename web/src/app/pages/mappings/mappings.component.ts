import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MetaService } from '../../core/services/meta.service';
import { GhlService } from '../../core/services/ghl.service';
import { MappingsService } from '../../core/services/mappings.service';
import { MetaConnection, MetaLeadForm } from '../../core/models/meta.models';
import { GhlConnection } from '../../core/models/ghl.models';
import { FieldMapping, GhlTargetFieldType, GhlTargetFieldTypeLabels } from '../../core/models/mapping.models';

interface FormOption {
  form: MetaLeadForm;
  pageName: string;
}

@Component({
  selector: 'app-mappings',
  standalone: true,
  imports: [CommonModule, FormsModule],
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

  readonly targetTypeLabels = GhlTargetFieldTypeLabels;
  readonly targetTypes: GhlTargetFieldType[] = [0, 1, 2];

  newFieldKey = '';
  newTargetType: GhlTargetFieldType = 0;
  newGhlFieldKey = '';

  readonly selectedForm = computed(() => this.formOptions().find((f) => f.form.id === this.selectedFormId())?.form ?? null);

  constructor(private metaService: MetaService, private ghlService: GhlService, private mappingsService: MappingsService) {}

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
    this.selectedGhlConnectionId.set(this.selectedForm()?.ghlConnectionId ?? '');
    this.mappingsService.getFieldMappings(formId).subscribe((m) => this.fieldMappings.set(m));
  }

  saveGhlConnection(): void {
    const formId = this.selectedFormId();
    if (!formId || !this.selectedGhlConnectionId()) return;

    this.isSavingLocation.set(true);
    this.mappingsService.setFormGhlConnection(formId, this.selectedGhlConnectionId()).subscribe({
      next: () => {
        this.isSavingLocation.set(false);
        const form = this.selectedForm();
        if (form) form.ghlConnectionId = this.selectedGhlConnectionId();
      },
      error: () => this.isSavingLocation.set(false),
    });
  }

  addFieldMapping(): void {
    const formId = this.selectedFormId();
    if (!formId || !this.newFieldKey || !this.newGhlFieldKey) return;

    this.mappingsService
      .upsertFieldMapping({ metaLeadFormId: formId, metaFieldKey: this.newFieldKey, targetType: this.newTargetType, ghlFieldKey: this.newGhlFieldKey })
      .subscribe((mapping) => {
        this.fieldMappings.update((list) => [...list.filter((m) => m.metaFieldKey !== mapping.metaFieldKey), mapping]);
        this.newFieldKey = '';
        this.newGhlFieldKey = '';
        this.newTargetType = 0;
      });
  }

  deleteFieldMapping(mapping: FieldMapping): void {
    this.mappingsService.deleteFieldMapping(mapping.id).subscribe(() => {
      this.fieldMappings.update((list) => list.filter((m) => m.id !== mapping.id));
    });
  }
}
