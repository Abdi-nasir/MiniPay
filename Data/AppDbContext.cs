using Microsoft.EntityFrameworkCore;
using MiniApy.Api.Entities;

namespace MiniApy.Api.Data;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Merchant> Merchants => Set<Merchant>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<Settlement> Settlements => Set<Settlement>();

    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<IdempotencyRecord> IdempotencyRecords =>
    Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMerchant(modelBuilder);
        ConfigurePayment(modelBuilder);
        ConfigureTransaction(modelBuilder);
        ConfigureRefund(modelBuilder);
        ConfigureSettlement(modelBuilder);
        ConfigureWebhookEvent(modelBuilder);
        ConfigureIdempotencyRecord(modelBuilder);
    }

    private static void ConfigureMerchant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.ToTable("Merchants");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(x => x.ApiKeyHash)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.WebhookUrl)
                .HasMaxLength(2_000);
        });
    }

    private static void ConfigurePayment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
            {
                x.MerchantId,
                x.Reference
            })
                .IsUnique();

            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Reference)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.FailureReason)
                .HasMaxLength(500);

            entity.HasOne(x => x.Merchant)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTransaction(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Reference)
                .IsUnique();

            entity.HasIndex(x => x.ProviderReference);

            entity.HasIndex(x => new
            {
                x.PaymentId,
                x.Status
            });

            entity.Property(x => x.Reference)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ProviderReference)
                .HasMaxLength(200);

            entity.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.FailureReason)
                .HasMaxLength(500);

            entity.HasOne(x => x.Payment)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.SettlementId);

            entity.HasOne(x => x.Settlement)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.SettlementId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRefund(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Refund>(entity =>
        {
            entity.ToTable("Refunds");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Reference)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.PaymentId,
                x.Status
            });

            entity.Property(x => x.Reference)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Amount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.Reason)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.FailureReason)
                .HasMaxLength(500);

            entity.HasOne(x => x.Payment)
                .WithMany(x => x.Refunds)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSettlement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.ToTable("Settlements");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Reference)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.MerchantId,
                x.PeriodStart,
                x.PeriodEnd
            });

            entity.Property(x => x.Reference)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.GrossAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.RefundAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.FeeAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.FeePercentage)
    .HasPrecision(9, 4);

            entity.Property(x => x.NetAmount)
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.HasOne(x => x.Merchant)
                .WithMany(x => x.Settlements)
                .HasForeignKey(x => x.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWebhookEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("WebhookEvents");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
            {
                x.Status,
                x.NextAttemptAt
            });

            entity.HasIndex(x => new
            {
                x.MerchantId,
                x.CreatedAt
            });

            entity.Property(x => x.EventType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.TargetUrl)
                .HasMaxLength(2_000)
                .IsRequired();

            entity.Property(x => x.PayloadJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.LastResponseBody)
                .HasMaxLength(10_000);

            entity.Property(x => x.LastError)
                .HasMaxLength(2_000);

            entity.HasOne(x => x.Merchant)
                .WithMany(x => x.WebhookEvents)
                .HasForeignKey(x => x.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Payment)
                .WithMany(x => x.WebhookEvents)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIdempotencyRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records");

            entity.HasKey(item => item.Id);

            entity.Property(item => item.Operation)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(item => item.Key)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(item => item.RequestHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.HasIndex(item => new
            {
                item.MerchantId,
                item.Operation,
                item.Key
            })
                .IsUnique()
                .HasDatabaseName(
                    "ux_idempotency_merchant_operation_key");

            entity.HasIndex(item => item.ExpiresAt);
        });
    }
}