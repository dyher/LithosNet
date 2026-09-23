#nullable disable
using System;
using System.Collections.Generic;

namespace LithosNet.Core {
    public abstract class AstNode { }
    public class VariableDeclarationNode : AstNode { public string TypeName; public string VariableName; public AstNode Initializer; }
    public class LiteralNode : AstNode { public LpcValue Value; }
    public class FunctionDeclarationNode : AstNode { public string ReturnType; public string Name; public List<ParameterNode> Parameters = new(); public List<AstNode> Body = new(); }
    public class ParameterNode : AstNode { public string TypeName; public string Name; }
    public class ReturnNode : AstNode { public AstNode Value; }
    public class FunctionCallNode : AstNode { public string Name; public List<AstNode> Arguments = new(); }
    public class VariableRefNode : AstNode { public string Name; }

    // 【新增】二元運算節點 (例如: user == "admin")
    public class BinaryOpNode : AstNode { public AstNode Left; public string Op; public AstNode Right; }
    
    // 【新增】If 條件節點
    public class IfNode : AstNode { public AstNode Condition; public AstNode ThenBranch; public AstNode ElseBranch; }
    
    // 【新增】程式碼區塊節點 { ... }
    public class BlockNode : AstNode { public List<AstNode> Statements = new(); }
}
