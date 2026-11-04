# LithosNet API Docs

仿 【entity-Neolith¦canonical_name=Neolith】 `doc/efun` 和 `doc/applies` 結構，每完成一項功能就寫一個 api 檔，中英文雙版。

結構:
- vm/ - 虛擬機核心 SValue/Program/Interpreter/ObjectManager
- compiler/ - 編譯器 Lex/Grammar/CodeGen/Opcodes
- efun/ - 外部函數 filter/map/sort_array/keys...
- apply/ - master applies create/preload/epilog...
- driver/ - 驅動層 Driver/CallOut/HeartBeat

中文在 zh/，英文在 en/，檔名 1:1 對應。
