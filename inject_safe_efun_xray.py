with open('LithosNet.VM/BuiltInEfuns.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# keys() X-Ray
if '[Efun X-Ray] keys' not in code:
    code = code.replace(
        'public static LpcValue Keys(LpcValue[] args) {',
        'public static LpcValue Keys(LpcValue[] args) {\n            Console.WriteLine($"🔍 [Efun X-Ray] keys() arg count: {args.Length}, arg0 Type: {(args.Length > 0 ? args[0].Type.ToString() : "NONE")}");'
    )

# implode() X-Ray
if '[Efun X-Ray] implode' not in code:
    code = code.replace(
        'public static LpcValue Implode(LpcValue[] args) {',
        'public static LpcValue Implode(LpcValue[] args) {\n            Console.WriteLine($"🔍 [Efun X-Ray] implode() arg count: {args.Length}, arg0 Type: {(args.Length > 0 ? args[0].Type.ToString() : "NONE")}");'
    )

# element_of() X-Ray
if '[Efun X-Ray] element_of' not in code:
    code = code.replace(
        'public static LpcValue ElementOf(LpcValue[] args) {',
        'public static LpcValue ElementOf(LpcValue[] args) {\n            Console.WriteLine($"🔍 [Efun X-Ray] element_of() arg0 Type: {(args.Length > 0 ? args[0].Type.ToString() : "NONE")}");'
    )

with open('LithosNet.VM/BuiltInEfuns.cs', 'w', encoding='utf-8') as f:
    f.write(code)
print("✅ 已注入絕對安全的 Efun X-Ray (只使用 .Type)！")
