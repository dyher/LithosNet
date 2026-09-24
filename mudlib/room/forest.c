
inherit "room";
int query_desc() { return "Dark Forest."; }
int get_exit(int dir) {
    if (dir == "south") return "town";
    return "";
}
