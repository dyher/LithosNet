// 幽暗森林

string query_desc() {
    return "這裡是幽暗的森林，樹葉沙沙作響，隱約傳來怪物的低吼。南方是說話之鎮。";
}

string get_exit(string dir) {
    if (dir == "south") return "town"; // 改為純名稱
    return "";
}
