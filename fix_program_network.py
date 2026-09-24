import re

with open("LithosNet.Host/Program.cs", "r", encoding="utf-8") as f:
    prog = f.read()

new_method = """        static async Task HandleClientAsync(TcpClient client) {
            var reader = PipeReader.Create(client.GetStream());
            var writer = PipeWriter.Create(client.GetStream());
            
            string currentObj = "";
            try {
                currentObj = ObjMgr.CallFunction(MasterObj, "connect").AsString();
            } catch (Exception e) {
                Console.WriteLine($"❌ master->connect() 失敗: {e.Message}");
                client.Close(); return;
            }

            SessionManager.Bind(currentObj, writer);
            SessionManager.CurrentPlayer.Value = currentObj;
            
            try { ObjMgr.CallFunction(currentObj, "logon"); } catch {}

            try {
                while (true) {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    while (true) {
                        if (buffer.Length == 0) break;
                        byte firstByte = buffer.FirstSpan[0];
                        
                        // 【Binary Protocol】0xFF + 4 bytes Length + Payload
                        if (firstByte == 0xFF) {
                            if (buffer.Length < 5) break;
                            var lenSpan = buffer.Slice(1, 4).FirstSpan;
                            int length = (lenSpan[0] << 24) | (lenSpan[1] << 16) | (lenSpan[2] << 8) | lenSpan[3];
                            if (buffer.Length < 5 + length) break;
                            
                            var payloadBytes = buffer.Slice(5, length);
                            string json = Encoding.UTF8.GetString(payloadBytes);
                            currentObj = SessionManager.GetObjName(writer);
                            if (!string.IsNullOrEmpty(currentObj)) {
                                try { ObjMgr.CallFunction(currentObj, "receive_binary", LpcValue.Create(json)); } catch {}
                            }
                            buffer = buffer.Slice(5 + length);
                        } 
                        // 【Text Protocol】按 \\n 分割
                        else {
                            SequencePosition? position = buffer.PositionOf((byte)'\\n');
                            if (position == null) break;
                            var lineBytes = buffer.Slice(0, position.Value);
                            string line = Encoding.UTF8.GetString(lineBytes).Trim();
                            currentObj = SessionManager.GetObjName(writer);
                            if (!string.IsNullOrEmpty(currentObj)) {
                                try { ObjMgr.CallFunction(currentObj, "receive_message", LpcValue.Create(line)); } catch {}
                            }
                            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                        }
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
        }"""

# 精準替換整個 HandleClientAsync 方法 (從方法簽名到最後一個縮排為 8 個空格的 })
prog = re.sub(r'        static async Task HandleClientAsync\(TcpClient client\) \{.*?\n        \}', new_method, prog, flags=re.DOTALL)

with open("LithosNet.Host/Program.cs", "w", encoding="utf-8") as f:
    f.write(prog)
print("✅ Program.cs 已完美升級為雙軌制網路層 (Text + Binary)！")
