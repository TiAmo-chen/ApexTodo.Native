namespace ApexTodo.Core.Models;

public class AppSettings
{
    public string TodoDbPath { get; set; } = string.Empty;
    public string GlobalShortcut { get; set; } = "Ctrl+Shift+A";
    public bool AlwaysOnTop { get; set; } = true;
    public bool DesktopPinned { get; set; }
    public bool DesktopLockPosition { get; set; }
    public bool DesktopMouseThrough { get; set; }
    public bool LaunchAtStartup { get; set; }
    public string Theme { get; set; } = "Dark";
    public double WindowOpacity { get; set; } = 1.0;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 360;
    public double WindowHeight { get; set; } = 500;
    public WebDavConfig WebDav { get; set; } = new();
}
