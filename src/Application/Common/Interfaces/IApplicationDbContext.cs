using LargeDocumentProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LargeDocumentProcessing.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Document> Documents { get; }

    DbSet<DocumentPage> DocumentPages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
