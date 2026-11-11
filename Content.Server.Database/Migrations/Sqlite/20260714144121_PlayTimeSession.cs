using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PlayTimeSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "play_time_session",
                columns: table => new
                {
                    play_time_session_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_play_time_session", x => x.play_time_session_id);
                    table.ForeignKey(
                        name: "FK_play_time_session_player_player_id",
                        column: x => x.player_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_play_time_session_end_time",
                table: "play_time_session",
                column: "end_time");

            migrationBuilder.CreateIndex(
                name: "IX_play_time_session_player_id",
                table: "play_time_session",
                column: "player_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "play_time_session");
        }
    }
}
