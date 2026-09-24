import os

files = {
    "mudlib/obj/master.c": """
int preload() {
    int _d1 = debug_message("Master preloading...");
    int _d2 = load_object("obj/room");
    int _d3 = load_object("room/town");
    int _d4 = load_object("room/forest");
    int _d5 = load_object("obj/player");
    int _d6 = load_object("obj/login");
    int _d7 = debug_message("Master ready!");
    return 1;
}

int connect() {
    int _d1 = debug_message("New connection...");
    return clone_object("login");
}
""",
    "mudlib/obj/login.c": """
int user_name;
int state;

int logon() {
    int _d1 = send_to_user("=== Welcome ===\\nEnter account: ");
    return 1;
}

int receive_message(int msg) {
    if (state == 0) {
        user_name = msg;
        state = 1;
        int _d1 = send_to_user("Enter password: ");
    } else {
        if (msg == "1234") {
            int _d1 = send_to_user("Login success!\\n");
            int p = clone_object("player");
            int _d2 = p->setup_user(user_name);
            int _d3 = exec(p, this_object());
            int _d4 = destruct(this_object());
        } else {
            int _d1 = send_to_user("Wrong!\\nEnter account: ");
            state = 0;
        }
    }
    return 1;
}
""",
    "mudlib/obj/player.c": """
inherit "std/living";
int name;
int hp;

int setup_user(int user_name) {
    name = user_name;
    hp = 1000;
    int _d1 = send_to_user("=== Welcome ===");
    int _d2 = send_to_user("Commands: move, spawn, cast, quit");
    int _d3 = setup_living(50, 50);
    return 1;
}

int logoff() {
    int _d1 = save_object(name);
    return 1;
}

int receive_message(int msg) {
    int _d1 = send_to_user("Text received.");
    return 1;
}

int receive_binary(int json_str) {
    int data = json_decode(json_str);
    int cmd = data["cmd"];
    if (cmd == "MOVE") {
        int newX = data["x"];
        int newY = data["y"];
        int _d1 = map_move(this_object(), newX, newY);
        x = newX;
        y = newY;
        int _d2 = send_to_user("Moved via Binary!");
    } else if (cmd == "ATTACK") {
        int _d1 = send_to_user("Attack via Binary!");
    }
    return 1;
}
""",
    "mudlib/obj/goblin.c": """
inherit "std/living";
int name;
int hp;

int create() {
    int _d1 = set_heart_beat(0);
    return 1;
}

int setup_living(int start_x, int start_y) {
    name = "Goblin";
    hp = 50;
    int _d1 = map_register(this_object(), start_x, start_y);
    return 1;
}

int take_damage(int dmg) {
    hp = hp - dmg;
    if (hp <= 0) {
        int _d1 = destruct(this_object());
    }
    return 1;
}
""",
    "mudlib/obj/room.c": """
int init(int who) {
    return 1;
}
int receive_message(int msg, int source) { return 1; }
int query_desc() { return "A room."; }
int get_exit(int dir) { return ""; }
""",
    "mudlib/room/town.c": """
inherit "room";
int query_desc() { return "Prontera."; }
int get_exit(int dir) {
    if (dir == "north") return "forest";
    return "";
}
""",
    "mudlib/room/forest.c": """
inherit "room";
int query_desc() { return "Dark Forest."; }
int get_exit(int dir) {
    if (dir == "south") return "town";
    return "";
}
""",
    "mudlib/std/living.c": """
int x;
int y;

int setup_living(int start_x, int start_y) {
    x = start_x;
    y = start_y;
    int _d1 = map_register(this_object(), x, y);
    return 1;
}

int aoi_enter(int who, int wx, int wy) { return 1; }
int aoi_leave(int who) { return 1; }
""",
    "mudlib/obj/benchmark.c": """
int calc_damage(int atk, int def) { return (atk - def) * 150 / 100; }
int do_benchmark() {
    int sum = 0; int i = 0;
    int start_ast = get_tick();
    while (i < 1000) { sum = sum + calc_damage(150, 50); i = i + 1; }
    int time_ast = get_tick() - start_ast;
    int _d1 = tell_object(this_player(), "AST done");
    int _d2 = compile_function("calc_damage");
    sum = 0; i = 0;
    int start_jit = get_tick();
    while (i < 1000) { sum = sum + calc_damage(150, 50); i = i + 1; }
    int time_jit = get_tick() - start_jit;
    int _d3 = tell_object(this_player(), "JIT done");
    return 1;
}
"""
}

for path, content in files.items():
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
print("✅ 所有核心 LPC 檔案已核彈級重寫！徹底免疫 Parser 限制！")
