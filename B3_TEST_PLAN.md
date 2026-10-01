# B3 基礎 Efun 邊界測試規劃

## 目標
徹底對齊 FluffOS 的基礎 efun，消除所有臆測

## 測試分類

### 字串處理
- [ ] sprintf (格式化輸出)
- [ ] sscanf (格式化解析)
- [ ] explode (字串分割)
- [ ] implode (陣列拼接)
- [ ] replace_string (替換)
- [ ] strlen / lower_case / upper_case

### 陣列/映射
- [ ] sizeof (各類型大小)
- [ ] member (成員查找)
- [ ] allocate (分配)
- [ ] keys / values (映射操作)
- [ ] map_delete (刪除鍵)

### 檔案系統
- [ ] read_file / write_file
- [ ] file_size
- [ ] rm / mkdir

### 物件查詢
- [ ] all_inventory / children
- [ ] find_object
- [ ] 型別檢查：objectp, stringp, intp, arrayp, mapp, functionp

### 數學/隨機
- [ ] random
- [ ] abs / max / min

### 時間
- [ ] time / ctime

### 非同步
- [ ] call_out / remove_call_out

## 執行方式
使用 Test Harness 自動化測試套件，一個指令跑完所有用例
