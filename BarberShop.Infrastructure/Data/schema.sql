-- =============================================
-- BarberShop Database Schema
-- SQL Server
-- =============================================

USE master;
GO

-- =============================================
-- Create database if it doesn't exist
-- =============================================
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'barbershop')
BEGIN
    CREATE DATABASE barbershop;
END
GO

USE barbershop;
GO

-- =============================================
-- Services
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Services]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Services] (
        [ServiceId]          INT             NOT NULL IDENTITY(1,1),
        [ServiceName]        NVARCHAR(100)   NOT NULL,
        [ServiceDescription] NVARCHAR(500)   NOT NULL DEFAULT '',
        [ServiceDuration]    INT             NOT NULL,
        [ServicePrice]       DECIMAL(10, 2)  NOT NULL,
        [CreatedAt]          DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [LastUpdatedAt]      DATETIME2       NULL,

        CONSTRAINT [PK_Services] PRIMARY KEY ([ServiceId])
    );
END
GO

-- =============================================
-- Customers (note: typo preserved from AppDbContext)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Customers]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Customers] (
        [CustomerId]          INT             NOT NULL IDENTITY(1,1),
        [CustomerName]        NVARCHAR(100)   NOT NULL,
        [CustomerDateOfBirth] DATETIME2       NULL,
        [CustomerEmail]       NVARCHAR(200)   NOT NULL DEFAULT '',
        [CustomerPhoneNumber] NVARCHAR(20)    NOT NULL DEFAULT '',
        [CreatedAt]           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [LastUpdatedAt]       DATETIME2       NULL,

        CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId])
    );
END
GO

-- =============================================
-- Workers
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Workers]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Workers] (
        [WorkerId]          INT             NOT NULL IDENTITY(1,1),
        [WorkerName]        NVARCHAR(100)   NOT NULL,
        [WorkerDateOfBirth] DATETIME2       NOT NULL,
        [WorkerAddress]     NVARCHAR(200)   NOT NULL DEFAULT '',
        [WorkerPosition]    NVARCHAR(100)   NOT NULL DEFAULT '',
        [WorkerPhoneNumber] NVARCHAR(20)    NOT NULL DEFAULT '',
        [WorkerWagePerHour] DECIMAL(10, 2)  NOT NULL,
        [Email]             NVARCHAR(200)   NOT NULL,
        [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [LastUpdatedAt]     DATETIME2       NULL,

        CONSTRAINT [PK_Workers] PRIMARY KEY ([WorkerId])
    );
END
GO

-- =============================================
-- WorkersService (join table from HasMany/WithMany)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WorkersService]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[WorkersService] (
        [ProvidedServicesServiceId] INT NOT NULL,
        [WorkersWorkerId]           INT NOT NULL,

        CONSTRAINT [PK_WorkersService] PRIMARY KEY ([ProvidedServicesServiceId], [WorkersWorkerId]),

        CONSTRAINT [FK_WorkersService_Services] FOREIGN KEY ([ProvidedServicesServiceId])
            REFERENCES [dbo].[Services] ([ServiceId]),

        CONSTRAINT [FK_WorkersService_Workers] FOREIGN KEY ([WorkersWorkerId])
            REFERENCES [dbo].[Workers] ([WorkerId])
    );
END
GO

-- =============================================
-- WorkerServices (explicit join entity)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WorkerServices]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[WorkerServices] (
        [WSWorkerId]  INT NOT NULL,
        [WSServiceId] INT NOT NULL,

        CONSTRAINT [PK_WorkerServices] PRIMARY KEY ([WSWorkerId], [WSServiceId]),

        CONSTRAINT [FK_WorkerServices_Workers] FOREIGN KEY ([WSWorkerId])
            REFERENCES [dbo].[Workers] ([WorkerId]),

        CONSTRAINT [FK_WorkerServices_Services] FOREIGN KEY ([WSServiceId])
            REFERENCES [dbo].[Services] ([ServiceId])
    );
END
GO

-- =============================================
-- Users
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [UserId]                  INT             NOT NULL IDENTITY(1,1),
        [UserEmail]               NVARCHAR(200)   NOT NULL,
        [UserPasswordHash]        NVARCHAR(500)   NOT NULL DEFAULT '',
        [UserRole]                INT             NOT NULL DEFAULT 0,
        [UserIsActive]            BIT             NOT NULL DEFAULT 1,
        [UserCreatedAt]           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [UserFailedLoginAttempts] INT             NOT NULL DEFAULT 0,
        [UserLockoutEnd]          DATETIME2       NULL,
        [UserGoogleId]            NVARCHAR(200)   NULL,
        [UserCustomerId]          INT             NULL,
        [UserWorkerId]            INT             NULL,

        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),

        CONSTRAINT [FK_Users_Customers] FOREIGN KEY ([UserCustomerId])
            REFERENCES [dbo].[Customers] ([CustomerId]),

        CONSTRAINT [FK_Users_Workers] FOREIGN KEY ([UserWorkerId])
            REFERENCES [dbo].[Workers] ([WorkerId])
    );
END
GO

