# Boot ES2 - 中文

尝试 boot ~/es2_auth/neolith.conf
MudlibDir=mudlib MasterFile=/adm/obj/master.lpc

当前进度:
- Driver --boot 已实现 ParseFile + Boot()
- ObjectManager 支持 .lpc 后缀
- master.lpc 编译失败: 需要 catch(new), 三元?: , stringp 等

下一步:
1. Preprocessor 支持 #include <globals.h> 展开 LOGIN_OB 等宏
2. Grammar 支持 catch(expr), new(class), ?: 三元
3. 补齐 efun: read_file, explode, filter, debug_message, sprintf, query_ip_number

英文 ../../en/driver/boot-es2.md
