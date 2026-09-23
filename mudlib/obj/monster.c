// 基礎怪物類別 (所有怪物都 inherit 它)

int hp = 100;
string name = "Unknown Monster";

void take_damage(int dmg) {
    hp = hp - dmg;
    debug_message(name + " 受到了 " + dmg + " 點傷害！");
    debug_message("剩餘 HP: ");
    debug_int(hp);
}
