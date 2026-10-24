// test_closure simple $1
int main() {
  closure cl = (: $1 :);
  return evaluate(cl, 42);
}
