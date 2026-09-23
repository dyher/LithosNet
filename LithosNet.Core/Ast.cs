#nullable disable
using System;
using System.Collections.Generic;

namespace LithosNet.Core {
    public abstract class AstNode { }

    // 變數宣告: int x = 10;
    public class VariableDeclarationNode : AstNode {
        public string TypeName { get; set; }
        public string VariableName { get; set; }
        public AstNode Initializer { get; set; }
    }

    // 字面量: 999, "hello"
    public class LiteralNode : AstNode {
        public LpcValue Value { get; set; }
    }

    // 【新增】函數宣告: void logon() { ... }
    public class FunctionDeclarationNode : AstNode {
        public string ReturnType { get; set; }
        public string Name { get; set; }
        public List<ParameterNode> Parameters { get; set; } = new();
        public List<AstNode> Body { get; set; } = new();
    }

    // 【新增】函數參數: string user
    public class ParameterNode : AstNode {
        public string TypeName { get; set; }
        public string Name { get; set; }
    }

    // 【新增】return 語句: return 1;
    public class ReturnNode : AstNode {
        public AstNode Value { get; set; }
    }

    // 【新增】函數呼叫: verify_login("admin")
    public class FunctionCallNode : AstNode {
        public string Name { get; set; }
        public List<AstNode> Arguments { get; set; } = new();
    }

    // 【新增】變數讀取
    public class VariableRefNode : AstNode {
        public string Name { get; set; }
    }
}
