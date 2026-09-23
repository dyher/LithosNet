#nullable disable
using System;

namespace LithosNet.Core {
    // LPC 基礎類型列舉
    public enum LpcType : byte {
        Int = 0,
        String = 1,
        Array = 2,
        Object = 3,
        Mapping = 4,
        Float = 5,
        Buffer = 6,
        Function = 7
    }

    // 【現代 C# 魔法】使用 readonly struct 實現零 GC 壓力的 LPC 變數
    // 淘汰 LPSharp 緩慢的 object 裝箱！
    public readonly struct LpcValue {
        public readonly LpcType Type;
        
        // 用於 Int 和 Float (避免裝箱)
        private readonly long _primitiveValue;
        
        // 用於 String, Array, Mapping, Object (參考類型)
        private readonly object _referenceValue;

        private LpcValue(LpcType type, long primitive, object reference) {
            Type = type;
            _primitiveValue = primitive;
            _referenceValue = reference;
        }

        // 工廠方法：快速建立各種類型
        public static LpcValue Create(int value) => new LpcValue(LpcType.Int, value, null);
        public static LpcValue Create(string value) => new LpcValue(LpcType.String, 0, value);
        
        // 取得值 (帶型別檢查)
        public int AsInt() {
            if (Type != LpcType.Int) throw new InvalidCastException($"Cannot cast {Type} to Int");
            return (int)_primitiveValue;
        }
        
        public string AsString() {
            if (Type != LpcType.String) throw new InvalidCastException($"Cannot cast {Type} to String");
            return (string)_referenceValue;
        }

        public override string ToString() {
            return Type switch {
                LpcType.Int => _primitiveValue.ToString(),
                LpcType.String => $"\"{_referenceValue}\"",
                _ => $"<{Type}>"
            };
        }
    }
}
