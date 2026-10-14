import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, Subject, tap } from 'rxjs';
import {
  CaptchaChallenge,
  CommentDto,
  CommentQuery,
  CreateCommentPayload,
  PagedResult
} from '../models/comment.models';

@Injectable({ providedIn: 'root' })
export class CommentsApiService {
  private readonly http = inject(HttpClient);

  readonly changed$ = new Subject<void>();

  private readonly ownCommentIds = new Set<number>();

  isOwn(commentId: number): boolean {
    return this.ownCommentIds.has(commentId);
  }

  getPage(query: CommentQuery): Observable<PagedResult<CommentDto>> {
    const params = new HttpParams()
      .set('page', query.page)
      .set('sortBy', query.sortBy)
      .set('desc', query.desc);

    return this.http.get<PagedResult<CommentDto>>('/api/comments', { params });
  }

  getCaptcha(): Observable<CaptchaChallenge> {
    return this.http.get<CaptchaChallenge>('/api/captcha');
  }

  preview(text: string): Observable<{ html: string }> {
    return this.http.post<{ html: string }>('/api/comments/preview', { text });
  }

  create(payload: CreateCommentPayload): Observable<CommentDto> {
    const body = new FormData();
    body.append('userName', payload.userName);
    body.append('email', payload.email);
    if (payload.homePage) body.append('homePage', payload.homePage);
    body.append('text', payload.text);
    if (payload.parentId !== null) body.append('parentId', String(payload.parentId));
    body.append('captchaId', payload.captchaId);
    body.append('captchaAnswer', payload.captchaAnswer);
    if (payload.file) body.append('file', payload.file);

    return this.http.post<CommentDto>('/api/comments', body).pipe(
      tap(created => {
        this.ownCommentIds.add(created.id);
        this.changed$.next();
      })
    );
  }
}
