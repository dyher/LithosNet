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
        public static LpcValue Create(Dictionary<string, LpcValue> value) => new LpcValue(LpcType.Mapping, 0, value);
        
        // 【新增】建立函數指標 (儲存 物件名稱 與 函數名稱)
        public static LpcValue CreateObject(string objName) => new LpcValue(LpcType.Object, 0, objName);
        
        public static LpcValue CreateFunction(string objName, string funcName) => 
            new LpcValue(LpcType.Function, 0, new Tuple<string, string>(objName, funcName));
        
        public int AsInt() {
            if (Type == LpcType.Int) return (int)_primitiveValue;
            if (Type == LpcType.String && _referenceValue != null && int.TryParse(_referenceValue.ToString(), out int res)) return res;
            return 0; // 寬容 Fallback
        }
        public string AsString() {
            if (Type == LpcType.String) return _referenceValue?.ToString() ?? "";
            if (Type == LpcType.Int) return _primitiveValue.ToString();
            if (Type == LpcType.Object || Type == LpcType.Array || Type == LpcType.Mapping) return _referenceValue?.ToString() ?? "";
            return ""; // 寬容 Fallback
        }
        public List<LpcValue> AsArray() => (Type == LpcType.Array) ? (List<LpcValue>)_referenceValue : throw new InvalidCastException();
        public Dictionary<string, LpcValue> AsMapping() => (Type == LpcType.Mapping) ? (Dictionary<string, LpcValue>)_referenceValue : throw new InvalidCastException();
        public Tuple<string, string> AsFunction() => (Type == LpcType.Function) ? (Tuple<string, string>)_referenceValue : throw new InvalidCastException();

        public override string ToString() {
            return Type switch {
                LpcType.Int => _primitiveValue.ToString(),
                LpcType.String => $"\"{_referenceValue}\"",
                LpcType.Array => $"<Array[{((List<LpcValue>)_referenceValue).Count}]>",
                LpcType.Mapping => $"<Mapping[{((Dictionary<string, LpcValue>)_referenceValue).Count}]>",
                LpcType.Function => $"<Function[{((Tuple<string, string>)_referenceValue).Item2}]>",
                _ => $"<{Type}>"
            };
        }
    }
}
