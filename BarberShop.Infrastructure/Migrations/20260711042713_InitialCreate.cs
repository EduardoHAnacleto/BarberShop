using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarberShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakStart = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakEnd = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerPhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ServiceDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ServiceDuration = table.Column<int>(type: "int", nullable: false),
                    ServicePrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.ServiceId);
                });

            migrationBuilder.CreateTable(
                name: "Workers",
                columns: table => new
                {
                    WorkerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkerPhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkerDateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkerAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkerWagePerHour = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    WorkerPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workers", x => x.WorkerId);
                });

            migrationBuilder.CreateTable(
                name: "WorkingHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HolidayClosedFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HolidayClosedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HolidayReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClosureType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    AppointmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentWorkerId = table.Column<int>(type: "int", nullable: false),
                    AppointmentCustomerId = table.Column<int>(type: "int", nullable: false),
                    AppointmentServiceId = table.Column<int>(type: "int", nullable: false),
                    AppointmentScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppointmentStatus = table.Column<int>(type: "int", nullable: false),
                    AppointmentCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentExtraDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentRecurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentReminder24hSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentReminder1hSentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_Appointments_Customers_AppointmentCustomerId",
                        column: x => x.AppointmentCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_Appointments_Services_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId");
                    table.ForeignKey(
                        name: "FK_Appointments_Workers_AppointmentWorkerId",
                        column: x => x.AppointmentWorkerId,
                        principalTable: "Workers",
                        principalColumn: "WorkerId");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserCustomerId = table.Column<int>(type: "int", nullable: true),
                    UserWorkerId = table.Column<int>(type: "int", nullable: true),
                    UserEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserPasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserRole = table.Column<int>(type: "int", nullable: false),
                    UserIsActive = table.Column<bool>(type: "bit", nullable: false),
                    UserCreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserFailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    UserLockoutEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserGoogleId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserSecurityStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValueSql: "CONVERT(NVARCHAR(64), NEWID())"),
                    UserPasswordResetToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserPasswordResetTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Customers_UserCustomerId",
                        column: x => x.UserCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_Users_Workers_UserWorkerId",
                        column: x => x.UserWorkerId,
                        principalTable: "Workers",
                        principalColumn: "WorkerId");
                });

            migrationBuilder.CreateTable(
                name: "Waitlist",
                columns: table => new
                {
                    WaitlistId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WaitlistCustomerId = table.Column<int>(type: "int", nullable: false),
                    WaitlistWorkerId = table.Column<int>(type: "int", nullable: false),
                    WaitlistServiceId = table.Column<int>(type: "int", nullable: false),
                    WaitlistPreferredDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    WaitlistNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waitlist", x => x.WaitlistId);
                    table.ForeignKey(
                        name: "FK_Waitlist_Customers_WaitlistCustomerId",
                        column: x => x.WaitlistCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Waitlist_Services_WaitlistServiceId",
                        column: x => x.WaitlistServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId");
                    table.ForeignKey(
                        name: "FK_Waitlist_Workers_WaitlistWorkerId",
                        column: x => x.WaitlistWorkerId,
                        principalTable: "Workers",
                        principalColumn: "WorkerId");
                });

            migrationBuilder.CreateTable(
                name: "WorkerSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkerId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakStart = table.Column<TimeSpan>(type: "time", nullable: true),
                    BreakEnd = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerSchedules_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "WorkerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkerServices",
                columns: table => new
                {
                    WSServiceId = table.Column<int>(type: "int", nullable: false),
                    WSWorkerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerServices", x => new { x.WSWorkerId, x.WSServiceId });
                    table.ForeignKey(
                        name: "FK_WorkerServices_Services",
                        column: x => x.WSServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkerServices_Workers",
                        column: x => x.WSWorkerId,
                        principalTable: "Workers",
                        principalColumn: "WorkerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkersService",
                columns: table => new
                {
                    ProvidedServicesId = table.Column<int>(type: "int", nullable: false),
                    WorkerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkersService", x => new { x.ProvidedServicesId, x.WorkerId });
                    table.ForeignKey(
                        name: "FK_WorkersService_Services_ProvidedServicesId",
                        column: x => x.ProvidedServicesId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkersService_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "WorkerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewAppointmentId = table.Column<int>(type: "int", nullable: false),
                    ReviewCustomerId = table.Column<int>(type: "int", nullable: false),
                    ReviewWorkerId = table.Column<int>(type: "int", nullable: false),
                    ReviewRating = table.Column<int>(type: "int", nullable: false),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_Reviews_Appointments_ReviewAppointmentId",
                        column: x => x.ReviewAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Customers_ReviewCustomerId",
                        column: x => x.ReviewCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_Reviews_Workers_ReviewWorkerId",
                        column: x => x.ReviewWorkerId,
                        principalTable: "Workers",
                        principalColumn: "WorkerId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentCustomerId",
                table: "Appointments",
                column: "AppointmentCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentScheduledFor",
                table: "Appointments",
                column: "AppointmentScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentServiceId",
                table: "Appointments",
                column: "AppointmentServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentStatus",
                table: "Appointments",
                column: "AppointmentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AppointmentWorkerId",
                table: "Appointments",
                column: "AppointmentWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSchedules_DayOfWeek",
                table: "BusinessSchedules",
                column: "DayOfWeek",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewAppointmentId",
                table: "Reviews",
                column: "ReviewAppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewCustomerId",
                table: "Reviews",
                column: "ReviewCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewWorkerId",
                table: "Reviews",
                column: "ReviewWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserCustomerId",
                table: "Users",
                column: "UserCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserEmail",
                table: "Users",
                column: "UserEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserWorkerId",
                table: "Users",
                column: "UserWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_Waitlist_WaitlistCustomerId",
                table: "Waitlist",
                column: "WaitlistCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Waitlist_WaitlistServiceId",
                table: "Waitlist",
                column: "WaitlistServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Waitlist_WaitlistWorkerId_WaitlistPreferredDate",
                table: "Waitlist",
                columns: new[] { "WaitlistWorkerId", "WaitlistPreferredDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerSchedules_WorkerId_DayOfWeek",
                table: "WorkerSchedules",
                columns: new[] { "WorkerId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerServices_WSServiceId",
                table: "WorkerServices",
                column: "WSServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkersService_WorkerId",
                table: "WorkersService",
                column: "WorkerId");

            // ── Seed data ──────────────────────────────────────────────────
            // Mirrors database/schema.sql's three seed blocks exactly (weekly
            // schedule defaults, admin account, demo worker) so a fresh
            // environment built purely from migrations is demoable, not an
            // empty shell. IF NOT EXISTS guards make this safe to leave in
            // place even though a first-ever Up() run never needs them.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[BusinessSchedules])
                BEGIN
                    INSERT INTO [dbo].[BusinessSchedules]
                        ([DayOfWeek], [IsOpen], [OpenTime], [CloseTime], [BreakStart], [BreakEnd])
                    VALUES
                        (0, 0, NULL,     NULL,     NULL,     NULL    ),
                        (1, 1, '09:00', '18:00', '12:00', '13:00'   ),
                        (2, 1, '09:00', '18:00', '12:00', '13:00'   ),
                        (3, 1, '09:00', '18:00', '12:00', '13:00'   ),
                        (4, 1, '09:00', '18:00', '12:00', '13:00'   ),
                        (5, 1, '09:00', '18:00', '12:00', '13:00'   ),
                        (6, 1, '10:30', '16:00', NULL,     NULL      );
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [UserEmail] = 'admin@barbershop.com')
                BEGIN
                    INSERT INTO [dbo].[Users] (
                        [UserEmail], [UserPasswordHash], [UserRole],
                        [UserIsActive], [UserCreatedAt], [UserFailedLoginAttempts]
                    )
                    VALUES (
                        'admin@barbershop.com',
                        '$2b$11$cD04dd4G8a7aCXrOcytdb.DxlMxiFpPI6RX/LxK15/G.hdfMlbYNi',
                        3, 1, GETUTCDATE(), 0
                    );
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Workers] WHERE [Email] = 'carlos@barbershop.com')
                BEGIN
                    INSERT INTO [dbo].[Workers] (
                        [WorkerName], [WorkerDateOfBirth], [WorkerAddress],
                        [WorkerPosition], [WorkerPhoneNumber], [WorkerWagePerHour], [Email]
                    )
                    VALUES (
                        'Carlos Silva', '1990-05-15', 'Rua das Flores 123',
                        'Barber', '+55 11 99999-0001', 25.00, 'carlos@barbershop.com'
                    );

                    DECLARE @workerId INT = SCOPE_IDENTITY();

                    INSERT INTO [dbo].[Users] (
                        [UserEmail], [UserPasswordHash], [UserRole],
                        [UserIsActive], [UserCreatedAt], [UserFailedLoginAttempts], [UserWorkerId]
                    )
                    VALUES (
                        'carlos@barbershop.com',
                        '$2b$11$zcq0Z3iGdwjj1KXHPsp04ueC6UbcYihSPee5t4HFa.qiJvsxztuCC',
                        1, 1, GETUTCDATE(), 0, @workerId
                    );
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessSchedules");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Waitlist");

            migrationBuilder.DropTable(
                name: "WorkerSchedules");

            migrationBuilder.DropTable(
                name: "WorkerServices");

            migrationBuilder.DropTable(
                name: "WorkersService");

            migrationBuilder.DropTable(
                name: "WorkingHours");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Workers");
        }
    }
}
