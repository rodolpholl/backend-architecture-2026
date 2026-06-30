using Microsoft.EntityFrameworkCore;

namespace FinControl.Infrastructure.Data;

/// <summary>
/// DbContext base com auditoria automática (CreatedAt / UpdatedAt).
///
/// O Outbox é gerenciado NATIVAMENTE pelo Wolverine via WolverineFx.EntityFrameworkCore.
/// Não existe tabela ou serviço manual de Outbox aqui.
/// Para ativá-lo num módulo, configure:
///   opts.PersistMessagesWithMarten(...) OU
///   opts.UseWolverineMessagestore("connStr", schema)
/// no WolverineOptions do Program.cs do módulo.
/// </summary>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AplicarAuditoria();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        AplicarAuditoria();
        return base.SaveChanges();
    }

    private void AplicarAuditoria()
    {
        var agora = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = agora;
                entry.Entity.UpdatedAt = agora;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = agora;
            }
        }
    }
}
