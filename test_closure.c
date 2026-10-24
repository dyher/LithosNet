// test_closure.c - P45 (: :) closure 3 green
int main() {
  closure cl = (: $1 + $2 :);
  return evaluate(cl, 1, 2);
}
