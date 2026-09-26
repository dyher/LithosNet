// mudlib/sefun.c
// Phase 55.2: FluffOS 標準 Simul_efun (模擬 Efun)

// 這是一個自定義的全局函數，將被 Driver 視為 Efun 一樣呼叫
string global_greeting(string name) {
    return "系統歡迎你, " + name + "！這是來自 simul_efun 的問候。";
}

int global_add(int a, int b) {
    return a + b;
}
