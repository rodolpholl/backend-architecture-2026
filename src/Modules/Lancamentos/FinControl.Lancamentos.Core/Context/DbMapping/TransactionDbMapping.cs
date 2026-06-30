using FinControl.Transactions.Core.Domain;
using FinControl.Transactions.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinControl.Transactions.Core.Context.DbMapping;

/// <summary>
/// Entity Framework Core mapping configuration for the Transaction entity.
/// </summary>
public class TransactionDbMapping : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("lancamentos", schema: "lancamentos");

        // Primary Key
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();

        // Navigation ID (external reference - auto-generated UUID)
        builder.Property(l => l.NavigationId)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd()
            .HasColumnName("navigation_id");

        builder.HasIndex(l => l.NavigationId)
            .IsUnique()
            .HasDatabaseName("idx_lancamento_navigation_id");

        // Category
        builder.Property(l => l.Category)
            .HasConversion<int>()
            .IsRequired()
            .HasColumnName("modalidade");

        // Amount (in cents, stored as BIGINT)
        builder.Property(l => l.Amount)
            .IsRequired()
            .HasColumnName("valor");

        // Descrição (optional)
        builder.Property(l => l.Description)
            .HasMaxLength(500)
            .HasColumnName("descricao");

        // Data do Lançamento
        builder.Property(l => l.TransactionDate)
            .IsRequired()
            .HasColumnName("data_lancamento");

        // Idempotency key — índice único garante que retransmissões não criem duplicatas
        builder.Property(l => l.IdempotencyKey)
            .IsRequired()
            .HasColumnName("idempotency_key");

        builder.HasIndex(l => l.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("idx_lancamento_idempotency_key");

        // Computed properties - not mapped
        builder.Ignore(l => l.FormattedAmount);
        builder.Ignore(l => l.Type);
        builder.Ignore(l => l.FormattedType);

        // Audit properties (IAuditableDomainEntity)
        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasColumnName("criado_em");

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("atualizado_em");

        builder.Property(l => l.CreatedBy)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("criado_por");

        builder.Property(l => l.CreatedByName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("criado_por_nome");

        builder.Property(l => l.CreatedByEmail)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("criado_por_email");

        builder.Property(l => l.UpdatedBy)
            .HasMaxLength(100)
            .HasColumnName("atualizado_por");

        builder.Property(l => l.UpdatedByName)
            .HasMaxLength(200)
            .HasColumnName("atualizado_por_nome");

        builder.Property(l => l.UpdatedByEmail)
            .HasMaxLength(200)
            .HasColumnName("atualizado_por_email");

        // Soft delete properties (ISoftDeleteDomainEntity)
        builder.Property(l => l.DeletedAt)
            .HasColumnName("deletado_em");

        builder.Property(l => l.DeletedBy)
            .HasMaxLength(100)
            .HasColumnName("deletado_por");

        // Indexes for query performance
        builder.HasIndex(l => l.TransactionDate)
            .HasDatabaseName("idx_lancamento_data");

        builder.HasIndex(l => new{l.TransactionDate, l.Category})
            .HasDatabaseName("idx_lancamento_data_modalidade");

        builder.HasIndex(l => new { l.CreatedBy, l.TransactionDate })
            .HasDatabaseName("idx_lancamento_criado_por_data");

        builder.HasIndex(l => l.CreatedBy)
            .HasDatabaseName("idx_lancamento_criado_por");

        builder.HasIndex(l => l.DeletedAt)
            .HasDatabaseName("idx_lancamento_deletado");
    }
}
