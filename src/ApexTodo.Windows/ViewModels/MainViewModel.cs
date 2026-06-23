using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ApexTodo.Core.Data;
using ApexTodo.Core.Models;
using ApexTodo.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApexTodo.Windows.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TodoRepository _todoRepo;
    private readonly SettingsService _settingsService;
    private readonly SyncService _syncService;
    private readonly DatabaseContext _db;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _showCompleted = true;

    [ObservableProperty]
    private bool _showSettings;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private bool _showToast;

    [ObservableProperty]
    private AppSettings _settings;

    [ObservableProperty]
    private string _syncMessage = string.Empty;

    public ObservableCollection<TodoItem> OpenTasks { get; } = new();
    public ObservableCollection<TodoItem> CompletedTasks { get; } = new();

    public MainViewModel()
    {
        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ApexTodo", "todo.db");

            _db = new DatabaseContext(dbPath);
            _todoRepo = new TodoRepository(_db);
            _settingsService = new SettingsService(_db);
            _settings = _settingsService.Load();

            Settings.TodoDbPath = dbPath;
            _settingsService.Save(Settings);

            _syncService = new SyncService(dbPath, () => Settings);
            _syncService.OnSyncCompleted += result =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SyncMessage = result.Message;
                    if (result.Success) RefreshTasks();
                    ShowToastMessage(result.Message);
                });
            };

            RefreshTasks();

            if (Settings.WebDav.Enabled)
                _syncService.StartAutoSync(Settings.WebDav.IntervalMinutes);
        }
        catch (Exception ex)
        {
            _settings = new AppSettings();
            _db = new DatabaseContext(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ApexTodo", "todo.db"));
            _todoRepo = new TodoRepository(_db);
            _settingsService = new SettingsService(_db);
            _syncService = new SyncService("", () => Settings);
            MessageBox.Show($"初始化出错: {ex.Message}", "ApexTodo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void AddTask()
    {
        try
        {
            var text = InputText.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _todoRepo.Add(text);
            InputText = string.Empty;
            RefreshTasks();
            ShowToastMessage("任务已添加");
        }
        catch (Exception ex)
        {
            ShowToastMessage($"添加失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleTask(TodoItem? item)
    {
        if (item == null) return;
        try
        {
            _todoRepo.Toggle(item.Id);
            RefreshTasks();
        }
        catch (Exception ex)
        {
            ShowToastMessage($"操作失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DeleteTask(TodoItem? item)
    {
        if (item == null) return;
        try
        {
            _todoRepo.Delete(item.Id);
            RefreshTasks();
            ShowToastMessage("任务已删除");
        }
        catch (Exception ex)
        {
            ShowToastMessage($"删除失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void UpdateTaskText(TodoItem? item)
    {
        if (item == null) return;
        try
        {
            _todoRepo.UpdateText(item.Id, item.Text);
        }
        catch (Exception ex)
        {
            ShowToastMessage($"更新失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CaptureFromClipboard()
    {
        try
        {
            var text = Clipboard.GetText().Trim();
            if (string.IsNullOrEmpty(text)) return;

            _todoRepo.Add(text);
            RefreshTasks();
            ShowToastMessage($"已捕获: {text}");
        }
        catch (Exception ex)
        {
            ShowToastMessage($"捕获失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RunSyncAsync()
    {
        try
        {
            await _syncService.SyncAsync();
        }
        catch (Exception ex)
        {
            ShowToastMessage($"同步失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            _settingsService.Save(Settings);
            if (Settings.WebDav.Enabled)
                _syncService.StartAutoSync(Settings.WebDav.IntervalMinutes);
            else
                _syncService.StopAutoSync();

            ShowSettings = false;
            ShowToastMessage("设置已保存");
        }
        catch (Exception ex)
        {
            ShowToastMessage($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    public void ReorderTasks(List<string> orderedIds)
    {
        try
        {
            _todoRepo.Reorder(orderedIds);
            RefreshTasks();
        }
        catch (Exception ex)
        {
            ShowToastMessage($"排序失败: {ex.Message}");
        }
    }

    public void RefreshTasks()
    {
        try
        {
            var all = _todoRepo.GetAll();
            var open = all.Where(t => !t.Completed).ToList();
            var completed = all.Where(t => t.Completed).ToList();

            OpenTasks.Clear();
            CompletedTasks.Clear();
            foreach (var t in open) OpenTasks.Add(t);
            foreach (var t in completed) CompletedTasks.Add(t);
        }
        catch (Exception ex)
        {
            ShowToastMessage($"刷新失败: {ex.Message}");
        }
    }

    private void ShowToastMessage(string message)
    {
        ToastMessage = message;
        ShowToast = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            ShowToast = false;
            timer.Stop();
        };
        timer.Start();
    }
}
