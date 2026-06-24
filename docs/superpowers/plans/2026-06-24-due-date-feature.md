# Due Date Feature Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为每个任务添加截止时间，显示剩余/逾期时间，支持快速选项和自定义日期选择。

**Architecture:** 数据层新增 `DueAt` 字段 + SQLite migration；ViewModel 层添加截止时间命令和定时刷新；UI 层在任务行末尾显示倒计时，添加时提供快速 Chip 选项，修改时弹出 Flyout。

**Tech Stack:** C# 9, .NET 9, WPF UI 3.x, Dapper, SQLite, CommunityToolkit.Mvvm

## Global Constraints

- 所有时间存储为 ISO 8601 格式 TEXT
- 无截止时间的任务 DueAt 为 null
- 每分钟刷新一次时间显示（DispatcherTimer）
- 已完成任务显示固定日期，未完成任务显示相对时间

---

### Task 1: 数据层 — Model + Migration + Repository

**Files:**
- Modify: `src/ApexTodo.Core/Models/TodoItem.cs`
- Modify: `src/ApexTodo.Core/Data/DatabaseContext.cs`
- Modify: `src/ApexTodo.Core/Services/TodoRepository.cs`

**Interfaces:**
- Produces: `TodoItem.DueAt` (DateTime?), `TodoRepository.Add(string text, DateTime? dueAt)`, `TodoRepository.UpdateDueAt(string id, DateTime? dueAt)`

- [ ] **Step 1: TodoItem 添加 DueAt 属性**

修改 `src/ApexTodo.Core/Models/TodoItem.cs`，在 `CompletedAt` 后添加：

```csharp
public DateTime? DueAt { get; set; }
```

- [ ] **Step 2: DatabaseContext 添加 migration**

修改 `src/ApexTodo.Core/Data/DatabaseContext.cs` 的 `Initialize()` 方法，在 `cmd.ExecuteNonQuery()` 之前添加列检测和迁移：

```csharp
private void Initialize()
{
    using var connection = CreateConnection();
    connection.Open();
    using var cmd = connection.CreateCommand();
    cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS todos (
            id TEXT PRIMARY KEY,
            text TEXT NOT NULL,
            completed INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL,
            completed_at TEXT,
            sort_order INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """;
    cmd.ExecuteNonQuery();

    // Migration: add due_at column if not exists
    using var checkCmd = connection.CreateCommand();
    checkCmd.CommandText = "PRAGMA table_info(todos)";
    using var reader = checkCmd.ExecuteReader();
    bool hasDueAt = false;
    while (reader.Read())
    {
        if (reader.GetString(1) == "due_at")
        {
            hasDueAt = true;
            break;
        }
    }
    reader.Close();

    if (!hasDueAt)
    {
        using var alterCmd = connection.CreateCommand();
        alterCmd.CommandText = "ALTER TABLE todos ADD COLUMN due_at TEXT";
        alterCmd.ExecuteNonQuery();
    }
}
```

- [ ] **Step 3: TodoRepository 更新查询和方法**

修改 `src/ApexTodo.Core/Services/TodoRepository.cs`：

3a. `GetAll()` 查询添加 `due_at AS DueAt`：

```csharp
public List<TodoItem> GetAll()
{
    using var conn = _db.CreateConnection();
    conn.Open();
    return conn.Query("""
        SELECT id AS Id, text AS Text, completed AS Completed,
               created_at AS CreatedAt, completed_at AS CompletedAt,
               due_at AS DueAt, sort_order AS SortOrder
        FROM todos
        ORDER BY completed ASC, sort_order ASC, created_at DESC
        """).Select(r => new TodoItem
    {
        Id = (string)r.Id,
        Text = (string)r.Text,
        Completed = (long)r.Completed == 1,
        CreatedAt = DateTime.Parse((string)r.CreatedAt),
        CompletedAt = r.CompletedAt != null ? DateTime.Parse((string)r.CompletedAt) : null,
        DueAt = r.DueAt != null ? DateTime.Parse((string)r.DueAt) : null,
        SortOrder = (int)(long)r.SortOrder
    }).ToList();
}
```

3b. `Add()` 方法添加 `dueAt` 参数：

```csharp
public TodoItem Add(string text, DateTime? dueAt = null)
{
    var item = new TodoItem
    {
        Id = Guid.NewGuid().ToString(),
        Text = text,
        CreatedAt = DateTime.Now,
        DueAt = dueAt,
        SortOrder = GetNextSortOrder()
    };

    using var conn = _db.CreateConnection();
    conn.Open();
    conn.Execute("""
        INSERT INTO todos (id, text, completed, created_at, due_at, sort_order)
        VALUES (@Id, @Text, 0, @CreatedAt, @DueAt, @SortOrder)
        """, item);

    return item;
}
```

