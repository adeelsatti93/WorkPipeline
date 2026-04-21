using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkPipeline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    GitHubToken = table.Column<string>(type: "TEXT", nullable: true),
                    GitHubRepoFullName = table.Column<string>(type: "TEXT", nullable: true),
                    GitHubBranch = table.Column<string>(type: "TEXT", nullable: true),
                    RepoLocalPath = table.Column<string>(type: "TEXT", nullable: true),
                    JiraCredentials = table.Column<string>(type: "TEXT", nullable: true),
                    JiraEmail = table.Column<string>(type: "TEXT", nullable: true),
                    JiraSiteUrl = table.Column<string>(type: "TEXT", nullable: true),
                    JiraBoardId = table.Column<string>(type: "TEXT", nullable: true),
                    JiraBoardName = table.Column<string>(type: "TEXT", nullable: true),
                    ClaudeApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TotalTickets = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedTickets = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSessions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketWorks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JiraTicketKey = table.Column<string>(type: "TEXT", nullable: false),
                    JiraTicketTitle = table.Column<string>(type: "TEXT", nullable: false),
                    JiraTicketDescription = table.Column<string>(type: "TEXT", nullable: true),
                    JiraIssueType = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", nullable: false),
                    Assignee = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchName = table.Column<string>(type: "TEXT", nullable: true),
                    PullRequestUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PullRequestNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    JiraTransitionedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    QueuePosition = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketWorks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketWorks_WorkSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "WorkSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TranscriptEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketWorkId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRetryRound = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TranscriptEntries_TicketWorks_TicketWorkId",
                        column: x => x.TicketWorkId,
                        principalTable: "TicketWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    TicketWorkId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookEvents_TicketWorks_TicketWorkId",
                        column: x => x.TicketWorkId,
                        principalTable: "TicketWorks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketWorks_SessionId",
                table: "TicketWorks",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TranscriptEntries_TicketWorkId",
                table: "TranscriptEntries",
                column: "TicketWorkId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_TicketWorkId",
                table: "WebhookEvents",
                column: "TicketWorkId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_WorkspaceId",
                table: "WorkSessions",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranscriptEntries");

            migrationBuilder.DropTable(
                name: "WebhookEvents");

            migrationBuilder.DropTable(
                name: "TicketWorks");

            migrationBuilder.DropTable(
                name: "WorkSessions");

            migrationBuilder.DropTable(
                name: "Workspaces");
        }
    }
}
