using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LithosNet.VM
{
    public static class SessionManager
    {
        // === 原有功能 ===
        private static readonly ConcurrentDictionary<string, string> _sessions = new();
        private static readonly ConcurrentDictionary<string, string> _inputTraps = new();
        private static readonly ConcurrentDictionary<string, string> _bots = new();
        public static readonly AsyncLocal<string> CurrentPlayer = new AsyncLocal<string>();
        
        // === Phase 86 新增：LpcSession 管理 ===
        private static readonly ConcurrentDictionary<string, LpcSession> _lpcSessionsByObject = new();
        private static ObjectManager _objMgr;
        
        public static void Initialize(ObjectManager objMgr)
        {
            _objMgr = objMgr;
        }
        
        public static LpcSession CreateLpcSession(TcpClient client)
        {
            var session = new LpcSession(client, _objMgr);
            Console.WriteLine($"[SessionManager] 建立 LpcSession");
            return session;
        }
        
        public static void BindSessionToObject(LpcSession session, string objectName)
        {
            session.ObjectName = objectName;
            _lpcSessionsByObject[objectName] = session;
            
            // 【Phase 86 終極修復】先設定 session 屬性，再綁定到 interpreter
            session.ObjMgr.BindSessionToObject(objectName, session);
            
            Console.WriteLine($"[SessionManager] ✅ Session 綁定完成: {objectName}");
        }
        
        public static LpcSession GetSessionByObject(string objectName)
        {
            _lpcSessionsByObject.TryGetValue(objectName, out var session);
            return session;
        }
        
        public static void RemoveSession(LpcSession session) => RemoveLpcSession(session);
        
        public static void RemoveLpcSession(LpcSession session)
        {
            if (!string.IsNullOrEmpty(session.ObjectName)) {
                _lpcSessionsByObject.TryRemove(session.ObjectName, out _);
            }
        }

        public static bool HasSession(string objName) {
            return _lpcSessionsByObject.ContainsKey(objName);
        }

        public static void CloseSession(string objName) {
            if (_lpcSessionsByObject.TryRemove(objName, out var session)) {
                Console.WriteLine($"[SessionManager] 🛑 Closing socket for destructed interactive object: {objName}");
                session.Client?.Close();
            }
        }
        
        public static async Task StartAsync(int port)
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"🌐 [SessionManager] 監聽端口: {port}");
            
            while (true) {
                try {
                    var client = await listener.AcceptTcpClientAsync();
                    Console.WriteLine($"🔌 新連線: {client.Client.RemoteEndPoint}");
                    
                    var session = CreateLpcSession(client);
                    _ = Task.Run(() => session.StartReceiveLoopAsync());
                    
                    try {
                        var result = await _objMgr.EnqueueAndAwaitAsync(() => {
                            return _objMgr.CallFunction("master", "connect", new Core.LpcValue[] {
                                Core.LpcValue.Create(port)
                            });
                        });
                        
                        if (result.Type == Core.LpcType.Object || result.Type == Core.LpcType.String) {
                            string objName = result.AsString();
                            BindSessionToObject(session, objName);
                            Console.WriteLine($"✅ master->connect() 返回: {objName}，已綁定 session");
                            
                            try {
                                await _objMgr.EnqueueAndAwaitAsync(() => {
                                    return _objMgr.CallFunction(objName, "logon", new Core.LpcValue[0]);
                                });
                            } catch (Exception ex) {
                                Console.WriteLine($"⚠ logon() 錯誤: {ex.Message}");
                            }
                        }
                    } catch (Exception ex) {
                        Console.WriteLine($"⚠ master->connect() 錯誤: {ex.Message}");
                        session.Close();
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"[SessionManager] Accept error: {ex.Message}");
                    break;
                }
            }
        }
        
        // === 原有方法 ===
        public static void SetInputTrap(string sessionObj, string funcName) {
            _inputTraps[sessionObj] = funcName;
        }
        
        public static void RegisterBot(string objName) {
            _bots[objName] = objName;
        }
        
        public static async Task SendAsync(string objName, string message) {
            // Phase 86: 優先使用 LpcSession
            if (_lpcSessionsByObject.TryGetValue(objName, out var lpcSession)) {
                lpcSession.Write(message);
                return;
            }
            // 降級到原有邏輯
            if (_sessions.TryGetValue(objName, out var connId)) {
                Console.WriteLine($"[SendAsync] {objName}: {message}");
            }
            await Task.CompletedTask;
        }
        
        public static List<string> GetAllSessions() => _sessions.Keys.ToList();
        
        public static void Exec(string newObj, string oldObj) {
            if (_sessions.TryRemove(oldObj, out var connId)) {
                _sessions[newObj] = connId;
            }
        }

        // 【Phase 87】TransferSession: 轉移網路連線控制權
        public static void TransferSession(string oldObjName, string newObjName) {
            if (_lpcSessionsByObject.TryGetValue(oldObjName, out var session)) {
                _lpcSessionsByObject.TryRemove(oldObjName, out _);
                session.ObjectName = newObjName;
                _lpcSessionsByObject[newObjName] = session;
                Console.WriteLine($"🔄 [SessionManager] Session 控制權已從 '{oldObjName}' 轉移至 '{newObjName}'");
            } else {
                Console.WriteLine($"⚠️ [SessionManager] 找不到舊物件 '{oldObjName}' 的 Session，無法轉移。");
            }
        }
    }
}
