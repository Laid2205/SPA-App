import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CommentDto, CommentQuery, PagedResult } from '../models/comment.models';

@Injectable({ providedIn: 'root' })
export class CommentsApiService {
  private readonly http = inject(HttpClient);

  getPage(query: CommentQuery): Observable<PagedResult<CommentDto>> {
    const params = new HttpParams()
      .set('page', query.page)
      .set('sortBy', query.sortBy)
      .set('desc', query.desc);

    return this.http.get<PagedResult<CommentDto>>('/api/comments', { params });
  }
}
