using Hemordna.Domain.Common;

namespace Hemordna.Domain.Households;

/// <summary>
/// How a member wants their own tasks presented. Individual, not a household setting - see
/// docs/PRODUCT.md §7. MVP priority order: Text, ImageAndText, LargeText, OneAtATime.
/// ImageOnly and ReadAloud are modelled now so the enum will not need a breaking change later,
/// but nothing implements them yet.
/// </summary>
public enum PresentationMode
{
    Text,
    ImageAndText,
    LargeText,
    OneAtATime,
    ImageOnly,
    ReadAloud
}

/// <summary>MVP has exactly two levels - see docs/PRODUCT.md §8.</summary>
public enum MotivationLevel
{
    None,
    Calm
}

/// <summary>
/// One member's personal display preferences. Never affects what any other member in the
/// household sees.
/// </summary>
public sealed class MemberPreference
{
    private MemberPreference(Guid householdId, Guid memberId, PresentationMode presentation, MotivationLevel motivation)
    {
        HouseholdId = householdId;
        MemberId = memberId;
        Presentation = presentation;
        Motivation = motivation;
    }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid MemberId { get; private set; }

    public PresentationMode Presentation { get; private set; }

    public MotivationLevel Motivation { get; private set; }

    public static MemberPreference CreateDefault(Guid householdId, Guid memberId)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));

        return new MemberPreference(householdId, memberId, PresentationMode.Text, MotivationLevel.None);
    }

    public void ChangePresentation(PresentationMode presentation)
    {
        if (!Enum.IsDefined(presentation))
        {
            throw new ArgumentOutOfRangeException(nameof(presentation), presentation, "Not a valid presentation mode.");
        }

        Presentation = presentation;
    }

    public void ChangeMotivation(MotivationLevel motivation)
    {
        if (!Enum.IsDefined(motivation))
        {
            throw new ArgumentOutOfRangeException(nameof(motivation), motivation, "Not a valid motivation level.");
        }

        Motivation = motivation;
    }
}
