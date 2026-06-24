using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApexTodo.Core.Models;
using ApexTodo.Windows.Services;
using ApexTodo.Windows.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ApexTodo.Windows.Views;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _vm;
    private readonly HotkeyService _hotkey;
    private bool _isPassthrough;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _hotkey = new HotkeyService();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        // Restore window state
        var s = _vm.Settings;
        Left = s.WindowLeft;
        Top = s.WindowTop;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        Topmost = s.AlwaysOnTop;
        Opacity = s.WindowOpacity;
        ApplyTheme(s.Theme);
        UpdatePinButton();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _hotkey.Register(this);
        _hotkey.OnCaptureTriggered += async () =>
        {
            await Task.Delay(100);
            Application.Current.Dispatcher.Invoke(() =>
                _vm.CaptureFromClipboardCommand.Execute(null));
        };
        _hotkey.OnToggleMouseThrough += () =>
        {
            Application.Current.Dispatcher.Invoke(() =>
                SetPassthrough(!_isPassthrough));
        };
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hotkey.Unregister();
        _vm.Settings.WindowLeft = Left;
        _vm.Settings.WindowTop = Top;
        _vm.Settings.WindowWidth = Width;
        _vm.Settings.WindowHeight = Height;
        _vm.Settings.AlwaysOnTop = Topmost;
        _vm.Settings.WindowOpacity = Opacity;
        _vm.SaveSettingsCommand.Execute(null);
    }

    private void PassthroughButton_Click(object sender, RoutedEventArgs e)
    {
        SetPassthrough(!_isPassthrough);
    }

    private void SetPassthrough(bool enabled)
    {
        _isPassthrough = enabled;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (enabled)
        {
            // 开启穿透：置顶 + 鼠标穿透
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            Topmost = true;
            _vm.Settings.AlwaysOnTop = true;
            UpdatePinButton();
            PassthroughBtn.Appearance = ControlAppearance.Primary;
            PassthroughBtn.ToolTip = "按 Ctrl+Shift+Z 取消穿透";
        }
        else
        {
            // 取消穿透
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
            PassthroughBtn.Appearance = ControlAppearance.Transparent;
            PassthroughBtn.ToolTip = "鼠标穿透";
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _vm.Settings.AlwaysOnTop = Topmost;
        UpdatePinButton();
    }

    private void UpdatePinButton()
    {
        if (Topmost)
        {
            PinBtn.Icon = new SymbolIcon(SymbolRegular.Pin24);
            PinBtn.Appearance = ControlAppearance.Primary;
            PinBtn.ToolTip = "已置顶（点击取消）";
        }
        else
        {
            PinBtn.Icon = new SymbolIcon(SymbolRegular.PinOff24);
            PinBtn.Appearance = ControlAppearance.Transparent;
            PinBtn.ToolTip = "点击置顶";
        }
    }

    private void ApplyTheme(string theme)
    {
        switch (theme)
        {
            case "Dark":
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            case "Light":
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case "System":
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica);
                // 跟随系统：WPF UI 会自动检测系统主题
                break;
        }
        UpdateThemeButtons(theme);
        _vm.Settings.Theme = theme;
    }

    private void UpdateThemeButtons(string selected)
    {
        // 重置所有按钮样式
        ThemeDarkBtn.Appearance = ControlAppearance.Secondary;
        ThemeLightBtn.Appearance = ControlAppearance.Secondary;
        ThemeSystemBtn.Appearance = ControlAppearance.Secondary;

        // 高亮选中的按钮
        switch (selected)
        {
            case "Dark": ThemeDarkBtn.Appearance = ControlAppearance.Primary; break;
            case "Light": ThemeLightBtn.Appearance = ControlAppearance.Primary; break;
            case "System": ThemeSystemBtn.Appearance = ControlAppearance.Primary; break;
        }
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e) => ApplyTheme("Dark");
    private void ThemeLight_Click(object sender, RoutedEventArgs e) => ApplyTheme("Light");
    private void ThemeSystem_Click(object sender, RoutedEventArgs e) => ApplyTheme("System");

    private void AlwaysOnTop_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            Topmost = cb.IsChecked == true;
            _vm.Settings.AlwaysOnTop = Topmost;
            UpdatePinButton();
        }
    }

    private void OpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.AddTaskCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TaskText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb && tb.Tag is string id)
        {
            var item = _vm.OpenTasks.FirstOrDefault(t => t.Id == id);
            if (item != null)
                _vm.UpdateTaskTextCommand.Execute(item);
        }
    }

    private void ToggleCompleted_Click(object sender, RoutedEventArgs e)
        => _vm.ShowCompleted = !_vm.ShowCompleted;

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
        => _vm.ShowSettings = false;

    private void OpenTasks_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
    }

    private void OpenTasks_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TodoItem)) is TodoItem)
        {
            var orderedIds = _vm.OpenTasks.Select(t => t.Id).ToList();
            _vm.ReorderTasks(orderedIds);
        }
    }

    private void DueChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string option)
        {
            _vm.SelectDueOptionCommand.Execute(option);
            UpdateChipStyles(option);
        }
    }

    private void UpdateChipStyles(string selected)
    {
        var selectedBg = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x89, 0xB4, 0xFA));
        var normalBg = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x31, 0x32, 0x44));
        var selectedFg = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E));
        var normalFg = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xCD, 0xD6, 0xF4));
        var normalBorder = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x45, 0x47, 0x5A));

        foreach (var (chip, tag) in new[]
        {
            (Chip1h, "1小时"), (Chip1d, "1天"), (Chip3d, "3天"),
            (Chip5d, "5天"), (Chip1m, "1个月")
        })
        {
            if (tag == selected)
            {
                chip.Background = selectedBg;
                chip.Foreground = selectedFg;
                chip.BorderThickness = new Thickness(0);
            }
            else
            {
                chip.Background = normalBg;
                chip.Foreground = normalFg;
                chip.BorderBrush = normalBorder;
                chip.BorderThickness = new Thickness(1);
            }
        }
    }

    private void DueText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBlock tb && tb.Tag is TodoItem item)
            _vm.OpenDueFlyoutCommand.Execute(item);
    }

    private void FlyoutDueChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string option)
        {
            _vm.SelectedDueOption = option;
            _vm.UpdateDueAtCommand.Execute(_vm.FlyoutTarget);
        }
    }

    private void ClearDue_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearDueAtCommand.Execute(_vm.FlyoutTarget);
    }

    private void CloseDueFlyout_Click(object sender, RoutedEventArgs e)
    {
        _vm.ShowDueFlyout = false;
    }
}
