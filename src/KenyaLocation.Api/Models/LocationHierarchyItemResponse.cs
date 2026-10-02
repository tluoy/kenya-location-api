namespace KenyaLocation.Api.Models;

public sealed record LocationHierarchyItemResponse(
    string Id,
    string Code,
    string Name,
    int Level,
    string LevelName,
    string? ParentId,
    int Depth
);