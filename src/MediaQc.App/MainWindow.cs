using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MediaQc.Checking;

namespace MediaQc.App;

/// <summary>
/// The app's one window (docs/decisions/2026-11-01-desktop-app.md): the profile, a place to drop files or folders on
/// (or pick them), and the checks with their verdicts, each report opened in the browser.
/// </summary>
sealed class MainWindow : Window
{
    readonly AppModel _model;
    readonly StackPanel _list = new() { Spacing = 6 };
    readonly TextBlock _hint = new() { Text = "Drop video files or folders here", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center };
    readonly CancellationTokenSource _closing = new();

    public MainWindow(AppModel model)
    {
        _model = model;
        Title = $"{Product.Name} {Product.Version}";
        Width = 760;
        Height = 560;
        MinWidth = 420;
        MinHeight = 320;

        var profiles = new ComboBox
        {
            ItemsSource = Profiles.All.Select(p => p.Name).ToList(),
            SelectedIndex = 0,
            MinWidth = 160,
        };
        profiles.SelectionChanged += (_, _) => _model.Profile = Profiles.All[Math.Max(0, profiles.SelectedIndex)];
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
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    _hint,
                    new TextBlock
                    {
                        Text = "Each is checked where it is; its reports are written beside it.",
                        Opacity = 0.7,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    },
                },
            },
        };
        DragDrop.SetAllowDrop(drop, true);
        drop.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
        drop.AddHandler(DragDrop.DropEvent, (_, e) => Dropped(e.DataTransfer));
        DropZone = drop;

        var layout = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(drop, Dock.Top);
        layout.Children.Add(top);
        layout.Children.Add(drop);
        layout.Children.Add(new ScrollViewer { Content = _list });
        Content = layout;

        Render();
        _ = FollowAsync();
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

    void Render()
    {
        _list.Children.Clear();
        foreach (var job in _model.Jobs)
            _list.Children.Add(Row(job));
    }

    Control Row(Job job)
    {
        var (label, colour) = Badge(job);
        var badge = new Border
        {
            Background = new SolidColorBrush(colour),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            MinWidth = 110,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = label, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
        };
        var text = new StackPanel { Spacing = 2, Margin = new Thickness(10, 0) };
        text.Children.Add(new TextBlock { Text = Path.GetFileName(job.File), FontWeight = FontWeight.SemiBold });
        text.Children.Add(new TextBlock { Text = job.File, Opacity = 0.65, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
        if (job.Error is { } error)
            text.Children.Add(new TextBlock { Text = error, Foreground = Brushes.Firebrick, FontSize = 12, TextWrapping = TextWrapping.Wrap });

        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (job.Html is { } html)
            links.Children.Add(Open("Report", html));
        if (job.Json is { } json)
            links.Children.Add(Open("JSON", json));

        var row = new DockPanel { Tag = job };
        DockPanel.SetDock(badge, Dock.Left);
        DockPanel.SetDock(links, Dock.Right);
        row.Children.Add(badge);
        row.Children.Add(links);
        row.Children.Add(text);
        return new Border { Padding = new Thickness(6), Child = row };
    }

    Button Open(string label, string path)
    {
        var button = new Button { Content = label };
        button.Click += async (_, _) => await Launcher.LaunchFileInfoAsync(new FileInfo(path));
        return button;
    }

    // The state, or the verdict once done, and its colour (the HTML report's).
    static (string Label, Color Colour) Badge(Job job) => job.State switch
    {
        JobState.Queued => ("Queued", Color.Parse("#6b6b6b")),
        JobState.Measuring => ("Measuring…", Color.Parse("#2f6fa8")),
        JobState.Failed => ("Couldn't read", Color.Parse("#8d1c12")),
        _ => job.Verdict switch
        {
            Severity.Failure => ("Failed", Color.Parse("#8d1c12")),
            Severity.Warning => ("Warnings", Color.Parse("#7a5200")),
            _ => ("Passed", Color.Parse("#17602a")),
        },
    };

    static void AutomationName(Control control, string name) => Avalonia.Automation.AutomationProperties.SetName(control, name);
}
