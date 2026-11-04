# Driver Layer - English

## Neolith Boot (main.cpp/backend.c)
1. DriverConfig.ParseFile(neolith.conf) -> MudlibDir, MasterFile, SimulEfunFile, Port
2. master_ob = 0, MS_MUDLIB_LIMBO
3. Load master object
4. get_simul_efun, preload, epilog applies
5. Main loop: call_out, heart_beat, comm

## LithosNet Plan
- P44: :: and class
- P45: 300 efuns (read_file, write_file, call_out, set_heart_beat, this_object...)
- P46: Apply.cs (applies.h enum), Driver.cs (Start), CallOutManager.cs, HeartBeatManager.cs

## 中文
../../zh/driver/overview.md
