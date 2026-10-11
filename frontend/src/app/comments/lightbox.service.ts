import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';

export interface LightboxItem {
  kind: 'image' | 'text';
  src: string;
  title: string;
}

@Injectable({ providedIn: 'root' })
export class LightboxService {
  private readonly http = inject(HttpClient);
  private subscription?: Subscription;

  readonly item = signal<LightboxItem | null>(null);
  readonly text = signal<string | null>(null);
  readonly textFailed = signal(false);

  open(item: LightboxItem): void {
    this.subscription?.unsubscribe();
    this.text.set(null);
    this.textFailed.set(false);
    this.item.set(item);

    if (item.kind === 'text') {
      this.subscription = this.http.get(item.src, { responseType: 'text' }).subscribe({
        next: content => this.text.set(content),
        error: () => this.textFailed.set(true)
      });
    }
  }

  close(): void {
    this.subscription?.unsubscribe();
    this.item.set(null);
  }
}
