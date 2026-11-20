# 数据包一键安装：自动匹配版本 + 引导选择存档

## 一、要解决的问题

玩家现在装数据包的过程是：下载 zip → 找到 `.minecraft/saves/<存档>/datapacks/` → 手动复制 → 进游戏验证是否生效。
PCLPE 要把这条路径缩短成：**拖入/选择 zip → PCL 自动判断它是哪个 MC 版本的数据包 → 列出可用存档（带图标/名字/版本）→ 点一下就装好并启用**。

## 二、用户流程

```
用户拖入 zip / 点"安装数据包"
        │
        ▼
① 识别是不是数据包
   ├─ zip 内含 pack.mcmeta，或 data/ 目录 → 是数据包
   └─ 否则 → 提示"这不是数据包"，结束
        │
        ▼
② 读取 pack.mcmeta → pack_format（及 supported_formats 范围）
   映射成 MC 版本区间，例如 pack_format 48 → 1.21 ~ 1.21.1
        │
        ▼
③ 自动选版本
   ├─ 用户当前选中的实例版本在兼容区间内 → 直接标记"当前版本可用"
   ├─ 不兼容 → 列出本机所有满足区间的实例，让用户选
   └─ 一个都没有 → 提示"需要 X 版本，是否先去下载？"（跳下载页）
        │
        ▼
④ 引导选择存档
   扫描所选实例的 saves/*：
   每个存档显示  图标(icon.png) 世界名(level.dat→LevelName)
                 最后游玩时间(文件夹 mtime)  MC 版本  [数据包数量]
   ├─ 该存档 datapacks/ 里已有同名包 → 提示"已存在，是否覆盖/换名"
   └─ 支持"新建一个空存档"（只建目录，进游戏自动生成）
        │
        ▼
⑤ 安装并启用
   复制 zip 到 saves/<世界>/datapacks/
   若 level.dat 的 DataPacks.Disabled 含该包名 → 从 Disabled 中移除（即启用）
   提示"已安装到 <世界>，进游戏即可使用"
```

## 三、技术设计

### 3.1 识别与版本匹配

- `PackMeta` 读取：zip 内 `pack.mcmeta`（JSON），字段 `pack.pack_format`、
  `pack.supported_formats`（1.20.2+ 可能是数组或 `{min_inclusive,max_inclusive}`）。
- 版本映射表：内置一张 `pack_format ↔ MC 版本区间` 表（1.13=4 … 1.21.x=48+），
  以常量表形式放在新模块里，和 PCL 已有的 MC 版本列表做区间求交。
- 结果结构：`DataPackInfo { Path, PackFormat, MinVersion, MaxVersion, IsDataPack }`。

### 3.2 存档扫描与 NBT

PCL 目前**没有 NBT 解析器**，需要新增一个最小实现（只读）：

- `NbtReader.vb`：支持 gzip 解压 + TAG_Compound/List/String/Int/Long/Byte 读取，够读 level.dat 即可。
- 从 `level.dat` 取：`LevelName`、`Version.Name`、`DataPacks.Enabled/Disabled`、`GameType`、`LastPlayed`。
- 图标：`saves/<世界>/icon.png`，用现有 `MyBitmap` 加载并做圆角裁剪。
- 数据包数量：`saves/<世界>/datapacks/*.zip` 计数。

### 3.3 UI

- 新增弹窗页 `PageInstanceDataPackInstall`（复用 `MyCard` / `MyListItem` / 玻璃样式）：
  - 顶部：数据包名 + pack_format + 版本匹配结论（绿/黄/红徽标）
  - 中部：实例选择（仅当需要时出现）
  - 下部：存档列表（图标 + 名字 + 版本 + 时间），单选，双击直接安装
- 入口：
  - 拖拽 zip 到窗口（`FormMain.FrmMain_Drop` 里识别 .zip → 走数据包分支）
  - 下载页「数据包」分类下的"本地安装"按钮
  - 版本设置页「数据包」卡片

### 3.4 安装

- 目标 `saves\<world>\datapacks\<name>.zip`；重名时按 `name_1.zip` 递增或询问覆盖。
- 启用：改了 level.dat 就要重写整个 NBT —— 风险高。
  **第一版不写 level.dat**：数据包默认就是启用的（除非玩家手动禁过）。
  只在检测到 `DataPacks.Disabled` 里包含它时提示"该包在此存档中被禁用，是否启用？"，
  启用需要写 NBT，放在 3.5 后续做（带 level.dat 备份）。

### 3.5 里程碑

| 阶段 | 内容 | 验收 |
|---|---|---|
| M1 | NBT 只读解析 + 存档扫描（名字/版本/图标/包数） | 能在日志里正确打印本机所有存档信息 |
| M2 | pack.mcmeta 解析 + 版本映射表 + 兼容判断 | 拖入 1.21 的数据包能报出正确区间，1.16 的报不兼容 |
| M3 | 安装弹窗 UI + 拖拽入口 + 复制到 datapacks | 拖入 zip → 选存档 → 文件出现在正确目录 |
| M4 | 实例自动选择 + "先去下载对应版本"跳转 | 无兼容实例时的引导可用 |
| M5 | level.dat 安全改写（带备份）用于启用被禁用的包 | 启用后进游戏数据包为勾选状态 |

### 3.6 风险

- level.dat 写坏会毁存档 → M5 必须先备份，且只在用户确认时写。
- pack_format 表需要随 MC 版本更新维护。
- 版本隔离：存档目录要用 `McInstanceSelected.PathIndie & "saves"` 而不是全局 `.minecraft\saves`。
- 大型整合包的存档可能有几十个，列表要虚拟化（复用 `MyVirtualizingElement`）。
