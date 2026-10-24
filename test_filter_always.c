// test_filter always true
int main() {
  int *arr = ({ 1,2,3,4,5,6 });
  int *res = filter(arr, (: 1 :));
  return sizeof(res);
}
