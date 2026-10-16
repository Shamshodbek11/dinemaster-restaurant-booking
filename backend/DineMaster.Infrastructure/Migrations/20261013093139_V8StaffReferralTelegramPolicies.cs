using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DineMaster.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V8StaffReferralTelegramPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.AddColumn<string>(
                name: "special_hours",
                table: "restaurants",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_by",
                table: "reservations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deposit_outcome",
                table: "reservations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_late_cancellation",
                table: "reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "pre_order_discount",
                table: "reservations",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "restaurant_discount",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "referral_code",
                table: "profiles",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "referral_rewarded",
                table: "profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "referred_by",
                table: "profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "telegram_chat_id",
                table: "profiles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_link_code",
                table: "profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "terms_accepted_at",
                table: "profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refund_requested_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "restaurant_staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_staff", x => x.id);
                    table.ForeignKey(
                        name: "FK_restaurant_staff_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restaurant_staff_restaurants_restaurant_id",
                        column: x => x.restaurant_id,
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profiles_referral_code",
                table: "profiles",
                column: "referral_code",
                unique: true,
                filter: "referral_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_profiles_referred_by",
                table: "profiles",
                column: "referred_by");

            migrationBuilder.CreateIndex(
                name: "IX_profiles_telegram_link_code",
                table: "profiles",
                column: "telegram_link_code",
                unique: true,
                filter: "telegram_link_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_staff_restaurant_id_user_id",
                table: "restaurant_staff",
                columns: new[] { "restaurant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_staff_user_id",
                table: "restaurant_staff",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_staff");

            migrationBuilder.DropIndex(
                name: "IX_profiles_referral_code",
                table: "profiles");

            migrationBuilder.DropIndex(
                name: "IX_profiles_referred_by",
                table: "profiles");

            migrationBuilder.DropIndex(
                name: "IX_profiles_telegram_link_code",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "special_hours",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "deposit_outcome",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "is_late_cancellation",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "pre_order_discount",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "restaurant_discount",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "referral_code",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "referral_rewarded",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "referred_by",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "telegram_chat_id",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "telegram_link_code",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "terms_accepted_at",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "refund_requested_at",
                table: "payments");

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expiry_date = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    expiry_month = table.Column<int>(type: "integer", nullable: false),
                    expiry_year = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    last_four = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_methods_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_user_id",
                table: "payment_methods",
                column: "user_id");
        }
    }
}
