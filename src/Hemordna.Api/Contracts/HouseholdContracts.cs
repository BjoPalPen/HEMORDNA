namespace Hemordna.Api.Contracts;

/// <summary>Request body for creating a household.</summary>
public sealed record CreateHouseholdRequest(string? Name);

/// <summary>A household as the API exposes it. Never the domain entity itself.</summary>
public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<HouseholdMemberResponse> Members,
    IReadOnlyList<AreaResponse> Areas);

public sealed record HouseholdMemberResponse(Guid Id, string DisplayName, bool IsActive);

public sealed record AreaResponse(Guid Id, string Name, bool IsActive);
