# Efun: filter / map / sort_array - 中文

## 根源
共用 F_EVALUATE 閉包，三個連鎖 bug:
1. InsByte(1) 是 opcode 2 = F_EFUN0，應用 F_CONST1=15 或 F_BYTE 10,n
2. Branch: InsByte(94) 註解寫 F_BRANCH_WHEN_ZERO 但 94 是 F_ADD，正確 18
3. foreach LVALUE 順序錯 KEYS,LVALUE,POP 導致 LVALUE 留堆疊

## 修後
- filter: a, closure, F_FILTER
- map: F_CONST0, F_MAP_ADD (不是 1)
- sort_array: a, closure, F_SORT_ARRAY

## 測試
filter({1,2,3,4,5}, (: $1%2==1 :)) -> {1,3,5}
map({1,2,3}, (: $1*2 :)) -> {2,4,6}
sort_array({5,2,8,1}, (: $1<$2 :)) -> {1,2,5,8}
