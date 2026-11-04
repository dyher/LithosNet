# Driver 启动流程 - 中文

Neolith 原版 main.cpp/backend.c
1. DriverConfig.ParseFile
2. master_ob=0 MS_MUDLIB_LIMBO
3. 载入 master
4. get_simul_efun preload epilog
5. 主循环 call_out heart_beat comm

LithosNet src/driver/Driver.cs
- Start(configPath) -> Boot()
- Boot(): LoadObject master, LoadObject sim, CallApply Preload, Preload list, CallApply Epilog
- 已完成 Apply.cs 50+常量, Driver.cs 骨架

待做 CallOutManager Tick, HeartBeatManager, comm.c
