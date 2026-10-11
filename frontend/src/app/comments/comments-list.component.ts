import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { Subscription } from 'rxjs';
import { CommentDto, PagedResult, SortField } from '../models/comment.models';
import { CommentsApiService } from '../services/comments-api.service';
import { CommentNodeComponent } from './comment-node.component';

@Component({
  selector: 'app-comments-list',
  imports: [DatePipe, CommentNodeComponent],
  templateUrl: './comments-list.component.html',
  styleUrl: './comments-list.component.css'
})
export class CommentsListComponent implements OnInit, OnDestroy {
  private readonly api = inject(CommentsApiService);
  private readonly destroyRef = inject(DestroyRef);
  private subscription?: Subscription;

  readonly data = signal<PagedResult<CommentDto> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly page = signal(1);
  readonly sortBy = signal<SortField>('date');
  readonly desc = signal(true);

  readonly visiblePages = computed(() => {
    const total = this.data()?.totalPages ?? 0;
    const start = Math.max(1, Math.min(this.page() - 2, total - 4));
    const end = Math.min(total, start + 4);
    return Array.from({ length: Math.max(0, end - start + 1) }, (_, i) => start + i);
  });

  ngOnInit(): void {
    this.load();

    this.api.changed$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  sort(field: SortField): void {
    if (this.sortBy() === field) {
      this.desc.update(d => !d);
    } else {
      this.sortBy.set(field);
      this.desc.set(field === 'date');
    }
    this.page.set(1);
    this.load();
  }

  goTo(page: number): void {
    const total = this.data()?.totalPages ?? 1;
    if (page < 1 || page > total || page === this.page()) return;
    this.page.set(page);
    this.load();
  }

  indicator(field: SortField): string {
    if (this.sortBy() !== field) return '';
    return this.desc() ? '▼' : '▲';
  }

  ariaSort(field: SortField): 'ascending' | 'descending' | 'none' {
    if (this.sortBy() !== field) return 'none';
    return this.desc() ? 'descending' : 'ascending';
  }

  private load(): void {
    this.subscription?.unsubscribe();
    this.loading.set(true);
    this.error.set(null);

    this.subscription = this.api
      .getPage({ page: this.page(), sortBy: this.sortBy(), desc: this.desc() })
      .subscribe({
        next: result => {
          this.data.set(result);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Не удалось загрузить комментарии. Проверьте, что API запущен.');
          this.loading.set(false);
        }
      });
  }
}
