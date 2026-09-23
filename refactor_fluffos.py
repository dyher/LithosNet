import os
import json

# 1. 建立 config.json
config = {
    "name": "Lithos.NET",
    "port": 6900,
    "master_object": "master",
    "mudlib_dir": "/home/tiny/LithosNet/mudlib/"
}
with open("config.json", "w") as f:
    json.dump(config, f, indent=4)
print("✅ config.json 已建立")

# 2. 重寫 SessionManager.cs (加入 Exec 連線轉移與反查機制)
session_mgr = """using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LithosNet.VM {
    public static class SessionManager {
        private static readonly ConcurrentDictionary<string, PipeWriter> _sessions = new();
        private static readonly ConcurrentDictionary<PipeWriter, string> _writerToObj = new();
        public static readonly System.Threading.AsyncLocal<string> CurrentPlayer = new System.Threading.AsyncLocal<string>();

        public static void Bind(string objName, PipeWriter writer) {
            _sessions[objName] = writer;
            _writerToObj[writer] = objName;
            Console.WriteLine($"🔌 [Session] 綁定連線: {objName}");
        }

        public static void Unbind(string objName) {
            if (_sessions.TryRemove(objName, out var writer)) {
                _writerToObj.TryRemove(writer, out _);
                Console.WriteLine($"❌ [Session] 斷開連線: {objName}");
            }
        }

        public static async Task SendAsync(string objName, string message) {
            if (_sessions.TryGetValue(objName, out var writer)) {
                byte[] bytes = Encoding.UTF8.GetBytes(message + "\\n");
                await writer.WriteAsync(bytes);
                await writer.FlushAsync();
            }
        }

        public static List<string> GetAllSessions() => _sessions.Keys.ToList();
        
        public static string GetObjName(PipeWriter writer) {
            return _writerToObj.TryGetValue(writer, out var name) ? name : "";
        }

        // 【FluffOS 核心】exec(new_obj, old_obj) - 無縫轉移 TCP 連線
        public static void Exec(string newObj, string oldObj) {
            if (_sessions.TryRemove(oldObj, out var writer)) {
                _sessions[newObj] = writer;
                _writerToObj[writer] = newObj;
                CurrentPlayer.Value = newObj;
                Console.WriteLine($"🔄 [Session] 連線無縫轉移: {oldObj} -> {newObj}");
            }
        }
    }
}
"""
with open("LithosNet.VM/SessionManager.cs", "w") as f: f.write(session_mgr)
print("✅ SessionManager.cs 已重寫 (支援 exec)")

# 3. 重寫 Program.cs (極簡 Driver，完全依賴 Master Object)
program_cs = """using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LithosNet.Core;
using LithosNet.VM;

namespace LithosNet.Host {
    class Program {
        static readonly ObjectManager ObjMgr = new();
        static string MudlibPath;
        static string MasterObj;

        static async Task Main(string[] args) {
            Console.WriteLine("==================================================");
            Console.WriteLine("🔥 [Phase 30] 啟動正統 FluffOS 架構模式！");
            Console.WriteLine("==================================================\\n");

            EfunRegistry.RegisterFromType(typeof(BuiltInEfuns));
            
            // 讀取 config.json
            string cfgText = File.ReadAllText("config.json");
            var cfg = JsonDocument.Parse(cfgText).RootElement;
            MudlibPath = cfg.GetProperty("mudlib_dir").GetString();
            MasterObj = cfg.GetProperty("master_object").GetString();
            int port = cfg.GetProperty("port").GetInt32();

            // 【FluffOS 標準】Driver 只負責載入 Master Object
            ObjMgr.Preload(MudlibPath + "obj/" + MasterObj + ".c");
            
            // 呼叫 master->preload() 讓 LPC 自己決定要預載入什麼
            try { ObjMgr.CallFunction(MasterObj, "preload"); } catch (Exception e) { Console.WriteLine($"⚠️ Master preload 錯誤: {e.Message}"); }

            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"\\n🚀 {cfg.GetProperty("name").GetString()} Driver 啟動！監聽端口: {port}\\n");

            while (true) {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client) {
            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());
            
            // 【FluffOS 標準】呼叫 master->connect() 取得 login 物件
            string currentObj = "";
            try {
                currentObj = ObjMgr.CallFunction(MasterObj, "connect").AsString();
            } catch (Exception e) {
                Console.WriteLine($"❌ master->connect() 失敗: {e.Message}");
                client.Close(); return;
            }

            SessionManager.Bind(currentObj, writer);
            SessionManager.CurrentPlayer.Value = currentObj;
            
            // 呼叫 login->logon() 進行初始握手
            try { ObjMgr.CallFunction(currentObj, "logon"); } catch {}

            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    SequencePosition? position;
                    while ((position = buffer.PositionOf((byte)'\\n')) != null) {
                        var lineBytes = buffer.Slice(0, position.Value);
                        string line = Encoding.UTF8.GetString(lineBytes).Trim();
                        
                        // 【FluffOS 標準】Driver 不拆分指令，直接將整行字串丟給 receive_message
                        currentObj = SessionManager.GetObjName(writer);
                        if (string.IsNullOrEmpty(currentObj)) break;
                        
                        try {
                            ObjMgr.CallFunction(currentObj, "receive_message", LpcValue.Create(line));
                        } catch (Exception e) {
                            Console.WriteLine($"⚠️ LPC 執行錯誤 ({currentObj}): {e.Message}");
                        }
                        
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted) break;
                }
            } catch { }
            finally { 
                currentObj = SessionManager.GetObjName(writer);
                if (!string.IsNullOrEmpty(currentObj)) {
                    try { ObjMgr.CallFunction(currentObj, "logoff"); } catch {}
                    SessionManager.Unbind(currentObj);
                    ObjMgr.DestructObject(currentObj);
                }
                client.Close(); 
            }
        }
    }
}
"""
with open("LithosNet.Host/Program.cs", "w") as f: f.write(program_cs)
print("✅ Program.cs 已重寫 (極簡 Driver，完全解耦)")

