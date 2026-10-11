import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  inject,
  input,
  output,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, Subject, catchError, debounceTime, filter, map, of, switchMap, tap } from 'rxjs';
import { CaptchaChallenge } from '../models/comment.models';
import { CommentsApiService } from '../services/comments-api.service';

type FieldName = 'userName' | 'email' | 'homePage' | 'text' | 'captchaAnswer';
type FormatTag = 'i' | 'strong' | 'code' | 'a';
interface PreviewState {
  html: string | null;
  error: string | null;
}

const LATIN_DIGITS = /^[a-zA-Z0-9]+$/;
const EMAIL = /^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$/;
const URL_HTTP = /^https?:\/\/[^\s<>"'`]+$/;

const PATTERN_MESSAGES: Record<FieldName, string> = {
  userName: 'Только латинские буквы и цифры',
  email: 'Неверный формат e-mail',
  homePage: 'Ссылка должна начинаться с http:// или https:// и не содержать пробелов',
  text: 'Недопустимый текст',
  captchaAnswer: 'Только латинские буквы и цифры'
};

const SERVER_FIELDS: Record<string, FieldName | 'file'> = {
  username: 'userName',
  email: 'email',
  homepage: 'homePage',
  text: 'text',
  captchaanswer: 'captchaAnswer',
  captchaid: 'captchaAnswer',
  file: 'file'
};

@Component({
  selector: 'app-comment-form',
  imports: [ReactiveFormsModule],
  templateUrl: './comment-form.component.html',
  styleUrl: './comment-form.component.css'
})
export class CommentFormComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(CommentsApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly parentId = input<number | null>(null);
  readonly created = output<void>();
  readonly cancelled = output<void>();

  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private readonly textArea = viewChild<ElementRef<HTMLTextAreaElement>>('textArea');

  readonly form = this.fb.group({
    userName: ['', [Validators.required, Validators.maxLength(50), Validators.pattern(LATIN_DIGITS)]],
    email: ['', [Validators.required, Validators.maxLength(254), Validators.pattern(EMAIL)]],
    homePage: ['', [Validators.maxLength(2048), Validators.pattern(URL_HTTP)]],
    text: ['', [Validators.required, Validators.maxLength(5000)]],
    captchaAnswer: ['', [Validators.required, Validators.maxLength(8), Validators.pattern(LATIN_DIGITS)]]
  });

  readonly captcha = signal<CaptchaChallenge | null>(null);
  readonly file = signal<File | null>(null);
  readonly fileError = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  readonly submitting = signal(false);

  readonly previewOpen = signal(false);
  readonly previewLoading = signal(false);
  readonly preview = signal<PreviewState | null>(null);

  private readonly previewRequests$ = new Subject<string>();

  ngOnInit(): void {
    this.loadCaptcha();

    this.previewRequests$
      .pipe(
        switchMap(text => this.requestPreview(text)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(state => {
        this.preview.set(state);
        this.previewLoading.set(false);
      });

    this.form.controls.text.valueChanges
      .pipe(
        filter(() => this.previewOpen()),
        tap(() => this.previewLoading.set(true)),
        debounceTime(400),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(text => this.previewRequests$.next(text));
  }

  loadCaptcha(): void {
    this.form.controls.captchaAnswer.reset();
    this.api.getCaptcha().subscribe({
      next: challenge => this.captcha.set(challenge),
      error: () => {
        this.captcha.set(null);
        this.formError.set('Не удалось загрузить CAPTCHA. Проверьте, что API запущен.');
      }
    });
  }

  error(name: FieldName): string | null {
    const control = this.form.controls[name];
    if (!(control.touched || control.dirty) || !control.errors) return null;

    const errors = control.errors;
    if (errors['server']) return errors['server'];
    if (errors['required']) return 'Обязательное поле';
    if (errors['maxlength']) return `Не более ${errors['maxlength'].requiredLength} символов`;
    if (errors['pattern']) return PATTERN_MESSAGES[name];
    return 'Некорректное значение';
  }

  insertTag(tag: FormatTag): void {
    const area = this.textArea()?.nativeElement;
    if (!area) return;

    const start = area.selectionStart;
    const end = area.selectionEnd;
    const selected = area.value.slice(start, end);

    const open = tag === 'a' ? '<a href="https://" title="">' : `<${tag}>`;
    const close = `</${tag}>`;

    area.setRangeText(open + selected + close, start, end, 'end');
    area.focus();

    if (tag === 'a') {
      const hrefStart = start + '<a href="'.length;
      area.setSelectionRange(hrefStart, hrefStart + 'https://'.length);
    } else if (!selected) {
      const caret = start + open.length;
      area.setSelectionRange(caret, caret);
    }

    area.dispatchEvent(new Event('input', { bubbles: true }));
  }

  togglePreview(): void {
    const open = !this.previewOpen();
    this.previewOpen.set(open);

    if (open) {
      this.previewLoading.set(true);
      this.previewRequests$.next(this.form.controls.text.value);
    }
  }

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.file.set(null);
    this.fileError.set(null);
    if (!file) return;

    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    const isImage = ['jpg', 'jpeg', 'png', 'gif'].includes(ext);
    const isText = ext === 'txt';

    if (!isImage && !isText) {
      this.rejectFile('Допустимы файлы JPG, GIF, PNG и TXT');
    } else if (isText && file.size > 100 * 1024) {
      this.rejectFile('Текстовый файл не должен быть больше 100 КБ');
    } else if (isImage && file.size > 5 * 1024 * 1024) {
      this.rejectFile('Изображение не должно быть больше 5 МБ');
    } else {
      this.file.set(file);
    }
  }

  clearFile(): void {
    this.file.set(null);
    this.fileError.set(null);
    const input = this.fileInput()?.nativeElement;
    if (input) input.value = '';
  }

  submit(): void {
    this.formError.set(null);

    const challenge = this.captcha();
    if (this.form.invalid || this.fileError() || !challenge) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);

    this.api
      .create({
        ...value,
        parentId: this.parentId(),
        captchaId: challenge.id,
        file: this.file()
      })
      .subscribe({
        next: () => {
          this.previewOpen.set(false);
          this.preview.set(null);
          this.form.controls.text.reset();
          this.clearFile();
          this.loadCaptcha();
          this.submitting.set(false);
          this.created.emit();
        },
        error: (err: HttpErrorResponse) => {
          this.loadCaptcha();
          this.applyServerError(err);
          this.submitting.set(false);
        }
      });
  }

  private requestPreview(text: string): Observable<PreviewState> {
    if (!text.trim()) return of({ html: '', error: null });

    return this.api.preview(text).pipe(
      map(result => ({ html: result.html, error: null })),
      catchError((err: HttpErrorResponse) => of({ html: null, error: this.previewError(err) }))
    );
  }

  private previewError(err: HttpErrorResponse): string {
    const errors: Record<string, string[]> | undefined = err.error?.errors;

    if (err.status === 400 && errors) {
      const entry = Object.entries(errors).find(([key]) => key.toLowerCase() === 'text');
      if (entry) return entry[1].join(' ');
    }
    return 'Не удалось получить предпросмотр.';
  }

  private rejectFile(message: string): void {
    this.fileError.set(message);
    const input = this.fileInput()?.nativeElement;
    if (input) input.value = '';
  }

  private applyServerError(err: HttpErrorResponse): void {
    if (err.status === 0) {
      this.formError.set('Нет связи с сервером.');
      return;
    }
    if (err.status === 413) {
      this.fileError.set('Файл слишком большой');
      return;
    }
    if (err.status === 404) {
      this.formError.set('Комментарий, на который вы отвечаете, больше не существует.');
      return;
    }

    const errors: Record<string, string[]> | undefined = err.error?.errors;
    if (err.status !== 400 || !errors) {
      this.formError.set('Не удалось отправить комментарий. Попробуйте ещё раз.');
      return;
    }

    const unmatched: string[] = [];

    for (const [key, messages] of Object.entries(errors)) {
      const message = messages.join(' ');
      const field = SERVER_FIELDS[(key.split('.').pop() ?? '').toLowerCase()];

      if (field === 'file') {
        this.fileError.set(message);
      } else if (field) {
        const control = this.form.controls[field];
        control.setErrors({ server: message });
        control.markAsTouched();
      } else {
        unmatched.push(message);
      }
    }

    if (unmatched.length > 0) this.formError.set(unmatched.join(' '));
  }
}
