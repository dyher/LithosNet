# Efun: filter / map / sort_array - English

## Root Cause
Share F_EVALUATE closure, 3 chained bugs:
1. InsByte(1) is opcode 2 = F_EFUN0, should use F_CONST1=15 or F_BYTE 10,n
2. Branch: InsByte(94) commented as F_BRANCH_WHEN_ZERO but 94 is F_ADD, correct 18
3. foreach LVALUE order KEYS,LVALUE,POP left LVALUE on stack

## Fixed
- filter: a, closure, F_FILTER
- map: F_CONST0, F_MAP_ADD not 1
- sort_array: a, closure, F_SORT_ARRAY

## Tests
filter({1,2,3,4,5}, (: $1%2==1 :)) -> {1,3,5}
map({1,2,3}, (: $1*2 :)) -> {2,4,6}
sort_array({5,2,8,1}, (: $1<$2 :)) -> {1,2,5,8}
