using System;

namespace LithosNet.Core {
    // 只要加上這個標籤，C# 方法就會自動註冊為 LPC 的 Efun！
    [AttributeUsage(AttributeTargets.Method)]
    public class EfunAttribute : Attribute {
        public string Name { get; }
        public EfunAttribute(string name) { Name = name; }
    }
}