3c. 添加 `UpdateDueAt` 方法：

```csharp
public void UpdateDueAt(string id, DateTime? dueAt)
{
    using var conn = _db.CreateConnection();
    conn.Open();
    conn.Execute("UPDATE todos SET due_at = @DueAt WHERE id = @Id",
        new { Id = id, DueAt = dueAt?.ToString("o") });
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build src/ApexTodo.Core`
Expected: 成功，0 错误

- [ ] **Step 5: 提交**

```bash
git add src/ApexTodo.Core/Models/TodoItem.cs src/ApexTodo.Core/Data/DatabaseContext.cs src/ApexTodo.Core/Services/TodoRepository.cs
git commit -m "feat(core): 添加截止时间数据层支持"
```

---

### Task 2: 时间显示转换器

**Files:**
- Modify: `src/ApexTodo.Windows/Converters/Converters.cs`

**Interfaces:**
- Produces: `DueTimeToTextConverter` (IValueConverter), `DueTimeToColorConverter` (IValueConverter)

- [ ] **Step 1: 添加 DueTimeToTextConverter**

在 `src/ApexTodo.Windows/Converters/Converters.cs` 末尾添加：

```csharp
public class DueTimeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dueAt)
            return string.Empty;

        var now = DateTime.Now;
        var isCompleted = parameter is bool b && b;

        if (isCompleted)
            return $"截止 {dueAt:MM-dd}";

        var span = dueAt - now;

        if (span.TotalSeconds <= 0)
        {
            // Overdue
            span = -span;
            if (span.TotalDays >= 1)
                return $"逾期 {(int)span.TotalDays}天{span.Hours}小时";
            if (span.TotalHours >= 1)
                return $"逾期 {(int)span.TotalHours}小时{span.Minutes}分钟";
            return $"逾期 {(int)span.TotalMinutes}分钟";
        }
        else
        {
            // Remaining
            if (span.TotalDays >= 1)
                return $"还剩 {(int)span.TotalDays}天{span.Hours}小时";
            if (span.TotalHours >= 1)
                return $"还剩 {(int)span.TotalHours}小时{span.Minutes}分钟";
            return $"还剩 {(int)span.TotalMinutes}分钟";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 2: 添加 DueTimeToColorConverter**

在同一文件末尾添加：

```csharp
public class DueTimeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dueAt)
            return Brushes.Transparent;

        var isCompleted = parameter is bool b && b;
        if (isCompleted)
            return new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)); // gray

        var span = dueAt - DateTime.Now;

        if (span.TotalSeconds <= 0)
            return new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)); // red - overdue
        if (span.TotalMinutes < 60)
            return new SolidColorBrush(Color.FromRgb(0xFA, 0xB3, 0x87)); // orange - < 1h
        if (span.TotalHours < 24)
            return new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF)); // yellow - < 1d

        return new SolidColorBrush(Color.FromRgb(0xC0, 0xCC, 0xD6)); // default
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build src/ApexTodo.Windows`
Expected: 成功，0 错误

- [ ] **Step 4: 提交**

```bash
git add src/ApexTodo.Windows/Converters/Converters.cs
git commit -m "feat(ui): 添加截止时间显示转换器"
```

---

### Task 3: ViewModel — 截止时间命令和定时刷新

**Files:**
- Modify: `src/ApexTodo.Windows/ViewModels/MainViewModel.cs`

**Interfaces:**
- Produces: `SelectedDueOption` (string), `SelectDueOptionCommand`, `UpdateDueAtCommand`, `ClearDueAtCommand`, `ShowDueFlyoutCommand`

- [ ] **Step 1: 添加截止时间相关属性**

在 `MainViewModel.cs` 的 `[ObservableProperty]` 区域添加：

```csharp
[ObservableProperty]
private string _selectedDueOption = "1天";

[ObservableProperty]
private TodoItem? _flyoutTarget; // 当前弹出 Flyout 的任务

[ObservableProperty]
private bool _showDueFlyout;
```

添加快速选项常量：

```csharp
public static readonly Dictionary<string, TimeSpan> DueOptions = new()
{
    ["1小时"] = TimeSpan.FromHours(1),
    ["1天"] = TimeSpan.FromDays(1),
    ["3天"] = TimeSpan.FromDays(3),
    ["5天"] = TimeSpan.FromDays(5),
    ["1个月"] = TimeSpan.FromDays(30),
};
```

- [ ] **Step 2: 修改 AddTask 命令支持截止时间**

```csharp
[RelayCommand]
private void AddTask()
{
    try
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var dueAt = DateTime.Now + DueOptions[SelectedDueOption];
        _todoRepo.Add(text, dueAt);
        InputText = string.Empty;
        RefreshTasks();
        ShowToastMessage("任务已添加");
    }
    catch (Exception ex)
    {
        ShowToastMessage($"添加失败: {ex.Message}");
    }
}
```

- [ ] **Step 3: 添加选择和更新截止时间命令**

```csharp
[RelayCommand]
private void SelectDueOption(string option)
{
    SelectedDueOption = option;
}

