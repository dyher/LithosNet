void preload() {
    debug_message("Master preloading...\n");
    load_object("obj/test_mmorpg");
    "/obj/test_mmorpg"->test_logic();
}

string connect() {
    debug_message("New connection...\n");
    return clone_object("login");
}
