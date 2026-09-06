namespace Hemordna.Infrastructure.Email;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "no-reply@hemordna.se";

    public string FromName { get; set; } = "Hemordna";
}
