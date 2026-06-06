using Backend.Configuration;
using Backend.Domain;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Persistence;

public sealed class SeedAdminSeeder(
    OjSharpDbContext dbContext,
    PasswordHasher passwordHasher,
    IOptions<SeedAdminOptions> seedAdminOptions)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        seedAdminOptions.Value.Validate();

        var options = seedAdminOptions.Value;
        var now = DateTimeOffset.UtcNow;
        var admin = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == options.Email, cancellationToken);
        if (admin is null)
        {
            dbContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "Seed Administrator",
                Email = options.Email,
                PasswordHash = passwordHasher.Hash(options.Password),
                Role = UserRoles.Administrator,
                Status = UserStatuses.Active,
                CreatedAt = now
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        admin.FullName = string.IsNullOrWhiteSpace(admin.FullName) ? "Seed Administrator" : admin.FullName;
        admin.PasswordHash = passwordHasher.Hash(options.Password);
        admin.Role = UserRoles.Administrator;
        admin.Status = UserStatuses.Active;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
