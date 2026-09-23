// 玩家藍本物件 (每個登入的玩家都會被 clone 出一份獨立的實體)

int hp = 1000;
string name = "Unknown Player";

// 【Apply】克隆後自動觸發的初始化函數
void create() {
    debug_message("🧬 [Player] 新的玩家實體已誕生！");
}

void setup_user(string user_name) {
    name = user_name;
    debug_message("歡迎玩家 " + name + " 進入遊戲！你的獨立 HP 是: ");
    debug_int(hp);
}

// 模擬玩家受傷 (只有這個 clone 實體的 HP 會減少)
void take_damage(int dmg) {
    hp = hp - dmg;
    debug_message(name + " 受到了 " + dmg + " 點傷害！剩餘 HP: ");
    debug_int(hp);
}
