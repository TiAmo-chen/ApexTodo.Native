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
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ApexTodo", "todo.db");

        _db = new DatabaseContext(dbPath);
        _todoRepo = new TodoRepository(_db);
        _settingsService = new SettingsService(_db);
        _settings = _settingsService.Load();

        // Ensure settings points to our db
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

    [RelayCommand]
    private void AddTask()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _todoRepo.Add(text);
        InputText = string.Empty;
        RefreshTasks();
        ShowToastMessage("任务已添加");
    }

    [RelayCommand]
    private void ToggleTask(TodoItem? item)
    {
        if (item == null) return;
        _todoRepo.Toggle(item.Id);
        RefreshTasks();
    }

    [RelayCommand]
    private void DeleteTask(TodoItem? item)
    {
        if (item == null) return;
        _todoRepo.Delete(item.Id);
        RefreshTasks();
        ShowToastMessage("任务已删除");
    }

    [RelayCommand]
    private void UpdateTaskText(TodoItem? item)
    {
        if (item == null) return;
        _todoRepo.UpdateText(item.Id, item.Text);
    }

    [RelayCommand]
    private void CaptureFromClipboard()
    {
        var text = Clipboard.GetText().Trim();
        if (string.IsNullOrEmpty(text)) return;

        _todoRepo.Add(text);
        RefreshTasks();
        ShowToastMessage($"已捕获: {text}");
    }

    [RelayCommand]
    private async Task RunSyncAsync()
    {
        await _syncService.SyncAsync();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.Save(Settings);
        if (Settings.WebDav.Enabled)
            _syncService.StartAutoSync(Settings.WebDav.IntervalMinutes);
        else
            _syncService.StopAutoSync();

        ShowSettings = false;
        ShowToastMessage("设置已保存");
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    public void ReorderTasks(List<string> orderedIds)
    {
        _todoRepo.Reorder(orderedIds);
        RefreshTasks();
    }

    public void RefreshTasks()
    {
        var all = _todoRepo.GetAll();
        var open = all.Where(t => !t.Completed).ToList();
        var completed = all.Where(t => t.Completed).ToList();

        OpenTasks.Clear();
        CompletedTasks.Clear();
        foreach (var t in open) OpenTasks.Add(t);
        foreach (var t in completed) CompletedTasks.Add(t);
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
