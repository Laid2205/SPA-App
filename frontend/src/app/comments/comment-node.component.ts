import { DatePipe } from '@angular/common';
import { Component, input, signal } from '@angular/core';
import { CommentDto } from '../models/comment.models';
import { CommentFormComponent } from './comment-form.component';

@Component({
  selector: 'app-comment-node',
  imports: [DatePipe, CommentFormComponent],
  templateUrl: './comment-node.component.html',
  styleUrl: './comment-node.component.css'
})
export class CommentNodeComponent {
  readonly comment = input.required<CommentDto>();

  /** У корневых комментариев шапка (имя, дата) уже есть в колонках таблицы */
  readonly showHeader = input(true);

  readonly replying = signal(false);
}
