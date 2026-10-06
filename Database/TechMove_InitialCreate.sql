IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Clients] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [ContactDetails] nvarchar(200) NOT NULL,
    [Region] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
);

CREATE TABLE [Contracts] (
    [Id] int NOT NULL IDENTITY,
    [ClientId] int NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ServiceLevel] nvarchar(20) NOT NULL,
    [SignedAgreementFileName] nvarchar(255) NULL,
    [SignedAgreementStoredName] nvarchar(100) NULL,
    [SignedAgreementUploadedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Contracts_EndAfterStart] CHECK ([EndDate] > [StartDate]),
    CONSTRAINT [FK_Contracts_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ServiceRequests] (
    [Id] int NOT NULL IDENTITY,
    [ContractId] int NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [CostUsd] decimal(18,2) NOT NULL,
    [ExchangeRate] decimal(18,6) NOT NULL,
    [Cost] decimal(18,2) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ServiceRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ServiceRequests_CostUsd_NonNegative] CHECK ([CostUsd] >= 0),
    CONSTRAINT [CK_ServiceRequests_Rate_Positive] CHECK ([ExchangeRate] > 0),
    CONSTRAINT [FK_ServiceRequests_Contracts_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [Contracts] ([Id]) ON DELETE NO ACTION
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactDetails', N'Name', N'Region') AND [object_id] = OBJECT_ID(N'[Clients]'))
    SET IDENTITY_INSERT [Clients] ON;
INSERT INTO [Clients] ([Id], [ContactDetails], [Name], [Region])
VALUES (1, N'ops@acmelogistics.co.za | 011 555 0101', N'Acme Logistics', N'Gauteng'),
(2, N'info@capefreight.co.za | 021 555 0202', N'Cape Freight Co', N'Western Cape'),
(3, N'admin@dps.co.za | 031 555 0303', N'Durban Port Services', N'KwaZulu-Natal');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ContactDetails', N'Name', N'Region') AND [object_id] = OBJECT_ID(N'[Clients]'))
    SET IDENTITY_INSERT [Clients] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientId', N'EndDate', N'ServiceLevel', N'SignedAgreementFileName', N'SignedAgreementStoredName', N'SignedAgreementUploadedAtUtc', N'StartDate', N'Status') AND [object_id] = OBJECT_ID(N'[Contracts]'))
    SET IDENTITY_INSERT [Contracts] ON;
INSERT INTO [Contracts] ([Id], [ClientId], [EndDate], [ServiceLevel], [SignedAgreementFileName], [SignedAgreementStoredName], [SignedAgreementUploadedAtUtc], [StartDate], [Status])
VALUES (1, 1, '2027-12-31', N'Gold', NULL, NULL, NULL, '2026-01-01', N'Active'),
(2, 2, '2025-12-31', N'Silver', NULL, NULL, NULL, '2024-01-01', N'Expired'),
(3, 3, '2027-02-28', N'Platinum', NULL, NULL, NULL, '2026-03-01', N'OnHold'),
(4, 1, '2027-10-31', N'Bronze', NULL, NULL, NULL, '2026-11-01', N'Draft');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClientId', N'EndDate', N'ServiceLevel', N'SignedAgreementFileName', N'SignedAgreementStoredName', N'SignedAgreementUploadedAtUtc', N'StartDate', N'Status') AND [object_id] = OBJECT_ID(N'[Contracts]'))
    SET IDENTITY_INSERT [Contracts] OFF;

CREATE INDEX [IX_Clients_Name] ON [Clients] ([Name]);

CREATE INDEX [IX_Contracts_ClientId] ON [Contracts] ([ClientId]);

CREATE INDEX [IX_Contracts_Status_StartDate_EndDate] ON [Contracts] ([Status], [StartDate], [EndDate]);

CREATE INDEX [IX_ServiceRequests_ContractId] ON [ServiceRequests] ([ContractId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261006181113_InitialCreate', N'10.0.12');

COMMIT;
GO

