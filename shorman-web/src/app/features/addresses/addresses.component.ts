import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgFor, NgIf } from '@angular/common';
import { AddressService } from '../../core/services/address.service';
import { Address } from '../../core/models/address.model';

@Component({
  selector: 'app-addresses',
  standalone: true,
  imports: [ReactiveFormsModule, NgFor, NgIf],
  templateUrl: './addresses.component.html',
  styleUrls: ['./addresses.component.scss']
})
export class AddressesComponent implements OnInit {
  private fb = inject(FormBuilder);
  private addressService = inject(AddressService);
  private cdr = inject(ChangeDetectorRef);

  addresses: Address[] = [];
  loading = true;
  error: string | null = null;
  showForm = false;
  saving = false;
  editingId: number | null = null;
  deleteConfirm: Address | null = null;

  form = this.fb.nonNullable.group({
    label: [''],
    street: ['', [Validators.required, Validators.minLength(2)]],
    houseNumber: ['', [Validators.required]],
    postalCode: ['', [Validators.required, Validators.pattern(/^\d{5}$/)]],
    city: ['', [Validators.required, Validators.minLength(2)]],
    country: ['Germany', Validators.required],
    isDefault: [false]
  });

  ngOnInit(): void {
    this.loadAddresses();
  }

  loadAddresses(): void {
    this.loading = true;
    this.error = null;

    this.addressService.getAddresses().subscribe({
      next: (addresses) => {
        this.addresses = Array.isArray(addresses) ? addresses : [];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Failed to load addresses. Please try again.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  startAdd(): void {
    this.editingId = null;
    this.showForm = true;
    this.form.reset({
      label: '',
      street: '',
      houseNumber: '',
      postalCode: '',
      city: '',
      country: 'Germany',
      isDefault: false
    });
  }

  startEdit(address: Address): void {
    this.editingId = address.id;
    this.showForm = true;
    this.form.patchValue({
      label: address.label || '',
      street: address.street,
      houseNumber: address.houseNumber,
      postalCode: address.postalCode,
      city: address.city,
      country: address.country,
      isDefault: address.isDefault
    });

    setTimeout(() => {
      document.querySelector('.form-section')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 100);
  }

  cancelForm(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset({
      label: '',
      street: '',
      houseNumber: '',
      postalCode: '',
      city: '',
      country: 'Germany',
      isDefault: false
    });
  }

  save(): void {
    if (this.form.invalid) {
      Object.keys(this.form.controls).forEach((key) => {
        this.form.get(key)?.markAsTouched();
      });
      return;
    }

    this.saving = true;
    const formValue = this.form.getRawValue();
    const operation = this.editingId
      ? this.addressService.updateAddress(this.editingId, formValue)
      : this.addressService.addAddress(formValue);

    operation.subscribe({
      next: (address) => {
        if (this.editingId) {
          const index = this.addresses.findIndex((item) => item.id === this.editingId);
          if (index !== -1) {
            this.addresses[index] = address;
            if (address.isDefault) {
              this.addresses = this.addresses.map((item) =>
                item.id === address.id ? item : { ...item, isDefault: false }
              );
            }
          }
        } else {
          this.addresses.push(address);
          if (address.isDefault) {
            this.addresses = this.addresses.map((item) =>
              item.id === address.id ? item : { ...item, isDefault: false }
            );
          }
        }

        this.showForm = false;
        this.editingId = null;
        this.saving = false;
        this.form.reset({
          label: '',
          street: '',
          houseNumber: '',
          postalCode: '',
          city: '',
          country: 'Germany',
          isDefault: false
        });
      },
      error: () => {
        this.error = 'Failed to save address. Please try again.';
        this.saving = false;
      }
    });
  }

  setDefault(id: number): void {
    this.addressService.setDefault(id).subscribe({
      next: () => {
        this.addresses = this.addresses.map((address) => ({
          ...address,
          isDefault: address.id === id
        }));
      },
      error: () => {
        this.error = 'Failed to set default address. Please try again.';
      }
    });
  }

  confirmDelete(address: Address): void {
    this.deleteConfirm = address;
  }

  deleteAddress(): void {
    if (!this.deleteConfirm) {
      return;
    }

    const id = this.deleteConfirm.id;
    this.deleteConfirm = null;

    this.addressService.deleteAddress(id).subscribe({
      next: () => {
        this.addresses = this.addresses.filter((address) => address.id !== id);
        if (this.addresses.length === 0) {
          this.showForm = false;
        }
      },
      error: () => {
        this.error = 'Failed to delete address. Please try again.';
      }
    });
  }
}
