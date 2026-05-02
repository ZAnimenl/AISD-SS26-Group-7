using Backend.Configuration;
using Backend.Domain;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Persistence;

public sealed class DemoDataSeeder(
    OjSharpDbContext dbContext,
    PasswordHasher passwordHasher,
    IOptions<SeedAdminOptions> seedAdminOptions)
{
    public static readonly Guid StudentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid PythonAssessmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ArraySumQuestionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid ReverseStringQuestionId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        seedAdminOptions.Value.Validate();

        var now = DateTimeOffset.UtcNow;
        await EnsureSeedAdminAsync(now, cancellationToken);

        if (await dbContext.Assessments.AnyAsync(cancellationToken))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == StudentUserId || user.Email == "student@example.com", cancellationToken))
        {
            dbContext.Users.Add(new User
            {
                Id = StudentUserId,
                FullName = "Alice Student",
                Email = "student@example.com",
                PasswordHash = passwordHasher.Hash("password"),
                Role = UserRoles.Student,
                Status = UserStatuses.Active,
                CreatedAt = now
            });
        }

        await SeedAssessmentAsync(now, cancellationToken);
    }

    private async Task EnsureSeedAdminAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var options = seedAdminOptions.Value;
        var admin = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == options.Email, cancellationToken);
        if (admin is null)
        {
            dbContext.Users.Add(new User
            {
                Id = AdminUserId,
                FullName = "Ada Admin",
                Email = options.Email,
                PasswordHash = passwordHasher.Hash(options.Password),
                Role = UserRoles.Administrator,
                Status = UserStatuses.Active,
                CreatedAt = now
            });
            return;
        }

        admin.FullName = string.IsNullOrWhiteSpace(admin.FullName) ? "Ada Admin" : admin.FullName;
        admin.PasswordHash = passwordHasher.Hash(options.Password);
        admin.Role = UserRoles.Administrator;
        admin.Status = UserStatuses.Active;
    }

    private async Task SeedAssessmentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var assessment = new Assessment
        {
            Id = PythonAssessmentId,
            Title = "Python and JavaScript Coding Assessment",
            Description = "Solve the following coding tasks.",
            DurationMinutes = 60,
            Status = AssessmentStatuses.Active,
            AiEnabled = true,
            CreatedAt = now
        };

        assessment.Questions.Add(new Question
        {
            Id = ArraySumQuestionId,
            AssessmentId = assessment.Id,
            Title = "Array Sum",
            ProblemDescriptionMarkdown = "## Task\nWrite a function that returns the sum of an array.",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = "def solve(arr):\n    pass\n",
                ["javascript"] = "function solve(arr) {\n  // TODO\n}\n"
            }),
            SortOrder = 1,
            MaxScore = 50,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    Name = "sample test 1",
                    Visibility = TestCaseVisibilities.Public,
                    Input = "[1,2,3]",
                    ExpectedOutput = "6"
                },
                new TestCase
                {
                    Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                    Name = "hidden mixed signs",
                    Visibility = TestCaseVisibilities.Hidden,
                    Input = "[-3,5,10]",
                    ExpectedOutput = "12"
                }
            ]
        });

        assessment.Questions.Add(new Question
        {
            Id = ReverseStringQuestionId,
            AssessmentId = assessment.Id,
            Title = "Reverse String",
            ProblemDescriptionMarkdown = "## Task\nReturn the input string in reverse order.",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = "def solve(value):\n    pass\n",
                ["javascript"] = "function solve(value) {\n  // TODO\n}\n"
            }),
            SortOrder = 2,
            MaxScore = 50,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    Name = "sample test 1",
                    Visibility = TestCaseVisibilities.Public,
                    Input = "hello",
                    ExpectedOutput = "olleh"
                },
                new TestCase
                {
                    Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    Name = "hidden palindrome",
                    Visibility = TestCaseVisibilities.Hidden,
                    Input = "level",
                    ExpectedOutput = "level"
                }
            ]
        });

        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
