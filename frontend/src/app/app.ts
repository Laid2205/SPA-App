import { Component } from '@angular/core';
import { CommentFormComponent } from './comments/comment-form.component';
import { CommentsListComponent } from './comments/comments-list.component';
import { LightboxComponent } from './comments/lightbox.component';

@Component({
  selector: 'app-root',
  imports: [CommentFormComponent, CommentsListComponent, LightboxComponent],
  templateUrl: './app.html'
})
export class App { }
