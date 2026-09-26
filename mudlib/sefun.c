// mudlib/sefun.c
// Phase 55.2: FluffOS 標準 Simul_efun (模擬 Efun)

string global_greeting(string name) {
    return "系統歡迎你, " + name + "！這是來自 simul_efun 的問候。";
}

int global_add(int a, int b) {
    return a + b;
}
