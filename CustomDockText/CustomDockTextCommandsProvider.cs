using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CustomDockText;

internal static class Ids
{
    public const string Provider = "customdocktext";
    public const string Edit = "customdocktext.edit";

    /// <summary>
    /// Command Palette resolves pinned Dock bands by this ID alone, ignoring the provider, so the
    /// Store and sideloaded builds use different IDs. Otherwise a pin left over from one build
    /// hides the other build's band from the Dock's Edit list.
    /// </summary>
    public static string Band => BandId.Value;

    private static readonly Lazy<string> BandId = new(() => IsStoreBuild() ? "customdocktext.band" : "customdocktext.sideloaded.band");

    private static bool IsStoreBuild()
    {
        try
        {
            return Windows.ApplicationModel.Package.Current.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.Store;
        }
        catch
        {
            return false;
        }
    }
}

public sealed partial class CustomDockTextCommandsProvider : CommandProvider
{
    private readonly Lock _lock = new();
    private DockTextItem? _item;

    public CustomDockTextCommandsProvider()
    {
        Id = Ids.Provider;
        DisplayName = "Custom Dock Text";
        Icon = IconHelpers.FromRelativePath(@"Assets\StoreLogo.png");
    }

    public override ICommandItem[] TopLevelCommands() =>
    [
        new CommandItem(new DockTextPage())
        {
            Title = "Set Dock text",
            Subtitle = "Change the custom text and icon on the Dock",
        },
    ];

    // Fresh objects for each Command Palette connection. With SDK 0.12, a property change goes to
    // each subscriber in turn and stops at the first that throws, so a Command Palette that has
    // exited would otherwise block updates to the running one.
    public override ICommandItem[]? GetDockBands()
    {
        var item = new DockTextItem();
        lock (_lock)
        {
            _item?.Detach();
            _item = item;
        }

        return [new WrappedDockItem([item], Ids.Band, "Custom Dock Text")];
    }
}
