string room_name;
int room_id;
void setup_room(string name, int id) {
    room_name = name;
    room_id = id;
    debug_message("BaseRoom Setup: " + room_name + "\n");
}
string get_description() {
    return "Generic room: " + room_name;
}
