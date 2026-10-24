using System;

namespace LithosNet.Compiler
{
    // Generated from neolith-1.0.0-alpha.10/lib/lpc/func_spec.c.in
    // Operators 0-113, BASE=114 for efuns
    // This matches Neolith 1:1 for bytecode hex identical
    public static class NeolithOpcodes
    {
        // Operators (F_* 0-113)
        public const byte F_POP_VALUE = 0;
        public const byte F_PUSH = 1;
        public const byte F_EFUN0 = 2;
        public const byte F_EFUN1 = 3;
        public const byte F_EFUN2 = 4;
        public const byte F_EFUN3 = 5;
        public const byte F_EFUNV = 6;
        public const byte F_NUMBER = 7;
        public const byte F_REAL = 8;
        public const byte F_LONG = 9;
        public const byte F_BYTE = 10;
        public const byte F_NBYTE = 11;
        public const byte F_STRING = 12;
        public const byte F_SHORT_STRING = 13;
        public const byte F_CONST0 = 14;
        public const byte F_CONST1 = 15;
        public const byte F_AGGREGATE = 16;
        public const byte F_AGGREGATE_ASSOC = 17;
        public const byte F_BRANCH_WHEN_ZERO = 18;
        public const byte F_BRANCH_WHEN_NON_ZERO = 19;
        public const byte F_BRANCH = 20;
        public const byte F_BBRANCH_WHEN_ZERO = 21;
        public const byte F_BBRANCH_WHEN_NON_ZERO = 22;
        public const byte F_BBRANCH = 23;
        public const byte F_BRANCH_NE = 24;
        public const byte F_BRANCH_GE = 25;
        public const byte F_BRANCH_LE = 26;
        public const byte F_BRANCH_EQ = 27;
        public const byte F_BBRANCH_LT = 28;
        public const byte F_FOREACH = 29;
        public const byte F_NEXT_FOREACH = 30;
        public const byte F_EXIT_FOREACH = 31;
        public const byte F_LOOP_COND_LOCAL = 32;
        public const byte F_LOOP_COND_NUMBER = 33;
        public const byte F_LOOP_INCR = 34;
        public const byte F_WHILE_DEC = 35;
        public const byte F_LOR = 36;
        public const byte F_LAND = 37;
        public const byte F_CATCH = 38;
        public const byte F_END_CATCH = 39;
        public const byte F_TIME_EXPRESSION = 40;
        public const byte F_END_TIME_EXPRESSION = 41;
        public const byte F_SWITCH = 42;
        public const byte F_CALL_FUNCTION_BY_ADDRESS = 43;
        public const byte F_CALL_INHERITED = 44;
        public const byte F_RETURN = 45;
        public const byte F_RETURN_ZERO = 46;
        public const byte F_EQ = 47;
        public const byte F_NE = 48;
        public const byte F_LE = 49;
        public const byte F_LT = 50;
        public const byte F_GE = 51;
        public const byte F_GT = 52;
        public const byte F_INC = 53;
        public const byte F_DEC = 54;
        public const byte F_PRE_INC = 55;
        public const byte F_POST_INC = 56;
        public const byte F_PRE_DEC = 57;
        public const byte F_POST_DEC = 58;
        public const byte F_TRANSFER_LOCAL = 59;
        public const byte F_LOCAL = 60;
        public const byte F_LOCAL_LVALUE = 61;
        public const byte F_GLOBAL = 62;
        public const byte F_GLOBAL_LVALUE = 63;
        public const byte F_MEMBER = 64;
        public const byte F_MEMBER_LVALUE = 65;
        public const byte F_INDEX = 66;
        public const byte F_INDEX_LVALUE = 67;
        public const byte F_RINDEX = 68;
        public const byte F_RINDEX_LVALUE = 69;
        public const byte F_NN_RANGE = 70;
        public const byte F_NN_RANGE_LVALUE = 71;
        public const byte F_RN_RANGE = 72;
        public const byte F_RN_RANGE_LVALUE = 73;
        public const byte F_RR_RANGE = 74;
        public const byte F_RR_RANGE_LVALUE = 75;
        public const byte F_NR_RANGE = 76;
        public const byte F_NR_RANGE_LVALUE = 77;
        public const byte F_NE_RANGE = 78;
        public const byte F_RE_RANGE = 79;
        public const byte F_ADD_EQ = 80;
        public const byte F_SUB_EQ = 81;
        public const byte F_AND_EQ = 82;
        public const byte F_OR_EQ = 83;
        public const byte F_XOR_EQ = 84;
        public const byte F_LSH_EQ = 85;
        public const byte F_RSH_EQ = 86;
        public const byte F_MULT_EQ = 87;
        public const byte F_DIV_EQ = 88;
        public const byte F_MOD_EQ = 89;
        public const byte F_ASSIGN = 90;
        public const byte F_VOID_ADD_EQ = 91;
        public const byte F_VOID_ASSIGN = 92;
        public const byte F_VOID_ASSIGN_LOCAL = 93;
        public const byte F_ADD = 94;
        public const byte F_SUBTRACT = 95;
        public const byte F_MULTIPLY = 96;
        public const byte F_DIVIDE = 97;
        public const byte F_MOD = 98;
        public const byte F_AND = 99;
        public const byte F_OR = 100;
        public const byte F_XOR = 101;
        public const byte F_LSH = 102;
        public const byte F_RSH = 103;
        public const byte F_NOT = 104;
        public const byte F_NEGATE = 105;
        public const byte F_COMPL = 106;
        public const byte F_FUNCTION_CONSTRUCTOR = 107;
        public const byte F_SIMUL_EFUN = 108;
        public const byte F_SSCANF = 109;
        public const byte F_PARSE_COMMAND = 110;
        public const byte F_NEW_CLASS = 111;
        public const byte F_NEW_EMPTY_CLASS = 112;
        public const byte F_EXPAND_VARARGS = 113;

