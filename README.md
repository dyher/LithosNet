
# LithosNet_V4 - 1:1 taedlar/neolith

Source: https://github.com/taedlar/neolith main commit at 2026
Headers copied from /tmp/neolith_dump (user upload)

## File map 1:1
- lib/lpc/types.h (24K) -> src/vm/SValue.cs : svalue_u { int64_t number; } T_NUMBER=0x2 etc.
- lib/lpc/svalue.h (3K) -> src/vm/SValue.cs SValueView owning/borrowing
- lib/lpc/object.h (4.3K) -> src/vm/Object.cs O_* flags 0x0001..0x8000, sentence_s with args, object_s variables[1] last
- lib/lpc/program.h (12K) -> src/vm/Program.cs NAME_* flags, TYPE_MOD_ARRAY, program_s 5 blocks, driver_id 0x20260113
- src/interpret.h (13K) -> src/vm/Interpreter.cs PUSH_STRING/NUMBER/GLOBAL/LOCAL, control_stack_s, FRAME_*, ES_STACK_FULL
- src/apply.h (2.2K) -> src/vm/Apply.cs APPLY_CACHE_SIZE, MASTER_APPROVED, APPLY_SLOT_CALL
- lib/lpc/func_spec.c.in (15K) -> src/packages/EfunSpec.cs opcode order = binary compat

## Phase1 Done
- SValue int64 per int64-design.md F_NUMBER 32bit + F_LONG 64bit
- Object lifecycle O_DESTRUCTED check after apply per copilot-instructions
- Program binary layout contiguous per program.h comment

Next Phase2: implement EvalInstruction bytecode loop src/interpret.c + compiler grammar.y
