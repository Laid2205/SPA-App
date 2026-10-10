import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommentsListComponent } from './comments/comments-list.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, CommentsListComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('frontend');
}
