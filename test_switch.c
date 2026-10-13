// test_switch.c - P43 switch 119 green
int test(int x) {
  switch(x) {
    case 1: return 11;
    case 2: return 22;
    case 5: return 119;
    default: return 0;
  }
}
int main() {
  return test(5);
}
