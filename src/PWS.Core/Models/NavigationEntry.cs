namespace PWS.Core.Models;

/// <summary>
/// Una voce nella history di navigazione del browser.
/// </summary>
public sealed class NavigationEntry
{
    public Uri Uri { get; init; } = new("pws://home");
    public string? Title { get; set; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public override string ToString() => Title ?? Uri.ToString();
}

