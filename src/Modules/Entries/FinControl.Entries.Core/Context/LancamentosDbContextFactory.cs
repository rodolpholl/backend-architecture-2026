using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinControl.Entries.Core.Context;

public class TransactionsDbContextFactory : IDesignTimeDbContextFactory<TransactionsDbContext>
{
    public TransactionsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=fincontrol_Entries;Username=fincontrol_admin;Password=fincontrol_dev_password_123")
            .Options;

        return new TransactionsDbContext(options);
    }
}

