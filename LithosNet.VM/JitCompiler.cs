using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using LithosNet.Core;

namespace LithosNet.VM {
    // 將 LPC 的 AST 編譯成 .NET 原生 Delegate (Func<int, int, int>)
    public static class JitCompiler {
        public static Delegate Compile(FunctionDeclarationNode func) {
            if (func.Parameters.Count != 2) throw new Exception("JIT 目前僅支援 2 個 int 參數的純數學函數");
            
            var p1 = Expression.Parameter(typeof(int), func.Parameters[0].Name);
            var p2 = Expression.Parameter(typeof(int), func.Parameters[1].Name);
            
            // 假設函數體只有一個 return 語句
            var returnNode = func.Body[0] as ReturnNode;
            if (returnNode == null) throw new Exception("JIT 僅支援包含單一 return 的函數");
            
            var bodyExpr = BuildExpression(returnNode.Value, p1, p2);
            
            // 🔥 核心魔法：將 Expression Tree 編譯為原生 .NET Delegate！
            var lambda = Expression.Lambda<Func<int, int, int>>(bodyExpr, p1, p2);
            return lambda.Compile(); 
        }

        private static Expression BuildExpression(AstNode node, ParameterExpression p1, ParameterExpression p2) {
            if (node is LiteralNode lit) return Expression.Constant(lit.Value.AsInt());
            if (node is VariableRefNode vref) {
                if (vref.Name == p1.Name) return p1;
                if (vref.Name == p2.Name) return p2;
            }
            if (node is BinaryOpNode bin) {
                var left = BuildExpression(bin.Left, p1, p2);
                var right = BuildExpression(bin.Right, p1, p2);
                return bin.Op switch {
                    "+" => Expression.Add(left, right),
                    "-" => Expression.Subtract(left, right),
                    "*" => Expression.Multiply(left, right),
                    "/" => Expression.Divide(left, right),
                    _ => throw new Exception($"JIT 不支援運算符: {bin.Op}")
                };
            }
            throw new Exception($"JIT 不支援的節點類型: {node.GetType().Name}");
        }
    }
}