[RelayCommand]
private void UpdateDueAt(TodoItem? item)
{
    if (item == null || FlyoutTarget == null) return;
    try
    {
        var dueAt = DateTime.Now + DueOptions[SelectedDueOption];
        _todoRepo.UpdateDueAt(item.Id, dueAt);
        ShowDueFlyout = false;
        FlyoutTarget = null;
        RefreshTasks();
    }
    catch (Exception ex)
    {
        ShowToastMessage($"更新失败: {ex.Message}");
    }
}

[RelayCommand]
private void ClearDueAt(TodoItem? item)
{
    if (item == null) return;
    try
    {
        _todoRepo.UpdateDueAt(item.Id, null);
        ShowDueFlyout = false;
        FlyoutTarget = null;
        RefreshTasks();
    }
    catch (Exception ex)
    {
        ShowToastMessage($"清除失败: {ex.Message}");
    }
}

[RelayCommand]
private void ShowDueFlyout(TodoItem? item)
{
    FlyoutTarget = item;
    ShowDueFlyout = true;
}
```

- [ ] **Step 4: 添加定时刷新**

在构造函数末尾（`RefreshTasks()` 之后）添加：

```csharp
// 每分钟刷新截止时间显示
var refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
refreshTimer.Tick += (_, _) => OnPropertyChanged(nameof(OpenTasks));
refreshTimer.Start();
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build`
Expected: 成功，0 错误

- [ ] **Step 6: 提交**

```bash
git add src/ApexTodo.Windows/ViewModels/MainViewModel.cs
git commit -m "feat(vm): 添加截止时间命令和定时刷新"
```

---

### Task 4: UI — 任务行截止时间显示 + 添加时 Chip + 修改时 Flyout

**Files:**
- Modify: `src/ApexTodo.Windows/Views/MainWindow.xaml`
- Modify: `src/ApexTodo.Windows/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `TodoItem.DueAt`, `MainViewModel.SelectedDueOption`, `MainViewModel.DueOptions`, `MainViewModel.ShowDueFlyout`, `MainViewModel.FlyoutTarget`
- Consumes: `DueTimeToTextConverter`, `DueTimeToColorConverter`

- [ ] **Step 1: MainWindow.xaml 添加转换器资源**

在 `Window.Resources` 中添加：

```xml
<converters:DueTimeToTextConverter x:Key="DueTimeToText"/>
<converters:DueTimeToColorConverter x:Key="DueTimeToColor"/>
```

- [ ] **Step 2: 任务输入区域添加快速选项 Chip**

在输入 Grid 后面（`<!-- Task list -->` 之前）添加：

```xml
<!-- Due date quick options -->
<StackPanel Grid.Row="1.5" Orientation="Horizontal" Margin="12,4,12,0">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Style.Triggers>
                <DataTrigger Binding="{Binding ShowSettings}" Value="True">
                    <Setter Property="Visibility" Value="Collapsed"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>
    <TextBlock Text="截止:" VerticalAlignment="Center" FontSize="12"
               Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}" Margin="0,0,8,0"/>
    <ItemsControl ItemsSource="{Binding DueOptions}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <StackPanel Orientation="Horizontal"/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <ui:Button Content="{Binding Key}" Margin="0,0,4,0" Padding="8,2"
                           FontSize="11" Click="DueChip_Click"
                           Tag="{Binding Key}"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</StackPanel>
```

注意：需要把 `Grid.Row="1"` 改为 `Grid.Row="1"` 并调整 RowDefinitions 增加一行。实际做法是在主 Grid 的 RowDefinitions 中将行数从 4 改为 5，把输入区改为 Row="1"，Chip 区为 Row="2"，任务列表为 Row="3"，底栏为 Row="4"。

- [ ] **Step 3: 未完成任务行添加截止时间显示**

在未完成任务的 DataTemplate 中，在删除按钮（Column="2"）前插入截止时间列：

