export type AttachmentType = 'Image' | 'Text';

export interface AttachmentDto {
  id: number;
  type: AttachmentType;
  fileName: string;
}

export interface CommentDto {
  id: number;
  parentId: number | null;
  userName: string;
  email: string;
  homePage: string | null;
  text: string;
  createdAt: string;
  attachment: AttachmentDto | null;
  replies: CommentDto[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type SortField = 'username' | 'email' | 'date';

export interface CommentQuery {
  page: number;
  sortBy: SortField;
  desc: boolean;
}

export interface CaptchaChallenge {
  id: string;
  image: string;
}

export interface CreateCommentPayload {
  userName: string;
  email: string;
  homePage: string;
  text: string;
  parentId: number | null;
  captchaId: string;
  captchaAnswer: string;
  file: File | null;
}

export interface CommentCreatedNotice {
  commentId: number;
  parentId: number | null;
  createdAt: string;
}
