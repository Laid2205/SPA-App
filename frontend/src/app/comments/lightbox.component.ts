import { Component, inject } from '@angular/core';
import { LightboxService } from './lightbox.service';

@Component({
  selector: 'app-lightbox',
  host: { '(document:keydown.escape)': 'lightbox.close()' },
  template: `
    @if (lightbox.item(); as item) {
      <div class="backdrop" role="dialog" aria-modal="true" [attr.aria-label]="item.title"
           (click)="lightbox.close()">
        <div class="box" (click)="$event.stopPropagation()">
          <button type="button" class="close" aria-label="Закрыть" (click)="lightbox.close()">×</button>

          @if (item.kind === 'image') {
            <img [src]="item.src" [alt]="item.title" />
          } @else {
            @if (lightbox.textFailed()) {
              <p class="status">Не удалось загрузить файл.</p>
            } @else if (lightbox.text() === null) {
              <p class="status">Загрузка…</p>
            } @else {
              <pre>{{ lightbox.text() }}</pre>
            }
          }

          <div class="caption">{{ item.title }}</div>
        </div>
      </div>
    }
  `,
  styles: [`
    .backdrop {
      position: fixed;
      inset: 0;
      z-index: 1000;
      display: grid;
      place-items: center;
      padding: 24px;
      background: rgba(0, 0, 0, 0.75);
      animation: fade-in 0.2s ease-out;
    }

    .box {
      position: relative;
      max-width: 100%;
      padding: 12px;
      background: #fff;
      border-radius: 8px;
      font-family: system-ui, sans-serif;
      animation: pop-in 0.25s ease-out;
    }

    img {
      display: block;
      max-width: 90vw;
      max-height: 75vh;
      border-radius: 4px;
    }

    pre {
      width: min(90vw, 720px);
      max-height: 65vh;
      margin: 0;
      overflow: auto;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      font: 13px/1.5 ui-monospace, Consolas, monospace;
      color: #1f2430;
    }

    .status {
      margin: 0;
      padding: 24px;
      color: #7a7f8c;
    }

    .caption {
      margin-top: 8px;
      font-size: 13px;
      color: #5a5f6d;
      overflow-wrap: anywhere;
    }

    .close {
      position: absolute;
      top: -14px;
      right: -14px;
      width: 32px;
      height: 32px;
      border: 0;
      border-radius: 50%;
      background: #1f2430;
      color: #fff;
      font-size: 20px;
      line-height: 1;
      cursor: pointer;
    }

    .close:hover,
    .close:focus-visible {
      background: #3a54d6;
    }

    @keyframes fade-in {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    @keyframes pop-in {
      from { opacity: 0; transform: scale(0.92); }
      to { opacity: 1; transform: scale(1); }
    }

    @media (prefers-reduced-motion: reduce) {
      .backdrop, .box { animation: none; }
    }
  `]
})
export class LightboxComponent {
  readonly lightbox = inject(LightboxService);
}
