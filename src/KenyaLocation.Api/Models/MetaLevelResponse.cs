namespace KenyaLocation.Api.Models;

public sealed record MetaLevelResponse(
    int Level,
    string LevelName,
    int Count
);

public sealed record MetaResponse(
    string Service,
    string Version,
    List<MetaLevelResponse> Levels
);