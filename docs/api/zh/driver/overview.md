# Driver 驅動層 - 中文

## Neolith 啟動順序 (main.cpp/backend.c)
1. DriverConfig.ParseFile(【entity-neolith¦canonical_name=Neolith】.conf) -> MudlibDir, MasterFile, SimulEfunFile, Port
2. master_ob = 0, MS_MUDLIB_LIMBO
3. 載入 master object
4. get_simul_efun, preload, epilog applies
5. 主迴圈: call_out, heart_beat, comm

## LithosNet 計劃
- P44: :: 與 class
- P45: 300 efuns (read_file, write_file, call_out, set_heart_beat, this_object...)
- P46: Apply.cs (applies.h enum), Driver.cs (Start), CallOutManager.cs, HeartBeatManager.cs

## 英文
../../en/driver/overview.md
