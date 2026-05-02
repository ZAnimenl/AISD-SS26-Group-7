using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class WorkspaceQuestionStateConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkspaceQuestionState>(entity =>
        {
            entity.ToTable("workspace_question_states");
            entity.HasKey(state => state.Id);
            entity.Property(state => state.SelectedLanguage).HasMaxLength(64).IsRequired();
            entity.Property(state => state.ActiveFile).HasMaxLength(200).IsRequired();
            entity.Property(state => state.FilesJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(state => new { state.SessionId, state.QuestionId }).IsUnique();
            entity.HasOne(state => state.Session)
                .WithMany(session => session.WorkspaceStates)
                .HasForeignKey(state => state.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
