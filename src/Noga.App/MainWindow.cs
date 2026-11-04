using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Noga.Checking;

namespace Noga.App;

/// <summary>
/// The app's one window (docs/decisions/2026-11-01-desktop-app.md): the profile, a place to drop files or folders on
/// (or pick them), and the checks with their verdicts, each report opened in the browser.
/// </summary>
sealed class MainWindow : Window
{
    readonly AppModel _model;
    readonly StackPanel _list = new() { Spacing = 6 };
    readonly TextBlock _hint = new()
    {
        Text = "Drop video files or folders here",
        FontSize = 16,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    // The second line, shown until the first check, when the drop zone shrinks to its first line.
    readonly TextBlock _more = new()
    {
        Text = "Each is checked where it is; its reports are written beside it.",
        Opacity = 0.7,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    readonly CancellationTokenSource _closing = new();

    // Each check's row as last drawn, with what it was drawn from.
    readonly Dictionary<int, (Control Control, TextBlock Time, Job Job, bool Dark, bool Narrow)> _rows = [];

    public MainWindow(AppModel model)
    {
        _model = model;
        Title = $"{Product.Name} {Product.Version}";
        Icon = AppIcon();
        Width = 760;
        Height = 560;
        MinWidth = 420;
        MinHeight = 320;

        // The shipped profiles, then the delivery templates beside the program (docs/decisions/2026-11-03-delivery-format.md).
        List<string> names = [.. Profiles.All.Select(p => p.Name), .. Profiles.Templates];
        var profiles = new ComboBox
        {
            ItemsSource = names,
            SelectedIndex = 0,
            MinWidth = 160,
        };
        profiles.SelectionChanged += (_, _) => _model.Profile = Profiles.Get(names[Math.Max(0, profiles.SelectedIndex)]);
        AutomationName(profiles, "Profile");

        var pick = new Button { Content = "Add files…" };
        pick.Click += async (_, _) => await PickAsync();
        AutomationName(pick, "Add files");

        var top = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 12) };
        var profileRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        profileRow.Children.Add(new TextBlock { Text = "Profile", VerticalAlignment = VerticalAlignment.Center });
        profileRow.Children.Add(profiles);
        DockPanel.SetDock(profileRow, Dock.Left);
        DockPanel.SetDock(pick, Dock.Right);
        top.Children.Add(profileRow);
        top.Children.Add(pick);

        var drop = new Border
        {
            Name = "Drop",
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Gray,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            Child = new StackPanel { Spacing = 4, Children = { _hint, _more } },
        };
        DropZone = drop;

        // Files can be dropped anywhere in the window (on the list too); the drop zone lights up while they are over it.
        var layout = new DockPanel { Margin = new Thickness(16), Background = Brushes.Transparent };
        DragDrop.SetAllowDrop(layout, true);
        layout.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            bool files = e.DataTransfer.Contains(DataFormat.File);
            e.DragEffects = files ? DragDropEffects.Copy : DragDropEffects.None;
            Highlight(files);
        });
        layout.AddHandler(DragDrop.DragLeaveEvent, (_, _) => Highlight(false));
        layout.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            Highlight(false);
            Dropped(e.DataTransfer);
        });
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(drop, Dock.Top);
        layout.Children.Add(top);
        layout.Children.Add(drop);
        layout.Children.Add(new ScrollViewer { Content = _list });
        Content = layout;

        Render();
        ActualThemeVariantChanged += (_, _) => Render();
        SizeChanged += (_, e) =>
        {
            if (e.PreviousSize.Width < NarrowWidth != e.NewSize.Width < NarrowWidth)
                Render();
        };

        // A measuring check's time goes up each second.
        var ticking = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        ticking.Tick += (_, _) =>
        {
            if (_model.Jobs.Any(j => j.State == JobState.Measuring))
                Render();
        };
        ticking.Start();
        Closed += (_, _) => ticking.Stop();
        _ = FollowAsync();
    }

    /// <summary>
    /// The app's icon (Assets/icon-256.png, embedded; tools/make-icons.sh draws it from Assets/icon.svg) for the title
    /// bar, the taskbar and the dock.
    /// </summary>
    internal static WindowIcon AppIcon()
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("Noga.App.icon.png")
            ?? throw new InvalidOperationException("noga-app has no embedded icon (Noga.App.icon.png)");
        return new WindowIcon(stream);
    }

    /// <summary>Where files are dropped (tests drop on it).</summary>
    internal Border DropZone { get; }

    /// <summary>The checks' rows, newest first (tests read them).</summary>
    internal IReadOnlyList<Control> Rows => [.. _list.Children];

    protected override void OnClosed(EventArgs e)
    {
        _closing.Cancel();
        _closing.Dispose();
        base.OnClosed(e);
    }

    /// <summary>The files and folders dropped: their local paths queued (others, such as a browser's, can't be checked).</summary>
    internal void Dropped(IDataTransfer data)
    {
        var paths = (data.TryGetFiles() ?? []).Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        int added = _model.Add(paths);
        _hint.Text = added == 0 ? "No video files there: drop files or folders" : "Drop video files or folders here";
    }

    async Task PickAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Files to check", AllowMultiple = true });
        _model.Add(files.Select(f => f.TryGetLocalPath()).OfType<string>());
    }

    // The checks, redrawn at each change until the window closes. The next change's signal is taken before each redraw,
    // so a change during the redraw wakes the loop again rather than waiting for one after it (which, after the last
    // check, would never come and leave its verdict unshown).
    async Task FollowAsync()
    {
        while (!_closing.IsCancellationRequested)
        {
            var changed = _model.Changed;
            await Dispatcher.UIThread.InvokeAsync(Render);
            await changed.WaitAsync(_closing.Token).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    // Below this width, a row's badge and buttons go on a line of their own.
    const double NarrowWidth = 600;

    bool Narrow => Bounds.Width is > 0 and < NarrowWidth;

    // The drop zone while files are dragged over the window: the accent border on the info tint.
    internal void Highlight(bool on)
    {
        DropZone.BorderBrush = on ? Brush.Parse("#2f6fa8") : Brushes.Gray;
        DropZone.Background = on ? Brush.Parse(StatusStyle.Colours("info", ActualThemeVariant == ThemeVariant.Dark).Background) : null;
    }

    void Render()
    {
        // Once there are checks, the drop zone keeps to one line and leaves the room to them.
        bool any = _model.Jobs.Count > 0;
        _more.IsVisible = !any;
        DropZone.Padding = new Thickness(any ? 8 : 16);

        // A row is built again only when its check changes (or the theme or the width does); otherwise it stays, and
        // only its time is written, so the focus and a click on Report aren't lost to a row rebuilt each second.
        bool dark = ActualThemeVariant == ThemeVariant.Dark, narrow = Narrow;
        var wanted = new List<Control>();
        foreach (var job in _model.Jobs)
        {
            // Its progress shows only in the time line, written below; a new percent doesn't rebuild the row.
            if (!_rows.TryGetValue(job.Id, out var row) || row.Job with { Progress = null } != job with { Progress = null } || row.Dark != dark || row.Narrow != narrow)
                _rows[job.Id] = row = Row(job, dark, narrow);
            row.Time.Text = Where(job);
            wanted.Add(row.Control);
        }

        // The list changed only where it must: a new check goes in at the top, a changed one in its place.
        for (int i = 0; i < wanted.Count; i++)
        {
            if (i < _list.Children.Count && _list.Children[i] == wanted[i])
                continue;
            _list.Children.Remove(wanted[i]);
            _list.Children.Insert(i, wanted[i]);
        }

        while (_list.Children.Count > wanted.Count)
            _list.Children.RemoveAt(_list.Children.Count - 1);
    }

    // How long it has been measuring or took, then the folder (the name is above it): the time first, so a long
    // folder trimmed at the end doesn't hide it.
    static string Where(Job job)
    {
        string time = job.Time(DateTimeOffset.Now);
        return time.Length == 0 ? job.Folder : $"{time} · {job.Folder}";
    }

    (Control Control, TextBlock Time, Job Job, bool Dark, bool Narrow) Row(Job job, bool dark, bool narrow)
    {
        var colours = StatusStyle.Colours(StatusStyle.Tone(job.Status), dark);
        var badge = new Border
        {
            Background = Brush.Parse(colours.Background),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = StatusStyle.Label(job.Status),
                Foreground = Brush.Parse(colours.Text),
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        };
        var text = new StackPanel { Spacing = 2, Margin = new Thickness(10, 0) };
        text.Children.Add(new TextBlock { Text = Path.GetFileName(job.File), FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var where = new TextBlock { Opacity = 0.65, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        text.Children.Add(where);
        if (job.Reason is { } reason)
            text.Children.Add(new TextBlock { Text = reason, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (job.ShortError is { } error)
            text.Children.Add(new TextBlock { Text = error, Foreground = Brush.Parse(StatusStyle.Colours("failure", dark).Text), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        ToolTip.SetTip(text, job.File);

        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (job.Html is { } html)
            links.Children.Add(Open("Report", html, main: true));
        if (job.Json is { } json)
            links.Children.Add(Open("JSON", json, main: false));

        // In a narrow window the badge and the buttons share a line and the text takes the width under them, rather
        // than being squeezed between them a few words wide.
        var row = new DockPanel { Tag = job };
        var head = narrow ? new DockPanel { Margin = new Thickness(0, 0, 0, 4) } : row;
        DockPanel.SetDock(badge, Dock.Left);
        DockPanel.SetDock(links, Dock.Right);
        head.Children.Add(badge);
        head.Children.Add(links);
        if (narrow)
        {
            DockPanel.SetDock(head, Dock.Top);
            row.Children.Add(head);
            text.Margin = default;
        }

        row.Children.Add(text);
        return (new Border { Padding = new Thickness(6), Child = row }, where, job, dark, narrow);
    }

    // The report is the main action (the accent button); the JSON, for pipelines, a quiet one.
    Button Open(string label, string path, bool main)
    {
        var button = new Button { Content = label };
        if (main)
            button.Classes.Add("accent");
        else
            button.FontSize = 12;

        button.Click += async (_, _) => await Launcher.LaunchFileInfoAsync(new FileInfo(path));
        return button;
    }

    static void AutomationName(Control control, string name) => Avalonia.Automation.AutomationProperties.SetName(control, name);
}
