# CallOut / HeartBeat - English

Neolith: call_out.cpp timer heap, heart_beat.c set_heart_beat

LithosNet:
- CallOutManager src/vm/CallOutManager.cs: CallOut(obj, func, delayMs, args), RemoveCallOut, Tick
- HeartBeatManager src/vm/HeartBeatManager.cs: SetHeartBeat, Tick every 1000ms

Tests call_out after 2s, set_heart_beat every sec
