import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { NgFor, NgIf } from '@angular/common';
import { AddressService } from '../../core/services/address.service';
import { Address } from '../../core/models/address.model';

@Component({
  selector: 'app-addresses',
  standalone: true,
  imports: [ReactiveFormsModule, NgFor, NgIf],
  template: `
    <div class="addr-page">
      <div class="container">
        <h1>My Addresses</h1>
        <div *ngIf="loading" class="loading">Loading…</div>
        <div class="addr-list">
          <div *ngFor="let addr of addresses" class="addr-card" [class.default]="addr.isDefault">
            <div class="addr-top">
              <strong>{{ addr.label || 'Address' }}</strong>
              <span class="default-tag" *ngIf="addr.isDefault">Default</span>
            </div>
            <div class="addr-text">{{ addr.street }} {{ addr.houseNumber }}, {{ addr.postalCode }} {{ addr.city }}, {{ addr.country }}</div>
            <div class="addr-actions">
              <button *ngIf="!addr.isDefault" (click)="setDefault(addr.id)" class="btn-default">Set Default</button>
              <button (click)="delete(addr.id)" class="btn-delete">Delete</button>
            </div>
          </div>
        </div>
        <div class="add-section">
          <button class="btn-toggle" (click)="showForm = !showForm">{{ showForm ? '✕ Cancel' : '+ Add Address' }}</button>
          <form *ngIf="showForm" [formGroup]="form" (ngSubmit)="save()" class="addr-form">
            <div class="form-row">
              <div class="fg"><label>Label</label><input formControlName="label" class="fc" placeholder="Home" /></div>
              <div class="fg"><label>Postal Code *</label><input formControlName="postalCode" class="fc" /></div>
            </div>
            <div class="form-row">
              <div class="fg"><label>Street *</label><input formControlName="street" class="fc" /></div>
              <div class="fg"><label>House No. *</label><input formControlName="houseNumber" class="fc" /></div>
            </div>
            <div class="form-row">
              <div class="fg"><label>City *</label><input formControlName="city" class="fc" /></div>
              <div class="fg"><label>Country *</label><input formControlName="country" class="fc" /></div>
            </div>
            <div class="fg-check"><label><input type="checkbox" formControlName="isDefault" /> Set as default</label></div>
            <button type="submit" class="btn-save" [disabled]="saving">{{ saving ? 'Saving…' : 'Save Address' }}</button>
          </form>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .addr-page { background: #F9FAFB; min-height: calc(100vh - 128px); padding: 2rem 0; }
    .container { max-width: 800px; margin: 0 auto; padding: 0 1rem; }
    h1 { font-size: 1.75rem; font-weight: 800; margin-bottom: 1.5rem; }
    .loading { color: #999; text-align: center; padding: 3rem; }
    .addr-list { display: flex; flex-direction: column; gap: 1rem; margin-bottom: 1.5rem; }
    .addr-card { background: #fff; border-radius: 12px; padding: 1.25rem; box-shadow: 0 1px 4px rgba(0,0,0,0.07); border: 2px solid transparent; }
    .addr-card.default { border-color: #2E7D32; }
    .addr-top { display: flex; gap: 0.5rem; align-items: center; margin-bottom: 0.4rem; }
    .default-tag { background: #E8F5E9; color: #2E7D32; font-size: 0.72rem; padding: 0.1rem 0.5rem; border-radius: 20px; font-weight: 600; }
    .addr-text { font-size: 0.9rem; color: #555; margin-bottom: 0.75rem; }
    .addr-actions { display: flex; gap: 0.5rem; }
    .btn-default { background: #E8F5E9; color: #2E7D32; border: none; padding: 0.35rem 0.85rem; border-radius: 6px; cursor: pointer; font-size: 0.82rem; font-weight: 600; }
    .btn-delete { background: #FFEBEE; color: #C62828; border: none; padding: 0.35rem 0.85rem; border-radius: 6px; cursor: pointer; font-size: 0.82rem; font-weight: 600; }
    .btn-toggle { background: none; border: 1.5px dashed #2E7D32; color: #2E7D32; padding: 0.5rem 1rem; border-radius: 8px; cursor: pointer; font-weight: 600; margin-bottom: 1rem; }
    .addr-form { background: #fff; border-radius: 12px; padding: 1.25rem; box-shadow: 0 1px 4px rgba(0,0,0,0.07); }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .fg { margin-bottom: 1rem; }
    .fg label { display: block; font-size: 0.85rem; font-weight: 600; color: #555; margin-bottom: 0.3rem; }
    .fc { width: 100%; padding: 0.6rem 0.8rem; border: 1.5px solid #ddd; border-radius: 8px; font-size: 0.9rem; box-sizing: border-box; }
    .fc:focus { outline: none; border-color: #2E7D32; }
    .fg-check { margin-bottom: 1rem; font-size: 0.88rem; color: #555; }
    .btn-save { background: #2E7D32; color: #fff; border: none; padding: 0.65rem 1.5rem; border-radius: 8px; font-weight: 700; cursor: pointer; }
    .btn-save:disabled { opacity: 0.6; }
    @media (max-width: 500px) { .form-row { grid-template-columns: 1fr; } }
  `]
})
export class AddressesComponent implements OnInit {
  private fb = inject(FormBuilder);
  private addressService = inject(AddressService);
  addresses: Address[] = [];
  loading = true;
  showForm = false;
  saving = false;
  form = this.fb.nonNullable.group({
    label: [''], street: ['', Validators.required], houseNumber: ['', Validators.required],
    postalCode: ['', Validators.required], city: ['', Validators.required], country: ['Germany'], isDefault: [false]
  });
  ngOnInit() {
    this.addressService.getAddresses().subscribe({ next: a => { this.addresses = a; this.loading = false; }, error: () => { this.loading = false; } });
  }
  setDefault(id: number) {
    this.addressService.setDefault(id).subscribe(() => this.addresses = this.addresses.map(a => ({ ...a, isDefault: a.id === id })));
  }
  delete(id: number) {
    this.addressService.deleteAddress(id).subscribe(() => this.addresses = this.addresses.filter(a => a.id !== id));
  }
  save() {
    if (this.form.invalid) return;
    this.saving = true;
    this.addressService.addAddress(this.form.getRawValue()).subscribe({
      next: a => { this.addresses.push(a); this.showForm = false; this.saving = false; this.form.reset({ country: 'Germany' }); },
      error: () => { this.saving = false; }
    });
  }
}
