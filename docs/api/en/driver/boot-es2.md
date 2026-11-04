# Boot ES2 - English

Try boot ~/es2_auth/neolith.conf
MudlibDir=mudlib MasterFile=/adm/obj/master.lpc

Current:
- Driver --boot implemented ParseFile + Boot()
- ObjectManager supports .lpc ext
- master.lpc compile fail: need catch(new), ternary ?:, stringp etc

Next:
1. Preprocessor support #include <globals.h> expand LOGIN_OB macros
2. Grammar support catch(expr), new(class), ?: ternary
3. Efuns: read_file, explode, filter, debug_message, sprintf, query_ip_number

中文 ../../zh/driver/boot-es2.md
