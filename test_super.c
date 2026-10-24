// test_super.c - P45 super chain 10 green
inherit "base";
int query_damage() { return 100; }
int query_super() { return ::query_damage() + 5; }
int main() { return query_super(); }