        // BASE for efuns
        public const int BASE = 114;

        // PUSH encoding from interpret.h
        public const byte PUSH_STRING = (0 << 6); // 0
        public const byte PUSH_NUMBER = (1 << 6); // 64
        public const byte PUSH_GLOBAL = (2 << 6); // 128
        public const byte PUSH_LOCAL = (3 << 6); // 192
        public const byte PUSH_WHAT = (3 << 6);
        public const byte PUSH_MASK = (byte)(0xff ^ PUSH_WHAT); // 63

        // A_* areas from compiler.h
        public const int A_PROGRAM = 0;
        public const int A_RUNTIME_FUNCTIONS = 1;
        public const int A_COMPILER_FUNCTIONS = 2;
        public const int A_RUNTIME_COMPRESSED = 3;
        public const int A_FUNCTION_FLAGS = 4;
        public const int A_STRINGS = 5;
        public const int A_VAR_NAME = 6;
        public const int A_VAR_TYPE = 7;
        public const int A_LINENUMBERS = 8;
        public const int A_FILE_INFO = 9;
        public const int A_INHERITS = 10;
        public const int A_CLASS_DEF = 11;
        public const int A_CLASS_MEMBER = 12;
        public const int A_ARGUMENT_TYPES = 13;
        public const int A_ARGUMENT_INDEX = 14;
        public const int NUMPAREAS = 15;
        public const int A_CASES = 15;
        public const int A_STRING_NEXT = 16;
        public const int A_STRING_REFS = 17;
        public const int A_INCLUDES = 18;
        public const int A_PATCH = 19;
        public const int A_INITIALIZER = 20;
        public const int A_FUNCTIONALS = 21;
        public const int A_FUNCTION_DEFS = 22;
        public const int A_VAR_TEMP = 23;
        public const int NUMAREAS = 24;

        // Compatibility aliases for old P45 names
        public const byte F_PUSH_MAPPING = F_AGGREGATE_ASSOC; // 17
        public const byte F_MAP_ADD = 71; // NN_RANGE_LVALUE reused as placeholder, will be efun
        public const byte F_PUSH_ARRAY = F_AGGREGATE; // 16
        public const byte F_SIZEOF = 200; // efun, not operator
        public const byte F_KEYS = 201;
        public const byte F_VALUES = 202;
        public const byte F_M_DELETE = 203;
        public const byte F_PUSH_CLOSURE = F_FUNCTION_CONSTRUCTOR; // 107
        public const byte F_EVALUATE = 115; // BASE+1
        public const byte F_CALL_OTHER = 114; // BASE+0
        public const byte F_CLONE_OBJECT = 122; // from efun list
    }
}

