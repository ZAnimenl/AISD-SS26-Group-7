using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class UserConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(64).IsRequired();
            entity.Property(user => user.Status).HasMaxLength(64).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();

            entity.Property(user => user.AuthProvider).HasMaxLength(32).IsRequired().HasDefaultValue("email");
            entity.Property(user => user.GoogleId).HasMaxLength(64);
            entity.Property(user => user.EmailVerified).HasDefaultValue(false);
            entity.Property(user => user.EmailVerificationToken).HasMaxLength(128);
            entity.HasIndex(user => user.GoogleId);
            entity.HasIndex(user => user.EmailVerificationToken);
        });
    }
}
