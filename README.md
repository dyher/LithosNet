# LithosNet - True 1:1 Port of taedlar/neolith

> **EN | 中文雙語** - Old Qwen speculation purged. Clean 1:1 from true sources.

## Why Rewrite? / 為何重寫？

**EN:** Previous `dyher/LithosNet` was Qwen speculation with wrong `Program` layout, `svalue_t` size, opcode numbers. From `8e4b6d1` all code is 1:1 from true neolith: `types.h`, `program.h`, `binaries.h`, `interpret.c`, `operator.c`.

**中文:** 舊版是 Qwen 臆測，`Program` 佈局、`svalue_t` 大小、opcode 全錯。從 `8e4b6d1` 起全部單文件真源 1:1 翻譯。

## Verified Mapping / 真源對應

| True Source | This Repo | Proof |
|---|---|---|
| `binaries.h` | `Program.DRIVER_ID` | `0x20260602` |
| `types.h` L44 | `SValueU` | `int64_t number` |
| `program.h` L196 | `Program` | 5 blocks, `<=65535` |
| `interpret.c` | `Interpreter` | `F_NUMBER=8 LOAD_INT / F_LONG=10 LOAD_LONG 8B LE` |
| `operator.c` | `f_and/f_xor` | `sp->u.number &=` int64_t |

## Build / 編譯

```bash
dotnet build -c Release
dotnet run -c Release -- --help
# [V4] driver_id=0x20260602 svalue_u.number=int64_t
```

## Design

- `Program` no V4 suffix, version is `DRIVER_ID`
- `SValue`: `T_NUMBER=0x2`, `STRING_SHARED=0x3`, `SValueView` pointer-sized
- `Interpreter`: true loop, `LOAD8/STORE8` LE

## Roadmap

- [x] P1-P5 Program/SValue/Object/Interpreter true
- [ ] P6 operator.c f_add int64_t
- [ ] P7 grammar.y
- [ ] P8 master/simul_efun

MIT
