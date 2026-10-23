using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Data;

public sealed class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<PasswordLogin> PasswordLogins => Set<PasswordLogin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<OwnedItem> OwnedItems => Set<OwnedItem>();
    public DbSet<StorePurchase> StorePurchases => Set<StorePurchase>();
    public DbSet<DailyState> DailyStates => Set<DailyState>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<PlayerStats> Stats => Set<PlayerStats>();
    public DbSet<LeaderboardScore> LeaderboardScores => Set<LeaderboardScore>();
    public DbSet<MatchRecord> Matches => Set<MatchRecord>();
    public DbSet<MatchHand> MatchHands => Set<MatchHand>();
    public DbSet<MatchMove> MatchMoves => Set<MatchMove>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.HasIndex(x => x.FriendCode).IsUnique();
            e.Property(x => x.FriendCode).HasMaxLength(7);
            e.Property(x => x.Name).HasMaxLength(16);
            e.Property(x => x.Color).HasMaxLength(7);
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.Planet).HasMaxLength(16);
            e.Property(x => x.SettingsJson).HasColumnType("jsonb");
            e.Property(x => x.DeviceId).HasMaxLength(128);
            e.Property(x => x.Platform).HasMaxLength(16);
        });
        b.Entity<PasswordLogin>(e =>
        {
            e.HasKey(x => x.Email);
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.Hash).HasMaxLength(200);
            e.HasIndex(x => x.AccountId).IsUnique();
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ExternalLogin>(e =>
        {
            e.HasKey(x => new { x.Provider, x.Subject });
            e.HasIndex(x => x.AccountId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.FamilyId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Wallet>(e =>
        {
            e.HasKey(x => x.AccountId);
            e.HasOne<Account>().WithOne().HasForeignKey<Wallet>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_wallets_non_negative", "chips >= 0 AND gems >= 0"));
        });
        b.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(x => new { x.AccountId, x.IdempotencyKey }).IsUnique();
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Reason).HasMaxLength(32);
            e.Property(x => x.IdempotencyKey).HasMaxLength(160);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OwnedItem>(e =>
        {
            e.HasKey(x => new { x.AccountId, x.ItemId });
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<StorePurchase>(e =>
        {
            e.HasKey(x => new { x.Platform, x.TransactionId });
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<DailyState>(e =>
        {
            e.HasKey(x => x.AccountId);
            e.HasOne<Account>().WithOne().HasForeignKey<DailyState>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Friendship>(e =>
        {
            e.HasKey(x => new { x.LowId, x.HighId });
            e.HasIndex(x => x.HighId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.LowId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.HighId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_friendships_ordered", "low_id < high_id"));
        });
        b.Entity<PlayerStats>(e =>
        {
            e.HasKey(x => x.AccountId);
            e.HasOne<Account>().WithOne().HasForeignKey<PlayerStats>(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LeaderboardScore>(e =>
        {
            e.HasKey(x => new { x.Board, x.AccountId });
            e.HasIndex(x => new { x.Board, x.Points }).IsDescending(false, true);
            e.Property(x => x.Board).HasMaxLength(16);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MatchRecord>(e =>
        {
            e.ToTable("matches");
            e.HasIndex(x => x.Status).HasFilter("status = 'Active'");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(12);
            e.Property(x => x.Mode).HasMaxLength(12);
            e.Property(x => x.OptionsJson).HasColumnType("jsonb");
        });
        b.Entity<MatchHand>(e =>
        {
            e.HasKey(x => new { x.MatchId, x.HandIndex });
            e.HasOne<MatchRecord>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MatchMove>(e =>
        {
            e.HasKey(x => new { x.MatchId, x.HandIndex, x.Version });
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.HasOne<MatchRecord>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<MatchParticipant>(e =>
        {
            e.HasKey(x => new { x.MatchId, x.Seat });
            e.HasIndex(x => new { x.AccountId, x.FinishedAt }).IsDescending(false, true);
            e.Property(x => x.Mode).HasMaxLength(12);
            e.HasOne<MatchRecord>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
