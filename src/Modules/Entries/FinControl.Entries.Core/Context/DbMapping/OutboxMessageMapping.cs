using FinControl.Entries.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinControl.Entries.Core.Context.DbMapping;

public sealed class OutboxMessageMapping : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", schema: "Entries");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).UseIdentityColumn();

        builder.Property(o => o.MessageType).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Payload).IsRequired();
        builder.Property(o => o.Exchange).IsRequired().HasMaxLength(200);
        builder.Property(o => o.RoutingKey).IsRequired().HasMaxLength(200);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.RetryCount).HasDefaultValue(0);
        builder.Property(o => o.LastError).HasMaxLength(2000);

        // Consultas de mensagens pendentes usam DeliveredAt IS NULL
        builder.HasIndex(o => o.DeliveredAt)
            .HasDatabaseName("idx_outbox_delivered_at");
    }
}

