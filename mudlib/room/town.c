
inherit "room";
int query_desc() { return "Prontera."; }
int get_exit(int dir) {
    if (dir == "north") return "forest";
    return "";
}
