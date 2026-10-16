CREATE SCHEMA IF NOT EXISTS CommentsDb DEFAULT CHARACTER SET utf8mb4;
USE CommentsDb;

CREATE TABLE Users (
  Id        INT          NOT NULL AUTO_INCREMENT,
  UserName  VARCHAR(50)  NOT NULL,
  Email     VARCHAR(254) NOT NULL,
  HomePage  VARCHAR(2048) NULL,
  CreatedAt DATETIME(6)  NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Users_UserName_Email (UserName, Email)
) ENGINE = InnoDB;

CREATE TABLE Comments (
  Id        INT           NOT NULL AUTO_INCREMENT,
  Text      VARCHAR(5000) NOT NULL,
  CreatedAt DATETIME(6)   NOT NULL,
  IpAddress VARCHAR(45)   NOT NULL,
  UserAgent VARCHAR(512)  NOT NULL,
  UserId    INT           NOT NULL,
  ParentId  INT           NULL,
  PRIMARY KEY (Id),
  KEY IX_Comments_ParentId (ParentId),
  KEY IX_Comments_CreatedAt (CreatedAt),
  KEY IX_Comments_UserId (UserId),
  CONSTRAINT FK_Comments_Users FOREIGN KEY (UserId)
    REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Comments_Parent FOREIGN KEY (ParentId)
    REFERENCES Comments (Id) ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE TABLE Attachments (
  Id               INT          NOT NULL AUTO_INCREMENT,
  Type             INT          NOT NULL COMMENT '1 = image, 2 = text',
  OriginalFileName VARCHAR(255) NOT NULL,
  StoredFileName   VARCHAR(100) NOT NULL,
  SizeBytes        BIGINT       NOT NULL,
  CommentId        INT          NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Attachments_CommentId (CommentId),
  CONSTRAINT FK_Attachments_Comments FOREIGN KEY (CommentId)
    REFERENCES Comments (Id) ON DELETE CASCADE
) ENGINE = InnoDB;