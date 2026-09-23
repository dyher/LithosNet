#nullable disable
using System;
using System.Collections.Generic;

namespace LithosNet.Core {
    public enum LpcType : byte {
        Int = 0, String = 1, Array = 2, Object = 3, Mapping = 4, Float = 5, Buffer = 6, Function = 7
    }

    public readonly struct LpcValue {
        public readonly LpcType Type;
        private readonly long _primitiveValue;
        private readonly object _referenceValue;

        private LpcValue(LpcType type, long primitive, object reference) {
            Type = type; _primitiveValue = primitive; _referenceValue = reference;
        }

        public static LpcValue Create(int value) => new LpcValue(LpcType.Int, value, null);
        public static LpcValue Create(string value) => new LpcValue(LpcType.String, 0, value);
        public static LpcValue Create(List<LpcValue> value) => new LpcValue(LpcType.Array, 0, value);
        // 【新增】建立 Mapping 類型
        public static LpcValue Create(Dictionary<string, LpcValue> value) => new LpcValue(LpcType.Mapping, 0, value);
        
        public int AsInt() => (Type == LpcType.Int) ? (int)_primitiveValue : throw new InvalidCastException();
        public string AsString() => (Type == LpcType.String) ? (string)_referenceValue : throw new InvalidCastException();
        public List<LpcValue> AsArray() => (Type == LpcType.Array) ? (List<LpcValue>)_referenceValue : throw new InvalidCastException();
        // 【新增】取得 Mapping
        public Dictionary<string, LpcValue> AsMapping() => (Type == LpcType.Mapping) ? (Dictionary<string, LpcValue>)_referenceValue : throw new InvalidCastException();

        public override string ToString() {
            return Type switch {
                LpcType.Int => _primitiveValue.ToString(),
                LpcType.String => $"\"{_referenceValue}\"",
                LpcType.Array => $"<Array[{((List<LpcValue>)_referenceValue).Count}]>",
                LpcType.Mapping => $"<Mapping[{((Dictionary<string, LpcValue>)_referenceValue).Count}]>",
                _ => $"<{Type}>"
            };
        }
    }
}
