using AGUIWebChat.Server.Domain.Chat;
using AGUIWebChat.Server.Domain.AI;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Data
{
    public sealed class ChatDbContext : DbContext
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
        {
        }

        public DbSet<AIProvider> AIProviders => Set<AIProvider>();

        public DbSet<AIModel> AIModels => Set<AIModel>();

        public DbSet<AIModelDefaults> AIModelDefaults => Set<AIModelDefaults>();

        public DbSet<AIModelReasoningEffort> AIModelReasoningEfforts => Set<AIModelReasoningEffort>();

        public DbSet<AIAgent> AIAgents => Set<AIAgent>();

        public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
        public DbSet<StoredChatMessage> ChatMessages => Set<StoredChatMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureAIProvider(modelBuilder);
            ConfigureAIModel(modelBuilder);
            ConfigureAIModelDefaults(modelBuilder);
            ConfigureReasoningEffort(modelBuilder);
            ConfigureAIAgent(modelBuilder);
            ConfigureConversations(modelBuilder);
        }

        private static void ConfigureConversations(ModelBuilder modelBuilder)
        {
            var conversation = modelBuilder.Entity<ChatConversation>();
            conversation.ToTable("ChatConversations");
            conversation.HasKey(x => x.Id);
            conversation.Property(x => x.OwnerKey).HasMaxLength(64).IsRequired();
            conversation.Property(x => x.Title).HasMaxLength(200).IsRequired();
            conversation.Property(x => x.ActiveRunId).IsConcurrencyToken();
            conversation.HasIndex(x => new { x.OwnerKey, x.UpdatedAt });

            var message = modelBuilder.Entity<StoredChatMessage>();
            message.ToTable("ChatMessages");
            message.HasKey(x => x.Id);
            message.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            message.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            message.Property(x => x.Content).IsRequired();
            message.Property(x => x.RunId).HasMaxLength(100);
            message.Property(x => x.ModelName).HasMaxLength(200);
            message.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
            message.HasOne(x => x.Conversation).WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureAIProvider(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AIProvider>();

            entity.ToTable("AIProviders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProviderType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Endpoint).HasMaxLength(500);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasData(new AIProvider
            {
                Id = 1,
                Name = "Ollama",
                ProviderType = "Ollama",
                Endpoint = null,
                IsEnabled = true
            });
        }

        private static void ConfigureAIModel(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AIModel>();

            entity.ToTable("AIModels");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ModelId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ThinkingMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new
            {
                x.ProviderId,
                x.ModelId
            }).IsUnique();
            entity.HasOne(x => x.Provider).WithMany(x => x.Models).HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureAIAgent(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AIAgent>();

            entity.ToTable("AIAgents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SystemPrompt).IsRequired();
            entity.Property(x => x.ReasoningEffort).HasMaxLength(100);
            entity.HasOne(x => x.AIModel).WithMany().HasForeignKey(x => x.AIModelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.Name).IsUnique();
        }

        private static void ConfigureAIModelDefaults(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AIModelDefaults>();

            entity.ToTable("AIModelDefaults");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AIModelId).IsUnique();
            entity.HasOne(x => x.AIModel).WithOne(x => x.Defaults).HasForeignKey<AIModelDefaults>(x => x.AIModelId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureReasoningEffort(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AIModelReasoningEffort>();

            entity.ToTable("AIModelReasoningEfforts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new
            {
                x.AIModelId,
                x.Value
            }).IsUnique();
            entity.HasOne(x => x.AIModel).WithMany(x => x.ReasoningEfforts).HasForeignKey(x => x.AIModelId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
