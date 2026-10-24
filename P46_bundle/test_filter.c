// test_filter.c - P45 filter true closure
int main() {
  int *arr = ({ 1,2,3,4,5,6 });
  int *res = filter(arr, (: $1 % 2 == 0 :));
  return sizeof(res);
}
