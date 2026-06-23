using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApexTodo.Core.Models;
using ApexTodo.Windows.Services;
using ApexTodo.Windows.ViewModels;

namespace ApexTodo.Windows.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly HotkeyService _hotkey;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        _hotkey = new HotkeyService();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        // Restore window position
        var s = _vm.Settings;
        Left = s.WindowLeft;
        Top = s.WindowTop;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        Topmost = s.AlwaysOnTop;
        Opacity = s.WindowOpacity;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _hotkey.Register(this);
        _hotkey.OnCaptureTriggered += async () =>
        {
            await Task.Delay(100); // Wait for clipboard
            _vm.CaptureFromClipboardCommand.Execute(null);
        };
        _hotkey.OnToggleMouseThrough += () =>
        {
            _vm.Settings.DesktopMouseThrough = !_vm.Settings.DesktopMouseThrough;
        };
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hotkey.Unregister();
        // Save window position
        _vm.Settings.WindowLeft = Left;
        _vm.Settings.WindowTop = Top;
        _vm.Settings.WindowWidth = Width;
        _vm.Settings.WindowHeight = Height;
        _vm.SaveSettingsCommand.Execute(null);
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

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _vm.Settings.AlwaysOnTop = Topmost;
    }

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
        if (sender is TextBox tb && tb.Tag is string id)
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

    // Simple drag-and-drop reorder
    private void OpenTasks_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
    }

    private void OpenTasks_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TodoItem)) is TodoItem dropped)
        {
            var orderedIds = _vm.OpenTasks.Select(t => t.Id).ToList();
            _vm.ReorderTasks(orderedIds);
        }
    }
}
