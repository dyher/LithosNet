// 說話之鎮

string query_desc() {
    return "這裡是繁華的說話之鎮 (Prontera)，石板路上人來人往。北方通往幽暗森林。";
}

string get_exit(string dir) {
    if (dir == "north") return "forest"; // 改為純名稱
    return "";
}
