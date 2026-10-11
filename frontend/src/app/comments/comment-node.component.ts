import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { AttachmentDto, CommentDto } from '../models/comment.models';
import { CommentFormComponent } from './comment-form.component';
import { LightboxService } from './lightbox.service';

@Component({
  selector: 'app-comment-node',
  imports: [DatePipe, CommentFormComponent],
  templateUrl: './comment-node.component.html',
  styleUrl: './comment-node.component.css'
})
export class CommentNodeComponent {
  private readonly lightbox = inject(LightboxService);

  readonly comment = input.required<CommentDto>();

  readonly showHeader = input(true);

  readonly replying = signal(false);

  openImage(event: MouseEvent, attachment: AttachmentDto): void {
    this.openInLightbox(event, 'image', attachment);
  }

  openText(event: MouseEvent, attachment: AttachmentDto): void {
    this.openInLightbox(event, 'text', attachment);
  }

  private openInLightbox(event: MouseEvent, kind: 'image' | 'text', attachment: AttachmentDto): void {
    if (event.ctrlKey || event.metaKey || event.shiftKey) return;

    event.preventDefault();
    this.lightbox.open({
      kind,
      src: `/api/attachments/${attachment.id}`,
      title: attachment.fileName
    });
  }
}
