TARGET = 'mudlib/obj/master.c'

perfect_code = '''int preload() {
    int r;
    int dmg;
    string filtered;
    int now;

    debug_message("Master preloading...\\n");
    
    r = random(50);
    dmg = 100 + r;
    debug_message("Combat Dmg: " + dmg + "\\n");

    filtered = replace_string("bad word", "bad", "***");
    debug_message("Filter: " + filtered + "\\n");

    now = time();
    debug_message("Time: " + now + "\\n");

    load_object("obj/room");
    load_object("room/town");
    load_object("room/forest");
    load_object("obj/player");
    load_object("obj/login");
    debug_message("Master ready!\\n");
    return 1;
}

int connect() {
    debug_message("New connection...\\n");
    return clone_object("login");
}
'''

with open(TARGET, 'w', encoding='utf-8') as f:
    f.write(perfect_code)
print("✅ 已完美覆蓋 mudlib/obj/master.c！C89 鐵律與無註解標準已確立！")
