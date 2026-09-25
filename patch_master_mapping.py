TARGET = 'mudlib/obj/master.c'
with open(TARGET, 'r', encoding='utf-8') as f:
    code = f.read()

if "epic_sword" not in code:
    # 在變數宣告區加入 mapping 與 array
    code = code.replace("int i;\n", "int i;\n    mapping epic_sword;\n    array affixes;\n    string affix;\n")
    
    # 在 Master ready! 之前加入 Mapping 測試邏輯
    map_test = '''
    epic_sword = ([ "name": "Shadowfang", "damage": 150, "durability": 100 ]);
    epic_sword["crit_rate"] = 25;
    m_delete(epic_sword, "durability");
    
    affixes = keys(epic_sword);
    debug_message("🗡 [Item] Epic Sword Affixes: " + implode(affixes, ", ") + "\\n");
    
    affix = element_of(affixes);
    debug_message("🎲 [Loot] Randomly rolled affix: " + affix + " -> " + epic_sword[affix] + "\\n");
'''
    code = code.replace(
        'debug_message("Master ready!\\n");',
        map_test + '\n    debug_message("Master ready!\\n");'
    )
    
    with open(TARGET, 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ master.c 已加入史詩級武器 Mapping 測試！")
else:
    print("ℹ Mapping 測試已存在。")
