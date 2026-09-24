with open('LithosNet.Host/Program.cs', 'r', encoding='utf-8') as f:
    code = f.read()

start_idx = code.find('static async Task HandleClientAsync(TcpClient client)')
if start_idx != -1:
    brace_count = 0
    in_method = False
    end_idx = start_idx
    # 手動計算大括號層級，精準找到方法的結尾
    for i in range(start_idx, len(code)):
        if code[i] == '{':
            brace_count += 1
            in_method = True
        elif code[i] == '}':
            brace_count -= 1
            if in_method and brace_count == 0:
                end_idx = i + 1
                break
    
    new_method = """static async Task HandleClientAsync(TcpClient client) {
        Console.WriteLine("🔌 [Network] 新客戶端連線！");
        var reader = PipeReader.Create(client.GetStream());
        var writer = PipeWriter.Create(client.GetStream());
        
        string currentObj = "";
        try {
            var connectRet = ObjMgr.CallFunction(MasterObj, "connect");
            Console.WriteLine($"🔍 [Diag] master->connect() 返回: {connectRet.AsString()}");
            currentObj = connectRet.AsString();
            if (string.IsNullOrEmpty(currentObj) || currentObj == "0") {
                Console.WriteLine("⚠ connect() 返回無效值，強制 Fallback...");
                ObjMgr.LoadObject("obj/login");
                currentObj = "login#1";
            }
        } catch (Exception ex) {
            Console.WriteLine($"❌ master->connect() 失敗:\\n{ex}");
            client.Close(); return;
        }

        SessionManager.Bind(currentObj, writer);
        Console.WriteLine($"🔌 [Session] 綁定連線: {currentObj}");
        
        // 【關鍵】強制呼叫 logon() apply
        try { 
            Console.WriteLine($"🔍 [Diag] 準備呼叫 {currentObj}->logon()...");
            ObjMgr.CallFunction(currentObj, "logon"); 
            Console.WriteLine("✅ logon() 呼叫成功！");
        } catch (Exception ex) { 
            Console.WriteLine($"❌ logon() 呼叫失敗:\\n{ex}"); 
        }

        try {
            Console.WriteLine("🔍 [Diag] 進入 TCP 讀取迴圈...");
            while (true) {
                // 【絕對關鍵】必須有 await，否則會變成 Busy Loop 瞬間耗盡 CPU！
                ReadResult result = await reader.ReadAsync();
                Console.WriteLine("🔍 [Diag] PipeReader 收到資料！");
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (true) {
                    if (buffer.Length == 0) break;
                    byte firstByte = buffer.Slice(0, 1).ToArray()[0];
                    
                    if (firstByte == 255) {
                        if (buffer.Length < 5) break;
                        byte[] lenBytes = buffer.Slice(1, 4).ToArray();
                        int length = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];
                        if (buffer.Length < 5 + length) break;
                        
                        byte[] payload = buffer.Slice(5, length).ToArray();
                        string json = Encoding.UTF8.GetString(payload);
                        try { ObjMgr.CallFunction(currentObj, "receive_binary", LpcValue.Create(json)); } catch {}
                        buffer = buffer.Slice(5 + length);
                    } else {
                        SequencePosition? position = buffer.PositionOf((byte)10);
                        if (position == null) break;
                        byte[] lineBytes = buffer.Slice(0, position.Value).ToArray();
                        string line = Encoding.UTF8.GetString(lineBytes).Trim();
                        try { ObjMgr.CallFunction(currentObj, "receive_message", LpcValue.Create(line)); } catch {}
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                }
                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) {
                    Console.WriteLine("⚠️ [Diag] PipeReader 收到 EOF，客戶端已斷開！");
                    break;
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"❌ [Network] TCP 讀取異常:\\n{ex}");
        } finally {
            Console.WriteLine($"❌ [Session] 斷開連線: {currentObj}");
            SessionManager.Unbind(currentObj);
            try { ObjMgr.CallFunction(currentObj, "logoff"); } catch {}
            client.Close();
        }
    }"""
    
    code = code[:start_idx] + new_method + code[end_idx:]
    with open('LithosNet.Host/Program.cs', 'w', encoding='utf-8') as f:
        f.write(code)
    print("✅ HandleClientAsync 已核彈級完美重寫！Busy Loop 與 logon() 缺失已徹底消滅！")
else:
    print("❌ 找不到 HandleClientAsync 方法！")
