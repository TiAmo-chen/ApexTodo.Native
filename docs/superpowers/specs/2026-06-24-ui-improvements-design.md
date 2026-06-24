# UI Improvements Design

## Overview

四个独立的 UI 改进：已完成任务颜色修复、主题切换、删除按钮裁剪修复、鼠标穿透功能。

---

## 1. 已完成任务颜色修复

**问题：** 已完成任务文本颜色太暗（#6C7086），在暗色背景上看不清楚。

**方案：** 已完成任务文本颜色改为 `#9399B2`，删除线颜色同步。在浅色主题下改为 `#6C7086`。

**修改文件：**
- `MainWindow.xaml` — 已完成任务 DataTemplate 中的 TextBlock Foreground

---

## 2. 主题切换

### 数据模型

`AppSettings.cs` 新增：
```csharp
public string Theme { get; set; } = "Dark"; // "Dark", "Light", "System"
```

### 设置面板

在"窗口置顶"之前添加主题选择区域，三个按钮：
- 深色
- 浅色
- 跟随系统

### 实现方式

使用 WPF UI 的 `Wpf.Ui.Appearance.ApplicationThemeManager`：
- `ApplicationTheme.Dark` — 深色
- `ApplicationTheme.Light` — 浅色
- 监听系统主题变化实现"跟随系统"

切换主题时调用 `ApplicationThemeManager.Apply(theme)` 并保存到设置。

### 修改文件

- `AppSettings.cs` — 添加 Theme 属性
- `MainWindow.xaml` — 设置面板添加主题选择 UI
- `MainWindow.xaml.cs` — 主题切换逻辑
- `MainViewModel.cs` — 主题相关命令

---

## 3. 删除按钮裁剪修复

**问题：** 任务行最右侧的 X 按钮显示不完整。

**方案：** 将删除按钮尺寸从 `Width="28" Height="28"` 调整为 `Width="32" Height="32"`，图标从 `Dismiss24` 改为 `Dismiss16` 或保持 `Dismiss24` 但增加 Padding。

**修改文件：**
- `MainWindow.xaml` — 未完成和已完成任务的删除按钮样式

---

## 4. 鼠标穿透功能

### 交互流程

```
正常状态 → 点击穿透按钮 → 穿透模式（置顶+鼠标穿透）
穿透模式 → 点击穿透按钮 → 正常状态
```

### 穿透模式行为

- 窗口自动置顶
- 鼠标点击穿透到下层窗口
- 只有穿透按钮保持可点击
- 穿透按钮高亮显示（蓝色）

### 实现方式

Win32 P/Invoke：
- `GetWindowLong` / `SetWindowLong` 设置 `WS_EX_TRANSPARENT` + `WS_EX_LAYERED`
- 穿透模式下，穿透按钮区域需要特殊处理使其保持可点击

### 标题栏

穿透按钮放在置顶按钮左边，穿透激活时显示蓝色高亮。

### 修改文件

- `MainWindow.xaml` — 标题栏添加穿透按钮
- `MainWindow.xaml.cs` — Win32 穿透逻辑
- `MainViewModel.cs` — 穿透状态属性（可选）

---

## 实现顺序

1. 已完成任务颜色修复（最简单）
2. 删除按钮裁剪修复（简单）
3. 主题切换（中等）
4. 鼠标穿透功能（较复杂）
