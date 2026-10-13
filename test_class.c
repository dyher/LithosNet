// test_class.c - P44 ::調父
inherit "base";
int query_damage() { return 100; }
int test() {
  return ::query_damage();
}
int main() {
  return test();
}
