// 標準生物藍本 (所有玩家與怪物都 inherit 它)

int x = 0;
int y = 0;

void setup_living(int start_x, int start_y) {
    x = start_x;
    y = start_y;
    map_register(this_object(), x, y);
}

// 【AOI Apply】預設實作：印出提示
void aoi_enter(string who, int wx, int wy) {
    send_to_user(sprintf("👁️ [AOI] %s 進入了你的視野 (%d, %d)", who, wx, wy));
}

void aoi_leave(string who) {
    send_to_user(sprintf("💨 [AOI] %s 離開了你的視野", who));
}
