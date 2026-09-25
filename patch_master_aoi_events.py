TARGET = 'mudlib/obj/master.c'
with open(TARGET, 'r', encoding='utf-8') as f:
    code = f.read()

if "aoi_enter" not in code:
    # 在 move_object 測試後，加入一個移動 dummy1 的動作，觸發視野變化
    move_test = '''
    debug_message("🏃 [Move] dummy1 is moving away...\\n");
    move_object("dummy1", 50, 50, 0);
'''
    code = code.replace(
        'debug_message("Master ready!\\n");',
        move_test + '\n    debug_message("Master ready!\\n");'
    )
    
    # 在檔案末尾加入 aoi_enter 和 aoi_leave 的 apply
    code += '''
int aoi_enter(string mover) {
    debug_message("👋 [AOI Apply] master sees " + mover + " ENTER!\\n");
    return 1;
}

int aoi_leave(string mover) {
    debug_message("🏃 [AOI Apply] master sees " + mover + " LEAVE!\\n");
    return 1;
}
'''
    with open(TARGET, 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ master.c 已加入 AOI 事件觸發測試！")
else:
    print("ℹ AOI 事件已存在。")
