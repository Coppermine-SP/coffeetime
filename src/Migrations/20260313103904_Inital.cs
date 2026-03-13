using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace coffeetime.Migrations
{
    /// <inheritdoc />
    public partial class Inital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ItemName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ItemDescription = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ItemPrice = table.Column<uint>(type: "int unsigned", nullable: false),
                    ItemSize = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.ItemId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_cache",
                columns: table => new
                {
                    UserObjectGuid = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    UserDisplayName = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_cache", x => x.UserObjectGuid);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "package_batches",
                columns: table => new
                {
                    BatchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    RoastedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BatchCount = table.Column<int>(type: "int", nullable: false),
                    RemainingCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_batches", x => x.BatchId);
                    table.CheckConstraint("CK_pkg_batch_count", "`BatchCount` BETWEEN 1 AND 30");
                    table.CheckConstraint("CK_pkg_remaining_count", "`RemainingCount` BETWEEN 0 AND 30");
                    table.ForeignKey(
                        name: "FK_pkg_batch_item",
                        column: x => x.ItemId,
                        principalTable: "items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pkg_batch_owner",
                        column: x => x.OwnerUserId,
                        principalTable: "user_cache",
                        principalColumn: "UserObjectGuid",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "batch_takes",
                columns: table => new
                {
                    BatchTakeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    TakenByUserId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_takes", x => x.BatchTakeId);
                    table.CheckConstraint("CK_batch_take_qty", "`Quantity` BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "FK_batch_take_batch",
                        column: x => x.BatchId,
                        principalTable: "package_batches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_batch_take_user",
                        column: x => x.TakenByUserId,
                        principalTable: "user_cache",
                        principalColumn: "UserObjectGuid",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_batch_takes_TakenByUserId",
                table: "batch_takes",
                column: "TakenByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_take_batch_created",
                table: "batch_takes",
                columns: new[] { "BatchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_take_created",
                table: "batch_takes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_pkg_item_roasted",
                table: "package_batches",
                columns: new[] { "ItemId", "RoastedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_pkg_owner_remaining",
                table: "package_batches",
                columns: new[] { "OwnerUserId", "RemainingCount" });

            migrationBuilder.CreateIndex(
                name: "IX_pkg_remaining_roasted",
                table: "package_batches",
                columns: new[] { "RemainingCount", "RoastedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batch_takes");

            migrationBuilder.DropTable(
                name: "package_batches");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.DropTable(
                name: "user_cache");
        }
    }
}
