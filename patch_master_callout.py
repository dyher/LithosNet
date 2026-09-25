import re

TARGET = 'mudlib/obj/master.c'
with open(TARGET, 'r', encoding='utf-8') as f:
    code = f.read()

if "delayed_func" not in code:
    # 在 preload 變數宣告區加入 handle
    code = code.replace("int now;\n", "int now;\n    int handle;\n")
    
    # 在 Master ready! 之前加入 call_out
    code = code.replace(
        'debug_message("Master ready!\\n");',
        'handle = call_out("delayed_func", 3);\n    debug_message("Scheduled call_out with handle: " + handle + "\\n");\n    debug_message("Master ready!\\n");'
    )
    
    # 在檔案末尾加入 delayed_func
    code += '''
int delayed_func() {
    debug_message("⏰ Delayed message from call_out! (3 seconds later)\\n");
    return 1;
}
'''
    with open(TARGET, 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ master.c 已加入 3 秒延遲觸發測試！")
else:
    print("ℹ delayed_func 已存在。")
