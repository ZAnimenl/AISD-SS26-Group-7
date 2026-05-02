using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public sealed class OjSharpDbContext(DbContextOptions<OjSharpDbContext> options) : DbContext(options)
{
    public DbSet<AiInteraction> AiInteractions => Set<AiInteraction>();

    public DbSet<Assessment> Assessments => Set<Assessment>();

    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();

    public DbSet<ExecutionRecord> ExecutionRecords => Set<ExecutionRecord>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<TestCase> TestCases => Set<TestCase>();

    public DbSet<User> Users => Set<User>();

    public DbSet<WorkspaceQuestionState> WorkspaceQuestionStates => Set<WorkspaceQuestionState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        UserConfiguration.Configure(modelBuilder);
        AssessmentConfiguration.Configure(modelBuilder);
        QuestionConfiguration.Configure(modelBuilder);
        TestCaseConfiguration.Configure(modelBuilder);
        AssessmentSessionConfiguration.Configure(modelBuilder);
        WorkspaceQuestionStateConfiguration.Configure(modelBuilder);
        SubmissionConfiguration.Configure(modelBuilder);
        ExecutionRecordConfiguration.Configure(modelBuilder);
        AiInteractionConfiguration.Configure(modelBuilder);
    }
}