# 4. 升級 Interpreter.cs (注入 exec 與 load_object)
with open("LithosNet.VM/Interpreter.cs", "r") as f: content = f.read()

exec_efun = """                    if (c.Name == "exec" && cArgs.Count >= 2) {
                        SessionManager.Exec(cArgs[0].AsString(), cArgs[1].AsString());
                        return LpcValue.Create(1);
                    }
                    if (c.Name == "load_object" && cArgs.Count >= 1) {
                        _objMgr.LoadObject(cArgs[0].AsString());
                        return LpcValue.Create(cArgs[0].AsString());
                    }
"""
if "c.Name == \"exec\"" not in content:
    content = content.replace("if (c.Name == \"this_object\")", exec_efun + "                    if (c.Name == \"this_object\")")
    with open("LithosNet.VM/Interpreter.cs", "w") as f: f.write(content)
    print("✅ Interpreter.cs 已注入 exec() 與 load_object()")

# 5. 重寫 master.c (正統 FluffOS Master Object)
master_c = """// 正統 FluffOS Master Object

void preload() {
    debug_message("🌍 [Master] 正在預載入核心藍本...");
    load_object("obj/room");
    load_object("room/town");
    load_object("room/forest");
    load_object("obj/player");
    load_object("obj/login");
    debug_message("🌍 [Master] 世界就緒！");
}

// 【Apply】當有新連線時，Driver 會呼叫此函數
string connect() {
    debug_message("🔌 [Master] 新連線請求，分配 login 物件...");
    return clone_object("login");
}
"""
with open("mudlib/obj/master.c", "w") as f: f.write(master_c)
print("✅ master.c 已重寫")

# 6. 重寫 login.c (處理握手與 exec 轉移)
login_c = """// 登入物件

string user_name = "";
int state = 0; // 0: 問帳號, 1: 問密碼

// 【Apply】連線綁定後自動觸發
void logon() {
    send_to_user("=== 歡迎來到 Lithos.NET ===\\n請輸入帳號: ");
}

// 【Apply】接收玩家輸入的每一行字串
void receive_message(string msg) {
    if (state == 0) {
        user_name = msg;
        state = 1;
        send_to_user("請輸入密碼: ");
    } else if (state == 1) {
        if (msg == "1234") {
            send_to_user("登入成功！正在進入世界...\\n");
            string p = clone_object("player");
            p->setup_user(user_name);
            
            // 【FluffOS 核心魔法】將 TCP 連線無縫轉移給 player，然後銷毀 login
            exec(p, this_object());
            destruct(this_object());
        } else {
            send_to_user("密碼錯誤！\\n請輸入帳號: ");
            state = 0;
        }
    }
}
"""
with open("mudlib/obj/login.c", "w") as f: f.write(login_c)
print("✅ login.c 已重寫")

# 7. 重寫 player.c (自己解析指令，不再依賴 Driver)
player_c = """// 玩家實體

string name = "Unknown";
string environment = ""; 
int hp = 1000;

void setup_user(string user_name) {
    name = user_name;
    send_to_user("=== 歡迎來到 Lithos.NET (MUD 向下兼容版) ===");
    send_to_user("可用指令: look, go <方向>, say <訊息>, bench, quit");
    move("town");
}

void logoff() { save_object(name); }

void receive_message(string msg) {
    // 【FluffOS 標準】LPC 自己負責拆分指令
    string[] parts = explode(msg, " ");
    string verb = parts[0];
    string args = "";
    if (sizeof(parts) > 1) {
        int i = 1;
        while(i < sizeof(parts)) {
            args = args + parts[i];
            if (i < sizeof(parts) - 1) args = args + " ";
            i++;
        }
    }
    
    if (verb == "look") send_to_user(environment->query_desc());
    else if (verb == "go") {
        string dest = environment->get_exit(args);
        if (dest != "") { move(dest); command("look", ""); }
        else send_to_user("那個方向沒有路。");
    }
    else if (verb == "say") {
        send_to_user("You say: " + args);
        message("say", name + " says: " + args);
    }
    else if (verb == "hp") send_to_user("HP: " + hp);
    else if (verb == "bench") {
        send_to_user("🔥 啟動 JIT 效能對決...");
        benchmark->do_benchmark();
    }
    else send_to_user("不懂指令: " + verb);
}

// 保留給內部呼叫的 command (例如 move 後自動 look)
void command(string verb, string args) {
    if (verb == "look") send_to_user(environment->query_desc());
}

void receive_msg(string msg, string source) { send_to_user("📢 [" + source + "] " + msg); }
string query_environment() { return environment; }
"""
with open("mudlib/obj/player.c", "w") as f: f.write(player_c)
print("✅ player.c 已重寫 (自行解析指令)")

print("\n🎉 所有核心檔案重構完成！")
