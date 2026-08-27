using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ChatNode.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ManualPartsPgvector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "EmbeddingVector",
                table: "manual_parts",
                type: "vector(1024)",
                nullable: true);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_manual_parts_embedding_vector " +
                "ON manual_parts USING hnsw (\"EmbeddingVector\" vector_cosine_ops)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_manual_parts_embedding_vector");

            migrationBuilder.DropColumn(
                name: "EmbeddingVector",
                table: "manual_parts");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
