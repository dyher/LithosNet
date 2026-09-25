TARGET = 'mudlib/obj/master.c'
with open(TARGET, 'r', encoding='utf-8') as f:
    code = f.read()

if "dummy1" not in code:
    # 在變數宣告區加入 array nearby;
    code = code.replace("int handle;\n", "int handle;\n    array nearby;\n    int i;\n")
    
    # 在 Master ready! 之前加入空間測試邏輯
    space_test = '''
    move_object("dummy1", 10, 10, 0);
    move_object("dummy2", 12, 10, 0);
    move_object("dummy3", 100, 100, 0);
    
    move_object("master", 10, 10, 0);
    
    nearby = get_objects_in_radius(10, 10, 0, 5);
    debug_message("👁 [AOI] Nearby objects (radius 5): " + implode(nearby, ", ") + "\\n");
'''
    code = code.replace(
        'debug_message("Master ready!\\n");',
        space_test + '\n    debug_message("Master ready!\\n");'
    )
    
    with open(TARGET, 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ master.c 已加入 3D 空間召喚與 AOI 查詢測試！")
else:
    print("ℹ 空間測試已存在。")
