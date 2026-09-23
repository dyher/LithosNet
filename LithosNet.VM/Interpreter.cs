#nullable disable
using System;
using System.Collections.Generic;
using LithosNet.Core;

namespace LithosNet.VM {
    public class ReturnSignal : Exception { public LpcValue Value; public ReturnSignal(LpcValue v) { Value = v; } }

    public class Interpreter {
        private readonly Scope _scope;
        public Interpreter(Scope scope) { _scope = scope; }

        public void Execute(List<AstNode> ast) { foreach (var n in ast) Visit(n); }

        public LpcValue CallFunction(string name, List<LpcValue> args) {
            if (_scope.HasFunction(name)) {
                var func = _scope.GetFunction(name);
                Console.WriteLine($"   ⚡ [VM] 呼叫 LPC 函數: {name}()");
                for (int i = 0; i < func.Parameters.Count && i < args.Count; i++) _scope.Set(func.Parameters[i].Name, args[i]);
                try { foreach (var s in func.Body) Visit(s); } catch (ReturnSignal r) { return r.Value; }
                return LpcValue.Create(0);
            }
            if (EfunRegistry.TryGet(name, out var efun)) return efun(args.ToArray());
            throw new Exception($"[VM] Function '{name}' not found.");
        }

        private void Visit(AstNode node) {
            switch (node) {
                case VariableDeclarationNode v: _scope.Set(v.VariableName, v.Initializer != null ? Eval(v.Initializer) : LpcValue.Create(0)); break;
                case FunctionDeclarationNode f: _scope.RegisterFunction(f); break;
                case ReturnNode r: throw new ReturnSignal(r.Value != null ? Eval(r.Value) : LpcValue.Create(0));
                case IfNode i: VisitIf(i); break;
                case BlockNode b: foreach (var s in b.Statements) Visit(s); break;
                case FunctionCallNode c: EvalCall(c); break;
            }
        }

        // 【新增】處理 if/else 邏輯
        private void VisitIf(IfNode node) {
            bool condResult = EvalBool(node.Condition);
            if (condResult) Visit(node.ThenBranch);
            else if (node.ElseBranch != null) Visit(node.ElseBranch);
        }

        private bool EvalBool(AstNode node) {
            var val = Eval(node);
            if (val.Type == LpcType.Int) return val.AsInt() != 0;
            return false;
        }

        private LpcValue Eval(AstNode node) {
            switch (node) {
                case LiteralNode l: return l.Value;
                case VariableRefNode v: return _scope.Get(v.Name);
                case FunctionCallNode c: return EvalCall(c);
                // 【新增】處理二元運算 (==, !=)
                case BinaryOpNode b:
                    var left = Eval(b.Left);
                    var right = Eval(b.Right);
                    bool res = false;
                    if (left.Type == LpcType.String && right.Type == LpcType.String)
                        res = b.Op == "==" ? left.AsString() == right.AsString() : left.AsString() != right.AsString();
                    else if (left.Type == LpcType.Int && right.Type == LpcType.Int)
                        res = b.Op == "==" ? left.AsInt() == right.AsInt() : left.AsInt() != right.AsInt();
                    return LpcValue.Create(res ? 1 : 0);
                default: return LpcValue.Create(0);
            }
        }

        private LpcValue EvalCall(FunctionCallNode node) {
            var args = new List<LpcValue>();
            foreach (var a in node.Arguments) args.Add(Eval(a));
            return CallFunction(node.Name, args);
        }
    }
}
