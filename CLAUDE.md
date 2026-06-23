# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

ApexTodo.Native 是 ApexTodo 的 C# 原生重写版本，使用 WPF 桌面框架 + SQLite 存储，替代原 Electron 版本。支持 Windows 桌面小组件模式和 WebDAV 同步。未来计划通过 .NET MAUI 扩展到 iOS。

## 构建和运行

```bash
dotnet build                                    # 编译整个解决方案（Linux 上需设置 EnableWindowsTargeting=true）
dotnet build src/ApexTodo.Core                  # 跨平台编译（可在 Linux CI 上运行）
dotnet run --project src/ApexTodo.Windows       # 运行 WPF 桌面应用
dotnet publish src/ApexTodo.Windows -c Release  # 发布
```

## 架构

```
ApexTodo.Native.sln
├── src/ApexTodo.Core/          # 跨平台核心库（无 UI 依赖）
│   ├── Models/
│   │   ├── TodoItem.cs         # 任务数据模型
│   │   ├── AppSettings.cs      # 应用设置模型
│   │   └── WebDavConfig.cs     # WebDAV 配置模型
│   ├── Data/
│   │   └── DatabaseContext.cs  # SQLite 连接管理，自动建表
│   └── Services/
│       ├── TodoRepository.cs   # 任务 CRUD（Dapper）
│       ├── SettingsService.cs  # 设置持久化（JSON 序列化存 SQLite）
│       └── SyncService.cs      # WebDAV 同步（HttpClient + PROPFIND）
│
└── src/ApexTodo.Windows/       # WPF 桌面应用
    ├── Views/
    │   └── MainWindow.xaml     # 无边框窗口：任务列表、设置面板、toast
    ├── ViewModels/
    │   └── MainViewModel.cs    # MVVM ViewModel（CommunityToolkit.Mvvm）
    ├── Services/
    │   └── HotkeyService.cs    # 全局热键（Win32 RegisterHotKey）
    ├── Converters/
    │   └── Converters.cs       # WPF 值转换器
    ├── App.xaml                # 深色主题资源（Catppuccin Mocha 色板）
    └── App.xaml.cs
```

### 数据流

```
用户操作 (WPF UI)
  → Command 绑定 (MainViewModel)
    → TodoRepository / SettingsService
      → SQLite 读写 (Dapper + Microsoft.Data.Sqlite)
        → 刷新 ObservableCollection → UI 自动更新
```

### 关键技术选型

| 组件 | 选择 | 说明 |
|------|------|------|
| MVVM | CommunityToolkit.Mvvm | Source Generator，[ObservableProperty] / [RelayCommand] |
| 数据库 | SQLite + Dapper | 轻量 ORM，SQL 直写 |
| WebDAV | HttpClient 直接实现 | PROPFIND 检查远端 mtime，GET/PUT 传输文件 |
| 托盘 | Hardcodet.NotifyIcon.Wpf | 系统托盘图标和菜单 |
| 热键 | Win32 P/Invoke | RegisterHotKey 注册全局快捷键 |

### SQLite 数据库

默认路径：`{Documents}/ApexTodo/todo.db`

```sql
CREATE TABLE todos (
    id TEXT PRIMARY KEY,
    text TEXT NOT NULL,
    completed INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    completed_at TEXT,
    sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

### WebDAV 同步策略

- 上传前检查远端文件 mtime（PROPFIND Depth:0）
- mtime 差 > 1 秒则覆盖（远端优先）
- 定时同步间隔可在设置中配置（默认 60 分钟）

## NuGet 依赖

### Core
- Microsoft.Data.Sqlite 9.0.0
- Dapper 2.1.66

### Windows
- CommunityToolkit.Mvvm 8.4.0
- Hardcodet.NotifyIcon.Wpf 2.0.1
- Microsoft.Data.Sqlite 9.0.0

## 代码约定

- .NET 9.0，C# 12，nullable enabled
- 深色主题：Catppuccin Mocha 色板（#1E1E2E 背景，#CDD6F4 前景）
- MVVM 模式：ViewModel 通过 CommunityToolkit.Mvvm 的 Source Generator 生成属性和命令
- 跨平台逻辑放 Core 层，Windows 特有逻辑放 Windows 层
