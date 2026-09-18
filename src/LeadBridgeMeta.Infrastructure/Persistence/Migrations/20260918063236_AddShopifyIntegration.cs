using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadBridgeMeta.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopifyIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShopifyConnectionId",
                table: "MetaLeadForms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopifyCustomerId",
                table: "LeadEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShopifyConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopDomain = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ShopName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopifyConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopifyConnections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShopifyFieldMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaLeadFormId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetaFieldKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    ShopifyFieldKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopifyFieldMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopifyFieldMappings_MetaLeadForms_MetaLeadFormId",
                        column: x => x.MetaLeadFormId,
                        principalTable: "MetaLeadForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShopifyFieldMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetaLeadForms_ShopifyConnectionId",
                table: "MetaLeadForms",
                column: "ShopifyConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyConnections_TenantId_ShopDomain",
                table: "ShopifyConnections",
                columns: new[] { "TenantId", "ShopDomain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyFieldMappings_MetaLeadFormId",
                table: "ShopifyFieldMappings",
                column: "MetaLeadFormId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyFieldMappings_TenantId",
                table: "ShopifyFieldMappings",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_MetaLeadForms_ShopifyConnections_ShopifyConnectionId",
                table: "MetaLeadForms",
                column: "ShopifyConnectionId",
                principalTable: "ShopifyConnections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetaLeadForms_ShopifyConnections_ShopifyConnectionId",
                table: "MetaLeadForms");

            migrationBuilder.DropTable(
                name: "ShopifyConnections");

            migrationBuilder.DropTable(
                name: "ShopifyFieldMappings");

            migrationBuilder.DropIndex(
                name: "IX_MetaLeadForms_ShopifyConnectionId",
                table: "MetaLeadForms");

            migrationBuilder.DropColumn(
                name: "ShopifyConnectionId",
                table: "MetaLeadForms");

            migrationBuilder.DropColumn(
                name: "ShopifyCustomerId",
                table: "LeadEvents");
        }
    }
}
