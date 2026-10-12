
// test.c - minimal LPC for P16a Grammar -> F_LONG test
// int64 test: 1234567890123 > int.MaxValue, should emit F_LONG

int test() {
  int a = 1234567890123;
  int b = 0xFF;
  return a & b;
}

int main() {
  return test();
}
