using System.Reflection;
using LargeDocumentProcessing.Application.Common.Interfaces;
using LargeDocumentProcessing.Domain.Entities;
using LargeDocumentProcessing.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LargeDocumentProcessing.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentPage> DocumentPages => Set<DocumentPage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
