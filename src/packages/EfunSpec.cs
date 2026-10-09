
// 1:1 from taedlar/neolith lib/lpc/func_spec.c.in
// Pass1 CMake configure, Pass2 C preprocessor, Pass3 edit_source POST_BUILD -> efuns_*.h
// WARNING: order changes driver_id per L20-L24

namespace LithosNet.V4.Efun;

public enum Opcode {
    // operators L38-L99 - order defines F_* opcodes
    F_POP_VALUE, F_PUSH, F_EFUN0, F_EFUN1, F_EFUN2, F_EFUN3, F_EFUNV,
    F_NUMBER, F_REAL, F_LONG, F_BYTE, F_NBYTE, F_STRING, F_SHORT_STRING, F_CONST0, F_CONST1,
    F_AGGREGATE, F_AGGREGATE_ASSOC,
    F_BRANCH_WHEN_ZERO, F_BRANCH_WHEN_NON_ZERO, F_BRANCH,
    F_BBRANCH_WHEN_ZERO, F_BBRANCH_WHEN_NON_ZERO, F_BBRANCH,
    F_BRANCH_NE, F_BRANCH_GE, F_BRANCH_LE, F_BRANCH_EQ, F_BBRANCH_LT,
    F_FOREACH, F_NEXT_FOREACH, F_EXIT_FOREACH,
    // ... L52-L99 trimmed for Phase1, full list in func_spec.c.in 15K
}

public static class EfunSpec {
    // Most frequent efuns top per L120 comment: least used at bottom because first 255 single byte
    public static readonly string[] OrderedEfuns = new string[]{
        "call_other","evaluate","this_object","getuid","to_int","to_float",
        "clone_object","bind","this_player","previous_object","destruct",
        // full list 500+ from func_spec.c.in
    };
}
