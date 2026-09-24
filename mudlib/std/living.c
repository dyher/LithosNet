
int x;
int y;

int setup_living(int start_x, int start_y) {
    x = start_x;
    y = start_y;
    map_register(this_object(), x, y);
    return 1;
}

int aoi_enter(int who, int wx, int wy) { return 1; }
int aoi_leave(int who) { return 1; }
