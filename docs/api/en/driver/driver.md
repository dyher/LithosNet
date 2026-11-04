# Driver Boot Flow - English

Neolith Original main.cpp/backend.c
1. DriverConfig.ParseFile
2. master_ob=0 MS_MUDLIB_LIMBO
3. Load master
4. get_simul_efun preload epilog
5. Main loop call_out heart_beat comm

LithosNet src/driver/Driver.cs
- Start(configPath) -> Boot()
- Boot(): LoadObject master, LoadObject sim, CallApply Preload, Preload list, CallApply Epilog
- Done Apply.cs 50+ consts, Driver.cs skeleton

TODO CallOutManager Tick, HeartBeatManager, comm.c
