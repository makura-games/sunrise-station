using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Content.Shared.Database;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbContext
{
    public DbSet<AHelpMessage> AHelpMessages { get; set; } = default!;
    public DbSet<MentorHelpTicket> MentorHelpTickets { get; set; } = default!;
    public DbSet<MentorHelpMessage> MentorHelpMessages { get; set; } = default!;
    public DbSet<UiLike> UiLikes { get; set; } = default!;
    public DbSet<TutorialCompletion> TutorialCompletions { get; set; } = default!;

    private static void ConfigureSunriseModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobAlternativeTitle>()
            .HasIndex(job => new { job.ProfileId, job.JobName })
            .IsUnique();
    }
}

public partial class Profile
{
    public float Width { get; set; } = 1f;
    public float Height { get; set; } = 1f;
    public string BodyType { get; set; } = null!;
    public string Voice { get; set; } = null!;
    public int HairColorType { get; set; }
    public string HairExtendedColor { get; set; } = null!;
    public int FacialHairColorType { get; set; }
    public string FacialHairExtendedColor { get; set; } = null!;
    public List<JobAlternativeTitle> JobAlternativeTitles { get; } = new();
}

public class JobAlternativeTitle
{
    public int Id { get; set; }
    public Profile Profile { get; set; } = null!;
    public int ProfileId { get; set; }

    [MaxLength(128)]
    public string JobName { get; set; } = null!;

    [MaxLength(128)]
    public string Title { get; set; } = null!;
}

[Table("ahelp_messages"), Index(nameof(ReceiverUserId))]
public class AHelpMessage
{
    [Key]
    public int Id { get; set; }

    [ForeignKey("Player")]
    public Guid ReceiverUserId { get; set; }

    [ForeignKey("Player")]
    public Guid SenderUserId { get; set; }

    public DateTimeOffset SentAt { get; set; }

    [Required, MaxLength(4096)]
    public string Message { get; set; } = string.Empty;

    public bool PlaySound { get; set; }
    public bool AdminOnly { get; set; }
}

/// <summary>
/// Represents a mentor help ticket.
/// </summary>
[Table("mentor_help_tickets"), Index(nameof(PlayerId)), Index(nameof(AssignedToUserId)), Index(nameof(Status)),
    Index(nameof(ClosedAt), nameof(AssignedToUserId))]
public class MentorHelpTicket
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The player who created the ticket.
    /// </summary>
    [ForeignKey("Player")]
    public Guid PlayerId { get; set; }

    /// <summary>
    /// The mentor or admin who claimed this ticket, or null if it is unclaimed.
    /// </summary>
    [ForeignKey("Player")]
    public Guid? AssignedToUserId { get; set; }

    /// <summary>
    /// The subject of the ticket.
    /// </summary>
    [Required, MaxLength(256)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The current status of the ticket.
    /// </summary>
    public MentorHelpTicketStatus Status { get; set; } = MentorHelpTicketStatus.Open;

    /// <summary>
    /// The time at which the ticket was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The time at which the ticket was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The time at which the ticket was closed, or null while it remains open.
    /// </summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>
    /// The user who closed the ticket.
    /// </summary>
    [ForeignKey("Player")]
    public Guid? ClosedByUserId { get; set; }

    /// <summary>
    /// The round in which the ticket was created.
    /// </summary>
    public int? RoundId { get; set; }

    /// <summary>
    /// The server on which the ticket was created.
    /// </summary>
    public int? ServerId { get; set; }
}

/// <summary>
/// Represents a message in a mentor help ticket.
/// </summary>
[Table("mentor_help_messages"), Index(nameof(TicketId)), Index(nameof(SentAt)),
    Index(nameof(SentAt), nameof(SenderUserId))]
public class MentorHelpMessage
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The ticket to which this message belongs.
    /// </summary>
    [ForeignKey("MentorHelpTicket")]
    public int TicketId { get; set; }

    public MentorHelpTicket Ticket { get; set; } = null!;

    /// <summary>
    /// The user who sent this message.
    /// </summary>
    [ForeignKey("Player")]
    public Guid SenderUserId { get; set; }

    /// <summary>
    /// The message content.
    /// </summary>
    [Required, MaxLength(4096)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The time at which the message was sent.
    /// </summary>
    public DateTimeOffset SentAt { get; set; }

    /// <summary>
    /// Whether this message is visible only to mentors and administrators.
    /// </summary>
    public bool IsStaffOnly { get; set; }
}

[PrimaryKey(nameof(ScopeId), nameof(ItemId), nameof(PlayerUserId))]
[Index(nameof(PlayerUserId), nameof(ScopeId))]
public sealed class UiLike
{
    [Required, MaxLength(128)]
    public string ScopeId { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string ItemId { get; set; } = string.Empty;

    [Required]
    public Guid PlayerUserId { get; set; }
}

[Table("tutorial_completion"), Index(nameof(PlayerUserId)), Index(nameof(TutorialId)),
    PrimaryKey(nameof(PlayerUserId), nameof(TutorialId))]
public class TutorialCompletion
{
    [Required, ForeignKey("Player")]
    public Guid PlayerUserId { get; set; }

    [Required]
    public string TutorialId { get; set; } = default!;

    public DateTimeOffset CompletedAt { get; set; }
    public int CompletionCount { get; set; } = 1;
    public double? AccountAgeDays { get; set; }
}