```xml
<DataTemplate>
    <Grid Margin="0,2">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <CheckBox Grid.Column="0" IsChecked="{Binding Completed}"
                  Command="{Binding DataContext.ToggleTaskCommand,
                      RelativeSource={RelativeSource AncestorType=ListView}}"
                  CommandParameter="{Binding}"
                  VerticalAlignment="Center" Margin="0,0,8,0"/>
        <TextBox Grid.Column="1" Text="{Binding Text, UpdateSourceTrigger=PropertyChanged}"
                 Background="Transparent" BorderThickness="0"
                 VerticalContentAlignment="Center"
                 LostFocus="TaskText_LostFocus"
                 Tag="{Binding Id}"/>
        <TextBlock Grid.Column="2" VerticalAlignment="Center" Margin="4,0"
                   Cursor="Hand" MouseLeftButtonDown="DueText_Click"
                   Tag="{Binding}"
                   Text="{Binding DueAt, Converter={StaticResource DueTimeToText}}"
                   Foreground="{Binding DueAt, Converter={StaticResource DueTimeToColor}}"
                   Visibility="{Binding DueAt, Converter={StaticResource NullToVis}}"/>
        <ui:Button Grid.Column="3" Appearance="Transparent"
                   Icon="{ui:SymbolIcon Dismiss24}" Width="28" Height="28"
                   Command="{Binding DataContext.DeleteTaskCommand,
                       RelativeSource={RelativeSource AncestorType=ListView}}"
                   CommandParameter="{Binding}"/>
    </Grid>
</DataTemplate>
```

- [ ] **Step 4: 已完成任务行添加截止时间显示**

在已完成任务的 DataTemplate 中，同样在删除按钮前添加（灰色固定日期）：

```xml
<TextBlock Grid.Column="2" VerticalAlignment="Center" Margin="4,0"
           Text="{Binding DueAt, Converter={StaticResource DueTimeToText},
               ConverterParameter={x:Static sys:Boolean.True}}"
           Foreground="{Binding DueAt, Converter={StaticResource DueTimeToColor},
               ConverterParameter={x:Static sys:Boolean.True}}"
           Visibility="{Binding DueAt, Converter={StaticResource NullToVis}}"/>
```

需要在 XAML 头部添加 `xmlns:sys="clr-namespace:System;assembly=mscorlib"`。

- [ ] **Step 5: 添加截止时间修改 Flyout**

在 Toast 之前添加：

```xml
<!-- Due date flyout -->
<ui:Card Margin="60,120,60,120" Padding="0"
         Visibility="{Binding ShowDueFlyout, Converter={StaticResource BoolToVis}}">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="修改截止时间" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,12"/>
        <WrapPanel Grid.Row="1" Margin="0,0,0,12">
            <ui:Button Content="1小时" Margin="0,0,4,4" Padding="8,4" Click="FlyoutDueChip_Click" Tag="1小时"/>
            <ui:Button Content="1天" Margin="0,0,4,4" Padding="8,4" Click="FlyoutDueChip_Click" Tag="1天"/>
            <ui:Button Content="3天" Margin="0,0,4,4" Padding="8,4" Click="FlyoutDueChip_Click" Tag="3天"/>
            <ui:Button Content="5天" Margin="0,0,4,4" Padding="8,4" Click="FlyoutDueChip_Click" Tag="5天"/>
            <ui:Button Content="1个月" Margin="0,0,4,4" Padding="8,4" Click="FlyoutDueChip_Click" Tag="1个月"/>
        </WrapPanel>
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right">
            <ui:Button Content="清除" Appearance="Caution" Click="ClearDue_Click" Margin="0,0,8,0"/>
            <ui:Button Content="关闭" Click="CloseDueFlyout_Click"/>
        </StackPanel>
    </Grid>
</ui:Card>
```

- [ ] **Step 6: MainWindow.xaml.cs 添加事件处理**

```csharp
private void DueChip_Click(object sender, RoutedEventArgs e)
{
    if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string option)
        _vm.SelectDueOptionCommand.Execute(option);
}

private void DueText_Click(object sender, RoutedEventArgs e)
{
    if (sender is TextBlock tb && tb.Tag is TodoItem item)
        _vm.ShowDueFlyoutCommand.Execute(item);
}

private void FlyoutDueChip_Click(object sender, RoutedEventArgs e)
{
    if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string option)
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
```

- [ ] **Step 7: 编译验证**

Run: `dotnet build`
Expected: 成功，0 错误

- [ ] **Step 8: 运行验证**

Run: `dotnet run --project src/ApexTodo.Windows`
Expected:
- 添加任务时显示 Chip 选项，默认选中"1天"
- 任务行末尾显示"还剩 X天X小时"
- 点击截止时间文本弹出 Flyout
- Flyout 中可快速修改或清除截止时间

- [ ] **Step 9: 提交**

```bash
git add src/ApexTodo.Windows/Views/MainWindow.xaml src/ApexTodo.Windows/Views/MainWindow.xaml.cs
git commit -m "feat(ui): 截止时间显示、添加Chip、修改Flyout"
```

---

### Task 5: 合并到 main 分支

- [ ] **Step 1: 切换到 main 并合并**

```bash
git checkout main
git merge feature/due-date
git push
```
