using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Blitztext.Core;
using Blitztext.Core.Models;

namespace Blitztext.App.Platform;

/// <summary>
/// The notification-area (tray) icon and its context menu. Windows equivalent of the macOS
/// <c>NSStatusItem</c> + menu. The icon colour reflects <see cref="MenuBarStatus"/>; the menu
/// lists the workflows (start / stop), opens settings, and quits.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly AppCoordinator _coordinator;
    private readonly Action _openSettings;
    private readonly NotifyIcon _notifyIcon;
    private readonly Dictionary<WorkflowType, ToolStripMenuItem> _items = new();
    private Icon? _currentIcon;

    public TrayIconController(AppCoordinator coordinator, Action openSettings)
    {
        _coordinator = coordinator;
        _openSettings = openSettings;

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Blitztext",
            Icon = BuildIcon(Color.Gray)
        };

        var menu = new ContextMenuStrip();
        foreach (var type in WorkflowTypeInfo.MainMenuCases)
        {
            var item = new ToolStripMenuItem(type.DisplayName()) { Tag = type };
            item.Click += (_, _) => ToggleWorkflow(type);
            _items[type] = item;
            menu.Items.Add(item);
        }

        var localItem = new ToolStripMenuItem(WorkflowType.LocalTranscription.DisplayName())
        {
            Tag = WorkflowType.LocalTranscription
        };
        localItem.Click += (_, _) => ToggleWorkflow(WorkflowType.LocalTranscription);
        _items[WorkflowType.LocalTranscription] = localItem;
        menu.Items.Add(localItem);

        menu.Items.Add(new ToolStripSeparator());

        var settings = new ToolStripMenuItem("Einstellungen …");
        settings.Click += (_, _) => _openSettings();
        menu.Items.Add(settings);

        var quit = new ToolStripMenuItem("Blitztext beenden");
        quit.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(quit);

        menu.Opening += (_, _) => RefreshAvailability();
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => _openSettings();
    }

    private void ToggleWorkflow(WorkflowType type)
    {
        var active = _coordinator.ActiveWorkflow;
        if (active is not null && active.Type == type && active.Phase.IsActive)
            active.Stop();
        else
            _coordinator.StartWorkflow(type, WorkflowLaunchSource.Manual);
    }

    private void RefreshAvailability()
    {
        foreach (var (type, item) in _items)
            item.Enabled = _coordinator.IsWorkflowAvailable(type);
    }

    public void Update(MenuBarStatus status)
    {
        var color = status.Kind switch
        {
            MenuBarStatusKind.Recording => Color.FromArgb(0xE5, 0x3E, 0x3E),
            MenuBarStatusKind.Processing => Color.FromArgb(0x3B, 0x82, 0xF6),
            MenuBarStatusKind.Success => Color.FromArgb(0x22, 0xC5, 0x5E),
            MenuBarStatusKind.Error => Color.FromArgb(0xF9, 0x73, 0x16),
            _ => Color.Gray
        };

        var newIcon = BuildIcon(color);
        _notifyIcon.Icon = newIcon;
        _notifyIcon.Text = status.Tooltip;

        DisposeCurrentIcon();
        _currentIcon = newIcon;
    }

    private static Icon BuildIcon(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 4, 4, 24, 24);
            using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("B", font, textBrush, new RectangleF(0, 0, 32, 32), sf);
        }

        var hicon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hicon).Clone();
        DestroyIcon(hicon);
        return icon;
    }

    private void DisposeCurrentIcon()
    {
        _currentIcon?.Dispose();
        _currentIcon = null;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        DisposeCurrentIcon();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
