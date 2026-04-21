using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkPipeline.Infrastructure.Migrations
{
    public partial class AddJiraStatusMappings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JiraInProgressStatus",
                table: "Workspaces",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JiraMergedStatus",
                table: "Workspaces",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "JiraInProgressStatus", table: "Workspaces");
            migrationBuilder.DropColumn(name: "JiraMergedStatus", table: "Workspaces");
        }
    }
}
