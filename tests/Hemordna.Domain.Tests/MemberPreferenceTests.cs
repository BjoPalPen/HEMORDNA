using Hemordna.Domain.Households;

namespace Hemordna.Domain.Tests;

public class MemberPreferenceTests
{
    [Fact]
    public void CreateDefault_starts_with_text_and_no_motivation()
    {
        var preference = MemberPreference.CreateDefault(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(PresentationMode.Text, preference.Presentation);
        Assert.Equal(MotivationLevel.None, preference.Motivation);
    }

    [Fact]
    public void ChangePresentation_replaces_the_mode()
    {
        var preference = MemberPreference.CreateDefault(Guid.NewGuid(), Guid.NewGuid());

        preference.ChangePresentation(PresentationMode.LargeText);

        Assert.Equal(PresentationMode.LargeText, preference.Presentation);
    }

    [Fact]
    public void ChangeMotivation_replaces_the_level()
    {
        var preference = MemberPreference.CreateDefault(Guid.NewGuid(), Guid.NewGuid());

        preference.ChangeMotivation(MotivationLevel.Calm);

        Assert.Equal(MotivationLevel.Calm, preference.Motivation);
    }
}
