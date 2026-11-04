# CallOut / HeartBeat - 中文

Neolith: call_out.cpp 定时器堆, heart_beat.c set_heart_beat

LithosNet:
- CallOutManager src/vm/CallOutManager.cs: CallOut(obj, func, delayMs, args), RemoveCallOut, Tick(interp) 检查 DueTick
- HeartBeatManager src/vm/HeartBeatManager.cs: SetHeartBeat, Tick 每1000ms扫描 heart_beat()

测试 call_out 2秒后执行, set_heart_beat 1 每秒
