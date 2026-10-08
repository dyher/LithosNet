# LithosNet_V4 - 1:1 復刻 taedlar/neolith

Source: https://github.com/taedlar/neolith main

對齊表:
- lib/lpc/svalue.h -> src/vm/SValue.cs (lpc::svalue owning + svalue_view)
- lib/lpc/object.h -> src/vm/Object.cs (object_t + O_DESTRUCTED)
- lib/lpc/program.h -> src/vm/Program.cs (program_t + function_table)
- src/interpret.c -> src/vm/Interpreter.cs (eval_instruction)
- lib/lpc/grammar.y -> src/compiler/Grammar.cs
- lib/lpc/func_spec.c.in -> src/packages/EfunSpec.cs (600+ efun生成)
- src/main.c + src/simulate.c -> src/driver/Main.cs (init_stem, epilog)

Phase:
V4-P1: svalue_t int64 + object lifecycle 1:1
V4-P2: compiler + bytecode VM
V4-P3: efun 600 + master + simul_efun 跑 ES2 /d/city/center
V4-P4: DGD atomic + dump_state
V4-P5: Skynet actor mq + cluster
