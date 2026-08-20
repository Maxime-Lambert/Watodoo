namespace Watodoo.Configuration;

public sealed class IgdbOptions
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    public int SeedMaxItems { get; init; } = 5000;

    public int NightlyLookbackDays { get; init; } = 3;
}
