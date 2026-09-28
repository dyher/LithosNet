inherit "/std/object";
int health = 100;
void create() {
    ::create();
    debug_message("🧬 [Living] create() called. health = " + health + "\n");
}
