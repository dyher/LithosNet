using System;
using System.IO;
using System.Collections.Generic;
using LithosNet.Core;
using LithosNet.Compiler;

namespace LithosNet.VM {
    public class ObjectManager {
        private readonly Dictionary<string, (Scope scope, Interpreter interp)> _objects = new();

        public Scope LoadObject(string path) {
            if (_objects.ContainsKey(path)) return _objects[path].scope;

            Console.WriteLine($"📂 [VM] 正在編譯 LPC 物件: {Path.GetFileName(path)}");
            string src = File.ReadAllText(path);

            var tokens = new Lexer(src).Tokenize();
            var ast = new Parser(tokens).Parse();

            var scope = new Scope();
            var interp = new Interpreter(scope);
            interp.Execute(ast);

            _objects[path] = (scope, interp);
            Console.WriteLine($"✅ [VM] 物件 {Path.GetFileName(path)} 就緒！\n");
            return scope;
        }

        // 【核心 API】呼叫 LPC 物件中的函數
        public LpcValue CallFunction(string path, string funcName, params LpcValue[] args) {
            LoadObject(path); // 確保已載入
            return _objects[path].interp.CallFunction(funcName, new List<LpcValue>(args));
        }
    }
}
