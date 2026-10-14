import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { CommentCreatedNotice } from '../models/comment.models';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  readonly commentCreated$ = new Subject<CommentCreatedNotice>();

  readonly resync$ = new Subject<void>();

  private connection?: HubConnection;
  private hasConnectedBefore = false;

  start(): void {
    if (this.connection) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/comments')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('commentCreated', (notice: CommentCreatedNotice) =>
      this.commentCreated$.next(notice));

    connection.onreconnected(() => this.resync$.next());

    connection.onclose(() => void this.connect());

    this.connection = connection;
    void this.connect();
  }

  private async connect(): Promise<void> {
    const connection = this.connection;
    if (!connection) return;

    while (true) {
      try {
        await connection.start();
        if (this.hasConnectedBefore) this.resync$.next();
        this.hasConnectedBefore = true;
        return;
      } catch {
        await new Promise(resolve => setTimeout(resolve, 5000));
      }
    }
  }
}
