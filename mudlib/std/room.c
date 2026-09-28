string room_name = "Unknown Room";
void create() { debug_message("🏠 [Room] create() called.\n"); }
void set_name(string name) { room_name = name; }
void init(object ob) { debug_message("🔔 [Room] init() called by " + ob + "\n"); }
