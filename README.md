# LithosNet

LithosNet 是一個基於 C# / .NET 8 構建的現代 MUD (Multi-User Dungeon) Driver。
本專案的設計哲學是 **「穩定、語義嚴謹、易於擴展」**，底層採用 ANTLR4 進行 LPC 語法解析，並透過純 C# 實現的 Tree-walking 直譯器執行。

LithosNet 致力於在 .NET 生態中重現 FluffOS / LDMud 的標準 LPC 開發體驗，同時利用現代 C# 的強大能力（如非同步 I/O、泛型、Reflection）來構建健壯的伺服器架構。

## 核心特性 (已完成)

### 1. 網路與登入流程
- **Telnet 協議支援**：基礎的 IAC 控制字元過濾與標準 CRLF 換行處理。
- **標準 MUD 登入鏈路**：完整實現 `master->connect()`、自動 Clone 虛擬路徑解析、`logon()` 觸發。
- **Input Trap 機制**：實現 `input_to()`，精準攔截並路由玩家的特定輸入。

### 2. 直譯器與語法
- **Tree-walking 直譯器**：優先保證語義的絕對正確與 `reload_object` (熱更新) 的無縫支援。
- **物件導向與繼承**：支援 `inherit`，實現 Blueprint 與 Clone 的記憶體隔離與原型鏈查找。
- **異常處理**：實現標準的 `catch()` 與 `throw()`，確保單一 LPC 腳本的錯誤不會導致 Driver 崩潰。

### 3. 狀態管理與持久化
- **物件持久化**：實現 `save_object()` / `restore_object()`，將物件變數狀態序列化至 JSON 並精準還原。
- **指令路由系統**：實現 `add_action()` 與 `command()`，支援當前物件與環境 (房間) 的鏈式查找。
- **基礎資料處理**：實現 `explode()`, `implode()`, `member_array()` 等核心字串與陣列操作 Efun。

## 架構決策與技術債清理

- **放棄早期 JIT 實驗**：為避免多執行路徑（直譯 vs JIT）帶來的語義不一致、Scope 鏈斷裂與熱更新衝突，LithosNet 已徹底清除早期的 `_compiledFunctions` 與 IL 生成代碼。
- **效能優化原則**：未來的任何效能優化（如 Bytecode 或 JIT）將嚴格遵循「Profiler 數據驅動」原則，並必須具備完整的 Invalidation (失效) 設計，拒絕盲目引入新名詞或過度設計。

## 未來規劃 (Roadmap)

1. **Apply 機制與 Efun 插件化**：將 Efun 從 `Interpreter` 中剝離，改用 C# `[Attribute]` 自動註冊；實現 `heart_beat`, `init`, `reset` 等核心 Apply。
2. **進階 Efun 擴充**：實作 `sprintf` (格式化輸出)、`read_file` / `write_file` (檔案 I/O) 以及高階陣列操作。
3. **網路層現代化**：引入 `System.IO.Pipelines` 實現零分配位元組流解析，並探索 WebSocket 支援以適應現代 Web 客戶端。

## 編譯與運行

### 前置需求
- .NET 8 SDK
- Linux / Windows / macOS

### 編譯
```bash
dotnet build LithosNet.sln
```

### 運行
```bash
cd LithosNet.Host
dotnet run
```
*Driver 啟動後將監聽 `config.json` 中指定的 Port (預設 6900)。*

### 連線測試
```bash
telnet 127.0.0.1 6900
```

## 目錄結構
- `LithosNet.Compiler`: 基於 ANTLR4 的 LPC 語法解析器與 AST 建構器。
- `LithosNet.Core`: 核心資料結構 (AST 節點, LpcValue)。
- `LithosNet.VM`: 虛擬機核心 (ObjectManager, Interpreter, SessionManager)。
- `LithosNet.Host`: 應用程式入口與 TCP 監聽。
- `mudlib/`: LPC 腳本原始碼目錄。