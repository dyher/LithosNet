# 🚀 Lithos.NET
### The Next-Generation LPC Game Server Engine
**Reimagining MUD architecture with C# .NET 8, Zero-Copy Networking, and Native FFI.**

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)
![Status](https://img.shields.io/badge/status-Active-success)

**Lithos.NET** is a modern, high-performance LPC (Lars Pensjö C) game server engine built from the ground up using **C# .NET 8**. 
It aims to provide the classic FluffOS/LDMud development experience while leveraging modern software engineering paradigms: memory safety, zero-copy asynchronous I/O, and native JIT compilation.

---

## 🌟 Why Lithos.NET?

For over 30 years, MUD engines (like MudOS, FluffOS, LDMud) have been built on C/C++. While powerful, they suffer from legacy baggage: complex Telnet state machines, manual memory management (segfaults), and opaque build systems (`edit_source`).

**Lithos.NET throws away the legacy baggage and embraces the future:**

| Feature | Legacy C/C++ Drivers (FluffOS/MudOS) | Lithos.NET (.NET 8) |
| :--- | :--- | :--- |
| **Networking** | `message_buf` + Telnet IAC escaping | `System.IO.Pipelines` (Zero-Copy, Native Binary/MMORPG support) |
| **Concurrency** | `setjmp/longjmp` (Fragile Fibers) | `async/await` & `Task.Delay` (Safe, Non-blocking `call_out`) |
| **Memory** | Manual Ref-Counting + Custom GC | .NET High-Performance GC + `readonly struct` (Zero GC pressure) |
| **Efun Binding** | `func_spec.c` + `edit_source` blackbox | C# Reflection + `[Efun]` Attributes (Hot-pluggable) |
| **FFI / Plugins** | Complex C pointers | `P/Invoke` (Nanosecond-level C/C++ library integration) |
| **Cross-Platform** | Painful Makefile/CMake tweaks | `dotnet run` (Works perfectly on Win/Linux/macOS/ARM64) |

---

## 🏗️ Core Architecture

- **Zero-Copy Network Layer**: Handles binary MMORPG packets natively without `\0` truncation or Telnet interference.
- **Turing-Complete LPC Interpreter**: A hand-written Lexer, Recursive Descent Parser, and AST-based Interpreter.
- **True OOP & Cloning**: Supports `inherit` (inheritance) and `clone_object()` (instantiation with isolated scopes).
- **Master Object**: Fully aligned with FluffOS architecture (`master.c` handles `connect()` applies).
- **Native FFI**: Seamlessly call C/C++ `.so`/`.dll` libraries for heavy computational tasks (e.g., combat formulas).

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run
```bash
git clone https://github.com/YOUR_USERNAME/LithosNet.git
cd LithosNet
dotnet build
dotnet run --project LithosNet.Host
```

### The Mudlib
Lithos.NET uses a standard FluffOS-style mudlib structure located in `mudlib/obj/`.
- `master.c`: The controller of the world.
- `login.c`: Handles user authentication and object cloning.
- `player.c`: The blueprint for player entities.

---

## 🛣️ Roadmap

- [x] Zero-Copy Binary Networking (Pipelines)
- [x] Turing-Complete LPC VM (If/While/For/Math)
- [x] Data Structures (Array, Mapping)
- [x] Cross-Object Communication (`->` Call Other)
- [x] Object-Oriented Inheritance (`inherit`)
- [x] Asynchronous Timers (`call_out`)
- [x] Object Instantiation (`clone_object`)
- [ ] Persistence & Serialization (JSON/SQLite)
- [ ] Command Parsing & Room Navigation
- [ ] Hot-Reloading (`update_object`)
- [ ] LuaJIT / Native Math Engine Integration

---

## 🤝 Contributing
Lithos.NET is an experimental and educational project aimed at pushing the boundaries of game server architecture. Contributions, issues, and discussions are highly welcome!

## 📄 License
MIT License
