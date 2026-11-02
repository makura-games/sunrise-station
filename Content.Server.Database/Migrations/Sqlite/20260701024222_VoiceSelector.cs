using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class VoiceSelector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sunrise-Edit - сохраняем TTS до создания отдельного голоса Wizden.
            migrationBuilder.RenameColumn(
                name: "voice",
                table: "profile",
                newName: "tts_voice");

            migrationBuilder.AddColumn<string>(
                name: "voice",
                table: "profile",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "voice",
                table: "profile");

            // Sunrise-Edit - возвращаем TTS в прежнюю колонку при откате.
            migrationBuilder.RenameColumn(
                name: "tts_voice",
                table: "profile",
                newName: "voice");
        }
    }
}
