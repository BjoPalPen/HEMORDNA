using System.Security.Cryptography;
using Hemordna.Domain.Areas;
using Hemordna.Domain.Common;

namespace Hemordna.Domain.Households;

/// <summary>
/// A household: one to many people who share a home. This is the tenant and security
/// boundary of the model - everything a client can reach is scoped by <see cref="Id"/>.
/// </summary>
/// <remarks>
/// Members and areas are owned by the household because both are small, bounded sets that
/// are almost always needed together. Task definitions and occurrences are deliberately
/// <em>not</em> owned here: they grow without bound and must be queryable per member and
/// per date without loading the whole household.
/// </remarks>
public sealed class Household
{
    private readonly List<HouseholdMember> _members = [];
    private readonly List<Area> _areas = [];

    private Household(Guid id, string name, DateTimeOffset createdAt, string inviteCode)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        InviteCode = inviteCode;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<HouseholdMember> Members => _members.AsReadOnly();

    public IReadOnlyCollection<Area> Areas => _areas.AsReadOnly();

    /// <summary>
    /// The code someone else types in at sign-up to join this household instead of creating
    /// their own - see <see cref="RegenerateInviteCode"/> for revoking a leaked one. Not a
    /// secret in the security sense (it only grants membership, nothing more), but random
    /// enough that it cannot be guessed.
    /// </summary>
    public string InviteCode { get; private set; }

    public static Household Create(string name, DateTimeOffset createdAt)
        => new(Guid.NewGuid(), Guard.AgainstNullOrWhiteSpace(name, nameof(name)), createdAt, GenerateInviteCode());

    public void Rename(string name) => Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

    /// <summary>
    /// Replaces the invite code with a fresh one, so a code shared with the wrong person (or
    /// simply no longer wanted) stops working. Anyone who already joined keeps their
    /// membership - this only affects future attempts to join.
    /// </summary>
    public void RegenerateInviteCode() => InviteCode = GenerateInviteCode();

    // Excludes visually ambiguous characters (0/O, 1/I/L) since the code is meant to be read
    // aloud or typed by hand. 8 characters from this 32-letter alphabet is over a trillion
    // combinations - not guessable, while staying short enough to share easily.
    private const string InviteCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int InviteCodeLength = 8;

    private static string GenerateInviteCode()
        => string.Create(InviteCodeLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = InviteCodeAlphabet[RandomNumberGenerator.GetInt32(InviteCodeAlphabet.Length)];
            }
        });

    /// <summary>Adds a member. Display names must be unique within the household.</summary>
    public HouseholdMember AddMember(
        string displayName,
        WeeklyTimeBudget weeklyTimeBudget,
        DateTimeOffset createdAt,
        HouseholdRole? role = null)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName));

        if (_members.Any(m => string.Equals(m.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"A member named '{trimmed}' already exists in this household.");
        }

        var member = HouseholdMember.Create(Id, trimmed, weeklyTimeBudget, createdAt, role);
        _members.Add(member);
        return member;
    }

    /// <summary>Adds an area. Names must be unique within the household.</summary>
    public Area AddArea(string name)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

        if (_areas.Any(a => string.Equals(a.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"An area named '{trimmed}' already exists in this household.");
        }

        var area = Area.Create(Id, trimmed);
        _areas.Add(area);
        return area;
    }
}
