namespace MediatrUnionPoc.Api.Authentication;

/// <summary>The optional, git-ignored, reloadable settings files a developer keeps beside <c>appsettings.Development.json</c>.</summary>
public static class LocalSettingsFile
{
    /// <summary>The hand-edited file, loaded only in the Development environment.</summary>
    public const string Name = "appsettings.Development.local.json";

    /// <summary>
    /// The file the <c>manage-api</c> scripts write to switch the development user (<c>set-user</c> and
    /// <c>clear-user</c>); loaded after <see cref="Name"/>, so it wins. Kept apart so a script can replace or
    /// delete it whole without merging into a hand-edited file.
    /// </summary>
    public const string DevUserName = "appsettings.Development.devuser.json";

    /// <summary>Every local settings file, in load order (later wins).</summary>
    public static IReadOnlyList<string> All { get; } = [Name, DevUserName];
}
