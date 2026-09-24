void preload() {
    debug_message("Master preloading...\n");
    
    // 1. 測試 random (模擬傷害浮動)
    int dmg = 100 + random(50);
    debug_message("⚔️ [Combat] Base: 100, Final Dmg: " + dmg + "\n");

    // 2. 測試 explode (模擬聊天室指令解析)
    array parts = explode("/say Hello World", " ");
    debug_message("💬 [Chat] Cmd: " + parts[0] + "\n");

    // 3. 測試 replace_string (模擬敏感詞過濾)
    string filtered = replace_string("This is a bad word.", "bad", "***");
    debug_message("🛡️ [Filter] Result: " + filtered + "\n");

    // 4. 測試 time
    int now = time();
    debug_message("⏱️ [Server] Unix Time: " + now + "\n");
    
    load_object("obj/login");
}

string connect() {
    debug_message("New connection...\n");
    return clone_object("obj/login");
}
