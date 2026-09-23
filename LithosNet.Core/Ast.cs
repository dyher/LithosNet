#nullable disable
using System;
using System.Collections.Generic;

namespace LithosNet.Core {
    public abstract class AstNode { }
    public class VariableDeclarationNode : AstNode { public string TypeName; public string VariableName; public AstNode Initializer; }
    public class AssignmentNode : AstNode { public string VariableName; public AstNode Value; }
    public class LiteralNode : AstNode { public LpcValue Value; }
    public class FunctionDeclarationNode : AstNode { public string ReturnType; public string Name; public List<ParameterNode> Parameters = new(); public List<AstNode> Body = new(); }
    public class ParameterNode : AstNode { public string TypeName; public string Name; }
    public class ReturnNode : AstNode { public AstNode Value; }
    public class FunctionCallNode : AstNode { public string Name; public List<AstNode> Arguments = new(); }
    public class VariableRefNode : AstNode { public string Name; }
    public class BinaryOpNode : AstNode { public AstNode Left; public string Op; public AstNode Right; }
    public class IfNode : AstNode { public AstNode Condition; public AstNode ThenBranch; public AstNode ElseBranch; }
    public class BlockNode : AstNode { public List<AstNode> Statements = new(); }
    public class WhileNode : AstNode { public AstNode Condition; public AstNode Body; }
    public class ForNode : AstNode { public AstNode Init; public AstNode Condition; public AstNode Step; public AstNode Body; }
    public class ArrayLiteralNode : AstNode { public List<AstNode> Elements = new(); }
    public class IndexAccessNode : AstNode { public AstNode Array; public AstNode Index; }
    public class IndexAssignmentNode : AstNode { public AstNode Array; public AstNode Index; public AstNode Value; }
    public class MappingLiteralNode : AstNode { public List<AstNode> Keys = new(); public List<AstNode> Values = new(); }
    public class CallOtherNode : AstNode { public AstNode Target; public string FuncName; public List<AstNode> Arguments = new(); }
    
    // 【新增】繼承節點: inherit "base";
    public class InheritNode : AstNode { public string ParentObjName; }
}
