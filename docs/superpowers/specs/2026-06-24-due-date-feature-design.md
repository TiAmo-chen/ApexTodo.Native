# Due Date Feature Design

## Overview

为 ApexTodo 添加截止时间功能。每个任务可设置一个截止时间，界面显示剩余时间或逾期时间，支持快速选项和自定义日期选择。

## Data Model

### TodoItem 新增字段

```csharp
public DateTime? DueAt { get; set; }  // 可为空，表示无截止时间
```

### SQLite Migration

在 `DatabaseContext.Initialize()` 中添加列检测和迁移：

```sql
-- 检查 due_at 列是否存在，不存在则添加
ALTER TABLE todos ADD COLUMN due_at TEXT;
```

使用 `PRAGMA table_info(todos)` 检查列是否已存在，避免重复 ALTER。

### Repository 更新

- `GetAll()`: 查询包含 `due_at` 列
- `Add()`: 接受可选的 `dueAt` 参数
- `UpdateDueAt(string id, DateTime? dueAt)`: 新增方法，更新截止时间

## UI Design

### 任务行布局

```
[✓] 任务文本                    还剩 2天3小时  [✕]
[✓] 任务文本                    逾期 1天5小时  [✕]
[✓] 已完成文本 (灰色+删除线)    截止 06-25     [✕]
```

- 未完成：显示相对时间，颜色随紧急程度变化
- 已完成：显示固定日期 `截止 MM-dd`，灰色带删除线
- 无截止时间：不显示时间区域

### 时间显示规则

| 状态 | 显示文本 | 颜色 |
|------|----------|------|
| 剩余 > 1天 | `还剩 X天X小时` | 默认前景色 |
| 剩余 1小时~1天 | `还剩 X小时X分钟` | 黄色 (#F9E2AF) |
| 剩余 < 1小时 | `还剩 X分钟` | 橙色 (#FAB387) |
| 已过期 | `逾期 X天X小时` | 红色 (#F38BA8) |
| 已完成 | `截止 MM-dd` | 灰色 |

刷新机制：`DispatcherTimer` 每分钟触发一次，重新计算所有任务的时间显示。

### 添加任务时

输入框下方增加一排快速选项 Chip：

```
[输入任务...]  [添加]
  [1小时] [1天✓] [3天] [5天] [1个月]
```

- 默认选中 `1天`
- 点击切换选中，单选模式
- Chip 样式：WPF UI `ui:Button`，选中时 `Appearance="Primary"`，未选中 `Appearance="Secondary"`
- 选中值决定 `DueAt = DateTime.Now + 选中时长`

### 修改截止时间

点击任务的截止时间文本区域，弹出 `ui:Flyout`：

```
┌─────────────────────┐
│  [1小时] [1天] [3天] │
│  [5天]  [1个月]      │
│                     │
│  [自定义]    [清除]  │
└─────────────────────┘
```

- **快速选项**：从当前时间起算，直接更新 DueAt
- **自定义**：弹出 DatePicker + 时间输入框，确认后更新
- **清除**：将 DueAt 设为 null

## Implementation Plan

### Step 1: Core 层改动
1. `TodoItem.cs` 添加 `DueAt` 属性
2. `DatabaseContext.cs` 添加 migration 逻辑（检测并添加 `due_at` 列）
3. `TodoRepository.cs` 更新 SQL 查询，添加 `UpdateDueAt` 方法

### Step 2: ViewModel 层改动
1. `MainViewModel.cs` 添加截止时间相关的命令和状态
2. 添加 `SelectedDueOption` 属性（绑定 Chip 选中状态）
3. 添加 `UpdateDueAt` 命令
4. 添加 DispatcherTimer 每分钟刷新时间显示

### Step 3: UI 层改动
1. `MainWindow.xaml` 任务行模板添加截止时间显示
2. 添加添加时的 Chip 选择区域
3. 添加修改时的 Flyout 弹窗
4. 截止时间文本的点击事件绑定

### Step 4: Converter 层
1. 添加 `DueTimeToTextConverter` — 将 DueAt 转换为显示文本
2. 添加 `DueTimeToColorConverter` — 根据紧急程度返回颜色

## Out of Scope

- 重复任务/周期任务
- 截止时间提醒通知
- 按截止时间排序
