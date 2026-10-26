-- Select the application database in SSMS before running this script.
-- The CLI executes this in DefaultConnection's database. No existing data is deleted.
SET XACT_ABORT ON;
IF DB_NAME() IN ('master','model','msdb','tempdb')
    THROW 51000, 'Select the SettleMate application database before initializing.', 1;
DECLARE @incompatible nvarchar(2048);
SELECT TOP (1) @incompatible = N'Existing schema is incompatible: missing ' + expected.TableName + N'.' + expected.ColumnName + N'. Preserve this database and use a fresh application database or a reviewed migration.'
FROM (VALUES
(N'dbo.Accounts',N'Id'),
(N'dbo.Accounts',N'FullName'),
(N'dbo.Accounts',N'Email'),
(N'dbo.Accounts',N'NormalizedEmail'),
(N'dbo.Accounts',N'Phone'),
(N'dbo.Accounts',N'PasswordHash'),
(N'dbo.Accounts',N'Role'),
(N'dbo.Accounts',N'Status'),
(N'dbo.Accounts',N'Reason'),
(N'dbo.Accounts',N'EmailVerified'),
(N'dbo.Accounts',N'SecurityStamp'),
(N'dbo.Accounts',N'DateOfBirth'),
(N'dbo.Accounts',N'Gender'),
(N'dbo.Accounts',N'Address'),
(N'dbo.Accounts',N'City'),
(N'dbo.Accounts',N'State'),
(N'dbo.Accounts',N'Pincode'),
(N'dbo.Accounts',N'Avatar'),
(N'dbo.Accounts',N'DocumentType'),
(N'dbo.Accounts',N'CreatedAt'),
(N'dbo.OtpChallenges',N'Id'),
(N'dbo.OtpChallenges',N'Email'),
(N'dbo.OtpChallenges',N'Purpose'),
(N'dbo.OtpChallenges',N'ActorId'),
(N'dbo.OtpChallenges',N'SessionHash'),
(N'dbo.OtpChallenges',N'CodeHash'),
(N'dbo.OtpChallenges',N'ExpiresAt'),
(N'dbo.OtpChallenges',N'Attempts'),
(N'dbo.OtpChallenges',N'IsUsed'),
(N'dbo.OtpChallenges',N'VerifiedAt'),
(N'dbo.OtpChallenges',N'ProofHash'),
(N'dbo.OtpChallenges',N'CreatedAt'),
(N'dbo.Listings',N'Id'),
(N'dbo.Listings',N'OwnerId'),
(N'dbo.Listings',N'Kind'),
(N'dbo.Listings',N'Title'),
(N'dbo.Listings',N'Description'),
(N'dbo.Listings',N'Type'),
(N'dbo.Listings',N'City'),
(N'dbo.Listings',N'State'),
(N'dbo.Listings',N'Address'),
(N'dbo.Listings',N'Price'),
(N'dbo.Listings',N'Deposit'),
(N'dbo.Listings',N'Status'),
(N'dbo.Listings',N'Available'),
(N'dbo.Listings',N'Reason'),
(N'dbo.Listings',N'Details'),
(N'dbo.Listings',N'CreatedAt'),
(N'dbo.Listings',N'UpdatedAt'),
(N'dbo.Files',N'Id'),
(N'dbo.Files',N'OwnerId'),
(N'dbo.Files',N'ListingId'),
(N'dbo.Files',N'Name'),
(N'dbo.Files',N'OriginalName'),
(N'dbo.Files',N'ContentType'),
(N'dbo.Files',N'Private'),
(N'dbo.Files',N'Purpose'),
(N'dbo.Files',N'CreatedAt'),
(N'dbo.Wishlists',N'AccountId'),
(N'dbo.Wishlists',N'ListingId'),
(N'dbo.VisitRequests',N'Id'),
(N'dbo.VisitRequests',N'UserId'),
(N'dbo.VisitRequests',N'ListingId'),
(N'dbo.VisitRequests',N'VisitAt'),
(N'dbo.VisitRequests',N'Message'),
(N'dbo.VisitRequests',N'Response'),
(N'dbo.VisitRequests',N'Status'),
(N'dbo.VisitRequests',N'CreatedAt'),
(N'dbo.Reviews',N'Id'),
(N'dbo.Reviews',N'UserId'),
(N'dbo.Reviews',N'ListingId'),
(N'dbo.Reviews',N'Rating'),
(N'dbo.Reviews',N'Text'),
(N'dbo.Reviews',N'Removed'),
(N'dbo.Reviews',N'Reason'),
(N'dbo.Reviews',N'CreatedAt'),
(N'dbo.ReviewReports',N'ReviewId'),
(N'dbo.ReviewReports',N'UserId'),
(N'dbo.ReviewReports',N'Reason'),
(N'dbo.CartItems',N'UserId'),
(N'dbo.CartItems',N'ListingId'),
(N'dbo.CartItems',N'Months'),
(N'dbo.Orders',N'Id'),
(N'dbo.Orders',N'BuyerId'),
(N'dbo.Orders',N'CheckoutToken'),
(N'dbo.Orders',N'Address'),
(N'dbo.Orders',N'City'),
(N'dbo.Orders',N'State'),
(N'dbo.Orders',N'Pincode'),
(N'dbo.Orders',N'Phone'),
(N'dbo.Orders',N'CreatedAt'),
(N'dbo.OrderItems',N'Id'),
(N'dbo.OrderItems',N'OrderId'),
(N'dbo.OrderItems',N'ListingId'),
(N'dbo.OrderItems',N'SellerId'),
(N'dbo.OrderItems',N'Title'),
(N'dbo.OrderItems',N'ListingType'),
(N'dbo.OrderItems',N'UnitPrice'),
(N'dbo.OrderItems',N'Deposit'),
(N'dbo.OrderItems',N'Months'),
(N'dbo.OrderItems',N'Amount'),
(N'dbo.OrderItems',N'Status'),
(N'dbo.OrderItems',N'Collected'),
(N'dbo.OrderItems',N'CreatedAt'),
(N'dbo.OrderItems',N'ProcessedAt'),
(N'dbo.OrderItems',N'CompletedAt'),
(N'dbo.OrderHistory',N'Id'),
(N'dbo.OrderHistory',N'OrderItemId'),
(N'dbo.OrderHistory',N'Status'),
(N'dbo.OrderHistory',N'ActorId'),
(N'dbo.OrderHistory',N'CreatedAt'),
(N'dbo.Tickets',N'Id'),
(N'dbo.Tickets',N'AccountId'),
(N'dbo.Tickets',N'FullName'),
(N'dbo.Tickets',N'Email'),
(N'dbo.Tickets',N'Subject'),
(N'dbo.Tickets',N'Message'),
(N'dbo.Tickets',N'Status'),
(N'dbo.Tickets',N'CreatedAt'),
(N'dbo.TicketReplies',N'Id'),
(N'dbo.TicketReplies',N'TicketId'),
(N'dbo.TicketReplies',N'AdminId'),
(N'dbo.TicketReplies',N'Message'),
(N'dbo.TicketReplies',N'CreatedAt'),
(N'dbo.Conversations',N'Id'),
(N'dbo.Conversations',N'ListingId'),
(N'dbo.Conversations',N'UserId'),
(N'dbo.Conversations',N'OwnerId'),
(N'dbo.Conversations',N'CreatedAt'),
(N'dbo.Messages',N'Id'),
(N'dbo.Messages',N'ConversationId'),
(N'dbo.Messages',N'SenderId'),
(N'dbo.Messages',N'Text'),
(N'dbo.Messages',N'CreatedAt'),
(N'dbo.Notifications',N'Id'),
(N'dbo.Notifications',N'AccountId'),
(N'dbo.Notifications',N'Text'),
(N'dbo.Notifications',N'Url'),
(N'dbo.Notifications',N'IsRead'),
(N'dbo.Notifications',N'CreatedAt'),
(N'dbo.ModerationHistory',N'Id'),
(N'dbo.ModerationHistory',N'ActorId'),
(N'dbo.ModerationHistory',N'Entity'),
(N'dbo.ModerationHistory',N'EntityId'),
(N'dbo.ModerationHistory',N'Action'),
(N'dbo.ModerationHistory',N'Reason'),
(N'dbo.ModerationHistory',N'CreatedAt'),
(N'dbo.PropertyViews',N'Id'),
(N'dbo.PropertyViews',N'ListingId'),
(N'dbo.PropertyViews',N'ViewerId'),
(N'dbo.PropertyViews',N'ViewDate'),
(N'dbo.EmailOutbox',N'Id'),
(N'dbo.EmailOutbox',N'Recipient'),
(N'dbo.EmailOutbox',N'Subject'),
(N'dbo.EmailOutbox',N'Body'),
(N'dbo.EmailOutbox',N'Attempts'),
(N'dbo.EmailOutbox',N'SentAt'),
(N'dbo.EmailOutbox',N'NextAttemptAt'),
(N'dbo.EmailOutbox',N'CreatedAt')
) expected(TableName,ColumnName)
WHERE OBJECT_ID(expected.TableName,N'U') IS NOT NULL AND COL_LENGTH(expected.TableName,expected.ColumnName) IS NULL;
IF @incompatible IS NOT NULL THROW 51001, @incompatible, 1;
BEGIN TRY
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Accounts',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Accounts(Id bigint IDENTITY PRIMARY KEY,FullName nvarchar(100) NOT NULL,Email nvarchar(254) NOT NULL,NormalizedEmail AS UPPER(Email) PERSISTED,Phone varchar(10) NOT NULL,PasswordHash varchar(300) NOT NULL,Role varchar(10) NOT NULL CHECK(Role IN(''User'',''Owner'',''Admin'')),Status varchar(20) NOT NULL CHECK(Status IN(''Active'',''PendingApproval'',''Approved'',''Rejected'',''Blocked'')),Reason nvarchar(500) NOT NULL DEFAULT '''',EmailVerified bit NOT NULL DEFAULT 1,SecurityStamp uniqueidentifier NOT NULL DEFAULT NEWID(),DateOfBirth date NULL,Gender varchar(20) NOT NULL DEFAULT ''Other'',Address nvarchar(300) NOT NULL DEFAULT '''',City nvarchar(80) NOT NULL,State nvarchar(80) NOT NULL,Pincode varchar(6) NULL,Avatar int NOT NULL DEFAULT 1 CHECK(Avatar BETWEEN 1 AND 8),DocumentType nvarchar(40) NOT NULL DEFAULT '''',CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.OtpChallenges',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.OtpChallenges(Id bigint IDENTITY PRIMARY KEY,Email nvarchar(254) NOT NULL,Purpose varchar(20) NOT NULL CHECK(Purpose IN(''Registration'',''ForgotPassword'',''ChangeEmail'')),ActorId bigint NULL REFERENCES dbo.Accounts(Id),SessionHash varchar(64) NOT NULL,CodeHash varchar(300) NOT NULL,ExpiresAt datetime2 NOT NULL,Attempts int NOT NULL DEFAULT 0,IsUsed bit NOT NULL DEFAULT 0,VerifiedAt datetime2 NULL,ProofHash varchar(64) NULL,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.Listings',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Listings(Id bigint IDENTITY PRIMARY KEY,OwnerId bigint NOT NULL REFERENCES dbo.Accounts(Id),Kind varchar(12) NOT NULL CHECK(Kind IN(''Property'',''Room'',''Finder'',''Item'')),Title nvarchar(100) NOT NULL,Description nvarchar(1200) NOT NULL,Type nvarchar(40) NOT NULL,City nvarchar(80) NOT NULL,State nvarchar(80) NOT NULL,Address nvarchar(300) NOT NULL DEFAULT '''',Price decimal(18,2) NOT NULL CHECK(Price BETWEEN 0 AND 10000000),Deposit decimal(18,2) NOT NULL DEFAULT 0 CHECK(Deposit>=0),Status varchar(25) NOT NULL CHECK(Status IN(''Draft'',''PendingVerification'',''Approved'',''Rejected'',''Unpublished'',''Removed'',''Reserved'',''Sold'')),Available bit NOT NULL DEFAULT 1,Reason nvarchar(500) NOT NULL DEFAULT '''',Details nvarchar(max) NOT NULL CHECK(ISJSON(Details)=1),CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),UpdatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.Files',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Files(Id bigint IDENTITY PRIMARY KEY,OwnerId bigint NOT NULL REFERENCES dbo.Accounts(Id),ListingId bigint NULL REFERENCES dbo.Listings(Id),Name varchar(80) NOT NULL UNIQUE,OriginalName nvarchar(255) NOT NULL,ContentType varchar(80) NOT NULL,Private bit NOT NULL,Purpose varchar(40) NOT NULL,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.Wishlists',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Wishlists(AccountId bigint NOT NULL REFERENCES dbo.Accounts(Id),ListingId bigint NOT NULL REFERENCES dbo.Listings(Id),PRIMARY KEY(AccountId,ListingId));');
END;
IF OBJECT_ID(N'dbo.VisitRequests',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.VisitRequests(Id bigint IDENTITY PRIMARY KEY,UserId bigint NOT NULL REFERENCES dbo.Accounts(Id),ListingId bigint NOT NULL REFERENCES dbo.Listings(Id),VisitAt datetime2 NOT NULL,Message nvarchar(1000) NOT NULL DEFAULT '''',Response nvarchar(1000) NOT NULL DEFAULT '''',Status varchar(12) NOT NULL DEFAULT ''Pending'' CHECK(Status IN(''Pending'',''Approved'',''Rejected'',''Completed'')),CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),UNIQUE(UserId,ListingId));');
END;
IF OBJECT_ID(N'dbo.Reviews',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Reviews(Id bigint IDENTITY PRIMARY KEY,UserId bigint NOT NULL REFERENCES dbo.Accounts(Id),ListingId bigint NOT NULL REFERENCES dbo.Listings(Id),Rating int NOT NULL CHECK(Rating BETWEEN 1 AND 5),Text nvarchar(1500) NOT NULL,Removed bit NOT NULL DEFAULT 0,Reason nvarchar(500) NOT NULL DEFAULT '''',CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),UNIQUE(UserId,ListingId));');
END;
IF OBJECT_ID(N'dbo.ReviewReports',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.ReviewReports(ReviewId bigint NOT NULL REFERENCES dbo.Reviews(Id),UserId bigint NOT NULL REFERENCES dbo.Accounts(Id),Reason nvarchar(500) NOT NULL,PRIMARY KEY(ReviewId,UserId));');
END;
IF OBJECT_ID(N'dbo.CartItems',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.CartItems(UserId bigint NOT NULL REFERENCES dbo.Accounts(Id),ListingId bigint NOT NULL REFERENCES dbo.Listings(Id),Months int NOT NULL DEFAULT 1 CHECK(Months BETWEEN 1 AND 24),PRIMARY KEY(UserId,ListingId));');
END;
IF OBJECT_ID(N'dbo.Orders',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Orders(Id bigint IDENTITY PRIMARY KEY,BuyerId bigint NOT NULL REFERENCES dbo.Accounts(Id),CheckoutToken uniqueidentifier NOT NULL UNIQUE,Address nvarchar(300) NOT NULL,City nvarchar(80) NOT NULL,State nvarchar(80) NOT NULL,Pincode varchar(6) NOT NULL,Phone varchar(10) NOT NULL,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.OrderItems',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.OrderItems(Id bigint IDENTITY PRIMARY KEY,OrderId bigint NOT NULL REFERENCES dbo.Orders(Id),ListingId bigint NOT NULL REFERENCES dbo.Listings(Id),SellerId bigint NOT NULL REFERENCES dbo.Accounts(Id),Title nvarchar(100) NOT NULL,ListingType nvarchar(40) NOT NULL,UnitPrice decimal(18,2) NOT NULL,Deposit decimal(18,2) NOT NULL,Months int NOT NULL,Amount decimal(18,2) NOT NULL,Status varchar(12) NOT NULL DEFAULT ''Pending'' CHECK(Status IN(''Pending'',''Processing'',''Completed'')),Collected bit NOT NULL DEFAULT 0,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),ProcessedAt datetime2 NULL,CompletedAt datetime2 NULL);');
END;
IF OBJECT_ID(N'dbo.OrderHistory',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.OrderHistory(Id bigint IDENTITY PRIMARY KEY,OrderItemId bigint NOT NULL REFERENCES dbo.OrderItems(Id),Status varchar(20) NOT NULL,ActorId bigint NOT NULL REFERENCES dbo.Accounts(Id),CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.Tickets',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Tickets(Id bigint IDENTITY PRIMARY KEY,AccountId bigint NULL REFERENCES dbo.Accounts(Id),FullName nvarchar(100) NOT NULL,Email nvarchar(254) NOT NULL,Subject nvarchar(150) NOT NULL,Message nvarchar(2000) NOT NULL,Status varchar(10) NOT NULL DEFAULT ''Open'' CHECK(Status IN(''Open'',''Resolved'')),CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.TicketReplies',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.TicketReplies(Id bigint IDENTITY PRIMARY KEY,TicketId bigint NOT NULL REFERENCES dbo.Tickets(Id),AdminId bigint NOT NULL REFERENCES dbo.Accounts(Id),Message nvarchar(2000) NOT NULL,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.Conversations',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Conversations(Id bigint IDENTITY PRIMARY KEY,ListingId bigint NOT NULL REFERENCES dbo.Listings(Id),UserId bigint NOT NULL REFERENCES dbo.Accounts(Id),OwnerId bigint NOT NULL REFERENCES dbo.Accounts(Id),CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),UNIQUE(ListingId,UserId));');
END;
IF OBJECT_ID(N'dbo.Messages',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Messages(Id bigint IDENTITY PRIMARY KEY,ConversationId bigint NOT NULL REFERENCES dbo.Conversations(Id),SenderId bigint NOT NULL REFERENCES dbo.Accounts(Id),Text nvarchar(2000) NOT NULL,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.Notifications',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.Notifications(Id bigint IDENTITY PRIMARY KEY,AccountId bigint NOT NULL REFERENCES dbo.Accounts(Id),Text nvarchar(500) NOT NULL,Url nvarchar(300) NOT NULL,IsRead bit NOT NULL DEFAULT 0,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.ModerationHistory',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.ModerationHistory(Id bigint IDENTITY PRIMARY KEY,ActorId bigint NOT NULL REFERENCES dbo.Accounts(Id),Entity varchar(20) NOT NULL,EntityId bigint NOT NULL,Action varchar(30) NOT NULL,Reason nvarchar(500) NOT NULL,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF OBJECT_ID(N'dbo.PropertyViews',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.PropertyViews(Id bigint IDENTITY PRIMARY KEY,ListingId bigint NOT NULL REFERENCES dbo.Listings(Id),ViewerId bigint NOT NULL REFERENCES dbo.Accounts(Id),ViewDate date NOT NULL DEFAULT CONVERT(date,SYSUTCDATETIME()),UNIQUE(ListingId,ViewerId,ViewDate));');
END;
IF OBJECT_ID(N'dbo.EmailOutbox',N'U') IS NULL
BEGIN
    EXEC(N'CREATE TABLE dbo.EmailOutbox(Id bigint IDENTITY PRIMARY KEY,Recipient nvarchar(254) NOT NULL,Subject nvarchar(200) NOT NULL,Body nvarchar(max) NOT NULL,Attempts int NOT NULL DEFAULT 0,SentAt datetime2 NULL,NextAttemptAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME());');
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Accounts') AND name=N'UX_Accounts_Email')
    EXEC(N'CREATE UNIQUE INDEX UX_Accounts_Email ON dbo.Accounts(NormalizedEmail);');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.OtpChallenges') AND name=N'IX_Otp_EmailPurpose')
    EXEC(N'CREATE INDEX IX_Otp_EmailPurpose ON dbo.OtpChallenges(Email,Purpose,CreatedAt DESC);');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Listings') AND name=N'IX_Listings_Search')
    EXEC(N'CREATE INDEX IX_Listings_Search ON dbo.Listings(Kind,Status,City,Price);');
COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
