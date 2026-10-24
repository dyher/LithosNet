// test_closure add
int main() {
  closure cl = (: $1 + $2 :);
  return evaluate(cl, 10, 20);
}
