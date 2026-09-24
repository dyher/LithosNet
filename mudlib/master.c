void preload() {
    int r;
    int dmg;
    string filtered;
    int now;

    debug_message("Master preloading...\n");
    
    r = random(50);
    dmg = 100 + r;
    debug_message("Combat Dmg: " + dmg + "\n");

    filtered = replace_string("bad word", "bad", "***");
    debug_message("Filter: " + filtered + "\n");

    now = time();
    debug_message("Time: " + now + "\n");
    
    load_object("obj/login");
}

string connect() {
    debug_message("New connection...\n");
    return clone_object("obj/login");
}