-- =============================================
-- Appointments
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Appointments]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Appointments] (
        [AppointmentId]           INT             NOT NULL IDENTITY(1,1),
        [AppointmentWorkerId]     INT             NOT NULL,
        [AppointmentCustomerId]   INT             NOT NULL,
        [AppointmentServiceId]    INT             NOT NULL,
        [AppointmentScheduledFor] DATETIME2       NOT NULL,
        [AppointmentStatus]       INT             NOT NULL DEFAULT 0,
        [AppointmentCompletedAt]  DATETIME2       NULL,
        [AppointmentExtraDetails] NVARCHAR(500)   NOT NULL DEFAULT '',
        [CreatedAt]               DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
        [LastUpdatedAt]           DATETIME2       NULL,

        CONSTRAINT [PK_Appointments] PRIMARY KEY ([AppointmentId]),

        CONSTRAINT [FK_Appointments_Workers] FOREIGN KEY ([AppointmentWorkerId])
            REFERENCES [dbo].[Workers] ([WorkerId]),

        CONSTRAINT [FK_Appointments_Customers] FOREIGN KEY ([AppointmentCustomerId])
            REFERENCES [dbo].[Customers] ([CustomerId]),

        CONSTRAINT [FK_Appointments_Services] FOREIGN KEY ([AppointmentServiceId])
            REFERENCES [dbo].[Services] ([ServiceId])
    );
END
GO

-- =============================================
-- BusinessSchedules
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BusinessSchedules]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[BusinessSchedules] (
        [Id]         INT     NOT NULL IDENTITY(1,1),
        [DayOfWeek]  INT     NOT NULL,
        [IsOpen]     BIT     NOT NULL DEFAULT 0,
        [OpenTime]   TIME    NULL,
        [CloseTime]  TIME    NULL,
        [BreakStart] TIME    NULL,
        [BreakEnd]   TIME    NULL,

        CONSTRAINT [PK_BusinessSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_BusinessSchedules_DayOfWeek] UNIQUE ([DayOfWeek])
    );
END
GO

-- =============================================
-- WorkingHours
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WorkingHours]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[WorkingHours] (
        [Id]               INT             NOT NULL IDENTITY(1,1),
        [HolidayClosedFrom]  DATETIME2     NOT NULL,
        [HolidayClosedUntil] DATETIME2     NULL,
        [HolidayReason]      NVARCHAR(200) NOT NULL DEFAULT '',
        [ClosureType]        INT           NOT NULL DEFAULT 0,

        CONSTRAINT [PK_WorkingHours] PRIMARY KEY ([Id])
    );
END
GO

-- =============================================
-- Indexes
-- =============================================

-- Users — busca por email frequente no login
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Email')
    CREATE INDEX [IX_Users_Email]
        ON [dbo].[Users] ([UserEmail]);
GO

-- Appointments — filtros mais comuns
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Appointments_WorkerId')
    CREATE INDEX [IX_Appointments_WorkerId]
        ON [dbo].[Appointments] ([AppointmentWorkerId]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Appointments_CustomerId')
    CREATE INDEX [IX_Appointments_CustomerId]
        ON [dbo].[Appointments] ([AppointmentCustomerId]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Appointments_ScheduledFor')
    CREATE INDEX [IX_Appointments_ScheduledFor]
        ON [dbo].[Appointments] ([AppointmentScheduledFor]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Appointments_Status')
    CREATE INDEX [IX_Appointments_Status]
        ON [dbo].[Appointments] ([AppointmentStatus]);
GO

-- =============================================
-- Seed — BusinessSchedules (7 dias da semana)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[BusinessSchedules])
BEGIN
    INSERT INTO [dbo].[BusinessSchedules] ([DayOfWeek], [IsOpen], [OpenTime], [CloseTime], [BreakStart], [BreakEnd])
    VALUES
        (0, 0, NULL,     NULL,     NULL,     NULL),     -- Sunday   — fechado
        (1, 1, '09:00', '18:00', '12:00', '13:00'),    -- Monday
        (2, 1, '09:00', '18:00', '12:00', '13:00'),    -- Tuesday
        (3, 1, '09:00', '18:00', '12:00', '13:00'),    -- Wednesday
        (4, 1, '09:00', '18:00', '12:00', '13:00'),    -- Thursday
        (5, 1, '09:00', '18:00', '12:00', '13:00'),    -- Friday
        (6, 1, '10:30', '16:00', NULL,     NULL);       -- Saturday — sem almoço, fecha cedo
END
GO

-- =============================================
-- Seed — Admin user (senha: Admin@123)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [UserEmail] = 'admin@barbershop.com')
BEGIN
    INSERT INTO [dbo].[Users] (
        [UserEmail],
        [UserPasswordHash],
        [UserRole],
        [UserIsActive],
        [UserCreatedAt],
        [UserFailedLoginAttempts]
    )
    VALUES (
        'admin@barbershop.com',
        -- BCrypt hash de 'Admin@123' com cost factor 11
        '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2uheWG/igi.',
        3,    -- UserRoles.Admin
        1,
        GETUTCDATE(),
        0
    );
END
GO