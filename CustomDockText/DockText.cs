using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CustomDockText;

/// <summary>The user's custom Dock text and optional icon, saved in the package's data folder.</summary>
internal static class DockTextStore
{
    public const int MaxLength = 80;

    /// <summary>Optional icons, as Segoe Fluent glyphs so they match the Dock's other monochrome icons.</summary>
    public static readonly (string Key, string Label, string Glyph)[] Icons =
    [
        ("none", "No icon", ""),
        ("lightbulb", "Lightbulb", "\uEA80"),
        ("star", "Star", "\uE734"),
        ("starfill", "Star (filled)", "\uE735"),
        ("heart", "Heart", "\uEB51"),
        ("heartfill", "Heart (filled)", "\uEB52"),
        ("code", "Code", "\uE943"),
        ("terminal", "Terminal", "\uE756"),
        ("bug", "Bug", "\uEBE8"),
        ("robot", "Robot", "\uE99A"),
        ("flag", "Flag", "\uE7C1"),
        ("pin", "Pin", "\uE718"),
        ("target", "Target", "\uF272"),
        ("smile", "Smile", "\uE76E"),
        ("celebrate", "Celebrate", "\uEC24"),
        ("home", "Home", "\uE80F"),
        ("calendar", "Calendar", "\uE787"),
        ("clock", "Clock", "\uE917"),
        ("music", "Music", "\uE8D6"),
        ("camera", "Camera", "\uE722"),
        ("mail", "Mail", "\uE715"),
        ("phone", "Phone", "\uE717"),
        ("globe", "Globe", "\uE774"),
        ("airplane", "Airplane", "\uE709"),
        ("car", "Car", "\uE804"),
        ("coffee", "Coffee", "\uEC32"),
        ("lightning", "Lightning", "\uE945"),
        ("sun", "Sun", "\uE706"),
        ("moon", "Moon", "\uE708"),
        ("cloud", "Cloud", "\uE753"),
        ("leaf", "Leaf", "\uEC0A"),
        ("game", "Game controller", "\uE7FC"),
        ("library", "Books", "\uE8F1"),
        ("work", "Briefcase", "\uE821"),
        ("shop", "Shopping cart", "\uE7BF"),
        ("people", "People", "\uE716"),
        ("person", "Person", "\uE77B"),
        ("health", "Health", "\uE95E"),
        ("megaphone", "Megaphone", "\uE789"),
        ("tag", "Tag", "\uE8EC"),
        ("check", "Check mark", "\uE73E"),
        ("info", "Info", "\uE946"),
        ("warning", "Warning", "\uE7BA"),
        ("pencil", "Pencil", "\uE70F"),
    ];

    private static readonly string Folder = DataFolder();
    private static readonly string FilePath = Path.Combine(Folder, "dock-text.json");
    private static readonly string LegacyPath = Path.Combine(Folder, "dock-text.txt");

    public static event Action? Changed;

    public static string Text { get; private set; } = string.Empty;

    public static string IconKey { get; private set; } = "none";

    public static string Glyph => Icons.FirstOrDefault(i => i.Key == IconKey).Glyph ?? string.Empty;

    static DockTextStore() => Load();

    public static void SetText(string text)
    {
        text = text.ReplaceLineEndings(" ").Trim();
        Text = text.Length > MaxLength ? text[..MaxLength] : text;
        Save();
    }

    public static void SetIcon(string iconKey)
    {
        IconKey = Icons.Any(i => i.Key == iconKey) ? iconKey : "none";
        Save();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, new JsonObject { ["text"] = Text, ["icon"] = IconKey }.ToJsonString());
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch
        {
            // Still shown for this session even if it couldn't be saved.
        }

        if (Changed is { } changed)
        {
            foreach (Action handler in changed.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch
                {
                    // One failing subscriber must not stop the others.
                }
            }
        }
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var o = JsonNode.Parse(File.ReadAllText(FilePath));
                Text = o?["text"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var icon = o?["icon"]?.GetValue<string>() ?? "none";
                IconKey = Icons.Any(i => i.Key == icon) ? icon : "none";
            }
            else if (File.Exists(LegacyPath))
            {
                Text = File.ReadAllText(LegacyPath).Trim();
            }
        }
        catch
        {
            // Start empty.
        }
    }

    private static string DataFolder()
    {
        try
        {
            return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "CustomDockText");
        }
    }
}

