// test_mapping.c - P45 mapping literal 3 green
int main() {
  mapping m = ([ "a": 1, "b": 2, "c": 3 ]);
  return sizeof(m);
}
