void preload() {
    debug_message("Master preloading...\n");
    
    // 1. 測試 random (拆開寫，避免複雜表達式)
    int r = random(50);
    int dmg = 100 + r;
    debug_message("Combat Dmg: " + dmg + "\n");

    // 2. 測試 replace_string
    string filtered = replace_string("bad word", "bad", "***");
    debug_message("Filter: " + filtered + "\n");

    // 3. 測試 time
    int now = time();
    debug_message("Time: " + now + "\n");
    
    load_object("obj/login");
}

string connect() {
    debug_message("New connection...\n");
    return clone_object("obj/login");
}