/// <summary>
/// What the Dock text opens: "Edit text" first, then every icon drawn with its real glyph
/// (a dropdown can't show icon-font characters). The current icon is tagged.
/// </summary>
internal sealed partial class DockTextPage : ListPage
{
    public DockTextPage()
    {
        Id = Ids.Edit;
        Name = "Edit";
        Title = "Dock text";
        Icon = new IconInfo("");
        PlaceholderText = "Search icons";
    }

    public override IListItem[] GetItems()
    {
        var items = new System.Collections.Generic.List<IListItem>
        {
            new ListItem(new EditTextPage())
            {
                Title = "Edit text",
                Subtitle = string.IsNullOrEmpty(DockTextStore.Text) ? "No text yet" : DockTextStore.Text,
                Icon = new IconInfo(""),
            },
        };

        foreach (var (key, label, glyph) in DockTextStore.Icons)
        {
            items.Add(new ListItem(new SetIconCommand(key))
            {
                Title = label,
                Section = "Icon",
                Icon = new IconInfo(glyph),
                Tags = key == DockTextStore.IconKey ? [new Tag("Current")] : [],
            });
        }

        return [.. items];
    }

    private sealed partial class SetIconCommand : InvokableCommand
    {
        private readonly string _key;

        public SetIconCommand(string key)
        {
            _key = key;
            Name = "Use this icon";
        }

        public override ICommandResult Invoke()
        {
            DockTextStore.SetIcon(_key);
            return CommandResult.Dismiss();
        }
    }
}

/// <summary>A one-field form for the text itself.</summary>
internal sealed partial class EditTextPage : ContentPage
{
    private readonly EditForm _form = new();

    public EditTextPage()
    {
        Name = "Edit";
        Title = "Edit Dock text";
        Icon = new IconInfo("");
    }

    public override IContent[] GetContent()
    {
        _form.Refresh();
        return [_form];
    }

    private sealed partial class EditForm : FormContent
    {
        private const string Template = """
            {
              "type": "AdaptiveCard",
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "version": "1.5",
              "body": [
                {
                  "type": "Input.Text",
                  "id": "text",
                  "label": "Text to show on the Dock",
                  "placeholder": "Anything you like. Leave empty to show a pencil.",
                  "maxLength": 80,
                  "value": "${text}"
                }
              ],
              "actions": [
                { "type": "Action.Submit", "title": "Save" }
              ]
            }
            """;

        public EditForm()
        {
            TemplateJson = Template;
            Refresh();
        }

        public void Refresh() => DataJson = new JsonObject { ["text"] = DockTextStore.Text }.ToJsonString();

        public override ICommandResult SubmitForm(string inputs, string data)
        {
            try
            {
                DockTextStore.SetText(JsonNode.Parse(string.IsNullOrEmpty(inputs) ? "{}" : inputs)?["text"]?.GetValue<string>() ?? string.Empty);
                return CommandResult.Dismiss();
            }
            catch
            {
                return CommandResult.ShowToast("Couldn't save the Dock text.");
            }
        }
    }
}

/// <summary>
/// The Dock button showing the text. Clicking it opens the editor. Asks the Dock for enough
/// label width to show the whole text instead of the default, which cuts off around 120 px.
/// </summary>
internal sealed partial class DockTextItem : ListItem, IExtendedAttributesProvider
{
    // Dock label-width hint understood by Command Palette 0.102+ (WellKnownExtensionAttributes.DockMaxLabelWidth,
    // which isn't in SDK 0.12 yet). The text is capped at 80 characters, so this always fits it.
    private const string DockMaxLabelWidth = "Microsoft.CommandPalette.Dock.MaxLabelWidth";

    private readonly Dictionary<string, object> _attributes = new()
    {
        [DockMaxLabelWidth] = $"{DockTextStore.MaxLength}ch",
    };

    public DockTextItem()
        : base(new DockTextPage())
    {
        Update();
        DockTextStore.Changed += Update;
    }

    public void Detach() => DockTextStore.Changed -= Update;

    IDictionary<string, object> IExtendedAttributesProvider.GetProperties() => _attributes;

    private void Update()
    {
        var text = DockTextStore.Text;
        if (string.IsNullOrEmpty(text))
        {
            Title = string.Empty;
            Subtitle = "Click to add Dock text";
            Icon = new IconInfo("");
            return;
        }

        Title = text;
        Subtitle = "Click to edit";

        // Without an icon of its own, the item would show the editor's pencil.
        Icon = new IconInfo(DockTextStore.Glyph);
    }
}
